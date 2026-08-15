using System;
using System.Collections.Generic;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
#if DEBUG
using System.Diagnostics;
#endif

namespace AdvancedStashSorting.Sorting;

public static class StashItemSorter
{
    internal static bool CriteriaApplicationActive { get; private set; }
    internal static SortConfiguration ActiveSortConfiguration { get; private set; }

    internal static OperationResult<ApplySortItemsPositionResult> Sort(CompoundItem sortedItem,
        InventoryController controller, SortConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        Grid grid = sortedItem.Grids[0];

        foreach (Item item in grid.Items)
            if (!grid.CanAccept(item))
                return new AutomaticSortNonFilteredItemError(sortedItem, item);

        if (!controller.IsAllowedToSort(sortedItem)) return new CannotSortItemError(sortedItem);

        List<Item> items = GetSortableItems(grid);
        List<ContainerRemoveResult> removeResults = new List<ContainerRemoveResult>(items.Count);
        List<GridAddResult> addResults = new List<GridAddResult>(items.Count);

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                OperationResult<ContainerRemoveResult> removeResult = item.Parent.Remove(item, false);

                if (removeResult.Failed)
                {
                    RollBackRemoveResults(removeResults);
                    return removeResult.Error;
                }

                removeResults.Add(removeResult.Value);
            }

            ConfiguredSortPlan sortPlan = BuildConfiguredSortPlan(items, configuration);

#if DEBUG
            Stopwatch stopwatch = Stopwatch.StartNew();
#endif
            OrderedLayoutSearchResult searchResult = OrderedStashLayoutPlanner.Search(
                grid,
                sortPlan.Items,
                sortPlan.PrimaryBatchStarts,
                configuration.SeparatePrimaryGroups);

#if DEBUG
            stopwatch.Stop();
            if (searchResult.SearchPerformed)
                LogSearch(searchResult, items.Count, stopwatch.Elapsed.TotalMilliseconds);
#endif
            if (searchResult.Plan == null)
            {
                RollBackRemoveResults(removeResults);
                return new AutomaticSortFailedError(sortedItem);
            }

            for (int i = 0; i < searchResult.Plan.Placements.Count; i++)
            {
                OrderedPlannedPlacement placement = searchResult.Plan.Placements[i];
                OperationResult<GridAddResult> addResult = grid.Add(placement.Item, placement.Location, false);

                if (addResult.Failed)
                {
                    RollBackAddResults(addResults);
                    RollBackRemoveResults(removeResults);
                    return addResult.Error;
                }

                addResults.Add(addResult.Value);
            }

            RollBackAddResults(addResults);
            RollBackRemoveResults(removeResults);

            return new ApplySortItemsPositionResult(sortedItem, removeResults, addResults, controller);
        }
        catch
        {
            RollBackAddResults(addResults);
            RollBackRemoveResults(removeResults);
            throw;
        }
    }

    private static List<Item> GetSortableItems(Grid grid)
    {
        List<Item> items = [];

        foreach (Item item in grid.Items)
            if (item.PinLockState == EItemPinLockState.Free)
                items.Add(item);

        return items;
    }

    internal static OperationResult<ApplySortItemsPositionResult> SortVanilla(CompoundItem sortedItem,
        InventoryController controller, SortConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        bool previous = CriteriaApplicationActive;
        SortConfiguration previousConfiguration = ActiveSortConfiguration;
        CriteriaApplicationActive = true;
        ActiveSortConfiguration = configuration;

        try
        {
            return ItemManipulator.Sort(sortedItem, controller, true);
        }
        finally
        {
            CriteriaApplicationActive = previous;
            ActiveSortConfiguration = previousConfiguration;
        }
    }

    internal static List<Item> ApplyConfiguredSort(List<Item> items, SortConfiguration configuration)
    {
        if (items == null) return null;
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        return BuildConfiguredSortPlan(items, configuration).Items;
    }

    private static ConfiguredSortPlan BuildConfiguredSortPlan(List<Item> items, SortConfiguration configuration)
    {
        if (items.Count == 0) return new ConfiguredSortPlan(items, []);

        int itemCount = items.Count;
        Item[] source = new Item[itemCount];

        for (int index = 0; index < itemCount; index++) source[index] = items[index];

        long[][] keyArrays = new long[configuration.Criteria.Count][];
        bool[] ascending = new bool[configuration.Criteria.Count];
        string[] templateIds = new string[itemCount];
        string[] primaryCategoryKeys = configuration.Criteria[0].Type == SortType.Category
            ? new string[itemCount]
            : null;

        for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
        {
            templateIds[itemIndex] = source[itemIndex].TemplateId;
            if (primaryCategoryKeys != null)
                primaryCategoryKeys[itemIndex] = ItemClassifier.Classify(source[itemIndex]);
        }

        for (int criterionIndex = 0; criterionIndex < configuration.Criteria.Count; criterionIndex++)
        {
            SortCriterion criterion = configuration.Criteria[criterionIndex];
            long[] keys = new long[itemCount];

            for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
                keys[itemIndex] = configuration.ComputeKey(source[itemIndex], criterion.Type);

            keyArrays[criterionIndex] = keys;
            ascending[criterionIndex] = criterion.Direction == SortDirection.Ascending;
        }

        ConfiguredOrderResult orderResult = ConfiguredOrderEngine.Build(
            keyArrays,
            ascending,
            templateIds,
            primaryCategoryKeys);
        List<Item> sorted = new(itemCount);

        for (int index = 0; index < itemCount; index++) sorted.Add(source[orderResult.Order[index]]);

        return new ConfiguredSortPlan(sorted, [..orderResult.PrimaryBatchStarts]);
    }

#if DEBUG
    private static void LogSearch(OrderedLayoutSearchResult result, int itemCount, double elapsedMilliseconds)
    {
        string layout = result.Plan == null
            ? $"failed={result.Status}"
            : $"rows={result.UsedBottom}, placedRows={result.PlacedBottom}, perimeter={result.Perimeter}, horizontal={result.HorizontalWeight}";
        Plugin.LogSource?.LogInfo(
            $"Inventory layout simulation: items={itemCount}, {layout}, time={elapsedMilliseconds:F2}ms.");
    }
#endif

    private static void RollBackAddResults(List<GridAddResult> results)
    {
        for (int i = results.Count - 1; i >= 0; i--) results[i].RollBack();
    }

    private static void RollBackRemoveResults(List<ContainerRemoveResult> results)
    {
        for (int i = results.Count - 1; i >= 0; i--) results[i].RollBack();
    }

    private sealed class ConfiguredSortPlan(List<Item> items, List<int> primaryBatchStarts)
    {
        public List<Item> Items { get; } = items;
        public List<int> PrimaryBatchStarts { get; } = primaryBatchStarts;
    }
}
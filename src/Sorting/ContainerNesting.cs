using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;

namespace AdvancedStashSorting.Sorting;

public static class ContainerNesting
{
    public static async Task<Error> MoveItems(CompoundItem sortedItem, InventoryController inventoryController,
        List<IOperationResult> stagedOperations, bool runNetworkTransactions)
    {
        List<NestingTarget> targets = GetTargets(sortedItem);

        if (targets.Count == 0) return null;

        List<Item> items = GetTopLevelItems(sortedItem);
        Dictionary<string, int> categoryIndex = SortKeyProvider.BuildCategoryIndex();
        items = items.OrderBy(item =>
            categoryIndex.TryGetValue(ItemClassifier.Classify(item), out int index)
                ? index
                : categoryIndex.Count).ToList();
        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            Item item = items[itemIndex];

            if (item.PinLockState != EItemPinLockState.Free || item is SimpleContainer) continue;

            string category = ItemClassifier.Classify(item);

            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                NestingTarget target = targets[targetIndex];

                if (target.Item == item || !ContainerCategorySettings.IsAllowed(target.Item, category)) continue;

                Grid grid = target.Grid;
                GridItemAddress address = grid.FindLocationForItem(item);

                if (address == null) continue;

                OperationResult<MoveResult> operation = ItemManipulator.Move(item, address, inventoryController, runNetworkTransactions);

                if (operation.Failed) continue;

                if (!runNetworkTransactions)
                {
                    stagedOperations.Add(operation.Value);
                }
                else
                {
                    IResult result = await inventoryController.TryRunNetworkTransaction(operation);

                    if (result.Failed) return new StringError(result.Error);
                }

                break;
            }
        }

        return null;
    }

    public static bool CanConfigureCategories(CompoundItem container)
    {
        if (container == null || container.Grids.Length == 0 || !IsInStash(container)) return false;
        if (container.GetItemComponent<TagComponent>() == null) return false;

        return container.ItemInteractionButtons?.Contains(EItemInfoButton.Tag) == true;
    }

    public static bool IsInStash(Item item)
    {
        if (item == null) return false;

        foreach (Item parent in item.GetAllParentItemsAndSelf())
            if (parent is Stash)
                return true;

        return false;
    }

    private static List<NestingTarget> GetTargets(CompoundItem sortedItem)
    {
        List<NestingTarget> targets = [];

        foreach (Grid rootGrid in sortedItem.Grids)
        foreach (Item item in rootGrid.Items)
        {
            if (item is not CompoundItem container || !CanUseAsTarget(container) || IsFolded(container)) continue;

            for (int gridIndex = 0; gridIndex < container.Grids.Length; gridIndex++)
            {
                Grid grid = container.Grids[gridIndex];
                targets.Add(new NestingTarget(container, grid));
            }
        }

        targets.Sort(CompareTargets);

        return targets;
    }

    private static bool CanUseAsTarget(CompoundItem container)
    {
        return CanConfigureCategories(container) && ContainerCategorySettings.HasSelection(container.Id);
    }
    
    private static bool IsFolded(Item item)
    {
        return item.TryGetItemComponent(out FoldableComponent foldable) && foldable.Folded;
    }

    private static int CompareTargets(NestingTarget left, NestingTarget right)
    {
        int restrictionComparison = left.RestrictionCount.CompareTo(right.RestrictionCount);

        if (restrictionComparison != 0) return restrictionComparison;

        return left.Capacity.CompareTo(right.Capacity);
    }

    private static int GetRestrictionCount(Grid grid)
    {
        if (grid.Filters == null) return 0;

        int count = 0;

        for (int filterIndex = 0; filterIndex < grid.Filters.Length; filterIndex++)
        {
            ItemFilter filter = grid.Filters[filterIndex];

            if (filter == null) continue;

            count += filter.Filter?.Length ?? 0;
            count += filter.ExcludedFilter?.Length ?? 0;
        }

        return count;
    }

    private static List<Item> GetTopLevelItems(CompoundItem sortedItem)
    {
        List<Item> items = [];

        foreach (Grid grid in sortedItem.Grids)
        foreach (Item item in grid.Items)
            items.Add(item);

        return items;
    }

    private sealed class NestingTarget(CompoundItem item, Grid grid)
    {
        public CompoundItem Item { get; } = item;
        public Grid Grid { get; } = grid;
        public int RestrictionCount { get; } = GetRestrictionCount(grid);
        public int Capacity { get; } = grid.GridWidth * grid.GridHeight;
    }
}
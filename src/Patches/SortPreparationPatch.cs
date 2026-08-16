using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AdvancedStashSorting.Sorting;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace AdvancedStashSorting.Patches;

public class SortPreparationPatch : ModulePatch
{
    internal static bool HandledSort;

    private static readonly AccessTools.FieldRef<SimpleStashPanel, InventorySelectableItemContext> StashItemContext =
        AccessTools.FieldRefAccess<SimpleStashPanel, InventorySelectableItemContext>("_itemContext");

    private static readonly AccessTools.FieldRef<GridWindow, ItemContext> GridWindowItemContext =
        AccessTools.FieldRefAccess<GridWindow, ItemContext>("_itemContext");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(GridSortPanel), nameof(GridSortPanel.Sort));
    }

    [PatchPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(GridSortPanel __instance, CompoundItem ____item, InventoryController ____controller)
    {
        HandledSort = false;

        if (!IsInventorySortPanel(__instance, ____item))
            return true;

        bool foldingEnabled = SortSettings.FoldingEnabled;
        bool stackingEnabled = SortSettings.StackingEnabled;
        bool nestingEnabled = SortSettings.NestingEnabled;
        bool hasCriteria = SortSettings.HasEnabledCriterion();

        if (SortInterceptionPolicy.ShouldRunOriginal(
                hasCriteria,
                foldingEnabled,
                stackingEnabled,
                nestingEnabled,
                out HandledSort)) return true;

        SortConfiguration sortConfiguration = hasCriteria
            ? SortConfiguration.Capture()
            : SortConfiguration.Empty;
        PreparationSettings settings = new(
            foldingEnabled,
            stackingEnabled,
            nestingEnabled,
            SortSettings.RecursiveNestingEnabled,
            sortConfiguration);
        _ = Sort(__instance, ____item, ____controller, settings);

        return false;
    }

    internal static bool IsInventorySortPanel(GridSortPanel panel, CompoundItem compoundItem)
    {
        if (panel == null || compoundItem == null) return false;

        ItemContext itemContext = null;
        SimpleStashPanel stashPanel = panel.GetComponentInParent<SimpleStashPanel>();

        if (stashPanel != null)
        {
            itemContext = StashItemContext(stashPanel);
        }
        else
        {
            GridWindow gridWindow = panel.GetComponentInParent<GridWindow>();
            if (gridWindow != null) itemContext = GridWindowItemContext(gridWindow);
        }

        if (itemContext == null ||
            itemContext.ViewType is not EItemViewType.Inventory and not EItemViewType.InventoryDuringMatching)
            return false;

        return compoundItem is Stash || ContainerNesting.IsInStash(compoundItem);
    }

    private static async Task Sort(GridSortPanel panel, CompoundItem compoundItem,
        InventoryController inventoryController, PreparationSettings settings)
    {
        bool progressStarted = false;

        try
        {
            panel.ChangeProgress(true);
            progressStarted = true;

            Error error = settings.HasPreparation
                ? await ValidatePreparation(compoundItem, inventoryController, settings)
                : null;

            if (error == null && settings.FoldingEnabled)
                error = await FoldItems(compoundItem, inventoryController, null, true);

            if (error == null && settings.StackingEnabled)
                error = await StackItems(compoundItem, inventoryController, null, true);

            if (error == null && settings.NestingEnabled)
                error = await ContainerNesting.MoveItems(compoundItem, inventoryController, null, true,
                    settings.RecursiveNestingEnabled);

            if (error == null && settings.SortConfiguration.HasCriteria)
            {
                OperationResult<ApplySortItemsPositionResult> operation =
                    BuildSortOperation(compoundItem, inventoryController, settings.SortConfiguration);

                if (operation.Failed)
                {
                    error = operation.Error;
                }
                else
                {
                    IResult result = await inventoryController.TryRunNetworkTransaction(operation);

                    if (result.Failed) error = new StringError(result.Error);
                }
            }

            DisplayError(error);
        }
        catch (Exception exception)
        {
            Plugin.LogSource?.LogError($"Sort preparation failed: {exception}");
        }
        finally
        {
            if (progressStarted && panel != null)
                try
                {
                    panel.ChangeProgress(false);
                }
                catch (Exception exception)
                {
                    Plugin.LogSource?.LogError($"Failed to stop sort progress: {exception}");
                }
        }
    }

    private static async Task<Error> ValidatePreparation(CompoundItem compoundItem,
        InventoryController inventoryController, PreparationSettings settings)
    {
        List<IOperationResult> operations = [];

        try
        {
            Error error = null;

            if (settings.FoldingEnabled) error = await FoldItems(compoundItem, inventoryController, operations, false);

            if (error == null && settings.StackingEnabled)
                error = await StackItems(compoundItem, inventoryController, operations, false);

            if (error == null && settings.NestingEnabled)
                error = await ContainerNesting.MoveItems(compoundItem, inventoryController, operations, false,
                    settings.RecursiveNestingEnabled);

            if (error != null) return error;

            if (!settings.SortConfiguration.HasCriteria) return null;

            OperationResult<ApplySortItemsPositionResult> sortOperation =
                BuildSortOperation(compoundItem, inventoryController, settings.SortConfiguration);

            return sortOperation.Failed ? sortOperation.Error : null;
        }
        finally
        {
            for (int i = operations.Count - 1; i >= 0; i--) operations[i].RollBack();
        }
    }

    private static OperationResult<ApplySortItemsPositionResult> BuildSortOperation(CompoundItem compoundItem,
        InventoryController inventoryController, SortConfiguration configuration)
    {
        if (compoundItem.Grids.Length == 1 && configuration.CompactSortingEnabled)
            return StashItemSorter.Sort(compoundItem, inventoryController, configuration);

        return StashItemSorter.SortVanilla(compoundItem, inventoryController, configuration);
    }

    private static async Task<Error> FoldItems(CompoundItem compoundItem, InventoryController inventoryController,
        List<IOperationResult> stagedOperations, bool runNetworkTransactions)
    {
        List<Item> items = GetGridItems(compoundItem);

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];

            if (item.PinLockState != EItemPinLockState.Free || !ItemManipulator.CanFold(item, out FoldableComponent foldable) ||
                foldable.Folded || HasContents(item) || ContainerCategorySettings.HasSelection(item.Id)) continue;

            OperationResult<FoldResult> operation = ItemManipulator.Fold(foldable, true, runNetworkTransactions);

            if (operation.Failed) return operation.Error;

            if (!runNetworkTransactions)
            {
                stagedOperations.Add(operation.Value);
                continue;
            }

            IResult result = await inventoryController.TryRunNetworkTransaction(operation);

            if (result.Failed) return new StringError(result.Error);
        }

        return null;
    }

    private static async Task<Error> StackItems(CompoundItem compoundItem, InventoryController inventoryController,
        List<IOperationResult> stagedOperations, bool runNetworkTransactions)
    {
        Dictionary<(string TemplateId, bool SpawnedInSession), List<Item>> groups = new Dictionary<(string TemplateId, bool SpawnedInSession), List<Item>>();
        List<Item> items = GetGridItems(compoundItem);

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];

            if (item.PinLockState != EItemPinLockState.Free || item.Owner == null || item.StackMaxSize <= 1 ||
                item.StackObjectsCount <= 0 || item.StackObjectsCount >= item.StackMaxSize) continue;

            (string TemplateId, bool SpawnedInSession) key = (item.TemplateId, item.SpawnedInSession);

            if (!groups.TryGetValue(key, out List<Item> group))
            {
                group = [];
                groups[key] = group;
            }

            group.Add(item);
        }

        foreach (List<Item> group in groups.Values)
        {
            Error error = await StackGroup(group, inventoryController, stagedOperations, runNetworkTransactions);

            if (error != null) return error;
        }

        return null;
    }

    private static async Task<Error> StackGroup(List<Item> group, InventoryController inventoryController,
        List<IOperationResult> stagedOperations, bool runNetworkTransactions)
    {
        while (true)
        {
            Item target = null;

            for (int i = 0; i < group.Count; i++)
            {
                Item candidate = group[i];

                if (candidate.StackObjectsCount <= 0 || candidate.StackObjectsCount >= candidate.StackMaxSize) continue;

                if (target == null || candidate.StackObjectsCount > target.StackObjectsCount) target = candidate;
            }

            if (target == null) return null;

            Item source = null;

            for (int i = 0; i < group.Count; i++)
            {
                Item candidate = group[i];

                if (candidate == target || candidate.StackObjectsCount <= 0 ||
                    candidate.StackObjectsCount >= candidate.StackMaxSize) continue;

                if (source == null || candidate.StackObjectsCount < source.StackObjectsCount) source = candidate;
            }

            if (source == null) return null;

            int targetCount = target.StackObjectsCount;
            int sourceCount = source.StackObjectsCount;
            OperationResult<ITransferOrMergeResult> operation =
                ItemManipulator.TransferOrMerge(source, target, inventoryController, runNetworkTransactions);

            if (operation.Failed) return operation.Error;

            if (!runNetworkTransactions)
            {
                stagedOperations.Add(operation.Value);
            }
            else
            {
                IResult result = await inventoryController.TryRunNetworkTransaction(operation);
                if (result.Failed) return new StringError(result.Error);
            }

            if (target.StackObjectsCount == targetCount && source.StackObjectsCount == sourceCount)
            {
#if DEBUG
                Plugin.LogSource?.LogWarning($"Stack operation completed without changing item {source.TemplateId}.");
#endif
                return null;
            }
        }
    }
    
    private static bool HasContents(Item item)
    {
        if (item is not CompoundItem compoundItem) return false;

        for (int i = 0; i < compoundItem.Grids.Length; i++)
        {
            foreach (Item _ in compoundItem.Grids[i].Items)
                return true;
        }

        return false;
    }

    private static List<Item> GetGridItems(CompoundItem compoundItem)
    {
        List<Item> items = [];

        foreach (Grid grid in compoundItem.Grids)
        foreach (Item item in grid.Items)
            items.Add(item);

        return items;
    }

    private static void DisplayError(Error error)
    {
        if (error == null) return;

        if (error is InventoryError inventoryError)
        {
            NotificationManager.DisplayWarningNotification(inventoryError.GetLocalizedDescription());
            return;
        }

        Debug.LogError(error);
    }

    private readonly struct PreparationSettings(
        bool foldingEnabled,
        bool stackingEnabled,
        bool nestingEnabled,
        bool recursiveNestingEnabled,
        SortConfiguration sortConfiguration)
    {
        public bool FoldingEnabled { get; } = foldingEnabled;
        public bool StackingEnabled { get; } = stackingEnabled;
        public bool NestingEnabled { get; } = nestingEnabled;
        public bool RecursiveNestingEnabled { get; } = recursiveNestingEnabled;

        public SortConfiguration SortConfiguration { get; } =
            sortConfiguration ?? throw new ArgumentNullException(nameof(sortConfiguration));

        public bool HasPreparation => FoldingEnabled || StackingEnabled || NestingEnabled;
    }
}

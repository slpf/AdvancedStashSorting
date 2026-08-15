using System.Collections.Generic;
using System.Reflection;
using AdvancedStashSorting.Sorting;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace AdvancedStashSorting.Patches;

public class ItemSorterCriteriaPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ItemSorter), nameof(ItemSorter.Sort));
    }

    [PatchPostfix]
    public static void Postfix(ref List<Item> __result)
    {
        SortConfiguration configuration = StashItemSorter.ActiveSortConfiguration;
        if (!StashItemSorter.CriteriaApplicationActive || configuration == null || __result == null ||
            __result.Count <= 1) return;

        __result = StashItemSorter.ApplyConfiguredSort(__result, configuration);
    }
}
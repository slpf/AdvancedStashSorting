using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace AdvancedStashSorting.Patches;

internal class UIFixesCompatPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(AccessTools.TypeByName("UIFixes.SortPatches+StackFirstPatch"), "Prefix");
    }

    [PatchPrefix]
    private static bool Prefix()
    {
        return !SortPreparationPatch.HandledSort;
    }
}
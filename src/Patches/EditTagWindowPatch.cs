using System.Reflection;
using AdvancedStashSorting.UI;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace AdvancedStashSorting.Patches;

public static class EditTagWindowPatch
{
    public static void Enable()
    {
        new ShowPatch().Enable();
        new SavePatch().Enable();
        new ClosePatch().Enable();
    }

    private sealed class ShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(EditTagWindow), nameof(EditTagWindow.Show));
        }

        [PatchPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(EditTagWindow __instance, TagComponent tagComponent)
        {
            TagCategoryPanelController controller = __instance.gameObject.GetComponent<TagCategoryPanelController>();

            if (controller == null) controller = __instance.gameObject.AddComponent<TagCategoryPanelController>();

            controller.Show(__instance, tagComponent?.Item as CompoundItem);
        }
    }

    private sealed class SavePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(EditTagWindow), "method_4");
        }

        [PatchPrefix]
        private static void Prefix(EditTagWindow __instance)
        {
            __instance.gameObject.GetComponent<TagCategoryPanelController>()?.Save();
        }
    }

    private sealed class ClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(EditTagWindow), nameof(EditTagWindow.Close));
        }

        [PatchPrefix]
        private static void Prefix(EditTagWindow __instance)
        {
            __instance.gameObject.GetComponent<TagCategoryPanelController>()?.Close();
        }
    }
}
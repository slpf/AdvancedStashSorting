using System.Reflection;
using AdvancedStashSorting.UI;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdvancedStashSorting.Patches;

public class SortButtonPatch : ModulePatch
{
    private static readonly AccessTools.FieldRef<GridSortPanel, Button> ButtonField =
        AccessTools.FieldRefAccess<GridSortPanel, Button>("_button");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(GridSortPanel), nameof(GridSortPanel.Show));
    }

    [PatchPostfix]
    public static void Postfix(GridSortPanel __instance, CompoundItem item)
    {
        Button button = ButtonField(__instance);

        bool isInventoryStash = item is Stash &&
                                SortPreparationPatch.IsInventorySortPanel(__instance, item);

        if (button == null) return;

        SortButtonRightClick trigger = button.gameObject.GetComponent<SortButtonRightClick>();
        if (!isInventoryStash)
        {
            trigger?.ClearButton();
            return;
        }

        if (trigger == null) trigger = button.gameObject.AddComponent<SortButtonRightClick>();

        trigger.SetButton(button.GetComponent<RectTransform>());
    }
}

public class SortButtonRightClick : MonoBehaviour, IPointerClickHandler
{
    private RectTransform _buttonRect;

    private void OnDisable()
    {
        SortOrderMenu.HideIfAnchoredTo(_buttonRect);
    }

    private void OnDestroy()
    {
        SortOrderMenu.DestroyIfAnchoredTo(_buttonRect);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right && _buttonRect != null)
            SortOrderMenu.Show(_buttonRect);
    }

    public void SetButton(RectTransform buttonRect)
    {
        _buttonRect = buttonRect;
    }

    public void ClearButton()
    {
        SortOrderMenu.HideIfAnchoredTo(_buttonRect);
        _buttonRect = null;
    }
}

using System;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public class CycleSelector : MonoBehaviour
{
    private TextMeshProUGUI _label;

    public static CycleSelector Create(Transform parent, string value, Action onPrevious, Action onNext,
        PhysicalPixelGrid pixelGrid)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        GameObject rowObject = new GameObject("CycleSelector", typeof(RectTransform), typeof(Image),
            typeof(LayoutElement));
        rowObject.transform.SetParent(parent, false);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();

        Image background = rowObject.GetComponent<Image>();
        background.color = SortTheme.CategoryRowBg;
        background.raycastTarget = true;

        LayoutElement rowLayoutElement = rowObject.GetComponent<LayoutElement>();
        rowLayoutElement.preferredHeight = pixelGrid.Snap(SortTheme.CategoryRowHeight);

        RectTransform rowContent = UiLayout.CreateHorizontalContent(
            rowRect,
            0f,
            0f,
            pixelGrid.Snap(SortTheme.Spacing),
            true,
            TextAnchor.MiddleCenter);

        CreateArrow(rowContent, true, onPrevious, pixelGrid);

        GameObject labelObject = new GameObject("Value", typeof(RectTransform), typeof(LayoutElement));
        labelObject.transform.SetParent(rowContent, false);
        LayoutElement labelLayoutElement = labelObject.GetComponent<LayoutElement>();
        labelLayoutElement.flexibleWidth = 1f;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        UiLayout.SetDefaultFont(label);
        label.text = value;
        label.fontSize = SortTheme.CategoryFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.color = SortTheme.CategoryText;

        CreateArrow(rowContent, false, onNext, pixelGrid);

        CycleSelector selector = rowObject.AddComponent<CycleSelector>();
        selector._label = label;

        return selector;
    }

    public void SetValue(string value)
    {
        if (_label != null) _label.text = value;
    }

    private static void CreateArrow(Transform parent, bool left, Action onClick, PhysicalPixelGrid pixelGrid)
    {
        float width = pixelGrid.Snap(18f);
        GameObject arrowObject = new GameObject(left ? "Previous" : "Next", typeof(RectTransform), typeof(Image),
            typeof(Button), typeof(LayoutElement));
        arrowObject.transform.SetParent(parent, false);

        Image hitArea = arrowObject.GetComponent<Image>();
        hitArea.color = SortTheme.Transparent;
        hitArea.raycastTarget = true;

        LayoutElement layoutElement = arrowObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.minWidth = width;

        Button button = arrowObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        if (onClick != null)
            button.onClick.AddListener(() =>
            {
                UiSound.Play(EUISoundType.ButtonClick);
                onClick();
            });

        GameObject graphicObject = new GameObject("Chevron", typeof(RectTransform), typeof(ChevronGraphic));
        graphicObject.transform.SetParent(arrowObject.transform, false);
        RectTransform graphicRect = graphicObject.GetComponent<RectTransform>();
        graphicRect.anchorMin = Vector2.zero;
        graphicRect.anchorMax = Vector2.one;
        graphicRect.offsetMin = Vector2.zero;
        graphicRect.offsetMax = Vector2.zero;

        ChevronGraphic graphic = graphicObject.GetComponent<ChevronGraphic>();
        graphic.Init(pixelGrid.Snap(SortTheme.ToggleCheckThickness), SortTheme.HandleColor);
        graphic.SetOpen(left);
    }
}
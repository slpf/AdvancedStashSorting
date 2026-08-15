using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public static class UiLayout
{
    public static void SetDefaultFont(TextMeshProUGUI text)
    {
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
    }

    public static RectTransform CreateHorizontalContent(
        RectTransform parent,
        float leftPadding,
        float rightPadding,
        float spacing,
        bool forceExpandHeight,
        TextAnchor alignment)
    {
        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.SetParent(parent, false);
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(leftPadding, 0f);
        contentRect.offsetMax = new Vector2(-rightPadding, 0f);

        HorizontalLayoutGroup layout = contentObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = forceExpandHeight;
        layout.childAlignment = alignment;
        return contentRect;
    }
}
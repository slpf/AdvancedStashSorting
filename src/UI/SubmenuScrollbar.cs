using System;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public sealed class SubmenuScrollbar : MonoBehaviour
{
    private RectTransform _viewport;
    private Vector2 _viewportOffset;
    private float _reservedWidth;

    public static SubmenuScrollbar Create(ScrollRect scroll, PhysicalPixelGrid pixelGrid)
    {
        if (scroll == null) throw new ArgumentNullException(nameof(scroll));

        if (scroll.viewport == null) throw new ArgumentException("Scroll viewport is required.", nameof(scroll));

        RectTransform viewport = scroll.viewport;
        float width = pixelGrid.Snap(SortTheme.ScrollbarWidth);
        GameObject trackObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        RectTransform track = trackObject.GetComponent<RectTransform>();
        track.SetParent(viewport.parent, false);
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = Vector2.one;
        track.offsetMin = new Vector2(viewport.offsetMax.x - width, viewport.offsetMin.y);
        track.offsetMax = viewport.offsetMax;
        trackObject.GetComponent<Image>().color = SortTheme.HeaderBg;

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        RectTransform handle = handleObject.GetComponent<RectTransform>();
        handle.SetParent(track, false);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;
        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = Color.white;

        Scrollbar scrollbar = trackObject.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        ColorBlock colors = scrollbar.colors;
        colors.normalColor = SortTheme.HandleColor;
        colors.highlightedColor = SortTheme.CriterionDisabledText;
        colors.pressedColor = SortTheme.CategoryText;
        colors.selectedColor = SortTheme.HandleColor;
        colors.colorMultiplier = 1f;
        scrollbar.colors = colors;
        Navigation navigation = scrollbar.navigation;
        navigation.mode = Navigation.Mode.None;
        scrollbar.navigation = navigation;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        SubmenuScrollbar result = trackObject.AddComponent<SubmenuScrollbar>();
        result._viewport = viewport;
        result._viewportOffset = viewport.offsetMax;
        result._reservedWidth = width + pixelGrid.Snap(SortTheme.Spacing);
        result.SetVisible(scroll.vertical);
        return result;
    }

    public void SetVisible(bool visible)
    {
        Vector2 offset = _viewportOffset;

        if (visible) offset.x -= _reservedWidth;

        if (_viewport.offsetMax != offset) _viewport.offsetMax = offset;

        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}

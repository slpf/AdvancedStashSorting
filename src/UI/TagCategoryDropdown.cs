using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public sealed class TagCategoryDropdown : MonoBehaviour
{
    private readonly List<string> _categories = [];
    private readonly List<ToggleRow> _rows = [];
    private readonly Vector3[] _corners = new Vector3[4];
    private RectTransform _anchor;
    private bool _closed;
    private RectTransform _content;
    private float _contentHeight;
    private Func<string, bool> _isSelected;
    private Action _onClose;
    private Action<string> _onToggle;
    private int _openedFrame;
    private RectTransform _owner;
    private CanvasGroup[] _ownerGroups;
    private RectTransform _panel;
    private PhysicalPixelGrid _pixelGrid;
    private Canvas _rootCanvas;
    private RectTransform _rootRect;
    private ScrollRect _scroll;

    public static TagCategoryDropdown Create(RectTransform owner, RectTransform anchor, TMP_FontAsset font,
        string title, IReadOnlyList<string> categories, Func<string, bool> isSelected, Action<string> onToggle,
        Action onClose)
    {
        if (categories == null) throw new ArgumentNullException(nameof(categories));

        if (isSelected == null) throw new ArgumentNullException(nameof(isSelected));

        if (onToggle == null) throw new ArgumentNullException(nameof(onToggle));

        if (owner == null || anchor == null || categories.Count == 0 || !owner.gameObject.activeInHierarchy ||
            !anchor.gameObject.activeInHierarchy)
            return null;

        Canvas sourceCanvas = owner.GetComponentInParent<Canvas>();
        Canvas rootCanvas = sourceCanvas != null ? sourceCanvas.rootCanvas : null;
        RectTransform rootRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;

        if (rootRect == null || !rootCanvas.isActiveAndEnabled || rootRect.rect.width <= 0f ||
            rootRect.rect.height <= 0f)
            return null;

        GameObject host = new GameObject("TagCategoryDropdown", typeof(RectTransform), typeof(Canvas),
            typeof(GraphicRaycaster));
        RectTransform hostRect = host.GetComponent<RectTransform>();
        hostRect.SetParent(rootRect, false);
        hostRect.anchorMin = Vector2.zero;
        hostRect.anchorMax = Vector2.one;
        hostRect.pivot = rootRect.pivot;
        hostRect.offsetMin = Vector2.zero;
        hostRect.offsetMax = Vector2.zero;
        hostRect.SetAsLastSibling();

        Canvas popupCanvas = host.GetComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.worldCamera = rootCanvas.worldCamera;
        popupCanvas.sortingLayerID = sourceCanvas.sortingLayerID;
        int sortingOrder = sourceCanvas.sortingOrder;

        foreach (Canvas parentCanvas in owner.GetComponentsInParent<Canvas>(true))
            if (parentCanvas.sortingLayerID == popupCanvas.sortingLayerID)
                sortingOrder = Mathf.Max(sortingOrder, parentCanvas.sortingOrder);

        popupCanvas.sortingOrder = Mathf.Min(short.MaxValue, sortingOrder + 1);

        TagCategoryDropdown result = host.AddComponent<TagCategoryDropdown>();
        result._owner = owner;
        result._anchor = anchor;
        result._rootCanvas = rootCanvas;
        result._rootRect = rootRect;
        result._pixelGrid = new PhysicalPixelGrid(rootCanvas.scaleFactor);
        result._ownerGroups = owner.GetComponentsInParent<CanvasGroup>(true);
        result._isSelected = isSelected;
        result._onToggle = onToggle;
        result._openedFrame = Time.frameCount;

        for (int i = 0; i < categories.Count; i++) result._categories.Add(categories[i]);

        try
        {
            result.Build(hostRect, font, title);

            if (!result.IsOwnerVisible() || !result.Place())
            {
                result.Close();
                return null;
            }

            result.Refresh();
            LayoutRebuilder.ForceRebuildLayoutImmediate(result._content);
            result._onClose = onClose;
            return result;
        }
        catch (Exception exception)
        {
            result.Close();
            Plugin.LogSource?.LogError($"Failed to create tag category dropdown: {exception}");
            return null;
        }
    }

    public void Refresh()
    {
        if (_closed || _isSelected == null) return;

        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i] != null)
                _rows[i].SetValue(_isSelected(_categories[i]));
    }

    public void Close()
    {
        if (_closed) return;

        _closed = true;
        Action onClose = _onClose;
        _onClose = null;
        _isSelected = null;
        _onToggle = null;
        _rows.Clear();
        _categories.Clear();
        gameObject.SetActive(false);
        Destroy(gameObject);
        onClose?.Invoke();
    }

    private void LateUpdate()
    {
        if (_closed) return;

        if (!IsOwnerVisible() || !Place())
        {
            Close();
            return;
        }

        if (Time.frameCount == _openedFrame ||
            !(Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2)))
            return;

        Camera camera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;

        if (!RectTransformUtility.RectangleContainsScreenPoint(_panel, Input.mousePosition, camera)) Close();
    }

    private void OnDisable()
    {
        Close();
    }

    private void OnDestroy()
    {
        Close();
    }

    private bool IsOwnerVisible()
    {
        if (_owner == null || _anchor == null || _rootCanvas == null || !_rootCanvas.isActiveAndEnabled ||
            !_owner.gameObject.activeInHierarchy || !_anchor.gameObject.activeInHierarchy)
            return false;

        for (int i = 0; i < _ownerGroups.Length; i++)
        {
            CanvasGroup group = _ownerGroups[i];

            if (group == null) continue;

            if (group.alpha <= 0f) return false;

            if (group.ignoreParentGroups) break;
        }

        return true;
    }

    private void Build(RectTransform host, TMP_FontAsset font, string title)
    {
        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(TagCategoryDropdownBackdrop));
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.SetParent(host, false);
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = SortTheme.Transparent;
        backdropImage.raycastTarget = true;
        Button backdropButton = backdrop.GetComponent<Button>();
        backdropButton.targetGraphic = backdropImage;
        backdropButton.transition = Selectable.Transition.None;
        backdropButton.onClick.AddListener(Close);
        backdrop.GetComponent<TagCategoryDropdownBackdrop>().Initialize(_owner);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
        _panel = panel.GetComponent<RectTransform>();
        _panel.SetParent(host, false);
        _panel.anchorMin = host.pivot;
        _panel.anchorMax = host.pivot;
        _panel.pivot = new Vector2(0f, 1f);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = SortTheme.PanelFill;
        panelImage.raycastTarget = true;
        Outline border = panel.GetComponent<Outline>();
        border.effectColor = SortTheme.PanelBorder;
        float borderThickness = _pixelGrid.Snap(SortTheme.BorderThickness);
        border.effectDistance = new Vector2(borderThickness, borderThickness);

        float padding = _pixelGrid.Snap(SortTheme.Padding);
        float headerHeight = _pixelGrid.Snap(SortTheme.HeaderHeight);
        float spacing = _pixelGrid.Snap(SortTheme.Spacing);
        MakeHeader(font, title, padding, headerHeight);

        GameObject scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.SetParent(_panel, false);
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(padding, padding);
        scrollRect.offsetMax = new Vector2(-padding, -padding - headerHeight - spacing);
        _scroll = scrollObject.GetComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.inertia = false;
        _scroll.scrollSensitivity = _pixelGrid.Snap(SortTheme.CategoryRowHeight) * 3f;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image),
            typeof(RectMask2D));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollRect, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = SortTheme.Transparent;
        viewportImage.raycastTarget = true;
        _scroll.viewport = viewport;

        GameObject contentObject = new GameObject("Categories", typeof(RectTransform), typeof(VerticalLayoutGroup));
        _content = contentObject.GetComponent<RectTransform>();
        _content.SetParent(viewport, false);
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);
        _content.anchoredPosition = Vector2.zero;
        _contentHeight = _categories.Count * _pixelGrid.Snap(SortTheme.CategoryRowHeight) +
                         Mathf.Max(0, _categories.Count - 1) * spacing;
        _content.sizeDelta = new Vector2(0f, _contentHeight);
        _scroll.content = _content;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < _categories.Count; i++)
        {
            string category = _categories[i];
            ToggleRow row = ToggleRow.Create(_content, Localization.Get(category), _isSelected(category),
                _ => Toggle(category), _pixelGrid);
            row.GetComponent<Image>().color = SortTheme.CategoryRowBg;
            TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();

            if (font != null) label.font = font;

            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            _rows.Add(row);
        }
    }

    private void MakeHeader(TMP_FontAsset font, string title, float padding, float height)
    {
        GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(Image));
        RectTransform header = headerObject.GetComponent<RectTransform>();
        header.SetParent(_panel, false);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.anchoredPosition = new Vector2(0f, -padding);
        header.sizeDelta = new Vector2(-padding * 2f, height);
        Image headerImage = headerObject.GetComponent<Image>();
        headerImage.color = SortTheme.HeaderBg;
        headerImage.raycastTarget = false;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(header, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        float textPadding = _pixelGrid.Snap(SortTheme.HeaderTextPadding);
        labelRect.offsetMin = new Vector2(_pixelGrid.Snap(SortTheme.RowTextPadding), textPadding);
        labelRect.offsetMax = new Vector2(-textPadding, -textPadding);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        UiLayout.SetDefaultFont(label);

        if (font != null) label.font = font;

        label.text = title;
        label.fontSize = SortTheme.HeaderFontSize;
        label.alignment = TextAlignmentOptions.Left;
        label.color = SortTheme.HeaderText;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
    }

    private bool Place()
    {
        if (_rootRect == null || _panel == null || _anchor == null) return false;

        Rect bounds = _rootRect.rect;
        float margin = _pixelGrid.Snap(SortTheme.Padding + SortTheme.BorderThickness);
        float width = Mathf.Min(_pixelGrid.Snap(SortTheme.Width), bounds.width - margin * 2f);
        float maximumHeight = bounds.height - margin * 2f;
        float fixedHeight = _pixelGrid.Snap(SortTheme.Padding) * 2f +
                            _pixelGrid.Snap(SortTheme.HeaderHeight) + _pixelGrid.Snap(SortTheme.Spacing);
        float minimumHeight = fixedHeight + _pixelGrid.Snap(SortTheme.CategoryRowHeight);

        if (!IsFinite(width) || !IsFinite(maximumHeight) || width <= 0f || maximumHeight < minimumHeight)
            return false;

        _anchor.GetWorldCorners(_corners);
        float anchorLeft = float.PositiveInfinity;
        float anchorBottom = float.PositiveInfinity;
        float anchorTop = float.NegativeInfinity;

        for (int i = 0; i < _corners.Length; i++)
        {
            Vector3 corner = _rootRect.InverseTransformPoint(_corners[i]);

            if (!IsFinite(corner.x) || !IsFinite(corner.y)) return false;

            anchorLeft = Mathf.Min(anchorLeft, corner.x);
            anchorBottom = Mathf.Min(anchorBottom, corner.y);
            anchorTop = Mathf.Max(anchorTop, corner.y);
        }

        float lowerBound = bounds.yMin + margin;
        float upperBound = bounds.yMax - margin;
        float gap = _pixelGrid.Snap(SortTheme.SubMenuOffset);
        float below = Mathf.Max(0f, anchorBottom - gap - lowerBound);
        float above = Mathf.Max(0f, upperBound - anchorTop - gap);
        float desiredHeight = fixedHeight + _contentHeight;
        bool openAbove = below < desiredHeight && above > below;
        float availableHeight = openAbove ? above : below;

        if (availableHeight < minimumHeight) availableHeight = maximumHeight;

        float height = Mathf.Min(desiredHeight, maximumHeight, availableHeight);
        float left = Mathf.Clamp(anchorLeft, bounds.xMin + margin, bounds.xMax - margin - width);
        float top = openAbove ? anchorTop + gap + height : anchorBottom - gap;
        top = Mathf.Clamp(top, lowerBound + height, upperBound);
        _panel.sizeDelta = new Vector2(width, height);
        _panel.anchoredPosition = new Vector2(left, top);
        _scroll.vertical = _contentHeight > height - fixedHeight;
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void Toggle(string category)
    {
        if (_closed) return;

        _onToggle?.Invoke(category);
        Refresh();
    }
}

public sealed class TagCategoryDropdownBackdrop : MonoBehaviour, ICanvasRaycastFilter
{
    private RectTransform _owner;

    public void Initialize(RectTransform owner)
    {
        _owner = owner;
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        return _owner == null || !RectTransformUtility.RectangleContainsScreenPoint(_owner, screenPoint, eventCamera);
    }
}

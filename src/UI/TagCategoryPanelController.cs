using System;
using System.Collections;
using System.Collections.Generic;
using AdvancedStashSorting.Sorting;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public sealed class TagCategoryPanelController : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<EditTagWindow, DefaultUIButton> SaveButtonSpawnerField =
        AccessTools.FieldRefAccess<EditTagWindow, DefaultUIButton>("_saveButtonSpawner");

    private static readonly AccessTools.FieldRef<EditTagWindow, ValidationInputField> TagInputField =
        AccessTools.FieldRefAccess<EditTagWindow, ValidationInputField>("_tagInput");

    private static readonly AccessTools.FieldRef<EditTagWindow, TagColorsPanel> ColorsPanelField =
        AccessTools.FieldRefAccess<EditTagWindow, TagColorsPanel>("_colorsPanel");

    private static readonly AccessTools.FieldRef<EditTagWindow, TextMeshProUGUI> ContainerTagLabelField =
        AccessTools.FieldRefAccess<EditTagWindow, TextMeshProUGUI>("_containerTagLabel");

    private const float RaiseOffsetScreenPercent = 0.10f;
    private readonly List<TagCategoryButton> _buttons = [];
    private List<string> _available;
    private CompoundItem _container;
    private int _generation;
    private bool _layoutApplied;
    private Vector2 _originalRootPosition;
    private Vector2 _originalRootSize;
    private Vector2 _originalSavePosition;
    private float _originalWindowAlpha;
    private GameObject _panel;
    private RectTransform _root;
    private RectTransform _saveRect;
    private HashSet<string> _selection;

    private EditTagWindow _window;
    private bool _windowAlphaChanged;
    private CanvasGroup _windowCanvasGroup;

    public void Show(EditTagWindow window, CompoundItem container)
    {
        Close();

        if (window == null || !ContainerNesting.CanConfigureCategories(container)) return;

        List<string> available = ContainerCategoryAvailability.GetAvailable(container);

        if (available.Count == 0) return;

        _window = window;
        _windowCanvasGroup = window.GetComponent<CanvasGroup>();

        if (_windowCanvasGroup == null) _windowCanvasGroup = window.gameObject.AddComponent<CanvasGroup>();

        _originalWindowAlpha = _windowCanvasGroup.alpha;
        _windowCanvasGroup.alpha = 0f;
        _windowAlphaChanged = true;
        _container = container;
        _available = available;
        _selection = ContainerCategorySettings.GetSelection(container, available);

        int generation = ++_generation;

        StartCoroutine(BuildAfterLayout(generation));
    }

    public void Save()
    {
        if (_container != null && _selection != null)
            Config.SaveContainerCategories(_container, _selection, _available);
    }

    public void Close()
    {
        _generation++;

        if (_panel != null)
        {
            _panel.SetActive(false);
            Destroy(_panel);
        }

        if (_windowAlphaChanged && _windowCanvasGroup != null) _windowCanvasGroup.alpha = _originalWindowAlpha;

        if (_layoutApplied)
        {
            if (_root != null)
            {
                _root.sizeDelta = _originalRootSize;
                _root.anchoredPosition = _originalRootPosition;
            }

            if (_saveRect != null) _saveRect.anchoredPosition = _originalSavePosition;
        }

        _buttons.Clear();
        _window = null;
        _container = null;
        _selection = null;
        _available = null;
        _root = null;
        _saveRect = null;
        _panel = null;
        _windowCanvasGroup = null;
        _originalWindowAlpha = 1f;
        _windowAlphaChanged = false;
        _layoutApplied = false;
    }

    private IEnumerator BuildAfterLayout(int generation)
    {
        yield return new WaitForEndOfFrame();

        if (generation != _generation || _window == null || !_window.gameObject.activeInHierarchy) yield break;

        Build();
    }

    private void Build()
    {
        _root = _window.GetComponent<RectTransform>();
        _saveRect = SaveButtonSpawnerField(_window).GetComponent<RectTransform>();
        RectTransform inputRect = TagInputField(_window).GetComponent<RectTransform>();
        RectTransform colorsRect = ColorsPanelField(_window).GetComponent<RectTransform>();
        RectTransform content = FindCommonParent(inputRect, colorsRect, _saveRect);

        if (_root == null || _saveRect == null || inputRect == null || content == null)
        {
            RestoreWindowAlpha();
            return;
        }

        _originalRootSize = _root.sizeDelta;
        _originalRootPosition = _root.anchoredPosition;
        _originalSavePosition = _saveRect.anchoredPosition;

        Vector3 originalSaveTop = GetTopCenter(_saveRect);
        Canvas canvas = _window.GetComponentInParent<Canvas>();
        float canvasScale = SortTheme.NormalizeScale(canvas != null ? canvas.rootCanvas.scaleFactor : 1f);
        float gridSpacing = SnapToPhysicalPixel(SortTheme.TagCategoryGridSpacing, canvasScale);
        float cellHeight = SnapToPhysicalPixel(SortTheme.TagCategoryCellHeight, canvasScale);

        int rowCount = Mathf.CeilToInt(_available.Count / (float)SortTheme.TagCategoryColumnCount);
        float fullGridHeight = rowCount * cellHeight + Mathf.Max(0, rowCount - 1) * gridSpacing;
        float panelHeight = SortTheme.TagCategoryTopPadding + SortTheme.TagCategoryTitleHeight +
                            SortTheme.TagCategorySectionSpacing + fullGridHeight;
        float extension = panelHeight;

        _root.sizeDelta = _originalRootSize + new Vector2(0f, extension);
        _root.anchoredPosition = _originalRootPosition + Vector2.down * extension * (1f - _root.pivot.y);
        _layoutApplied = true;

        _panel = new GameObject("AutocollectCategories", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(content, false);
        Transform inputPanel = FindDirectChild(content, inputRect);
        _panel.transform.SetSiblingIndex(inputPanel.GetSiblingIndex() + 1);
        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        bool panelUsesLayout = content.GetComponent<LayoutGroup>() != null;

        if (panelUsesLayout)
        {
            float panelWidth = Mathf.Max(SortTheme.TagCategoryMinimumWidth,
                content.rect.width - SortTheme.TagCategoryHorizontalMargin * 2f);
            LayoutElement panelLayout = _panel.AddComponent<LayoutElement>();
            panelLayout.minWidth = panelWidth;
            panelLayout.preferredWidth = panelWidth;
            panelLayout.flexibleWidth = 0f;
            panelLayout.minHeight = panelHeight;
            panelLayout.preferredHeight = panelHeight;
            panelLayout.flexibleHeight = 0f;
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        }
        else
        {
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.offsetMin = new Vector2(SortTheme.TagCategoryHorizontalMargin, panelRect.offsetMin.y);
            panelRect.offsetMax = new Vector2(-SortTheme.TagCategoryHorizontalMargin, panelRect.offsetMax.y);
            panelRect.sizeDelta = new Vector2(-SortTheme.TagCategoryHorizontalMargin * 2f, panelHeight);
            Vector3 localPanelTop = content.InverseTransformPoint(originalSaveTop);
            panelRect.anchoredPosition = new Vector2(0f, localPanelTop.y - content.rect.yMax);
        }

        Image panelImage = _panel.GetComponent<Image>();
        panelImage.color = SortTheme.PanelFill;
        panelImage.raycastTarget = true;

        Canvas.ForceUpdateCanvases();
        TMP_FontAsset font = ContainerTagLabelField(_window).font;

        MakeHeader(panelRect, font);
        MakeCategoryViewport(panelRect, font, fullGridHeight, gridSpacing, cellHeight, canvasScale);
        RefreshButtons();

        Canvas.ForceUpdateCanvases();
        RaiseWindow(_window);

        if (!panelUsesLayout)
        {
            Vector3 currentSaveTop = GetTopCenter(_saveRect);
            float desiredSaveTopY = originalSaveTop.y - extension * _root.lossyScale.y;
            _saveRect.position += new Vector3(0f, desiredSaveTopY - currentSaveTop.y, 0f);
        }

        if (_windowAlphaChanged && _windowCanvasGroup != null)
        {
            _windowCanvasGroup.alpha = _originalWindowAlpha;
            _windowAlphaChanged = false;
        }
    }

    private void RestoreWindowAlpha()
    {
        if (_windowAlphaChanged && _windowCanvasGroup != null)
        {
            _windowCanvasGroup.alpha = _originalWindowAlpha;
            _windowAlphaChanged = false;
        }
    }

    private static void RaiseWindow(EditTagWindow window)
    {
        RectTransform rect = window.GetComponent<RectTransform>();
        Canvas canvas = window.GetComponentInParent<Canvas>();
        float scale = canvas != null && canvas.rootCanvas != null ? canvas.rootCanvas.scaleFactor : 1f;

        if (scale <= 0f) scale = 1f;

        float offsetPixels = Screen.height * RaiseOffsetScreenPercent;
        rect.anchoredPosition += Vector2.up * (offsetPixels / scale);
    }

    private static RectTransform FindCommonParent(params RectTransform[] rects)
    {
        if (rects == null || rects.Length == 0 || rects[0] == null) return null;

        Transform candidate = rects[0].parent;

        while (candidate != null)
        {
            bool containsAll = true;

            for (int i = 1; i < rects.Length; i++)
                if (rects[i] == null || !rects[i].IsChildOf(candidate))
                {
                    containsAll = false;
                    break;
                }

            if (containsAll) return candidate as RectTransform;

            candidate = candidate.parent;
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, Transform descendant)
    {
        Transform result = descendant;

        while (result.parent != null && result.parent != parent) result = result.parent;

        return result;
    }

    private static Vector3 GetTopCenter(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return (corners[1] + corners[2]) * 0.5f;
    }

    private static float SnapToPhysicalPixel(float value, float canvasScale)
    {
        return Mathf.Max(1f, Mathf.Round(value * canvasScale)) / canvasScale;
    }

    private void MakeHeader(RectTransform parent, TMP_FontAsset font)
    {
        RectTransform headerRect = MakeTopRect(parent, "Header", SortTheme.TagCategoryTopPadding,
            SortTheme.TagCategoryTitleHeight, typeof(Image));
        headerRect.GetComponent<Image>().color = SortTheme.TagCategoryHeaderBg;

        GameObject filterObject = new GameObject("FilterIcon", typeof(RectTransform), typeof(Image));
        filterObject.transform.SetParent(headerRect, false);
        RectTransform filterRect = filterObject.GetComponent<RectTransform>();
        filterRect.anchorMin = new Vector2(0f, 0.5f);
        filterRect.anchorMax = new Vector2(0f, 0.5f);
        filterRect.pivot = new Vector2(0f, 0.5f);
        filterRect.anchoredPosition = new Vector2(SortTheme.TagCategoryFilterPadding, 0f);
        filterRect.sizeDelta = new Vector2(SortTheme.TagCategoryHeaderIconSize, SortTheme.TagCategoryHeaderIconSize);
        Image filterIcon = filterObject.GetComponent<Image>();
        filterIcon.sprite = TagHeaderSprites.Filter();
        filterIcon.color = SortTheme.TagCategoryFilterColor;
        filterIcon.preserveAspect = true;
        filterIcon.raycastTarget = false;

        TextMeshProUGUI title = MakeText(headerRect, Localization.Get("container_categories_title"), font,
            SortTheme.TagCategoryTitleFontSize, SortTheme.TagCategoryHeaderText);
        title.alignment = TextAlignmentOptions.Left;
        title.rectTransform.offsetMin =
            new Vector2(
                SortTheme.TagCategoryFilterPadding + SortTheme.TagCategoryHeaderIconSize +
                SortTheme.TagCategoryHeaderIconSpacing, 0f);
        title.rectTransform.offsetMax =
            new Vector2(
                -SortTheme.TagCategoryTogglePadding - SortTheme.TagCategoryToggleWidth -
                SortTheme.TagCategoryHeaderIconSpacing, 0f);

        GameObject toggleObject = new GameObject("ToggleAll", typeof(RectTransform), typeof(TagToggleBackground),
            typeof(TagToggleButtonVisual), typeof(Button));
        toggleObject.transform.SetParent(headerRect, false);
        RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1f, 0.5f);
        toggleRect.anchorMax = new Vector2(1f, 0.5f);
        toggleRect.pivot = new Vector2(1f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(-SortTheme.TagCategoryTogglePadding, 0f);
        toggleRect.sizeDelta = new Vector2(SortTheme.TagCategoryToggleWidth, SortTheme.TagCategoryTitleHeight);
        TagToggleBackground toggleBackground = toggleObject.GetComponent<TagToggleBackground>();
        toggleBackground.color = Color.white;
        Button toggleButton = toggleObject.GetComponent<Button>();
        toggleButton.targetGraphic = toggleBackground;
        toggleButton.transition = Selectable.Transition.None;
        toggleObject.GetComponent<TagToggleButtonVisual>().Initialize(toggleBackground);
        HoverTooltipArea tooltip = toggleObject.AddComponent<HoverTooltipArea>();
        tooltip.SetMessageText(ToggleAllTooltip);
        toggleButton.onClick.AddListener(() =>
        {
            ToggleAll();
            tooltip.Show();
        });

        GameObject toggleIconObject = new GameObject("GridIcon", typeof(RectTransform), typeof(Image));
        toggleIconObject.transform.SetParent(toggleRect, false);
        RectTransform toggleIconRect = toggleIconObject.GetComponent<RectTransform>();
        toggleIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        toggleIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        toggleIconRect.pivot = new Vector2(0.5f, 0.5f);
        toggleIconRect.anchoredPosition = new Vector2(0f, SortTheme.TagCategoryToggleIconOffsetY);
        toggleIconRect.sizeDelta =
            new Vector2(SortTheme.TagCategoryToggleIconSize, SortTheme.TagCategoryToggleIconSize);
        Image toggleIcon = toggleIconObject.GetComponent<Image>();
        toggleIcon.sprite = TagHeaderSprites.Grid();
        toggleIcon.color = SortTheme.CategoryText;
        toggleIcon.preserveAspect = true;
        toggleIcon.raycastTarget = false;
    }

    private void MakeCategoryViewport(RectTransform parent, TMP_FontAsset font, float gridHeight, float gridSpacing,
        float cellHeight, float canvasScale)
    {
        float top = SortTheme.TagCategoryTopPadding + SortTheme.TagCategoryTitleHeight +
                    SortTheme.TagCategorySectionSpacing;
        RectTransform viewport = MakeTopRect(parent, "CategoryViewport", top, gridHeight, typeof(Image), typeof(RectMask2D));
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = SortTheme.Transparent;
        viewportImage.raycastTarget = true;

        GameObject contentObject = new GameObject("Categories", typeof(RectTransform), typeof(RemainderGridLayoutGroup));
        contentObject.transform.SetParent(viewport, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, gridHeight);

        RemainderGridLayoutGroup grid = contentObject.GetComponent<RemainderGridLayoutGroup>();
        int contentWidthPixels = Mathf.Max(1, Mathf.FloorToInt(parent.rect.width * canvasScale));
        int spacingPixels = Mathf.Max(1, Mathf.RoundToInt(gridSpacing * canvasScale));
        int occupiedBySpacing = spacingPixels * (SortTheme.TagCategoryColumnCount - 1);
        int cellWidthPixels = Mathf.Max(1, (contentWidthPixels - occupiedBySpacing) / SortTheme.TagCategoryColumnCount);
        int remainderPixels = Mathf.Max(0,
            contentWidthPixels - occupiedBySpacing - cellWidthPixels * SortTheme.TagCategoryColumnCount);
        float cellWidth = cellWidthPixels / canvasScale;
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.spacing = new Vector2(gridSpacing, gridSpacing);
        grid.LastColumnExtraWidth = remainderPixels / canvasScale;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = SortTheme.TagCategoryColumnCount;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;

        for (int i = 0; i < _available.Count; i++)
        {
            string category = _available[i];
            TagCategoryButton button = TagCategoryButton.Create(content, category, Localization.Get(category), font, ToggleCategory);
            _buttons.Add(button);
        }
    }

    private static RectTransform MakeTopRect(RectTransform parent, string name, float top, float height,
        params Type[] components)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));

        for (int i = 0; i < components.Length; i++) child.AddComponent(components[i]);

        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(0f, height);

        return rect;
    }

    private static TextMeshProUGUI MakeText(RectTransform parent, string value, TMP_FontAsset font, float fontSize,
        Color color)
    {
        GameObject textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(SortTheme.TagCategoryTextPadding, 0f);
        rect.offsetMax = new Vector2(-SortTheme.TagCategoryTextPadding, 0f);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private void ToggleAll()
    {
        if (_selection.SetEquals(_available))
            _selection.Clear();
        else
            _selection = new HashSet<string>(_available);

        RefreshButtons();
    }

    private string ToggleAllTooltip()
    {
        bool allSelected = _selection != null && _available != null && _selection.SetEquals(_available);
        return Localization.Get(allSelected ? "disable_all" : "enable_all");
    }

    private void ToggleCategory(string category)
    {
        if (!_selection.Add(category)) _selection.Remove(category);

        RefreshButtons();
    }

    private void RefreshButtons()
    {
        foreach (TagCategoryButton button in _buttons) button.SetSelected(_selection != null && _selection.Contains(button.Category));
    }
}

public sealed class TagCategoryButton : MonoBehaviour
{
    private Image _background;
    private TextMeshProUGUI _label;
    private Action<string> _onClick;

    public string Category { get; private set; }

    public static TagCategoryButton Create(Transform parent, string category, string label, TMP_FontAsset font,
        Action<string> onClick)
    {
        GameObject buttonObject = new GameObject("Category_" + category, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        TagCategoryButton result = buttonObject.AddComponent<TagCategoryButton>();
        result.Category = category;
        result._onClick = onClick;
        result._background = buttonObject.GetComponent<Image>();

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        result._label = MakeLabel(rect, label, font);
        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(result.Click);

        return result;
    }

    public void SetSelected(bool selected)
    {
        _background.color = selected ? SortTheme.CategoryRowBg : SortTheme.CriterionDisabledBg;
        _label.color = selected ? SortTheme.CategoryText : SortTheme.CriterionDisabledText;
    }

    private static TextMeshProUGUI MakeLabel(RectTransform parent, string value, TMP_FontAsset font)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(SortTheme.TagCategoryTextPadding, 0f);
        rect.offsetMax = new Vector2(-SortTheme.TagCategoryTextPadding, 0f);
        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.text = value;
        label.fontSize = SortTheme.TagCategoryCellFontSize;
        label.color = SortTheme.CategoryText;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private void Click()
    {
        _onClick?.Invoke(Category);
    }
}
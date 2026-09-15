using System;
using System.Collections;
using System.Collections.Generic;
using AdvancedStashSorting.Sorting;
using EFT.InventoryLogic;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public sealed class TagCategoryPanelController : MonoBehaviour
{
    private const float WindowVerticalMarginPixels = 32f;
    private readonly Dictionary<string, List<string>> _availableByMain = new();
    private readonly List<TagCategoryButton> _buttons = [];
    private readonly List<string> _categories = [];
    private List<string> _available;
    private CompoundItem _container;
    private TagCategoryDropdown _dropdown;
    private TagCategoryButton _dropdownButton;
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
        BuildCategories();

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
        CloseDropdown();

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

        _availableByMain.Clear();
        _buttons.Clear();
        _categories.Clear();
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

    private void OnDisable()
    {
        Close();
    }

    private IEnumerator BuildAfterLayout(int generation)
    {
        yield return new WaitForEndOfFrame();

        if (generation != _generation || _window == null || !_window.gameObject.activeInHierarchy) yield break;

        try
        {
            Build();
        }
        catch (Exception exception)
        {
            Plugin.LogSource?.LogError($"Failed to build tag categories: {exception}");
            Close();
        }
    }

    private void Build()
    {
        _root = _window.GetComponent<RectTransform>();
        _saveRect = _window._saveButtonSpawner.GetComponent<RectTransform>();
        RectTransform inputRect = _window._tagInput.GetComponent<RectTransform>();
        RectTransform colorsRect = _window._colorsPanel.GetComponent<RectTransform>();
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
        float categoryGridHeight = CalculateGridHeight(_categories.Count, cellHeight, gridSpacing);
        float gridTop = SortTheme.TagCategoryTopPadding + SortTheme.TagCategoryTitleHeight +
                        SortTheme.TagCategorySectionSpacing;
        float panelHeight = gridTop + categoryGridHeight;
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
        TMP_FontAsset font = _window._containerTagLabel.font;

        MakeHeader(panelRect, font);
        MakeCategoryViewport(panelRect, font, gridTop, categoryGridHeight, gridSpacing, cellHeight, canvasScale);
        RefreshButtons();

        Canvas.ForceUpdateCanvases();
        ClampWindowVertically(_window);

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

    private static void ClampWindowVertically(EditTagWindow window)
    {
        RectTransform rect = window.GetComponent<RectTransform>();
        Canvas canvas = window.GetComponentInParent<Canvas>();
        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;

        if (rect == null || rect.parent == null || canvasRect == null || canvasRect.rect.height <= 0f) return;

        float scaleFactor = rootCanvas.scaleFactor;

        if (float.IsNaN(scaleFactor) || float.IsInfinity(scaleFactor) || scaleFactor <= 0f) scaleFactor = 1f;

        float margin = Mathf.Min(WindowVerticalMarginPixels / scaleFactor, canvasRect.rect.height * 0.5f);
        float lowerBound = canvasRect.rect.yMin + margin;
        float upperBound = canvasRect.rect.yMax - margin;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float windowBottom = float.PositiveInfinity;
        float windowTop = float.NegativeInfinity;

        for (int i = 0; i < corners.Length; i++)
        {
            float cornerY = canvasRect.InverseTransformPoint(corners[i]).y;
            windowBottom = Mathf.Min(windowBottom, cornerY);
            windowTop = Mathf.Max(windowTop, cornerY);
        }

        float windowHeight = windowTop - windowBottom;
        float availableHeight = upperBound - lowerBound;
        float windowCenter = (windowBottom + windowTop) * 0.5f;
        float targetCenter;

        if (windowHeight <= availableHeight)
        {
            float halfHeight = windowHeight * 0.5f;
            targetCenter = Mathf.Clamp(windowCenter, lowerBound + halfHeight, upperBound - halfHeight);
        }
        else
        {
            targetCenter = (lowerBound + upperBound) * 0.5f;
        }

        float offset = targetCenter - windowCenter;

        if (Mathf.Approximately(offset, 0f)) return;

        Vector3 worldOffset = canvasRect.TransformVector(Vector3.up * offset);
        Vector3 parentOffset = rect.parent.InverseTransformVector(worldOffset);
        rect.anchoredPosition += new Vector2(parentOffset.x, parentOffset.y);
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

    private static float CalculateGridHeight(int itemCount, float cellHeight, float gridSpacing)
    {
        int rowCount = Mathf.CeilToInt(itemCount / (float)SortTheme.TagCategoryColumnCount);
        return rowCount * cellHeight + Mathf.Max(0, rowCount - 1) * gridSpacing;
    }

    private void BuildCategories()
    {
        _availableByMain.Clear();
        _categories.Clear();
        HashSet<string> available = new HashSet<string>(_available);

        foreach (string category in CategoryCatalog.GetMainOrder())
        {
            if (!CategoryCatalog.HasChildren(category))
            {
                if (available.Contains(category)) _categories.Add(category);

                continue;
            }

            List<string> children = CategoryCatalog.GetSubOrder(category);
            children.RemoveAll(child => !available.Contains(child));

            if (children.Count == 0) continue;

            _availableByMain.Add(category, children);
            _categories.Add(category);
        }
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
            UiSound.Play(EUISoundType.ButtonClick);
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

    private void MakeCategoryViewport(RectTransform parent, TMP_FontAsset font,
        float top, float gridHeight, float gridSpacing, float cellHeight, float canvasScale)
    {
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

        for (int i = 0; i < _categories.Count; i++)
        {
            string category = _categories[i];
            bool hasChildren = _availableByMain.ContainsKey(category);
            TagCategoryButton button = TagCategoryButton.Create(content, category, Localization.Get(category), font,
                hasChildren ? ToggleMainCategory : ToggleCategory);

            if (hasChildren)
            {
                button.AddDropdownButton(() => ToggleDropdown(button, font), canvasScale);
                HoverTooltipArea tooltip = button.gameObject.AddComponent<HoverTooltipArea>();
                tooltip.SetMessageText(() => GroupTooltip(category));
            }

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
        if (_selection == null) return;

        if (!_selection.Add(category)) _selection.Remove(category);

        RefreshButtons();
    }

    private void ToggleMainCategory(string category)
    {
        if (_selection == null || !_availableByMain.TryGetValue(category, out List<string> children) ||
            children.Count == 0)
            return;

        if (_selection.IsSupersetOf(children))
            _selection.ExceptWith(children);
        else
            _selection.UnionWith(children);

        RefreshButtons();
    }

    private void RefreshButtons()
    {
        foreach (TagCategoryButton button in _buttons)
        {
            if (_availableByMain.TryGetValue(button.Category, out List<string> children))
            {
                int selectedCount = CountSelected(children);
                button.SetSelected(selectedCount == children.Count, selectedCount > 0 && selectedCount < children.Count);
            }
            else
            {
                button.SetSelected(_selection != null && _selection.Contains(button.Category));
            }
        }

        _dropdown?.Refresh();
    }

    private int CountSelected(List<string> categories)
    {
        if (_selection == null) return 0;

        int count = 0;

        foreach (string category in categories)
            if (_selection.Contains(category))
                count++;

        return count;
    }

    private string GroupTooltip(string category)
    {
        if (!_availableByMain.TryGetValue(category, out List<string> children)) return Localization.Get(category);

        return $"{Localization.Get(category)} ({CountSelected(children)}/{children.Count})";
    }

    private void ToggleDropdown(TagCategoryButton button, TMP_FontAsset font)
    {
        bool wasOpen = _dropdownButton == button;
        CloseDropdown();

        if (wasOpen || _root == null || !_availableByMain.TryGetValue(button.Category, out List<string> children)) return;

        _dropdown = TagCategoryDropdown.Create(_root, button.GetComponent<RectTransform>(), font,
            Localization.Get(button.Category), children, category => _selection != null && _selection.Contains(category),
            ToggleCategory, OnDropdownClosed);

        if (_dropdown == null) return;

        _dropdownButton = button;
        button.SetDropdownOpen(true);
    }

    private void CloseDropdown()
    {
        if (_dropdown != null) _dropdown.Close();

        OnDropdownClosed();
    }

    private void OnDropdownClosed()
    {
        if (_dropdownButton != null) _dropdownButton.SetDropdownOpen(false);

        _dropdownButton = null;
        _dropdown = null;
    }
}

public sealed class TagCategoryButton : MonoBehaviour
{
    private Image _background;
    private Button _dropdownControl;
    private TagDropdownArrowGraphic _dropdownArrow;
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
        button.targetGraphic = result._background;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(result.Click);

        return result;
    }

    public void SetSelected(bool selected, bool partiallySelected = false)
    {
        _background.color = partiallySelected
            ? Color.Lerp(SortTheme.CriterionDisabledBg, SortTheme.CategoryRowBg, 0.5f)
            : selected ? SortTheme.CategoryRowBg : SortTheme.CriterionDisabledBg;
        _label.color = selected || partiallySelected ? SortTheme.CategoryText : SortTheme.CriterionDisabledText;
    }

    public void AddDropdownButton(Action onClick, float canvasScale)
    {
        PhysicalPixelGrid pixelGrid = new PhysicalPixelGrid(canvasScale);
        float width = pixelGrid.Snap(SortTheme.TagCategorySubmenuWidth);
        _label.rectTransform.offsetMax = new Vector2(-SortTheme.TagCategoryTextPadding - width, 0f);

        GameObject buttonObject = new GameObject("Subcategories", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(width, 0f);
        rect.anchoredPosition = Vector2.zero;
        Image background = buttonObject.GetComponent<Image>();
        background.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        _dropdownControl = button;
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = SortTheme.TagCategoryToggleNormal;
        colors.highlightedColor = SortTheme.TagCategoryToggleHover;
        colors.pressedColor = SortTheme.TagCategoryTogglePressed;
        colors.selectedColor = SortTheme.TagCategoryToggleNormal;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = SortTheme.TagCategoryToggleFadeDuration;
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            UiSound.Play(EUISoundType.ButtonClick);
            onClick?.Invoke();
        });

        GameObject arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(TagDropdownArrowGraphic));
        arrowObject.transform.SetParent(rect, false);
        RectTransform arrowRect = arrowObject.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = new Vector2(pixelGrid.Snap(SortTheme.HandleWidth * 2f / 3f),
            pixelGrid.Snap(SortTheme.HandleWidth / 3f));
        _dropdownArrow = arrowObject.GetComponent<TagDropdownArrowGraphic>();
        _dropdownArrow.color = SortTheme.CategoryText;
        _dropdownArrow.raycastTarget = false;
    }

    public void SetDropdownOpen(bool open)
    {
        if (_dropdownArrow != null) _dropdownArrow.SetOpen(open);

        if (_dropdownControl == null) return;

        ColorBlock colors = _dropdownControl.colors;
        colors.normalColor = open ? SortTheme.RowSubmenuOpen : SortTheme.TagCategoryToggleNormal;
        colors.selectedColor = colors.normalColor;
        _dropdownControl.colors = colors;
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
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private void Click()
    {
        UiSound.Play(EUISoundType.MenuCheckBox);
        _onClick?.Invoke(Category);
    }
}

public sealed class TagDropdownArrowGraphic : MaskableGraphic
{
    private bool _open;

    public void SetOpen(bool open)
    {
        if (_open == open) return;

        _open = open;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        float baseY = _open ? rect.yMin : rect.yMax;
        float tipY = _open ? rect.yMax : rect.yMin;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = new Vector2(rect.xMin, baseY);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector2(rect.xMax, baseY);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector2(rect.center.x, tipY);
        vertexHelper.AddVert(vertex);
        vertexHelper.AddTriangle(0, 1, 2);
    }
}

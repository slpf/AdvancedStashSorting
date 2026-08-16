using System.Collections.Generic;
using AdvancedStashSorting.Sorting;
using EFT;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public static class SortOrderMenu
{
    private static GameObject _host;
    private static RectTransform _hostRect;
    private static GameObject _root;
    private static RectTransform _rootRect;
    private static RectTransform _rootContentRect;
    private static GameObject _subRoot;
    private static RectTransform _subRootRect;
    private static RectTransform _subContentRect;
    private static ReorderRow _subMenuParentRow;
    private static RectTransform _anchorRect;
    private static GameObject _backdrop;
    private static GameObject _overlay;
    private static Canvas _sourceCanvas;
    private static RectTransform _hostParent;
    private static bool _localeSubscribed;
    private static string _currentParent;
    private static PhysicalPixelGrid _pixelGrid;
    private static CycleSelector _rarityDirectionSelector;
    private static CycleSelector _rarityPresetSelector;
    private static readonly List<RarityColorRow> RarityColorRows = [];

    public static void Show(RectTransform buttonRect)
    {
        Canvas sourceCanvas = FindSourceCanvas(buttonRect, out RectTransform hostParent);

        if (sourceCanvas == null || hostParent == null) return;

        float currentScale = SortTheme.NormalizeScale(sourceCanvas.rootCanvas.scaleFactor);

        if (_root != null &&
            (_sourceCanvas != sourceCanvas || _hostParent != hostParent ||
             !Mathf.Approximately(_pixelGrid.CanvasScale, currentScale)))
            DestroyMenu();

        if (_root == null) Build(sourceCanvas, hostParent, currentScale);

        if (_root.activeSelf)
        {
            Hide();
            return;
        }

        if (!Place(buttonRect)) return;

        _anchorRect = buttonRect;
        _backdrop.SetActive(true);
        _root.SetActive(true);
        _overlay.SetActive(true);
        _host.transform.SetAsLastSibling();
        _backdrop.transform.SetAsLastSibling();
        _root.transform.SetAsLastSibling();
        _overlay.transform.SetAsLastSibling();
    }

    internal static void Hide()
    {
        if (_root != null) _root.SetActive(false);

        HideSubMenu();

        if (_backdrop != null) _backdrop.SetActive(false);

        if (_overlay != null) _overlay.SetActive(false);

        Config.Save();
    }

    private static void DestroyMenu()
    {
        if (_host != null)
        {
            _host.SetActive(false);
            Object.Destroy(_host);
        }

        Config.Save();

        _host = null;
        _hostRect = null;
        _backdrop = null;
        _overlay = null;
        _sourceCanvas = null;
        _hostParent = null;
        _root = null;
        _rootRect = null;
        _rootContentRect = null;
        _subRoot = null;
        _subRootRect = null;
        _subContentRect = null;
        _subMenuParentRow = null;
        _anchorRect = null;
        _currentParent = null;
        _rarityDirectionSelector = null;
        _rarityPresetSelector = null;
        RarityColorRows.Clear();
    }

    public static void HideIfAnchoredTo(RectTransform buttonRect)
    {
        if (_anchorRect == buttonRect) Hide();
    }

    public static void DestroyIfAnchoredTo(RectTransform buttonRect)
    {
        if (_anchorRect == buttonRect) DestroyMenu();
    }

    internal static void InvalidateCanvas(GameObject host)
    {
        if (_host == host) DestroyMenu();
    }

    private static void Build(Canvas sourceCanvas, RectTransform hostParent, float canvasScale)
    {
        _sourceCanvas = sourceCanvas;
        _hostParent = hostParent;
        _pixelGrid = new PhysicalPixelGrid(canvasScale);

        _host = new GameObject("SortOrderMenuHost", typeof(RectTransform), typeof(LayoutElement));
        _hostRect = _host.GetComponent<RectTransform>();
        _hostRect.SetParent(hostParent, false);
        _hostRect.anchorMin = Vector2.zero;
        _hostRect.anchorMax = Vector2.one;
        _hostRect.offsetMin = Vector2.zero;
        _hostRect.offsetMax = Vector2.zero;
        _host.GetComponent<LayoutElement>().ignoreLayout = true;
        _host.AddComponent<SortMenuCanvasMonitor>().Initialize(sourceCanvas.rootCanvas);

#if DEBUG
        RectTransform sourceRect = sourceCanvas.transform as RectTransform;
        Vector2 sourceSize = sourceRect != null ? sourceRect.rect.size : Vector2.zero;
        Plugin.LogSource?.LogInfo(
            $"Sort menu UI built in {hostParent.name} on {sourceCanvas.name}: root={sourceCanvas.rootCanvas.name}, overrideSorting={sourceCanvas.overrideSorting}, order={sourceCanvas.sortingOrder}, canvasSize={sourceSize.x:0.#}x{sourceSize.y:0.#}, resolution={Screen.width}x{Screen.height}, canvasScale={canvasScale:0.###}");
#endif

        Transform parent = _host.transform;

        _backdrop = new GameObject("SortOrderMenuBackdrop", typeof(RectTransform), typeof(Image),
            typeof(SortMenuBackdropInput));
        RectTransform backdropRect = _backdrop.GetComponent<RectTransform>();
        backdropRect.SetParent(parent, false);
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = _backdrop.GetComponent<Image>();
        backdropImage.color = SortTheme.Transparent;
        backdropImage.raycastTarget = true;

        _overlay = new GameObject("SortOrderMenuOverlay", typeof(RectTransform));
        RectTransform overlayRect = _overlay.GetComponent<RectTransform>();
        overlayRect.SetParent(parent, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        _root = new GameObject("SortOrderMenu", typeof(RectTransform), typeof(Image), typeof(Outline));
        _rootRect = _root.GetComponent<RectTransform>();
        _rootRect.SetParent(parent, false);
        _rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        _rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        _rootRect.pivot = new Vector2(0f, 0f);
        _rootRect.sizeDelta = new Vector2(_pixelGrid.Snap(SortTheme.Width), 0f);

        Image background = _root.GetComponent<Image>();
        background.color = SortTheme.PanelFill;
        background.raycastTarget = true;

        Outline border = _root.GetComponent<Outline>();
        border.effectColor = SortTheme.PanelBorder;
        float borderThickness = _pixelGrid.Snap(SortTheme.BorderThickness);
        border.effectDistance = new Vector2(borderThickness, borderThickness);
        _rootContentRect = CreatePanelContent(_rootRect);

        MakeHeader(_rootContentRect, Localization.Get("sortby"));

        ToggleRow separationRow = null;
        ToggleRow recursiveNestingRow = null;

        ReorderableList.Create(_rootContentRect, _overlay.transform, SortSettings.SortOrder, _pixelGrid,
            new ReorderableListOptions<SortTypeSetting>
            {
                GetName = setting => Localization.Get(setting.Type.ToString()),
                GetBackground = setting => setting.Enabled ? SortTheme.CategoryRowBg : SortTheme.CriterionDisabledBg,
                GetTextColor = setting => setting.Enabled ? SortTheme.CategoryText : SortTheme.CriterionDisabledText,
                OnClick = setting =>
                {
                    setting.Enabled = !setting.Enabled;
                    Config.MarkDirty();
                    RefreshSeparationRow(separationRow);
                },
                OnReorder = list =>
                {
                    SortSettings.SortOrder = list;
                    Config.MarkDirty();
                    RefreshSeparationRow(separationRow);
                },
                GetDirection = setting => setting.Direction == SortDirection.Ascending,
                OnToggleDirection = setting =>
                {
                    setting.Direction = setting.Direction == SortDirection.Ascending
                        ? SortDirection.Descending
                        : SortDirection.Ascending;
                    Config.MarkDirty();
                },
                OnBeginDrag = HideSubMenu,
                HasSubmenu = setting => setting.Type == SortType.Rarity,
                OnSubmenuClick = (row, _) => ShowRarityMenu(row),
                HasDirection = setting => setting.Type == SortType.Amount
            });

        MakeSpacer(_rootContentRect);
        MakeHeader(_rootContentRect, Localization.Get("categories"));

        List<string> mainOrder = CategoryCatalog.GetMainOrder();

        ReorderableList.Create(_rootContentRect, _overlay.transform, mainOrder, _pixelGrid,
            new ReorderableListOptions<string>
            {
                GetName = category => Localization.Get(category),
                GetBackground = _ => SortTheme.CategoryRowBg,
                GetTextColor = _ => SortTheme.CategoryText,
                OnReorder = list =>
                {
                    CategoryCatalog.ApplyMainOrder(list);
                    Config.MarkDirty();
                },
                OnBeginDrag = HideSubMenu,
                HasSubmenu = CategoryCatalog.HasChildren,
                OnSubmenuClick = (row, category) =>
                {
                    if (_currentParent == category || !CategoryCatalog.HasChildren(category))
                    {
                        HideSubMenu();
                        return;
                    }

                    ShowSubCategories(category, row);
                }
            });

        MakeSpacer(_rootContentRect);
        MakeHeader(_rootContentRect, Localization.Get("toggles"));

        ToggleRow.Create(_rootContentRect, Localization.Get("compact_sorting"), SortSettings.CompactSortingEnabled, b =>
        {
            SortSettings.CompactSortingEnabled = b;
            Config.MarkDirty();
            RefreshSeparationRow(separationRow);
        }, _pixelGrid);
        
        separationRow = ToggleRow.Create(_rootContentRect, Localization.Get("separation"), false, b =>
        {
            SortSettings.SeparationEnabled = b;
            Config.MarkDirty();
        }, _pixelGrid);
        RefreshSeparationRow(separationRow);

        ToggleRow.Create(_rootContentRect, Localization.Get("folding"), SortSettings.FoldingEnabled, b =>
        {
            SortSettings.FoldingEnabled = b;
            Config.MarkDirty();
        }, _pixelGrid);

        ToggleRow.Create(_rootContentRect, Localization.Get("stacking"), SortSettings.StackingEnabled, b =>
        {
            SortSettings.StackingEnabled = b;
            Config.MarkDirty();
        }, _pixelGrid);

        ToggleRow.Create(_rootContentRect, Localization.Get("nesting"), SortSettings.NestingEnabled, b =>
        {
            SortSettings.NestingEnabled = b;
            Config.MarkDirty();
            RefreshRecursiveNestingRow(recursiveNestingRow);
        }, _pixelGrid);

        recursiveNestingRow = ToggleRow.Create(_rootContentRect, Localization.Get("nesting_recursive"),
            SortSettings.RecursiveNestingEnabled, b =>
            {
                SortSettings.RecursiveNestingEnabled = b;
                Config.MarkDirty();
            }, _pixelGrid);
        RefreshRecursiveNestingRow(recursiveNestingRow);

        ResizePanel(_rootRect, _rootContentRect);

        _backdrop.SetActive(false);
        _overlay.SetActive(false);
        _root.SetActive(false);

        if (!_localeSubscribed)
        {
            _localeSubscribed = true;
            LocalizationManager.Instance.AddLocaleUpdateListener(OnLocaleChanged);
        }
    }

    private static void RefreshSeparationRow(ToggleRow separationRow)
    {
        if (separationRow == null) return;

        bool available = SortSettings.CanSeparateCategories();
        separationRow.SetInteractable(available);
        separationRow.SetValue(available && SortSettings.SeparationEnabled);
    }

    private static void RefreshRecursiveNestingRow(ToggleRow recursiveRow)
    {
        if (recursiveRow == null) return;

        bool available = SortSettings.NestingEnabled;
        recursiveRow.SetInteractable(available);
        recursiveRow.SetValue(available && SortSettings.RecursiveNestingEnabled);
    }

    private static void ShowSubCategories(string parent, ReorderRow row)
    {
        HideSubMenu();

        List<string> subOrder = CategoryCatalog.GetSubOrder(parent);

        if (subOrder.Count == 0) return;

        _currentParent = parent;
        CreateSubMenu(row);

        MakeHeader(_subContentRect, Localization.Get(parent));

        ReorderableList.Create(_subContentRect, _overlay.transform, subOrder, _pixelGrid,
            new ReorderableListOptions<string>
            {
                GetName = category => Localization.Get(category),
                GetBackground = _ => SortTheme.CategoryRowBg,
                GetTextColor = _ => SortTheme.CategoryText,
                OnReorder = list =>
                {
                    CategoryCatalog.ApplySubOrder(parent, list);
                    Config.MarkDirty();
                }
            });

        ResizePanel(_subRootRect, _subContentRect);
        ShowSubMenu(row.RectTransform, ShouldAnchorSubMenuToBottom(parent));
    }

    private static void ShowRarityMenu(ReorderRow row)
    {
        if (_subMenuParentRow == row)
        {
            HideSubMenu();
            return;
        }

        HideSubMenu();
        CreateSubMenu(row);

        _rarityDirectionSelector = CycleSelector.Create(_subContentRect, RarityDirectionLabel(),
            ToggleRarityDirection, ToggleRarityDirection, _pixelGrid);
        _rarityPresetSelector = CycleSelector.Create(_subContentRect, RarityPresetLabel(),
            () => ChangeRarityPreset(-1), () => ChangeRarityPreset(1), _pixelGrid);

        RefreshRarityRows();
        ShowSubMenu(row.RectTransform);
    }

    private static void CreateSubMenu(ReorderRow row)
    {
        _subMenuParentRow = row;
        _subMenuParentRow.SetSubmenuOpen(true);

        _subRoot = new GameObject("SortOrderSubMenu", typeof(RectTransform), typeof(Image), typeof(Outline));
        _subRootRect = _subRoot.GetComponent<RectTransform>();
        _subRootRect.SetParent(_root.transform.parent, false);
        _subRootRect.anchorMin = new Vector2(0.5f, 0.5f);
        _subRootRect.anchorMax = new Vector2(0.5f, 0.5f);
        _subRootRect.pivot = new Vector2(0f, 1f);
        _subRootRect.sizeDelta = new Vector2(_pixelGrid.Snap(SortTheme.Width), 0f);

        Image background = _subRoot.GetComponent<Image>();
        background.color = SortTheme.PanelFill;
        background.raycastTarget = true;

        Outline border = _subRoot.GetComponent<Outline>();
        border.effectColor = SortTheme.PanelBorder;
        float borderThickness = _pixelGrid.Snap(SortTheme.BorderThickness);
        border.effectDistance = new Vector2(borderThickness, borderThickness);
        _subContentRect = CreatePanelContent(_subRootRect);
    }

    private static void ShowSubMenu(RectTransform rowRect, bool anchorToBottom = false)
    {
        int cornerIndex = anchorToBottom ? 3 : 2;
        Vector2 anchor = GetScreenCorner(rowRect, cornerIndex);
        _subRootRect.pivot = anchorToBottom ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
        int offsetPixels = _pixelGrid.ToPixels(SortTheme.Spacing + SortTheme.SubMenuOffset);

        if (!SetScreenPosition(_subRootRect, new Vector2(anchor.x + offsetPixels, anchor.y)))
        {
            HideSubMenu();
            return;
        }

        _subRoot.transform.SetAsLastSibling();
        _overlay.transform.SetAsLastSibling();
    }

    private static bool ShouldAnchorSubMenuToBottom(string parent)
    {
        if (parent != "m_weapon_mods") return false;

        List<string> mainOrder = CategoryCatalog.GetMainOrder();
        int index = mainOrder.IndexOf(parent);
        return index >= 0 && index * 3 >= mainOrder.Count * 2;
    }

    private static void ToggleRarityDirection()
    {
        SortTypeSetting setting = SortSettings.Get(SortType.Rarity);

        if (setting == null) return;

        setting.Direction = setting.Direction == SortDirection.Ascending
            ? SortDirection.Descending
            : SortDirection.Ascending;
        Config.MarkDirty();
        _rarityDirectionSelector?.SetValue(RarityDirectionLabel());
    }

    private static void ChangeRarityPreset(int delta)
    {
        const int presetCount = 4;
        int preset = ((int)RaritySettings.ActivePreset + delta + presetCount) % presetCount;
        RaritySettings.ActivePreset = (RarityColorPreset)preset;
        RaritySettings.MarkTierLookupDirty();
        Config.MarkDirty();
        _rarityPresetSelector?.SetValue(RarityPresetLabel());
        RefreshRarityRows();
    }

    private static void RefreshRarityRows()
    {
        if (RaritySettings.ActivePreset != RarityColorPreset.Custom)
        {
            foreach (RarityColorRow t in RarityColorRows)
            {
                t.gameObject.SetActive(false);
                Object.Destroy(t.gameObject);
            }

            RarityColorRows.Clear();
            ResizePanel(_subRootRect, _subContentRect);
            return;
        }

        if (RarityColorRows.Count == 0)
            for (int i = 0; i < 7; i++)
            {
                int index = i;
                RarityColorRow colorRow = RarityColorRow.Create(
                    _subContentRect,
                    value =>
                    {
                        RaritySettings.CustomColors[index] = value;
                        RaritySettings.MarkTierLookupDirty();
                        Config.MarkDirty();
                    },
                    _pixelGrid);
                RarityColorRows.Add(colorRow);
            }

        for (int i = 0; i < RarityColorRows.Count; i++)
            RarityColorRows[i].SetValue(
                Localization.Get(RaritySettings.GetCustomLabelKey(i)),
                RaritySettings.GetColor(i));

        ResizePanel(_subRootRect, _subContentRect);
    }

    private static string RarityDirectionLabel()
    {
        return Localization.Get(SortSettings.IsAscending(SortType.Rarity) ? "ascending" : "descending");
    }

    private static string RarityPresetLabel()
    {
        return Localization.Get("rarity_preset_" + RaritySettings.ActivePreset);
    }

    private static RectTransform CreatePanelContent(RectTransform panelRect)
    {
        float padding = _pixelGrid.Snap(SortTheme.Padding);
        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.SetParent(panelRect, false);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = new Vector2(0f, -padding);
        contentRect.sizeDelta = new Vector2(-padding * 2f, 0f);

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset();
        layout.spacing = _pixelGrid.Snap(SortTheme.Spacing);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return contentRect;
    }

    private static void ResizePanel(RectTransform panelRect, RectTransform contentRect)
    {
        if (panelRect == null || contentRect == null) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        float contentHeight = _pixelGrid.Snap(Mathf.Max(0f, LayoutUtility.GetPreferredHeight(contentRect)));
        Vector2 contentSize = contentRect.sizeDelta;
        contentSize.y = contentHeight;
        contentRect.sizeDelta = contentSize;
        float padding = _pixelGrid.Snap(SortTheme.Padding);
        float panelHeight = _pixelGrid.Snap(contentHeight + padding * 2f);
        panelRect.sizeDelta = new Vector2(_pixelGrid.Snap(SortTheme.Width), panelHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
    }

    private static void MakeHeader(RectTransform parent, string text)
    {
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        header.transform.SetParent(parent, false);
        Image bg = header.GetComponent<Image>();
        bg.color = SortTheme.HeaderBg;
        bg.raycastTarget = false;
        header.GetComponent<LayoutElement>().preferredHeight = _pixelGrid.Snap(SortTheme.HeaderHeight);

        GameObject textGo = new GameObject("T", typeof(RectTransform));
        textGo.transform.SetParent(header.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        float textPadding = _pixelGrid.Snap(SortTheme.HeaderTextPadding);
        textRect.offsetMin = new Vector2(_pixelGrid.Snap(SortTheme.RowTextPadding), textPadding);
        textRect.offsetMax = new Vector2(-textPadding, -textPadding);
        TextMeshProUGUI t = textGo.AddComponent<TextMeshProUGUI>();
        UiLayout.SetDefaultFont(t);
        t.text = text;
        t.fontSize = SortTheme.HeaderFontSize;
        t.alignment = TextAlignmentOptions.Left;
        t.raycastTarget = false;
        t.color = SortTheme.HeaderText;
    }

    private static void MakeSpacer(RectTransform parent)
    {
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        LayoutElement le = spacer.GetComponent<LayoutElement>();
        le.preferredHeight = _pixelGrid.Snap(SortTheme.SpacerSize);
        le.flexibleHeight = 0f;
        le.flexibleWidth = 0f;
    }

    private static bool Place(RectTransform buttonRect)
    {
        Vector2 topRight = GetScreenCorner(buttonRect, 2);
        return SetScreenPosition(_rootRect, topRight);
    }

    private static bool SetScreenPosition(RectTransform rectTransform, Vector2 screenPoint)
    {
        if (_hostRect == null || _sourceCanvas == null) return false;

        screenPoint = PhysicalPixelGrid.SnapScreenPoint(screenPoint);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _hostRect,
                screenPoint,
                CanvasCamera(_sourceCanvas.rootCanvas),
                out Vector2 localPoint))
            return false;

        rectTransform.anchoredPosition = localPoint;
        return true;
    }

    private static Vector2 GetScreenCorner(RectTransform rectTransform, int cornerIndex)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
        Camera camera = CanvasCamera(rootCanvas);

        return RectTransformUtility.WorldToScreenPoint(camera, corners[cornerIndex]);
    }

    private static Canvas FindSourceCanvas(RectTransform buttonRect, out RectTransform hostParent)
    {
        hostParent = null;

        ItemUiContext itemUiContext = ItemUiContext.Instance;
        RectTransform contextMenuArea = itemUiContext != null ? itemUiContext.ContextMenuArea : null;
        Canvas contextCanvas = contextMenuArea != null ? contextMenuArea.GetComponentInParent<Canvas>() : null;

        if (contextMenuArea != null && contextMenuArea.gameObject.activeInHierarchy &&
            contextCanvas != null && contextCanvas.isActiveAndEnabled)
        {
            hostParent = contextMenuArea;
            return contextCanvas;
        }

        if (buttonRect == null) return null;

        Canvas buttonCanvas = buttonRect.GetComponentInParent<Canvas>();

        if (buttonCanvas == null) return null;

        if (HasActiveRaycaster(buttonCanvas))
        {
            hostParent = buttonCanvas.transform as RectTransform;
            return hostParent != null ? buttonCanvas : null;
        }

        Canvas[] canvases = buttonRect.GetComponentsInParent<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
            if (HasActiveRaycaster(canvases[i]))
            {
                hostParent = canvases[i].transform as RectTransform;
                return hostParent != null ? canvases[i] : null;
            }

        Plugin.LogSource?.LogError($"No active GraphicRaycaster found for sort button {buttonRect.name}.");
        return null;
    }

    private static bool HasActiveRaycaster(Canvas canvas)
    {
        if (canvas == null || !canvas.isActiveAndEnabled) return false;

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        return raycaster != null && raycaster.isActiveAndEnabled;
    }

    private static Camera CanvasCamera(Canvas canvas)
    {
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static void HideSubMenu()
    {
        if (_subMenuParentRow != null)
        {
            _subMenuParentRow.SetSubmenuOpen(false);
            _subMenuParentRow = null;
        }

        if (_subRoot != null)
        {
            _subRoot.SetActive(false);
            Object.Destroy(_subRoot);
            _subRoot = null;
            _subRootRect = null;
            _subContentRect = null;
        }

        _currentParent = null;
        _rarityDirectionSelector = null;
        _rarityPresetSelector = null;
        RarityColorRows.Clear();
    }

    private static void OnLocaleChanged()
    {
        DestroyMenu();
    }
}

public sealed class SortMenuBackdropInput : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left ||
            eventData.button == PointerEventData.InputButton.Right)
            SortOrderMenu.Hide();
    }
}

using System;
using System.Collections.Generic;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public sealed class ReorderableListOptions<T>
{
    public Func<T, string> GetName { get; set; }
    public Func<T, Color> GetBackground { get; set; }
    public Func<T, Color> GetTextColor { get; set; }
    public Action<T> OnClick { get; set; }
    public Action<List<T>> OnReorder { get; set; }
    public Func<T, bool> GetDirection { get; set; }
    public Action<T> OnToggleDirection { get; set; }
    public Action OnBeginDrag { get; set; }
    public Func<T, bool> HasSubmenu { get; set; }
    public Action<ReorderRow, T> OnSubmenuClick { get; set; }
    public Func<T, bool> HasDirection { get; set; }
}

public class ReorderableList : MonoBehaviour
{
    private readonly List<ReorderRow> _rows = [];
    private RectTransform _content;
    private bool _dragActive;
    private ReorderRow _draggedRow;

    private float _dragOriginContentX;
    private float _dragOriginZ;
    private int _dragStartIndex = -1;
    private float _listMaxY;
    private float _listMinY;
    private Action _onBeginDrag;

    private Action<IReadOnlyList<ReorderRow>> _onReorder;
    private Transform _overlay;
    private PhysicalPixelGrid _pixelGrid;
    private GameObject _placeholder;

    private void OnDisable()
    {
        CancelDrag();
    }

    public static void Create<T>(Transform parent, Transform overlay, IReadOnlyList<T> items,
        PhysicalPixelGrid pixelGrid, ReorderableListOptions<T> options)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        if (overlay == null) throw new ArgumentNullException(nameof(overlay));

        if (items == null) throw new ArgumentNullException(nameof(items));

        if (options == null) throw new ArgumentNullException(nameof(options));

        if (options.GetName == null) throw new ArgumentNullException(nameof(options.GetName));

        if (options.GetBackground == null) throw new ArgumentNullException(nameof(options.GetBackground));

        if (options.GetTextColor == null) throw new ArgumentNullException(nameof(options.GetTextColor));

        if (options.GetDirection == null != (options.OnToggleDirection == null))
            throw new ArgumentException("Direction getter and toggle action must be provided together.");

        GameObject contentGo = new GameObject("ReorderableContent", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentGo.transform.SetParent(parent, false);
        RectTransform contentRect = contentGo.GetComponent<RectTransform>();
        VerticalLayoutGroup contentLayout = contentGo.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset();
        contentLayout.spacing = pixelGrid.Snap(SortTheme.CategorySpacing);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childAlignment = TextAnchor.UpperCenter;

        ReorderableList list = contentGo.AddComponent<ReorderableList>();
        list._content = contentRect;
        list._overlay = overlay;
        list._pixelGrid = pixelGrid;
        list._onBeginDrag = options.OnBeginDrag;

        Dictionary<ReorderRow, T> itemsByRow = new Dictionary<ReorderRow, T>();
        for (int i = 0; i < items.Count; i++)
        {
            T item = items[i];
            ReorderRow row = list.AddRow(item, options);
            itemsByRow.Add(row, item);
        }

        if (options.OnReorder != null)
            list._onReorder = rows =>
            {
                List<T> reorderedItems = new List<T>(rows.Count);
                for (int i = 0; i < rows.Count; i++) reorderedItems.Add(itemsByRow[rows[i]]);

                options.OnReorder(reorderedItems);
            };
    }

    private ReorderRow AddRow<T>(T item, ReorderableListOptions<T> options)
    {
        GameObject go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(_content, false);
        RectTransform rowRect = go.GetComponent<RectTransform>();

        Image bg = go.GetComponent<Image>();
        bg.color = options.GetBackground(item);
        bg.raycastTarget = true;

        go.GetComponent<LayoutElement>().preferredHeight = _pixelGrid.Snap(SortTheme.CategoryRowHeight);

        RectTransform rowContent = UiLayout.CreateHorizontalContent(
            rowRect,
            _pixelGrid.Snap(SortTheme.RowTextPadding),
            _pixelGrid.Snap(6f),
            _pixelGrid.Snap(6f),
            true,
            TextAnchor.MiddleLeft);

        ReorderRow row = go.AddComponent<ReorderRow>();
        _rows.Add(row);

        GameObject textGo = new GameObject("T", typeof(RectTransform));
        textGo.transform.SetParent(rowContent, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        UiLayout.SetDefaultFont(text);
        text.text = options.GetName(item);
        text.fontSize = SortTheme.CategoryFontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.color = options.GetTextColor(item);
        LayoutElement textLe = textGo.AddComponent<LayoutElement>();
        textLe.flexibleWidth = 1f;

        row.Init(this, bg, text, () => options.GetBackground(item), () => options.GetTextColor(item),
            options.OnClick == null ? null : _ => options.OnClick(item));

        if (options.GetDirection != null && options.OnToggleDirection != null &&
            (options.HasDirection == null || options.HasDirection(item)))
            AddDirectionMarker(rowContent, row, item, options);

        if (options.HasSubmenu != null && options.HasSubmenu(item))
            AddSubmenuButton(rowContent, row,
                options.OnSubmenuClick == null ? null : clickedRow => options.OnSubmenuClick(clickedRow, item));

        row.RefreshColor();

        return row;
    }

    private void AddDirectionMarker<T>(Transform parent, ReorderRow row, T item, ReorderableListOptions<T> options)
    {
        float width = _pixelGrid.Snap(SortTheme.HandleWidth);
        float thickness = _pixelGrid.Snap(SortTheme.HandleBarHeight);
        float gap = _pixelGrid.Snap(SortTheme.HandleBarSpacing);

        GameObject go = new GameObject("Dir", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image bg = go.GetComponent<Image>();
        bg.color = SortTheme.Transparent;
        bg.raycastTarget = true;

        ConfigureBarsLayout(go.GetComponent<VerticalLayoutGroup>(), gap);
        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
        le.preferredHeight = thickness * 3 + gap * 2;
        le.flexibleHeight = 1f;

        LayoutElement[] bars = AddBars(go.transform, DirectionWidths(options.GetDirection(item)), thickness);
        HoverTooltipArea tooltip = go.AddComponent<HoverTooltipArea>();
        tooltip.SetMessageText(() => Localization.Get(options.GetDirection(item) ? "ascending" : "descending"));

        DirectionToggle toggle = go.AddComponent<DirectionToggle>();
        toggle.Init(row, () =>
        {
            UiSound.Play(EUISoundType.ButtonClick);
            options.OnToggleDirection(item);
            SetBarWidths(bars, DirectionWidths(options.GetDirection(item)));
            tooltip.Show();
        });
    }

    private void AddSubmenuButton(Transform parent, ReorderRow row, Action<ReorderRow> onClick)
    {
        GameObject container = new GameObject("SubmenuButtonContainer", typeof(RectTransform), typeof(LayoutElement));
        container.transform.SetParent(parent, false);

        LayoutElement layoutElement = container.GetComponent<LayoutElement>();
        float handleWidth = _pixelGrid.Snap(SortTheme.HandleWidth);
        layoutElement.preferredWidth = handleWidth;
        layoutElement.minWidth = handleWidth;
        layoutElement.flexibleHeight = 1f;

        GameObject chevron = new GameObject("SubmenuButton", typeof(RectTransform), typeof(ChevronGraphic));
        chevron.transform.SetParent(container.transform, false);
        RectTransform chevronRect = chevron.GetComponent<RectTransform>();
        chevronRect.anchorMin = new Vector2(0.5f, 0.5f);
        chevronRect.anchorMax = new Vector2(0.5f, 0.5f);
        chevronRect.pivot = new Vector2(0.5f, 0.5f);
        chevronRect.sizeDelta = new Vector2(handleWidth, handleWidth);
        chevronRect.anchoredPosition = Vector2.zero;

        ChevronGraphic graphic = chevron.GetComponent<ChevronGraphic>();
        graphic.Init(_pixelGrid.Snap(SortTheme.ToggleCheckThickness), SortTheme.HandleColor);
        row.SetSubmenuChevron(graphic);

        if (onClick != null)
        {
            GameObject hitArea = new GameObject("SubmenuButtonHitArea", typeof(RectTransform), typeof(Image), typeof(Button));
            hitArea.transform.SetParent(container.transform, false);
            RectTransform hitRect = hitArea.GetComponent<RectTransform>();
            hitRect.anchorMin = new Vector2(1f, 0f);
            hitRect.anchorMax = new Vector2(1f, 1f);
            hitRect.pivot = new Vector2(1f, 0.5f);
            hitRect.sizeDelta = new Vector2(_pixelGrid.Snap(SortTheme.SubmenuHitWidth), 0f);
            hitRect.anchoredPosition = Vector2.zero;

            Image hitImage = hitArea.GetComponent<Image>();
            hitImage.color = SortTheme.Transparent;
            hitImage.raycastTarget = true;

            Button button = hitArea.GetComponent<Button>();
            button.targetGraphic = hitImage;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                UiSound.Play(EUISoundType.ButtonClick);
                onClick(row);
            });
        }
    }

    private static void ConfigureBarsLayout(VerticalLayoutGroup vlg, float gap)
    {
        vlg.padding = new RectOffset();
        vlg.spacing = gap;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.MiddleRight;
    }

    private static LayoutElement[] AddBars(Transform parent, float[] widths, float thickness)
    {
        LayoutElement[] bars = new LayoutElement[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            bar.transform.SetParent(parent, false);
            Image img = bar.GetComponent<Image>();
            img.color = SortTheme.HandleColor;
            img.raycastTarget = false;
            LayoutElement ble = bar.GetComponent<LayoutElement>();
            ble.preferredWidth = widths[i];
            ble.minWidth = widths[i];
            ble.flexibleWidth = 0f;
            ble.preferredHeight = thickness;
            ble.flexibleHeight = 0f;
            bars[i] = ble;
        }

        return bars;
    }

    private static void SetBarWidths(LayoutElement[] bars, float[] widths)
    {
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i].preferredWidth = widths[i];
            bars[i].minWidth = widths[i];
        }
    }

    private float[] DirectionWidths(bool ascending)
    {
        int widthPixels = _pixelGrid.ToPixels(SortTheme.HandleWidth);
        int smallPixels = Math.Max(2, widthPixels / 3);
        int mediumPixels = Math.Max(smallPixels + 1, widthPixels * 2 / 3);
        float width = _pixelGrid.FromPixels(widthPixels);
        float small = _pixelGrid.FromPixels(smallPixels);
        float medium = _pixelGrid.FromPixels(mediumPixels);
        return ascending ? [small, medium, width] : [width, medium, small];
    }

    public void BeginDrag(ReorderRow row)
    {
        if (_dragActive) CancelDrag();

        _onBeginDrag?.Invoke();

        _overlay.SetAsLastSibling();

        int index = row.transform.GetSiblingIndex();
        _draggedRow = row;
        _dragStartIndex = index;
        _dragActive = true;
        _dragOriginContentX = row.RectTransform.localPosition.x;
        _dragOriginZ = row.transform.position.z;

        _placeholder = CreatePlaceholder();
        _placeholder.transform.SetParent(_content, false);
        _placeholder.transform.SetSiblingIndex(index);

        row.transform.SetParent(_overlay, true);
        row.transform.SetAsLastSibling();
        row.SetColor(SortTheme.RowDrag);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        float halfRow = row.RectTransform.rect.height * 0.5f;
        _listMinY = _content.rect.yMin + halfRow;
        _listMaxY = _content.rect.yMax - halfRow;
    }

    public void DragUpdate(ReorderRow row, PointerEventData eventData)
    {
        if (!_dragActive || row != _draggedRow || _placeholder == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, eventData.position,
                eventData.pressEventCamera, out Vector2 local)) return;

        float clampedY = Mathf.Clamp(local.y, _listMinY, _listMaxY);
        Vector3 world = _content.TransformPoint(new Vector3(_dragOriginContentX, clampedY, 0f));
        row.transform.position = new Vector3(world.x, world.y, _dragOriginZ);

        int target = 0;

        foreach (ReorderRow other in _rows)
        {
            if (other == row) continue;

            if (other.transform.localPosition.y > local.y) target++;
        }

        int maxIndex = _content.childCount - 1;

        if (target > maxIndex) target = maxIndex;

        if (_placeholder.transform.GetSiblingIndex() != target)
        {
            _placeholder.transform.SetSiblingIndex(target);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }
    }

    public void EndDrag(ReorderRow row)
    {
        if (!_dragActive || row != _draggedRow) return;

        int target = _placeholder != null ? _placeholder.transform.GetSiblingIndex() : _dragStartIndex;
        bool orderChanged = target != _dragStartIndex;

        DestroyPlaceholder();

        row.transform.SetParent(_content, false);
        row.transform.SetSiblingIndex(target);
        row.RefreshColor();

        ClearDragState();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        CommitOrder(orderChanged);
    }

    private void CancelDrag()
    {
        if (!_dragActive) return;

        ReorderRow row = _draggedRow;
        int target = _dragStartIndex;

        DestroyPlaceholder();

        if (row != null && _content != null)
        {
            row.transform.SetParent(_content, false);
            target = Mathf.Clamp(target, 0, _content.childCount - 1);
            row.transform.SetSiblingIndex(target);
            row.RefreshColor();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        ClearDragState();
    }

    private void DestroyPlaceholder()
    {
        if (_placeholder == null) return;

        GameObject placeholder = _placeholder;
        _placeholder = null;
        placeholder.transform.SetParent(null, false);
        Destroy(placeholder);
    }

    private GameObject CreatePlaceholder()
    {
        GameObject placeholder = new GameObject("Placeholder", typeof(RectTransform), typeof(Image), typeof(LayoutElement),
            typeof(Outline));

        Image image = placeholder.GetComponent<Image>();
        image.color = SortTheme.PlaceholderFill;
        image.raycastTarget = false;

        LayoutElement layoutElement = placeholder.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = _pixelGrid.Snap(SortTheme.CategoryRowHeight);

        Outline outline = placeholder.GetComponent<Outline>();
        outline.effectColor = SortTheme.PlaceholderBorder;
        float borderThickness = _pixelGrid.Snap(SortTheme.BorderThickness);
        outline.effectDistance = new Vector2(borderThickness, borderThickness);
        outline.useGraphicAlpha = true;

        return placeholder;
    }

    private void ClearDragState()
    {
        _draggedRow = null;
        _dragStartIndex = -1;
        _dragActive = false;
    }

    private void CommitOrder(bool notify)
    {
        _rows.Clear();

        for (int i = 0; i < _content.childCount; i++)
        {
            ReorderRow row = _content.GetChild(i).GetComponent<ReorderRow>();
            if (row != null) _rows.Add(row);
        }

        if (notify) _onReorder?.Invoke(_rows);
    }
}

public class ReorderRow : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler,
    IEndDragHandler
{
    private Image _bg;
    private Func<Color> _getBg;
    private Func<Color> _getText;
    private ReorderableList _list;
    private Action<ReorderRow> _onClick;
    private ChevronGraphic _submenuChevron;
    private bool _submenuOpen;
    private TextMeshProUGUI _text;

    public bool Dragged { get; private set; }

    public RectTransform RectTransform => (RectTransform)transform;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        Dragged = true;
        _list.BeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && Dragged) _list.DragUpdate(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && Dragged) _list.EndDrag(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || Dragged || _onClick == null) return;

        _onClick(this);
        RefreshColor();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) Dragged = false;
    }

    public void Init(
        ReorderableList list,
        Image bg,
        TextMeshProUGUI text,
        Func<Color> getBg,
        Func<Color> getText,
        Action<ReorderRow> onClick)
    {
        _list = list;
        _bg = bg;
        _text = text;
        _getBg = getBg;
        _getText = getText;
        _onClick = onClick;
    }

    public void SetColor(Color color)
    {
        if (_bg != null) _bg.color = color;
    }

    public void RefreshColor()
    {
        if (_bg != null) _bg.color = _submenuOpen ? SortTheme.RowSubmenuOpen : _getBg();

        if (_text != null) _text.color = _getText();
    }

    public void SetSubmenuOpen(bool value)
    {
        _submenuOpen = value;
        if (_submenuChevron != null) _submenuChevron.SetOpen(value);

        RefreshColor();
    }

    public void SetSubmenuChevron(ChevronGraphic submenuChevron)
    {
        _submenuChevron = submenuChevron;
        _submenuChevron.SetOpen(_submenuOpen);
    }

    public void ResetDragged()
    {
        Dragged = false;
    }
}

public class ChevronGraphic : MaskableGraphic
{
    private bool _pointsLeft;
    private float _thickness;

    public void Init(float thickness, Color chevronColor)
    {
        _thickness = thickness;
        color = chevronColor;
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void SetOpen(bool value)
    {
        if (_pointsLeft == value) return;

        _pointsLeft = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        float availableSize = Mathf.Min(rect.width, rect.height);

        float size = availableSize * 0.50f;
        Vector2 center = rect.center;
        float horizontalDirection = _pointsLeft ? -1f : 1f;
        float backX = center.x - horizontalDirection * size * 0.25f;
        float pointX = center.x + horizontalDirection * size * 0.25f;
        Vector2 top = new Vector2(backX, center.y + size * 0.5f);
        Vector2 middle = new Vector2(pointX, center.y);
        Vector2 bottom = new Vector2(backX, center.y - size * 0.5f);
        Color32 vertexColor = color;

        AddButton(vertexHelper, top, middle, bottom, _thickness, vertexColor);
    }

    private static void AddButton(VertexHelper vertexHelper, Vector2 top, Vector2 middle, Vector2 bottom,
        float thickness, Color32 color)
    {
        float halfThickness = thickness * 0.5f;
        Vector2 topDirection = (middle - top).normalized;
        Vector2 bottomDirection = (bottom - middle).normalized;
        Vector2 topNormal = new Vector2(-topDirection.y, topDirection.x) * halfThickness;
        Vector2 bottomNormal = new Vector2(-bottomDirection.y, bottomDirection.x) * halfThickness;
        Vector2 miterDirection = (topNormal + bottomNormal).normalized;
        float miterScale = halfThickness / Mathf.Max(0.001f, Vector2.Dot(miterDirection, topNormal.normalized));
        Vector2 miter = miterDirection * miterScale;

        int vertexIndex = vertexHelper.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = top + topNormal;
        vertexHelper.AddVert(vertex);
        vertex.position = top - topNormal;
        vertexHelper.AddVert(vertex);
        vertex.position = middle + miter;
        vertexHelper.AddVert(vertex);
        vertex.position = middle - miter;
        vertexHelper.AddVert(vertex);
        vertex.position = bottom + bottomNormal;
        vertexHelper.AddVert(vertex);
        vertex.position = bottom - bottomNormal;
        vertexHelper.AddVert(vertex);

        vertexHelper.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 3);
        vertexHelper.AddTriangle(vertexIndex, vertexIndex + 3, vertexIndex + 2);
        vertexHelper.AddTriangle(vertexIndex + 2, vertexIndex + 3, vertexIndex + 5);
        vertexHelper.AddTriangle(vertexIndex + 2, vertexIndex + 5, vertexIndex + 4);
    }
}

public class DirectionToggle : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    private Action _onClick;
    private ReorderRow _row;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _row == null || _row.Dragged) return;

        _onClick?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && _row != null) _row.ResetDragged();
    }

    public void Init(ReorderRow row, Action onClick)
    {
        _row = row;
        _onClick = onClick;
    }
}
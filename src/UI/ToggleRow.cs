using System;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public class ToggleRow : MonoBehaviour, IPointerClickHandler
{
    private CanvasGroup _canvasGroup;
    private GameObject _check;
    private bool _interactable = true;
    private Action<bool> _onChange;
    private bool _value;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_interactable || eventData.button != PointerEventData.InputButton.Left) return;

        UiSound.Play(EUISoundType.MenuCheckBox);

        _value = !_value;

        if (_check != null) _check.SetActive(_value);

        _onChange?.Invoke(_value);
    }

    public static ToggleRow Create(Transform parent, string label, bool initial, Action<bool> onChange,
        PhysicalPixelGrid pixelGrid)
    {
        GameObject go = new GameObject("ToggleRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement),
            typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        RectTransform rowRect = go.GetComponent<RectTransform>();
        Image rowBg = go.GetComponent<Image>();
        rowBg.color = SortTheme.Transparent;
        rowBg.raycastTarget = true;
        float rowHeight = pixelGrid.Snap(SortTheme.CategoryRowHeight);
        go.GetComponent<LayoutElement>().preferredHeight = rowHeight;

        RectTransform rowContent = UiLayout.CreateHorizontalContent(
            rowRect,
            pixelGrid.Snap(SortTheme.RowTextPadding),
            pixelGrid.Snap(6f),
            pixelGrid.Snap(8f),
            false,
            TextAnchor.MiddleLeft);

        GameObject textGo = new GameObject("T", typeof(RectTransform));
        textGo.transform.SetParent(rowContent, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        UiLayout.SetDefaultFont(text);
        text.text = label;
        text.fontSize = SortTheme.CategoryFontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;
        text.color = SortTheme.CategoryText;
        LayoutElement textLe = textGo.AddComponent<LayoutElement>();
        textLe.flexibleWidth = 1f;
        textLe.preferredHeight = rowHeight;

        GameObject boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        boxGo.transform.SetParent(rowContent, false);
        Image boxImg = boxGo.GetComponent<Image>();
        boxImg.color = SortTheme.HandleColor;
        boxImg.raycastTarget = false;
        LayoutElement boxLe = boxGo.GetComponent<LayoutElement>();
        float boxSize = pixelGrid.Snap(SortTheme.ToggleBoxSize);
        boxLe.preferredWidth = boxSize;
        boxLe.minWidth = boxSize;
        boxLe.preferredHeight = boxSize;
        boxLe.minHeight = boxSize;
        boxLe.flexibleHeight = 0f;

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(boxGo.transform, false);
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        float borderThickness = pixelGrid.Snap(SortTheme.BorderThickness);
        fillRect.offsetMin = new Vector2(borderThickness, borderThickness);
        fillRect.offsetMax = new Vector2(-borderThickness, -borderThickness);
        Image fillImg = fillGo.GetComponent<Image>();
        fillImg.color = SortTheme.PanelFill;
        fillImg.raycastTarget = false;

        GameObject check = MakeCheckmark(
            boxGo.transform,
            pixelGrid.Snap(SortTheme.ToggleCheckThickness),
            pixelGrid.Snap(2f),
            SortTheme.CategoryText);
        check.SetActive(initial);

        ToggleRow row = go.AddComponent<ToggleRow>();
        row._check = check;
        row._canvasGroup = go.GetComponent<CanvasGroup>();
        row._value = initial;
        row._onChange = onChange;

        return row;
    }

    public void SetInteractable(bool interactable)
    {
        _interactable = interactable;

        if (_canvasGroup != null)
        {
            _canvasGroup.interactable = interactable;
            _canvasGroup.blocksRaycasts = interactable;
            _canvasGroup.alpha = interactable ? 1f : SortTheme.DisabledAlpha;
        }
    }

    public void SetValue(bool value)
    {
        _value = value;

        if (_check != null) _check.SetActive(value);
    }

    private static GameObject MakeCheckmark(Transform parent, float thickness, float margin, Color color)
    {
        GameObject go = new GameObject("Check", typeof(RectTransform), typeof(CheckmarkGraphic));
        go.transform.SetParent(parent, false);

        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margin, margin);
        r.offsetMax = new Vector2(-margin, -margin);

        CheckmarkGraphic graphic = go.GetComponent<CheckmarkGraphic>();
        graphic.Init(thickness, color);

        return go;
    }
}

public class CheckmarkGraphic : MaskableGraphic
{
    private float _thickness;

    public void Init(float thickness, Color checkColor)
    {
        _thickness = thickness;
        color = checkColor;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        Vector2 p0 = new Vector2(rect.xMin + rect.width * 0.10f, rect.yMin + rect.height * 0.52f);
        Vector2 p1 = new Vector2(rect.xMin + rect.width * 0.38f, rect.yMin + rect.height * 0.24f);
        Vector2 p2 = new Vector2(rect.xMin + rect.width * 0.90f, rect.yMin + rect.height * 0.80f);
        Color32 vertexColor = color;

        AddSegment(vertexHelper, p0, p1, _thickness, vertexColor);
        AddSegment(vertexHelper, p1, p2, _thickness, vertexColor);
    }

    private static void AddSegment(VertexHelper vertexHelper, Vector2 start, Vector2 end, float thickness,
        Color32 color)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);
        int vertexIndex = vertexHelper.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = start + normal;
        vertexHelper.AddVert(vertex);
        vertex.position = start - normal;
        vertexHelper.AddVert(vertex);
        vertex.position = end - normal;
        vertexHelper.AddVert(vertex);
        vertex.position = end + normal;
        vertexHelper.AddVert(vertex);

        vertexHelper.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
        vertexHelper.AddTriangle(vertexIndex, vertexIndex + 2, vertexIndex + 3);
    }
}
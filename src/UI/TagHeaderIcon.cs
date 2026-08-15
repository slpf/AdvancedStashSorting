using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public static class TagHeaderSprites
{
    private static Sprite _filter;
    private static Sprite _grid;

    public static Sprite Filter()
    {
        if (_filter == null) _filter = CreateFilter();

        return _filter;
    }

    public static Sprite Grid()
    {
        if (_grid == null) _grid = CreateGrid();

        return _grid;
    }

    private static Sprite CreateFilter()
    {
        int textureSize = SortTheme.TagCategoryIconTextureSize;

        Vector2[] points =
        [
            Point(textureSize, 0.08f, 0.84f),
            Point(textureSize, 0.92f, 0.84f),
            Point(textureSize, 0.62f, 0.5f),
            Point(textureSize, 0.62f, 0.2f),
            Point(textureSize, 0.44f, 0.1f),
            Point(textureSize, 0.44f, 0.5f),
            Point(textureSize, 0.08f, 0.84f)
        ];

        Color[] pixels = new Color[textureSize * textureSize];
        float halfStroke = SortTheme.TagCategoryFilterSpriteStroke * 0.5f;

        for (int y = 0; y < textureSize; y++)
        for (int x = 0; x < textureSize; x++)
        {
            Vector2 sample = new Vector2(x + 0.5f, y + 0.5f);
            float distance = float.MaxValue;

            for (int i = 0; i < points.Length - 1; i++)
                distance = Mathf.Min(distance, DistanceToSegment(sample, points[i], points[i + 1]));

            float alpha = Coverage(distance - halfStroke);
            pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
        }

        return CreateSprite("AdvancedStashSortingFilter", textureSize, pixels);
    }

    private static Sprite CreateGrid()
    {
        int textureSize = SortTheme.TagCategoryIconTextureSize;
        float padding = SortTheme.TagCategoryGridSpritePadding;
        float gap = SortTheme.TagCategoryGridSpriteGap;
        float cellSize = (textureSize - padding * 2f - gap) * 0.5f;
        float radius = SortTheme.TagCategoryGridSpriteRadius;

        Vector2[] centers =
        [
            new(padding + cellSize * 0.5f, padding + cellSize * 1.5f + gap),
            new(padding + cellSize * 1.5f + gap, padding + cellSize * 1.5f + gap),
            new(padding + cellSize * 0.5f, padding + cellSize * 0.5f),
            new(padding + cellSize * 1.5f + gap, padding + cellSize * 0.5f)
        ];

        Color[] pixels = new Color[textureSize * textureSize];
        Vector2 halfSize = new Vector2(cellSize * 0.5f, cellSize * 0.5f);

        for (int y = 0; y < textureSize; y++)
        for (int x = 0; x < textureSize; x++)
        {
            Vector2 sample = new Vector2(x + 0.5f, y + 0.5f);
            float distance = float.MaxValue;

            foreach (Vector2 t in centers) distance = Mathf.Min(distance, RoundedBoxDistance(sample, t, halfSize, radius));

            pixels[y * textureSize + x] = new Color(1f, 1f, 1f, Coverage(distance));
        }

        return CreateSprite("AdvancedStashSortingGrid", textureSize, pixels);
    }

    private static Sprite CreateSprite(string name, int textureSize, Color[] pixels)
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true, true)
        {
            name = name,
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0,
            hideFlags = HideFlags.HideAndDontSave
        };

        texture.SetPixels(pixels);
        texture.Apply(true, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize,
            0u,
            SpriteMeshType.FullRect);

        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;

        return sprite;
    }

    private static float Coverage(float distance)
    {
        float antialias = Mathf.Max(0.01f, SortTheme.TagCategoryIconAntialias);
        return Mathf.Clamp01(0.5f - distance / antialias);
    }

    private static Vector2 Point(int textureSize, float x, float y)
    {
        return new Vector2(x * textureSize, y * textureSize);
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;

        if (lengthSquared <= Mathf.Epsilon) return Vector2.Distance(point, start);

        float position = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return Vector2.Distance(point, start + segment * position);
    }

    private static float RoundedBoxDistance(Vector2 point, Vector2 center, Vector2 halfSize, float radius)
    {
        Vector2 roundedHalfSize = halfSize - new Vector2(radius, radius);
        Vector2 distance = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y)) - roundedHalfSize;
        Vector2 outside = new Vector2(Mathf.Max(distance.x, 0f), Mathf.Max(distance.y, 0f));
        return outside.magnitude + Mathf.Min(Mathf.Max(distance.x, distance.y), 0f) - radius;
    }
}

public sealed class TagToggleBackground : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        float chamfer = Mathf.Clamp(SortTheme.TagCategoryToggleChamfer, 0f, Mathf.Min(rect.width, rect.height));
        int index = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMin), color, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMax - chamfer), color, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMin + chamfer, rect.yMax), color, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMax), color, Vector2.zero);
        vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMin), color, Vector2.zero);
        vertexHelper.AddTriangle(index, index + 1, index + 2);
        vertexHelper.AddTriangle(index, index + 2, index + 3);
        vertexHelper.AddTriangle(index, index + 3, index + 4);
    }
}

public sealed class TagToggleButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    private Graphic _background;
    private bool _hovered;

    private void OnDisable()
    {
        _hovered = false;
        SetColor(SortTheme.TagCategoryToggleNormal, 0f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetColor(SortTheme.TagCategoryTogglePressed, 0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        SetColor(SortTheme.TagCategoryToggleHover, SortTheme.TagCategoryToggleFadeDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        SetColor(SortTheme.TagCategoryToggleNormal, SortTheme.TagCategoryToggleFadeDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetColor(_hovered ? SortTheme.TagCategoryToggleHover : SortTheme.TagCategoryToggleNormal, 0f);
    }

    public void Initialize(Graphic background)
    {
        _background = background;
        SetColor(SortTheme.TagCategoryToggleNormal, 0f);
    }

    private void SetColor(Color value, float duration)
    {
        _background?.CrossFadeColor(value, duration, true, true);
    }
}
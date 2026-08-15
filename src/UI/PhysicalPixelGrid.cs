using UnityEngine;

namespace AdvancedStashSorting.UI;

public readonly struct PhysicalPixelGrid
{
    public PhysicalPixelGrid(float canvasScale)
    {
        CanvasScale = SortTheme.NormalizeScale(canvasScale);
    }

    public float CanvasScale { get; }

    public int ToPixels(float value)
    {
        if (value <= 0f) return 0;

        return Mathf.Max(1, Mathf.RoundToInt(value * CanvasScale));
    }

    public float FromPixels(int pixels)
    {
        return pixels <= 0 ? 0f : pixels / CanvasScale;
    }

    public float Snap(float value)
    {
        return FromPixels(ToPixels(value));
    }

    public static Vector2 SnapScreenPoint(Vector2 point)
    {
        return new Vector2(Mathf.Round(point.x), Mathf.Round(point.y));
    }
}
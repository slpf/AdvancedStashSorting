using UnityEngine;

namespace AdvancedStashSorting.UI;

public sealed class SortMenuCanvasMonitor : MonoBehaviour
{
    private Canvas _canvas;
    private float _scale;
    private Vector2 _size;

    private void LateUpdate()
    {
        if (_canvas == null)
        {
            SortOrderMenu.InvalidateCanvas(gameObject);
            return;
        }

        RectTransform canvasRect = _canvas.transform as RectTransform;
        float scale = SortTheme.NormalizeScale(_canvas.rootCanvas.scaleFactor);
        Vector2 size = canvasRect != null ? canvasRect.rect.size : Vector2.zero;

        if (Mathf.Approximately(_scale, scale) && (_size - size).sqrMagnitude <= 0.01f) return;

        SortOrderMenu.InvalidateCanvas(gameObject);
    }

    public void Initialize(Canvas canvas)
    {
        _canvas = canvas;
        CaptureGeometry();
    }

    private void CaptureGeometry()
    {
        RectTransform canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
        _scale = _canvas != null ? SortTheme.NormalizeScale(_canvas.rootCanvas.scaleFactor) : 1f;
        _size = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
    }
}
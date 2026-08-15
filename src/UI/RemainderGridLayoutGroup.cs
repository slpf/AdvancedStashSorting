using UnityEngine;
using UnityEngine.UI;

namespace AdvancedStashSorting.UI;

public sealed class RemainderGridLayoutGroup : GridLayoutGroup
{
    private float _lastColumnExtraWidth;

    public float LastColumnExtraWidth
    {
        get => _lastColumnExtraWidth;
        set
        {
            if (Mathf.Approximately(_lastColumnExtraWidth, value)) return;

            _lastColumnExtraWidth = Mathf.Max(0f, value);
            SetDirty();
        }
    }

    public override void SetLayoutHorizontal()
    {
        base.SetLayoutHorizontal();
        AdjustEdgeColumns();
    }

    public override void SetLayoutVertical()
    {
        base.SetLayoutVertical();
        AdjustEdgeColumns();
    }

    private void AdjustEdgeColumns()
    {
        if (constraintCount <= 0) return;

        if (_lastColumnExtraWidth <= 0f) return;

        for (int i = constraintCount - 1; i < rectChildren.Count; i += constraintCount)
        {
            RectTransform child = rectChildren[i];
            Vector2 size = child.sizeDelta;
            Vector2 position = child.anchoredPosition;
            size.x = cellSize.x + _lastColumnExtraWidth;
            position.x += child.pivot.x * _lastColumnExtraWidth;
            child.sizeDelta = size;
            child.anchoredPosition = position;
        }
    }
}
using UnityEngine;

public partial class GridMaskPainter
{
    private void Update()
    {
        if (!_ready) return;

        if (Input.GetKeyDown(clearKey))
        {
            ClearAll();
            return;
        }

        bool isHolding = Input.GetMouseButton(mouseButton);
        bool hasUV = TryGetBrushUV(out Vector2 uv);

        if (palmCursor != null)
            palmCursor.gameObject.SetActive(showPalmCursor && hasUV);

        if (hasUV && palmCursor != null)
            UpdatePalmCursor(uv, brushRadius);

        if (gridOverlay != null)
        {
            gridOverlay.ClearHighlights();
            if (hasUV)
                HighlightCoveredCells(uv, brushRadius);
        }

        if (!isHolding || !hasUV) return;

        float delta = (1f / Mathf.Max(0.1f, secondsPerCell)) * Time.deltaTime;
        if (FillCoveredCells(uv, brushRadius, delta))
        {
            ApplyMask();
            UpdateProgressUI();
        }
    }

    private bool TryGetBrushUV(out Vector2 uv01)
    {
        uv01 = default;

        RectTransform rt = targetImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, Input.mousePosition, uiCamera, out Vector2 localPoint))
            return false;

        Rect rect = rt.rect;

        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;

        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return false;

        uv01 = new Vector2(u, v);
        return true;
    }

    private void UpdatePalmCursor(Vector2 uv01, float radiusUV)
    {
        RectTransform rt = targetImage.rectTransform;
        Rect rect = rt.rect;

        float localX = rect.xMin + uv01.x * rect.width;
        float localY = rect.yMin + uv01.y * rect.height;

        palmCursor.position = rt.TransformPoint(new Vector3(localX, localY, 0f));

        float size = (radiusUV * 2f) * Mathf.Min(rect.width, rect.height);
        palmCursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        palmCursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);
    }
}

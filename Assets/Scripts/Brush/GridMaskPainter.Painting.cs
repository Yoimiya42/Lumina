using UnityEngine;

public partial class GridMaskPainter
{
    private bool FillCoveredCells(Vector2 centerUV, float radiusUV, float delta)
    {
        bool changed = false;

        PaintGridMath.GetCoveredRange(
            centerUV,
            radiusUV,
            gridX,
            gridY,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY,
            out float cellW,
            out float cellH,
            out float r2);

        for (int y = minY; y <= maxY; y++)
        {
            float yMin = y * cellH;
            float yMax = (y + 1) * cellH;

            for (int x = minX; x <= maxX; x++)
            {
                float xMin = x * cellW;
                float xMax = (x + 1) * cellW;

                if (!PaintGridMath.CircleIntersectsRect(centerUV, r2, xMin, yMin, xMax, yMax))
                    continue;

                int idx = y * gridX + x;

                float before = cell[idx];
                if (before >= 1f) continue;

                float after = Mathf.Clamp01(before + delta);

                if (!Mathf.Approximately(after, before))
                {
                    cell[idx] = after;
                    changed = true;

                    totalFill01 += (after - before);

                    if (before < 1f && after >= 1f)
                        gridOverlay?.SetCellCompleted(x, y, true);
                }
            }
        }

        return changed;
    }

    private void HighlightCoveredCells(Vector2 centerUV, float radiusUV)
    {
        PaintGridMath.GetCoveredRange(
            centerUV,
            radiusUV,
            gridX,
            gridY,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY,
            out float cellW,
            out float cellH,
            out float r2);

        for (int y = minY; y <= maxY; y++)
        {
            float yMin = y * cellH;
            float yMax = (y + 1) * cellH;

            for (int x = minX; x <= maxX; x++)
            {
                float xMin = x * cellW;
                float xMax = (x + 1) * cellW;

                if (!PaintGridMath.CircleIntersectsRect(centerUV, r2, xMin, yMin, xMax, yMax))
                    continue;

                gridOverlay?.HighlightCell(x, y);
            }
        }
    }

    private void UpdateProgressUI()
    {
        float p = GetProgress01();
        if (progressSlider != null) progressSlider.value = p;
        if (progressText != null) progressText.text = Mathf.RoundToInt(p * 100f) + "%";
    }
}

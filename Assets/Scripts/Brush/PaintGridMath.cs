using UnityEngine;

public static class PaintGridMath
{
    public static (int gridX, int gridY) ResolveGridSize(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Easy => (8, 8),
            Difficulty.Medium => (12, 12),
            _ => (16, 16)
        };
    }

    public static void GetCoveredRange(
        Vector2 centerUV,
        float radiusUV,
        int gridX,
        int gridY,
        out int minX,
        out int maxX,
        out int minY,
        out int maxY,
        out float cellW,
        out float cellH,
        out float radiusSquared)
    {
        cellW = 1f / Mathf.Max(1, gridX);
        cellH = 1f / Mathf.Max(1, gridY);

        minX = Mathf.Clamp(Mathf.FloorToInt((centerUV.x - radiusUV) / cellW), 0, Mathf.Max(1, gridX) - 1);
        maxX = Mathf.Clamp(Mathf.FloorToInt((centerUV.x + radiusUV) / cellW), 0, Mathf.Max(1, gridX) - 1);
        minY = Mathf.Clamp(Mathf.FloorToInt((centerUV.y - radiusUV) / cellH), 0, Mathf.Max(1, gridY) - 1);
        maxY = Mathf.Clamp(Mathf.FloorToInt((centerUV.y + radiusUV) / cellH), 0, Mathf.Max(1, gridY) - 1);

        radiusSquared = radiusUV * radiusUV;
    }

    public static bool CircleIntersectsRect(Vector2 center, float radiusSquared, float xMin, float yMin, float xMax, float yMax)
    {
        float cx = Mathf.Clamp(center.x, xMin, xMax);
        float cy = Mathf.Clamp(center.y, yMin, yMax);

        float dx = center.x - cx;
        float dy = center.y - cy;

        return (dx * dx + dy * dy) <= radiusSquared;
    }
}

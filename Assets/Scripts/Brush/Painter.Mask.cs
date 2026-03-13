using UnityEngine;

public partial class Painter
{
    private void AllocateMask(int gx, int gy)
    {
        gridX = Mathf.Max(1, gx);
        gridY = Mathf.Max(1, gy);

        cell = new float[gridX * gridY];

        if (maskTex != null)
            Destroy(maskTex);

        maskTex = new Texture2D(gridX, gridY, TextureFormat.R8, false, true);
        maskTex.wrapMode = TextureWrapMode.Clamp;
        maskTex.filterMode = FilterMode.Point;
        maskTex.name = "GridMask_Runtime";
    }

    private void ClearAll_Internal()
    {
        if (cell == null) return;
        for (int i = 0; i < cell.Length; i++) cell[i] = 0f;
        totalFill01 = 0f;
    }

    private void SetCells(float[] saved)
    {
        System.Array.Copy(saved, cell, cell.Length);

        totalFill01 = 0f;
        for (int i = 0; i < cell.Length; i++)
            totalFill01 += Mathf.Clamp01(cell[i]);
    }

    private void ApplyCompletedOverlayFromCells()
    {
        if (gridOverlay == null || cell == null) return;
        gridOverlay.ApplyCompletedFromCells(cell);
    }

    private void ApplyMask()
    {
        if (maskTex == null || cell == null) return;

        for (int y = 0; y < gridY; y++)
            for (int x = 0; x < gridX; x++)
            {
                float v = Mathf.Clamp01(cell[y * gridX + x]);
                byte b = (byte)Mathf.RoundToInt(v * 255f);
                maskTex.SetPixel(x, y, new Color32(b, 0, 0, 255));
            }

        maskTex.Apply(false, false);
    }
}

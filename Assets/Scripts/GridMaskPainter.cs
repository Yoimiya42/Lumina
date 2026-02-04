using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridMaskPainter : MonoBehaviour
{
    [Header("Target UI")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Canvas targetCanvas;
    [Tooltip("Overlay -> leave None. Screen Space - Camera / World Space -> assign the UI camera.")]
    [SerializeField] private Camera uiCamera;

    [Header("Main Material (SG_GrayscaleToColor)")]
    [SerializeField] private Material mainMaterial;

    [Header("Grid Overlay (UI)")]
    [SerializeField] private GridOverlayRenderer gridOverlay;

    public enum GridDensityPreset { Low, Medium, High }

    [Header("Grid Density Preset (fallback)")]
    [SerializeField] private GridDensityPreset gridDensity = GridDensityPreset.High;

    private int gridX;
    private int gridY;

    [Header("Brush")]
    [Range(0.02f, 0.2f)]
    [SerializeField] private float brushRadius = 0.05f;

    [Header("Speed")]
    [Tooltip("Seconds required to fully color ONE cell while covered. Example: 5 means each cell needs 5 seconds.")]
    [SerializeField] private float secondsPerCell = 5f;

    [Header("Input (temporary)")]
    [SerializeField] private int mouseButton = 0;
    [SerializeField] private KeyCode clearKey = KeyCode.C;

    [Header("Visual Feedback")]
    [SerializeField] private RectTransform palmCursor;
    [SerializeField] private bool showPalmCursor = true;

    [Header("Progress UI")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text progressText;

    private float totalFill01 = 0f;

    private Material runtimeMainMat;
    private Texture2D maskTex;
    private float[] cell;

    private static readonly int MainTexProp = Shader.PropertyToID("_MainTex");
    private static readonly int MaskTexProp = Shader.PropertyToID("_MaskTex");

    private bool _ready = false;

    private void Awake()
    {
        if (targetImage == null || targetCanvas == null || mainMaterial == null)
        {
            Debug.LogError("[GridMaskPainter] Missing references in Inspector.");
            enabled = false;
            return;
        }

        
        runtimeMainMat = new Material(mainMaterial);
        targetImage.material = runtimeMainMat;

        if (palmCursor != null)
            palmCursor.gameObject.SetActive(showPalmCursor);

        
        ApplyGridPreset(gridDensity);
        AllocateMask(gridX, gridY);

        
        if (targetImage.sprite != null)
            runtimeMainMat.SetTexture(MainTexProp, targetImage.sprite.texture);

        runtimeMainMat.SetTexture(MaskTexProp, maskTex);

        if (gridOverlay != null)
            gridOverlay.Configure(gridX, gridY);

        _ready = true;
    }

    /// <summary>
    /// NEW: Called by GameEntryController when entering game.
    /// This will (1) set sprite, (2) map difficulty -> grid density (8/12/16),
    /// (3) rebuild mask + grid overlay, (4) reset progress.
    /// </summary>
    public void BeginNewImage(Sprite sprite, Difficulty difficulty)
    {
        if (!_ready)
        {
            Debug.LogWarning("[GridMaskPainter] Not ready yet. Ensure this component is enabled and Awake ran.");
        }

        if (sprite != null)
        {
            targetImage.sprite = sprite;
            if (runtimeMainMat != null)
                runtimeMainMat.SetTexture(MainTexProp, sprite.texture);
        }

        // difficulty -> preset
        GridDensityPreset preset = difficulty switch
        {
            Difficulty.Easy => GridDensityPreset.Low,      // 8x8
            Difficulty.Medium => GridDensityPreset.Medium, // 12x12
            _ => GridDensityPreset.High                    // 16x16
        };

        ApplyGridPreset(preset);

        // reallocate mask/cells for new grid size
        AllocateMask(gridX, gridY);
        if (runtimeMainMat != null)
            runtimeMainMat.SetTexture(MaskTexProp, maskTex);

        // rebuild grid overlay
        if (gridOverlay != null)
        {
            gridOverlay.Configure(gridX, gridY);
            gridOverlay.Rebuild();
        }

        ApplyMask();
        UpdateProgressUI();

        Debug.Log($"[GridMaskPainter] BeginNewImage diff={difficulty} preset={preset} grid={gridX}x{gridY}");
    }

    private void Update()
    {
        if (!_ready) return;

        if (Input.GetKeyDown(clearKey))
        {
            ClearAll();
            ApplyMask();
            UpdateProgressUI();

            if (gridOverlay != null)
                gridOverlay.Rebuild();
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

    private void ApplyGridPreset(GridDensityPreset preset)
    {
        gridDensity = preset;
        switch (preset)
        {
            case GridDensityPreset.Low: gridX = 8; gridY = 8; break;
            case GridDensityPreset.Medium: gridX = 12; gridY = 12; break;
            default: gridX = 16; gridY = 16; break;
        }
    }

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

        ClearAll();
        totalFill01 = 0f;
    }

    private void ClearAll()
    {
        if (cell == null) return;
        for (int i = 0; i < cell.Length; i++)
            cell[i] = 0f;

        totalFill01 = 0f;
        UpdateProgressUI();
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

    private bool FillCoveredCells(Vector2 centerUV, float radiusUV, float delta)
    {
        bool changed = false;

        float cellW = 1f / gridX;
        float cellH = 1f / gridY;

        int minX = Mathf.Clamp(Mathf.FloorToInt((centerUV.x - radiusUV) / cellW), 0, gridX - 1);
        int maxX = Mathf.Clamp(Mathf.FloorToInt((centerUV.x + radiusUV) / cellW), 0, gridX - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((centerUV.y - radiusUV) / cellH), 0, gridY - 1);
        int maxY = Mathf.Clamp(Mathf.FloorToInt((centerUV.y + radiusUV) / cellH), 0, gridY - 1);

        float r2 = radiusUV * radiusUV;

        for (int y = minY; y <= maxY; y++)
        {
            float yMin = y * cellH;
            float yMax = (y + 1) * cellH;

            for (int x = minX; x <= maxX; x++)
            {
                float xMin = x * cellW;
                float xMax = (x + 1) * cellW;

                if (!CircleIntersectsRect(centerUV, r2, xMin, yMin, xMax, yMax))
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
                    {
                        if (gridOverlay != null)
                            gridOverlay.SetCellCompleted(x, y, true);
                    }
                }
            }
        }

        return changed;
    }

    private void HighlightCoveredCells(Vector2 centerUV, float radiusUV)
    {
        float cellW = 1f / gridX;
        float cellH = 1f / gridY;

        int minX = Mathf.Clamp(Mathf.FloorToInt((centerUV.x - radiusUV) / cellW), 0, gridX - 1);
        int maxX = Mathf.Clamp(Mathf.FloorToInt((centerUV.x + radiusUV) / cellW), 0, gridX - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((centerUV.y - radiusUV) / cellH), 0, gridY - 1);
        int maxY = Mathf.Clamp(Mathf.FloorToInt((centerUV.y + radiusUV) / cellH), 0, gridY - 1);

        float r2 = radiusUV * radiusUV;

        for (int y = minY; y <= maxY; y++)
        {
            float yMin = y * cellH;
            float yMax = (y + 1) * cellH;

            for (int x = minX; x <= maxX; x++)
            {
                float xMin = x * cellW;
                float xMax = (x + 1) * cellW;

                if (!CircleIntersectsRect(centerUV, r2, xMin, yMin, xMax, yMax))
                    continue;

                gridOverlay.HighlightCell(x, y);
            }
        }
    }

    private void UpdateProgressUI()
    {
        if (cell == null || cell.Length == 0) return;

        float completed01 = Mathf.Clamp01(totalFill01 / cell.Length);

        if (progressSlider != null)
            progressSlider.value = completed01;

        if (progressText != null)
            progressText.text = Mathf.RoundToInt(completed01 * 100f) + "%";
    }

    private static bool CircleIntersectsRect(Vector2 c, float r2, float xMin, float yMin, float xMax, float yMax)
    {
        float cx = Mathf.Clamp(c.x, xMin, xMax);
        float cy = Mathf.Clamp(c.y, yMin, yMax);

        float dx = c.x - cx;
        float dy = c.y - cy;

        return (dx * dx + dy * dy) <= r2;
    }

    private void ApplyMask()
    {
        if (maskTex == null || cell == null) return;

        for (int y = 0; y < gridY; y++)
        {
            for (int x = 0; x < gridX; x++)
            {
                float v = cell[y * gridX + x];
                byte b = (byte)Mathf.RoundToInt(v * 255f);
                maskTex.SetPixel(x, y, new Color32(b, 0, 0, 255));
            }
        }

        maskTex.Apply(false, false);
    }

    public void SetBrushRadius(float radius)
    {
        brushRadius = Mathf.Clamp(radius, 0.01f, 0.2f);
    }

    public float GetBrushRadiusUV() => brushRadius;

    private void OnDestroy()
    {
        if (runtimeMainMat != null) Destroy(runtimeMainMat);
        if (maskTex != null) Destroy(maskTex);
    }
}

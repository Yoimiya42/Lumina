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

    [Header("Grid Density Preset")]
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
    [SerializeField] private int mouseButton = 0; // 0 = left mouse
    [SerializeField] private KeyCode clearKey = KeyCode.C;

    [Header("Visual Feedback")]
    [Tooltip("Yellow circle UI image that follows the hand/mouse.")]
    [SerializeField] private RectTransform palmCursor;
    [SerializeField] private bool showPalmCursor = true;

    [Header("Progress UI")]
    [SerializeField] private Slider progressSlider;      // 拖你的 ProgressBar(Slider) 进来
    [SerializeField] private TMP_Text progressText;   // 若用TMP就用这行替代上面那行

    private float totalFill01 = 0f; // 所有cell进度之和（每个cell 0..1）
    // Runtime
    private Material runtimeMainMat;
    private Texture2D maskTex;         // size = gridX x gridY
    private float[] cell;              // flattened: y*gridX + x

    private static readonly int MainTexProp = Shader.PropertyToID("_MainTex");
    private static readonly int MaskTexProp = Shader.PropertyToID("_MaskTex");

    private void Awake()
    {
        if (targetImage == null || targetCanvas == null || mainMaterial == null)
        {
            Debug.LogError("[GridMaskPainter] Missing references in Inspector.");
            enabled = false;
            return;
        }

        ApplyGridPreset();

        // Clone material at runtime
        runtimeMainMat = new Material(mainMaterial);
        targetImage.material = runtimeMainMat;

        // Ensure _MainTex matches sprite texture
        if (targetImage.sprite != null)
            runtimeMainMat.SetTexture(MainTexProp, targetImage.sprite.texture);

        AllocateMask(gridX, gridY);
        runtimeMainMat.SetTexture(MaskTexProp, maskTex);

        if (palmCursor != null)
            palmCursor.gameObject.SetActive(showPalmCursor);

        Debug.Log("[GridMaskPainter] Ready. Hold mouse to paint slowly. Press C to clear.");

        if (gridOverlay != null)
            gridOverlay.Configure(gridX, gridY);
    }

    private void Update()
    {
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

        // --- 顺序非常重要 ---
        if (gridOverlay != null)
        {
            // 1. 先把所有还存在的格子重置为白色
            gridOverlay.ClearHighlights();
        
            // 2. 如果鼠标在范围内，把鼠标下的格子变紫色
            if (hasUV)
                HighlightCoveredCells(uv, brushRadius);
        }

        // 3. 填色逻辑
        if (!isHolding || !hasUV) return;

        float delta = (1f / Mathf.Max(0.1f, secondsPerCell)) * Time.deltaTime;
        if (FillCoveredCells(uv, brushRadius, delta)) { 
            ApplyMask();
            UpdateProgressUI();
        }
    }

    private void ApplyGridPreset() {
        switch (gridDensity)
        {
            case GridDensityPreset.Low:     gridX = 8; gridY = 8; break;
            case GridDensityPreset.Medium:  gridX = 12; gridY = 12; break;
            default:                        gridX = 16; gridY = 16; break;
        }
    }

    private void AllocateMask(int gx, int gy)
    {
        gridX = Mathf.Max(1, gx);
        gridY = Mathf.Max(1, gy);

        cell = new float[gridX * gridY];

        maskTex = new Texture2D(gridX, gridY, TextureFormat.R8, false, true);
        maskTex.wrapMode = TextureWrapMode.Clamp;
        maskTex.filterMode = FilterMode.Point; // crisp grid boundaries
        maskTex.name = "GridMask_Runtime";

        ClearAll();
        totalFill01 = 0f;
        ApplyMask();
        UpdateProgressUI();
    }

    private void ClearAll()
    {
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
        // Convert UV back to local position inside the Image rect
        RectTransform rt = targetImage.rectTransform;
        Rect rect = rt.rect;

        float localX = rect.xMin + uv01.x * rect.width;
        float localY = rect.yMin + uv01.y * rect.height;

        // Place cursor in the same RectTransform space as the Image
        // Best practice: keep palmCursor under the same Canvas as targetImage
        palmCursor.position = rt.TransformPoint(new Vector3(localX, localY, 0f));

        // Scale cursor diameter to match brush radius in screen space
        float size = (radiusUV * 2f) * Mathf.Min(rect.width, rect.height);
        float diameterLocalX = size;
        float diameterLocalY = size;

        palmCursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, diameterLocalX);
        palmCursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, diameterLocalY);
    }

    /// <summary>
    /// Fill every cell whose rectangle intersects the brush circle (UV space).
    /// Progress is accumulated and preserved when not covered.
    /// </summary>
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

    private void UpdateProgressUI() {
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


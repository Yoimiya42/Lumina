using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class GridMaskPainter : MonoBehaviour
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

    [Header("Brush")]
    [Range(0.02f, 0.2f)]
    [SerializeField] private float brushRadius = 0.05f;

    [Header("Speed")]
    [Tooltip("Seconds required to fully color ONE cell while covered.")]
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

    private int gridX;
    private int gridY;

    private float totalFill01 = 0f;

    private Material runtimeMainMat;
    private Texture2D maskTex;
    private float[] cell;

    private bool _ready = false;

    private static readonly int MainTexProp = Shader.PropertyToID("_MainTex");
    private static readonly int MaskTexProp = Shader.PropertyToID("_MaskTex");

    public int GridX => gridX;
    public int GridY => gridY;

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
            palmCursor.gameObject.SetActive(false);
    }

    /// <summary>
    /// Enter game / restore state.
    /// - sprite: original full-res sprite for gameplay
    /// - difficulty: determines grid resolution only
    /// - savedCellsOrNull: length must equal gridX*gridY, values 0..1
    /// </summary>
    public void BeginOrRestore(Sprite sprite, Difficulty difficulty, float[] savedCellsOrNull)
    {
        _ready = false;

        if (sprite != null)
        {
            targetImage.sprite = sprite;
            runtimeMainMat.SetTexture(MainTexProp, sprite.texture);
        }

        (gridX, gridY) = PaintGridMath.ResolveGridSize(difficulty);

        AllocateMask(gridX, gridY);
        runtimeMainMat.SetTexture(MaskTexProp, maskTex);

        if (gridOverlay != null)
            gridOverlay.Configure(gridX, gridY);

        if (savedCellsOrNull != null && savedCellsOrNull.Length == gridX * gridY)
            SetCells(savedCellsOrNull);
        else
            ClearAll_Internal();

        ApplyMask();
        ApplyCompletedOverlayFromCells();

        UpdateProgressUI();

        if (palmCursor != null)
            palmCursor.gameObject.SetActive(showPalmCursor);

        _ready = true;
    }

    public float GetProgress01()
    {
        if (cell == null || cell.Length == 0) return 0f;
        return Mathf.Clamp01(totalFill01 / cell.Length);
    }

    public float[] GetCellsCopy()
    {
        if (cell == null) return null;
        var copy = new float[cell.Length];
        System.Array.Copy(cell, copy, cell.Length);
        return copy;
    }

    public void SetBrushRadius(float radiusUV)
    {
        brushRadius = Mathf.Clamp(radiusUV, 0.01f, 0.2f);
    }

    public float GetBrushRadiusUV() => brushRadius;

    public void SetSecondsPerCell(float seconds)
    {
        secondsPerCell = Mathf.Max(0.1f, seconds);
    }

    /// <summary>
    /// Public clear (runtime reset inside gameplay).
    /// Note: Your "Reset" button in menu should delete save; this only clears current session visuals.
    /// </summary>
    public void ClearAll()
    {
        ClearAll_Internal();
        ApplyMask();
        ApplyCompletedOverlayFromCells();
        UpdateProgressUI();
    }

    private void OnDestroy()
    {
        if (runtimeMainMat != null) Destroy(runtimeMainMat);
        if (maskTex != null) Destroy(maskTex);
    }
}

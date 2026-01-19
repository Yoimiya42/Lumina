using UnityEngine;
using UnityEngine.UI;

public class ColoringController : MonoBehaviour
{
    [Header("Target UI")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Canvas targetCanvas;
    [Tooltip("Overlay -> leave None. Screen Space - Camera / World Space -> assign the UI camera.")]
    [SerializeField] private Camera uiCamera;

    [Header("Materials")]
    [Tooltip("Material based on SG_GrayscaleToColor (e.g. M_GrayscaleToColor).")]
    [SerializeField] private Material mainMaterial;
    [Tooltip("Material using Hidden/MaskStamp (e.g. M_MaskStamp).")]
    [SerializeField] private Material stampMaterial;

    [Header("Mask RenderTexture")]
    [SerializeField] private int maskResolution = 1024;

    [Header("Brush")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float brushRadiusUV = 0.05f;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float brushHardness = 0.35f;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float brushStrength = 0.08f;
    [Header("Stamp Rate")]
    [Tooltip("Seconds between stamps while holding input. Larger = slower fill.")]
    [SerializeField] private float stampIntervalSeconds = 0.06f;

    [Header("Input (temporary)")]
    [SerializeField] private int mouseButton = 0; // 0 = left
    [SerializeField] private KeyCode clearKey = KeyCode.C;

    [Header("Debug / Compatibility")]
    [Tooltip("Keep Fill fixed at 1 so mask directly controls coloring. Recommended for now.")]
    [SerializeField] private bool forceFillOne = true;

    // Runtime resources
    private RenderTexture maskA;
    private RenderTexture maskB;
    private Material runtimeMainMat;

    // Timers
    private float stampTimer = 0f;

    // Shader property IDs
    private static readonly int MainTexProp = Shader.PropertyToID("_MainTex");
    private static readonly int MaskTexProp = Shader.PropertyToID("_MaskTex");

    private static readonly int BrushUVProp = Shader.PropertyToID("_BrushUV");
    private static readonly int BrushRadiusProp = Shader.PropertyToID("_BrushRadius");
    private static readonly int BrushHardnessProp = Shader.PropertyToID("_BrushHardness");
    private static readonly int BrushStrengthProp = Shader.PropertyToID("_BrushStrength");

    private void Awake()
    {
        // Validate references
        if (targetImage == null || targetCanvas == null || mainMaterial == null || stampMaterial == null)
        {
            Debug.LogError("[ColoringController] Missing references in Inspector. " +
                           "Need Target Image, Target Canvas, Main Material, Stamp Material.");
            enabled = false;
            return;
        }

        // Create runtime instance of main material (single owner)
        runtimeMainMat = new Material(mainMaterial);
        targetImage.material = runtimeMainMat;

        // Ensure _MainTex matches sprite texture (important for UI Image)
        if (targetImage.sprite != null)
        {
            runtimeMainMat.SetTexture(MainTexProp, targetImage.sprite.texture);
        }

        // Create mask RTs
        maskA = CreateMaskRT(maskResolution);
        maskB = CreateMaskRT(maskResolution);

        ClearRenderTexture(maskA, Color.black);
        ClearRenderTexture(maskB, Color.black);

        runtimeMainMat.SetTexture(MaskTexProp, maskA);

        Debug.Log("[ColoringController] Ready. Hold mouse to paint. Press C to clear.");
    }

    private void Update()
    {
        // Clear
        if (Input.GetKeyDown(clearKey))
        {
            ClearRenderTexture(maskA, Color.black);
            ClearRenderTexture(maskB, Color.black);
            runtimeMainMat.SetTexture(MaskTexProp, maskA);
        }

        // Paint
        if (Input.GetMouseButton(mouseButton))
        {
            stampTimer += Time.deltaTime;

            if (stampTimer >= stampIntervalSeconds)
            {
                stampTimer = 0f;

                if (GetBrushUV(out Vector2 uv01))
                {
                    StampAtUv(uv01);
                }
            }
        }
        else
        {
            stampTimer = 0f;
        }
    }

    /// <summary>
    /// Convert screen position to UV (0..1) inside the target Image rect.
    /// NOTE: This assumes Preserve Aspect is OFF. We'll add the draw-rect mapping later when needed.
    /// </summary>
    private bool GetBrushUV(out Vector2 uv01)
    {
        uv01 = default;

        RectTransform rt = targetImage.rectTransform;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, Input.mousePosition, uiCamera, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = rt.rect;

        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;

        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return false;

        uv01 = new Vector2(u, v);
        return true;
    }

    private void StampAtUv(Vector2 uv01)
    {
        // Drive stamp shader
        stampMaterial.SetVector(BrushUVProp, new Vector4(uv01.x, uv01.y, 0f, 0f));
        stampMaterial.SetFloat(BrushRadiusProp, brushRadiusUV);
        stampMaterial.SetFloat(BrushHardnessProp, brushHardness);
        stampMaterial.SetFloat(BrushStrengthProp, brushStrength);

        // Ping-pong blit
        Graphics.Blit(maskA, maskB, stampMaterial);
        (maskA, maskB) = (maskB, maskA);

        runtimeMainMat.SetTexture(MaskTexProp, maskA);
    }

    private static RenderTexture CreateMaskRT(int size)
    {
        RenderTexture rt = new RenderTexture(size, size, 0, RenderTextureFormat.R8);
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.useMipMap = false;
        rt.Create();
        return rt;
    }

    private static void ClearRenderTexture(RenderTexture rt, Color color)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, color);
        RenderTexture.active = prev;
    }

    private void OnDestroy()
    {
        if (maskA != null) { maskA.Release(); Destroy(maskA); }
        if (maskB != null) { maskB.Release(); Destroy(maskB); }
        if (runtimeMainMat != null) Destroy(runtimeMainMat);
    }
}

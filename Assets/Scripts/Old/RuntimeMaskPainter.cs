using UnityEngine;
using UnityEngine.UI;

public class RuntimeMaskPainter : MonoBehaviour
{

    [Header("Target UI")]

    [SerializeField] private Image targetImage;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera uiCamera;


    [Header("Materials")]

    [SerializeField] private Material mainMaterial;
    [SerializeField] private Material stampMaterial;


    [Header("Mask RT Settings")]

    [SerializeField] private int maskResolution = 1024;


    [Header("Brush")]

    [Range(0.01f, 0.5f)]
    [SerializeField] private float brushRadiusUV = 0.07f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float brushHardness = 0.35f;

    [Range(0.0f, 5.0f)]
    [SerializeField] private float brushStrength = 0.7f;

    [SerializeField] private float stampIntervalSeconds = 0.03f;
    private float stampTimer = 0f;

    private RenderTexture maskA;
    private RenderTexture maskB;

    private Material runtimeMainMat;

    private static readonly int MaskTexProp = Shader.PropertyToID("_MaskTex");
    private static readonly int BrushUVProp = Shader.PropertyToID("_BrushUV");
    private static readonly int BrushRadiusProp = Shader.PropertyToID("_BrushRadius");
    private static readonly int BrushHardnessProp = Shader.PropertyToID("_BrushHardness");
    private static readonly int BrushStrengthProp = Shader.PropertyToID("_BrushStrength");



    private void Awake()
    {
        if (targetImage == null ||
            targetCanvas == null ||
            stampMaterial == null ||
            mainMaterial == null) { 
            
            Debug.LogError("RuntimeMaskPainter: Missing references in the inspector.");
            enabled = false;
            return;
        }

        // Clone the main material for runtime use
        runtimeMainMat = new Material(mainMaterial);
        targetImage.material = runtimeMainMat;

        // Create mask RenderTextures
        maskA = CreateMaskRT(maskResolution);
        maskB = CreateMaskRT(maskResolution);

        // Clear to black
        ClearRenderTexture(maskA, Color.black);
        ClearRenderTexture(maskB, Color.black);

        // Assign dynamic mask to the main material
        runtimeMainMat.SetTexture(MaskTexProp, maskA);
        runtimeMainMat.SetFloat(Shader.PropertyToID("_Fill"), 1f);
    }
    private void Update()
    {

        if (Input.GetMouseButton(0))
        {
            stampTimer += Time.deltaTime;
            if (stampTimer >= stampIntervalSeconds) 
            { 
                stampTimer = 0f;

                if (GetBrushUV(out Vector2 uv01))
                    StampAtUv(uv01);
            }
        }
        else
        {
            stampTimer = 0f; // reset timer when not painting
        }

        // Clear mask on 'C' key press
        if (Input.GetKeyDown(KeyCode.C))
        {
            // Clear the mask
            ClearRenderTexture(maskA, Color.black);
            ClearRenderTexture(maskB, Color.black);
        }

    }

    private bool GetBrushUV(out Vector2 uv01)
    { 
        uv01 = default;

        RectTransform rt = targetImage.rectTransform;
        Vector2 localPoint;

        bool isInside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt,
            Input.mousePosition,
            uiCamera,
            out localPoint
        );

        if (!isInside)
            return false;

        Rect rect = rt.rect;
        float u = (localPoint.x - rect.xMin) / rect.width;
        float v = (localPoint.y - rect.yMin) / rect.height;

        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return false;   

        uv01 = new Vector2(u, v);
        return true;
    }

    private void StampAtUv(Vector2 uv01) { 
        stampMaterial.SetVector(BrushUVProp, new Vector4(uv01.x, uv01.y, 0f, 0f));
        stampMaterial.SetFloat(BrushRadiusProp, brushRadiusUV);
        stampMaterial.SetFloat(BrushHardnessProp, brushHardness);
        stampMaterial.SetFloat(BrushStrengthProp, brushStrength);

        Graphics.Blit(maskA, maskB, stampMaterial);

        // Swap
        (maskA, maskB) = (maskB, maskA);

        // Update the main material with the new mask
        runtimeMainMat.SetTexture(MaskTexProp,  maskA);
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
        if (maskA != null)  maskA.Release();
        if (maskB != null)  maskB.Release();
    }
}

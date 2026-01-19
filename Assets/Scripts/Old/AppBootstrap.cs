using UnityEngine;
using UnityEngine.UI;

public class AppBootstrap : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Image colorImage;

    [Header("Fill Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float fillAmount = 0f;

    [SerializeField] private float fillSpeedPerSecond = 0.25f;

    [Header("Input")]
    [SerializeField] private KeyCode holdKey = KeyCode.Space;

    private static readonly int FillProp = Shader.PropertyToID("_Fill");
    private static readonly int MainTexProp = Shader.PropertyToID("_MainTex");

    private Material runtimeMat;

    private void Awake()
    {
        if (colorImage == null)
        {
            Debug.LogError("[AppBootstrap] Missing reference: " +
                "colorImage is not assigned in Inspector.");
            enabled = false; // Disable this script to avoid null reference spam.
            return;
        }

        if (colorImage.material == null) 
        {
            Debug.LogError("[AppBootstrap] ColorImage has no Material.");
            enabled = false;
            return;
        }


        // Create a runtime instance of the material to avoid modifying the original asset.
        runtimeMat = Instantiate(colorImage.material);
        colorImage.material = runtimeMat;

        // ensure _MainTex matches the sprite's texture
        if (colorImage.sprite != null)
            runtimeMat.SetTexture(MainTexProp, colorImage.sprite.texture);

        runtimeMat.SetFloat(FillProp, fillAmount);
        Debug.Log("[AppBootstrap] Ready. Hold Space to restore color from grayscale. Press R to reset.");
    }

    // Update is called once per frame
    void Update()
    {
        bool isHolding = Input.GetKey(holdKey);

        if (isHolding) {
            fillAmount += fillSpeedPerSecond * Time.deltaTime;

            // Mathf: Math Utility Class for Float Operations
            fillAmount = Mathf.Clamp01(fillAmount);
            runtimeMat.SetFloat(FillProp, fillAmount);
        }

        // Optional debug: press R to reset
        if (Input.GetKeyDown(KeyCode.R))
        { 
            fillAmount = 0f;
            runtimeMat.SetFloat(FillProp, fillAmount);
            Debug.Log("[AppBootstrap] Reset fill to 0.");
        }
    }

}

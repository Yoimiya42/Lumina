using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class GameEntryController : MonoBehaviour
{
    [Header("Source (Menu)")]
    [SerializeField] private ThemeMenuBuilder menuBuilder;

    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;

    [Header("Game UI")]
    [SerializeField] private Image gameColorImage;                 // ColorImage (Image)
    [SerializeField] private AspectRatioFitter aspectFitter;       // AspectBox上的AspectRatioFitter
    [SerializeField] private GridMaskPainter painter;              // 上色脚本

    private void Awake()
    {
        if (menuPanel != null)
            menuPanel.SetActive(true);
        if (gamePanel != null)
            gamePanel.SetActive(false);
    }
    public void EnterGame()
    {
        if (menuBuilder == null)
        {
            Debug.LogError("[GameEntryController] Missing menuBuilder reference.");
            return;
        }

        string path = menuBuilder.SelectedImagePath;
        string id = menuBuilder.SelectedImageId;
        Difficulty diff = menuBuilder.SelectedDifficulty;

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("[GameEntryController] No image selected.");
            return;
        }

        if (gameColorImage == null)
        {
            Debug.LogError("[GameEntryController] Missing gameColorImage reference.");
            return;
        }

        Sprite fullSprite = LoadSpriteFromFile(path);
        if (fullSprite == null)
        {
            Debug.LogError($"[GameEntryController] Failed to load image: {path}");
            return;
        }

        // 1) 让容器按图片比例变化（避免留白导致网格错位）
        if (aspectFitter != null)
        {
            float w = fullSprite.rect.width;
            float h = fullSprite.rect.height;
            if (h > 0.01f) aspectFitter.aspectRatio = w / h;
        }

        // 2) 关闭 preserveAspect（我们用 AspectRatioFitter 来保证比例）
        gameColorImage.sprite = fullSprite;
        gameColorImage.preserveAspect = false;

        // 3) 初始化/重建 Painter 的网格密度（把难度传过去）
        if (painter != null)
            painter.BeginNewImage(fullSprite, diff);

        // 4) 切换面板
        if (menuPanel != null) menuPanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);

        Canvas.ForceUpdateCanvases();

        Debug.Log($"[GameEntryController] EnterGame OK. imageId={id}, diff={diff}, path={path}");
    }

    private static Sprite LoadSpriteFromFile(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes == null || bytes.Length == 0)
                return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            tex.name = Path.GetFileName(filePath);

            if (!tex.LoadImage(bytes, markNonReadable: false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f
            );
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameEntryController] LoadSpriteFromFile failed: {filePath}\n{e}");
            return null;
        }
    }
}

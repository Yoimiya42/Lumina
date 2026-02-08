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
    [SerializeField] private Image gameColorImage;
    [SerializeField] private AspectRatioFitter aspectFitter;
    [SerializeField] private Painter painter;

    public string CurrentImageId { get; private set; }
    public Difficulty CurrentDifficulty { get; private set; }

    private void Awake()
    {
        gamePanel?.SetActive(false);
        menuPanel?.SetActive(true);
    }
    public void EnterGame()
    {
        string path = menuBuilder?.SelectedImagePath;
        string imageId = menuBuilder?.SelectedImageId;

        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(imageId))
        {
            Debug.LogWarning("[GameEntryController] No selection.");
            return;
        }

        if (gameColorImage == null || painter == null)
        {
            Debug.LogError("[GameEntryController] Missing gameColorImage or painter.");
            return;
        }

        var fullSprite = LoadSpriteFromFile(path);
        if (fullSprite == null)
        {
            Debug.LogError($"[GameEntryController] Failed to load: {path}");
            return;
        }

        if (aspectFitter != null)
        {
            float w = fullSprite.rect.width;
            float h = fullSprite.rect.height;
            if (h > 0.01f) aspectFitter.aspectRatio = w / h;
        }

        gameColorImage.sprite = fullSprite;
        gameColorImage.preserveAspect = false;

        Difficulty diff = menuBuilder.SelectedDifficulty;
        float[] savedCells = null;

        if (ImageProgressRepository.TryGet(imageId, out var entry) && entry != null && entry.progress01 > 0f)
        {
            diff = (Difficulty)entry.lockedDifficulty;
            savedCells = entry.cells;
        }

        painter.BeginOrRestore(fullSprite, diff, savedCells);

        CurrentImageId = imageId;
        CurrentDifficulty = diff;

        menuPanel?.SetActive(false);
        gamePanel?.SetActive(true);

        Canvas.ForceUpdateCanvases();

        Debug.Log($"[GameEntryController] EnterGame OK imageId={imageId} diff={diff} db={ImageProgressRepository.DebugGetFilePath()}");
    }

    private static Sprite LoadSpriteFromFile(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            byte[] bytes = File.ReadAllBytes(filePath);

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

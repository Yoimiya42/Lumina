using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scans images from Lumina's user content folder and loads them as Sprites (for thumbnails / selection UI).
/// Default scan directory: LauncherRoot/UserContent/Lumina/Images
///
/// Notes:
/// - This loads images into memory. For many large images, you should generate thumbnails and load those instead.
/// - For "first step", this is stable and simple.
/// </summary>
public class LuminaImageFolderScanner : MonoBehaviour
{
    [Header("Scan Settings")]
    [Tooltip("If empty, uses LuminaContentPaths.GetImagesDir().")]
    [SerializeField] private string imagesDirOverride = "";

    [Tooltip("Scan subfolders (themes) recursively.")]
    [SerializeField] private bool includeSubfolders = true;

    [Tooltip("Allowed file extensions (lowercase, include dot).")]
    [SerializeField] private string[] extensions = { ".png", ".jpg", ".jpeg", ".webp" };

    [Header("Runtime Output")]
    [SerializeField] private List<LuminaImageItem> items = new List<LuminaImageItem>();

    [Header("Events")]
    public UnityEvent<List<LuminaImageItem>> OnScanCompleted;

    [Serializable]
    public class LuminaImageItem
    {
        public string filePath;     // absolute path
        public string theme;        // folder name under Images (or "" if none)
        public string fileName;     // without extension
        public Sprite sprite;       // loaded sprite (thumbnail use)
        public Vector2Int size;     // original pixel size
    }

    public IReadOnlyList<LuminaImageItem> Items => items;

    private void Awake()
    {
        // Ensure folders always exist (portable layout)
        LuminaContentPaths.EnsureFolders();
    }

    [ContextMenu("Scan Now")]
    public void Scan()
    {
        items.Clear();

        string root = string.IsNullOrWhiteSpace(imagesDirOverride)
            ? LuminaContentPaths.GetImagesDir()
            : Path.GetFullPath(imagesDirOverride);

        if (!Directory.Exists(root))
        {
            Debug.LogWarning($"[LuminaImageFolderScanner] Images directory not found, creating: {root}");
            Directory.CreateDirectory(root);
        }

        var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(root, "*.*", option);

        foreach (var f in files)
        {
            if (!IsAllowed(f)) continue;

            var item = LoadAsItem(root, f);
            if (item != null)
                items.Add(item);
        }

        Debug.Log($"[LuminaImageFolderScanner] Scan completed. Found {items.Count} images. Dir={root}");
        OnScanCompleted?.Invoke(new List<LuminaImageItem>(items));
    }

    private bool IsAllowed(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        for (int i = 0; i < extensions.Length; i++)
        {
            if (extensions[i] == null) continue;
            if (ext == extensions[i].ToLowerInvariant())
                return true;
        }
        return false;
    }

    private LuminaImageItem LoadAsItem(string imagesRoot, string filePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes == null || bytes.Length == 0) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            tex.name = Path.GetFileName(filePath);

            if (!tex.LoadImage(bytes, markNonReadable: false))
            {
                Destroy(tex);
                return null;
            }

            // Create sprite
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f
            );

            // Theme name = immediate folder under Images
            string theme = GetThemeName(imagesRoot, filePath);

            return new LuminaImageItem
            {
                filePath = filePath,
                theme = theme,
                fileName = Path.GetFileNameWithoutExtension(filePath),
                sprite = sprite,
                size = new Vector2Int(tex.width, tex.height)
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LuminaImageFolderScanner] Failed to load image: {filePath}\n{e}");
            return null;
        }
    }

    private string GetThemeName(string imagesRoot, string filePath)
    {
        // imagesRoot: .../UserContent/Lumina/Images
        // filePath:   .../UserContent/Lumina/Images/CAT/a.png
        // => theme = CAT
        try
        {
            var root = new DirectoryInfo(imagesRoot).FullName;
            var full = new FileInfo(filePath).Directory?.FullName;
            if (string.IsNullOrEmpty(full)) return "";

            // If the image is directly under Images, no theme folder
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                return "";

            // theme is the first folder segment after root
            var relative = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Length > 0 ? parts[0] : "";
        }
        catch
        {
            return "";
        }
    }
}

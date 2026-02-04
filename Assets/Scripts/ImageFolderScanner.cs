using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scans images from configured Images folder and loads them as Sprites (for thumbnails/selection UI).
/// </summary>
public class ImageFolderScanner : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private PathSettings pathSettings;

    [Header("Scan Settings")]
    [Tooltip("If empty, uses ImagesDir resolved from pathSettings. If not empty, treated as absolute override.")]
    [SerializeField] private string imagesDirAbsoluteOverride = "";

    [Tooltip("Scan subfolders (themes) recursively.")]
    [SerializeField] private bool includeSubfolders = true;

    [Tooltip("Allowed file extensions (lowercase, include dot).")]
    [SerializeField] private string[] extensions = { ".png", ".jpg", ".jpeg", ".webp" };

    [Header("Runtime Output")]
    [SerializeField] private List<ImageItem> items = new List<ImageItem>();

    [Header("Events")]
    public UnityEvent<List<ImageItem>> OnScanCompleted;

    [Serializable]
    public class ImageItem
    {
        public string filePath;     // absolute path
        public string theme;        // first folder under Images (or "" if none)
        public string fileName;     // without extension
        public Sprite sprite;       // thumbnail sprite
        public Vector2Int size;     // original pixel size
        public string imageId;      // sha1(file bytes)
    }

    public IReadOnlyList<ImageItem> Items => items;

    private void Awake()
    {
        if (pathSettings == null)
        {
            Debug.LogError("[ImageFolderScanner] Missing PathSettings reference.");
            enabled = false;
            return;
        }

        ContentPaths.EnsureFolders(pathSettings);

        ImageProgressRepository.Configure(pathSettings);
        Debug.Log("[ImageProgressRepository] SavePath = " + ImageProgressRepository.DebugGetFilePath());
    }


    [ContextMenu("Scan Now")]
    public void Scan()
    {
        items.Clear();

        string imagesRoot = ResolveImagesRoot();
        if (!Directory.Exists(imagesRoot))
        {
            Debug.LogWarning($"[ImageFolderScanner] Images directory not found, creating: {imagesRoot}");
            Directory.CreateDirectory(imagesRoot);
        }

        var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(imagesRoot, "*.*", option);

        foreach (var f in files)
        {
            if (!IsAllowed(f)) continue;

            var item = LoadAsItem(imagesRoot, f);
            if (item != null)
                items.Add(item);
        }

        Debug.Log($"[ImageFolderScanner] Scan completed. Found {items.Count} images. Dir={imagesRoot}");
        OnScanCompleted?.Invoke(new List<ImageItem>(items));
    }

    private string ResolveImagesRoot()
    {
        if (!string.IsNullOrWhiteSpace(imagesDirAbsoluteOverride))
            return Path.GetFullPath(imagesDirAbsoluteOverride);

        return ContentPaths.GetImagesFolder(pathSettings);
    }

    private bool IsAllowed(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        for (int i = 0; i < extensions.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(extensions[i])) continue;
            if (ext == extensions[i].ToLowerInvariant())
                return true;
        }
        return false;
    }

    private ImageItem LoadAsItem(string imagesRoot, string filePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes == null || bytes.Length == 0) return null;

            string imageId = Sha1Hex(bytes);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            tex.name = Path.GetFileName(filePath);

            if (!tex.LoadImage(bytes, markNonReadable: false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f
            );

            string theme = GetThemeName(imagesRoot, filePath);

            return new ImageItem
            {
                filePath = filePath,
                theme = theme,
                fileName = Path.GetFileNameWithoutExtension(filePath),
                sprite = sprite,
                size = new Vector2Int(tex.width, tex.height),
                imageId = imageId
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImageFolderScanner] Failed to load image: {filePath}\n{e}");
            return null;
        }
    }

    private string GetThemeName(string imagesRoot, string filePath)
    {
        try
        {
            var root = new DirectoryInfo(imagesRoot).FullName;
            var full = new FileInfo(filePath).Directory?.FullName;
            if (string.IsNullOrEmpty(full)) return "";

            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                return "";

            var relative = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return parts.Length > 0 ? parts[0] : "";
        }
        catch
        {
            return "";
        }
    }

    private static string Sha1Hex(byte[] input)
    {
        using var sha1 = SHA1.Create();
        byte[] hash = sha1.ComputeHash(input);

        var sb = new StringBuilder(hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
            sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }
}

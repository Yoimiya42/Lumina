using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Resolve stable content directories for Lumina in a "portable zip" layout.
/// Expected layout (recommended):
///   LauncherRoot/
///     Launcher.exe
///     Games/Lumina/   (this Unity build)
///     UserContent/Lumina/Images
///     UserContent/Lumina/Thumbnails
///     UserContent/Config
///
/// This class finds LauncherRoot (parent of Games/) at runtime and creates folders if missing.
/// It also supports command-line override:
///   -luminaContentRoot "D:\SomePath\UserContent"
///   -luminaImagesDir   "D:\SomePath\UserContent\Lumina\Images"
/// </summary>
public static class LuminaContentPaths
{
    // Change only this if you rename the game folder in UserContent
    public const string GameFolderName = "Lumina";

    // Command-line flags (optional)
    private const string ArgContentRoot = "-luminaContentRoot";
    private const string ArgImagesDir = "-luminaImagesDir";

    /// <summary>
    /// Returns the directory that contains the running executable (or project in Editor).
    /// In Windows build: .../Games/Lumina/
    /// </summary>
    public static string GetGameRoot()
    {
        // In build, Application.dataPath = .../Lumina_Data
        // So exe folder = parent of dataPath
        // In Editor, dataPath = .../Assets, parent is project root.
        var dataPath = Application.dataPath;
        var parent = Directory.GetParent(dataPath);
        return parent != null ? parent.FullName : Application.persistentDataPath;
    }

    /// <summary>
    /// Try to resolve LauncherRoot.
    /// If the game is located at LauncherRoot/Games/Lumina/, then LauncherRoot is 2 levels up from exe folder.
    /// </summary>
    public static string GetLauncherRoot()
    {
        string gameRoot = GetGameRoot(); // .../Games/Lumina (in build)
        DirectoryInfo dir = new DirectoryInfo(gameRoot);

        // Heuristic: if current path ends with ".../Games/<something>", then launcher root is parent of "Games".
        // Example: LauncherRoot/Games/Lumina -> parent of Games is LauncherRoot
        // We'll search upwards for a folder named "Games".
        DirectoryInfo cursor = dir;
        while (cursor != null)
        {
            if (string.Equals(cursor.Name, "Games", StringComparison.OrdinalIgnoreCase))
                return cursor.Parent != null ? cursor.Parent.FullName : gameRoot;

            cursor = cursor.Parent;
        }

        // Fallback: assume launcher root = gameRoot's parent
        return Directory.GetParent(gameRoot)?.FullName ?? gameRoot;
    }

    /// <summary>
    /// Base UserContent folder.
    /// By default: LauncherRoot/UserContent
    /// Can be overridden by command line:
    ///   -luminaContentRoot "...\UserContent"
    /// </summary>
    public static string GetUserContentRoot()
    {
        string overrideRoot = GetCommandLineValue(ArgContentRoot);
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return NormalizePath(overrideRoot);

        return Path.Combine(GetLauncherRoot(), "UserContent");
    }

    /// <summary>
    /// Images directory.
    /// Default: LauncherRoot/UserContent/Lumina/Images
    /// Can be overridden by command line:
    ///   -luminaImagesDir "...\Images"
    /// </summary>
    public static string GetImagesDir()
    {
        string overrideImages = GetCommandLineValue(ArgImagesDir);
        if (!string.IsNullOrWhiteSpace(overrideImages))
            return NormalizePath(overrideImages);

        return Path.Combine(GetUserContentRoot(), GameFolderName, "Images");
    }

    /// <summary>
    /// Thumbnail cache directory (optional).
    /// Default: LauncherRoot/UserContent/Lumina/Thumbnails
    /// </summary>
    public static string GetThumbnailsDir()
    {
        return Path.Combine(GetUserContentRoot(), GameFolderName, "Thumbnails");
    }

    /// <summary>
    /// Config directory (optional, shared).
    /// Default: LauncherRoot/UserContent/Config
    /// </summary>
    public static string GetConfigDir()
    {
        return Path.Combine(GetUserContentRoot(), "Config");
    }

    /// <summary>
    /// Ensure the required folders exist.
    /// Call this once on startup.
    /// </summary>
    public static void EnsureFolders()
    {
        CreateIfMissing(GetUserContentRoot());
        CreateIfMissing(Path.Combine(GetUserContentRoot(), GameFolderName));
        CreateIfMissing(GetImagesDir());
        CreateIfMissing(GetThumbnailsDir());
        CreateIfMissing(GetConfigDir());
    }

    private static void CreateIfMissing(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LuminaContentPaths] Failed to create directory: {path}\n{e}");
        }
    }

    private static string GetCommandLineValue(string key)
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }
        }
        catch { /* ignore */ }
        return null;
    }

    private static string NormalizePath(string p)
    {
        try { return Path.GetFullPath(p); }
        catch { return p; }
    }
}

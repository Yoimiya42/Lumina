using System;
using System.IO;
using UnityEngine;

public static class ContentPaths
{
 
    /// In build: Application.dataPath = .../<GameName>_Data
    /// GameRoot = parent of dataPath = folder containing the EXE.
    /// In Editor: parent of Assets folder.
    public static string GetGameRoot()
    {
        var parent = Directory.GetParent(Application.dataPath);
        return parent != null ? parent.FullName : Application.persistentDataPath;
    }

    /// Finds LauncherRoot by searching upwards for a folder named settings.gamesFolderName.
    /// If found: LauncherRoot = parent of that folder.
    /// If not found: fallback to GameRoot's parent.
    public static string GetLauncherRoot(PathSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (!string.IsNullOrWhiteSpace(settings.launcherRootAbsoluteOverride))
            return NormalizePath(settings.launcherRootAbsoluteOverride);

        string gameRoot = GetGameRoot();
        DirectoryInfo cursor = new DirectoryInfo(gameRoot);

        while (cursor != null)
        {
            if (string.Equals(cursor.Name, settings.gamesFolder, StringComparison.OrdinalIgnoreCase))
                return cursor.Parent != null ? cursor.Parent.FullName : gameRoot;

            cursor = cursor.Parent;
        }   
        return Directory.GetParent(gameRoot)?.FullName ?? gameRoot;
    }

    private static string NormalizePath(string path)
    {
        try {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    public static string GetUserContentRoot(PathSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (!string.IsNullOrWhiteSpace(settings.userContentAbsoluteOverride))
            return NormalizePath(settings.userContentAbsoluteOverride);

        return Path.Combine(GetLauncherRoot(settings), settings.userContentFolder);
    }

    public static string GetMyGameContentRoot(PathSettings settings)
    {
        return Path.Combine(GetUserContentRoot(settings), settings.myGameFolder);
    }

    public static string GetImagesFolder(PathSettings settings)
    {
        return Path.Combine(GetMyGameContentRoot(settings), settings.imagesFolder);
    }

    public static string GetThumbnailsFolder(PathSettings settings)
    {
        return Path.Combine(GetMyGameContentRoot(settings), settings.thumbnailsFolder);
    }

    public static void EnsureFolders(PathSettings settings)
    {
        CreateIfMissing(GetUserContentRoot(settings));
        CreateIfMissing(GetMyGameContentRoot(settings));
        CreateIfMissing(GetImagesFolder(settings));
        CreateIfMissing(GetThumbnailsFolder(settings));
    }

    private static void CreateIfMissing(string path)
    {
        try { 
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch (Exception e) 
        {
            Debug.LogWarning($"Failed to create directory: {path}\n");
        }

    }
}

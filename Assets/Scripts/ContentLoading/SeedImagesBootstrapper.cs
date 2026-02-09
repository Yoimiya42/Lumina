using System.IO;
using UnityEngine;

public class SeedImagesBootstrapper : MonoBehaviour
{
    [SerializeField] private PathSettings pathSettings;
    [SerializeField] private string seedFolderName = "SeedImages";

    private void Awake()
    {
        if (pathSettings == null)
        {
            Debug.LogError("[SeedImagesBootstrapper] Missing PathSettings.");
            return;
        }

        ContentPaths.EnsureFolders(pathSettings);

        string dstImages = ContentPaths.GetImagesFolder(pathSettings);

        string srcSeed = Path.Combine(Application.streamingAssetsPath, seedFolderName);

        if (Directory.Exists(dstImages) &&
            Directory.GetFiles(dstImages, "*.*", SearchOption.AllDirectories).Length > 0)
        {
            Debug.Log("[SeedImagesBootstrapper] Images already exist, skip seeding: " + dstImages);
            return;
        }

        if (!Directory.Exists(srcSeed))
        {
            Debug.LogWarning("[SeedImagesBootstrapper] Seed folder not found: " + srcSeed);
            return;
        }

        Directory.CreateDirectory(dstImages);
        CopyDirectoryRecursive(srcSeed, dstImages);

        Debug.Log($"[SeedImagesBootstrapper] Seeded images: {srcSeed} -> {dstImages}");
    }

    private static void CopyDirectoryRecursive(string src, string dst)
    {
        Directory.CreateDirectory(dst);

        foreach (var file in Directory.GetFiles(src))
        {
            var name = Path.GetFileName(file);
            File.Copy(file, Path.Combine(dst, name), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(src))
        {
            var name = Path.GetFileName(dir);
            CopyDirectoryRecursive(dir, Path.Combine(dst, name));
        }
    }
}

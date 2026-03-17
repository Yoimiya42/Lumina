using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ContentLoadingTests
{
    private readonly List<GameObject> _createdObjects = new();
    private readonly List<ScriptableObject> _createdScriptableObjects = new();
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();

    [SetUp]
    public void SetUp()
    {
        ResetImageProgressRepository(null, configured: false);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
        }

        for (int i = _createdScriptableObjects.Count - 1; i >= 0; i--)
        {
            if (_createdScriptableObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdScriptableObjects[i]);
        }

        for (int i = _createdFiles.Count - 1; i >= 0; i--)
        {
            if (File.Exists(_createdFiles[i]))
                File.Delete(_createdFiles[i]);
        }

        for (int i = _createdDirectories.Count - 1; i >= 0; i--)
        {
            if (Directory.Exists(_createdDirectories[i]))
                Directory.Delete(_createdDirectories[i], recursive: true);
        }

        _createdObjects.Clear();
        _createdScriptableObjects.Clear();
        _createdFiles.Clear();
        _createdDirectories.Clear();

        ResetImageProgressRepository(null, configured: false);
    }

    [Test]
    public void ContentPaths_GetLauncherRoot_WhenGamesFolderMatchesCurrentRoot_ReturnsParent()
    {
        var settings = CreatePathSettings();
        string gameRoot = ContentPaths.GetGameRoot();
        var gameRootInfo = new DirectoryInfo(gameRoot);

        settings.gamesFolder = gameRootInfo.Name;

        string launcherRoot = ContentPaths.GetLauncherRoot(settings);
        string expected = gameRootInfo.Parent != null ? gameRootInfo.Parent.FullName : gameRoot;

        Assert.That(launcherRoot, Is.EqualTo(expected));
    }

    [Test]
    public void ContentPaths_GetLauncherRoot_WhenGamesFolderNotFound_FallsBackToGameRoot()
    {
        var settings = CreatePathSettings();
        settings.gamesFolder = $"DefinitelyMissing_{Guid.NewGuid():N}";

        Assert.That(ContentPaths.GetLauncherRoot(settings), Is.EqualTo(ContentPaths.GetGameRoot()));
    }

    [Test]
    public void ContentPaths_WithOverrides_BuildsExpectedFolders()
    {
        string tempRoot = CreateTempDirectory();
        string launcherOverride = Path.Combine(tempRoot, ".", "LauncherRoot");
        string userContentOverride = Path.Combine(tempRoot, "CustomUserContent");
        var settings = CreatePathSettings();

        settings.launcherRootAbsoluteOverride = launcherOverride;
        settings.userContentAbsoluteOverride = userContentOverride;

        string expectedLauncher = Path.GetFullPath(launcherOverride);
        string expectedUserContent = Path.GetFullPath(userContentOverride);
        string expectedGameContent = Path.Combine(expectedUserContent, settings.myGameFolder);

        Assert.That(ContentPaths.GetLauncherRoot(settings), Is.EqualTo(expectedLauncher));
        Assert.That(ContentPaths.GetUserContentRoot(settings), Is.EqualTo(expectedUserContent));
        Assert.That(ContentPaths.GetMyGameContentRoot(settings), Is.EqualTo(expectedGameContent));
        Assert.That(ContentPaths.GetImagesFolder(settings), Is.EqualTo(Path.Combine(expectedGameContent, settings.imagesFolder)));
        Assert.That(ContentPaths.GetThumbnailsFolder(settings), Is.EqualTo(Path.Combine(expectedGameContent, settings.thumbnailsFolder)));
        Assert.That(ContentPaths.GetSavesFolder(settings), Is.EqualTo(Path.Combine(expectedGameContent, settings.savesFolder)));
    }

    [Test]
    public void ContentPaths_EnsureFolders_CreatesExpectedDirectoryTree()
    {
        string tempRoot = CreateTempDirectory();
        var settings = CreatePathSettings();

        settings.userContentAbsoluteOverride = Path.Combine(tempRoot, "UserContentRoot");

        ContentPaths.EnsureFolders(settings);

        Assert.That(Directory.Exists(ContentPaths.GetUserContentRoot(settings)), Is.True);
        Assert.That(Directory.Exists(ContentPaths.GetMyGameContentRoot(settings)), Is.True);
        Assert.That(Directory.Exists(ContentPaths.GetImagesFolder(settings)), Is.True);
        Assert.That(Directory.Exists(ContentPaths.GetThumbnailsFolder(settings)), Is.True);
        Assert.That(Directory.Exists(ContentPaths.GetSavesFolder(settings)), Is.True);
    }

    [Test]
    public void ContentBootstrap_Awake_WithMissingPathSettings_DisablesComponentAndLogsError()
    {
        var bootstrapObject = new GameObject("ContentBootstrap", typeof(ContentBootstrap));
        var bootstrap = bootstrapObject.GetComponent<ContentBootstrap>();

        _createdObjects.Add(bootstrapObject);

        LogAssert.Expect(LogType.Error, "ContentBootstrap] Missing LuminaPathSettings reference.");
        InvokePrivate(bootstrap, "Awake");

        Assert.That(bootstrap.enabled, Is.False);
    }

    [Test]
    public void ContentBootstrap_Awake_WithPathSettings_CreatesFolders()
    {
        string tempRoot = CreateTempDirectory();
        var settings = CreatePathSettings();
        var bootstrapObject = new GameObject("ContentBootstrap", typeof(ContentBootstrap));
        var bootstrap = bootstrapObject.GetComponent<ContentBootstrap>();

        settings.userContentAbsoluteOverride = Path.Combine(tempRoot, "UserContentRoot");

        _createdObjects.Add(bootstrapObject);
        SetPrivateField(bootstrap, "pathSettings", settings);
        InvokePrivate(bootstrap, "Awake");

        Assert.That(bootstrap.enabled, Is.True);
        Assert.That(Directory.Exists(ContentPaths.GetImagesFolder(settings)), Is.True);
        Assert.That(Directory.Exists(ContentPaths.GetThumbnailsFolder(settings)), Is.True);
        Assert.That(Directory.Exists(ContentPaths.GetSavesFolder(settings)), Is.True);
    }

    [Test]
    public void ImageProgressRepository_Configure_UsesPreferredSaveFolder_WhenWritable()
    {
        string tempRoot = CreateTempDirectory();
        var settings = CreatePathSettings();

        settings.userContentAbsoluteOverride = Path.Combine(tempRoot, "UserContentRoot");

        ResetImageProgressRepository(null, configured: false);
        ImageProgressRepository.Configure(settings);

        string expectedPath = Path.Combine(ContentPaths.GetSavesFolder(settings), "luminate_image_progress_db.json");
        Assert.That(ImageProgressRepository.DebugGetFilePath(), Is.EqualTo(expectedPath));
    }

    [Test]
    public void ImageProgressRepository_SetAndTryGet_PersistsClampedClonedData()
    {
        string filePath = CreateTempFilePath(".json");
        float[] cells = { 0.2f, 0.4f, 0.6f, 0.8f };

        ResetImageProgressRepository(filePath, configured: true);
        ImageProgressRepository.Set("image-a", Difficulty.Hard, 0, -5, cells, 1.8f);
        cells[0] = 9f;

        ResetImageProgressRepository(filePath, configured: true);

        bool found = ImageProgressRepository.TryGet("image-a", out var entry);

        Assert.That(found, Is.True);
        Assert.That(entry.lockedDifficulty, Is.EqualTo((int)Difficulty.Hard));
        Assert.That(entry.progress01, Is.EqualTo(1f));
        Assert.That(entry.gridX, Is.EqualTo(1));
        Assert.That(entry.gridY, Is.EqualTo(1));
        Assert.That(entry.cells, Is.EqualTo(new[] { 0.2f, 0.4f, 0.6f, 0.8f }));
    }

    [Test]
    public void ImageProgressRepository_TryGet_SanitizesMalformedDataLoadedFromDisk()
    {
        string filePath = CreateTempFilePath(".json");
        File.WriteAllText(
            filePath,
            "{\"version\":1,\"entries\":[{\"imageId\":\"image-b\",\"lockedDifficulty\":99,\"progress01\":-3,\"gridX\":0,\"gridY\":-2,\"cells\":null,\"lastUpdatedUtcTicks\":123}]}");

        ResetImageProgressRepository(filePath, configured: true);

        bool found = ImageProgressRepository.TryGet("image-b", out var entry);

        Assert.That(found, Is.True);
        Assert.That(entry.lockedDifficulty, Is.EqualTo(2));
        Assert.That(entry.progress01, Is.EqualTo(0f));
        Assert.That(entry.gridX, Is.EqualTo(1));
        Assert.That(entry.gridY, Is.EqualTo(1));
        Assert.That(entry.cells, Is.Empty);
    }

    [Test]
    public void ImageProgressRepository_Reset_RemovesEntryFromRepository()
    {
        string filePath = CreateTempFilePath(".json");

        ResetImageProgressRepository(filePath, configured: true);
        ImageProgressRepository.Set("image-c", Difficulty.Medium, 4, 4, new float[16], 0.5f);
        ImageProgressRepository.Reset("image-c");

        ResetImageProgressRepository(filePath, configured: true);

        Assert.That(ImageProgressRepository.TryGet("image-c", out _), Is.False);
    }

    [Test]
    public void ImageFolderScanner_Scan_LoadsAllowedImagesAndResolvesThemes()
    {
        string imagesRoot = CreateTempDirectory();
        string themeDirectory = Directory.CreateDirectory(Path.Combine(imagesRoot, "Animals")).FullName;
        string rootImage = Path.Combine(imagesRoot, "fern.png");
        string themedImage = Path.Combine(themeDirectory, "cat.png");

        WriteSolidPng(rootImage, Color.green);
        WriteSolidPng(themedImage, Color.cyan);
        File.WriteAllText(Path.Combine(imagesRoot, "ignore.txt"), "not an image");

        var scanner = CreateScanner(imagesRoot, includeSubfolders: true);

        scanner.Scan();

        Assert.That(scanner.Items.Count, Is.EqualTo(2));

        var rootItem = scanner.Items.Single(x => x.fileName == "fern");
        var themeItem = scanner.Items.Single(x => x.fileName == "cat");

        Assert.That(rootItem.theme, Is.EqualTo(string.Empty));
        Assert.That(themeItem.theme, Is.EqualTo("Animals"));
        Assert.That(rootItem.size, Is.EqualTo(new Vector2Int(2, 2)));
        Assert.That(themeItem.size, Is.EqualTo(new Vector2Int(2, 2)));
        Assert.That(rootItem.imageId, Has.Length.EqualTo(40));
        Assert.That(themeItem.imageId, Has.Length.EqualTo(40));
    }

    [Test]
    public void ImageFolderScanner_Scan_WithTopDirectoryOnly_SkipsNestedImages()
    {
        string imagesRoot = CreateTempDirectory();
        string themeDirectory = Directory.CreateDirectory(Path.Combine(imagesRoot, "Animals")).FullName;

        WriteSolidPng(Path.Combine(imagesRoot, "fern.png"), Color.green);
        WriteSolidPng(Path.Combine(themeDirectory, "cat.png"), Color.cyan);

        var scanner = CreateScanner(imagesRoot, includeSubfolders: false);

        scanner.Scan();

        Assert.That(scanner.Items.Count, Is.EqualTo(1));
        Assert.That(scanner.Items[0].fileName, Is.EqualTo("fern"));
    }

    [Test]
    public void SeedImagesBootstrapper_Awake_SeedsImagesWhenDestinationIsEmpty()
    {
        string tempRoot = CreateTempDirectory();
        string seedFolderName = $"SeedImages_{Guid.NewGuid():N}";
        string seedRoot = CreateStreamingAssetsSeedFolder(seedFolderName);
        string themeRoot = Directory.CreateDirectory(Path.Combine(seedRoot, "Animals")).FullName;
        var settings = CreatePathSettings();
        var bootstrapObject = new GameObject("SeedImagesBootstrapper", typeof(SeedImagesBootstrapper));
        var bootstrap = bootstrapObject.GetComponent<SeedImagesBootstrapper>();

        settings.userContentAbsoluteOverride = Path.Combine(tempRoot, "UserContentRoot");
        WriteSolidPng(Path.Combine(seedRoot, "root.png"), Color.green);
        WriteSolidPng(Path.Combine(themeRoot, "cat.png"), Color.cyan);

        _createdObjects.Add(bootstrapObject);
        SetPrivateField(bootstrap, "pathSettings", settings);
        SetPrivateField(bootstrap, "seedFolderName", seedFolderName);

        InvokePrivate(bootstrap, "Awake");

        string dstImages = ContentPaths.GetImagesFolder(settings);
        Assert.That(File.Exists(Path.Combine(dstImages, "root.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(dstImages, "Animals", "cat.png")), Is.True);
    }

    [Test]
    public void SeedImagesBootstrapper_Awake_SkipsSeedingWhenImagesAlreadyExist()
    {
        string tempRoot = CreateTempDirectory();
        string seedFolderName = $"SeedImages_{Guid.NewGuid():N}";
        string seedRoot = CreateStreamingAssetsSeedFolder(seedFolderName);
        var settings = CreatePathSettings();
        var bootstrapObject = new GameObject("SeedImagesBootstrapper", typeof(SeedImagesBootstrapper));
        var bootstrap = bootstrapObject.GetComponent<SeedImagesBootstrapper>();

        settings.userContentAbsoluteOverride = Path.Combine(tempRoot, "UserContentRoot");
        ContentPaths.EnsureFolders(settings);

        string dstImages = ContentPaths.GetImagesFolder(settings);
        WriteSolidPng(Path.Combine(dstImages, "existing.png"), Color.yellow);
        WriteSolidPng(Path.Combine(seedRoot, "new-seed.png"), Color.magenta);

        _createdObjects.Add(bootstrapObject);
        SetPrivateField(bootstrap, "pathSettings", settings);
        SetPrivateField(bootstrap, "seedFolderName", seedFolderName);

        InvokePrivate(bootstrap, "Awake");

        Assert.That(File.Exists(Path.Combine(dstImages, "existing.png")), Is.True);
        Assert.That(File.Exists(Path.Combine(dstImages, "new-seed.png")), Is.False);
    }

    [Test]
    public void SeedImagesBootstrapper_Awake_WhenSeedFolderMissing_LogsWarningAndLeavesImagesEmpty()
    {
        string tempRoot = CreateTempDirectory();
        string seedFolderName = $"MissingSeed_{Guid.NewGuid():N}";
        var settings = CreatePathSettings();
        var bootstrapObject = new GameObject("SeedImagesBootstrapper", typeof(SeedImagesBootstrapper));
        var bootstrap = bootstrapObject.GetComponent<SeedImagesBootstrapper>();

        settings.userContentAbsoluteOverride = Path.Combine(tempRoot, "UserContentRoot");

        _createdObjects.Add(bootstrapObject);
        SetPrivateField(bootstrap, "pathSettings", settings);
        SetPrivateField(bootstrap, "seedFolderName", seedFolderName);

        LogAssert.Expect(LogType.Warning, new Regex(@"\[SeedImagesBootstrapper\] Seed folder not found:"));
        InvokePrivate(bootstrap, "Awake");

        string dstImages = ContentPaths.GetImagesFolder(settings);
        Assert.That(Directory.Exists(dstImages), Is.True);
        Assert.That(Directory.GetFiles(dstImages, "*.*", SearchOption.AllDirectories), Is.Empty);
    }

    private ImageFolderScanner CreateScanner(string imagesRoot, bool includeSubfolders)
    {
        var scannerObject = new GameObject("ImageFolderScanner", typeof(ImageFolderScanner));
        var scanner = scannerObject.GetComponent<ImageFolderScanner>();

        _createdObjects.Add(scannerObject);
        SetPrivateField(scanner, "imagesDirAbsoluteOverride", imagesRoot);
        SetPrivateField(scanner, "includeSubfolders", includeSubfolders);
        SetPrivateField(scanner, "extensions", new[] { ".png" });

        return scanner;
    }

    private PathSettings CreatePathSettings()
    {
        var settings = ScriptableObject.CreateInstance<PathSettings>();

        settings.gamesFolder = "Games";
        settings.userContentFolder = "UserContent";
        settings.myGameFolder = "Lumina";
        settings.imagesFolder = "Images";
        settings.thumbnailsFolder = "Thumbnails";
        settings.savesFolder = "Saves";
        settings.launcherRootAbsoluteOverride = string.Empty;
        settings.userContentAbsoluteOverride = string.Empty;

        _createdScriptableObjects.Add(settings);
        return settings;
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumina-content-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _createdDirectories.Add(path);
        return path;
    }

    private string CreateStreamingAssetsSeedFolder(string seedFolderName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, seedFolderName);
        Directory.CreateDirectory(path);
        _createdDirectories.Add(path);
        return path;
    }

    private string CreateTempFilePath(string extension)
    {
        string directory = CreateTempDirectory();
        string path = Path.Combine(directory, $"data{extension}");
        _createdFiles.Add(path);
        return path;
    }

    private static void WriteSolidPng(string path, Color color)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { color, color, color, color });
        texture.Apply();

        try
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ResetImageProgressRepository(string filePath, bool configured)
    {
        SetStaticField(typeof(ImageProgressRepository), "_configured", configured);
        SetStaticField(typeof(ImageProgressRepository), "_filePath", filePath);
        SetStaticField(typeof(ImageProgressRepository), "_db", null);
        SetStaticField(typeof(ImageProgressRepository), "_map", null);
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' was not found on {target.GetType().Name}.");
        return method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static void SetStaticField(Type targetType, string fieldName, object value)
    {
        var field = targetType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Static field '{fieldName}' was not found on {targetType.Name}.");
        field.SetValue(null, value);
    }
}

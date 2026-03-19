using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract class PlayModeIntegrationTestBase
{
    private readonly List<GameObject> _roots = new();
    private readonly List<UnityEngine.Object> _createdUnityObjects = new();
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();

    protected void BeforeEach()
    {
        ResetImageProgressRepository(null, configured: false);
    }

    protected IEnumerator AfterEach()
    {
        for (int i = _createdUnityObjects.Count - 1; i >= 0; i--)
        {
            if (_createdUnityObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdUnityObjects[i]);
        }

        for (int i = _roots.Count - 1; i >= 0; i--)
        {
            if (_roots[i] != null)
                UnityEngine.Object.Destroy(_roots[i]);
        }

        yield return null;

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

        _createdUnityObjects.Clear();
        _roots.Clear();
        _createdFiles.Clear();
        _createdDirectories.Clear();

        ResetImageProgressRepository(null, configured: false);
    }

    protected IEnumerator ActivateHarness(IntegrationHarness harness)
    {
        harness.Root.SetActive(true);
        yield return null;
        yield return null;
    }

    protected IntegrationHarness CreateHarness(string imagesRoot, Difficulty selectedDifficulty)
    {
        string tempRoot = CreateTempDirectory();
        var settings = CreatePathSettings(Path.Combine(tempRoot, "UserContentRoot"));

        var root = new GameObject("PlayModeIntegrationHarness");
        root.SetActive(false);
        _roots.Add(root);

        var canvasObject = CreateUiObject("CanvasRoot", root.transform, typeof(Canvas));
        var prefabShelf = CreateUiObject("PrefabShelf", root.transform);
        prefabShelf.SetActive(false);

        var menuPanel = CreateUiObject("MenuPanel", canvasObject.transform);
        var contentObject = CreateUiObject("ContentRoot", menuPanel.transform);
        var dropdownObject = CreateUiObject("DifficultyDropdown", menuPanel.transform, typeof(TMP_Dropdown));
        var startButtonObject = CreateUiObject("StartButton", menuPanel.transform, typeof(Button));
        var resetButtonObject = CreateUiObject("ResetButton", menuPanel.transform, typeof(Button));
        var scannerObject = CreateObject("ImageFolderScanner", menuPanel.transform, typeof(ImageFolderScanner));
        var builderObject = CreateObject("ThemeMenuBuilder", menuPanel.transform, typeof(ThemeMenuBuilder));

        var gamePanel = CreateUiObject("GamePanel", canvasObject.transform);
        var imageObject = CreateUiObject("GameImage", gamePanel.transform, typeof(Image), typeof(AspectRatioFitter));
        var painterObject = CreateObject("Painter", root.transform, typeof(Painter));
        var entryObject = CreateObject("GameEntryController", root.transform, typeof(GameEntryController));
        var exitObject = CreateObject("GameExitController", root.transform, typeof(GameExitController));

        var sectionPrefab = CreateSectionPrefab(prefabShelf.transform);
        var thumbnailPrefab = CreateThumbnailPrefab(prefabShelf.transform);

        var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        dropdown.options = new List<TMP_Dropdown.OptionData>
        {
            new("Easy"),
            new("Medium"),
            new("Hard")
        };
        dropdown.value = (int)selectedDifficulty;

        var scanner = scannerObject.GetComponent<ImageFolderScanner>();
        SetPrivateField(scanner, "pathSettings", settings);
        SetPrivateField(scanner, "imagesDirAbsoluteOverride", imagesRoot);
        SetPrivateField(scanner, "includeSubfolders", true);
        SetPrivateField(scanner, "extensions", new[] { ".png" });

        var builder = builderObject.GetComponent<ThemeMenuBuilder>();
        SetPrivateField(builder, "contentRoot", contentObject.GetComponent<RectTransform>());
        SetPrivateField(builder, "sectionPrefab", sectionPrefab);
        SetPrivateField(builder, "thumbnailPrefab", thumbnailPrefab);
        SetPrivateField(builder, "scanner", scanner);
        SetPrivateField(builder, "difficultyDropdown", dropdown);
        SetPrivateField(builder, "startButton", startButtonObject.GetComponent<Button>());
        SetPrivateField(builder, "resetButton", resetButtonObject.GetComponent<Button>());

        var painter = painterObject.GetComponent<Painter>();
        SetPrivateField(painter, "targetImage", imageObject.GetComponent<Image>());
        SetPrivateField(painter, "targetCanvas", canvasObject.GetComponent<Canvas>());
        SetPrivateField(painter, "mainMaterial", CreatePainterMaterial());

        var entryController = entryObject.GetComponent<GameEntryController>();
        SetPrivateField(entryController, "menuBuilder", builder);
        SetPrivateField(entryController, "menuPanel", menuPanel);
        SetPrivateField(entryController, "gamePanel", gamePanel);
        SetPrivateField(entryController, "gameColorImage", imageObject.GetComponent<Image>());
        SetPrivateField(entryController, "aspectFitter", imageObject.GetComponent<AspectRatioFitter>());
        SetPrivateField(entryController, "painter", painter);

        var exitController = exitObject.GetComponent<GameExitController>();
        SetPrivateField(exitController, "entryController", entryController);
        SetPrivateField(exitController, "painter", painter);
        SetPrivateField(exitController, "menuPanel", menuPanel);
        SetPrivateField(exitController, "gamePanel", gamePanel);
        SetPrivateField(exitController, "menuRootForThumbnails", contentObject.transform);

        return new IntegrationHarness
        {
            Root = root,
            Builder = builder,
            EntryController = entryController,
            ExitController = exitController,
            Painter = painter,
            MenuPanel = menuPanel,
            GamePanel = gamePanel,
            ContentRoot = contentObject.GetComponent<RectTransform>(),
            DifficultyDropdown = dropdown,
            StartButton = startButtonObject.GetComponent<Button>(),
            ResetButton = resetButtonObject.GetComponent<Button>(),
            GameImage = imageObject.GetComponent<Image>()
        };
    }

    protected ThumbnailItemView FindThumbnail(IntegrationHarness harness, string fileName)
    {
        var thumbnail = harness.ContentRoot
            .GetComponentsInChildren<ThumbnailItemView>(true)
            .SingleOrDefault(x => string.Equals(Path.GetFileNameWithoutExtension(x.ImagePath), fileName, StringComparison.OrdinalIgnoreCase));

        Assert.That(thumbnail, Is.Not.Null, $"Thumbnail '{fileName}' was not built.");
        return thumbnail;
    }

    protected void TrackRuntimeSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        _createdUnityObjects.Add(sprite.texture);
        _createdUnityObjects.Add(sprite);
    }

    protected void WriteSolidPng(string path, Color color)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.SetPixels(new[] { color, color, color, color });
        texture.Apply();

        try
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            _createdFiles.Add(path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    protected string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumina-playmode-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _createdDirectories.Add(path);
        return path;
    }

    protected static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        return field.GetValue(target) as T;
    }

    protected static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    protected static void ResetImageProgressRepository(string filePath, bool configured)
    {
        SetStaticField(typeof(ImageProgressRepository), "_configured", configured);
        SetStaticField(typeof(ImageProgressRepository), "_filePath", filePath);
        SetStaticField(typeof(ImageProgressRepository), "_db", null);
        SetStaticField(typeof(ImageProgressRepository), "_map", null);
    }

    private ThemeSectionView CreateSectionPrefab(Transform parent)
    {
        var sectionObject = CreateUiObject("SectionPrefab", parent, typeof(ThemeSectionView));
        var titleObject = CreateUiObject("Title", sectionObject.transform, typeof(TextMeshProUGUI));
        var bodyObject = CreateUiObject(
            "Body",
            sectionObject.transform,
            typeof(GridLayoutGroup),
            typeof(LayoutElement),
            typeof(ThemeBodyHeightFitter));

        var bodyGrid = bodyObject.GetComponent<GridLayoutGroup>();
        bodyGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        bodyGrid.constraintCount = 2;
        bodyGrid.cellSize = new Vector2(80f, 80f);
        bodyGrid.spacing = new Vector2(8f, 8f);

        var section = sectionObject.GetComponent<ThemeSectionView>();
        SetPrivateField(section, "themeText", titleObject.GetComponent<TextMeshProUGUI>());
        SetPrivateField(section, "bodyRoot", bodyObject.GetComponent<RectTransform>());
        SetPrivateField(section, "bodyFitter", bodyObject.GetComponent<ThemeBodyHeightFitter>());

        return section;
    }

    private ThumbnailItemView CreateThumbnailPrefab(Transform parent)
    {
        var thumbnailObject = CreateUiObject(
            "ThumbnailPrefab",
            parent,
            typeof(Button),
            typeof(Outline),
            typeof(ThumbnailItemView));
        var thumbImageObject = CreateUiObject("ThumbImage", thumbnailObject.transform, typeof(Image));
        var progressTextObject = CreateUiObject("ProgressText", thumbnailObject.transform, typeof(TextMeshProUGUI));
        var trophyIconObject = CreateUiObject("TrophyIcon", thumbnailObject.transform, typeof(Image));

        var thumbnail = thumbnailObject.GetComponent<ThumbnailItemView>();
        SetPrivateField(thumbnail, "thumbImage", thumbImageObject.GetComponent<Image>());
        SetPrivateField(thumbnail, "progressText", progressTextObject.GetComponent<TextMeshProUGUI>());
        SetPrivateField(thumbnail, "trophyIcon", trophyIconObject.GetComponent<Image>());

        return thumbnail;
    }

    private Material CreatePainterMaterial()
    {
        var shader = Shader.Find("Shader Graphs/SG_GrayscaleToColor");
        Assert.That(shader, Is.Not.Null, "Shader 'Shader Graphs/SG_GrayscaleToColor' was not found for Play Mode tests.");

        var material = new Material(shader);
        _createdUnityObjects.Add(material);
        return material;
    }

    private PathSettings CreatePathSettings(string userContentRoot)
    {
        var settings = ScriptableObject.CreateInstance<PathSettings>();
        settings.gamesFolder = "Games";
        settings.userContentFolder = "UserContent";
        settings.myGameFolder = "Lumina";
        settings.imagesFolder = "Images";
        settings.thumbnailsFolder = "Thumbnails";
        settings.savesFolder = "Saves";
        settings.userContentAbsoluteOverride = userContentRoot;

        _createdUnityObjects.Add(settings);
        return settings;
    }

    private GameObject CreateObject(string name, Transform parent, params Type[] components)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);

        for (int i = 0; i < components.Length; i++)
            gameObject.AddComponent(components[i]);

        return gameObject;
    }

    private GameObject CreateUiObject(string name, Transform parent, params Type[] extraComponents)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);

        for (int i = 0; i < extraComponents.Length; i++)
            gameObject.AddComponent(extraComponents[i]);

        return gameObject;
    }

    private static void SetStaticField(Type targetType, string fieldName, object value)
    {
        var field = targetType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Static field '{fieldName}' was not found on {targetType.Name}.");
        field.SetValue(null, value);
    }

    protected sealed class IntegrationHarness
    {
        public GameObject Root { get; set; }
        public ThemeMenuBuilder Builder { get; set; }
        public GameEntryController EntryController { get; set; }
        public GameExitController ExitController { get; set; }
        public Painter Painter { get; set; }
        public GameObject MenuPanel { get; set; }
        public GameObject GamePanel { get; set; }
        public RectTransform ContentRoot { get; set; }
        public TMP_Dropdown DifficultyDropdown { get; set; }
        public Button StartButton { get; set; }
        public Button ResetButton { get; set; }
        public Image GameImage { get; set; }
    }
}

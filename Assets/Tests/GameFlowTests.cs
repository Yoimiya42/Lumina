using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class GameFlowTests
{
    private readonly List<GameObject> _createdObjects = new();
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();
    private readonly List<UnityEngine.Object> _createdUnityObjects = new();

    [SetUp]
    public void SetUp()
    {
        ResetImageProgressRepository(null, configured: false);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdUnityObjects.Count - 1; i >= 0; i--)
        {
            if (_createdUnityObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdUnityObjects[i]);
        }

        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
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

        _createdUnityObjects.Clear();
        _createdObjects.Clear();
        _createdFiles.Clear();
        _createdDirectories.Clear();

        ResetImageProgressRepository(null, configured: false);
    }

    [Test]
    public void GameEntryController_LoadSpriteFromFile_LoadsPngSprite()
    {
        string imagePath = CreateTempFilePath(".png");
        WriteSolidPng(imagePath, Color.magenta);

        var sprite = (Sprite)InvokePrivateStatic(typeof(GameEntryController), "LoadSpriteFromFile", imagePath);

        Assert.That(sprite, Is.Not.Null);
        Assert.That(sprite.rect.width, Is.EqualTo(2));
        Assert.That(sprite.rect.height, Is.EqualTo(2));

        UnityEngine.Object.DestroyImmediate(sprite.texture);
        UnityEngine.Object.DestroyImmediate(sprite);
    }

    [Test]
    public void GameEntryController_LoadSpriteFromFile_MissingPathReturnsNull()
    {
        var sprite = (Sprite)InvokePrivateStatic(typeof(GameEntryController), "LoadSpriteFromFile", "Z:\\does-not-exist.png");

        Assert.That(sprite, Is.Null);
    }

    [Test]
    public void EnterGame_WhenNoSelection_DoesNotSwitchPanels()
    {
        var harness = CreateEntryHarness(null, null, Difficulty.Medium);

        LogAssert.Expect(LogType.Warning, new Regex(@"\[GameEntryController\] No selection\."));
        harness.EntryController.EnterGame();

        Assert.That(harness.MenuPanel.activeSelf, Is.True);
        Assert.That(harness.GamePanel.activeSelf, Is.False);
        Assert.That(harness.EntryController.CurrentImageId, Is.Null);
        Assert.That(harness.GameImage.sprite, Is.Null);
    }

    [Test]
    public void EnterGame_WhenImageFileIsInvalid_DoesNotEnterGame()
    {
        string imagePath = CreateTempFilePath(".png");
        File.WriteAllText(imagePath, "not a real png");

        var harness = CreateEntryHarness(imagePath, "broken-image", Difficulty.Easy);

        LogAssert.Expect(LogType.Error, new Regex(@"\[GameEntryController\] Failed to load:"));
        harness.EntryController.EnterGame();

        Assert.That(harness.MenuPanel.activeSelf, Is.True);
        Assert.That(harness.GamePanel.activeSelf, Is.False);
        Assert.That(harness.EntryController.CurrentImageId, Is.Null);
        Assert.That(harness.GameImage.sprite, Is.Null);
    }

    [Test]
    public void EnterGame_WithSavedProgress_RestoresLockedDifficultyAndCells()
    {
        string dbPath = CreateTempFilePath(".json");
        string imagePath = CreateTempFilePath(".png");
        string imageId = "restored-image";
        var savedCells = new float[16 * 16];

        for (int i = 0; i < savedCells.Length / 2; i++)
            savedCells[i] = 1f;

        WriteSolidPng(imagePath, Color.cyan);
        ResetImageProgressRepository(dbPath, configured: true);
        ImageProgressRepository.Set(imageId, Difficulty.Hard, 16, 16, savedCells, 0.5f);

        var harness = CreateEntryHarness(imagePath, imageId, Difficulty.Easy);

        harness.EntryController.EnterGame();
        TrackRuntimeSprite(harness.GameImage.sprite);

        Assert.That(harness.MenuPanel.activeSelf, Is.False);
        Assert.That(harness.GamePanel.activeSelf, Is.True);
        Assert.That(harness.EntryController.CurrentImageId, Is.EqualTo(imageId));
        Assert.That(harness.EntryController.CurrentDifficulty, Is.EqualTo(Difficulty.Hard));
        Assert.That(harness.Painter.GridX, Is.EqualTo(16));
        Assert.That(harness.Painter.GridY, Is.EqualTo(16));
        Assert.That(harness.Painter.GetCellsCopy(), Is.EqualTo(savedCells));
        Assert.That(harness.GameImage.sprite, Is.Not.Null);
        Assert.That(harness.AspectFitter.aspectRatio, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void GameExitController_BackToMenuAndSave_PersistsProgressAndSwitchesPanels()
    {
        string dbPath = CreateTempFilePath(".json");
        ResetImageProgressRepository(dbPath, configured: true);

        var entryObject = CreateObject("Entry", typeof(GameEntryController));
        var painterObject = CreateObject("Painter", typeof(Painter));
        var exitObject = CreateObject("Exit", typeof(GameExitController));
        var menuPanel = CreateObject("MenuPanel");
        var gamePanel = CreateObject("GamePanel");

        var entryController = entryObject.GetComponent<GameEntryController>();
        var painter = painterObject.GetComponent<Painter>();
        var exitController = exitObject.GetComponent<GameExitController>();

        menuPanel.SetActive(false);
        gamePanel.SetActive(true);

        SetAutoPropertyBackingField(entryController, "CurrentImageId", "image-save");
        SetAutoPropertyBackingField(entryController, "CurrentDifficulty", Difficulty.Medium);

        SetPrivateField(painter, "gridX", 2);
        SetPrivateField(painter, "gridY", 2);
        SetPrivateField(painter, "cell", new[] { 1f, 0.5f, 0.5f, 0f });
        SetPrivateField(painter, "totalFill01", 2f);

        SetPrivateField(exitController, "entryController", entryController);
        SetPrivateField(exitController, "painter", painter);
        SetPrivateField(exitController, "menuPanel", menuPanel);
        SetPrivateField(exitController, "gamePanel", gamePanel);

        exitController.BackToMenuAndSave();

        Assert.That(menuPanel.activeSelf, Is.True);
        Assert.That(gamePanel.activeSelf, Is.False);
        Assert.That(ImageProgressRepository.TryGet("image-save", out var entry), Is.True);
        Assert.That(entry.lockedDifficulty, Is.EqualTo((int)Difficulty.Medium));
        Assert.That(entry.progress01, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(entry.gridX, Is.EqualTo(2));
        Assert.That(entry.gridY, Is.EqualTo(2));
        Assert.That(entry.cells, Is.EqualTo(new[] { 1f, 0.5f, 0.5f, 0f }));
    }

    private EntryHarness CreateEntryHarness(string imagePath, string imageId, Difficulty selectedDifficulty)
    {
        var menuBuilderObject = CreateObject("MenuBuilder", typeof(ThemeMenuBuilder));
        var dropdownObject = CreateObject("DifficultyDropdown", typeof(RectTransform), typeof(TMP_Dropdown));
        var menuPanel = CreateObject("MenuPanel");
        var gamePanel = CreateObject("GamePanel");
        var entryObject = CreateObject("EntryController", typeof(GameEntryController));

        var menuBuilder = menuBuilderObject.GetComponent<ThemeMenuBuilder>();
        var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        dropdown.options = new List<TMP_Dropdown.OptionData>
        {
            new("Easy"),
            new("Medium"),
            new("Hard")
        };
        dropdown.value = (int)selectedDifficulty;

        SetPrivateField(menuBuilder, "difficultyDropdown", dropdown);
        SetAutoPropertyBackingField(menuBuilder, "SelectedImagePath", imagePath);
        SetAutoPropertyBackingField(menuBuilder, "SelectedImageId", imageId);

        var painterHarness = CreatePainterHarness();
        var entryController = entryObject.GetComponent<GameEntryController>();

        SetPrivateField(entryController, "menuBuilder", menuBuilder);
        SetPrivateField(entryController, "menuPanel", menuPanel);
        SetPrivateField(entryController, "gamePanel", gamePanel);
        SetPrivateField(entryController, "gameColorImage", painterHarness.GameImage);
        SetPrivateField(entryController, "aspectFitter", painterHarness.AspectFitter);
        SetPrivateField(entryController, "painter", painterHarness.Painter);

        InvokePrivate(entryController, "Awake");

        return new EntryHarness
        {
            EntryController = entryController,
            Painter = painterHarness.Painter,
            MenuPanel = menuPanel,
            GamePanel = gamePanel,
            GameImage = painterHarness.GameImage,
            AspectFitter = painterHarness.AspectFitter
        };
    }

    private PainterHarness CreatePainterHarness()
    {
        var canvasObject = CreateObject("Canvas", typeof(Canvas));
        var imageObject = CreateObject("GameImage", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        imageObject.transform.SetParent(canvasObject.transform, false);

        var painterObject = CreateObject("Painter", typeof(Painter));
        var painter = painterObject.GetComponent<Painter>();
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/M_GrayscaleToColor.mat");

        Assert.That(material, Is.Not.Null, "Failed to load test material from Assets/Shaders/M_GrayscaleToColor.mat.");

        SetPrivateField(painter, "targetImage", imageObject.GetComponent<Image>());
        SetPrivateField(painter, "targetCanvas", canvasObject.GetComponent<Canvas>());
        SetPrivateField(painter, "mainMaterial", material);
        InvokePrivate(painter, "Awake");

        return new PainterHarness
        {
            Painter = painter,
            GameImage = imageObject.GetComponent<Image>(),
            AspectFitter = imageObject.GetComponent<AspectRatioFitter>()
        };
    }

    private void TrackRuntimeSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        _createdUnityObjects.Add(sprite.texture);
        _createdUnityObjects.Add(sprite);
    }

    private GameObject CreateObject(string name, params Type[] componentTypes)
    {
        var gameObject = componentTypes.Length == 0
            ? new GameObject(name)
            : new GameObject(name, componentTypes);

        _createdObjects.Add(gameObject);
        return gameObject;
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumina-gameflow-tests-{Guid.NewGuid():N}");
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
        texture.SetPixels(Enumerable.Repeat(color, 4).ToArray());
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

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' was not found on {target.GetType().Name}.");
        return method.Invoke(target, args);
    }

    private static object InvokePrivateStatic(Type targetType, string methodName, params object[] args)
    {
        var method = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' was not found on {targetType.Name}.");
        return method.Invoke(null, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
    {
        SetPrivateField(target, $"<{propertyName}>k__BackingField", value);
    }

    private static void ResetImageProgressRepository(string filePath, bool configured)
    {
        SetStaticField(typeof(ImageProgressRepository), "_configured", configured);
        SetStaticField(typeof(ImageProgressRepository), "_filePath", filePath);
        SetStaticField(typeof(ImageProgressRepository), "_db", null);
        SetStaticField(typeof(ImageProgressRepository), "_map", null);
    }

    private static void SetStaticField(Type targetType, string fieldName, object value)
    {
        var field = targetType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Static field '{fieldName}' was not found on {targetType.Name}.");
        field.SetValue(null, value);
    }

    private sealed class EntryHarness
    {
        public GameEntryController EntryController { get; set; }
        public Painter Painter { get; set; }
        public GameObject MenuPanel { get; set; }
        public GameObject GamePanel { get; set; }
        public Image GameImage { get; set; }
        public AspectRatioFitter AspectFitter { get; set; }
    }

    private sealed class PainterHarness
    {
        public Painter Painter { get; set; }
        public Image GameImage { get; set; }
        public AspectRatioFitter AspectFitter { get; set; }
    }
}

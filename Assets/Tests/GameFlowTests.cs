using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameFlowTests
{
    private readonly List<GameObject> _createdObjects = new();
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
}

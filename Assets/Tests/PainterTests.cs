using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class PainterTests
{
    private readonly List<GameObject> _createdObjects = new();
    private readonly List<UnityEngine.Object> _createdUnityObjects = new();

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

        _createdUnityObjects.Clear();
        _createdObjects.Clear();
    }

    [Test]
    public void BeginOrRestore_WithSavedCells_RestoresProgressAndReturnsClonedCells()
    {
        var harness = CreatePainterHarness();
        var sprite = CreateSprite(Color.red);
        var savedCells = new float[64];

        savedCells[0] = 1f;
        savedCells[1] = 0.5f;
        savedCells[2] = 0.25f;

        harness.Painter.BeginOrRestore(sprite, Difficulty.Easy, savedCells);

        Assert.That(harness.Painter.GridX, Is.EqualTo(8));
        Assert.That(harness.Painter.GridY, Is.EqualTo(8));
        Assert.That(harness.TargetImage.sprite, Is.SameAs(sprite));
        Assert.That(harness.Painter.GetProgress01(), Is.EqualTo(1.75f / 64f).Within(0.0001f));

        var copy = harness.Painter.GetCellsCopy();
        Assert.That(copy, Is.EqualTo(savedCells));

        copy[0] = 0f;
        Assert.That(harness.Painter.GetCellsCopy()[0], Is.EqualTo(1f));
    }

    [Test]
    public void BeginOrRestore_WithInvalidSavedCellsLength_StartsFromEmptyState()
    {
        var harness = CreatePainterHarness();
        var sprite = CreateSprite(Color.green);

        harness.Painter.BeginOrRestore(sprite, Difficulty.Medium, new float[5]);

        Assert.That(harness.Painter.GridX, Is.EqualTo(12));
        Assert.That(harness.Painter.GridY, Is.EqualTo(12));
        Assert.That(harness.Painter.GetProgress01(), Is.EqualTo(0f));
        Assert.That(harness.Painter.GetCellsCopy().All(x => Mathf.Approximately(x, 0f)), Is.True);
    }

    [Test]
    public void ClearAll_AfterRestoredState_ResetsCellsAndProgress()
    {
        var harness = CreatePainterHarness();
        var sprite = CreateSprite(Color.blue);
        var savedCells = new float[64];

        savedCells[10] = 1f;
        savedCells[20] = 0.75f;

        harness.Painter.BeginOrRestore(sprite, Difficulty.Easy, savedCells);
        harness.Painter.ClearAll();

        Assert.That(harness.Painter.GetProgress01(), Is.EqualTo(0f));
        Assert.That(harness.Painter.GetCellsCopy().All(x => Mathf.Approximately(x, 0f)), Is.True);
    }

    private PainterHarness CreatePainterHarness()
    {
        var canvasObject = CreateObject("Canvas", typeof(Canvas));
        var imageObject = CreateObject("GameImage", typeof(RectTransform), typeof(Image));
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
            TargetImage = imageObject.GetComponent<Image>()
        };
    }

    private Sprite CreateSprite(Color color)
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var colors = Enumerable.Repeat(color, 16).ToArray();
        texture.SetPixels(colors);
        texture.Apply();

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        _createdUnityObjects.Add(texture);
        _createdUnityObjects.Add(sprite);
        return sprite;
    }

    private GameObject CreateObject(string name, params Type[] components)
    {
        var gameObject = components.Length == 0
            ? new GameObject(name)
            : new GameObject(name, components);

        _createdObjects.Add(gameObject);
        return gameObject;
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

    private sealed class PainterHarness
    {
        public Painter Painter { get; set; }
        public Image TargetImage { get; set; }
    }
}

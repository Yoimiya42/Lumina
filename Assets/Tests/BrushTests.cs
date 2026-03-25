using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class BrushTests
{
    private readonly List<GameObject> _createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
        }

        _createdObjects.Clear();
    }

    [TestCase(Difficulty.Easy, 8, 8)]
    [TestCase(Difficulty.Medium, 12, 12)]
    [TestCase(Difficulty.Hard, 16, 16)]
    public void PaintGridMath_ResolveGridSize_ReturnsExpectedGrid(Difficulty difficulty, int expectedX, int expectedY)
    {
        var result = PaintGridMath.ResolveGridSize(difficulty);

        Assert.That(result.gridX, Is.EqualTo(expectedX));
        Assert.That(result.gridY, Is.EqualTo(expectedY));
    }

    [Test]
    public void PaintGridMath_GetCoveredRange_ClampsIndicesToGridBounds()
    {
        PaintGridMath.GetCoveredRange(
            new Vector2(0.05f, 0.95f),
            0.2f,
            4,
            2,
            out int minX,
            out int maxX,
            out int minY,
            out int maxY,
            out float cellW,
            out float cellH,
            out float radiusSquared);

        Assert.That(minX, Is.EqualTo(0));
        Assert.That(maxX, Is.EqualTo(1));
        Assert.That(minY, Is.EqualTo(1));
        Assert.That(maxY, Is.EqualTo(1));
        Assert.That(cellW, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(cellH, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(radiusSquared, Is.EqualTo(0.04f).Within(0.0001f));
    }

    [Test]
    public void PaintGridMath_CircleIntersectsRect_ReturnsTrueWhenTouchingCell()
    {
        bool hit = PaintGridMath.CircleIntersectsRect(
            new Vector2(0.5f, 0.5f),
            0.01f,
            0.4f,
            0.4f,
            0.6f,
            0.6f);

        Assert.That(hit, Is.True);
    }

    [Test]
    public void PaintGridMath_CircleIntersectsRect_ReturnsFalseWhenSeparated()
    {
        bool hit = PaintGridMath.CircleIntersectsRect(
            new Vector2(0.1f, 0.1f),
            0.0025f,
            0.4f,
            0.4f,
            0.6f,
            0.6f);

        Assert.That(hit, Is.False);
    }

    [Test]
    public void BrushSizeUI_SelectPreset_UpdatesPainterRadiusAndButtonStates()
    {
        var painterObject = CreateObject("Painter", typeof(Painter));
        var brushUiObject = CreateObject("BrushSizeUI", typeof(BrushSizeUI));
        var painter = painterObject.GetComponent<Painter>();
        var brushUi = brushUiObject.GetComponent<BrushSizeUI>();
        var buttons = new[]
        {
            CreateObject("Button0", typeof(Button)).GetComponent<Button>(),
            CreateObject("Button1", typeof(Button)).GetComponent<Button>(),
            CreateObject("Button2", typeof(Button)).GetComponent<Button>()
        };

        SetPrivateField(brushUi, "painter", painter);
        SetPrivateField(brushUi, "presets", new[] { 0.02f, 0.05f, 0.1f });
        SetPrivateField(brushUi, "buttons", buttons);

        brushUi.SelectPreset(1);

        Assert.That(painter.GetBrushRadiusUV(), Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(buttons[0].interactable, Is.True);
        Assert.That(buttons[1].interactable, Is.False);
        Assert.That(buttons[2].interactable, Is.True);
    }

    [Test]
    public void BrushSizeUI_SelectPreset_InvalidIndex_DoesNotChangePainterRadius()
    {
        var painterObject = CreateObject("Painter", typeof(Painter));
        var brushUiObject = CreateObject("BrushSizeUI", typeof(BrushSizeUI));
        var painter = painterObject.GetComponent<Painter>();
        var brushUi = brushUiObject.GetComponent<BrushSizeUI>();

        painter.SetBrushRadius(0.07f);
        SetPrivateField(brushUi, "painter", painter);
        SetPrivateField(brushUi, "presets", new[] { 0.02f, 0.05f, 0.1f });

        brushUi.SelectPreset(99);

        Assert.That(painter.GetBrushRadiusUV(), Is.EqualTo(0.07f).Within(0.0001f));
    }

    [Test]
    public void Breath_CombineUrl_TrimsSlashesBeforeJoining()
    {
        string combined = (string)InvokePrivateStatic(
            typeof(Breath),
            "CombineUrl",
            "http://127.0.0.1:8000/",
            "/webhooks/breathing-volume");

        Assert.That(combined, Is.EqualTo("http://127.0.0.1:8000/webhooks/breathing-volume"));
    }

    [Test]
    public void Breath_TryParseSingleFloat_ParsesScientificNotation()
    {
        object[] args = { "{\"breathing_rate\":1.25e-1}", "breathing_rate", 0f };
        bool parsed = (bool)InvokePrivateStatic(typeof(Breath), "TryParseSingleFloat", args);

        Assert.That(parsed, Is.True);
        Assert.That((float)args[2], Is.EqualTo(0.125f).Within(0.0001f));
    }

    [Test]
    public void Breath_ComputeMultiplierAndGate_AppliesBonusesAndTurnsPaintingOn()
    {
        var painterObject = CreateObject("Painter", typeof(Painter));
        var breathObject = CreateObject("Breath", typeof(Breath));
        var painter = painterObject.GetComponent<Painter>();
        var breath = breathObject.GetComponent<Breath>();

        SetPrivateField(breath, "painter", painter);
        SetPrivateField(breath, "gatePainting", true);
        SetPrivateField(breath, "breathOnThreshold01", 0.2f);
        SetPrivateField(breath, "breathOffThreshold01", 0.1f);
        SetPrivateField(breath, "minMultiplier", 1f);
        SetPrivateField(breath, "maxMultiplier", 3f);
        SetPrivateField(breath, "gamma", 1f);
        SetPrivateField(breath, "rawVolumeSensitivity", 1f);
        SetPrivateField(breath, "rawSignalBlend", 1f);
        SetPrivateField(breath, "breathHoldSec", 0f);
        SetPrivateField(breath, "activeSignalFloor01", 0f);
        SetPrivateField(breath, "useRegularity", true);
        SetPrivateField(breath, "regularityWeight", 0.5f);
        SetPrivateField(breath, "useRateBonus", true);
        SetPrivateField(breath, "targetBpmMin", 6f);
        SetPrivateField(breath, "targetBpmMax", 10f);
        SetPrivateField(breath, "bpmBonus", 0.25f);
        SetPrivateField(breath, "calibLerp", 0f);
        SetPrivateField(breath, "_vMin", 0f);
        SetPrivateField(breath, "_vMax", 1f);

        float multiplier = (float)InvokePrivate(
            breath,
            "ComputeMultiplierAndGate",
            0.5f,
            0.4f,
            0.12f);

        Assert.That(multiplier, Is.EqualTo(1.75f).Within(0.0001f));
        Assert.That((bool)GetPrivateField(painter, "breathPaintActive"), Is.True);
    }

    [Test]
    public void Breath_ComputeMultiplierAndGate_DropsBelowOffThresholdAndTurnsPaintingOff()
    {
        var painterObject = CreateObject("Painter", typeof(Painter));
        var breathObject = CreateObject("Breath", typeof(Breath));
        var painter = painterObject.GetComponent<Painter>();
        var breath = breathObject.GetComponent<Breath>();

        SetPrivateField(breath, "painter", painter);
        SetPrivateField(breath, "gatePainting", true);
        SetPrivateField(breath, "breathOnThreshold01", 0.2f);
        SetPrivateField(breath, "breathOffThreshold01", 0.1f);
        SetPrivateField(breath, "minMultiplier", 1f);
        SetPrivateField(breath, "maxMultiplier", 2f);
        SetPrivateField(breath, "gamma", 1f);
        SetPrivateField(breath, "rawVolumeSensitivity", 1f);
        SetPrivateField(breath, "rawSignalBlend", 1f);
        SetPrivateField(breath, "breathHoldSec", 0f);
        SetPrivateField(breath, "activeSignalFloor01", 0f);
        SetPrivateField(breath, "useRegularity", false);
        SetPrivateField(breath, "useRateBonus", false);
        SetPrivateField(breath, "calibLerp", 0f);
        SetPrivateField(breath, "_vMin", 0f);
        SetPrivateField(breath, "_vMax", 1f);

        float multiplier = (float)InvokePrivate(
            breath,
            "ComputeMultiplierAndGate",
            0.05f,
            1f,
            0f);

        Assert.That(multiplier, Is.EqualTo(1.05f).Within(0.0001f));
        Assert.That((bool)GetPrivateField(painter, "breathPaintActive"), Is.False);
    }

    [Test]
    public void Breath_ComputeMultiplierAndGate_HoldsBreathStateBrieflyAcrossSmallDips()
    {
        var painterObject = CreateObject("Painter", typeof(Painter));
        var breathObject = CreateObject("Breath", typeof(Breath));
        var painter = painterObject.GetComponent<Painter>();
        var breath = breathObject.GetComponent<Breath>();

        SetPrivateField(breath, "painter", painter);
        SetPrivateField(breath, "gatePainting", true);
        SetPrivateField(breath, "breathOnThreshold01", 0.2f);
        SetPrivateField(breath, "breathOffThreshold01", 0.1f);
        SetPrivateField(breath, "minMultiplier", 1f);
        SetPrivateField(breath, "maxMultiplier", 2f);
        SetPrivateField(breath, "gamma", 1f);
        SetPrivateField(breath, "rawVolumeSensitivity", 20f);
        SetPrivateField(breath, "rawSignalBlend", 1f);
        SetPrivateField(breath, "breathHoldSec", 0.5f);
        SetPrivateField(breath, "activeSignalFloor01", 0f);
        SetPrivateField(breath, "useRegularity", false);
        SetPrivateField(breath, "useRateBonus", false);
        SetPrivateField(breath, "calibLerp", 0f);
        SetPrivateField(breath, "_vMin", 0f);
        SetPrivateField(breath, "_vMax", 1f);

        InvokePrivate(
            breath,
            "ComputeMultiplierAndGate",
            0.02f,
            1f,
            0f);

        float multiplier = (float)InvokePrivate(
            breath,
            "ComputeMultiplierAndGate",
            0.008f,
            1f,
            0f);

        Assert.That(multiplier, Is.EqualTo(1.16f).Within(0.0001f));
        Assert.That((bool)GetPrivateField(painter, "breathPaintActive"), Is.True);
    }

    private GameObject CreateObject(string name, params Type[] componentTypes)
    {
        var gameObject = componentTypes.Length == 0
            ? new GameObject(name)
            : new GameObject(name, componentTypes);

        _createdObjects.Add(gameObject);
        return gameObject;
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

    private static object GetPrivateField(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}

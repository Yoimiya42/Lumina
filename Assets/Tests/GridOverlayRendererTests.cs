using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class GridOverlayRendererTests
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

    [Test]
    public void Configure_BuildsExpectedNumberOfCells()
    {
        var overlay = CreateOverlay();

        overlay.Configure(3, 2);

        Assert.That(overlay.transform.childCount, Is.EqualTo(6));
        Assert.That(GetCell(overlay, 0, 0), Is.Not.Null);
        Assert.That(GetCell(overlay, 2, 1), Is.Not.Null);
    }

    [Test]
    public void SetCellCompleted_WhenDisableOnComplete_IsTrue_DisablesCellObject()
    {
        var overlay = CreateOverlay(disableOnComplete: true);

        overlay.Configure(1, 1);
        overlay.SetCellCompleted(0, 0, true);

        Assert.That(GetCell(overlay, 0, 0).gameObject.activeSelf, Is.False);
    }

    [Test]
    public void SetCellCompleted_WhenDisableOnComplete_IsFalse_AppliesCompletedColor()
    {
        var overlay = CreateOverlay(disableOnComplete: false);
        var completedColor = new Color(0.2f, 0.7f, 0.4f, 1f);

        SetPrivateField(overlay, "completedColor", completedColor);
        overlay.Configure(1, 1);
        overlay.SetCellCompleted(0, 0, true);

        AssertColor(completedColor, GetFirstLineImage(GetCell(overlay, 0, 0)).color);
        Assert.That(GetCell(overlay, 0, 0).gameObject.activeSelf, Is.True);
    }

    [Test]
    public void HighlightCell_ThenClearHighlights_ChangesAndResetsBorderColor()
    {
        var overlay = CreateOverlay(disableOnComplete: false);
        var normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        var highlightColor = new Color(0.8f, 0.2f, 0.1f, 1f);

        SetPrivateField(overlay, "normalColor", normalColor);
        SetPrivateField(overlay, "highlightColor", highlightColor);
        overlay.Configure(1, 1);

        overlay.HighlightCell(0, 0);
        AssertColor(highlightColor, GetFirstLineImage(GetCell(overlay, 0, 0)).color);

        overlay.ClearHighlights();
        AssertColor(normalColor, GetFirstLineImage(GetCell(overlay, 0, 0)).color);
    }

    [Test]
    public void ApplyCompletedFromCells_MarksOnlyCompletedCells()
    {
        var overlay = CreateOverlay(disableOnComplete: true);

        overlay.Configure(2, 2);
        overlay.ApplyCompletedFromCells(new[] { 1f, 0f, 0f, 1f });

        Assert.That(GetCell(overlay, 0, 0).gameObject.activeSelf, Is.False);
        Assert.That(GetCell(overlay, 1, 0).gameObject.activeSelf, Is.True);
        Assert.That(GetCell(overlay, 0, 1).gameObject.activeSelf, Is.True);
        Assert.That(GetCell(overlay, 1, 1).gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator Rebuild_AfterGridSizeChange_ReplacesExistingGrid()
    {
        var overlay = CreateOverlay();

        overlay.Configure(2, 2);
        SetPrivateField(overlay, "gridX", 1);
        SetPrivateField(overlay, "gridY", 1);
        overlay.Rebuild();

        yield return null;

        Assert.That(overlay.transform.childCount, Is.EqualTo(1));
        Assert.That(GetCell(overlay, 0, 0), Is.Not.Null);
    }

    private GridOverlayRenderer CreateOverlay(bool disableOnComplete = true)
    {
        var targetObject = CreateObject("Target", typeof(RectTransform));
        var overlayObject = CreateObject("Overlay", typeof(RectTransform), typeof(GridOverlayRenderer));
        overlayObject.transform.SetParent(targetObject.transform, false);

        var overlay = overlayObject.GetComponent<GridOverlayRenderer>();
        SetPrivateField(overlay, "disableOnComplete", disableOnComplete);

        return overlay;
    }

    private GameObject CreateObject(string name, params Type[] components)
    {
        var gameObject = components.Length == 0
            ? new GameObject(name)
            : new GameObject(name, components);

        _createdObjects.Add(gameObject);
        return gameObject;
    }

    private static Transform GetCell(GridOverlayRenderer overlay, int x, int y)
    {
        return overlay.transform.Find($"Cell_{x}_{y}");
    }

    private static Image GetFirstLineImage(Transform cell)
    {
        return cell.GetChild(0).GetComponent<Image>();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static void AssertColor(Color expected, Color actual)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
    }
}

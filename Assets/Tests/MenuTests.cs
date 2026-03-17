using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuTests
{
    private readonly List<GameObject> _createdObjects = new();
    private string _progressFilePath;

    [SetUp]
    public void SetUp()
    {
        _progressFilePath = Path.Combine(Path.GetTempPath(), $"lumina-menu-tests-{Guid.NewGuid():N}.json");
        ResetImageProgressRepository(_progressFilePath);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _createdObjects.Count - 1; i >= 0; i--)
        {
            if (_createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(_createdObjects[i]);
        }

        _createdObjects.Clear();
        ResetImageProgressRepository(_progressFilePath);

        if (!string.IsNullOrEmpty(_progressFilePath) && File.Exists(_progressFilePath))
            File.Delete(_progressFilePath);
    }

    [Test]
    public void ThemeBodyHeightFitter_Refit_UsesOnlyActiveChildren()
    {
        var body = CreateBodyRoot("Body");
        var grid = body.GetComponent<GridLayoutGroup>();
        var layout = body.GetComponent<LayoutElement>();
        var fitter = body.GetComponent<ThemeBodyHeightFitter>();

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.cellSize = new Vector2(80f, 20f);
        grid.spacing = new Vector2(0f, 6f);
        grid.padding = new RectOffset(0, 0, 4, 8);

        CreateUiObject("ItemA", body.transform);
        CreateUiObject("ItemB", body.transform);
        var hidden = CreateUiObject("Hidden", body.transform);
        hidden.SetActive(false);

        InitializeBodyFitter(fitter, grid, layout, body.GetComponent<RectTransform>());
        fitter.Refit();

        Assert.That(layout.preferredHeight, Is.EqualTo(32f).Within(0.001f));
    }

    [Test]
    public void ThemeBodyHeightFitter_Refit_WithNoActiveChildren_SetsZeroHeight()
    {
        var body = CreateBodyRoot("Body");
        var grid = body.GetComponent<GridLayoutGroup>();
        var layout = body.GetComponent<LayoutElement>();
        var fitter = body.GetComponent<ThemeBodyHeightFitter>();

        InitializeBodyFitter(fitter, grid, layout, body.GetComponent<RectTransform>());
        fitter.Refit();

        Assert.That(layout.preferredHeight, Is.EqualTo(0f));
    }

    [Test]
    public void ThemeSectionView_SetExpanded_UpdatesBodyStateAndTitle()
    {
        var sectionObject = CreateUiObject("Section", null, typeof(ThemeSectionView));
        var titleObject = CreateUiObject("Title", sectionObject.transform, typeof(TextMeshProUGUI));
        var bodyObject = CreateBodyRoot("Body", sectionObject.transform);
        var bodyGrid = bodyObject.GetComponent<GridLayoutGroup>();
        var bodyLayout = bodyObject.GetComponent<LayoutElement>();
        var bodyFitter = bodyObject.GetComponent<ThemeBodyHeightFitter>();
        var bodyRoot = bodyObject.GetComponent<RectTransform>();
        var title = titleObject.GetComponent<TextMeshProUGUI>();
        var section = sectionObject.GetComponent<ThemeSectionView>();

        bodyGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        bodyGrid.constraintCount = 1;
        bodyGrid.cellSize = new Vector2(50f, 18f);
        bodyGrid.padding = new RectOffset(0, 0, 2, 3);
        CreateUiObject("Thumb", bodyObject.transform);

        InitializeBodyFitter(bodyFitter, bodyGrid, bodyLayout, bodyRoot);
        SetPrivateField(section, "themeText", title);
        SetPrivateField(section, "bodyRoot", bodyRoot);
        SetPrivateField(section, "bodyFitter", bodyFitter);

        section.SetTitle("Nature");
        section.SetExpanded(true, force: true);

        Assert.That(title.text, Is.EqualTo("Nature"));
        Assert.That(section.IsExpanded, Is.True);
        Assert.That(bodyRoot.gameObject.activeSelf, Is.True);
        Assert.That(bodyLayout.preferredHeight, Is.EqualTo(23f).Within(0.001f));

        section.Toggle();

        Assert.That(section.IsExpanded, Is.False);
        Assert.That(bodyRoot.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void ThemeSectionToggle_StandaloneToggle_UpdatesBodyAndHeaderColor()
    {
        var collapsedColor = new Color(0.2f, 0.3f, 0.4f, 1f);
        var expandedColor = new Color(0.7f, 0.8f, 0.9f, 1f);
        var toggleObject = CreateUiObject("Header", null, typeof(Image), typeof(ThemeSectionToggle));
        var body = CreateUiObject("Body");
        var background = toggleObject.GetComponent<Image>();
        var toggle = toggleObject.GetComponent<ThemeSectionToggle>();

        SetPrivateField(toggle, "headerBackground", background);
        SetPrivateField(toggle, "body", body);
        SetPrivateField(toggle, "collapsedColor", collapsedColor);
        SetPrivateField(toggle, "expandedColor", expandedColor);

        toggle.SetExpanded(false);
        Assert.That(body.activeSelf, Is.False);
        AssertColor(collapsedColor, background.color);

        toggle.Toggle();

        Assert.That(body.activeSelf, Is.True);
        AssertColor(expandedColor, background.color);
    }

    [Test]
    public void ThemeSectionToggle_DelegatedToggle_UpdatesSectionViewAndHeaderColor()
    {
        var collapsedColor = new Color(0.1f, 0.2f, 0.3f, 1f);
        var expandedColor = new Color(0.5f, 0.6f, 0.7f, 1f);
        var sectionObject = CreateUiObject("Section", null, typeof(ThemeSectionView));
        var bodyObject = CreateUiObject("Body", sectionObject.transform);
        var headerObject = CreateUiObject("Header", sectionObject.transform, typeof(Image), typeof(ThemeSectionToggle));
        var section = sectionObject.GetComponent<ThemeSectionView>();
        var toggle = headerObject.GetComponent<ThemeSectionToggle>();
        var background = headerObject.GetComponent<Image>();
        var bodyRoot = bodyObject.GetComponent<RectTransform>();

        SetPrivateField(section, "bodyRoot", bodyRoot);
        section.SetExpanded(false, force: true);

        SetPrivateField(toggle, "headerBackground", background);
        SetPrivateField(toggle, "body", bodyObject);
        SetPrivateField(toggle, "collapsedColor", collapsedColor);
        SetPrivateField(toggle, "expandedColor", expandedColor);
        SetPrivateField(toggle, "_sectionView", section);
        SetPrivateField(toggle, "_delegateToSectionView", true);

        toggle.Toggle();

        Assert.That(section.IsExpanded, Is.True);
        Assert.That(bodyObject.activeSelf, Is.True);
        AssertColor(expandedColor, background.color);

        toggle.SetExpanded(false);

        Assert.That(section.IsExpanded, Is.False);
        Assert.That(bodyObject.activeSelf, Is.False);
        AssertColor(collapsedColor, background.color);
    }

    [Test]
    public void ThemeMenuBuilder_Build_GroupsItemsByThemeAndDisablesControlsWithoutSelection()
    {
        var harness = CreateBuilderHarness();
        var items = new[]
        {
            CreateItem("Nature", "fern", "fern-path", "image-nature"),
            CreateItem("", "stone", "stone-path", "image-default"),
            CreateItem("Animals", "fox", "fox-path", "image-animals")
        };

        harness.Builder.Build(items);

        Assert.That(harness.ContentRoot.childCount, Is.EqualTo(3));
        CollectionAssert.AreEqual(
            new[] { "ThemeSection_Animals", "ThemeSection_Default", "ThemeSection_Nature" },
            Enumerable.Range(0, harness.ContentRoot.childCount)
                .Select(i => harness.ContentRoot.GetChild(i).name)
                .ToArray());

        Assert.That(harness.Builder.SelectedImageId, Is.Null);
        Assert.That(harness.Builder.SelectedImagePath, Is.Null);
        Assert.That(harness.StartButton.interactable, Is.False);
        Assert.That(harness.DifficultyDropdown.interactable, Is.False);
        Assert.That(harness.ResetButton.interactable, Is.False);
    }

    [Test]
    public void ThemeMenuBuilder_ThumbnailClick_SelectsAndDeselectsItem()
    {
        var harness = CreateBuilderHarness();
        var item = CreateItem("Nature", "fern", "fern-path", "image-1");

        harness.Builder.Build(new[] { item });

        var thumbnail = harness.ContentRoot.GetComponentInChildren<ThumbnailItemView>(true);
        InvokePrivate(thumbnail, "HandleClicked");

        Assert.That(harness.Builder.SelectedImagePath, Is.EqualTo("fern-path"));
        Assert.That(harness.Builder.SelectedImageId, Is.EqualTo("image-1"));
        Assert.That(harness.StartButton.interactable, Is.True);
        Assert.That(harness.DifficultyDropdown.interactable, Is.True);
        Assert.That(harness.ResetButton.interactable, Is.False);

        InvokePrivate(thumbnail, "HandleClicked");

        Assert.That(harness.Builder.SelectedImagePath, Is.Null);
        Assert.That(harness.Builder.SelectedImageId, Is.Null);
        Assert.That(harness.StartButton.interactable, Is.False);
        Assert.That(harness.DifficultyDropdown.interactable, Is.False);
        Assert.That(harness.ResetButton.interactable, Is.False);
    }

    [Test]
    public void ThemeMenuBuilder_SavedProgress_LocksDifficultyAndResetClearsEntry()
    {
        var harness = CreateBuilderHarness();
        var item = CreateItem("Nature", "fern", "fern-path", "image-progress");

        ImageProgressRepository.Set(item.imageId, Difficulty.Hard, 4, 4, new float[16], 0.5f);
        harness.Builder.Build(new[] { item });

        var thumbnail = harness.ContentRoot.GetComponentInChildren<ThumbnailItemView>(true);
        InvokePrivate(thumbnail, "HandleClicked");

        Assert.That(harness.Builder.SelectedDifficulty, Is.EqualTo(Difficulty.Hard));
        Assert.That(harness.DifficultyDropdown.interactable, Is.False);
        Assert.That(harness.ResetButton.interactable, Is.True);

        InvokePrivate(harness.Builder, "ResetSelected");

        Assert.That(ImageProgressRepository.TryGet(item.imageId, out _), Is.False);
        Assert.That(harness.DifficultyDropdown.interactable, Is.True);
        Assert.That(harness.ResetButton.interactable, Is.False);
    }

    private BuilderHarness CreateBuilderHarness()
    {
        var canvasObject = CreateUiObject("CanvasRoot", null, typeof(Canvas));
        var contentObject = CreateUiObject("ContentRoot", canvasObject.transform);
        var dropdownObject = CreateUiObject("DifficultyDropdown", canvasObject.transform, typeof(TMP_Dropdown));
        var startButtonObject = CreateUiObject("StartButton", canvasObject.transform, typeof(Button));
        var resetButtonObject = CreateUiObject("ResetButton", canvasObject.transform, typeof(Button));
        var builderObject = CreateUiObject("Builder", canvasObject.transform, typeof(ThemeMenuBuilder));

        var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        dropdown.options = new List<TMP_Dropdown.OptionData>
        {
            new("Easy"),
            new("Medium"),
            new("Hard")
        };
        dropdown.value = 1;

        var builder = builderObject.GetComponent<ThemeMenuBuilder>();
        SetPrivateField(builder, "contentRoot", contentObject.GetComponent<RectTransform>());
        SetPrivateField(builder, "sectionPrefab", CreateSectionPrefab());
        SetPrivateField(builder, "thumbnailPrefab", CreateThumbnailPrefab());
        SetPrivateField(builder, "difficultyDropdown", dropdown);
        SetPrivateField(builder, "startButton", startButtonObject.GetComponent<Button>());
        SetPrivateField(builder, "resetButton", resetButtonObject.GetComponent<Button>());

        return new BuilderHarness
        {
            Builder = builder,
            ContentRoot = contentObject.GetComponent<RectTransform>(),
            DifficultyDropdown = dropdown,
            StartButton = startButtonObject.GetComponent<Button>(),
            ResetButton = resetButtonObject.GetComponent<Button>()
        };
    }

    private ThemeSectionView CreateSectionPrefab()
    {
        var sectionObject = CreateUiObject("SectionPrefab", null, typeof(ThemeSectionView));
        var titleObject = CreateUiObject("Title", sectionObject.transform, typeof(TextMeshProUGUI));
        var bodyObject = CreateUiObject("Body", sectionObject.transform);
        var section = sectionObject.GetComponent<ThemeSectionView>();

        SetPrivateField(section, "themeText", titleObject.GetComponent<TextMeshProUGUI>());
        SetPrivateField(section, "bodyRoot", bodyObject.GetComponent<RectTransform>());

        return section;
    }

    private ThumbnailItemView CreateThumbnailPrefab()
    {
        var thumbnailObject = CreateUiObject("ThumbnailPrefab", null, typeof(Button), typeof(ThumbnailItemView));
        return thumbnailObject.GetComponent<ThumbnailItemView>();
    }

    private ImageFolderScanner.ImageItem CreateItem(string theme, string fileName, string filePath, string imageId)
    {
        return new ImageFolderScanner.ImageItem
        {
            theme = theme,
            fileName = fileName,
            filePath = filePath,
            imageId = imageId,
            sprite = null
        };
    }

    private GameObject CreateBodyRoot(string name, Transform parent = null)
    {
        return CreateUiObject(name, parent, typeof(GridLayoutGroup), typeof(LayoutElement), typeof(ThemeBodyHeightFitter));
    }

    private GameObject CreateUiObject(string name, Transform parent = null, params Type[] extraComponents)
    {
        var componentTypes = new List<Type> { typeof(RectTransform) };
        componentTypes.AddRange(extraComponents);

        var gameObject = new GameObject(name, componentTypes.ToArray());
        if (parent != null)
            gameObject.transform.SetParent(parent, false);

        _createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void InitializeBodyFitter(
        ThemeBodyHeightFitter fitter,
        GridLayoutGroup grid,
        LayoutElement layout,
        RectTransform rectTransform)
    {
        SetPrivateField(fitter, "_grid", grid);
        SetPrivateField(fitter, "_le", layout);
        SetPrivateField(fitter, "_rt", rectTransform);
    }

    private static void ResetImageProgressRepository(string filePath)
    {
        SetStaticField(typeof(ImageProgressRepository), "_configured", true);
        SetStaticField(typeof(ImageProgressRepository), "_filePath", filePath);
        SetStaticField(typeof(ImageProgressRepository), "_db", null);
        SetStaticField(typeof(ImageProgressRepository), "_map", null);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' was not found on {target.GetType().Name}.");
        method.Invoke(target, null);
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

    private static void AssertColor(Color expected, Color actual)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
    }

    private sealed class BuilderHarness
    {
        public ThemeMenuBuilder Builder { get; set; }
        public RectTransform ContentRoot { get; set; }
        public TMP_Dropdown DifficultyDropdown { get; set; }
        public Button StartButton { get; set; }
        public Button ResetButton { get; set; }
    }
}

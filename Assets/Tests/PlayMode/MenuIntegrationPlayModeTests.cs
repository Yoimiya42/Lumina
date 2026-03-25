using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class MenuIntegrationPlayModeTests : PlayModeIntegrationTestBase
{
    [SetUp]
    public void SetUp()
    {
        BeforeEach();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        return AfterEach();
    }

    [UnityTest]
    public IEnumerator ScannerAndMenuBuilder_Start_BuildsSectionsFromImagesDirectory()
    {
        string imagesRoot = CreateTempDirectory();
        string themeRoot = Directory.CreateDirectory(Path.Combine(imagesRoot, "Animals")).FullName;

        WriteSolidPng(Path.Combine(imagesRoot, "fern.png"), Color.green);
        WriteSolidPng(Path.Combine(themeRoot, "cat.png"), Color.cyan);

        var harness = CreateHarness(imagesRoot, Difficulty.Medium);

        yield return ActivateHarness(harness);

        Assert.That(harness.ContentRoot.childCount, Is.EqualTo(2));
        CollectionAssert.AreEqual(
            new[] { "ThemeSection_Animals", "ThemeSection_Default" },
            Enumerable.Range(0, harness.ContentRoot.childCount)
                .Select(i => harness.ContentRoot.GetChild(i).name)
                .ToArray());

        Assert.That(harness.ContentRoot.GetComponentsInChildren<ThumbnailItemView>(true).Length, Is.EqualTo(2));
        Assert.That(harness.StartButton.interactable, Is.False);
        Assert.That(harness.DifficultyDropdown.interactable, Is.False);
        Assert.That(harness.ResetButton.interactable, Is.False);
    }

    [UnityTest]
    public IEnumerator ShowColorButton_TogglesThumbnailPreviewMaterial()
    {
        string imagesRoot = CreateTempDirectory();
        WriteSolidPng(Path.Combine(imagesRoot, "fern.png"), Color.green);

        var harness = CreateHarness(imagesRoot, Difficulty.Medium);

        yield return ActivateHarness(harness);

        var thumbnail = FindThumbnail(harness, "fern");
        var thumbImage = GetPrivateField<UnityEngine.UI.Image>(thumbnail, "thumbImage");
        var grayscaleMaterial = GetPrivateField<Material>(harness.Builder, "thumbnailGrayscaleMaterial");

        Assert.That(thumbImage.material, Is.EqualTo(grayscaleMaterial));

        harness.ShowColorButton.onClick.Invoke();
        yield return null;

        Assert.That(harness.Builder.AreThumbnailsShownInColor, Is.True);
        Assert.That(thumbImage.material, Is.Null);

        harness.ShowColorButton.onClick.Invoke();
        yield return null;

        Assert.That(harness.Builder.AreThumbnailsShownInColor, Is.False);
        Assert.That(thumbImage.material, Is.EqualTo(grayscaleMaterial));
    }
}

using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class GameFlowIntegrationPlayModeTests : PlayModeIntegrationTestBase
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
    public IEnumerator SelectThumbnailAndEnterGame_SwitchesPanelsAndLoadsSprite()
    {
        string imagesRoot = CreateTempDirectory();
        string imagePath = Path.Combine(imagesRoot, "fox.png");
        WriteSolidPng(imagePath, Color.magenta);

        var harness = CreateHarness(imagesRoot, Difficulty.Medium);

        yield return ActivateHarness(harness);

        var thumbnail = FindThumbnail(harness, "fox");
        thumbnail.GetComponent<Button>().onClick.Invoke();
        yield return null;

        Assert.That(harness.Builder.SelectedImagePath, Is.EqualTo(imagePath));
        Assert.That(harness.Builder.SelectedImageId, Is.EqualTo(thumbnail.ImageId));
        Assert.That(harness.StartButton.interactable, Is.True);
        Assert.That(harness.DifficultyDropdown.interactable, Is.True);

        harness.EntryController.EnterGame();
        yield return null;

        TrackRuntimeSprite(harness.GameImage.sprite);

        Assert.That(harness.MenuPanel.activeSelf, Is.False);
        Assert.That(harness.GamePanel.activeSelf, Is.True);
        Assert.That(harness.EntryController.CurrentImageId, Is.EqualTo(thumbnail.ImageId));
        Assert.That(harness.EntryController.CurrentDifficulty, Is.EqualTo(Difficulty.Medium));
        Assert.That(harness.Painter.GridX, Is.EqualTo(12));
        Assert.That(harness.Painter.GridY, Is.EqualTo(12));
        Assert.That(harness.GameImage.sprite, Is.Not.Null);
        Assert.That(harness.GameImage.sprite.rect.width, Is.EqualTo(2));
        Assert.That(harness.GameImage.sprite.rect.height, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator SavedProgressAndBackToMenu_FlowsAcrossMenuGameAndRepository()
    {
        string imagesRoot = CreateTempDirectory();
        string imagePath = Path.Combine(imagesRoot, "owl.png");
        WriteSolidPng(imagePath, Color.yellow);

        var harness = CreateHarness(imagesRoot, Difficulty.Easy);

        yield return ActivateHarness(harness);

        var thumbnail = FindThumbnail(harness, "owl");
        var savedCells = new float[16 * 16];
        for (int i = 0; i < savedCells.Length / 2; i++)
            savedCells[i] = 1f;

        ImageProgressRepository.Set(thumbnail.ImageId, Difficulty.Hard, 16, 16, savedCells, 0.5f);
        thumbnail.RefreshProgressFromStore();

        thumbnail.GetComponent<Button>().onClick.Invoke();
        yield return null;

        Assert.That(harness.Builder.SelectedDifficulty, Is.EqualTo(Difficulty.Hard));
        Assert.That(harness.DifficultyDropdown.interactable, Is.False);
        Assert.That(harness.ResetButton.interactable, Is.True);

        harness.EntryController.EnterGame();
        yield return null;

        TrackRuntimeSprite(harness.GameImage.sprite);

        Assert.That(harness.EntryController.CurrentDifficulty, Is.EqualTo(Difficulty.Hard));
        Assert.That(harness.Painter.GridX, Is.EqualTo(16));
        Assert.That(harness.Painter.GridY, Is.EqualTo(16));
        Assert.That(harness.Painter.GetCellsCopy(), Is.EqualTo(savedCells));

        var updatedCells = new float[16 * 16];
        for (int i = 0; i < 16 * 12; i++)
            updatedCells[i] = 1f;

        SetPrivateField(harness.Painter, "cell", updatedCells);
        SetPrivateField(harness.Painter, "totalFill01", 16f * 12f);

        harness.ExitController.BackToMenuAndSave();
        yield return null;

        Assert.That(harness.MenuPanel.activeSelf, Is.True);
        Assert.That(harness.GamePanel.activeSelf, Is.False);
        Assert.That(ImageProgressRepository.TryGet(thumbnail.ImageId, out var entry), Is.True);
        Assert.That(entry.lockedDifficulty, Is.EqualTo((int)Difficulty.Hard));
        Assert.That(entry.progress01, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(entry.cells, Is.EqualTo(updatedCells));

        var progressText = GetPrivateField<TMP_Text>(thumbnail, "progressText");
        var trophyIcon = GetPrivateField<Image>(thumbnail, "trophyIcon");

        Assert.That(progressText.gameObject.activeSelf, Is.True);
        Assert.That(progressText.text, Is.EqualTo("75%"));
        Assert.That(trophyIcon.gameObject.activeSelf, Is.False);
    }
}

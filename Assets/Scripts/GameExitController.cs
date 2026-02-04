using UnityEngine;

public class GameExitController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameEntryController entryController;
    [SerializeField] private GridMaskPainter painter;

    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;

    [Header("Menu Refresh")]
    [Tooltip("Drag ThemesScrollView/Viewport/Content here (where thumbnails live)")]
    [SerializeField] private Transform menuRootForThumbnails;

    public void ExitToMenuAndSave()
    {
        if (entryController == null || painter == null)
        {
            Debug.LogError("[GameExitController] Missing refs.");
            return;
        }

        string imageId = entryController.CurrentImageId;
        if (!string.IsNullOrEmpty(imageId))
        {
            float progress01 = painter.GetProgress01();
            float[] cells = painter.GetCellsCopy();

            ImageProgressRepository.Set(
                imageId,
                entryController.CurrentDifficulty,
                painter.GridX,
                painter.GridY,
                cells,
                progress01
            );

            Debug.Log($"[GameExitController] Saved imageId={imageId} progress={progress01:P0} db={ImageProgressRepository.DebugGetFilePath()}");
        }

        gamePanel?.SetActive(false);
        menuPanel?.SetActive(true);

        RefreshMenuThumbnailProgress();
    }

    private void RefreshMenuThumbnailProgress()
    {
        Transform root = menuRootForThumbnails != null
            ? menuRootForThumbnails
            : (menuPanel != null ? menuPanel.transform : null);

        if (root == null) return;

        var thumbs = root.GetComponentsInChildren<ThumbnailItemView>(includeInactive: true);
        foreach (var t in thumbs)
        {
            if (t != null)
                t.RefreshProgressFromStore();
        }

        Canvas.ForceUpdateCanvases();
    }
}

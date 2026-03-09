using UnityEngine;

public class GameExitController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameEntryController entryController;
    [SerializeField] private Painter painter;

    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;

    [Header("Menu Refresh")]
    [Tooltip("Drag ThemesScrollView/Viewport/Content here (where thumbnails live)")]
    [SerializeField] private Transform menuRootForThumbnails;

    /// <summary>
    /// In-game Back button:
    /// Save progress, then return to menu.
    /// </summary>
    public void BackToMenuAndSave()
    {
        SaveCurrentProgress();

        gamePanel?.SetActive(false);
        menuPanel?.SetActive(true);

        RefreshMenuThumbnailProgress();
    }

    /// <summary>
    /// In-game Quit button:
    /// Save progress, then quit the whole application.
    /// </summary>
    public void QuitGameAndSave()
    {
        SaveCurrentProgress();
        QuitApplication();
    }

    /// <summary>
    /// Main menu Quit button:
    /// Quit directly without saving gameplay progress.
    /// </summary>
    public void QuitGameDirectly()
    {
        QuitApplication();
    }

    /// <summary>
    /// Shared save logic for in-game actions.
    /// </summary>
    private void SaveCurrentProgress()
    {
        if (entryController == null || painter == null)
        {
            Debug.LogError("[GameExitController] Missing refs, cannot save progress.");
            return;
        }

        string imageId = entryController.CurrentImageId;
        if (string.IsNullOrEmpty(imageId))
        {
            Debug.LogWarning("[GameExitController] CurrentImageId is empty, skip saving.");
            return;
        }

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

        Debug.Log(
            $"[GameExitController] Saved imageId={imageId} progress={progress01:P0} db={ImageProgressRepository.DebugGetFilePath()}"
        );
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

    private void QuitApplication()
    {
        Debug.Log("[GameExitController] Quitting application...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
using UnityEngine;

public class GameExitController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameEntryController entryController;
    [SerializeField] private GridMaskPainter painter;

    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;

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
        Canvas.ForceUpdateCanvases();
    }
}

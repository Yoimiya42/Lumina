using UnityEngine;
using UnityEngine.UI;

public class BrushSizeUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Painter painter;

    [Header("Preset Radii (UV)")]
    [SerializeField] private float[] presets = { 0.02f, 0.05f, 0.075f, 0.1f, 0.15f };

    [Header("Optional: highlight selected")]
    [SerializeField] private Button[] buttons;

    private int currentIndex = -1;

    private void Start()
    {
        // Default to 0.05f size
        SelectPreset(2);
    }

    public void SelectPreset(int index)
    {
        if (painter == null) return;
        if (index < 0 || index >= presets.Length) return;

        currentIndex = index;
        painter.SetBrushRadius(presets[index]);

        if (buttons != null && buttons.Length == presets.Length)
        {
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) buttons[i].interactable = (i != currentIndex);
        }
    }
}

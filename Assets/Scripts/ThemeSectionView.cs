using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ThemeSectionView : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TMP_Text themeText;   // ThemeHeaderItem/ThemeText
    [SerializeField] private Button toggleButton;  // ThemeHeaderItem (or a child button)

    [Header("Body")]
    [SerializeField] private RectTransform bodyRoot; // ThemeBody
    [SerializeField] private RectTransform gridRoot; // ThemeBody/GridRoot (has GridLayoutGroup)
    [SerializeField] private LayoutElement bodyLayout;
    [SerializeField] private GridLayoutGroup grid;

    [SerializeField] private bool startCollapsed = false;

    private bool _expanded;

    private void Awake()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);

        SetExpanded(!startCollapsed, true);
    }

    public void SetTitle(string title)
    {
        if (themeText != null)
            themeText.text = title;
    }

    public RectTransform GridRoot => gridRoot;

    public void SetExpanded(bool expanded, bool force = false)
    {
        if (!force && _expanded == expanded) return;
        _expanded = expanded;

        if (bodyRoot != null)
            bodyRoot.gameObject.SetActive(_expanded);

        if (_expanded) RefreshBodyHeight();
        else if (bodyLayout != null) bodyLayout.preferredHeight = 0f;
    }

    public void Toggle() => SetExpanded(!_expanded);

    public void RefreshBodyHeight()
    {
        if (grid == null || bodyLayout == null || gridRoot == null) return;

        int itemCount = gridRoot.childCount;
        int columns = Mathf.Max(1, grid.constraintCount);
        int rows = Mathf.CeilToInt(itemCount / (float)columns);

        float height =
            grid.padding.top + grid.padding.bottom +
            rows * grid.cellSize.y +
            Mathf.Max(0, rows - 1) * grid.spacing.y;

        bodyLayout.preferredHeight = height;
    }
}

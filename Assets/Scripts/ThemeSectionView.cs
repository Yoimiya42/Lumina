using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ThemeSectionView : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private TMP_Text themeText;

    [Header("Body")]
    [SerializeField] private RectTransform bodyRoot; // ThemeBody
    [SerializeField] private ThemeBodyHeightFitter bodyFitter;

    [SerializeField] private bool startCollapsed = false;

    private bool _expanded;

    public RectTransform BodyRoot => bodyRoot;

    private void Awake()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);

        // 确保引用
        if (bodyFitter == null && bodyRoot != null)
            bodyFitter = bodyRoot.GetComponent<ThemeBodyHeightFitter>();

        SetExpanded(!startCollapsed, force: true);
    }

    public void SetTitle(string title)
    {
        if (themeText != null)
            themeText.text = title;
    }

    public void Toggle()
    {
        SetExpanded(!_expanded);
    }

    public void SetExpanded(bool expanded, bool force = false)
    {
        if (!force && _expanded == expanded) return;
        _expanded = expanded;

        if (bodyRoot != null)
            bodyRoot.gameObject.SetActive(_expanded);

        // 展开时重算一次高度（防止第一次展开不撑开）
        if (_expanded)
            bodyFitter?.Refit();

        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        if (transform.parent is RectTransform p)
            LayoutRebuilder.MarkLayoutForRebuild(p);
    }
}

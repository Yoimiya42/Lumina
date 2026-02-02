using UnityEngine;
using UnityEngine.UI;

public class ThemeSectionToggle : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Button headerButton;     // ThemeHeaderItem Button
    [SerializeField] private Image headerBackground;  // ThemeHeaderItem Image
    [SerializeField] private GameObject body;         // ThemeBody

    [Header("Colors")]
    [SerializeField] private Color collapsedColor = new Color(0.75f, 0.85f, 1f, 1f);
    [SerializeField] private Color expandedColor = new Color(0.45f, 0.70f, 1f, 1f);

    [SerializeField] private bool startExpanded = false;

    private bool isExpanded;

    private void Reset()
    {
        headerButton = GetComponent<Button>();
        headerBackground = GetComponent<Image>();
    }

    private void Awake()
    {
        if (headerButton == null) headerButton = GetComponent<Button>();
        if (headerBackground == null) headerBackground = GetComponent<Image>();

        if (headerButton != null)
            headerButton.onClick.AddListener(Toggle);

        SetExpanded(startExpanded);
    }

    public void Toggle()
    {
        SetExpanded(!isExpanded);
    }

    public void SetExpanded(bool expanded)
    {
        isExpanded = expanded;

        if (body != null)
            body.SetActive(isExpanded);

        if (headerBackground != null)
            headerBackground.color = isExpanded ? expandedColor : collapsedColor;
    }
}

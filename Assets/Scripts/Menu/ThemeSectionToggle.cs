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
    private ThemeSectionView _sectionView;
    private bool _delegateToSectionView;

    private void Reset()
    {
        headerButton = GetComponent<Button>();
        headerBackground = GetComponent<Image>();
    }

    private void Awake()
    {
        if (headerButton == null) headerButton = GetComponent<Button>();
        if (headerBackground == null) headerBackground = GetComponent<Image>();

        _sectionView = GetComponentInParent<ThemeSectionView>();
        _delegateToSectionView = _sectionView != null;

        if (!_delegateToSectionView && headerButton != null)
            headerButton.onClick.AddListener(Toggle);

        if (_delegateToSectionView)
            SyncFromCurrentState();
        else
            SetExpanded(startExpanded);
    }


    private void Start()
    {
        if (_delegateToSectionView && _sectionView != null)
        {
            _sectionView.SetExpanded(startExpanded, force: true);
            SyncFromCurrentState();
        }
    }
    private void LateUpdate()
    {
        if (_delegateToSectionView)
            SyncFromCurrentState();
    }

    private void OnDestroy()
    {
        if (!_delegateToSectionView && headerButton != null)
            headerButton.onClick.RemoveListener(Toggle);
    }

    public void Toggle()
    {
        if (_delegateToSectionView && _sectionView != null)
        {
            _sectionView.Toggle();
            SyncFromCurrentState();
            return;
        }

        SetExpanded(!isExpanded);
    }

    public void SetExpanded(bool expanded)
    {
        if (_delegateToSectionView && _sectionView != null)
        {
            _sectionView.SetExpanded(expanded);
            SyncFromCurrentState();
            return;
        }

        isExpanded = expanded;

        if (body != null)
            body.SetActive(isExpanded);

        ApplyHeaderVisual();
    }

    private void SyncFromCurrentState()
    {
        if (body != null)
            isExpanded = body.activeSelf;
        else if (_sectionView != null && _sectionView.BodyRoot != null)
            isExpanded = _sectionView.BodyRoot.gameObject.activeSelf;

        ApplyHeaderVisual();
    }

    private void ApplyHeaderVisual()
    {
        if (headerBackground != null)
            headerBackground.color = isExpanded ? expandedColor : collapsedColor;
    }
}


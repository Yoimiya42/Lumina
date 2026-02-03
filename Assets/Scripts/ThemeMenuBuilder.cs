using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds ThemeSections and Thumbnails using ImageFolderScanner output.
/// No absolute paths required; scanner resolves everything via PathSettings/ContentPaths.
/// </summary>
public class ThemeMenuBuilder : MonoBehaviour
{
    [Header("Scanner (your existing system)")]
    [SerializeField] private ImageFolderScanner scanner;

    [Header("ScrollView Content")]
    [SerializeField] private RectTransform contentRoot;      // ThemesScrollView/Viewport/Content

    [Header("Prefabs")]
    [SerializeField] private ThemeSectionView sectionPrefab; // PF_ThemeSection
    [SerializeField] private ThumbnailItemView thumbPrefab;  // PF_ThumbnailItem

    [Header("Control Bar")]
    [SerializeField] private TMP_Dropdown difficultyDropdown;   
    [SerializeField] private Button startButton;
    [SerializeField] private Button resetButton;

    [Header("Grid Config")]
    [SerializeField] private int gridColumns = 3;

    public string SelectedImagePath { get; private set; }
    public Difficulty SelectedDifficulty { get; private set; }

    private ThumbnailItemView _selectedItem;

    private void Awake()
    {
        if (difficultyDropdown != null)
            difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        if (scanner != null)
            scanner.OnScanCompleted.AddListener(OnScanCompleted);
    }

    private void Start()
    {
        if (scanner == null)
        {
            Debug.LogError("[ThemeMenuBuilder] Missing ImageFolderScanner reference.");
            return;
        }

        // This will ensure folders (scanner already does in Awake) and then scan.
        scanner.Scan();

        SelectedDifficulty = GetDropdownDifficulty();
        UpdateStartButton();
        UpdateResetButton();
    }

    private void OnScanCompleted(List<ImageFolderScanner.ImageItem> items)
    {
        BuildFromItems(items);
    }

    private void BuildFromItems(List<ImageFolderScanner.ImageItem> items)
    {
        if (contentRoot == null || sectionPrefab == null || thumbPrefab == null)
        {
            Debug.LogError("[ThemeMenuBuilder] Missing contentRoot/sectionPrefab/thumbPrefab.");
            return;
        }

        ClearContent();

        // Group by theme folder name (CAT/DOG/...)
        // Note: scanner.theme is "" if directly under ImagesRoot (no folder).
        var byTheme = new Dictionary<string, List<ImageFolderScanner.ImageItem>>();
        foreach (var it in items)
        {
            string theme = string.IsNullOrWhiteSpace(it.theme) ? "Uncategorized" : it.theme;

            if (!byTheme.TryGetValue(theme, out var list))
            {
                list = new List<ImageFolderScanner.ImageItem>();
                byTheme[theme] = list;
            }
            list.Add(it);
        }

        // Optional: sort themes alphabetically for stable UI
        var themes = new List<string>(byTheme.Keys);
        themes.Sort();

        foreach (var theme in themes)
        {
            var section = Instantiate(sectionPrefab, contentRoot);
            section.SetTitle(theme);

            // Ensure Grid fixed columns
            var grid = section.GridRoot.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Max(1, gridColumns);
            }

            // Build thumbs
            var list = byTheme[theme];
            // Optional: sort files for stable ordering
            list.Sort((a, b) => string.Compare(a.fileName, b.fileName, System.StringComparison.OrdinalIgnoreCase));

            foreach (var img in list)
            {
                var item = Instantiate(thumbPrefab, section.GridRoot);
                item.Bind(img.sprite, img.filePath, OnThumbnailClicked);
            }

            section.RefreshBodyHeight();
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private void OnThumbnailClicked(ThumbnailItemView clicked)
    {
        // Click same item again => deselect
        if (_selectedItem == clicked)
        {
            _selectedItem.SetSelected(false);
            _selectedItem = null;
            SelectedImagePath = null;

            // When nothing selected, dropdown should be interactable (free choice)
            if (difficultyDropdown != null)
                difficultyDropdown.interactable = true;

            UpdateStartButton();
            UpdateResetButton();
            return;
        }

        // Select a new item => cancel old highlight
        if (_selectedItem != null)
            _selectedItem.SetSelected(false);

        _selectedItem = clicked;
        _selectedItem.SetSelected(true);
        SelectedImagePath = clicked.ImagePath;

        ApplyDifficultyRuleForSelectedImage();
        UpdateStartButton();
        UpdateResetButton();
    }


    /// <summary>
    /// Your rule:
    /// - If progress > 0: dropdown shows locked difficulty and is disabled.
    /// - Else: dropdown is enabled and user can choose freely.
    /// </summary>
    private void ApplyDifficultyRuleForSelectedImage()
    {
        if (difficultyDropdown == null || string.IsNullOrEmpty(SelectedImagePath))
            return;

        if (ProgressStore.TryGet(SelectedImagePath, out var e) && e.progress01 > 0f)
        {
            int locked = Mathf.Clamp(e.lockedDifficulty, 0, 2);
            difficultyDropdown.value = locked;
            difficultyDropdown.RefreshShownValue();

            SelectedDifficulty = (Difficulty)locked;
            difficultyDropdown.interactable = false;
        }
        else
        {
            difficultyDropdown.interactable = true;
            SelectedDifficulty = GetDropdownDifficulty();
        }
    }

    private void OnDifficultyChanged(int index)
    {
        SelectedDifficulty = (Difficulty)Mathf.Clamp(index, 0, 2);
    }

    private void OnResetClicked()
    {
        if (string.IsNullOrEmpty(SelectedImagePath))
            return;

        ProgressStore.Reset(SelectedImagePath);

        if (_selectedItem != null)
            _selectedItem.RefreshProgressFromStore();

        if (difficultyDropdown != null)
        {
            difficultyDropdown.interactable = true;
            SelectedDifficulty = GetDropdownDifficulty();
        }

        UpdateResetButton();
    }

    private void UpdateStartButton()
    {
        if (startButton != null)
            startButton.interactable = !string.IsNullOrEmpty(SelectedImagePath);
    }

    private void UpdateResetButton()
    {
        if (resetButton == null) return;

        bool canReset = false;
        if (!string.IsNullOrEmpty(SelectedImagePath) &&
            ProgressStore.TryGet(SelectedImagePath, out var e) &&
            e.progress01 > 0f)
        {
            canReset = true;
        }

        resetButton.interactable = canReset;
    }

    private Difficulty GetDropdownDifficulty()
    {
        if (difficultyDropdown == null) return Difficulty.Easy;
        return (Difficulty)Mathf.Clamp(difficultyDropdown.value, 0, 2);
    }

    private void ClearContent()
    {
        _selectedItem = null;
        SelectedImagePath = null;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ThemeMenuBuilder : MonoBehaviour
{
    [Header("UI Roots")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private ThemeSectionView sectionPrefab;
    [SerializeField] private ThumbnailItemView thumbnailPrefab;

    [Header("Scanner")]
    [SerializeField] private ImageFolderScanner scanner;

    [Header("Control Bar")]
    [SerializeField] private TMP_Dropdown difficultyDropdown;
    [SerializeField] private Button startButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button showColorButton;

    [Header("Thumbnail Preview")]
    [SerializeField] private Material thumbnailGrayscaleMaterial;

    public string SelectedImagePath { get; private set; }
    public string SelectedImageId { get; private set; }
    public bool AreThumbnailsShownInColor => _thumbnailsShownInColor;

    public Difficulty SelectedDifficulty
    {
        get
        {
            if (difficultyDropdown == null) return Difficulty.Medium;
            int v = Mathf.Clamp(difficultyDropdown.value, 0, 2);
            return (Difficulty)v;
        }
    }

    private ThumbnailItemView _selectedItem;
    private bool _scannerListenerBound;
    private TMP_Text _showColorButtonLabel;
    private bool _thumbnailsShownInColor;

    private void Awake()
    {
        if (startButton != null) startButton.interactable = false;
        if (resetButton != null) resetButton.interactable = false;

        if (showColorButton != null)
        {
            _showColorButtonLabel = showColorButton.GetComponentInChildren<TMP_Text>(true);
            showColorButton.onClick.AddListener(ToggleThumbnailColorMode);
        }

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetSelected);

        UpdateShowColorButtonLabel();
    }

    private void Start()
    {
        if (scanner == null)
        {
            Debug.LogError("[ThemeMenuBuilder] Missing scanner reference.");
            return;
        }

        BindScannerListener();
        scanner.Scan();
    }

    private void OnDestroy()
    {
        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetSelected);

        if (showColorButton != null)
            showColorButton.onClick.RemoveListener(ToggleThumbnailColorMode);

        UnbindScannerListener();
    }

    private void BindScannerListener()
    {
        if (scanner == null || _scannerListenerBound)
            return;

        scanner.OnScanCompleted.AddListener(OnScanCompleted);
        _scannerListenerBound = true;
    }

    private void UnbindScannerListener()
    {
        if (scanner == null || !_scannerListenerBound)
            return;

        scanner.OnScanCompleted.RemoveListener(OnScanCompleted);
        _scannerListenerBound = false;
    }

    private void OnScanCompleted(List<ImageFolderScanner.ImageItem> items)
    {
        Build(items);
    }

    public void Build(IReadOnlyList<ImageFolderScanner.ImageItem> items)
    {
        if (contentRoot == null || sectionPrefab == null || thumbnailPrefab == null)
        {
            Debug.LogError("[ThemeMenuBuilder] Missing contentRoot/sectionPrefab/thumbnailPrefab reference.");
            return;
        }

        if (items == null)
            items = System.Array.Empty<ImageFolderScanner.ImageItem>();

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        _selectedItem = null;
        SelectedImagePath = null;
        SelectedImageId = null;
        UpdateUiLockState();

        var groups = items
            .GroupBy(x => string.IsNullOrWhiteSpace(x.theme) ? "Default" : x.theme)
            .OrderBy(g => g.Key);

        foreach (var g in groups)
        {
            var section = Instantiate(sectionPrefab, contentRoot);
            section.name = $"ThemeSection_{g.Key}";
            section.SetTitle(g.Key);

            var body = section.BodyRoot;

            foreach (var it in g)
            {
                var thumb = Instantiate(thumbnailPrefab, body);
                thumb.name = $"Thumb_{it.fileName}";
                thumb.Bind(it.sprite, it.filePath, it.imageId, OnThumbnailClicked);
                thumb.SetPreviewColorMode(_thumbnailsShownInColor, thumbnailGrayscaleMaterial);
            }

            body.GetComponent<ThemeBodyHeightFitter>()?.Refit();
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private void OnThumbnailClicked(ThumbnailItemView clicked)
    {
        if (_selectedItem == clicked)
        {
            _selectedItem.SetSelected(false);
            _selectedItem = null;
            SelectedImagePath = null;
            SelectedImageId = null;
            UpdateUiLockState();
            return;
        }

        _selectedItem?.SetSelected(false);

        _selectedItem = clicked;
        _selectedItem.SetSelected(true);
        SelectedImagePath = clicked.ImagePath;
        SelectedImageId = clicked.ImageId;

        UpdateUiLockState();
    }

    private void UpdateUiLockState()
    {
        bool hasSelection = !string.IsNullOrEmpty(SelectedImageId);

        if (startButton != null)
            startButton.interactable = hasSelection;

        if (difficultyDropdown != null)
            difficultyDropdown.interactable = hasSelection;

        bool canReset = false;

        if (hasSelection &&
            ImageProgressRepository.TryGet(SelectedImageId, out var entry) &&
            entry != null &&
            entry.progress01 > 0f)
        {
            if (difficultyDropdown != null)
            {
                difficultyDropdown.value = Mathf.Clamp(entry.lockedDifficulty, 0, difficultyDropdown.options.Count - 1);
                difficultyDropdown.interactable = false;
            }

            canReset = true;
        }

        if (resetButton != null)
            resetButton.interactable = canReset;
    }

    private void ResetSelected()
    {
        if (string.IsNullOrEmpty(SelectedImageId))
            return;

        ImageProgressRepository.Reset(SelectedImageId);

        if (difficultyDropdown != null)
            difficultyDropdown.interactable = !string.IsNullOrEmpty(SelectedImageId);

        _selectedItem?.RefreshProgressFromStore();
        UpdateUiLockState();
    }

    public void ToggleThumbnailColorMode()
    {
        _thumbnailsShownInColor = !_thumbnailsShownInColor;
        ApplyThumbnailColorMode();
        UpdateShowColorButtonLabel();
    }

    private void ApplyThumbnailColorMode()
    {
        if (contentRoot == null)
            return;

        var thumbnails = contentRoot.GetComponentsInChildren<ThumbnailItemView>(includeInactive: true);
        for (int i = 0; i < thumbnails.Length; i++)
        {
            if (thumbnails[i] != null)
                thumbnails[i].SetPreviewColorMode(_thumbnailsShownInColor, thumbnailGrayscaleMaterial);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void UpdateShowColorButtonLabel()
    {
        if (_showColorButtonLabel != null)
            _showColorButtonLabel.text = _thumbnailsShownInColor ? "Show Gray" : "Show Color";
    }
}

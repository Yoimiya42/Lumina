using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ThemeMenuBuilder : MonoBehaviour
{
    [Header("UI Roots")]
    [SerializeField] private RectTransform gridRoot;            // ThemeBody (or a GridRoot under it)
    [SerializeField] private ThumbnailItemView thumbnailPrefab; // PF_ThumbnailItem

    [Header("Control Bar")]
    [SerializeField] private Dropdown difficultyDropdown; // UnityEngine.UI.Dropdown (legacy)
    [SerializeField] private Button startButton;
    [SerializeField] private Button resetButton;

    [Header("Data Source")]
    [Tooltip("Folder that contains images or theme subfolders.")]
    [SerializeField] private string imagesRootAbsolutePath;

    public string SelectedImagePath { get; private set; }
    public Difficulty SelectedDifficulty { get; private set; }

    private readonly List<ThumbnailItemView> _items = new();
    private ThumbnailItemView _selectedItem;

    private void Awake()
    {
        if (difficultyDropdown != null)
            difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);
    }

    private void Start()
    {
        Build();
        SelectedDifficulty = GetDropdownDifficulty();
        UpdateStartButton();
        UpdateResetButton();
    }

    [ContextMenu("Build")]
    public void Build()
    {
        if (gridRoot == null || thumbnailPrefab == null)
        {
            Debug.LogError("[ThemeMenuBuilder] Missing gridRoot or thumbnailPrefab.");
            return;
        }

        ClearGrid();

        if (string.IsNullOrWhiteSpace(imagesRootAbsolutePath) || !Directory.Exists(imagesRootAbsolutePath))
        {
            Debug.LogError($"[ThemeMenuBuilder] Invalid imagesRootAbsolutePath: {imagesRootAbsolutePath}");
            return;
        }

        var imageFiles = CollectImages(imagesRootAbsolutePath);

        foreach (var path in imageFiles)
        {
            Sprite sprite = LoadSpriteFromFile(path);

            var item = Instantiate(thumbnailPrefab, gridRoot);
            item.Bind(sprite, path, OnThumbnailClicked);

            _items.Add(item);
        }

        // Optional: force layout refresh if needed
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridRoot);
    }

    private void OnThumbnailClicked(ThumbnailItemView clicked)
    {
        if (_selectedItem != null)
            _selectedItem.SetSelected(false);

        _selectedItem = clicked;
        _selectedItem.SetSelected(true);

        SelectedImagePath = clicked.ImagePath;

        ApplyDifficultyRuleForSelectedImage();

        UpdateStartButton();
        UpdateResetButton();

        Debug.Log($"[ThemeMenuBuilder] Selected={SelectedImagePath}, Difficulty={SelectedDifficulty}, DropdownEnabled={difficultyDropdown?.interactable}");
    }

    /// <summary>
    /// Implements your rule:
    /// - If progress > 0: dropdown shows locked difficulty and becomes disabled.
    /// - Else: dropdown enabled, user can pick freely.
    /// </summary>
    private void ApplyDifficultyRuleForSelectedImage()
    {
        if (difficultyDropdown == null || string.IsNullOrEmpty(SelectedImagePath))
            return;

        if (ProgressStore.TryGet(SelectedImagePath, out var e) && e.progress01 > 0f)
        {
            // Lock
            int locked = Mathf.Clamp(e.lockedDifficulty, 0, 2);
            difficultyDropdown.value = locked;
            difficultyDropdown.RefreshShownValue();

            SelectedDifficulty = (Difficulty)locked;
            difficultyDropdown.interactable = false;
        }
        else
        {
            // Unlocked
            difficultyDropdown.interactable = true;
            SelectedDifficulty = GetDropdownDifficulty();
        }
    }

    private void OnDifficultyChanged(int index)
    {
        // Only matters when unlocked (interactable=true); when locked, dropdown won't change anyway
        SelectedDifficulty = (Difficulty)Mathf.Clamp(index, 0, 2);
    }

    private void OnResetClicked()
    {
        if (string.IsNullOrEmpty(SelectedImagePath))
            return;

        ProgressStore.Reset(SelectedImagePath);

        // Refresh UI: progress badge and dropdown unlock
        if (_selectedItem != null)
            _selectedItem.RefreshProgressFromStore();

        if (difficultyDropdown != null)
        {
            difficultyDropdown.interactable = true;
            // After reset, difficulty is free again; keep current dropdown selection
            SelectedDifficulty = GetDropdownDifficulty();
        }

        UpdateResetButton();

        Debug.Log($"[ThemeMenuBuilder] Reset: {SelectedImagePath}");
    }

    private void UpdateStartButton()
    {
        if (startButton != null)
            startButton.interactable = !string.IsNullOrEmpty(SelectedImagePath);
    }

    private void UpdateResetButton()
    {
        if (resetButton == null) return;

        // Enable reset only if selected image has progress > 0
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

    private void ClearGrid()
    {
        _items.Clear();
        _selectedItem = null;
        SelectedImagePath = null;

        for (int i = gridRoot.childCount - 1; i >= 0; i--)
            Destroy(gridRoot.GetChild(i).gameObject);
    }

    private static List<string> CollectImages(string root)
    {
        var list = new List<string>();

        // If subfolders exist, treat them as themes; otherwise take direct files
        var subdirs = Directory.GetDirectories(root);
        if (subdirs.Length > 0)
        {
            foreach (var dir in subdirs)
            {
                foreach (var f in Directory.GetFiles(dir))
                    if (IsImageFile(f)) list.Add(f);
            }
        }
        else
        {
            foreach (var f in Directory.GetFiles(root))
                if (IsImageFile(f)) list.Add(f);
        }

        return list;
    }

    private static bool IsImageFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
    }

    private static Sprite LoadSpriteFromFile(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
                return null;

            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ThemeMenuBuilder] LoadSprite failed: {path}\n{e}");
            return null;
        }
    }
}

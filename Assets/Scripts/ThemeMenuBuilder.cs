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

    public string SelectedImagePath { get; private set; }

    private ThumbnailItemView _selectedItem;

    private void Awake()
    {
        if (startButton != null) startButton.interactable = false;
        if (resetButton != null) resetButton.interactable = false;

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetSelected);
    }

    private void Start()
    {
        if (scanner == null)
        {
            Debug.LogError("[ThemeMenuBuilder] Missing scanner reference.");
            return;
        }

        scanner.OnScanCompleted.AddListener(OnScanCompleted);
        scanner.Scan();
    }

    private void OnScanCompleted(List<ImageFolderScanner.ImageItem> items)
    {
        Build(items);
    }

    public void Build(IReadOnlyList<ImageFolderScanner.ImageItem> items)
    {
        if (contentRoot == null || sectionPrefab == null || thumbnailPrefab == null)
        {
            Debug.LogError("[ThemeMenuBuilder] Missing references in inspector.");
            return;
        }

        // 清空旧内容
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        _selectedItem = null;
        SelectedImagePath = null;
        UpdateUiLockState();

        // 主题 = Images 下一级文件夹名（你的 scanner 已经给了 item.theme）
        var groups = items
            .GroupBy(x => string.IsNullOrWhiteSpace(x.theme) ? "Default" : x.theme)
            .OrderBy(g => g.Key);

        foreach (var g in groups)
        {
            var section = Instantiate(sectionPrefab, contentRoot);
            section.name = $"ThemeSection_{g.Key}";
            section.SetTitle(g.Key);

            var body = section.BodyRoot;
            if (body == null)
            {
                Debug.LogError($"[ThemeMenuBuilder] BodyRoot null on section {g.Key}. Check prefab refs.");
                continue;
            }

            // 生成缩略图到 ThemeBody 下
            foreach (var it in g)
            {
                var thumb = Instantiate(thumbnailPrefab, body);
                thumb.name = $"Thumb_{it.fileName}";

                // 你 ThumbnailItemView 的 Bind 至少需要 sprite + path + onClick
                thumb.Bind(it.sprite, it.filePath, OnThumbnailClicked);

                // 如果你 ThumbnailItemView 里有刷新进度的方法，可以在 Bind 内做
                // thumb.RefreshProgressFromStore();
            }

            // 让 ThemeBody 根据缩略图数量撑高
            var fitter = body.GetComponent<ThemeBodyHeightFitter>();
            fitter?.Refit();
        }

        // 刷新整体布局
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private void OnThumbnailClicked(ThumbnailItemView clicked)
    {
        // 再点同一个：取消
        if (_selectedItem == clicked)
        {
            _selectedItem.SetSelected(false);
            _selectedItem = null;
            SelectedImagePath = null;

            UpdateUiLockState();
            return;
        }

        // 取消旧的
        if (_selectedItem != null)
            _selectedItem.SetSelected(false);

        _selectedItem = clicked;
        _selectedItem.SetSelected(true);
        SelectedImagePath = clicked.ImagePath;

        UpdateUiLockState();
    }

    private void UpdateUiLockState()
    {
        bool hasSelection = !string.IsNullOrEmpty(SelectedImagePath);

        if (startButton != null)
            startButton.interactable = hasSelection;

        // 默认：未选中则 dropdown 不可用（你也可以改成可用但没意义）
        if (difficultyDropdown != null)
            difficultyDropdown.interactable = hasSelection;

        bool canReset = false;

        if (hasSelection &&
            ProgressStore.TryGet(SelectedImagePath, out var entry) &&
            entry != null &&
            entry.progress01 > 0f)
        {
            // 有进度：锁定难度
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
        if (string.IsNullOrEmpty(SelectedImagePath))
            return;

        ProgressStore.Reset(SelectedImagePath);

        // Reset 后：难度重新可选（前提是仍然选着这张图）
        if (difficultyDropdown != null)
            difficultyDropdown.interactable = !string.IsNullOrEmpty(SelectedImagePath);

        // 刷新缩略图上的进度显示（如果你 ThumbnailItemView 有对应方法）
        // _selectedItem.RefreshProgressFromStore();

        UpdateUiLockState();
    }
}

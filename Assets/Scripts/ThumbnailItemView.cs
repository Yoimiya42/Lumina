using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ThumbnailItemView : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image thumbImage;        // 指向子物体 ThumbImage 的 Image
    [SerializeField] private TMP_Text progressText;   // 指向子物体 ProgressText 的 TMP_Text

    private Outline _outline;
    private Button _button;

    private System.Action<ThumbnailItemView> _onClick;

    public string ImagePath { get; private set; }

    private void Awake()
    {
        _button = GetComponent<Button>();
        _outline = GetComponent<Outline>(); // 组件在 prefab 根上

        if (_outline != null) _outline.enabled = false;

        // 避免子物体挡点击
        if (thumbImage != null) thumbImage.raycastTarget = false;
        if (progressText != null) progressText.raycastTarget = false;

        _button.onClick.AddListener(() => _onClick?.Invoke(this));
    }

    public void Bind(Sprite sprite, string imagePath, System.Action<ThumbnailItemView> onClick)
    {
        ImagePath = imagePath;
        _onClick = onClick;

        if (thumbImage != null)
        {
            thumbImage.sprite = sprite;
            thumbImage.preserveAspect = true;
            thumbImage.enabled = (sprite != null);
        }

        RefreshProgressFromStore();
    }

    public void SetSelected(bool selected)
    {
        if (_outline != null) _outline.enabled = selected;
    }

    public void RefreshProgressFromStore()
    {
        if (progressText == null) return;

        if (!string.IsNullOrEmpty(ImagePath) &&
            ProgressStore.TryGet(ImagePath, out var entry) &&
            entry != null &&
            entry.progress01 > 0f)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = $"{Mathf.RoundToInt(entry.progress01 * 100f)}%";
        }
        else
        {
            progressText.text = "";
            progressText.gameObject.SetActive(false);
        }
    }
}

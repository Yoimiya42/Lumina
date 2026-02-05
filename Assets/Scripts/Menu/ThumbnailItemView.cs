using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ThumbnailItemView : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image thumbImage;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image trophyIcon;

    private Outline _outline;
    private Button _button;
    private System.Action<ThumbnailItemView> _onClick;

    public string ImagePath { get; private set; } // load raw picture
    public string ImageId { get; private set; }   // key（sha1 bytes）

    private void Awake()
    {
        _button = GetComponent<Button>();
        _outline = GetComponent<Outline>();
        if (_outline != null) _outline.enabled = false;

        if (thumbImage != null) thumbImage.raycastTarget = false;
        if (progressText != null) progressText.raycastTarget = false;

        _button.onClick.AddListener(() => _onClick?.Invoke(this));
    }

    public void Bind(Sprite sprite, string imagePath, string imageId, System.Action<ThumbnailItemView> onClick)
    {
        ImagePath = imagePath;
        ImageId = imageId;
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

        if (!string.IsNullOrEmpty(ImageId) &&
            ImageProgressRepository.TryGet(ImageId, out var entry) &&
            entry != null &&
            entry.progress01 > 0f)
        {
            if (entry.progress01 >= 0.999f)
            {
                // Completed: display trophy
                progressText.gameObject.SetActive(false);
                trophyIcon.gameObject.SetActive(true);
            }
            else
            {
                // Uncompleted: display percentage
                trophyIcon.gameObject.SetActive(false);
                progressText.gameObject.SetActive(true);
                progressText.text = Mathf.RoundToInt(entry.progress01 * 100f) + "%";
            }
        }
        else
        {
            progressText.text = "";
            trophyIcon.gameObject.SetActive(false);
            progressText.gameObject.SetActive(false);
        }
    }
}

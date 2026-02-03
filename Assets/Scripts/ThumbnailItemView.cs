using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class ThumbnailItemView : MonoBehaviour
{
    [SerializeField] private Image thumbImage;      // child: ThumbImage
    [SerializeField] private TMP_Text progressText; // child: ProgressText

    [Header("Selection")]
    [SerializeField] private Outline outline;       // add Outline on this object and drag here
    [SerializeField] private Color unselectedBg = new Color(1f, 1f, 1f, 0.03f); // almost transparent
    [SerializeField] private Color selectedBg = new Color(1f, 1f, 1f, 0.08f); // slightly visible (optional)

    private Button _button;
    private Image _bg;
    private string _imagePath;
    private System.Action<ThumbnailItemView> _onClick;

    private bool _selected;

    public string ImagePath => _imagePath;
    public bool IsSelected => _selected;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _bg = GetComponent<Image>();

        if (thumbImage != null) thumbImage.raycastTarget = false;
        if (progressText != null) progressText.raycastTarget = false;

        _button.onClick.AddListener(() => _onClick?.Invoke(this));

        SetSelected(false);
        SetProgressVisible(false);
    }

    public void Bind(Sprite sprite, string imagePath, System.Action<ThumbnailItemView> onClick)
    {
        _imagePath = imagePath;
        _onClick = onClick;

        if (thumbImage != null)
        {
            thumbImage.sprite = sprite;
            thumbImage.preserveAspect = true;
            thumbImage.enabled = (sprite != null);
        }

        RefreshProgressFromStore();
    }

    public void RefreshProgressFromStore()
    {
        if (progressText == null) return;

        if (ProgressStore.TryGet(_imagePath, out var e) && e.progress01 > 0f)
        {
            int percent = Mathf.RoundToInt(e.progress01 * 100f);
            progressText.text = $"{percent}%";
            SetProgressVisible(true);
        }
        else
        {
            progressText.text = "";
            SetProgressVisible(false);
        }
    }

    private void SetProgressVisible(bool visible)
    {
        if (progressText != null)
            progressText.gameObject.SetActive(visible);
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;

        if (_bg != null)
            _bg.color = selected ? selectedBg : unselectedBg;

        if (outline != null)
            outline.enabled = selected;
    }
}

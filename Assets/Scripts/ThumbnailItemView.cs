using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class ThumbnailItemView : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Image thumbImage;      // child: ThumbImage
    [SerializeField] private TMP_Text progressText; // child: ProgressText

    [Header("Selection Colors (change Button targetGraphic Image.color)")]
    [SerializeField] private Color unselectedColor = new Color(1f, 1f, 1f, 0.20f);
    [SerializeField] private Color selectedColor = new Color(0.60f, 0.30f, 1f, 0.65f);

    private Button _button;
    private Image _bg;              // this GameObject's Image (Button targetGraphic)
    private string _imagePath;
    private System.Action<ThumbnailItemView> _onClick;

    public string ImagePath => _imagePath;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _bg = GetComponent<Image>();

        _button.transition = Selectable.Transition.ColorTint;
        _button.targetGraphic = _bg;

        _button.onClick.AddListener(() => _onClick?.Invoke(this));

        if (thumbImage != null) thumbImage.raycastTarget = false;
        if (progressText != null) progressText.raycastTarget = false;

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
        if (_bg != null)
            _bg.color = selected ? selectedColor : unselectedColor;
    }
}

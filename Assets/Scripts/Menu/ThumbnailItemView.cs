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
    private Sprite _colorSprite;
    private Sprite _grayscaleSprite;

    public string ImagePath { get; private set; } // load raw picture
    public string ImageId { get; private set; }   // key（sha1 bytes）

    private void Awake()
    {
        _button = GetComponent<Button>();
        _outline = GetComponent<Outline>();
        if (_outline != null) _outline.enabled = false;

        if (thumbImage != null) thumbImage.raycastTarget = false;
        if (progressText != null) progressText.raycastTarget = false;
        if (trophyIcon != null) trophyIcon.raycastTarget = false;

        _button.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClicked);

        ReleaseGeneratedPreview();
    }

    public void Bind(Sprite sprite, string imagePath, string imageId, System.Action<ThumbnailItemView> onClick)
    {
        ImagePath = imagePath;
        ImageId = imageId;
        _onClick = onClick;
        _colorSprite = sprite;

        ReleaseGeneratedPreview();
        _grayscaleSprite = CreateGrayscaleSprite(sprite);

        if (thumbImage != null)
        {
            thumbImage.sprite = sprite;
            thumbImage.preserveAspect = true;
            thumbImage.enabled = (sprite != null);
        }

        RefreshProgressFromStore();
    }

    public void SetPreviewColorMode(bool showColor, Material grayscaleMaterial)
    {
        if (thumbImage == null)
            return;

        thumbImage.material = null;
        thumbImage.sprite = showColor
            ? _colorSprite
            : (_grayscaleSprite != null ? _grayscaleSprite : _colorSprite);
        thumbImage.preserveAspect = true;
        thumbImage.enabled = (thumbImage.sprite != null);

        thumbImage.SetMaterialDirty();
        thumbImage.SetVerticesDirty();
    }

    public void SetSelected(bool selected)
    {
        if (_outline != null) _outline.enabled = selected;
    }

    public void RefreshProgressFromStore()
    {
        if (!string.IsNullOrEmpty(ImageId) &&
            ImageProgressRepository.TryGet(ImageId, out var entry) &&
            entry != null &&
            entry.progress01 > 0f)
        {
            if (entry.progress01 >= 0.999f)
            {
                // Completed: display trophy
                SetProgressVisible(false);
                SetTrophyVisible(true);
            }
            else
            {
                // Uncompleted: display percentage
                SetTrophyVisible(false);
                SetProgressVisible(true);
                if (progressText != null)
                    progressText.text = Mathf.RoundToInt(entry.progress01 * 100f) + "%";
            }
        }
        else
        {
            if (progressText != null)
                progressText.text = "";

            SetTrophyVisible(false);
            SetProgressVisible(false);
        }
    }

    private void HandleClicked()
    {
        _onClick?.Invoke(this);
    }

    private void SetProgressVisible(bool visible)
    {
        if (progressText != null)
            progressText.gameObject.SetActive(visible);
    }

    private void SetTrophyVisible(bool visible)
    {
        if (trophyIcon != null)
            trophyIcon.gameObject.SetActive(visible);
    }

    private void ReleaseGeneratedPreview()
    {
        if (_grayscaleSprite == null)
            return;

        var tex = _grayscaleSprite.texture;
        DestroySafely(_grayscaleSprite);

        if (tex != null)
            DestroySafely(tex);

        _grayscaleSprite = null;
    }

    private static Sprite CreateGrayscaleSprite(Sprite source)
    {
        if (source == null || source.texture == null)
            return null;

        var rect = source.rect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        if (width <= 0 || height <= 0)
            return null;

        var pixels = source.texture.GetPixels(
            Mathf.RoundToInt(rect.x),
            Mathf.RoundToInt(rect.y),
            width,
            height);

        for (int i = 0; i < pixels.Length; i++)
        {
            var color = pixels[i];
            float gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            pixels[i] = new Color(gray, gray, gray, color.a);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = source.texture.name + "_GrayscalePreview"
        };
        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        var pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, source.pixelsPerUnit);
    }

    private static void DestroySafely(Object obj)
    {
        if (obj == null)
            return;

        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}

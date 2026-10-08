using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnTheLookout.UI;

/// <summary>
/// A small timer bar that sits just above PEAK's stamina bar and looks like it: it is built from
/// clones of the stamina bar's own frame (StaminaBar.staminaBarOutline) and brushed fill
/// (StaminaBar.staminaBar), tinted light blue, with a PEAK-font label inside.
/// It is placed from the real frame's on-screen corners every frame, so it never overlaps the
/// main bar and stays left-aligned with it.
/// </summary>
internal sealed class StaminaTimerBar
{
    // Proportions relative to the main bar.
    private const float WidthFraction = 0.55f; // of the main bar's full width
    private const float Scale = 0.7f; // overall size vs. the main bar
    private const float GapPixels = 6f; // space between the two bars

    private readonly StaminaBar _source;
    private readonly RectTransform _root;
    private readonly CanvasGroup _group;
    private readonly RectTransform _fill;
    private readonly Image _fillImage;
    private readonly Image _back;
    private readonly TextMeshProUGUI _label;
    private readonly float _innerWidth;
    private readonly float _padX;

    public bool IsValid => _root != null && _source != null && _source.staminaBarOutline != null;

    public StaminaTimerBar(StaminaBar source, TextMeshProUGUI fontStyle)
    {
        _source = source;
        RectTransform frameSource = source.staminaBarOutline;
        RectTransform fillSource = source.staminaBar;

        float frameHeight = frameSource.rect.height;
        float fillHeight = fillSource.rect.height > 0f ? fillSource.rect.height : frameHeight * 0.6f;
        _padX = 7f; // the main bar's frame is 14 px wider than its fill (StaminaBar.Update)
        float padY = Mathf.Max(0f, (frameHeight - fillHeight) * 0.5f);
        _innerWidth = Mathf.Max(60f, source.fullBar.sizeDelta.x * WidthFraction);

        var go = new GameObject("OTL_TimerBar", typeof(RectTransform));
        _root = (RectTransform)go.transform;
        _root.SetParent(frameSource.parent, false);
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = Vector2.zero;
        _root.sizeDelta = new Vector2(_innerWidth + _padX * 2f, frameHeight);
        _root.localScale = Vector3.one * Scale;
        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // Dark track behind the fill (same brushed sprite as the fill).
        _back = CloneImage(fillSource, _root, "OTL_TimerTrack");
        Inset((RectTransform)_back.transform, _padX, padY);
        _back.color = new Color(0f, 0f, 0f, 0.35f);

        // Brushed fill, tinted per state.
        _fillImage = CloneImage(fillSource, _root, "OTL_TimerFill");
        _fill = (RectTransform)_fillImage.transform;
        _fill.anchorMin = new Vector2(0f, 0f);
        _fill.anchorMax = new Vector2(0f, 1f);
        _fill.pivot = new Vector2(0f, 0.5f);
        _fill.anchoredPosition = new Vector2(_padX, 0f);
        _fill.sizeDelta = new Vector2(_innerWidth, -padY * 2f);

        // The cream frame on top, exactly like the main bar's.
        Image frame = CloneImage(frameSource, _root, "OTL_TimerFrame");
        RectTransform frameRect = (RectTransform)frame.transform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;

        // Label inside the bar: PEAK font, white with a dark outline so it reads on any fill.
        var labelGo = new GameObject("OTL_TimerLabel", typeof(RectTransform));
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(_root, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(_padX, 0f);
        labelRect.offsetMax = new Vector2(-_padX, 0f);
        _label = labelGo.AddComponent<TextMeshProUGUI>();
        _label.font = fontStyle.font;
        _label.fontSharedMaterial = fontStyle.fontSharedMaterial;
        _label.fontStyle = fontStyle.fontStyle;
        _label.alignment = TextAlignmentOptions.Center;
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 8f;
        _label.fontSizeMax = Mathf.Max(10f, frameHeight * 0.8f);
        _label.raycastTarget = false;
        _label.color = Color.white;
        _label.outlineWidth = 0.25f;
        _label.outlineColor = new Color32(20, 30, 45, 255);
    }

    /// <summary>Clone only the Image of a stamina-bar part (no children, no game scripts).</summary>
    private static Image CloneImage(RectTransform source, Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        if (source.GetComponent<Image>() is { } src)
        {
            image.sprite = src.sprite;
            image.type = src.type;
            image.material = src.material;
            image.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
            image.preserveAspect = src.preserveAspect;
            image.color = src.color;
        }

        image.raycastTarget = false;
        return image;
    }

    private static void Inset(RectTransform rt, float x, float y)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(x, y);
        rt.offsetMax = new Vector2(-x, -y);
    }

    /// <param name="text">Label; empty hides the bar.</param>
    /// <param name="fill">0-1, or negative for no fill (label only).</param>
    public void Show(string text, float fill, Color color)
    {
        _group.alpha = Mathf.MoveTowards(_group.alpha, text.Length > 0 ? 1f : 0f, Time.deltaTime * 6f);
        Follow();
        if (text.Length == 0) return;

        _label.text = text;
        bool hasFill = fill >= 0f;
        _fillImage.enabled = hasFill;
        _fill.sizeDelta = new Vector2(_innerWidth * Mathf.Clamp01(fill), _fill.sizeDelta.y);
        _fillImage.color = color;
        _label.color = hasFill ? Color.white : color;
    }

    /// <summary>Sit just above the main bar's frame, left edges aligned.</summary>
    private void Follow()
    {
        var corners = new Vector3[4];
        _source.staminaBarOutline.GetWorldCorners(corners); // 0 bottom-left, 1 top-left
        Vector3 up = _source.staminaBarOutline.up * (GapPixels * _source.staminaBarOutline.lossyScale.y);
        _root.position = corners[1] + up;
    }

    public void Destroy()
    {
        if (_root != null) Object.Destroy(_root.gameObject);
    }
}

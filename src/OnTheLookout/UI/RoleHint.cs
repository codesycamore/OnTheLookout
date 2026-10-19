using OnTheLookout.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OnTheLookout.UI;

/// <summary>
/// Bottom left, stacked just above PEAK's stamina bar (above the host's "+" tip when that shows):
/// - a small tip, always shown to every living player during a round: "PRESS HOTKEY (P) FOR YOUR ROLE HINT";
/// - above it, the role hint: the local player's role abilities in a few lines on a dark translucent plate. It shows
///   for RoleHintSeconds after the roles are drawn at the start of every leg, then fades away; KeyRoleHint (P) shows
///   or hides it at any time during a round.
/// Both use the host tip's look and are placed from the stamina bar's screen position each frame (as the host tip is).
/// </summary>
internal sealed class RoleHint : MonoBehaviour
{
    private const float FadeSeconds = 0.8f;
    private const float FontSize = 20f;
    private const float Gap = 6f;

    /// <summary>Weight of the bold labels (TMP SDF _WeightBold; TMP's default bold is about 0.75): bold, but thinner.</summary>
    private const float LabelBoldWeight = 0.3f;

    private static readonly Color Yellow = new(1f, 0.84f, 0.2f, 1f);

    private Canvas? _canvas;
    private RectTransform? _plate, _tipPlate;
    private TextMeshProUGUI? _text, _tip;
    private CanvasGroup? _plateGroup;
    private readonly Vector3[] _corners = new Vector3[4];

    /// <summary>Shown until this time (then fades); float.MaxValue while toggled on with the key; -2 = show at the next frame.</summary>
    private float _showUntil = -1f;
    private Role _builtFor;

    private void Awake()
    {
        RoundManager.LegStarted += _ => _showUntil = -2f; // shown once the role is known (next Update)
    }

    private void Update()
    {
        Character local = Character.localCharacter;
        GUIManager gui = GUIManager.instance;
        RectTransform? bar = gui != null && gui.bar != null ? gui.bar.fullBar : null;
        bool inRound = RoundManager.IsActive && local != null && !local.data.dead && bar != null && bar.gameObject.activeInHierarchy;
        if (!inRound)
        {
            if (_canvas != null && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
            if (!RoundManager.IsActive) _showUntil = -1f;
            return;
        }

        if (_canvas == null) Build(gui!);
        _canvas!.gameObject.SetActive(true);

        Keyboard? kb = Keyboard.current;
        if (kb != null && kb[Plugin.ModConfig.KeyRoleHint.Value].wasPressedThisFrame && !HostMenu.IsOpen && !ChaserOddsMenu.IsOpen)
        {
            _showUntil = PlateAlpha > 0f ? -1f : float.MaxValue;
        }

        if (_showUntil == -2f) _showUntil = Time.time + Mathf.Max(0f, Plugin.ModConfig.RoleHintSeconds.Synced());

        // The tip (always) and the hint (while shown).
        string tip = $"PRESS HOTKEY ({KeyName()}) FOR YOUR ROLE HINT";
        if (_tip!.text != tip)
        {
            _tip.text = tip;
            _tipPlate!.sizeDelta = new Vector2(_tip.preferredWidth + 28f, _tip.preferredHeight + 10f);
        }

        Role role = RoleManager.IsChaser(local) ? Role.Chaser : Role.Runner;
        if (_text!.text.Length == 0 || role != _builtFor)
        {
            _builtFor = role;
            _text.text = TextFor(role);
            _text.fontSize = FontSize; // the same size for both roles
            _text.ForceMeshUpdate();
            _plate!.sizeDelta = new Vector2(_text.preferredWidth + 32f, _text.preferredHeight + 20f);
        }

        float alpha = PlateAlpha;
        _plateGroup!.alpha = alpha;
        _plate!.gameObject.SetActive(alpha > 0f);
        Place(bar!);
    }

    private float PlateAlpha =>
        _showUntil == float.MaxValue ? 1f : _showUntil < 0f ? 0f : Mathf.Clamp01((_showUntil + FadeSeconds - Time.time) / FadeSeconds);

    /// <summary>The abilities of each role, with the current values; one line each, about the same length.</summary>
    private static string TextFor(Role role)
    {
        var cfg = Plugin.ModConfig;
        string yellow = ColorUtility.ToHtmlStringRGB(Yellow);
        string title(string t) => $"<b><color=#{yellow}>{t}</color></b>";
        if (role == Role.Chaser)
        {
            return title("CHASER ROLE HINT") + "  catch the runners before they reach the peak\n"
                + "<b>Capture:</b> hold interact on a runner up close\n"
                + $"<b>Blowgun:</b> a dart makes a runner drowsy ({cfg.BlowgunCooldownSeconds.Synced():0}s cooldown)\n"
                + $"<b>Gem:</b> speed, unlimited stamina, see runners through walls ({cfg.GemCooldownSeconds.Synced():0}s cooldown)\n"
                + "<b>Items:</b> only food and healing; only clown luggage opens for you\n"
                + "<b>Watch out:</b> a runner looking at you freezes you; no captures in safe zones";
        }

        return title("RUNNER ROLE HINT") + "  reach the next campfire, then the peak\n"
            + $"<b>Freeze:</b> look at a chaser within {cfg.FreezeRange.Synced():0} m to freeze them for {cfg.FreezeDuration.Synced():0}s\n"
            + $"<b>Dash gem:</b> dash {cfg.DashDistance.Synced():0} m forward and heal a little ({cfg.DashCooldownSeconds.Synced():0}s cooldown)\n"
            + "<b>Snowballs and stones:</b> blind or hurt a chaser\n"
            + "<b>Safe zones:</b> campfires and the peak; green glow = unopened luggage";
    }

    private static string KeyName()
    {
        Keyboard? kb = Keyboard.current;
        Key k = Plugin.ModConfig.KeyRoleHint.Value;
        string name = kb != null ? kb[k].displayName : "";
        return string.IsNullOrWhiteSpace(name) ? k.ToString().ToUpperInvariant() : name.ToUpperInvariant();
    }

    private void Build(GUIManager gui)
    {
        _canvas = PeakMenuKit.CreateCanvas("OTL_RoleHint", transform, gui, 19);
        _canvas.GetComponent<GraphicRaycaster>().enabled = false; // never blocks clicks

        // The tip: the host tip's look (HostMenu.BuildHint).
        _tipPlate = NewPlate("TipPlate", 0.45f);
        _tip = PeakMenuKit.AddText(_tipPlate, "", gui.interactNameText, 18f, Yellow, 30f);
        Stretch(_tip.rectTransform, Vector2.zero);

        // The role hint.
        _plate = NewPlate("HintPlate", 0.62f);
        _plateGroup = _plate.gameObject.AddComponent<CanvasGroup>();
        _plateGroup.interactable = false;
        _plateGroup.blocksRaycasts = false;
        _text = PeakMenuKit.AddText(_plate, "", gui.interactNameText, FontSize, Color.white, 30f);
        _text.alignment = TextAlignmentOptions.TopLeft;
        _text.enableAutoSizing = false;
        _text.richText = true;
        Stretch(_text.rectTransform, new Vector2(16f, 10f));

        // Thinner bold: TMP draws <b> on an SDF font by thickening it with the material's _WeightBold. A material
        // instance for this text only, so nothing else in PEAK's UI changes.
        Material thin = _text.fontMaterial;
        if (thin.HasProperty("_WeightBold")) thin.SetFloat("_WeightBold", LabelBoldWeight);
    }

    private RectTransform NewPlate(string name, float opacity)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_canvas!.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = Vector2.zero; // bottom-left corner, placed above the stamina bar
        Image plate = go.AddComponent<Image>();
        plate.color = new Color(0.04f, 0.05f, 0.08f, opacity);
        plate.raycastTarget = false;
        return rt;
    }

    private static void Stretch(RectTransform rt, Vector2 padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = padding;
        rt.offsetMax = -padding;
    }

    /// <summary>Stamina bar top-left, then the host tip (if it shows), then the tip, then the hint, upwards.</summary>
    private void Place(RectTransform bar)
    {
        bar.GetWorldCorners(_corners); // 1 = top-left
        Canvas? barCanvas = bar.GetComponentInParent<Canvas>();
        Camera? cam = barCanvas != null && barCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? barCanvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, _corners[1]);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas!.transform, screen, null, out Vector2 local)) return;

        Vector2 at = local + new Vector2(0f, 12f); // where HostMenu puts the host tip
        if (HostMenu.HintHeight > 0f) at.y += HostMenu.HintHeight + Gap;
        _tipPlate!.anchoredPosition = at;
        at.y += _tipPlate.sizeDelta.y + Gap;
        _plate!.anchoredPosition = at;
    }
}

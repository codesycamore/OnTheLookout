using System.Linq;
using System.Text;
using DG.Tweening;
using OnTheLookout.Core;
using OnTheLookout.Freeze;
using OnTheLookout.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnTheLookout.UI;

/// <summary>
/// HUD, styled with PEAK's own fonts and stamina-bar sprite so it blends in:
/// - a light-blue timer bar just above the stamina bar (frozen / immune), plus "SAFE ZONE" for runners;
/// - centre screen (PEAK title font): role reveal (red chaser / yellow runner), head-start countdown
///   (translucent for runners), leg / round announcements;
/// - full-screen blindness for chasers during the reveal + head start, fading out near the end;
/// - chaser list under the ascent label (top right) and short toasts for captures / conversions / rewards.
/// Pieces attached to PEAK's HUD are rebuilt automatically when it is recreated (scene changes).
/// </summary>
internal sealed class Hud : MonoBehaviour
{
    private static readonly Color ChaserRed = new(0.93f, 0.22f, 0.2f);
    private static readonly Color RunnerYellow = new(1f, 0.84f, 0.2f);
    private static readonly Color Warm = new(1f, 0.78f, 0.36f);
    private static readonly Color ZombieGreen = new(0.62f, 0.9f, 0.38f);
    private const string IceHex = "#9FDCFF";

    // Our own overlay canvas (blindness + centre text), above PEAK's HUD.
    private Canvas? _overlay;
    private CanvasScaler? _overlayScaler;
    private Image? _blind;
    private TextMeshProUGUI? _centerMain;
    private TextMeshProUGUI? _centerSub;
    private string _lastCenter = "";
    private bool _overlayFontSet;

    // Attached to PEAK's HUD canvas.
    private Canvas? _hudCanvas;
    private BlowgunCooldownUI? _blowgunCooldown;
    private BlowgunCooldownUI? _gemCooldown;
    private BlowgunCooldownUI? _dashCooldown;
    private TextMeshProUGUI? _chaserList;
    private TextMeshProUGUI? _toast;
    private CanvasGroup? _toastGroup;
    private float _nextListRefresh;

    // Timed centre announcement.
    private string _announce = "", _announceSub = "";
    private Color _announceColor = Color.white;
    private float _announceUntil;

    private void Awake() => BuildOverlay();

    private void OnEnable()
    {
        RoundManager.LegStarted += OnLegStarted;
        RoundManager.LegCompleted += OnLegCompleted;
        RoundManager.RoundEnded += OnRoundEnded;
        ModNetwork.NoticeReceived += OnNotice;
        ItemRules.LocalDenied += Toast;
    }

    private void OnDisable()
    {
        RoundManager.LegStarted -= OnLegStarted;
        RoundManager.LegCompleted -= OnLegCompleted;
        RoundManager.RoundEnded -= OnRoundEnded;
        ModNetwork.NoticeReceived -= OnNotice;
        ItemRules.LocalDenied -= Toast;
    }

    // ---------- Build: overlay ----------

    private void BuildOverlay()
    {
        var go = new GameObject("OTL_Overlay", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        _overlay = go.AddComponent<Canvas>();
        _overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlay.sortingOrder = 100;
        _overlayScaler = go.AddComponent<CanvasScaler>();
        _overlayScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _overlayScaler.referenceResolution = new Vector2(1920f, 1080f);
        _overlayScaler.matchWidthOrHeight = 0.5f;

        RectTransform blind = NewRect("OTL_Blind", go.transform);
        Stretch(blind);
        _blind = blind.gameObject.AddComponent<Image>();
        _blind.color = new Color(0f, 0f, 0f, 0f);
        _blind.raycastTarget = false;

        _centerMain = NewText("OTL_CenterMain", go.transform, null, 150f, TextAlignmentOptions.Center);
        _centerMain.rectTransform.sizeDelta = new Vector2(1800f, 220f);
        _centerMain.rectTransform.anchoredPosition = new Vector2(0f, 40f);

        _centerSub = NewText("OTL_CenterSub", go.transform, null, 46f, TextAlignmentOptions.Center);
        _centerSub.rectTransform.sizeDelta = new Vector2(1800f, 80f);
        _centerSub.rectTransform.anchoredPosition = new Vector2(0f, 170f);
    }

    /// <summary>Match PEAK's fonts and HUD scaling once the game UI exists.</summary>
    private void StyleOverlay(GUIManager gui)
    {
        if (_overlayFontSet || _centerMain == null || _centerSub == null) return;
        TextMeshProUGUI title = gui.heroText != null ? gui.heroText : gui.interactNameText;
        ApplyFont(_centerMain, title);
        ApplyFont(_centerSub, gui.interactNameText);
        _centerMain.color = _centerSub.color = Color.white;

        if (gui.hudCanvas != null && gui.hudCanvas.GetComponent<CanvasScaler>() is { } scaler && _overlayScaler != null)
        {
            _overlayScaler.uiScaleMode = scaler.uiScaleMode;
            _overlayScaler.referenceResolution = scaler.referenceResolution;
            _overlayScaler.matchWidthOrHeight = scaler.matchWidthOrHeight;
            _overlayScaler.screenMatchMode = scaler.screenMatchMode;
        }

        if (gui.hudCanvas != null && _overlay != null) _overlay.sortingOrder = gui.hudCanvas.sortingOrder + 10;
        _overlayFontSet = true;
    }

    // ---------- Build: PEAK HUD attachments ----------

    private bool EnsureHud()
    {
        GUIManager gui = GUIManager.instance;
        if (gui == null || gui.hudCanvas == null) return false;
        StyleOverlay(gui);

        if (_hudCanvas == gui.hudCanvas && _toast != null)
        {
            if (_chaserList == null) BuildChaserList(_hudCanvas.transform, gui.interactNameText);
            return true;
        }

        _hudCanvas = gui.hudCanvas;
        BuildChaserList(_hudCanvas.transform, gui.interactNameText);
        BuildToast(_hudCanvas.transform, gui.interactNameText);
        _blowgunCooldown?.Destroy();
        _gemCooldown?.Destroy();
        _dashCooldown?.Destroy();
        if (_overlay != null)
        {
            TextMeshProUGUI font = gui.heroText != null ? gui.heroText : gui.interactNameText;
            _blowgunCooldown = new BlowgunCooldownUI(_overlay, font);
            _gemCooldown = new BlowgunCooldownUI(_overlay, font, "Gem", () => ChaserKit.GemOnCooldown,
                () => ChaserKit.GemCooldownLeft, item => ChaserKit.IsGem(item), RoleManager.IsChaser);
            _dashCooldown = new BlowgunCooldownUI(_overlay, font, "Dash", () => RunnerDash.OnCooldown,
                () => RunnerDash.CooldownLeft, item => RunnerDash.IsRunnerGem(item), RoleManager.IsRunner);
        }
        Plugin.Log.LogInfo($"[OTL][UI] HUD built on '{_hudCanvas.name}' (stamina bar found: {gui.bar != null}).");
        return true;
    }

    private void BuildChaserList(Transform canvas, TextMeshProUGUI fallbackStyle)
    {
        // Directly under PEAK's ascent label (top right), same font and alignment.
        AscentUI? ascent = Object.FindFirstObjectByType<AscentUI>(FindObjectsInactive.Include);
        TextMeshProUGUI? ascentText = ascent != null ? ascent.text : null;

        if (ascentText != null && ascentText.gameObject.activeInHierarchy)
        {
            RectTransform a = ascentText.rectTransform;
            _chaserList = NewText("OTL_ChaserList", a.parent, ascentText, ascentText.fontSize * 0.7f, ascentText.alignment);
            RectTransform rt = _chaserList.rectTransform;
            rt.anchorMin = a.anchorMin;
            rt.anchorMax = a.anchorMax;
            rt.pivot = new Vector2(a.pivot.x, 1f);
            rt.sizeDelta = new Vector2(Mathf.Max(a.rect.width, 420f), 400f);
            rt.anchoredPosition = a.anchoredPosition - new Vector2(0f, a.rect.height * (1f - a.pivot.y) + 6f);
        }
        else
        {
            _chaserList = NewText("OTL_ChaserList", canvas, fallbackStyle, fallbackStyle.fontSize * 0.8f, TextAlignmentOptions.TopRight);
            RectTransform rt = _chaserList.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(420f, 400f);
            rt.anchoredPosition = new Vector2(-40f, -90f);
        }

        _chaserList.verticalAlignment = VerticalAlignmentOptions.Top;
        _chaserList.text = "";
    }

    private void BuildToast(Transform canvas, TextMeshProUGUI style)
    {
        _toast = NewText("OTL_Toast", canvas, style, style.fontSize, TextAlignmentOptions.Center);
        RectTransform rt = _toast.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(1400f, 80f);
        rt.anchoredPosition = new Vector2(0f, -230f);
        _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>();
        _toastGroup.alpha = 0f;
    }

    // ---------- Helpers ----------

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TextMeshProUGUI? style, float size, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(800f, 80f);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (style != null)
        {
            ApplyFont(t, style);
            t.color = style.color;
        }

        t.fontSize = size;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    private static void ApplyFont(TextMeshProUGUI target, TextMeshProUGUI style)
    {
        target.font = style.font;
        target.fontSharedMaterial = style.fontSharedMaterial;
        target.fontStyle = style.fontStyle;
    }

    // ---------- Update ----------

    private void Update()
    {
        UpdateCenter();
        ChaserKit.LocalTick();
        if (!EnsureHud()) return;
        if (_blowgunCooldown is { IsValid: true }) _blowgunCooldown.Update(GUIManager.instance);
        if (_gemCooldown is { IsValid: true }) _gemCooldown.Update(GUIManager.instance);
        if (_dashCooldown is { IsValid: true }) _dashCooldown.Update(GUIManager.instance);
        if (Time.time >= _nextListRefresh)
        {
            _nextListRefresh = Time.time + 0.25f;
            UpdateChaserList();
        }
    }

    private void UpdateCenter()
    {
        if (_centerMain == null || _centerSub == null || _blind == null) return;

        Character local = Character.localCharacter;
        bool chaser = local != null && RoundManager.IsActive && RoleManager.RoleOf(Net.Actor(local)) == Role.Chaser;
        string main = "", sub = "";
        Color color = Color.white;
        Color subColor = Color.white;
        float alpha = 1f;
        bool rainbow = false;

        if (RoundManager.IsGameOver && RoundManager.AirportSecondsLeft > 0f)
        {
            // The game was won: a rainbow GAME OVER!, then the countdown back to the airport (head-start countdown style).
            float left = RoundManager.AirportSecondsLeft;
            float countdown = Mathf.Max(1f, Plugin.ModConfig.GameOverAirportSeconds.Synced());
            if (left > countdown)
            {
                bool runnersWon = RoundManager.State == RoundState.RunnersWon;
                main = "GAME OVER!";
                sub = runnersWon ? "THE RUNNERS WIN" : "THE CHASERS WIN";
                subColor = runnersWon ? RunnerYellow : ChaserRed;
                rainbow = true;
            }
            else
            {
                main = Mathf.CeilToInt(left).ToString();
                sub = "RETURNING BACK TO AIRPORT IN...";
            }
        }
        else if (Time.time < _announceUntil)
        {
            main = _announce;
            sub = _announceSub;
            color = _announceColor;
        }
        else if (local != null && RoundManager.InReveal)
        {
            main = chaser ? "CHASER" : "RUNNER";
            sub = "YOU ARE A...";
            color = chaser ? ChaserRed : RunnerYellow;
        }
        else if (local != null && RoundManager.InCountdown)
        {
            main = Mathf.CeilToInt(RoundManager.HoldSecondsLeft).ToString();
            sub = chaser ? "THE HUNT BEGINS IN" : "YOU HAVE A HEADSTART... RUN!";
            alpha = chaser ? 1f : Mathf.Clamp01(Plugin.ModConfig.CountdownOpacity.Value);
            if (!chaser) color = subColor = RunnerYellow; // the runners' head start message in PEAK yellow
        }

        if (main != _lastCenter)
        {
            _lastCenter = main;
            _centerMain.rectTransform.DOKill();
            _centerMain.rectTransform.localScale = Vector3.one * 1.25f;
            _centerMain.rectTransform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
        }

        _centerMain.text = main;
        _centerSub.text = sub;
        _centerMain.color = new Color(color.r, color.g, color.b, alpha);
        _centerSub.color = new Color(subColor.r, subColor.g, subColor.b, alpha * 0.85f);
        if (rainbow) Rainbow(_centerMain);
        else if (_rainbowShown)
        {
            _rainbowShown = false;
            _centerMain.ForceMeshUpdate(); // back to the plain colour
        }

        // Chasers are blind during the reveal and head start; the blackness fades over the last 40%.
        float blind = 0f;
        if (chaser && RoundManager.InHold)
        {
            float total = Mathf.Max(0.1f, RoundManager.HoldTotalSeconds);
            blind = Mathf.Clamp01(RoundManager.HoldSecondsLeft / (total * 0.4f));
        }

        _blind.color = new Color(0f, 0f, 0f, blind);
    }

    private bool _rainbowShown;

    /// <summary>Each letter its own colour, the rainbow sliding along the text (like the sugar-rush stamina bar).</summary>
    private void Rainbow(TextMeshProUGUI text)
    {
        _rainbowShown = true;
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            Color32 c = Color.HSVToRGB(Mathf.Repeat(i * 0.09f - Time.time * 0.6f, 1f), 0.75f, 1f);
            Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
            for (int v = 0; v < 4; v++) colors[ch.vertexIndex + v] = c;
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private void UpdateChaserList()
    {
        if (_chaserList == null) return;
        if (!Plugin.ModConfig.ShowChaserList.Value || !RoundManager.IsActive)
        {
            _chaserList.text = "";
            return;
        }

        var sb = new StringBuilder("<size=75%><alpha=#B0>CHASERS</size><alpha=#FF>\n");
        int me = Character.localCharacter != null ? Net.Actor(Character.localCharacter) : -1;
        foreach (int actor in RoleManager.Chasers.OrderBy(Net.NameOf))
        {
            Character? c = Net.CharacterOf(actor);
            string name = Net.NameOf(actor) + (actor == me ? " (you)" : "");
            if (c != null && c.data.dead) sb.Append($"<alpha=#60><s>{name}</s><alpha=#FF>\n");
            else if (FreezeState.IsFrozen(actor)) sb.Append($"<color={IceHex}>{name}  {FrozenSecondsShown(actor)}s</color>\n");
            else sb.Append(name).Append('\n');
        }

        _chaserList.text = sb.ToString();
    }

    private float _nextFreezeWarning;

    /// <summary>
    /// Whole seconds of a chaser's freeze left, for the chaser list. A freeze never stacks or extends (the host
    /// ignores looks while a chaser is frozen or immune), so the number is clamped to FreezeDuration; if the
    /// replicated end time ever says more, that is logged so the cause can be found.
    /// </summary>
    private int FrozenSecondsShown(int actor)
    {
        float duration = Mathf.Max(0.1f, Plugin.ModConfig.FreezeDuration.Synced());
        float left = FreezeState.FrozenSecondsLeft(actor);
        if (left > duration + 0.5f && Time.time >= _nextFreezeWarning)
        {
            _nextFreezeWarning = Time.time + 5f;
            Plugin.Log.LogWarning($"[OTL][UI] chaser list: {Net.NameOf(actor)} shows {left:0.0}s of freeze left, more than FreezeDuration {duration:0.#}s.");
        }

        return Mathf.Clamp(Mathf.CeilToInt(left), 0, Mathf.CeilToInt(duration));
    }

    // ---------- Events ----------

    private void OnLegStarted(bool newRound)
    {
        if (!newRound) return;
        // Re-anchor under the ascent label in case the HUD was first built where it didn't exist (airport).
        if (_chaserList != null) Destroy(_chaserList.gameObject);
        _chaserList = null;
    }

    private void OnLegCompleted()
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(1f, Plugin.ModConfig.RoleWindowSeconds.Synced()));
        string choose = $"YOU HAVE {seconds} SECONDS TO CHOOSE YOUR ROLE FOR THE NEXT LEG";
        if (RoundManager.ChasersWonLeg) Announce("THE CHASERS WIN THIS LEG", choose, ChaserRed, seconds);
        else Announce("THE RUNNERS WIN THIS LEG", choose, RunnerYellow, seconds);
    }

    private void OnRoundEnded(RoundState result)
    {
        _announceUntil = 0f; // the centre shows GAME OVER! (with who won), then the airport countdown
        Plugin.Log.LogInfo($"[OTL][UI] game over: {result}.");
    }

    private void OnNotice(Notice notice, int a, int b)
    {
        int me = Character.localCharacter != null ? Net.Actor(Character.localCharacter) : -1;
        switch (notice)
        {
            case Notice.Captured:
                Toast($"{Net.NameOf(b)} was caught by {Net.NameOf(a)}!");
                if (Plugin.ModConfig.CaptureSound.Value && Net.CharacterOf(b) is { } runner) CaptureSfx.Play(runner.Center);
                break;
            case Notice.Converted:
                Toast($"{Net.NameOf(a)} has joined the chasers");
                if (a == me) Announce("CHASER", "YOU HAVE JOINED THE CHASERS", ChaserRed, 4f);
                break;
            case Notice.Rewarded:
                Toast($"{Net.NameOf(a)} reached the safe zone first - energy drink!");
                break;
            case Notice.Restarted:
                Toast("The host restarted from the last campfire");
                AdminRestart.OnRestartNotice();
                break;
            case Notice.ZombieHunt:
                bool mineShowing = _announceSub == "IT'S COMING FOR YOU" && Time.time < _announceUntil;
                if (a == me || !mineShowing) Announce("A ZOMBIE IS ON THE HUNT", a == me ? "IT'S COMING FOR YOU" : $"IT'S AFTER {Net.NameOf(a).ToUpperInvariant()}", ZombieGreen, 5f);
                LavaAlert.Play();
                break;
            case Notice.MissingMod:
                Toast($"{Net.NameOf(a)} doesn't have OnTheLookout {Plugin.Version}");
                break;
        }
    }

    private void Announce(string main, string sub, Color color, float seconds)
    {
        _announce = main;
        _announceSub = sub;
        _announceColor = color;
        _announceUntil = Time.time + seconds;
    }

    private void Toast(string text)
    {
        if (!EnsureHud() || _toast == null || _toastGroup == null) return;
        _toast.text = text;
        _toastGroup.DOKill();
        _toast.rectTransform.DOKill();
        _toast.rectTransform.localScale = Vector3.one * 0.85f;
        _toast.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        DOTween.Sequence()
            .Append(_toastGroup.DOFade(1f, 0.2f))
            .AppendInterval(3.5f)
            .Append(_toastGroup.DOFade(0f, 0.6f))
            .SetTarget(_toastGroup);
    }
}

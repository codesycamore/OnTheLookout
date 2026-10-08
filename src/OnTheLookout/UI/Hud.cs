using System.Collections;
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
/// HUD built from PEAK's own pieces so it blends in and scales with the game's HUD canvas:
/// - status widget: a clone of PEAK's action progress ring (UI_UseItemProgress) plus labels in
///   PEAK's interact font, for frozen / freeze immunity / head start / safe zone;
/// - chaser list in the ascent label's font, placed just under it (top right);
/// - toasts for captures, conversions and rewards; hero titles for role and round result.
/// Everything is rebuilt automatically when the HUD canvas is recreated (scene changes).
/// </summary>
internal sealed class Hud : MonoBehaviour
{
    private static readonly Color Warm = new(1f, 0.78f, 0.36f);
    private static readonly Color Soft = new(0.9f, 0.9f, 0.9f);
    private static readonly Color Good = new(0.6f, 0.95f, 0.55f);
    private const string IceHex = "#9FDCFF";

    private Canvas? _canvas;

    private RectTransform? _status;
    private Image? _ringFill;
    private Image? _ringEmpty;
    private TextMeshProUGUI? _statusTitle;
    private TextMeshProUGUI? _statusTime;
    private bool _statusVisible;

    private TextMeshProUGUI? _chaserList;
    private float _nextListRefresh;

    private TextMeshProUGUI? _toast;
    private CanvasGroup? _toastGroup;

    private void OnEnable()
    {
        RoundManager.RoundStarted += OnRoundStarted;
        RoundManager.RoundEnded += OnRoundEnded;
        ModNetwork.NoticeReceived += OnNotice;
    }

    private void OnDisable()
    {
        RoundManager.RoundStarted -= OnRoundStarted;
        RoundManager.RoundEnded -= OnRoundEnded;
        ModNetwork.NoticeReceived -= OnNotice;
    }

    // ---------- Build ----------

    private bool EnsureBuilt()
    {
        GUIManager gui = GUIManager.instance;
        if (gui == null || gui.hudCanvas == null) return false;
        if (_canvas == gui.hudCanvas && _status != null)
        {
            if (_chaserList == null) BuildChaserList(_canvas.transform, gui.interactNameText);
            return true;
        }

        _canvas = gui.hudCanvas;
        TextMeshProUGUI style = gui.interactNameText;
        BuildStatus(_canvas.transform, style);
        BuildChaserList(_canvas.transform, style);
        BuildToast(_canvas.transform, style);
        Plugin.Log.LogInfo($"[OTL][UI] HUD built on '{_canvas.name}' (ring template found: {_ringFill != null}).");
        return true;
    }

    private void BuildStatus(Transform canvas, TextMeshProUGUI style)
    {
        _status = NewRect("OTL_Status", canvas);
        _status.anchorMin = _status.anchorMax = new Vector2(0.5f, 0.5f);
        _status.anchoredPosition = new Vector2(0f, -190f);
        _status.localScale = Vector3.zero;

        // Clone PEAK's own progress ring so it's pixel-identical to the item/interact ring.
        UI_UseItemProgress? template = canvas.GetComponentInChildren<UI_UseItemProgress>(true);
        if (template != null)
        {
            GameObject ring = Instantiate(template.gameObject, _status);
            ring.name = "OTL_Ring";
            UI_UseItemProgress clone = ring.GetComponent<UI_UseItemProgress>();
            _ringFill = clone.fill;
            _ringEmpty = clone.empty;
            DestroyImmediate(clone); // stop the game's script from driving our copy
            ring.SetActive(true);
            var rt = (RectTransform)ring.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            _ringFill.enabled = _ringEmpty.enabled = true;
        }

        _statusTime = NewText("OTL_StatusTime", _status, style, style.fontSize * 0.9f, TextAlignmentOptions.Center);
        _statusTime.rectTransform.anchoredPosition = Vector2.zero;

        _statusTitle = NewText("OTL_StatusTitle", _status, style, style.fontSize * 1.1f, TextAlignmentOptions.Center);
        _statusTitle.rectTransform.anchoredPosition = new Vector2(0f, -70f);
    }

    private void BuildChaserList(Transform canvas, TextMeshProUGUI fallbackStyle)
    {
        // Sit directly under PEAK's ascent label (top right) using the same font and alignment.
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
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(1400f, 80f);
        rt.anchoredPosition = new Vector2(0f, -230f);
        _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>();
        _toastGroup.alpha = 0f;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TextMeshProUGUI style, float size, TextAlignmentOptions align)
    {
        RectTransform rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(800f, 80f);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = style.font;
        t.fontSharedMaterial = style.fontSharedMaterial;
        t.fontStyle = style.fontStyle;
        t.color = style.color;
        t.fontSize = size;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // ---------- Update ----------

    private void Update()
    {
        if (!EnsureBuilt()) return;
        UpdateStatus();
        if (Time.time >= _nextListRefresh)
        {
            _nextListRefresh = Time.time + 0.25f;
            UpdateChaserList();
        }
    }

    private void UpdateStatus()
    {
        Character local = Character.localCharacter;
        string title = "", time = "";
        float fill = -1f;
        Color color = Soft;

        if (local != null && Net.InRoom && RoundManager.IsActive)
        {
            int me = Net.Actor(local);
            var cfg = Plugin.ModConfig;
            bool chaser = RoleManager.IsChaser(local);

            if (FreezeState.IsFrozen(me))
            {
                float left = FreezeState.FrozenSecondsLeft(me);
                title = "FROZEN";
                time = left.ToString("0.0");
                fill = left / Mathf.Max(0.1f, cfg.FreezeDuration.Synced());
                color = local.refs.afflictions.colorCold;
            }
            else if (RoundManager.InHeadStart)
            {
                float left = RoundManager.HeadStartSecondsLeft;
                title = chaser ? "RELEASED IN" : "HEAD START";
                time = Mathf.CeilToInt(left).ToString();
                fill = left / Mathf.Max(0.1f, RoundManager.HeadStartTotal);
                color = chaser ? Warm : Good;
            }
            else if (chaser && FreezeState.IsOnCooldown(me))
            {
                float left = FreezeState.CooldownSecondsLeft(me);
                title = "FREEZE IMMUNE";
                time = left.ToString("0.0");
                fill = left / Mathf.Max(0.1f, cfg.FreezeCooldownSeconds.Synced());
                color = Soft;
            }
            else if (!chaser && !local.data.dead && SafeZoneSystem.IsSafe(local.Center))
            {
                title = "SAFE";
                color = Warm;
            }
        }

        bool show = title.Length > 0;
        if (show != _statusVisible && _status != null)
        {
            _statusVisible = show;
            _status.DOKill();
            if (show) _status.DOScale(1f, 0.25f).SetEase(Ease.OutBack); // same pop-in as PEAK's ring
            else _status.DOScale(0f, 0.15f).SetEase(Ease.InBack);
        }

        if (!show) return;

        _statusTitle!.text = title;
        _statusTitle.color = color;
        _statusTime!.text = time;
        if (_ringFill != null && _ringEmpty != null)
        {
            bool ring = fill >= 0f;
            _ringFill.enabled = _ringEmpty.enabled = ring;
            _ringFill.fillAmount = Mathf.Clamp01(fill);
            _ringFill.color = color;
        }
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
            else if (FreezeState.IsFrozen(actor)) sb.Append($"<color={IceHex}>{name}  {FreezeState.FrozenSecondsLeft(actor):0}s</color>\n");
            else sb.Append(name).Append('\n');
        }

        _chaserList.text = sb.ToString();
    }

    // ---------- Announcements ----------

    private void OnRoundStarted()
    {
        // Re-anchor under the ascent label in case the HUD was first built where it didn't exist (airport).
        if (_chaserList != null) Destroy(_chaserList.gameObject);
        _chaserList = null;
        StartCoroutine(RoleTitleLater());
    }

    private IEnumerator RoleTitleLater()
    {
        yield return new WaitForSeconds(0.5f);
        Character local = Character.localCharacter;
        if (local == null) yield break;
        Hero(RoleManager.RoleOf(Net.Actor(local)) == Role.Chaser ? "YOU ARE A CHASER" : "YOU ARE A RUNNER");
    }

    private void OnRoundEnded(RoundState result) =>
        Hero(result == RoundState.RunnersWon ? "THE RUNNERS ESCAPED" : "THE CHASERS WIN");

    private static void Hero(string text)
    {
        try
        {
            GUIManager.instance?.SetHeroTitle(text, null);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[OTL][UI] hero title failed ({text}): {e.Message}");
        }
    }

    private void OnNotice(Notice notice, int a, int b)
    {
        string text = notice switch
        {
            Notice.Captured => $"{Net.NameOf(b)} was caught by {Net.NameOf(a)}!",
            Notice.Converted => $"{Net.NameOf(a)} has joined the chasers",
            Notice.Rewarded => $"{Net.NameOf(a)} reached the campfire first - ancient loot!",
            Notice.MissingMod => $"{Net.NameOf(a)} doesn't have OnTheLookout {Plugin.Version}",
            _ => "",
        };
        if (text.Length > 0) Toast(text);
    }

    private void Toast(string text)
    {
        if (!EnsureBuilt() || _toast == null || _toastGroup == null) return;
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

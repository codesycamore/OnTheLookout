using System;
using System.Collections.Generic;
using DG.Tweening;
using HarmonyLib;
using OnTheLookout.Core;
using OnTheLookout.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OnTheLookout.UI;

/// <summary>
/// Host menu (toggle with KeyHostMenu, "=" by default): restart at the airport, restart at the previous
/// campfire, teleport everyone to the next campfire, and switch the debug hotkeys on/off.
/// Built from PEAK's own pieces so it fits in: the buttons are clones of the pause menu's Resume button
/// (with PEAK's scripts and localization stripped so they only do what we tell them), titles use PEAK's
/// hero font, and the canvas copies the HUD's scaling. While it's open, GUIManager reports a window that
/// shows the cursor and blocks player input, exactly like PEAK's own menus.
/// </summary>
internal sealed class HostMenu : MonoBehaviour
{
    private const float ConfirmSeconds = 3f;

    public static bool IsOpen { get; private set; }

    private Canvas? _canvas;
    private RectTransform? _panel;
    private TextMeshProUGUI? _debugLabel;
    private TextMeshProUGUI? _randomMapLabel;
    private readonly Dictionary<TextMeshProUGUI, string> _labels = new();
    private TextMeshProUGUI? _pendingLabel;
    private Action? _pendingAction;
    private float _pendingUntil;

    public static bool Install(Harmony harmony) =>
        // Patch target: GUIManager.UpdateWindowStatus() (postfix). Why: PEAK recomputes every frame whether
        // an open window shows the cursor (CursorHandler) and blocks player input (Character.CanDoInput).
        SafePatch.Postfix(harmony, typeof(GUIManager), nameof(GUIManager.UpdateWindowStatus), typeof(HostMenu), nameof(WindowStatusPostfix), "HostMenu");

    public static void WindowStatusPostfix(GUIManager __instance)
    {
        if (!IsOpen && !ChaserOddsMenu.IsOpen) return;
        __instance.windowShowingCursor = true;
        __instance.windowBlockingInput = true;
    }

    private void Update()
    {
        Keyboard? kb = Keyboard.current;
        bool canUse = Net.InRoom && Net.IsHost && GUIManager.instance != null && Character.localCharacter != null;
        UpdateHint(canUse && !IsOpen && !ChaserOddsMenu.IsOpen);
        if (IsOpen && !canUse)
        {
            Close();
            return;
        }

        if (kb == null || !canUse) return;
        Key hostKey = Plugin.ModConfig.KeyHostMenu.Value;
        if (kb[hostKey].wasPressedThisFrame || (hostKey == Key.Equals && kb.numpadPlusKey.wasPressedThisFrame))
        {
            if (IsOpen) Close();
            else Open();
        }
        else if (IsOpen && kb.escapeKey.wasPressedThisFrame)
        {
            Close();
        }

        if (_pendingLabel != null && Time.time > _pendingUntil) ClearPending();
    }

    // ---------- Host hint above the stamina bar ----------

    private Canvas? _hintCanvas;
    private RectTransform? _hintPlate;
    private TextMeshProUGUI? _hint;
    private readonly Vector3[] _corners = new Vector3[4];

    /// <summary>
    /// Host only: a small "PRESS HOTKEY (+) FOR HOST CONTROLS" just above the host's own stamina bar, on a
    /// translucent plate (same look as the role hint, smaller). Placed from the stamina bar's screen position
    /// each frame so it follows PEAK's HUD layout.
    /// </summary>
    private void UpdateHint(bool show)
    {
        GUIManager gui = GUIManager.instance;
        RectTransform? bar = gui != null && gui.bar != null ? gui.bar.fullBar : null;
        show &= bar != null && bar.gameObject.activeInHierarchy;
        if (show && _hintCanvas == null) BuildHint(gui!);
        HintHeight = 0f;
        if (_hintCanvas == null) return;
        if (_hintCanvas.gameObject.activeSelf != show) _hintCanvas.gameObject.SetActive(show);
        if (!show) return;

        string text = $"PRESS HOTKEY ({HostKeyName()}) FOR HOST CONTROLS";
        if (_hint!.text != text)
        {
            _hint.text = text;
            _hintPlate!.sizeDelta = new Vector2(_hint.preferredWidth + 28f, _hint.preferredHeight + 10f);
        }

        bar!.GetWorldCorners(_corners); // 1 = top-left
        Canvas? barCanvas = bar.GetComponentInParent<Canvas>();
        Camera? cam = barCanvas != null && barCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? barCanvas.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, _corners[1]);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_hintCanvas.transform, screen, null, out Vector2 local))
        {
            _hintPlate!.anchoredPosition = local + new Vector2(0f, 12f);
        }

        HintHeight = _hintPlate!.sizeDelta.y;
    }

    /// <summary>Height of the host tip above the stamina bar while it shows (0 when hidden); the role tips stack above it.</summary>
    public static float HintHeight { get; private set; }

    private void BuildHint(GUIManager gui)
    {
        _hintCanvas = PeakMenuKit.CreateCanvas("OTL_HostHint", transform, gui, 19);
        _hintCanvas.GetComponent<GraphicRaycaster>().enabled = false;
        var plateGo = new GameObject("Plate", typeof(RectTransform));
        plateGo.transform.SetParent(_hintCanvas.transform, false);
        _hintPlate = (RectTransform)plateGo.transform;
        _hintPlate.anchorMin = _hintPlate.anchorMax = new Vector2(0.5f, 0.5f);
        _hintPlate.pivot = new Vector2(0f, 0f); // bottom-left sits on the bar's top-left corner
        Image plate = plateGo.AddComponent<Image>();
        plate.color = new Color(0.04f, 0.05f, 0.08f, 0.45f);
        plate.raycastTarget = false;

        _hint = PeakMenuKit.AddText(_hintPlate, "", gui.interactNameText, 18f, new Color(1f, 0.84f, 0.2f, 1f), 30f);
        RectTransform rt = _hint.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>The host menu key as players think of it: "=" is the "+" key on most keyboards.</summary>
    private static string HostKeyName()
    {
        Key key = Plugin.ModConfig.KeyHostMenu.Value;
        if (key is Key.Equals or Key.NumpadPlus) return "+";
        Keyboard? kb = Keyboard.current;
        string name = kb != null ? kb[key].displayName : "";
        return string.IsNullOrWhiteSpace(name) ? key.ToString().ToUpperInvariant() : name.ToUpperInvariant();
    }

    // ---------- Open / close ----------

    private void Open()
    {
        if (!Build()) return;
        IsOpen = true;
        _canvas!.gameObject.SetActive(true);
        RefreshDebugLabel();
        RectTransform panel = _panel!;
        panel.DOKill();
        panel.localScale = Vector3.one * 0.9f;
        panel.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
    }

    private void Close()
    {
        IsOpen = false;
        ClearPending();
        if (_panel != null) ShowPage(settings: false); // opens on the main page next time
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    // ---------- Build ----------

    private bool Build()
    {
        GUIManager gui = GUIManager.instance;
        if (_canvas != null) return true;
        Button? template = PeakMenuKit.ButtonTemplate(gui);
        if (template == null)
        {
            Plugin.Log.LogWarning("[OTL][HostMenu] PEAK's pause menu button not found; can't build the menu.");
            return false;
        }

        _canvas = PeakMenuKit.CreateCanvas("OTL_HostMenu", transform, gui, 50);
        _panel = PeakMenuKit.CreatePanel(_canvas, template, new Vector2(760f, 640f));
        // The panel grows and shrinks with the page shown (main page / settings page).
        _panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI titleFont = gui.heroText != null ? gui.heroText : gui.interactNameText;
        PeakMenuKit.AddText(_panel, "HOST MENU", titleFont, 64f, Color.white, 80f);
        PeakMenuKit.AddText(_panel, "ON THE LOOKOUT", gui.interactNameText, 26f, new Color(1f, 0.84f, 0.2f), 36f);

        int first = _panel.childCount;
        AddButton(template, "RESTART AT THE AIRPORT", confirm: true, AdminRestart.HostReturnToAirport);
        AddButton(template, "RESTART AT PREVIOUS CAMPFIRE", confirm: true, AdminRestart.HostRestartFromCampfire);
        AddButton(template, "TELEPORT EVERYONE TO NEXT CAMPFIRE", confirm: false, AdminRestart.HostTeleportToNextCampfire);
        AddButton(template, "SETTINGS", confirm: false, () => ShowPage(settings: true), closeAfter: false);
        _randomMapLabel = AddButton(template, "", confirm: false, ToggleRandomMap, closeAfter: false);
        _debugLabel = AddButton(template, "", confirm: false, ToggleDebugKeys, closeAfter: false);
        AddButton(template, "CLOSE", confirm: false, () => { });
        CollectPage(_mainPage, first);

        first = _panel.childCount;
        BuildSettingsPage(gui, template);
        CollectPage(_settingsPage, first);
        ShowPage(settings: false);

        _canvas.gameObject.SetActive(false);
        Plugin.Log.LogInfo("[OTL][HostMenu] built.");
        return true;
    }

    /// <summary>A clone of PEAK's pause-menu button with our label and action.</summary>
    private TextMeshProUGUI AddButton(Button template, string label, bool confirm, Action action, bool closeAfter = true)
    {
        (Button button, TextMeshProUGUI text) = PeakMenuKit.CloneButton(_panel!, template, label);
        _labels[text] = label;

        button.onClick.AddListener(() =>
        {
            if (confirm && _pendingLabel != text)
            {
                ClearPending();
                _pendingLabel = text;
                _pendingAction = action;
                _pendingUntil = Time.time + ConfirmSeconds;
                text.text = "CLICK AGAIN TO CONFIRM";
                return;
            }

            ClearPending();
            try
            {
                action();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[OTL][HostMenu] '{label}' failed: {e}");
            }

            if (closeAfter) Close();
        });

        return text;
    }

    private void ClearPending()
    {
        if (_pendingLabel != null && _labels.TryGetValue(_pendingLabel, out string original)) _pendingLabel.text = original;
        _pendingLabel = null;
        _pendingAction = null;
    }

    // ---------- Pages ----------

    private readonly List<GameObject> _mainPage = new();
    private readonly List<GameObject> _settingsPage = new();

    /// <summary>Everything added to the panel from child <paramref name="first"/> on belongs to <paramref name="page"/>.</summary>
    private void CollectPage(List<GameObject> page, int first)
    {
        for (int i = first; i < _panel!.childCount; i++) page.Add(_panel.GetChild(i).gameObject);
    }

    private void ShowPage(bool settings)
    {
        ClearPending();
        foreach (GameObject go in _mainPage) go.SetActive(!settings);
        foreach (GameObject go in _settingsPage) go.SetActive(settings);
        if (settings) RefreshSettings();
    }

    // ---------- Settings page ----------

    /// <summary>
    /// One editable setting: the settings players may change (ModConfig.Configurable), with a step and limits for the
    /// - / + buttons. Changing one writes the config file and the host's values are synced to everyone at once
    /// (ModConfig: config.SettingChanged -> ConfigSync.Publish).
    /// </summary>
    private sealed class SettingRow
    {
        public string Label = "";
        public Func<BepInEx.Configuration.ConfigEntry<float>> Entry = null!;
        public float Step, Min, Max;
        public bool Seconds = true;
        public Func<float, string>? Format;
        public TextMeshProUGUI? Text;
    }

    private readonly List<SettingRow> _settings = new();

    private void BuildSettingsPage(GUIManager gui, Button template)
    {
        var cfg = Plugin.ModConfig;
        _settings.Clear();
        _settings.Add(new SettingRow { Label = "HEAD START", Entry = () => cfg.HeadStartSeconds, Step = 5f, Min = 0f, Max = 120f });
        _settings.Add(new SettingRow { Label = "HEAD-START BOOST (BIOMES 1-4)", Entry = () => cfg.HeadStartBoostSeconds, Step = 1f, Min = 0f, Max = 20f });
        // CHASERS: AUTO (0, one per RunnersPerChaser runners) or an exact count; used from the next role draw on.
        _settings.Add(new SettingRow { Label = "CHASERS", Entry = () => cfg.ChaserCount, Step = 1f, Min = 0f, Max = 10f, Seconds = false,
            Format = v => v < 0.5f ? $"AUTO (1 PER {Mathf.RoundToInt(cfg.RunnersPerChaser.Value)} RUNNERS)" : v.ToString("0") });
        _settings.Add(new SettingRow { Label = "ZOMBIES START AFTER", Entry = () => cfg.ZombieStartDelaySeconds, Step = 30f, Min = 0f, Max = 1200f });
        _settings.Add(new SettingRow { Label = "ZOMBIE WAVE COOLDOWN", Entry = () => cfg.ZombieWaveDelaySeconds, Step = 15f, Min = 0f, Max = 600f });
        _settings.Add(new SettingRow { Label = "ZOMBIE LIFETIME", Entry = () => cfg.ZombieLifetimeSeconds, Step = 15f, Min = 15f, Max = 600f });

        PeakMenuKit.AddText(_panel!, "SETTINGS  (FROM THE NEXT LEG ON)", gui.interactNameText, 22f, new Color(1f, 1f, 1f, 0.7f), 30f);
        foreach (SettingRow row in _settings) AddSettingRow(gui, template, row);
        AddButton(template, "RESET TO DEFAULTS", confirm: true, ResetSettings, closeAfter: false);
        AddButton(template, "BACK", confirm: false, () => ShowPage(settings: false), closeAfter: false);
    }

    /// <summary>[ - ]  LABEL: value  [ + ]</summary>
    private void AddSettingRow(GUIManager gui, Button template, SettingRow row)
    {
        RectTransform rect = PeakMenuKit.NewRect("OTL_SettingRow", _panel!);
        rect.sizeDelta = new Vector2(0f, 64f);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        (Button minus, _) = PeakMenuKit.CloneButton(rect, template, "-");
        minus.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;
        row.Text = PeakMenuKit.AddText(rect, "", gui.interactNameText, 26f, Color.white, 64f);
        row.Text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        (Button plus, _) = PeakMenuKit.CloneButton(rect, template, "+");
        plus.gameObject.AddComponent<LayoutElement>().preferredWidth = 90f;

        minus.onClick.AddListener(() => ChangeSetting(row, -row.Step));
        plus.onClick.AddListener(() => ChangeSetting(row, row.Step));
    }

    private void ChangeSetting(SettingRow row, float delta)
    {
        BepInEx.Configuration.ConfigEntry<float> entry = row.Entry();
        float value = Mathf.Clamp(Mathf.Round((entry.Value + delta) / row.Step) * row.Step, row.Min, row.Max);
        if (Mathf.Approximately(value, entry.Value)) return;
        entry.Value = value; // saved to the config file and synced to everyone (ModConfig: SettingChanged)
        Plugin.Log.LogInfo($"[OTL][HostMenu] {entry.Definition.Key} = {value:0.##}.");
        RefreshSettings();
    }

    private void ResetSettings()
    {
        foreach (SettingRow row in _settings)
        {
            BepInEx.Configuration.ConfigEntry<float> entry = row.Entry();
            entry.Value = (float)entry.DefaultValue;
        }

        Plugin.Log.LogInfo("[OTL][HostMenu] settings reset to the defaults.");
        RefreshSettings();
    }

    private void RefreshSettings()
    {
        foreach (SettingRow row in _settings)
        {
            if (row.Text == null) continue;
            float value = row.Entry().Value;
            string shown = row.Format != null ? row.Format(value) : row.Seconds ? FormatSeconds(value) : value.ToString("0");
            row.Text.text = $"{row.Label}: {shown}";
        }
    }

    private static string FormatSeconds(float seconds) =>
        seconds >= 60f ? $"{Mathf.FloorToInt(seconds / 60f)}:{Mathf.RoundToInt(seconds % 60f):00} MIN" : $"{seconds:0} S";

    // ---------- Debug hotkeys toggle ----------

    private void ToggleDebugKeys()
    {
        Plugin.ModConfig.DebugKeys.Value = !Plugin.ModConfig.DebugKeys.Value;
        RefreshDebugLabel();
        Plugin.Log.LogInfo($"[OTL][HostMenu] debug hotkeys {(Plugin.ModConfig.DebugKeys.Value ? "enabled" : "disabled")}.");
    }

    // ---------- Random map toggle ----------

    private void ToggleRandomMap()
    {
        Plugin.ModConfig.RandomMap.Value = !Plugin.ModConfig.RandomMap.Value;
        RefreshDebugLabel();
        Plugin.Log.LogInfo($"[OTL][HostMenu] random map {(Plugin.ModConfig.RandomMap.Value ? "on" : "off (today's map)")}.");
    }

    private void RefreshDebugLabel()
    {
        if (_randomMapLabel != null)
        {
            string map = $"MAP: {(Plugin.ModConfig.RandomMap.Value ? "RANDOM" : "TODAY'S MAP")}";
            _randomMapLabel.text = map;
            _labels[_randomMapLabel] = map;
        }

        if (_debugLabel == null) return;
        string label = $"DEBUG HOTKEYS: {(Plugin.ModConfig.DebugKeys.Value ? "ON" : "OFF")}";
        _debugLabel.text = label;
        _labels[_debugLabel] = label;
    }
}

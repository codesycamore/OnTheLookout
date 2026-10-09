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
    }

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

        _hint = PeakMenuKit.AddText(_hintPlate, "", gui.interactNameText, 22f, new Color(1f, 0.84f, 0.2f, 1f), 30f);
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
        _panel = PeakMenuKit.CreatePanel(_canvas, template, new Vector2(720f, 640f));

        TextMeshProUGUI titleFont = gui.heroText != null ? gui.heroText : gui.interactNameText;
        PeakMenuKit.AddText(_panel, "HOST MENU", titleFont, 64f, Color.white, 80f);
        PeakMenuKit.AddText(_panel, "ON THE LOOKOUT", gui.interactNameText, 26f, new Color(1f, 0.84f, 0.2f), 36f);

        AddButton(template, "RESTART AT THE AIRPORT", confirm: true, AdminRestart.HostReturnToAirport);
        AddButton(template, "RESTART AT PREVIOUS CAMPFIRE", confirm: true, AdminRestart.HostRestartFromCampfire);
        AddButton(template, "TELEPORT EVERYONE TO NEXT CAMPFIRE", confirm: false, AdminRestart.HostTeleportToNextCampfire);
        _debugLabel = AddButton(template, "", confirm: false, ToggleDebugKeys, closeAfter: false);
        AddButton(template, "CLOSE", confirm: false, () => { });

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

    // ---------- Debug hotkeys toggle ----------

    private void ToggleDebugKeys()
    {
        Plugin.ModConfig.DebugKeys.Value = !Plugin.ModConfig.DebugKeys.Value;
        RefreshDebugLabel();
        Plugin.Log.LogInfo($"[OTL][HostMenu] debug hotkeys {(Plugin.ModConfig.DebugKeys.Value ? "enabled" : "disabled")}.");
    }

    private void RefreshDebugLabel()
    {
        if (_debugLabel == null) return;
        string label = $"DEBUG HOTKEYS: {(Plugin.ModConfig.DebugKeys.Value ? "ON" : "OFF")}";
        _debugLabel.text = label;
        _labels[_debugLabel] = label;
    }
}

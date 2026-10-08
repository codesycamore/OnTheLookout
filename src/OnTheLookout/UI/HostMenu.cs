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
        if (IsOpen && !canUse)
        {
            Close();
            return;
        }

        if (kb == null || !canUse) return;
        if (kb[Plugin.ModConfig.KeyHostMenu.Value].wasPressedThisFrame)
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

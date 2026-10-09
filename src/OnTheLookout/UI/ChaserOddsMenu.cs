using System.Collections.Generic;
using DG.Tweening;
using OnTheLookout.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OnTheLookout.UI;

/// <summary>
/// Role menu (KeyChaserOdds, "-" by default), open in the airport (for the first draw on the shore) and
/// while the role window runs after each leg: every player is a RUNNER by default and can volunteer as a
/// CHASER. Chasers are drawn at random from the volunteers. Only the player sees their own choice; it goes
/// privately to the host (<see cref="ChaserPreference"/>). A hint is shown while the menu is available.
/// Same PEAK-style building blocks as the host menu (<see cref="PeakMenuKit"/>).
/// </summary>
internal sealed class ChaserOddsMenu : MonoBehaviour
{
    private static readonly Color Selected = new(1f, 0.84f, 0.2f);

    public static bool IsOpen { get; private set; }

    private Canvas? _canvas;
    private RectTransform? _panel;
    private Canvas? _hintCanvas;
    private TextMeshProUGUI? _hint;
    private RectTransform? _hintPlate;
    private readonly Dictionary<ChaserPref, (TextMeshProUGUI Label, string Text)> _options = new();

    private void Update()
    {
        bool available = ChaserPreference.Available && GUIManager.instance != null;
        if (IsOpen && !available) Close();
        UpdateHint(available);
        if (!available) return;

        Keyboard? kb = Keyboard.current;
        if (kb == null) return;
        if (kb[Plugin.ModConfig.KeyChaserOdds.Value].wasPressedThisFrame)
        {
            if (IsOpen) Close();
            else Open();
        }
        else if (IsOpen && kb.escapeKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    // ---------- Hint ----------

    private void UpdateHint(bool available)
    {
        bool show = available && !IsOpen && !HostMenu.IsOpen;
        if (show && _hintCanvas == null) BuildHint();
        if (_hintCanvas != null && _hintCanvas.gameObject.activeSelf != show) _hintCanvas.gameObject.SetActive(show);
        if (show && _hint != null)
        {
            string text = $"PRESS HOTKEY ({KeyName()}) TO SELECT YOUR ROLE: {(ChaserPreference.Local == ChaserPref.Chaser ? "CHASER" : "RUNNER")}";
            if (_hint.text != text)
            {
                _hint.text = text;
                FitHintPlate();
            }
        }
    }

    private void BuildHint()
    {
        GUIManager gui = GUIManager.instance;
        _hintCanvas = PeakMenuKit.CreateCanvas("OTL_ChaserOddsHint", transform, gui, 20);
        _hintCanvas.GetComponent<GraphicRaycaster>().enabled = false; // never blocks clicks
        // Bright PEAK yellow on a dark translucent plate so it reads on snow, sky and dark rock alike.
        // (A TMP outline thick enough for contrast eats into the glyphs of PEAK's bold font, so no outline.)
        var plateGo = new GameObject("Plate", typeof(RectTransform));
        plateGo.transform.SetParent(_hintCanvas.transform, false);
        _hintPlate = (RectTransform)plateGo.transform;
        _hintPlate.anchorMin = _hintPlate.anchorMax = _hintPlate.pivot = new Vector2(0.5f, 0f);
        _hintPlate.anchoredPosition = new Vector2(0f, 36f);
        Image plate = plateGo.AddComponent<Image>();
        plate.color = new Color(0.04f, 0.05f, 0.08f, 0.72f);
        plate.raycastTarget = false;

        _hint = PeakMenuKit.AddText(_hintPlate, "", gui.interactNameText, 34f, new Color(1f, 0.84f, 0.2f, 1f), 50f);
        RectTransform rt = _hint.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private void FitHintPlate()
    {
        if (_hint == null || _hintPlate == null) return;
        _hintPlate.sizeDelta = new Vector2(_hint.preferredWidth + 48f, _hint.preferredHeight + 16f);
    }

    private static string KeyName()
    {
        Keyboard? kb = Keyboard.current;
        string name = kb != null ? kb[Plugin.ModConfig.KeyChaserOdds.Value].displayName : "";
        return string.IsNullOrWhiteSpace(name) ? Plugin.ModConfig.KeyChaserOdds.Value.ToString().ToUpperInvariant() : name.ToUpperInvariant();
    }

    // ---------- Menu ----------

    private void Open()
    {
        if (!Build()) return;
        IsOpen = true;
        _canvas!.gameObject.SetActive(true);
        RefreshSelection();
        RectTransform panel = _panel!;
        panel.DOKill();
        panel.localScale = Vector3.one * 0.9f;
        panel.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
    }

    private void Close()
    {
        IsOpen = false;
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    private bool Build()
    {
        if (_canvas != null) return true;
        GUIManager gui = GUIManager.instance;
        Button? template = PeakMenuKit.ButtonTemplate(gui);
        if (template == null)
        {
            Plugin.Log.LogWarning("[OTL][Roles] PEAK's pause menu button not found; can't build the menu.");
            return false;
        }

        _canvas = PeakMenuKit.CreateCanvas("OTL_ChaserOdds", transform, gui, 50);
        _panel = PeakMenuKit.CreatePanel(_canvas, template, new Vector2(720f, 520f));
        TextMeshProUGUI titleFont = gui.heroText != null ? gui.heroText : gui.interactNameText;
        PeakMenuKit.AddText(_panel, "YOUR ROLE", titleFont, 64f, Color.white, 80f);
        PeakMenuKit.AddText(_panel, "ONLY YOU CAN SEE THIS  -  CHASERS ARE DRAWN FROM VOLUNTEERS", gui.interactNameText, 22f, new Color(1f, 1f, 1f, 0.7f), 32f);

        AddOption(template, ChaserPref.Runner, "I WANT TO BE A RUNNER");
        AddOption(template, ChaserPref.Chaser, "I WANT TO BE A CHASER");
        (Button close, _) = PeakMenuKit.CloneButton(_panel, template, "CLOSE");
        close.onClick.AddListener(Close);

        _canvas.gameObject.SetActive(false);
        return true;
    }

    private void AddOption(Button template, ChaserPref pref, string text)
    {
        (Button button, TextMeshProUGUI label) = PeakMenuKit.CloneButton(_panel!, template, text);
        _options[pref] = (label, text);
        button.onClick.AddListener(() =>
        {
            ChaserPreference.SetLocal(pref);
            RefreshSelection();
        });
    }

    private void RefreshSelection()
    {
        foreach (KeyValuePair<ChaserPref, (TextMeshProUGUI Label, string Text)> option in _options)
        {
            bool selected = option.Key == ChaserPreference.Local;
            option.Value.Label.text = selected ? $"> {option.Value.Text} <" : option.Value.Text;
            option.Value.Label.color = selected ? Selected : Color.white;
        }
    }
}

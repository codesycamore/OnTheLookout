using OnTheLookout.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnTheLookout.UI;

/// <summary>
/// Runner's side of a capture: while a chaser holds interact on the local runner (<see cref="Modules.TagSystem"/>),
/// a red bar under the crosshair fills up over the same hold time the chaser sees on their ring, so the runner
/// knows how close the capture is. The chaser's client sends the start (server time + duration) and the stop.
/// </summary>
internal sealed class CaptureIndicator : MonoBehaviour
{
    private const float BarWidth = 420f, BarHeight = 18f;
    private static readonly Color FillColor = new(0.95f, 0.15f, 0.12f, 0.95f);

    private static float s_StartedAt = -1f, s_Duration;
    private static int s_Chaser;

    private Canvas? _canvas;
    private RectTransform? _fill;
    private TextMeshProUGUI? _label;

    /// <summary>Local runner's client: a chaser started (durationMs &gt; 0) or stopped (0) capturing them.</summary>
    public static void OnProgress(int chaserActor, int startServerTime, int durationMs)
    {
        if (durationMs <= 0)
        {
            if (chaserActor == s_Chaser) s_StartedAt = -1f;
            return;
        }

        s_Chaser = chaserActor;
        s_Duration = durationMs / 1000f;
        s_StartedAt = Time.time - Mathf.Max(0f, unchecked(Net.Now - startServerTime) / 1000f); // allow for the message's travel time
    }

    private void Update()
    {
        Character local = Character.localCharacter;
        float progress = s_StartedAt < 0f ? -1f : (Time.time - s_StartedAt) / Mathf.Max(0.1f, s_Duration);
        bool show = progress >= 0f && progress <= 1.15f && local != null && !local.data.dead && RoleManager.IsRunner(local);
        if (!show)
        {
            if (progress > 1.15f) s_StartedAt = -1f; // never got a stop message: give up after the hold time
            if (_canvas != null && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
            return;
        }

        if (_canvas == null && !Build()) return;
        if (!_canvas!.gameObject.activeSelf) _canvas.gameObject.SetActive(true);
        _fill!.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(progress), BarHeight);
        _label!.text = $"{Net.NameOf(s_Chaser).ToUpperInvariant()} IS CAPTURING YOU!";
    }

    private bool Build()
    {
        GUIManager gui = GUIManager.instance;
        if (gui == null || gui.interactNameText == null) return false;
        _canvas = PeakMenuKit.CreateCanvas("OTL_CaptureIndicator", transform, gui, 30);
        _canvas.GetComponent<GraphicRaycaster>().enabled = false;

        RectTransform back = NewImage("Back", (RectTransform)_canvas.transform, new Color(0.05f, 0.05f, 0.08f, 0.7f), new Vector2(BarWidth + 8f, BarHeight + 8f));
        back.anchoredPosition = new Vector2(0f, -120f); // under the crosshair
        _fill = NewImage("Fill", back, FillColor, new Vector2(0f, BarHeight));
        _fill.anchorMin = _fill.anchorMax = _fill.pivot = new Vector2(0f, 0.5f);
        _fill.anchoredPosition = new Vector2(4f, 0f);

        _label = PeakMenuKit.AddText(back, "", gui.interactNameText, 28f, new Color(1f, 0.35f, 0.3f, 1f), 40f);
        RectTransform rt = _label.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(900f, 40f);
        rt.anchoredPosition = new Vector2(0f, BarHeight + 10f);
        _label.outlineWidth = 0.15f;
        _label.outlineColor = new Color32(10, 12, 20, 255);
        return true;
    }

    private static RectTransform NewImage(string name, RectTransform parent, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OnTheLookout.UI;

/// <summary>
/// Building blocks for the mod's PEAK-style menus (host menu, chaser-odds menu): an overlay canvas scaled
/// like PEAK's HUD, a dimmed backdrop, a panel framed with PEAK's own button sprite, PEAK-font text, and
/// clones of PEAK's pause-menu button stripped of PEAK's scripts so they only do what we tell them.
/// </summary>
internal static class PeakMenuKit
{
    /// <summary>PEAK's pause-menu "Resume" button, used as the template for every menu button.</summary>
    public static Button? ButtonTemplate(GUIManager gui) =>
        gui.pauseMenuMainPage != null ? gui.pauseMenuMainPage.resumeButton : null;

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>A clickable overlay canvas above PEAK's HUD, scaled like the HUD.</summary>
    public static Canvas CreateCanvas(string name, Transform parent, GUIManager gui, int sortingOffset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = (gui.hudCanvas != null ? gui.hudCanvas.sortingOrder : 0) + sortingOffset;
        var scaler = go.AddComponent<CanvasScaler>();
        if (gui.hudCanvas != null && gui.hudCanvas.GetComponent<CanvasScaler>() is { } hud)
        {
            scaler.uiScaleMode = hud.uiScaleMode;
            scaler.referenceResolution = hud.referenceResolution;
            scaler.matchWidthOrHeight = hud.matchWidthOrHeight;
            scaler.screenMatchMode = hud.screenMatchMode;
        }
        else
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>A full-screen dim behind the panel plus the panel itself (vertical layout), framed with PEAK's button sprite.</summary>
    public static RectTransform CreatePanel(Canvas canvas, Button template, Vector2 size)
    {
        RectTransform dim = NewRect("Dim", canvas.transform);
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.offsetMin = dim.offsetMax = Vector2.zero;
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform panel = NewRect("Panel", canvas.transform);
        panel.sizeDelta = size;
        var image = panel.gameObject.AddComponent<Image>();
        if (template.GetComponent<Image>() is { sprite: not null } buttonImage)
        {
            image.sprite = buttonImage.sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = buttonImage.pixelsPerUnitMultiplier;
        }

        image.color = new Color(0.13f, 0.11f, 0.09f, 0.96f);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 30, 30);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return panel;
    }

    public static TextMeshProUGUI AddText(Transform parent, string text, TextMeshProUGUI style, float size, Color color, float height)
    {
        RectTransform rt = NewRect("Text", parent);
        rt.sizeDelta = new Vector2(0f, height);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = style.font;
        t.fontSharedMaterial = style.fontSharedMaterial;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    /// <summary>A clone of PEAK's pause-menu button with our label and no listeners.</summary>
    public static (Button Button, TextMeshProUGUI Label) CloneButton(Transform parent, Button template, string label)
    {
        GameObject go = Object.Instantiate(template.gameObject, parent);
        go.name = "OTL_Button";
        go.SetActive(true);

        // Keep only Unity UI / TextMesh Pro parts: PEAK's own scripts (localization, page navigation, sounds
        // wired to the pause page) would fight our label or act on the pause menu.
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string ns = mb.GetType().Namespace ?? "";
            if (!ns.StartsWith("UnityEngine", StringComparison.Ordinal) && !ns.StartsWith("TMPro", StringComparison.Ordinal)) Object.DestroyImmediate(mb);
        }

        var button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        TextMeshProUGUI text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        text.text = label;

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(0f, Mathf.Max(64f, rt.sizeDelta.y));
        return (button, text);
    }
}

using OnTheLookout.Core;
using OnTheLookout.Modules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OnTheLookout.UI;

/// <summary>
/// Blowgun cooldown indicator for chasers: a clone of PEAK's own action progress ring
/// (UI_UseItemProgress) draining over the cooldown, with the seconds left in the middle.
/// It lives directly on the HUD canvas (drawn on top, not inside the hotbar's own layout, which can
/// clip or hide extra children) and is moved every frame to sit just above the hotbar slot holding
/// the blowgun, or above the middle of the hotbar if that slot can't be found.
/// </summary>
internal sealed class BlowgunCooldownUI
{
    private const float RingScale = 0.8f;
    private const float AboveSlotPixels = 70f;

    private readonly RectTransform _root;
    private readonly Image? _fill;
    private readonly TextMeshProUGUI _text;
    private readonly Vector3[] _corners = new Vector3[4];
    private bool _logged;

    public bool IsValid => _root != null;

    public BlowgunCooldownUI(Canvas hud, TextMeshProUGUI fontStyle)
    {
        var go = new GameObject("OTL_BlowgunCooldown", typeof(RectTransform));
        _root = (RectTransform)go.transform;
        _root.SetParent(hud.transform, false);
        _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
        _root.sizeDelta = new Vector2(120f, 120f);
        go.SetActive(false);

        UI_UseItemProgress? template = hud.GetComponentInChildren<UI_UseItemProgress>(true);
        if (template != null)
        {
            GameObject ring = Object.Instantiate(template.gameObject, _root);
            ring.name = "OTL_BlowgunRing";
            UI_UseItemProgress clone = ring.GetComponent<UI_UseItemProgress>();
            _fill = clone.fill;
            Image empty = clone.empty;
            Object.DestroyImmediate(clone); // stop the game's script from driving (and hiding) our copy
            ring.SetActive(true);
            var rt = (RectTransform)ring.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one * RingScale;
            _fill.enabled = empty.enabled = true;
            _fill.color = new Color(1f, 0.45f, 0.35f);
        }

        var textGo = new GameObject("OTL_BlowgunCooldownText", typeof(RectTransform));
        var textRect = (RectTransform)textGo.transform;
        textRect.SetParent(_root, false);
        textRect.sizeDelta = new Vector2(140f, 70f);
        _text = textGo.AddComponent<TextMeshProUGUI>();
        _text.font = fontStyle.font;
        _text.fontSharedMaterial = fontStyle.fontSharedMaterial;
        _text.fontSize = fontStyle.fontSize;
        _text.alignment = TextAlignmentOptions.Center;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.raycastTarget = false;
        _text.color = Color.white;
        _text.outlineWidth = 0.25f;
        _text.outlineColor = new Color32(20, 30, 45, 255);

        Plugin.Log.LogInfo($"[OTL][UI] blowgun cooldown indicator built (PEAK ring found: {template != null}).");
    }

    public void Update(GUIManager gui)
    {
        Character local = Character.localCharacter;
        bool show = BlowgunSystem.OnCooldown && local != null && RoleManager.IsChaser(local) && !local.data.dead;
        if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
        if (!show) return;

        _root.SetAsLastSibling(); // draw above the rest of the HUD

        int slot = BlowgunSlot();
        RectTransform? anchor = SlotRect(gui, slot) ?? SlotRect(gui, gui.items != null ? gui.items.Length / 2 : -1);
        if (anchor != null)
        {
            anchor.GetWorldCorners(_corners); // 1 top-left, 2 top-right
            Vector3 topCenter = (_corners[1] + _corners[2]) * 0.5f;
            _root.position = topCenter + anchor.up * (AboveSlotPixels * _root.lossyScale.y);
        }
        else
        {
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.anchoredPosition = new Vector2(0f, 220f);
        }

        if (!_logged)
        {
            _logged = true;
            Plugin.Log.LogInfo($"[OTL][UI] blowgun cooldown shown (blowgun slot {slot}, anchored to {(anchor != null ? anchor.name : "screen bottom")}).");
        }

        float total = Mathf.Max(0.1f, Plugin.ModConfig.BlowgunCooldownSeconds.Synced());
        float left = BlowgunSystem.CooldownSecondsLeft;
        if (_fill != null) _fill.fillAmount = left / total;
        _text.text = Mathf.CeilToInt(left).ToString();
    }

    private static RectTransform? SlotRect(GUIManager gui, int slot) =>
        gui.items != null && slot >= 0 && slot < gui.items.Length && gui.items[slot] != null
            ? gui.items[slot].transform as RectTransform
            : null;

    private static int BlowgunSlot()
    {
        Player player = Player.localPlayer;
        if (player == null || player.itemSlots == null) return -1;
        for (int i = 0; i < player.itemSlots.Length; i++)
        {
            ItemSlot s = player.itemSlots[i];
            if (s != null && !s.IsEmpty() && s.prefab != null && ItemCatalog.IsBlowgun(s.prefab)) return i;
        }

        return -1;
    }

    public void Destroy()
    {
        if (_root != null) Object.Destroy(_root.gameObject);
    }
}

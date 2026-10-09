using DG.Tweening;
using OnTheLookout.Core;
using OnTheLookout.Modules;
using TMPro;
using UnityEngine;

namespace OnTheLookout.UI;

/// <summary>
/// A chaser item's cooldown (blowgun, chaser napberry): the seconds left as a number, in the same PEAK title
/// font as the head-start countdown but smaller, just above the hotbar slot that holds the item.
/// It lives on the mod's own overlay canvas (always drawn on top, nothing in PEAK's HUD can hide or
/// clip it) and is placed by converting the slot's position to screen space and back, so it lines up
/// whatever render mode PEAK's HUD canvas uses. Falls back to above the middle of the hotbar.
/// </summary>
internal sealed class BlowgunCooldownUI
{
    private const float FontSize = 64f;
    private const float AboveSlot = 45f; // overlay units above the slot's top edge

    private readonly RectTransform _overlay;
    private readonly RectTransform _root;
    private readonly TextMeshProUGUI _text;
    private readonly Vector3[] _corners = new Vector3[4];
    private int _lastShown = -1;
    private bool _logged;

    public bool IsValid => _root != null && _overlay != null;

    private readonly string _label;
    private readonly System.Func<bool> _onCooldown;
    private readonly System.Func<float> _secondsLeft;
    private readonly System.Func<Item, bool> _isItem;

    public BlowgunCooldownUI(Canvas overlay, TextMeshProUGUI fontStyle)
        : this(overlay, fontStyle, "Blowgun", () => BlowgunSystem.OnCooldown, () => BlowgunSystem.CooldownSecondsLeft, ItemCatalog.IsBlowgun)
    {
    }

    public BlowgunCooldownUI(Canvas overlay, TextMeshProUGUI fontStyle, string label, System.Func<bool> onCooldown,
        System.Func<float> secondsLeft, System.Func<Item, bool> isItem)
    {
        _label = label;
        _onCooldown = onCooldown;
        _secondsLeft = secondsLeft;
        _isItem = isItem;
        _overlay = (RectTransform)overlay.transform;
        var go = new GameObject("OTL_" + label + "Cooldown", typeof(RectTransform));
        _root = (RectTransform)go.transform;
        _root.SetParent(_overlay, false);
        _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
        _root.sizeDelta = new Vector2(200f, 90f);

        _text = go.AddComponent<TextMeshProUGUI>();
        _text.font = fontStyle.font;
        _text.fontSharedMaterial = fontStyle.fontSharedMaterial;
        _text.fontSize = FontSize;
        _text.alignment = TextAlignmentOptions.Center;
        _text.textWrappingMode = TextWrappingModes.NoWrap;
        _text.raycastTarget = false;
        _text.color = Color.white;
        _text.outlineWidth = 0.2f;
        _text.outlineColor = new Color32(20, 30, 45, 255);
        go.SetActive(false);
    }

    public void Update(GUIManager gui)
    {
        Character local = Character.localCharacter;
        bool show = _onCooldown() && local != null && RoleManager.IsChaser(local) && !local.data.dead;
        if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
        if (!show)
        {
            _lastShown = -1;
            return;
        }

        int slot = ItemSlotIndex();
        RectTransform? anchor = SlotRect(gui, slot) ?? SlotRect(gui, gui.items != null ? gui.items.Length / 2 : -1);
        Vector2 position = new(0f, -_overlay.rect.height * 0.5f + 220f); // bottom-centre fallback
        if (anchor != null)
        {
            anchor.GetWorldCorners(_corners); // 1 top-left, 2 top-right
            Vector3 topCenter = (_corners[1] + _corners[2]) * 0.5f;
            Canvas? slotCanvas = anchor.GetComponentInParent<Canvas>();
            Camera? cam = slotCanvas != null && slotCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? slotCanvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, topCenter);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, screen, null, out Vector2 local2))
            {
                position = local2 + new Vector2(0f, AboveSlot);
            }
        }

        _root.anchoredPosition = position;

        int seconds = Mathf.CeilToInt(_secondsLeft());
        if (seconds != _lastShown)
        {
            _lastShown = seconds;
            _text.text = seconds.ToString();
            _root.DOKill();
            _root.localScale = Vector3.one * 1.2f;
            _root.DOScale(1f, 0.2f).SetEase(Ease.OutBack); // same little pop as the head-start countdown
        }

        if (!_logged)
        {
            _logged = true;
            Plugin.Log.LogInfo($"[OTL][UI] {_label} cooldown shown (slot {slot}, above {(anchor != null ? anchor.name : "screen bottom")}, at {position}).");
        }
    }

    private static RectTransform? SlotRect(GUIManager gui, int slot) =>
        gui.items != null && slot >= 0 && slot < gui.items.Length && gui.items[slot] != null
            ? gui.items[slot].transform as RectTransform
            : null;

    private int ItemSlotIndex()
    {
        Player player = Player.localPlayer;
        if (player == null || player.itemSlots == null) return -1;
        for (int i = 0; i < player.itemSlots.Length; i++)
        {
            ItemSlot s = player.itemSlots[i];
            if (s != null && !s.IsEmpty() && s.prefab != null && _isItem(s.prefab)) return i;
        }

        return -1;
    }

    public void Destroy()
    {
        if (_root != null) Object.Destroy(_root.gameObject);
    }
}

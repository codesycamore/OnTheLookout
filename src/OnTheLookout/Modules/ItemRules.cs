using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OnTheLookout.Core;
using Peak;
using Photon.Pun;

namespace OnTheLookout.Modules;

/// <summary>
/// Rule 6 (chasers may only use healing items) and rule 11 (amulets and gems banned for everyone).
/// Enforced on the host at pickup (Item.RequestPickup runs on the master client), with a local
/// backup that blocks using a disallowed item that is already in hand.
/// </summary>
internal static class ItemRules
{
    private static readonly Dictionary<ushort, (bool Banned, bool Healing)> s_Cache = new();
    private static string s_CacheStamp = "";

    public static bool Install(Harmony harmony)
    {
        // Patch target: Item.RequestPickup(PhotonView) (prefix, [PunRPC] sent to the master client).
        // Why: the host already accepts or denies every pickup here; we add our own deny reasons.
        bool a = SafePatch.Prefix(harmony, typeof(Item), nameof(Item.RequestPickup), typeof(ItemRules), nameof(RequestPickupPrefix), "Items");

        // Patch target: CharacterItems.DoUsing() (prefix). Why: single place the local player's held item is used.
        bool b = SafePatch.Prefix(harmony, typeof(CharacterItems), "DoUsing", typeof(ItemRules), nameof(DoUsingPrefix), "Items");
        return a && b;
    }

    public static bool RequestPickupPrefix(Item __instance, PhotonView characterView)
    {
        if (!Net.IsHost || !RoundManager.IsActive || characterView == null) return true;
        Character c = characterView.GetComponent<Character>();
        if (c == null) return true;

        Item item = Effective(__instance);
        string? reason = IsBanned(item) ? "banned item"
            : RoleManager.IsChaser(c) && !IsChaserAllowed(item) ? "chasers can only use healing items"
            : null;
        if (reason is null) return true;

        Plugin.Log.LogInfo($"[OTL][Items] HOST denied {c.photonView.Owner.NickName} picking up {NameOf(item)}: {reason}.");
        __instance.view.RPC("DenyPickupRPC", characterView.Owner);
        return false;
    }

    public static bool DoUsingPrefix(CharacterItems __instance)
    {
        if (!RoundManager.IsActive) return true;
        Character c = __instance.character;
        if (c == null || !c.IsLocal || c.data.currentItem == null) return true;

        Item item = Effective(c.data.currentItem);
        return !IsBanned(item) && (!RoleManager.IsChaser(c) || IsChaserAllowed(item));
    }

    private static Item Effective(Item item) => item.isSecretlyOtherItemPrefab != null ? item.isSecretlyOtherItemPrefab : item;

    private static string NameOf(Item item) => item.UIData?.itemName ?? item.gameObject.name;

    public static bool IsBanned(Item item) => Classify(item).Banned;

    public static bool IsChaserAllowed(Item item) =>
        MatchesList(item, Plugin.ModConfig.ChaserAllowedItems.Synced())
        || (Plugin.ModConfig.ChaserAutoAllowHealing.Synced() && Classify(item).Healing);

    private static (bool Banned, bool Healing) Classify(Item item)
    {
        var cfg = Plugin.ModConfig;
        string stamp = $"{cfg.BanAmulets.Synced()}|{cfg.BanGems.Synced()}|{cfg.BannedItems.Synced()}";
        if (stamp != s_CacheStamp)
        {
            s_Cache.Clear();
            s_CacheStamp = stamp;
        }

        if (s_Cache.TryGetValue(item.itemID, out var cached)) return cached;

        bool amulet = item.GetComponentInChildren<AmuletBase>(true) != null || (item.itemTags & Item.ItemTags.ScoutAmulet) != 0;
        bool gem = item.GetComponentInChildren<Action_StrangeGem>(true) != null || item.GetComponentInChildren<Action_HealingGem>(true) != null;
        bool banned = (cfg.BanAmulets.Synced() && amulet) || (cfg.BanGems.Synced() && gem) || MatchesList(item, cfg.BannedItems.Synced());

        bool healing = item.GetComponentsInChildren<Action_ModifyStatus>(true)
                .Any(a => a.statusType == CharacterAfflictions.STATUSTYPE.Injury && a.changeAmount < 0f)
            || item.GetComponentInChildren<Action_HealingGem>(true) != null;

        var result = (banned, healing);
        s_Cache[item.itemID] = result;
        Plugin.Log.LogInfo($"[OTL][Items] classified {NameOf(item)} (id {item.itemID}, prefab {CleanName(item)}): banned={banned} healing={healing}");
        return result;
    }

    private static string CleanName(Item item) => item.gameObject.name.Replace("(Clone)", "").Trim();

    private static bool MatchesList(Item item, string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return false;
        string prefab = CleanName(item);
        string display = item.UIData?.itemName ?? "";
        return csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Any(s => s.Equals(prefab, StringComparison.OrdinalIgnoreCase) || s.Equals(display, StringComparison.OrdinalIgnoreCase));
    }
}

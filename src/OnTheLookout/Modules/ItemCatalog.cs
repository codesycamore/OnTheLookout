using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OnTheLookout.Core;
using Peak;
using UnityEngine;
using Zorro.Core;

namespace OnTheLookout.Modules;

/// <summary>
/// Classifies items for the item rules: banned (amulets, gems, blowguns, rescue claws, hidden items,
/// config list) and healing (chasers may use). Results are cached per item ID and recomputed when
/// the relevant host settings change.
///
/// "Hidden" = an item in ItemDatabase that never appears in normal play: it is in no spawn pool
/// (LootData.spawnLocations), is not produced by another legitimate item (found by scanning item
/// components' fields for Item/GameObject references, e.g. Action_ConsumeAndSpawn.itemToSpawn), and
/// isn't one of the special items the game spawns in code (backpack, flare, passport, guidebook, Bing Bong).
/// </summary>
internal static class ItemCatalog
{
    private static readonly Dictionary<ushort, (bool Banned, bool Healing)> s_Cache = new();
    private static string s_Stamp = "";
    private static HashSet<ushort>? s_Legit;

    public static Item Effective(Item item) => item.isSecretlyOtherItemPrefab != null ? item.isSecretlyOtherItemPrefab : item;

    public static string NameOf(Item item) => item.UIData?.itemName is { Length: > 0 } n ? n : CleanName(item);

    private static string CleanName(Item item) => item.gameObject.name.Replace("(Clone)", "").Trim();

    public static bool IsBanned(Item item) => Classify(Effective(item)).Banned;

    public static bool IsHealing(Item item) => Classify(Effective(item)).Healing;

    public static bool IsChaserAllowed(Item item)
    {
        Item e = Effective(item);
        return !IsBanned(e)
            && (MatchesList(e, Plugin.ModConfig.ChaserAllowedItems.Synced())
                || (Plugin.ModConfig.ChaserAutoAllowHealing.Synced() && Classify(e).Healing));
    }

    private static (bool Banned, bool Healing) Classify(Item item)
    {
        var cfg = Plugin.ModConfig;
        string stamp = string.Join("|", cfg.BanAmulets.Synced(), cfg.BanGems.Synced(), cfg.BanBlowguns.Synced(), cfg.BanRescueClaws.Synced(),
            cfg.BanHiddenItems.Synced(), cfg.AllowedHiddenItems.Synced(), cfg.BannedItems.Synced());
        if (stamp != s_Stamp)
        {
            s_Cache.Clear();
            s_Stamp = stamp;
        }

        if (s_Cache.TryGetValue(item.itemID, out var cached)) return cached;

        string? why = null;
        if (cfg.BanAmulets.Synced() && (Has<AmuletBase>(item) || (item.itemTags & Item.ItemTags.ScoutAmulet) != 0)) why = "amulet";
        else if (cfg.BanGems.Synced() && (Has<Action_StrangeGem>(item) || Has<Action_HealingGem>(item))) why = "gem";
        else if (cfg.BanBlowguns.Synced() && Has<Action_RaycastDart>(item)) why = "blowgun";
        else if (cfg.BanRescueClaws.Synced() && Has<RescueHook>(item)) why = "rescue claw";
        else if (MatchesList(item, cfg.BannedItems.Synced())) why = "BannedItems list";
        else if (cfg.BanHiddenItems.Synced() && IsHidden(item) && !MatchesList(item, cfg.AllowedHiddenItems.Synced())) why = "hidden item";

        float heal = item.GetComponentsInChildren<Action_ModifyStatus>(true)
            .Where(a => a.statusType == CharacterAfflictions.STATUSTYPE.Injury && a.changeAmount < 0f)
            .Sum(a => -a.changeAmount);
        bool healing = heal > 0f || Has<Action_HealingGem>(item);

        var result = (why != null, healing);
        s_Cache[item.itemID] = result;
        Plugin.Log.LogInfo($"[OTL][Items] {NameOf(item)} (prefab {CleanName(item)}, id {item.itemID}): banned={why ?? "no"} healing={healing} (injury -{heal:0.##})");
        return result;
    }

    private static bool Has<T>(Item item) where T : Component => item.GetComponentInChildren<T>(true) != null;

    private static bool MatchesList(Item item, string csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return false;
        string prefab = CleanName(item);
        string display = item.UIData?.itemName ?? "";
        return csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Any(s => s.Equals(prefab, StringComparison.OrdinalIgnoreCase) || s.Equals(display, StringComparison.OrdinalIgnoreCase));
    }

    // ---------- Hidden-item detection ----------

    private static bool IsHidden(Item item)
    {
        s_Legit ??= BuildLegitSet();
        return s_Legit != null && !s_Legit.Contains(item.itemID);
    }

    private static HashSet<ushort>? BuildLegitSet()
    {
        ItemDatabase db = SingletonAsset<ItemDatabase>.Instance;
        if (db == null) return null;
        List<Item> all = db.itemLookup.Values.Where(i => i != null).ToList();

        var legit = new HashSet<Item>(all.Where(i =>
            (i.GetComponent<LootData>() is { } loot && loot.spawnLocations != SpawnPool.None)
            || i is Backpack || Has<Flare>(i) || Has<Action_Passport>(i) || Has<Action_Guidebook>(i)
            || Has<Action_GuidebookScroll>(i) || (i.itemTags & Item.ItemTags.BingBong) != 0));

        // Anything a legitimate item can turn into or spawn is legitimate too (transitively).
        var queue = new Queue<Item>(legit);
        while (queue.Count > 0)
        {
            foreach (Item referenced in ReferencedItems(queue.Dequeue()))
            {
                if (legit.Add(referenced)) queue.Enqueue(referenced);
            }
        }

        List<string> hidden = all.Where(i => !legit.Contains(i)).Select(NameOf).OrderBy(n => n).ToList();
        Plugin.Log.LogInfo($"[OTL][Items] {all.Count} items in database, {hidden.Count} hidden (never spawn in normal play): {string.Join(", ", hidden)}");
        return new HashSet<ushort>(legit.Select(i => i.itemID));
    }

    private static IEnumerable<Item> ReferencedItems(Item item)
    {
        var found = new List<Item>();
        if (item.isSecretlyOtherItemPrefab != null) found.Add(item.isSecretlyOtherItemPrefab);

        foreach (MonoBehaviour component in item.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            for (Type? t = component.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    object? value;
                    try { value = field.GetValue(component); }
                    catch { continue; }
                    Collect(value, found);
                }
            }
        }

        return found.Where(i => i != item);
    }

    private static void Collect(object? value, List<Item> found)
    {
        switch (value)
        {
            case Item i when i != null:
                found.Add(i);
                break;
            case GameObject go when go != null && go.GetComponent<Item>() is { } gi:
                found.Add(gi);
                break;
            case IEnumerable list and not string:
                foreach (object? element in list)
                {
                    if (element is Item or GameObject) Collect(element, found);
                }

                break;
        }
    }
}

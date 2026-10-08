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
/// Classifies items for the item rules and finds special items by type or name.
/// - banned: amulets (incl. Scout's Honor), gems, rescue claws, hidden items, the BannedItems list;
/// - blowgun: removed from the world, but chasers may hold one (granted by <see cref="LegLoadout"/>);
/// - healing / food: what chasers may pick up and use.
/// Results are cached per item ID and recomputed when the relevant host settings change.
///
/// "Hidden" = an item in ItemDatabase that never appears in normal play: it is in no spawn pool
/// (LootData.spawnLocations), is not produced by another legitimate item (found by scanning item
/// components' fields for Item/GameObject references, e.g. Action_ConsumeAndSpawn.itemToSpawn), and
/// isn't one of the special items the game spawns in code (backpack, flare, passport, guidebook, Bing Bong).
/// </summary>
internal static class ItemCatalog
{
    private readonly struct Info
    {
        public Info(string? banReason, bool blowgun, bool healing, bool food)
        {
            BanReason = banReason;
            Blowgun = blowgun;
            Healing = healing;
            Food = food;
        }

        public string? BanReason { get; }
        public bool Blowgun { get; }
        public bool Healing { get; }
        public bool Food { get; }
    }

    private const string HiddenReason = "hidden item";

    private static readonly Dictionary<ushort, Info> s_Cache = new();
    private static string s_Stamp = "";
    private static HashSet<ushort>? s_Legit;
    private static List<Item>? s_ClownLoot;

    public static Item Effective(Item item) => item.isSecretlyOtherItemPrefab != null ? item.isSecretlyOtherItemPrefab : item;

    public static string NameOf(Item item) => item.UIData?.itemName is { Length: > 0 } n ? n : CleanName(item);

    private static string CleanName(Item item) => item.gameObject.name.Replace("(Clone)", "").Trim();

    public static bool IsBanned(Item item) => Classify(Effective(item)).BanReason != null;

    public static bool IsBlowgun(Item item) => Classify(Effective(item)).Blowgun;

    /// <summary>Removed from world spawns: banned items, and blowguns when BanBlowguns is on.</summary>
    public static bool RemoveFromWorld(Item item) =>
        IsBanned(item) || (IsBlowgun(item) && Plugin.ModConfig.BanBlowguns.Synced());

    /// <summary>
    /// Like <see cref="RemoveFromWorld"/> but ignores the "hidden item" rule. Used for scenery items
    /// (FakeItem.Awake), which run while the level loads, before the level's spawners can be scanned.
    /// </summary>
    public static bool RemoveFromWorldExplicit(Item item)
    {
        Info info = Classify(Effective(item));
        return (info.BanReason != null && info.BanReason != HiddenReason) || (info.Blowgun && Plugin.ModConfig.BanBlowguns.Synced());
    }

    /// <summary>Items named in our own settings are never treated as hidden.</summary>
    private static bool AlwaysLegit(Item item)
    {
        var cfg = Plugin.ModConfig;
        return MatchesList(item, cfg.AllowedHiddenItems.Synced()) || MatchesList(item, cfg.RunnerLegItems.Synced()) || MatchesList(item, cfg.CampfireFoodItems.Synced())
            || MatchesList(item, cfg.ChaserAllowedItems.Synced());
    }

    /// <summary>A new level has different spawners: recompute everything.</summary>
    public static void OnSceneLoaded()
    {
        s_Cache.Clear();
        s_Legit = null;
        s_ClownLoot = null;
    }

    public static bool IsChaserAllowed(Item item)
    {
        Item e = Effective(item);
        Info info = Classify(e);
        var cfg = Plugin.ModConfig;
        return info.BanReason == null
            && !MatchesList(e, cfg.ChaserForbiddenItems.Synced()) // explicit block-list beats the food/healing rules
            && (info.Blowgun
                || MatchesList(e, cfg.ChaserAllowedItems.Synced())
                || (cfg.ChaserAutoAllowHealing.Synced() && info.Healing)
                || (cfg.ChaserAutoAllowFood.Synced() && info.Food));
    }

    private static Info Classify(Item item)
    {
        var cfg = Plugin.ModConfig;
        string stamp = string.Join("|", cfg.BanAmulets.Synced(), cfg.BanGems.Synced(), cfg.BanRescueClaws.Synced(),
            cfg.BanHiddenItems.Synced(), cfg.AllowedHiddenItems.Synced(), cfg.BannedItems.Synced(), cfg.AllowJetpacks.Synced(), cfg.AllowGliders.Synced(),
            cfg.RunnerLegItems.Synced(), cfg.ChaserAllowedItems.Synced(), cfg.CampfireFoodItems.Synced(), cfg.ChaserForbiddenItems.Synced());
        if (stamp != s_Stamp)
        {
            s_Cache.Clear();
            s_ClownLoot = null;
            s_Stamp = stamp;
        }

        if (s_Cache.TryGetValue(item.itemID, out Info cached)) return cached;

        bool blowgun = Has<Action_RaycastDart>(item);
        string? why = null;
        if (cfg.BanAmulets.Synced() && (Has<AmuletBase>(item) || (item.itemTags & Item.ItemTags.ScoutAmulet) != 0)) why = "amulet";
        else if (cfg.BanGems.Synced() && (Has<Action_StrangeGem>(item) || Has<Action_HealingGem>(item))) why = "gem";
        else if (cfg.BanRescueClaws.Synced() && Has<RescueHook>(item)) why = "rescue claw";
        else if (!cfg.AllowJetpacks.Synced() && (Has<JetpackItem>(item) || Has<Rocketpack>(item) || (item is Backpack bp && bp.backpackType is BackpackSlot.BackpackType.Jetpack or BackpackSlot.BackpackType.Rocketpack))) why = "jetpack";
        else if (!cfg.AllowGliders.Synced() && Has<Glider>(item)) why = "glider";
        else if (MatchesList(item, cfg.BannedItems.Synced())) why = "BannedItems list";
        else if (!blowgun && cfg.BanHiddenItems.Synced() && IsHidden(item) && !AlwaysLegit(item)) why = HiddenReason;

        float heal = item.GetComponentsInChildren<Action_ModifyStatus>(true)
            .Where(a => a.statusType == CharacterAfflictions.STATUSTYPE.Injury && a.changeAmount < 0f)
            .Sum(a => -a.changeAmount);
        bool healing = heal > 0f || Has<Action_HealingGem>(item);

        bool food = Has<Action_RestoreHunger>(item)
            || (item.itemTags & (Item.ItemTags.PackagedFood | Item.ItemTags.Berry | Item.ItemTags.Mushroom)) != 0
            || item.GetComponentsInChildren<Action_ModifyStatus>(true).Any(a => a.statusType == CharacterAfflictions.STATUSTYPE.Hunger && a.changeAmount < 0f);

        var info = new Info(why, blowgun, healing, food);
        s_Cache[item.itemID] = info;
        Plugin.Log.LogInfo($"[OTL][Items] {NameOf(item)} (prefab {CleanName(item)}, id {item.itemID}): banned={why ?? "no"} blowgun={blowgun} healing={healing} (injury -{heal:0.##}) food={food}");
        return info;
    }

    private static bool Has<T>(Item item) where T : Component => item.GetComponentInChildren<T>(true) != null;

    private static string Normalize(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static bool NameMatches(Item item, string wanted)
    {
        string w = Normalize(wanted);
        return w.Length > 0 && (Normalize(CleanName(item)) == w || Normalize(item.UIData?.itemName ?? "") == w);
    }

    private static bool MatchesList(Item item, string csv) =>
        !string.IsNullOrWhiteSpace(csv) && csv.Split(',').Any(s => NameMatches(item, s));

    // ---------- Lookups ----------

    private static IEnumerable<Item> AllItems()
    {
        ItemDatabase db = SingletonAsset<ItemDatabase>.Instance;
        return db == null ? Enumerable.Empty<Item>() : db.itemLookup.Values.Where(i => i != null);
    }

    public static Item? Blowgun => AllItems().FirstOrDefault(i => Has<Action_RaycastDart>(i));

    /// <summary>The ordinary backpack (not a fanny pack, jetpack or rocket pack).</summary>
    public static Backpack? PlainBackpack => AllItems().OfType<Backpack>().FirstOrDefault(b => b.backpackType == BackpackSlot.BackpackType.Backpack);

    public static Flare? FlarePrefab => AllItems().Select(i => i.GetComponentInChildren<Flare>(true)).FirstOrDefault(f => f != null);

    /// <summary>Items matching a comma-separated list of prefab/display names (spaces and case ignored).</summary>
    public static List<Item> FindByNames(string csv)
    {
        LogAllNamesOnce();
        var result = new List<Item>();
        foreach (string wanted in csv.Split(','))
        {
            if (string.IsNullOrWhiteSpace(wanted)) continue;
            Item? found = Resolve(wanted);
            if (found != null) result.Add(found);
            else Plugin.Log.LogWarning($"[OTL][Items] no item named '{wanted.Trim()}' in the item database (see the item list above for exact names).");
        }

        return result;
    }

    /// <summary>
    /// Friendly names that aren't PEAK's real item names; tried (exact, then "contains") when the
    /// name itself doesn't match. PEAK's banana-like fruit is not called "Banana".
    /// </summary>
    private static readonly Dictionary<string, string[]> s_Aliases = new()
    {
        ["banana"] = new[] { "berrynana", "nana" },
    };

    private static readonly Dictionary<string, Item?> s_Resolved = new();
    private static bool s_NamesLogged;

    private static Item? Resolve(string wanted)
    {
        string key = Normalize(wanted);
        if (s_Resolved.TryGetValue(key, out Item? cached) && cached != null) return cached;

        Item? found = AllItems().FirstOrDefault(i => NameMatches(i, wanted));
        if (found == null)
        {
            var tokens = new List<string> { key };
            if (s_Aliases.TryGetValue(key, out string[] aliases)) tokens.AddRange(aliases);
            foreach (string token in tokens)
            {
                // Exact alias first, then the shortest name containing it (skipping peels/leftovers).
                found = AllItems().FirstOrDefault(i => NameMatches(i, token))
                    ?? AllItems()
                        .Where(i => (Normalize(CleanName(i)) + "|" + Normalize(i.UIData?.itemName ?? "")) is var n
                            && n.Contains(token) && !n.Contains("peel"))
                        .OrderBy(i => NameOf(i).Length)
                        .FirstOrDefault();
                if (found != null)
                {
                    Plugin.Log.LogInfo($"[OTL][Items] '{wanted.Trim()}' resolved to {NameOf(found)} (prefab {CleanName(found)}).");
                    break;
                }
            }
        }

        s_Resolved[key] = found;
        return found;
    }

    /// <summary>Once per session: every item's display and prefab name, for writing config lists.</summary>
    private static void LogAllNamesOnce()
    {
        if (s_NamesLogged) return;
        List<string> names = AllItems().Select(i => $"{NameOf(i)} [{CleanName(i)}]").Distinct().OrderBy(n => n).ToList();
        if (names.Count == 0) return;
        s_NamesLogged = true;
        Plugin.Log.LogInfo($"[OTL][Items] all items (display [prefab]): {string.Join(", ", names)}");
    }

    /// <summary>Food and healing items that spawn in normal play and aren't banned (clown luggage contents).</summary>
    public static List<Item> ClownLoot
    {
        get
        {
            s_Legit ??= BuildLegitSet();
            return s_ClownLoot ??= AllItems()
                .Where(i => s_Legit != null && s_Legit.Contains(i.itemID) && i.isSecretlyOtherItemPrefab == null)
                .Where(i => i.GetComponent<LootData>() is { } loot && loot.spawnLocations != SpawnPool.None)
                .Where(i => !RemoveFromWorld(i) && (Classify(i).Food || Classify(i).Healing) && !MatchesList(i, Plugin.ModConfig.ChaserForbiddenItems.Synced()))
                .ToList();
        }
    }

    // ---------- Hidden-item detection ----------

    private static bool IsHidden(Item item)
    {
        s_Legit ??= BuildLegitSet();
        return s_Legit != null && !s_Legit.Contains(item.itemID);
    }

    private static HashSet<ushort>? BuildLegitSet()
    {
        List<Item> all = AllItems().ToList();
        if (all.Count == 0) return null;

        var legit = new HashSet<Item>(all.Where(i =>
            (i.GetComponent<LootData>() is { } loot && loot.spawnLocations != SpawnPool.None)
            || i is Backpack || Has<Flare>(i) || Has<Action_Passport>(i) || Has<Action_Guidebook>(i)
            || Has<Action_GuidebookScroll>(i) || (i.itemTags & Item.ItemTags.BingBong) != 0));

        // Items placed by the level itself: single-item spawners (Spawner.spawnedObjectPrefab, e.g. the
        // Roots shelf shrooms, snow piles, berry bushes) and any other spawner-like component.
        int sceneRefs = 0;
        foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (component == null || (component is not Spawner && component.GetType().Name.IndexOf("Spawn", StringComparison.Ordinal) < 0)) continue;
            foreach (Item referenced in FieldReferences(component))
            {
                if (legit.Add(referenced)) sceneRefs++;
            }
        }

        Plugin.Log.LogInfo($"[OTL][Items] {sceneRefs} item type(s) are placed by this level's spawners.");

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
            if (component != null) found.AddRange(FieldReferences(component));
        }

        return found.Where(i => i != item);
    }

    /// <summary>Item prefabs referenced by any field of a component (Item, GameObject, or lists of them).</summary>
    private static List<Item> FieldReferences(MonoBehaviour component)
    {
        var found = new List<Item>();
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

        return found;
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

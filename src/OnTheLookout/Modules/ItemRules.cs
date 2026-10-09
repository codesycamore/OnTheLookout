using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Item rules, enforced in layers so nothing slips through:
/// - banned items (amulets, gems, rescue claws, hidden items) and world blowguns are removed when the
///   host spawns them, hidden when they are "fake" scenery items, and can't be picked up or used;
/// - during a round chasers can only pick up and use food, healing items and their blowgun;
///   the blowgun is chasers-only and has a cooldown (<see cref="BlowgunSystem"/>);
/// - clown luggage can only be opened by chasers and contains food and healing items.
/// PEAK has two pickup paths: real items (Item.RequestPickup) and lightweight scenery items
/// (FakeItem -> FakeItemManager.RPC_RequestFakeItemPickup, which adds straight to the inventory);
/// both are covered.
/// </summary>
internal static class ItemRules
{
    /// <summary>Raised locally when the local player is refused an item (for a HUD hint).</summary>
    public static event Action<string>? LocalDenied;

    private static float s_NextDenyHint;

    public static bool Install(Harmony harmony)
    {
        const string m = "Items";
        bool ok = true;
        Type t = typeof(ItemRules);

        // Spawner.SpawnItems (postfix, host): luggage, statues and world spawners spawn through here.
        ok &= SafePatch.Postfix(harmony, typeof(Spawner), nameof(Spawner.SpawnItems), t, nameof(SpawnItemsPostfix), m);
        // Spawner.GetObjectsToSpawn (postfix, host): what a spawner is about to spawn (clown luggage contents).
        ok &= SafePatch.Postfix(harmony, typeof(Spawner), "GetObjectsToSpawn", t, nameof(GetObjectsToSpawnPostfix), m);

        // Real items: hover prompt, client pickup, host pickup, use.
        ok &= SafePatch.Prefix(harmony, typeof(Item), nameof(Item.IsInteractible), t, nameof(IsInteractiblePrefix), m);
        ok &= SafePatch.Prefix(harmony, typeof(Item), nameof(Item.Interact), t, nameof(InteractPrefix), m);
        ok &= SafePatch.Prefix(harmony, typeof(Item), nameof(Item.RequestPickup), t, nameof(RequestPickupPrefix), m);
        foreach (string method in new[] { nameof(Item.StartUsePrimary), nameof(Item.ContinueUsePrimary), nameof(Item.StartUseSecondary), nameof(Item.ContinueUseSecondary) })
        {
            ok &= SafePatch.Prefix(harmony, typeof(Item), method, t, nameof(UsePrefix), m);
        }

        // Fake (scenery) items: hide banned ones, hover prompt, client pickup, host pickup.
        ok &= SafePatch.Postfix(harmony, typeof(FakeItem), "Awake", t, nameof(FakeAwakePostfix), m);
        ok &= SafePatch.Prefix(harmony, typeof(FakeItem), nameof(FakeItem.IsInteractible), t, nameof(FakeIsInteractiblePrefix), m);
        ok &= SafePatch.Prefix(harmony, typeof(FakeItem), nameof(FakeItem.Interact), t, nameof(FakeInteractPrefix), m);
        ok &= SafePatch.Prefix(harmony, typeof(FakeItemManager), nameof(FakeItemManager.RPC_RequestFakeItemPickup), t, nameof(FakePickupPrefix), m);

        // Clown luggage: chasers only.
        ok &= SafePatch.Prefix(harmony, typeof(Luggage), nameof(Luggage.IsInteractible), t, nameof(LuggageInteractiblePrefix), m);
        ok &= SafePatch.Prefix(harmony, typeof(Luggage), nameof(Luggage.IsConstantlyInteractable), t, nameof(LuggageInteractiblePrefix), m);
        ok &= SafePatch.Prefix(harmony, typeof(Luggage), nameof(Luggage.Interact_CastFinished), t, nameof(LuggageCastFinishedPrefix), m);
        return ok;
    }

    /// <summary>Why <paramref name="c"/> may not have <paramref name="item"/>, or null if allowed.</summary>
    private static string? Refusal(Character? c, Item item)
    {
        // The chaser gem is a chaser kit item: allowed for chasers even though amulets are banned, never for anyone else.
        if (ChaserKit.IsGem(item)) return RoleManager.IsChaser(c) && Plugin.ModConfig.ChaserGem.Synced() ? null : "Only chasers can use this gem";
        if (ItemCatalog.IsBanned(item)) return "This item is banned";
        if (ItemCatalog.IsBlowgun(item)) return RoleManager.IsChaser(c) ? null : "Only chasers can use the blowgun";
        if (RoleManager.IsChaser(c) && !ItemCatalog.IsChaserAllowed(item)) return "Chasers can only use food and healing items";
        return null;
    }

    /// <summary>
    /// <see cref="Refusal"/> plus, for picking up: a chaser carries one blowgun and one chaser gem at most (a second
    /// would do nothing; both cooldowns belong to the player, not the item).
    /// </summary>
    private static string? PickupRefusal(Character? c, Item item)
    {
        string? why = Refusal(c, item);
        if (why != null || c == null || c.player == null || !RoleManager.IsChaser(c)) return why;
        Item e = ItemCatalog.Effective(item);
        if (ItemCatalog.IsBlowgun(e) && c.player.HasInAnySlot(e.itemID)) return "You already have a blowgun";
        if (ChaserKit.IsGem(e) && c.player.HasInAnySlot(e.itemID)) return "You already have the gem";
        return null;
    }

    // ---------- World spawns ----------

    public static void SpawnItemsPostfix(List<PhotonView> __result)
    {
        if (!Net.IsHost || __result == null) return;
        for (int i = __result.Count - 1; i >= 0; i--)
        {
            PhotonView view = __result[i];
            if (view == null || view.GetComponent<Item>() is not { } item || !ItemCatalog.RemoveFromWorld(item)) continue;
            Plugin.Log.LogInfo($"[OTL][Items] HOST removed spawned {ItemCatalog.NameOf(item)}.");
            PhotonNetwork.Destroy(view.gameObject);
            __result.RemoveAt(i);
        }
    }

    public static void GetObjectsToSpawnPostfix(Spawner __instance, ref List<GameObject> __result)
    {
        if (__result == null || __instance is not Luggage luggage || luggage is RespawnChest) return;
        if (!IsClownLuggage(luggage))
        {
            AddLuggageExtras(__result);
            return;
        }

        if (!Plugin.ModConfig.ClownLuggageChasersOnly.Synced()) return;
        List<Item> loot = ItemCatalog.ClownLoot;
        if (loot.Count == 0) return;
        for (int i = 0; i < __result.Count; i++)
        {
            __result[i] = loot[UnityEngine.Random.Range(0, loot.Count)].gameObject;
        }

        Plugin.Log.LogInfo($"[OTL][Items] clown luggage filled with {__result.Count} food/healing item(s).");
    }

    /// <summary>
    /// Regular (non-clown) luggage during a round: each item it is about to spawn has a LuggageExtraChance chance
    /// to be one of LuggageExtraItems instead (fortified milk, snowball, brown berrynana by default), every leg.
    /// </summary>
    private static void AddLuggageExtras(List<GameObject> spawns)
    {
        var cfg = Plugin.ModConfig;
        float chance = Mathf.Clamp01(cfg.LuggageExtraChance.Synced());
        if (!RoundManager.IsActive || chance <= 0f || spawns.Count == 0) return;
        List<Item> extras = ItemCatalog.FindByNames(cfg.LuggageExtraItems.Synced());
        if (extras.Count == 0) return;

        int swapped = 0;
        for (int i = 0; i < spawns.Count; i++)
        {
            if (UnityEngine.Random.value >= chance) continue;
            spawns[i] = extras[UnityEngine.Random.Range(0, extras.Count)].gameObject;
            swapped++;
        }

        if (swapped > 0) Plugin.Log.LogInfo($"[OTL][Items] luggage: {swapped} of {spawns.Count} item(s) swapped for runner extras.");
    }

    // ---------- Real items ----------

    public static bool IsInteractiblePrefix(Item __instance, Character interactor, ref bool __result)
    {
        if (PickupRefusal(interactor, __instance) is null) return true;
        __result = false;
        return false;
    }

    public static bool InteractPrefix(Item __instance, Character interactor)
    {
        string? why = PickupRefusal(interactor, __instance);
        if (why is null) return true;
        if (interactor != null && interactor.IsLocal) Hint(why);
        return false;
    }

    public static bool RequestPickupPrefix(Item __instance, PhotonView characterView)
    {
        if (!Net.IsHost || characterView == null) return true;
        string? why = PickupRefusal(characterView.GetComponent<Character>(), __instance);
        if (why is null) return true;

        Plugin.Log.LogInfo($"[OTL][Items] HOST denied {characterView.Owner?.NickName} picking up {ItemCatalog.NameOf(__instance)}: {why}.");
        __instance.view.RPC("DenyPickupRPC", characterView.Owner);
        return false;
    }

    public static bool UsePrefix(Item __instance)
    {
        Character holder = __instance.holderCharacter;
        if (holder == null || !holder.IsLocal) return true;
        if (ChaserKit.HandleUse(__instance)) return false; // the chaser gem: a boost instead of its vanilla power
        string? why = Refusal(holder, __instance);
        if (why is null && ItemCatalog.IsBlowgun(__instance) && BlowgunSystem.OnCooldown) why = "Blowgun is recharging";
        if (why is null) return true;
        Hint(why);
        return false;
    }

    // ---------- Fake (scenery) items ----------

    public static void FakeAwakePostfix(FakeItem __instance)
    {
        if (__instance.realItemPrefab != null && ItemCatalog.RemoveFromWorldExplicit(__instance.realItemPrefab))
        {
            __instance.gameObject.SetActive(false);
        }
    }

    public static bool FakeIsInteractiblePrefix(FakeItem __instance, Character interactor, ref bool __result)
    {
        if (__instance.realItemPrefab == null || PickupRefusal(interactor, __instance.realItemPrefab) is null) return true;
        __result = false;
        return false;
    }

    public static bool FakeInteractPrefix(FakeItem __instance, Character interactor)
    {
        string? why = __instance.realItemPrefab != null ? PickupRefusal(interactor, __instance.realItemPrefab) : null;
        if (why is null) return true;
        if (interactor != null && interactor.IsLocal) Hint(why);
        return false;
    }

    public static bool FakePickupPrefix(FakeItemManager __instance, PhotonView characterView, int fakeItemIndex)
    {
        if (!Net.IsHost || characterView == null || !__instance.TryGetFakeItem(fakeItemIndex, out FakeItem fake) || fake.realItemPrefab == null) return true;
        string? why = PickupRefusal(characterView.GetComponent<Character>(), fake.realItemPrefab);
        if (why is null) return true;

        Plugin.Log.LogInfo($"[OTL][Items] HOST denied {characterView.Owner?.NickName} picking up {ItemCatalog.NameOf(fake.realItemPrefab)} (scenery item): {why}.");
        __instance.photonView.RPC("RPC_DenyFakeItemPickup", characterView.Owner, fakeItemIndex);
        return false;
    }

    // ---------- Clown luggage ----------

    /// <summary>PEAK tags clown luggage "ClownLuggage" (see AchievementManager); the spawn pool is a fallback.</summary>
    private static bool IsClownLuggage(Luggage luggage) =>
        luggage is not RespawnChest
        && (luggage.CompareTag("ClownLuggage") || (luggage.GetSpawnPool() & SpawnPool.LuggageClown) != 0);

    /// <summary>Why <paramref name="interactor"/> may not open <paramref name="luggage"/>, or null.</summary>
    private static string? LuggageRefusal(Luggage luggage, Character interactor)
    {
        if (!RoundManager.IsActive) return null;
        // Scout statues (RespawnChest) neither revive nor give items during a round: everyone is brought
        // back at the campfire after each leg instead.
        if (luggage is RespawnChest) return Plugin.ModConfig.DisableScoutStatues.Synced() ? "Scout statues are disabled in this mode" : null;
        var cfg = Plugin.ModConfig;
        bool chaser = RoleManager.IsChaser(interactor);
        bool clown = IsClownLuggage(luggage);
        if (clown && !chaser && cfg.ClownLuggageChasersOnly.Synced()) return "Only chasers can open clown luggage";
        if (!clown && chaser && cfg.ChasersOnlyOpenClownLuggage.Synced()) return "Chasers can only open clown luggage";
        return null;
    }

    public static bool LuggageInteractiblePrefix(Luggage __instance, Character interactor, ref bool __result)
    {
        if (LuggageRefusal(__instance, interactor) is null) return true;
        __result = false;
        return false;
    }

    public static bool LuggageCastFinishedPrefix(Luggage __instance, Character interactor)
    {
        string? why = LuggageRefusal(__instance, interactor);
        if (why is null) return true;
        if (interactor != null && interactor.IsLocal) Hint(why);
        return false;
    }

    private static void Hint(string why)
    {
        if (Time.time < s_NextDenyHint) return;
        s_NextDenyHint = Time.time + 2f;
        LocalDenied?.Invoke(why);
    }
}

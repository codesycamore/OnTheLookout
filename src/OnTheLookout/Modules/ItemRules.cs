using System;
using System.Collections.Generic;
using HarmonyLib;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Item rules, enforced in layers so nothing slips through:
/// - banned items (amulets, gems, blowguns, rescue claws, hidden items) are destroyed when the host
///   spawns them, and can't be picked up or used by anyone, round or not;
/// - during a round, chasers can only pick up and use healing items: no hover prompt, no pickup
///   (client side and host side), no use.
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

        // Patch target: Spawner.SpawnItems(List<Transform>) (postfix, host). Why: luggage, statues and world
        // spawners all spawn their items through here; banned ones are destroyed right away.
        ok &= SafePatch.Postfix(harmony, typeof(Spawner), nameof(Spawner.SpawnItems), typeof(ItemRules), nameof(SpawnItemsPostfix), m);

        // Patch target: Item.IsInteractible(Character) (prefix). Why: hides the pickup prompt for forbidden items.
        ok &= SafePatch.Prefix(harmony, typeof(Item), nameof(Item.IsInteractible), typeof(ItemRules), nameof(IsInteractiblePrefix), m);

        // Patch target: Item.Interact(Character) (prefix). Why: client-side pickup; refuse before a request is sent.
        ok &= SafePatch.Prefix(harmony, typeof(Item), nameof(Item.Interact), typeof(ItemRules), nameof(InteractPrefix), m);

        // Patch target: Item.RequestPickup(PhotonView) (prefix, [PunRPC] on the master client).
        // Why: the host accepts or denies every pickup here; authoritative backstop.
        ok &= SafePatch.Prefix(harmony, typeof(Item), nameof(Item.RequestPickup), typeof(ItemRules), nameof(RequestPickupPrefix), m);

        // Patch targets: Item.StartUsePrimary/ContinueUsePrimary/StartUseSecondary/ContinueUseSecondary (prefixes).
        // Why: every item use (eat, drink, apply, shoot) starts here; blocks items already in hand.
        foreach (string method in new[] { nameof(Item.StartUsePrimary), nameof(Item.ContinueUsePrimary), nameof(Item.StartUseSecondary), nameof(Item.ContinueUseSecondary) })
        {
            ok &= SafePatch.Prefix(harmony, typeof(Item), method, typeof(ItemRules), nameof(UsePrefix), m);
        }

        return ok;
    }

    /// <summary>Why <paramref name="c"/> may not have <paramref name="item"/>, or null if allowed.</summary>
    private static string? Refusal(Character? c, Item item)
    {
        if (ItemCatalog.IsBanned(item)) return "This item is banned";
        if (RoleManager.IsChaser(c) && !ItemCatalog.IsChaserAllowed(item)) return "Chasers can only use healing items";
        return null;
    }

    public static void SpawnItemsPostfix(List<PhotonView> __result)
    {
        if (!Net.IsHost || __result == null) return;
        for (int i = __result.Count - 1; i >= 0; i--)
        {
            PhotonView view = __result[i];
            if (view == null || view.GetComponent<Item>() is not { } item || !ItemCatalog.IsBanned(item)) continue;
            Plugin.Log.LogInfo($"[OTL][Items] HOST removed spawned {ItemCatalog.NameOf(item)}.");
            PhotonNetwork.Destroy(view.gameObject);
            __result.RemoveAt(i);
        }
    }

    public static bool IsInteractiblePrefix(Item __instance, Character interactor, ref bool __result)
    {
        if (Refusal(interactor, __instance) is null) return true;
        __result = false;
        return false;
    }

    public static bool InteractPrefix(Item __instance, Character interactor)
    {
        string? why = Refusal(interactor, __instance);
        if (why is null) return true;
        if (interactor != null && interactor.IsLocal) Hint(why);
        return false;
    }

    public static bool RequestPickupPrefix(Item __instance, PhotonView characterView)
    {
        if (!Net.IsHost || characterView == null) return true;
        Character c = characterView.GetComponent<Character>();
        string? why = Refusal(c, __instance);
        if (why is null) return true;

        Plugin.Log.LogInfo($"[OTL][Items] HOST denied {characterView.Owner?.NickName} picking up {ItemCatalog.NameOf(__instance)}: {why}.");
        __instance.view.RPC("DenyPickupRPC", characterView.Owner);
        return false;
    }

    public static bool UsePrefix(Item __instance)
    {
        Character holder = __instance.holderCharacter;
        if (holder == null || !holder.IsLocal) return true;
        string? why = Refusal(holder, __instance);
        if (why is null) return true;
        Hint(why);
        return false;
    }

    private static void Hint(string why)
    {
        if (Time.time < s_NextDenyHint) return;
        s_NextDenyHint = Time.time + 2f;
        LocalDenied?.Invoke(why);
    }
}

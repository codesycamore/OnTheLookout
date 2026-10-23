using System.Collections.Generic;
using HarmonyLib;
using OnTheLookout.Core;
using Peak.Afflictions;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Fortified milk is heavier and its invincibility is shorter:
/// - its weight (Item.CarryWeight, which feeds the Weight status) is multiplied by FortifiedMilkWeightMultiplier;
/// - the invincibility it gives (Affliction_Invincibility with isFromMilk) lasts FortifiedMilkInvincibilityMultiplier
///   of its vanilla time. That also shortens the capture protection milk gives runners.
/// The energy drink keeps its vanilla length (only its drowsiness afterwards is scaled: <see cref="Tweaks"/>).
/// </summary>
internal static class MilkRules
{
    private static readonly Dictionary<ushort, bool> s_IsMilk = new();

    public static bool Install(Harmony harmony)
    {
        const string m = "Milk";
        bool ok = true;

        // Item.CarryWeight getter (postfix). Why: CharacterAfflictions.UpdateWeight sums this for every carried item.
        ok &= SafePatch.Postfix(harmony, typeof(Item), "get_CarryWeight", typeof(MilkRules), nameof(CarryWeightPostfix), m);

        // CharacterAfflictions.AddAffliction(Affliction, bool) (postfix). Why: it stores a copy of the incoming
        // affliction (or stacks it, resetting the time); we shorten the stored copy, never the item's own data.
        ok &= SafePatch.Postfix(harmony, typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddAffliction), typeof(MilkRules), nameof(AddAfflictionPostfix), m);
        return ok;
    }

    private static bool IsMilk(Item item)
    {
        if (s_IsMilk.TryGetValue(item.itemID, out bool cached)) return cached;
        bool milk = ItemCatalog.NameOf(item).Replace(" ", "").IndexOf("fortifiedmilk", System.StringComparison.OrdinalIgnoreCase) >= 0
            || item.gameObject.name.IndexOf("FortifiedMilk", System.StringComparison.OrdinalIgnoreCase) >= 0;
        return s_IsMilk[item.itemID] = milk;
    }

    public static void CarryWeightPostfix(Item __instance, ref int __result)
    {
        if (__result <= 0) return;
        if (IsMilk(__instance)) __result = Mathf.RoundToInt(__result * Mathf.Max(0f, Plugin.ModConfig.FortifiedMilkWeightMultiplier.Synced()));
        else if (RoundManager.IsActive && IsFaerieLantern(__instance))
        {
            __result = Mathf.RoundToInt(__result * Mathf.Max(0f, Plugin.ModConfig.FaerieLanternWeightMultiplier.Synced()));
        }
        else if (RoundManager.IsActive && IsLantern(__instance))
        {
            __result = Mathf.RoundToInt(__result * Mathf.Max(0f, Plugin.ModConfig.LanternWeightMultiplier.Synced()));
        }
    }

    private static readonly Dictionary<ushort, bool> s_IsLantern = new();
    private static readonly Dictionary<ushort, bool> s_IsPlainLantern = new();

    /// <summary>A regular lantern (prefab "Lantern"; the faerie lantern is Lantern_Faerie).</summary>
    private static bool IsLantern(Item item)
    {
        if (s_IsPlainLantern.TryGetValue(item.itemID, out bool cached)) return cached;
        return s_IsPlainLantern[item.itemID] = ItemCatalog.MatchesNames(ItemCatalog.Effective(item), "Lantern");
    }

    private static bool IsFaerieLantern(Item item)
    {
        if (s_IsLantern.TryGetValue(item.itemID, out bool cached)) return cached;
        return s_IsLantern[item.itemID] = ItemCatalog.MatchesNames(ItemCatalog.Effective(item), "Lantern_Faerie");
    }

    /// <summary>Set while the mod adds its own milk-like invincibility (the runners' head start), which keeps its full length.</summary>
    public static bool KeepLength;

    public static void AddAfflictionPostfix(CharacterAfflictions __instance, Affliction affliction)
    {
        if (KeepLength || __instance.character == null || !__instance.character.IsLocal) return;
        if (affliction is Affliction_Invincibility { isFromMilk: true })
        {
            Shorten<Affliction_Invincibility>(__instance, affliction, Plugin.ModConfig.FortifiedMilkInvincibilityMultiplier.Synced());
        }
    }

    private static void Shorten<T>(CharacterAfflictions afflictions, Affliction incoming, float multiplier) where T : Affliction
    {
        multiplier = Mathf.Clamp01(multiplier);
        foreach (Affliction stored in afflictions.afflictionList)
        {
            if (stored is T) stored.totalTime = Mathf.Min(stored.totalTime, stored.timeElapsed + incoming.totalTime * multiplier);
        }
    }
}

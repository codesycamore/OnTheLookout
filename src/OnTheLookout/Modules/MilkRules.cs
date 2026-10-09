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
/// Same idea for the energy drink: its speed boost (Affliction_FasterBoi) lasts EnergyDrinkDurationMultiplier of its time.
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
        if (__result <= 0 || !IsMilk(__instance)) return;
        __result = Mathf.RoundToInt(__result * Mathf.Max(0f, Plugin.ModConfig.FortifiedMilkWeightMultiplier.Synced()));
    }

    public static void AddAfflictionPostfix(CharacterAfflictions __instance, Affliction affliction)
    {
        if (__instance.character == null || !__instance.character.IsLocal) return;
        if (affliction is Affliction_Invincibility { isFromMilk: true })
        {
            Shorten<Affliction_Invincibility>(__instance, affliction, Plugin.ModConfig.FortifiedMilkInvincibilityMultiplier.Synced());
        }
        else if (affliction is Affliction_FasterBoi)
        {
            // The energy drink's speed boost (shroomberries' FasterBoi is already cut to 1 s by ShroomberryRules).
            Shorten<Affliction_FasterBoi>(__instance, affliction, Plugin.ModConfig.EnergyDrinkDurationMultiplier.Synced());
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

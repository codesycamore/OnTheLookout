using System.Collections;
using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Shroomberries can be eaten, but their effects only last ShroomberryEffectSeconds (default 1 s); the
/// hunger they cure is untouched (vanilla). Mushroom-style effects are applied by the eater's own client,
/// some right away and some after a short delay (Action_RandomMushroomEffect waits 3 s), so for a few
/// seconds after eating or being fed a shroomberry everything that hits the local player is shortened:
/// - timed effects (speed, low gravity, invincibility, blind, numb, ...) end ShroomberryEffectSeconds after they start;
/// - knock-downs (Character.Fall) last at most ShroomberryEffectSeconds;
/// - negative statuses it adds (spores, poison, drowsy, ...) are taken away again after ShroomberryEffectSeconds.
/// Hunger and weight are never touched.
/// </summary>
internal static class ShroomberryRules
{
    private const float Window = 5f; // covers the 3 s delay of delayed mushroom effects

    private static float s_WindowUntil = -1f;

    private static bool Active => Time.time < s_WindowUntil;

    private static float EffectSeconds => Mathf.Max(0f, Plugin.ModConfig.ShroomberryEffectSeconds.Synced());

    public static bool Install(Harmony harmony)
    {
        const string m = "Shroomberry";
        bool ok = true;

        // Eating (primary) or feeding (secondary) finishes here, before the item's actions run.
        ok &= SafePatch.Prefix(harmony, typeof(Item), "FinishCastPrimary", typeof(ShroomberryRules), nameof(FinishCastPrefix), m);
        ok &= SafePatch.Prefix(harmony, typeof(Item), nameof(Item.FinishCastSecondary), typeof(ShroomberryRules), nameof(FinishCastPrefix), m);
        GlobalEvents.OnItemConsumed += (item, eater) =>
        {
            if (eater != null && eater.IsLocal && IsShroomberry(item)) Open();
        };

        // CharacterAfflictions.AddAffliction(Affliction, bool) (postfix). Why: it stores a copy of the
        // effect in afflictionList; we shorten that copy (never the item's own data).
        ok &= SafePatch.Postfix(harmony, typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddAffliction), typeof(ShroomberryRules), nameof(AddAfflictionPostfix), m);

        // Character.Fall(float seconds, float) (prefix). Why: knock-down length.
        ok &= SafePatch.Prefix(harmony, typeof(Character), nameof(Character.Fall), typeof(ShroomberryRules), nameof(FallPrefix), m);

        // CharacterAfflictions.AddStatus (postfix). Why: negative statuses from the berry are removed again shortly after.
        ok &= SafePatch.Postfix(harmony, typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus), typeof(ShroomberryRules), nameof(AddStatusPostfix), m);
        return ok;
    }

    private static bool IsShroomberry(Item? item) =>
        item != null && ItemCatalog.NameOf(ItemCatalog.Effective(item)).Replace(" ", "").IndexOf("shroomberry", System.StringComparison.OrdinalIgnoreCase) >= 0;

    private static void Open() => s_WindowUntil = Time.time + Window;

    public static void FinishCastPrefix(Item __instance)
    {
        if (IsShroomberry(__instance) && __instance.holderCharacter != null && __instance.holderCharacter.IsLocal) Open();
    }

    public static void AddAfflictionPostfix(CharacterAfflictions __instance, Peak.Afflictions.Affliction affliction)
    {
        if (!Active || affliction == null || __instance.character == null || !__instance.character.IsLocal) return;
        foreach (Peak.Afflictions.Affliction stored in __instance.afflictionList)
        {
            if (stored.GetAfflictionType() != affliction.GetAfflictionType()) continue;
            stored.totalTime = Mathf.Min(stored.totalTime, stored.timeElapsed + EffectSeconds);
        }
    }

    public static void FallPrefix(Character __instance, ref float seconds)
    {
        if (Active && __instance.IsLocal) seconds = Mathf.Min(seconds, EffectSeconds);
    }

    public static void AddStatusPostfix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, bool fromRPC)
    {
        if (!Active || fromRPC || amount <= 0f || statusType is CharacterAfflictions.STATUSTYPE.Hunger or CharacterAfflictions.STATUSTYPE.Weight) return;
        if (__instance.character == null || !__instance.character.IsLocal) return;
        ModNetwork.Instance?.StartCoroutine(TakeBackLater(__instance, statusType, amount));
    }

    private static IEnumerator TakeBackLater(CharacterAfflictions afflictions, CharacterAfflictions.STATUSTYPE statusType, float amount)
    {
        yield return new WaitForSeconds(EffectSeconds);
        if (afflictions != null) afflictions.SubtractStatus(statusType, amount);
    }
}

using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Small rule tweaks: runner stamina regen, no Scoutmaster, no revival curse, energy drink drowsiness, slower hunger
/// (none for runners in a safe zone).
/// </summary>
internal static class Tweaks
{
    public static bool Install(Harmony harmony)
    {
        const string m = "Tweaks";
        bool ok = true;

        // Character.CanRegenStamina() (postfix, private). Why: PEAK regenerates
        // AddStamina(fixedDeltaTime * 0.2) whenever this returns true (Character.UpdateVariablesFixed);
        // runners get the extra percentage on top.
        ok &= SafePatch.Postfix(harmony, typeof(Character), "CanRegenStamina", typeof(Tweaks), nameof(RegenPostfix), m);

        // ScoutmasterSpawner.SpawnScoutmaster() (prefix, private). Why: the host spawns him here at scene load.
        ok &= SafePatch.Prefix(harmony, typeof(ScoutmasterSpawner), "SpawnScoutmaster", typeof(Tweaks), nameof(SpawnScoutmasterPrefix), m);
        // Scoutmaster.SetCurrentTarget(Character, float) (prefix). Why: belt and braces if he exists anyway.
        ok &= SafePatch.Prefix(harmony, typeof(Scoutmaster), nameof(Scoutmaster.SetCurrentTarget), typeof(Tweaks), nameof(SetTargetPrefix), m);

        // Character.ApplyPostReviveStatus(CharacterAfflictions) (prefix, static). Why: adds the revival curse and hunger.
        ok &= SafePatch.Prefix(harmony, typeof(Character), nameof(Character.ApplyPostReviveStatus), typeof(Tweaks), nameof(PostReviveStatusPrefix), m);

        // Affliction_FasterBoi.OnRemoved() (prefix + postfix). Why: the energy drink's speed boost adds
        // drowsyOnEnd drowsiness when it wears off; scaled for the call, then restored.
        ok &= SafePatch.Prefix(harmony, typeof(Peak.Afflictions.Affliction_FasterBoi), nameof(Peak.Afflictions.Affliction_FasterBoi.OnRemoved), typeof(Tweaks), nameof(EnergyDrinkEndPrefix), m);
        ok &= SafePatch.Postfix(harmony, typeof(Peak.Afflictions.Affliction_FasterBoi), nameof(Peak.Afflictions.Affliction_FasterBoi.OnRemoved), typeof(Tweaks), nameof(EnergyDrinkEndPostfix), m);

        // CharacterAfflictions.UpdateNormalStatuses() (prefix + postfix, private). Why: the per-frame vanilla hunger tick
        // (AddStatus(Hunger, deltaTime * hungerPerSecond * Ascents.hungerRateMultiplier)) happens in here; the flag lets
        // the AddStatus prefix below scale that tick only, not food or other hunger sources.
        ok &= SafePatch.Prefix(harmony, typeof(CharacterAfflictions), "UpdateNormalStatuses", typeof(Tweaks), nameof(NormalStatusesPrefix), m);
        ok &= SafePatch.Postfix(harmony, typeof(CharacterAfflictions), "UpdateNormalStatuses", typeof(Tweaks), nameof(NormalStatusesPostfix), m);
        // CharacterAfflictions.AddStatus(STATUSTYPE, float, ...) (prefix). Why: scales the hunger tick above.
        ok &= SafePatch.Prefix(harmony, typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus), typeof(Tweaks), nameof(HungerTickPrefix), m);

        // Interaction.DoInteractableRaycasts(out IInteractible, ...) (prefix). Why: finds what the player
        // can interact with each frame; returning nothing blocks every interaction and prompt.
        ok &= SafePatch.Prefix(harmony, typeof(Interaction), nameof(Interaction.DoInteractableRaycasts), typeof(Tweaks), nameof(InteractRaycastPrefix), m);
        return ok;
    }

    private static float EnergyDrinkMultiplier =>
        RoundManager.IsActive ? Mathf.Max(0f, Plugin.ModConfig.EnergyDrinkDrowsyMultiplier.Synced()) : 1f;

    public static void EnergyDrinkEndPrefix(Peak.Afflictions.Affliction_FasterBoi __instance) =>
        __instance.drowsyOnEnd *= EnergyDrinkMultiplier;

    public static void EnergyDrinkEndPostfix(Peak.Afflictions.Affliction_FasterBoi __instance)
    {
        float m = EnergyDrinkMultiplier;
        if (m > 0f) __instance.drowsyOnEnd /= m;
    }

    private static bool s_InStatusTick;

    public static void NormalStatusesPrefix() => s_InStatusTick = true;

    public static void NormalStatusesPostfix() => s_InStatusTick = false;

    /// <summary>
    /// During a round the vanilla hunger tick (already scaled by the ascent) grows at HungerRateMultiplier of its
    /// rate, and not at all for a runner standing in a safe zone.
    /// </summary>
    public static void HungerTickPrefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount)
    {
        if (!s_InStatusTick || statusType != CharacterAfflictions.STATUSTYPE.Hunger || amount <= 0f || !RoundManager.IsActive) return;
        Character c = __instance.character;
        if (c == null || !c.IsLocal) return;
        amount = RoleManager.IsRunner(c) && SafeZoneSystem.IsSafe(c.Center)
            ? 0f
            : amount * Mathf.Max(0f, Plugin.ModConfig.HungerRateMultiplier.Synced());
    }

    public static bool InteractRaycastPrefix(ref IInteractible interactableResult)
    {
        if (!RoundManager.InSpawnLock) return true;
        interactableResult = null!;
        return false;
    }

    public static void RegenPostfix(Character __instance, bool __result)
    {
        if (!__result || !__instance.IsLocal || !RoleManager.IsRunner(__instance)) return;
        float extra = Plugin.ModConfig.RunnerStaminaRegenMultiplier.Synced() - 1f;
        if (extra > 0f) __instance.AddStamina(Time.fixedDeltaTime * 0.2f * extra);
    }

    public static bool SpawnScoutmasterPrefix()
    {
        if (!Plugin.ModConfig.DisableScoutmaster.Synced()) return true;
        Plugin.Log.LogInfo("[OTL][Tweaks] Scoutmaster disabled.");
        return false;
    }

    public static bool SetTargetPrefix(Character setCurrentTarget) =>
        setCurrentTarget == null || !Plugin.ModConfig.DisableScoutmaster.Synced();

    public static bool PostReviveStatusPrefix() =>
        !(RoundManager.IsActive && Plugin.ModConfig.NoReviveCurse.Synced());
}

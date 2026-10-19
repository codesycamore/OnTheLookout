using HarmonyLib;
using Photon.Pun;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Small rule tweaks: runner stamina regen, no Scoutmaster, no revival curse, energy drink drowsiness, slower hunger
/// (none for runners in a safe zone), no flare at the peak on any ascent.
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

        // Ascents.shouldSpawnFlare (getter, postfix). Why: PEAK's peak flare (Peak.EndgameFlareSpawner and
        // DestroyFlareDuringAscentChallenge) only exists while this is true (below Ascent 4, or a custom run's setting).
        ok &= SafePatch.Postfix(harmony, typeof(Ascents), "get_shouldSpawnFlare", typeof(Tweaks), nameof(ShouldSpawnFlarePostfix), m);
        ModNetwork.HostTicked += HostRemovePeakFlares;
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

    // ---------- No flare at the peak ----------

    /// <summary>On the mountain in a game of this mode (the host runs it): not in the airport, a run going.</summary>
    private static bool InModeRun =>
        Plugin.ModConfig.NoPeakFlares.Synced() && Net.InRoom && Net.GetRoom(ConfigSync.RoomKey) != null
        && RunManager.Instance != null && RunManager.Instance.runStarted
        && !(Character.localCharacter != null && Character.localCharacter.inAirport);

    public static void ShouldSpawnFlarePostfix(ref bool __result)
    {
        if (__result && InModeRun) __result = false;
    }

    private static float s_NextFlareSweep;

    /// <summary>
    /// Host, every 2 s during a game: a flare lying at the peak (past PEAK's last progress point, or within 10 m of the
    /// peak's flare spawner) is removed, e.g. one spawned before the round started.
    /// </summary>
    private static void HostRemovePeakFlares()
    {
        if (!Net.IsHost || !InModeRun || Time.time < s_NextFlareSweep) return;
        s_NextFlareSweep = Time.time + 2f;
        MountainProgressHandler? progress = Zorro.Core.Singleton<MountainProgressHandler>.Instance;
        Peak.EndgameFlareSpawner[] spawners = Object.FindObjectsByType<Peak.EndgameFlareSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Flare flare in Object.FindObjectsByType<Flare>(FindObjectsSortMode.None))
        {
            Item? item = flare != null ? flare.GetComponent<Item>() : null;
            if (item == null || item.itemState != ItemState.Ground || item.view == null) continue;
            Vector3 pos = item.transform.position;
            bool atPeak = (progress != null && progress.IsAtPeak(pos))
                || System.Array.Exists(spawners, s => s != null && Vector3.Distance(s.transform.position, pos) <= 10f);
            if (!atPeak) continue;
            PhotonNetwork.Destroy(item.gameObject);
            Plugin.Log.LogInfo("[OTL][Tweaks] HOST removed a flare at the peak.");
        }
    }

    public static bool PostReviveStatusPrefix() =>
        !(RoundManager.IsActive && Plugin.ModConfig.NoReviveCurse.Synced());
}

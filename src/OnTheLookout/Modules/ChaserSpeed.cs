using System.Collections.Generic;
using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Chasers move a bit faster than runners (they can't use most items), plus a short boost after
/// each capture: +CaptureBoostPercent for CaptureBoostSeconds, and each further capture while the
/// boost is active adds CaptureBoostStackPercent and refreshes the timer.
/// The boost is tracked on every client from the host's capture notice; it only matters on the
/// chaser's own client because movement is owner-simulated.
/// </summary>
internal static class ChaserSpeed
{
    private static readonly Dictionary<int, (float Percent, float Until)> s_Boosts = new();

    public static bool Install(Harmony harmony)
    {
        ModNetwork.NoticeReceived += (notice, chaser, _) =>
        {
            if (notice == Notice.Captured) AddCaptureBoost(chaser);
        };

        // Patch target: CharacterMovement.GetMovementForce() (postfix, private).
        // Why: the walking/sprinting force for this physics step; PEAK's own speed buff
        // (Affliction_FasterBoi) changes the same value through movementModifier.
        return SafePatch.Postfix(harmony, typeof(CharacterMovement), "GetMovementForce", typeof(ChaserSpeed), nameof(Postfix), "Speed");
    }

    private static void AddCaptureBoost(int actor)
    {
        var cfg = Plugin.ModConfig;
        float percent = BoostPercent(actor) > 0f
            ? BoostPercent(actor) + cfg.CaptureBoostStackPercent.Synced()
            : cfg.CaptureBoostPercent.Synced();
        s_Boosts[actor] = (percent, Time.time + cfg.CaptureBoostSeconds.Synced());

        // The capturing chaser also gets a full morale boost: PEAK's own morale animation plus a full
        // extra-stamina bar (Character.MoraleBoost, the same thing a campfire gives).
        Character local = Character.localCharacter;
        if (cfg.CaptureMoraleBoost.Synced() && local != null && Net.Actor(local) == actor && !local.data.dead)
        {
            local.MoraleBoost(1f, 1);
        }
    }

    /// <summary>Current capture boost in percent (0 if none).</summary>
    public static float BoostPercent(int actor) =>
        s_Boosts.TryGetValue(actor, out var b) && Time.time < b.Until ? b.Percent : 0f;

    public static float BoostSecondsLeft(int actor) =>
        s_Boosts.TryGetValue(actor, out var b) ? Mathf.Max(0f, b.Until - Time.time) : 0f;

    public static void Postfix(CharacterMovement __instance, ref float __result)
    {
        Character c = __instance.character;
        if (__result <= 0f || !RoleManager.IsChaser(c)) return;
        __result *= Plugin.ModConfig.ChaserSpeedMultiplier.Synced() * (1f + BoostPercent(Net.Actor(c)) / 100f);
    }
}

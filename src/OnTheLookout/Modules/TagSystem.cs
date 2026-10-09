using System.Collections.Generic;
using HarmonyLib;
using OnTheLookout.Core;
using OnTheLookout.Freeze;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Capture: a chaser looks at a runner and holds interact for CaptureHoldSeconds, the same way a hungry
/// scout eats another one (PEAK's CharacterInteractible "constant" interaction, with its hold ring).
/// When the hold finishes the chaser's client sends a claim and only the host decides (roles, head start,
/// freeze, safe zone, milk, distance), then kills the runner with the game's own RPCA_Die.
/// </summary>
internal static class TagSystem
{
    private const float RunnerDebounce = 2f;

    private static readonly Dictionary<int, float> s_LastCapture = new();

    public static bool Install(Harmony harmony)
    {
        const string m = "Tag";
        bool ok = true;
        System.Type t = typeof(TagSystem);
        System.Type ci = typeof(CharacterInteractible);

        // CharacterInteractible is what lets you carry, feed or eat another scout. Prefixes on its interaction
        // methods (local player's client) turn "look at a runner as a chaser" into a hold-to-capture
        // interaction: interactible, constant (hold), prompt "CAPTURE", hold time, and the result on finish.
        ok &= SafePatch.Prefix(harmony, ci, nameof(CharacterInteractible.IsInteractible), t, nameof(BoolPrefix), m);
        ok &= SafePatch.Prefix(harmony, ci, nameof(CharacterInteractible.IsPrimaryInteractible), t, nameof(BoolPrefix), m);
        ok &= SafePatch.Prefix(harmony, ci, nameof(CharacterInteractible.IsConstantlyInteractable), t, nameof(BoolPrefix), m);
        ok &= SafePatch.Prefix(harmony, ci, nameof(CharacterInteractible.GetInteractionText), t, nameof(TextPrefix), m);
        ok &= SafePatch.Prefix(harmony, ci, nameof(CharacterInteractible.GetInteractTime), t, nameof(TimePrefix), m);
        ok &= SafePatch.Prefix(harmony, ci, nameof(CharacterInteractible.Interact), t, nameof(InteractPrefix), m);
        ok &= SafePatch.Prefix(harmony, ci, nameof(CharacterInteractible.Interact_CastFinished), t, nameof(CastFinishedPrefix), m);
        return ok;
    }

    /// <summary>The local chaser may start capturing this character right now (the host checks again).</summary>
    private static bool CanCapture(CharacterInteractible target, Character? interactor)
    {
        Character runner = target.character;
        if (!RoundManager.IsChasing || interactor == null || !interactor.IsLocal || runner == null || runner == interactor) return false;
        if (!RoleManager.IsChaser(interactor) || !RoleManager.IsRunner(runner)) return false;
        if (interactor.data.dead || interactor.data.passedOut || FreezeState.IsFrozen(Net.Actor(interactor))) return false;
        if (runner.data.dead || (runner.data.passedOut && !Plugin.ModConfig.TagPassedOutRunners.Synced())) return false;
        return !SafeZoneSystem.IsSafe(runner.Center);
    }

    public static bool BoolPrefix(CharacterInteractible __instance, Character interactor, ref bool __result)
    {
        if (!CanCapture(__instance, interactor)) return true;
        __result = true;
        return false;
    }

    public static bool TextPrefix(CharacterInteractible __instance, ref string __result)
    {
        if (!CanCapture(__instance, Character.localCharacter)) return true;
        __result = "CAPTURE";
        return false;
    }

    public static bool TimePrefix(CharacterInteractible __instance, Character interactor, ref float __result)
    {
        if (!CanCapture(__instance, interactor)) return true;
        __result = Mathf.Max(0.1f, Plugin.ModConfig.CaptureHoldSeconds.Synced());
        return false;
    }

    /// <summary>Pressing interact on a runner as a chaser starts the hold; it must not also carry/drop them.</summary>
    public static bool InteractPrefix(CharacterInteractible __instance, Character interactor) => !CanCapture(__instance, interactor);

    public static bool CastFinishedPrefix(CharacterInteractible __instance, Character interactor)
    {
        if (!CanCapture(__instance, interactor)) return true;
        int chaserActor = Net.Actor(interactor), runnerActor = Net.Actor(__instance.character);
        Plugin.Log.LogInfo($"[OTL][Tag] capture hold finished on {Net.NameOf(runnerActor)}; claiming.");
        if (Net.IsHost) HostHandleClaim(chaserActor, runnerActor, PhotonNetwork.LocalPlayer.ActorNumber);
        else Net.SendToHost(Msg.TagClaim, chaserActor, runnerActor);
        return false;
    }

    /// <summary>
    /// Fortified milk applies Affliction_Invincibility with isFromMilk; afflictions are replicated
    /// to every client (AfflictionSyncData), so the host sees it on remote runners too.
    /// </summary>
    public static bool HasMilkProtection(Character c) =>
        c.refs.afflictions.afflictionList.Exists(a => a is Peak.Afflictions.Affliction_Invincibility { isFromMilk: true });

    /// <summary>Host only: validate a capture claim and apply it.</summary>
    public static void HostHandleClaim(int chaserActor, int runnerActor, int sender)
    {
        if (!Net.IsHost || !RoundManager.IsChasing) return;

        // Only the capturing chaser may claim.
        if (sender != chaserActor) return;

        Character? chaser = Net.CharacterOf(chaserActor);
        Character? runner = Net.CharacterOf(runnerActor);
        if (chaser == null || runner == null) return;
        if (!RoleManager.IsChaser(chaser) || !RoleManager.IsRunner(runner)) return;
        if (chaser.data.dead || chaser.data.passedOut || FreezeState.IsFrozen(chaserActor)) return;
        if (runner.data.dead) return;
        if (runner.data.passedOut && !Plugin.ModConfig.TagPassedOutRunners.Synced()) return;
        if (s_LastCapture.TryGetValue(runnerActor, out float last) && Time.time - last < RunnerDebounce) return;

        if (Plugin.ModConfig.MilkProtectsFromCapture.Synced() && HasMilkProtection(runner))
        {
            Plugin.Log.LogInfo($"[OTL][Tag] {Net.NameOf(chaserActor)} tried to capture {Net.NameOf(runnerActor)}, who is protected by fortified milk.");
            return;
        }

        if (SafeZoneSystem.IsSafe(runner.Center))
        {
            Plugin.Log.LogInfo($"[OTL][Tag] {Net.NameOf(chaserActor)} tried to capture {Net.NameOf(runnerActor)} in a campfire safe zone.");
            return;
        }

        float distance = Vector3.Distance(chaser.Center, runner.Center);
        if (distance > Plugin.ModConfig.TagMaxDistance.Synced())
        {
            Plugin.Log.LogInfo($"[OTL][Tag] claim rejected: {distance:0.0}m apart on the host.");
            return;
        }

        s_LastCapture[runnerActor] = Time.time;
        Plugin.Log.LogInfo($"[OTL][Tag] HOST: {Net.NameOf(chaserActor)} captured {Net.NameOf(runnerActor)}.");
        runner.view.RPC("RPCA_Die", RpcTarget.All);
        ModNetwork.Broadcast(Notice.Captured, chaserActor, runnerActor);
    }
}

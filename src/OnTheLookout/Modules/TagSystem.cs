using System.Collections.Generic;
using HarmonyLib;
using OnTheLookout.Core;
using OnTheLookout.Freeze;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Capture: a chaser's body colliding with a runner's body. Collisions are observed on the clients
/// that own either body (most accurate view) and on the host; each sends a claim, and only the host
/// decides (roles, head start, freeze, safe zone, distance sanity), then kills the runner with the
/// game's own RPCA_Die. Dead runners form the conversion pool (<see cref="ConversionSystem"/>).
/// </summary>
internal static class TagSystem
{
    private const float ClaimInterval = 0.5f;
    private const float RunnerDebounce = 2f;

    private static readonly Dictionary<long, float> s_LastClaim = new();
    private static readonly Dictionary<int, float> s_LastCapture = new();
    private static int s_CharacterLayer = -1;

    public static bool Install(Harmony harmony) =>
        // Patch target: Bodypart.OnCollisionEnter(Collision) (postfix, private).
        // Why: fires for every body-part contact between characters. Same hook Hide-and-PEAK uses.
        SafePatch.Postfix(harmony, typeof(Bodypart), "OnCollisionEnter", typeof(TagSystem), nameof(CollisionPostfix), "Tag");

    public static void CollisionPostfix(Bodypart __instance, Collision collision)
    {
        if (!RoundManager.IsChasing || collision?.collider == null) return;

        if (s_CharacterLayer < 0) s_CharacterLayer = LayerMask.NameToLayer("Character");
        if (collision.collider.gameObject.layer != s_CharacterLayer) return;

        Character self = __instance.character;
        if (self == null || (!self.IsLocal && !Net.IsHost)) return;

        Character other = collision.collider.GetComponentInParent<Character>();
        if (other == null || other == self) return;

        Character chaser, runner;
        if (RoleManager.IsChaser(self) && RoleManager.IsRunner(other)) { chaser = self; runner = other; }
        else if (RoleManager.IsRunner(self) && RoleManager.IsChaser(other)) { chaser = other; runner = self; }
        else return;

        int chaserActor = Net.Actor(chaser), runnerActor = Net.Actor(runner);
        if (FreezeState.IsFrozen(chaserActor) || runner.data.dead) return;

        long key = ((long)chaserActor << 32) | (uint)runnerActor;
        if (s_LastClaim.TryGetValue(key, out float last) && Time.time - last < ClaimInterval) return;
        s_LastClaim[key] = Time.time;

        if (Net.IsHost) HostHandleClaim(chaserActor, runnerActor, PhotonNetwork.LocalPlayer.ActorNumber);
        else Net.SendToHost(Msg.TagClaim, chaserActor, runnerActor);
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

        // Only the two players involved (or the host's own simulation) may claim.
        if (sender != chaserActor && sender != runnerActor && sender != PhotonNetwork.LocalPlayer.ActorNumber) return;

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
            Plugin.Log.LogInfo($"[OTL][Tag] {Net.NameOf(runnerActor)} touched by {Net.NameOf(chaserActor)} but is protected by fortified milk.");
            return;
        }

        if (SafeZoneSystem.IsSafe(runner.Center))
        {
            Plugin.Log.LogInfo($"[OTL][Tag] {Net.NameOf(runnerActor)} touched by {Net.NameOf(chaserActor)} but is in a campfire safe zone.");
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

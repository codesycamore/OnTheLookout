using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Rule 7: faster fog (and rising lava / gloom) that only hurts runners. PEAK has two fog implementations
/// (OrbFogHandler/FogSphere and the legacy rising Fog); both move locally on every client and apply
/// damage locally to Character.localCharacter, so speed and exemption are applied on each client
/// from host-synced settings.
/// </summary>
internal static class FogSystem
{
    private static float Multiplier => RoundManager.IsActive ? Plugin.ModConfig.FogSpeedMultiplier.Synced() : 1f;

    /// <summary>The local player is a chaser during a round, so fog must not hurt them.</summary>
    private static bool LocalExempt => RoleManager.IsChaser(Character.localCharacter);

    public static bool Install(Harmony harmony)
    {
        const string m = "Fog";
        bool ok = true;

        // Patch targets: OrbFogHandler.Move / Fog.Move (postfixes). Why: they advance the fog by speed*dt each frame.
        ok &= SafePatch.Postfix(harmony, typeof(OrbFogHandler), "Move", typeof(FogSystem), nameof(OrbMovePostfix), m);
        ok &= SafePatch.Postfix(harmony, typeof(Fog), "Move", typeof(FogSystem), nameof(LegacyMovePostfix), m);
        // OrbFogHandler.WaitToMove (prefix, private). Why: while the fog waits, the host starts it here
        // (StartMovingRPC) when its timer runs out or everyone has moved on. During a round we replace that
        // decision: the fog starts FogStartDelaySeconds after the head start ends, every leg.
        ok &= SafePatch.Prefix(harmony, typeof(OrbFogHandler), "WaitToMove", typeof(FogSystem), nameof(OrbWaitPrefix), m);


        // LavaRising.Update (prefix + postfix). Why: the Caldera's rising lava and the rising gloom are a separate
        // hazard (LavaRising), started by the host after its own wait and moved by timeTraveled on every client.
        // During a round the host starts it FogStartDelaySeconds after the head start (like the fog), and it
        // rises FogSpeedMultiplier times faster.
        ok &= SafePatch.Prefix(harmony, typeof(LavaRising), "Update", typeof(FogSystem), nameof(LavaUpdatePrefix), m);
        ok &= SafePatch.Postfix(harmony, typeof(LavaRising), "Update", typeof(FogSystem), nameof(LavaUpdatePostfix), m);

        // Patch target: FogSphere.SetSharderVars (transpiler + postfix). Why: the method mixes shader setup
        // with fog damage, so only its CharacterAfflictions.AddStatus call is swapped for a gate.
        ok &= SafePatch.Transpiler(harmony, typeof(FogSphere), "SetSharderVars", typeof(FogSystem), nameof(SphereTranspiler), m);
        ok &= SafePatch.Postfix(harmony, typeof(FogSphere), "SetSharderVars", typeof(FogSystem), nameof(SpherePostfix), m);

        // Patch target: Fog.MakePlayerCold (prefix). Why: legacy fog damage, nothing else in it.
        ok &= SafePatch.Prefix(harmony, typeof(Fog), "MakePlayerCold", typeof(FogSystem), nameof(LegacyColdPrefix), m);

        return ok;
    }

    public static void OrbMovePostfix(OrbFogHandler __instance)
    {
        float extra = Multiplier - 1f;
        if (extra == 0f || !__instance.isMoving) return;
        __instance.currentSize -= __instance.speed * extra * Time.deltaTime;
    }

    public static bool OrbWaitPrefix(OrbFogHandler __instance)
    {
        if (!RoundManager.IsActive) return true;
        if (Net.IsHost && !__instance.isMoving && Ascents.currentAscent >= 0 && RoundManager.IsChasing
            && RoundManager.ChaseElapsedSeconds >= Plugin.ModConfig.FogStartDelaySeconds.Synced())
        {
            Plugin.Log.LogInfo($"[OTL][Fog] HOST: fog starts rising ({Plugin.ModConfig.FogStartDelaySeconds.Synced():0}s after the head start).");
            __instance.photonView.RPC("StartMovingRPC", Photon.Pun.RpcTarget.All);
        }

        return false;
    }

    private static bool IsLavaOrGloom(LavaRising lava) =>
        lava.risingFieldType switch
        {
            LavaRising.RisingFieldType.Lava => RunSettings.GetValue(RunSettings.SETTINGTYPE.Hazard_TheLavaRises) != 0,
            LavaRising.RisingFieldType.Gloom => RunSettings.GetValue(RunSettings.SETTINGTYPE.Hazard_TheGloomRises) != 0,
            _ => false, // the rising souls have their own trigger
        };

    public static void LavaUpdatePrefix(LavaRising __instance)
    {
        if (!RoundManager.IsActive || !Net.IsHost || __instance.started || !IsLavaOrGloom(__instance)) return;
        MapHandler? map = Zorro.Core.Singleton<MapHandler>.Instance;
        if (map == null || map.GetCurrentSegment() != __instance.requiredSegment) return;

        if (RoundManager.IsChasing && Ascents.fogEnabled
            && RoundManager.ChaseElapsedSeconds >= Plugin.ModConfig.FogStartDelaySeconds.Synced())
        {
            __instance.started = true;
            __instance.secondsWaitedToStart = Mathf.Max(__instance.secondsWaitedToStart, __instance.initialWaitTime + 0.01f);
            __instance.photonView.RPC("RPC_SyncLava", Photon.Pun.RpcTarget.Others, true, __instance.ended, __instance.timeTraveled, __instance.secondsWaitedToStart);
            Plugin.Log.LogInfo($"[OTL][Fog] HOST: the {__instance.risingFieldType} starts rising ({Plugin.ModConfig.FogStartDelaySeconds.Synced():0}s after the head start).");
        }
        else
        {
            __instance.secondsWaitedToStart = 0f; // PEAK's own wait never gets far enough to start it
        }
    }

    public static void LavaUpdatePostfix(LavaRising __instance)
    {
        float extra = Multiplier - 1f;
        if (extra <= 0f || !__instance.enabled || !__instance.started || __instance.ended) return;
        __instance.timeTraveled += Time.deltaTime * extra;
    }

    public static void LegacyMovePostfix(Fog __instance)
    {
        float extra = Multiplier - 1f;
        if (extra == 0f || __instance.currentStop >= __instance.stops.Length) return;
        __instance.fogHeight += __instance.fogSpeed * extra * Time.deltaTime;
    }

    public static IEnumerable<CodeInstruction> SphereTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo addStatus = AccessTools.Method(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus));
        MethodInfo gate = AccessTools.Method(typeof(FogSystem), nameof(AddStatusGate));
        int replaced = 0;
        foreach (CodeInstruction ins in instructions)
        {
            if ((ins.opcode == OpCodes.Callvirt || ins.opcode == OpCodes.Call) && ins.operand is MethodInfo mi && mi == addStatus)
            {
                replaced++;
                yield return new CodeInstruction(OpCodes.Call, gate) { labels = ins.labels, blocks = ins.blocks };
            }
            else
            {
                yield return ins;
            }
        }

        if (replaced == 0) Plugin.Log.LogError("[OTL][Fog] FogSphere.SetSharderVars: AddStatus call not found - chasers will take fog damage.");
        else Plugin.Log.LogInfo($"[OTL][Fog] gated {replaced} fog damage call(s).");
    }

    /// <summary>Stand-in for the instance call CharacterAfflictions.AddStatus(...) inside the fog damage code.</summary>
    public static bool AddStatusGate(CharacterAfflictions self, CharacterAfflictions.STATUSTYPE statusType, float amount,
        bool fromRPC, bool playEffects, bool notify, bool ignoreInvincibility, bool ignoreSkeleton) =>
        !LocalExempt && self.AddStatus(statusType, amount, fromRPC, playEffects, notify, ignoreInvincibility, ignoreSkeleton);

    public static void SpherePostfix()
    {
        if (LocalExempt && Character.localCharacter != null) Character.localCharacter.data.isInFog = false;
    }

    public static bool LegacyColdPrefix() => !LocalExempt;
}

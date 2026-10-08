using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Rule 7: faster fog that only hurts runners. PEAK has two fog implementations
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

        // Patch target: FogSphere.SetSharderVars (transpiler + postfix). Why: the method mixes shader setup
        // with fog damage, so only its CharacterAfflictions.AddStatus call is swapped for a gate.
        ok &= SafePatch.Transpiler(harmony, typeof(FogSphere), "SetSharderVars", typeof(FogSystem), nameof(SphereTranspiler), m);
        ok &= SafePatch.Postfix(harmony, typeof(FogSphere), "SetSharderVars", typeof(FogSystem), nameof(SpherePostfix), m);

        // Patch target: Fog.MakePlayerCold (prefix). Why: legacy fog damage, nothing else in it.
        ok &= SafePatch.Prefix(harmony, typeof(Fog), "MakePlayerCold", typeof(FogSystem), nameof(LegacyColdPrefix), m);

        // Patch target: OrbFogHandler.PlayersHaveMovedOn (prefix). Why: vanilla waits for EVERY living player
        // to pass the start line before the fog moves; lagging chasers would stall it.
        ok &= SafePatch.Prefix(harmony, typeof(OrbFogHandler), "PlayersHaveMovedOn", typeof(FogSystem), nameof(MovedOnPrefix), m);
        return ok;
    }

    public static void OrbMovePostfix(OrbFogHandler __instance)
    {
        float extra = Multiplier - 1f;
        if (extra == 0f || !__instance.isMoving) return;
        __instance.currentSize -= __instance.speed * extra * Time.deltaTime;
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

    public static bool MovedOnPrefix(OrbFogHandler __instance, ref bool __result)
    {
        if (!RoundManager.IsActive || !Plugin.ModConfig.FogIgnoresChasers.Synced()) return true;

        __result = false;
        if (Character.AllCharacters.Count == 0 || Ascents.currentAscent < 0) return false;

        bool anyRunner = false;
        foreach (Character c in Character.AllCharacters)
        {
            if (c.data.dead || RoleManager.IsChaser(c)) continue;
            anyRunner = true;
            if (c.Center.y < __instance.currentStartHeight || c.Center.z < __instance.currentStartForward) return false;
        }

        __result = anyRunner;
        return false;
    }
}

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// The fog, rising lava and rising gloom are vanilla (PEAK's own start timing and speed); the only change is
/// that fog never hurts chasers. Fog damages locally on every client, so the exemption runs on each client.
/// </summary>
internal static class FogSystem
{
    /// <summary>The local player is a chaser during a round, so fog must not hurt them.</summary>
    private static bool LocalExempt => RoleManager.IsChaser(Character.localCharacter);

    public static bool Install(Harmony harmony)
    {
        const string m = "Fog";
        bool ok = true;

        // Patch target: FogSphere.SetSharderVars (transpiler + postfix). Why: the method mixes shader setup
        // with fog damage, so only its CharacterAfflictions.AddStatus call is swapped for a gate.
        ok &= SafePatch.Transpiler(harmony, typeof(FogSphere), "SetSharderVars", typeof(FogSystem), nameof(SphereTranspiler), m);
        ok &= SafePatch.Postfix(harmony, typeof(FogSphere), "SetSharderVars", typeof(FogSystem), nameof(SpherePostfix), m);

        // Patch target: Fog.MakePlayerCold (prefix). Why: legacy fog damage, nothing else in it.
        ok &= SafePatch.Prefix(harmony, typeof(Fog), "MakePlayerCold", typeof(FogSystem), nameof(LegacyColdPrefix), m);

        return ok;
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

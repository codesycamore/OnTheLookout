using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Conversion: when a scout statue (RespawnChest) is used during a round and there are ghosts
/// (dead runners - captured or killed; passing out doesn't count), the statue revives everyone dead
/// as in vanilla, and one random revived ghost becomes an extra chaser
/// (the others come back as runners). Dead chasers are revived too (optional).
/// With no ghosts, the statue behaves exactly like vanilla (items, or reviving downed players).
/// </summary>
internal static class ConversionSystem
{
    public static bool Install(Harmony harmony) =>
        // Patch target: RespawnChest.SpawnItems(List<Transform>) (prefix, host).
        // Why: opening a statue calls this on the host; vanilla decides here between spawning items and
        // RespawnAllPlayersHere() (which revives everyone dead). Hooking here also works on ascents
        // where statues normally can't revive.
        SafePatch.Prefix(harmony, typeof(RespawnChest), nameof(RespawnChest.SpawnItems), typeof(ConversionSystem), nameof(SpawnItemsPrefix), "Conversion");

    public static bool SpawnItemsPrefix(RespawnChest __instance, ref List<PhotonView> __result)
    {
        if (!Net.IsHost || !RoundManager.IsActive) return true;

        int[] ghosts = PhotonNetwork.PlayerList.Select(p => p.ActorNumber)
            .Where(a => RoleManager.RoleOf(a) == Role.Runner && Net.CharacterOf(a) is { data.dead: true })
            .ToArray();
        int[] deadChasers = Plugin.ModConfig.ReviveDeadChasers.Synced()
            ? RoleManager.Chasers.Where(a => Net.CharacterOf(a) is { data.dead: true }).ToArray()
            : System.Array.Empty<int>();
        if (ghosts.Length == 0 && deadChasers.Length == 0) return true; // vanilla statue

        __instance.photonView.RPC("RemoveSkeletonRPC", RpcTarget.AllBuffered);

        // Everyone dead (or fully passed out) is revived, as in vanilla; then one random revived ghost
        // (a dead runner) becomes a chaser and the rest come back as runners.
        int[] converted = ghosts.OrderBy(_ => Random.value)
            .Take(Mathf.Max(0, Plugin.ModConfig.GhostsConvertedPerStatue.Synced()))
            .ToArray();
        foreach (int actor in converted)
        {
            RoleManager.SetRole(actor, Role.Chaser);
            LegLoadout.GiveBlowgunLater(actor);
            ModNetwork.Broadcast(Notice.Converted, actor, 0);
        }

        int revived = 0;
        foreach (Character c in Character.AllCharacters)
        {
            if (c.isBot || !(c.data.dead || c.data.fullyPassedOut)) continue;
            if (RoleManager.RoleOf(Net.Actor(c)) == Role.Chaser && c.data.dead && !converted.Contains(Net.Actor(c))
                && !Plugin.ModConfig.ReviveDeadChasers.Synced()) continue;
            Revive(Net.Actor(c), __instance);
            revived++;
        }

        Plugin.Log.LogInfo($"[OTL][Conversion] HOST statue: revived {revived}, {ghosts.Length} ghost(s), converted {string.Join(", ", converted.Select(Net.NameOf))}.");
        __result = new List<PhotonView>();
        return false;
    }

    private static void Revive(int actor, RespawnChest statue)
    {
        Character? c = Net.CharacterOf(actor);
        if (c == null) return;
        c.view.RPC("RPCA_ReviveAtPosition", RpcTarget.All, statue.RandomRevivePoint, false, (int)statue.SegmentNumber);
    }
}

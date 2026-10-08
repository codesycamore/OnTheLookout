using System.Collections;
using System.Linq;
using HarmonyLib;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Rule 9: when a campfire is lit (the biome advances), one random dead runner (captured or killed
/// by the environment; passing out doesn't count) becomes a chaser and is revived at that campfire
/// with the game's own RPCA_ReviveAtPosition. Optionally dead chasers are revived too.
/// </summary>
internal static class ConversionSystem
{
    public static bool Install(Harmony harmony) =>
        // Patch target: Campfire.Light_Rpc(bool updateSegment, float) (postfix, private [PunRPC]).
        // Why: runs on every client when a campfire is lit; updateSegment=true means the run advances
        // to the next segment (MapHandler.GoToSegment), and we get the campfire's position for free.
        SafePatch.Postfix(harmony, typeof(Campfire), "Light_Rpc", typeof(ConversionSystem), nameof(LightPostfix), "Conversion");

    public static void LightPostfix(Campfire __instance, bool updateSegment)
    {
        if (!updateSegment || !Net.IsHost || !RoundManager.IsActive || !Plugin.ModConfig.ConvertOnBiomeChange.Synced()) return;
        ModNetwork.Instance?.StartCoroutine(ConvertLater(__instance.transform.position));
    }

    private static IEnumerator ConvertLater(Vector3 campfire)
    {
        yield return new WaitForSeconds(Plugin.ModConfig.ConversionDelaySeconds.Synced());
        if (!Net.IsHost || !RoundManager.IsActive) yield break;

        int[] pool = PhotonNetwork.PlayerList
            .Select(p => p.ActorNumber)
            .Where(a => RoleManager.RoleOf(a) == Role.Runner && Net.CharacterOf(a) is { data.dead: true })
            .OrderBy(_ => Random.value)
            .Take(Mathf.Max(0, Plugin.ModConfig.ConversionsPerBiome.Synced()))
            .ToArray();

        foreach (int actor in pool)
        {
            RoleManager.SetRole(actor, Role.Chaser);
            Revive(actor, campfire);
            ModNetwork.Broadcast(Notice.Converted, actor, 0);
        }

        if (Plugin.ModConfig.ReviveDeadChasers.Synced())
        {
            foreach (int actor in RoleManager.Chasers.ToArray())
            {
                if (pool.Contains(actor) || Net.CharacterOf(actor) is not { data.dead: true }) continue;
                Revive(actor, campfire);
            }
        }

        Plugin.Log.LogInfo($"[OTL][Conversion] HOST converted {pool.Length} runner(s): {string.Join(", ", pool.Select(Net.NameOf))}");
    }

    private static void Revive(int actor, Vector3 campfire)
    {
        Character? c = Net.CharacterOf(actor);
        if (c == null) return;
        Vector2 offset = Random.insideUnitCircle.normalized * 4f;
        Vector3 position = campfire + new Vector3(offset.x, 2f, offset.y);
        c.view.RPC("RPCA_ReviveAtPosition", RpcTarget.All, position, false, -1);
    }
}

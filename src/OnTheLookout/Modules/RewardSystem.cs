using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Rule 10: the first runner to reach the safe zone of each (unlit) campfire gets a reward item
/// (RewardItems, default a fortified milk), given by the host.
/// Rewarded campfires are replicated in <see cref="RoomKey"/> so a new host won't pay out twice.
/// </summary>
internal static class RewardSystem
{
    public const string RoomKey = "otl.rwd";

    private static readonly HashSet<int> s_Rewarded = new();

    public static bool Enabled { get; set; }

    private static int KeyOf(Campfire fire)
    {
        Vector3 p = fire.transform.position;
        return unchecked((Mathf.RoundToInt(p.x) * 73856093) ^ (Mathf.RoundToInt(p.y) * 19349663) ^ (Mathf.RoundToInt(p.z) * 83492791));
    }

    /// <summary>Host, ~2x per second.</summary>
    public static void HostTick()
    {
        if (!Enabled || !RoundManager.IsActive || RoundManager.InHold) return;
        float radius = Plugin.ModConfig.CampfireSafeRadius.Synced();

        foreach (Campfire fire in SafeZoneSystem.Campfires)
        {
            if (!fire.isActiveAndEnabled || fire.state != Campfire.FireState.Off || s_Rewarded.Contains(KeyOf(fire))) continue;

            Character? winner = Character.AllCharacters
                .Where(c => RoleManager.IsRunner(c) && !c.data.dead && !c.data.passedOut)
                .Where(c => Vector3.Distance(c.Center, fire.transform.position) <= radius)
                .OrderBy(c => Vector3.Distance(c.Center, fire.transform.position))
                .FirstOrDefault();
            if (winner != null) Reward(fire, winner);
        }
    }

    private static void Reward(Campfire fire, Character runner)
    {
        s_Rewarded.Add(KeyOf(fire));
        Net.SetRoom(RoomKey, s_Rewarded.ToArray());

        int actor = Net.Actor(runner);
        int spawned = 0;
        try
        {
            // One random pick from RewardItems per count (default: one fortified milk), into the inventory
            // if there is room, otherwise at their feet.
            var pool = ItemCatalog.FindByNames(Plugin.ModConfig.RewardItems.Synced());
            for (int i = 0; pool.Count > 0 && i < Mathf.Max(1, Plugin.ModConfig.RewardItemCount.Synced()); i++)
            {
                LegLoadout.Give(runner, pool[UnityEngine.Random.Range(0, pool.Count)]);
                spawned++;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[OTL][Reward] failed to give the reward: {e}");
        }

        Plugin.Log.LogInfo($"[OTL][Reward] HOST: {Net.NameOf(actor)} reached a campfire first; gave {spawned} item(s).");
        ModNetwork.Broadcast(Notice.Rewarded, actor, spawned);
    }

    /// <summary>Host: <paramref name="fire"/> pays out no reward (e.g. every runner died and the chasers were sent there).</summary>
    public static void HostMarkRewarded(Campfire fire)
    {
        if (!s_Rewarded.Add(KeyOf(fire))) return;
        Net.SetRoom(RoomKey, s_Rewarded.ToArray());
        Plugin.Log.LogInfo("[OTL][Reward] HOST: no first-runner reward at the next campfire (every runner died).");
    }

    /// <summary>Host: new round, every campfire can pay out again.</summary>
    public static void HostReset()
    {
        s_Rewarded.Clear();
        Net.SetRoom(RoomKey, Array.Empty<int>());
    }

    public static void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!changed.TryGetValue(RoomKey, out object? raw) || raw is not int[] keys) return;
        s_Rewarded.Clear();
        s_Rewarded.UnionWith(keys);
    }

    public static void Clear() => s_Rewarded.Clear();
}

using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;

namespace OnTheLookout.Core;

internal enum Role : byte
{
    Runner = 1,
    Chaser = 2,
}

/// <summary>
/// Roles per Photon actor number, replicated through the room property <see cref="RoomKey"/>.
/// Only the host writes. Anyone not in the table is a runner (covers late joiners).
/// </summary>
internal static class RoleManager
{
    public const string RoomKey = "otl.roles";

    private static readonly Dictionary<int, Role> s_Roles = new();
    private static readonly Random s_Rng = new();

    public static event Action? RolesChanged;

    public static Role RoleOf(int actor) => s_Roles.TryGetValue(actor, out Role r) ? r : Role.Runner;

    public static bool IsChaser(int actor) => RoundManager.IsActive && RoleOf(actor) == Role.Chaser;

    public static bool IsChaser(Character? c) => c != null && !c.isBot && IsChaser(Net.Actor(c));

    public static bool IsRunner(Character? c) => c != null && !c.isBot && RoundManager.IsActive && RoleOf(Net.Actor(c)) == Role.Runner;

    public static IEnumerable<int> Chasers => s_Roles.Where(kv => kv.Value == Role.Chaser).Select(kv => kv.Key);

    /// <summary>Host: draw the chasers for a new run (ChasersByPlayerCount, weighted by airport chaser odds), always leaving at least one runner.</summary>
    public static void AssignRandom()
    {
        if (!Net.IsHost) return;
        // Weighted random order (Efraimidis-Spirakis: key = u^(1/weight), highest keys first): airport chaser
        // odds raise or lower each player's chance; with every weight 1 this is a plain shuffle. Weight 0 = picked last.
        List<int> actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber)
            .OrderByDescending(a => DrawKey(ChaserPreference.WeightOf(a)))
            .ToList();
        int count = Math.Max(0, Math.Min(ChasersFor(actors.Count), actors.Count - 1));

        s_Roles.Clear();
        for (int i = 0; i < actors.Count; i++)
        {
            s_Roles[actors[i]] = i < count ? Role.Chaser : Role.Runner;
        }

        Plugin.Log.LogInfo($"[OTL][Roles] HOST assigned {count} chaser(s): {string.Join(", ", Chasers.Select(Net.NameOf))}");
        ChaserPreference.HostClear(); // odds only count for this draw; the next run starts fresh
        Publish();
    }

    /// <summary>Weighted draw key: higher weight = more likely to come first. Weight 0 = always last.</summary>
    private static double DrawKey(float weight) => weight <= 0f ? -1.0 : Math.Pow(s_Rng.NextDouble(), 1.0 / weight);

    /// <summary>
    /// Chaser count for a lobby size from <c>ChasersByPlayerCount</c> ("minPlayers:chasers, ..."):
    /// the entry with the highest minPlayers that is still &lt;= players wins. Defaults to 1.
    /// </summary>
    public static int ChasersFor(int players) => CountFor(Plugin.ModConfig.ChasersByPlayerCount.Value, players);

    /// <summary>
    /// Reads a "minPlayers:count, ..." table (e.g. "1:1, 6:2"): the entry with the highest minPlayers that
    /// is still &lt;= players wins. Defaults to 1.
    /// </summary>
    public static int CountFor(string table, int players)
    {
        int best = 1, bestMin = int.MinValue;
        foreach (string pair in table.Split(','))
        {
            string[] parts = pair.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int min) && int.TryParse(parts[1].Trim(), out int chasers)
                && players >= min && min > bestMin)
            {
                best = chasers;
                bestMin = min;
            }
        }

        return best;
    }

    /// <summary>Host only.</summary>
    public static void SetRole(int actor, Role role)
    {
        if (!Net.IsHost) return;
        s_Roles[actor] = role;
        Plugin.Log.LogInfo($"[OTL][Roles] HOST set {Net.NameOf(actor)}#{actor} -> {role}");
        Publish();
    }

    private static void Publish()
    {
        var table = new Hashtable();
        foreach (KeyValuePair<int, Role> kv in s_Roles)
        {
            table[kv.Key] = (byte)kv.Value;
        }

        Net.SetRoom(RoomKey, table);
        RolesChanged?.Invoke();
    }

    public static void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!changed.TryGetValue(RoomKey, out object? raw) || raw is not Hashtable table) return;
        s_Roles.Clear();
        foreach (System.Collections.DictionaryEntry kv in table)
        {
            if (kv.Key is int actor && kv.Value is byte role) s_Roles[actor] = (Role)role;
        }

        RolesChanged?.Invoke();
    }

    public static void Clear()
    {
        s_Roles.Clear();
        RolesChanged?.Invoke();
    }
}

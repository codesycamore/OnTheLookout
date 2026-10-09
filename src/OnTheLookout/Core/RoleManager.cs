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

    /// <summary>
    /// Host, at the start of every leg: draw the chasers from the players who chose CHASER in the role menu
    /// (<see cref="ChaserPreference"/>), at most <see cref="MaxChasers"/>. If nobody volunteered, one random
    /// player becomes the chaser. Always leaves at least one runner (a solo player stays a runner).
    /// </summary>
    public static void AssignFromPreferences()
    {
        if (!Net.IsHost) return;
        List<int> actors = PhotonNetwork.PlayerList.Select(p => p.ActorNumber).ToList();
        int max = Math.Min(MaxChasers(actors.Count), actors.Count - 1);

        List<int> volunteers = actors.Where(ChaserPreference.WantsChaser).OrderBy(_ => s_Rng.Next()).ToList();
        List<int> chosen = volunteers.Take(Math.Max(0, max)).ToList();
        if (chosen.Count == 0 && max > 0) chosen.Add(actors[s_Rng.Next(actors.Count)]);

        s_Roles.Clear();
        foreach (int actor in actors)
        {
            s_Roles[actor] = chosen.Contains(actor) ? Role.Chaser : Role.Runner;
        }

        Plugin.Log.LogInfo($"[OTL][Roles] HOST drew {chosen.Count} chaser(s) of max {max} ({volunteers.Count} volunteer(s)): {string.Join(", ", Chasers.Select(Net.NameOf))}");
        Publish();
    }

    /// <summary>At most one chaser per RunnersPerChaser runners: floor(players / (RunnersPerChaser + 1)), at least 1.</summary>
    public static int MaxChasers(int players)
    {
        float perChaser = Math.Max(1f, Plugin.ModConfig.RunnersPerChaser.Synced());
        return Math.Max(1, (int)Math.Floor(players / (perChaser + 1f)));
    }

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

    /// <summary>Host: nobody has a role (back in the airport).</summary>
    public static void HostClearAll()
    {
        if (!Net.IsHost) return;
        s_Roles.Clear();
        Publish();
    }

    /// <summary>Host only.</summary>
    public static void SetRole(int actor, Role role)
    {
        if (!Net.IsHost) return;
        Role previous = RoleOf(actor);
        s_Roles[actor] = role;
        Plugin.Log.LogInfo($"[OTL][Roles] HOST set {Net.NameOf(actor)}#{actor} -> {role}");
        Publish();
        if (previous != role) Modules.RoleSwap.HostOnSwap(actor, role); // a swap during a leg (debug key)
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

using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Core;

internal enum ChaserPref : byte
{
    Runner = 0,
    Chaser = 1,
}

/// <summary>
/// Each player's role choice: RUNNER (the default) or CHASER. Chasers are drawn at random from the pool of
/// players who chose CHASER (<see cref="RoleManager.AssignFromPreferences"/>), no odds or weights. The choice
/// is made in the role menu in the airport (for the first draw on the shore) and during the role window
/// after each leg (<see cref="RoundManager.InRoleWindow"/>), and sent privately to the host (an event to the
/// master client only, never stored in room or player properties). It stays until the player changes it,
/// and resets to RUNNER when a run ends and everyone is back in the airport.
/// </summary>
internal static class ChaserPreference
{
    private static readonly HashSet<int> s_HostVolunteers = new();

    /// <summary>The local player's current choice (shown in their own menu only).</summary>
    public static ChaserPref Local { get; private set; } = ChaserPref.Runner;

    public static bool Enabled => Plugin.ModConfig.ChaserPreferenceEnabled.Synced();

    /// <summary>The local player is in the airport, or the role window is open, in a modded lobby with the feature on.</summary>
    public static bool Available => Enabled && Net.InRoom && Net.GetRoom(ConfigSync.RoomKey) != null && (InAirport || RoundManager.InRoleWindow);

    public static bool InAirport => Character.localCharacter != null && Character.localCharacter.inAirport;

    public static void SetLocal(ChaserPref pref)
    {
        if (!Available) return;
        Local = pref;
        if (Net.IsHost) HostSet(PhotonNetwork.LocalPlayer.ActorNumber, pref);
        else Net.SendToHost(Msg.ChaserPreference, (byte)pref);
    }

    private static float s_NextResend;

    /// <summary>
    /// Every client, each frame: while the choice is open (airport or role window) a non-host keeps re-sending
    /// its current choice to the host every 2 s, so the host's pool always matches what the player sees (a
    /// choice can't get lost to timing, e.g. the host clearing choices as it arrives in the airport).
    /// </summary>
    public static void LocalTick()
    {
        if (!Available || Net.IsHost || Time.time < s_NextResend) return;
        s_NextResend = Time.time + 2f;
        Net.SendToHost(Msg.ChaserPreference, (byte)Local);
    }

    /// <summary>Host: store a player's choice. Logged without saying whose or what, to keep it secret.</summary>
    public static void HostSet(int actor, ChaserPref pref)
    {
        if (!Net.IsHost) return;
        bool changed = pref == ChaserPref.Chaser ? s_HostVolunteers.Add(actor) : s_HostVolunteers.Remove(actor);
        if (changed) Plugin.Log.LogInfo($"[OTL][Roles] HOST: a role choice was updated ({s_HostVolunteers.Count} volunteer(s)).");
    }

    /// <summary>Host: whether this player is in the chaser pool. With the feature off, everyone is.</summary>
    public static bool WantsChaser(int actor) => !Enabled || s_HostVolunteers.Contains(actor);

    /// <summary>Back to the default (everyone RUNNER), e.g. when everyone is back in the airport.</summary>
    public static void Clear()
    {
        s_HostVolunteers.Clear();
        Local = ChaserPref.Runner;
    }
}

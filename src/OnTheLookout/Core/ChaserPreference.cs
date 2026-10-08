using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Core;

internal enum ChaserPref : byte
{
    NoPreference = 0,
    WantChaser = 1,
    RatherRun = 2,
}

/// <summary>
/// Chaser odds chosen in the airport. Each player's choice is sent privately to the host (an event to the
/// master client only; it is never stored in room or player properties, so no other player's game ever
/// receives it). The host turns it into a weight for the initial role draw only (RoleManager.AssignRandom),
/// then forgets every choice. Each player's own choice is cleared when they leave the airport, so it
/// resets for every run. Statue conversions are not affected.
/// </summary>
internal static class ChaserPreference
{
    private static readonly Dictionary<int, ChaserPref> s_HostChoices = new();

    /// <summary>The local player's current choice (shown in their own menu only).</summary>
    public static ChaserPref Local { get; private set; }

    public static bool Enabled => Plugin.ModConfig.ChaserPreferenceEnabled.Synced();

    /// <summary>The local player is in the airport of a modded lobby with the feature on.</summary>
    public static bool Available =>
        Enabled && Net.InRoom && Character.localCharacter != null && Character.localCharacter.inAirport;

    public static void SetLocal(ChaserPref pref)
    {
        if (!Available) return;
        Local = pref;
        if (Net.IsHost) HostSet(PhotonNetwork.LocalPlayer.ActorNumber, pref);
        else Net.SendToHost(Msg.ChaserPreference, (byte)pref);
    }

    /// <summary>Every client, each frame: forget the choice once the player has left the airport.</summary>
    public static void LocalTick()
    {
        if (Local != ChaserPref.NoPreference && Character.localCharacter != null && !Character.localCharacter.inAirport)
        {
            Local = ChaserPref.NoPreference;
        }
    }

    /// <summary>Host: store a player's choice. Logged without saying whose or what, to keep it secret.</summary>
    public static void HostSet(int actor, ChaserPref pref)
    {
        if (!Net.IsHost) return;
        if (pref == ChaserPref.NoPreference) s_HostChoices.Remove(actor);
        else s_HostChoices[actor] = pref;
        Plugin.Log.LogInfo($"[OTL][Odds] HOST: a chaser preference was updated ({s_HostChoices.Count} set).");
    }

    /// <summary>Host: a player's weight in the initial role draw.</summary>
    public static float WeightOf(int actor)
    {
        var cfg = Plugin.ModConfig;
        if (!Enabled) return 1f;
        ChaserPref pref = s_HostChoices.TryGetValue(actor, out ChaserPref p) ? p : ChaserPref.NoPreference;
        float weight = pref switch
        {
            ChaserPref.WantChaser => cfg.ChaserOddsWantChaser.Synced(),
            ChaserPref.RatherRun => cfg.ChaserOddsRatherRun.Synced(),
            _ => cfg.ChaserOddsNoPreference.Synced(),
        };
        return Mathf.Max(0f, weight);
    }

    /// <summary>Host: the draw is done; every choice is forgotten so the next run starts fresh.</summary>
    public static void HostClear() => s_HostChoices.Clear();

    public static void Clear()
    {
        s_HostChoices.Clear();
        Local = ChaserPref.NoPreference;
    }
}

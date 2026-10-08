using System;
using System.Collections;
using System.Linq;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using OnTheLookout.Freeze;
using OnTheLookout.Modules;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace OnTheLookout.Core;

internal enum RoundState : byte
{
    Idle = 0,
    Active = 1, // a leg is being played
    LegComplete = 2, // every living runner reached the next campfire's safe zone; waiting for a runner to light it
    RunnersWon = 3,
    ChasersWon = 4,
}

/// <summary>
/// A round is played in legs, campfire to campfire. Each leg starts with a role reveal and a head
/// start, during which chasers are frozen and blind. A leg ends when every living runner is inside
/// the next campfire's safe zone; a runner lighting that campfire starts the next leg.
/// State lives in the room property <see cref="RoomKey"/> =
/// int[] { state, legStart, revealMs, headStartMs, roundId, legId } with absolute server timestamps,
/// so clients compute every countdown locally and a new host after migration simply continues.
/// </summary>
internal static class RoundManager
{
    public const string RoomKey = "otl.round";

    private static RoundState s_State;
    private static int s_LegStart, s_RevealMs, s_HeadStartMs, s_RoundId, s_LegId;

    /// <summary>Raised on every client. Argument: true for the first leg of a new round.</summary>
    public static event Action<bool>? LegStarted;
    public static event Action? LegCompleted;
    public static event Action<RoundState>? RoundEnded;

    public static RoundState State => s_State;

    /// <summary>A round is in progress (roles apply).</summary>
    public static bool IsActive => Net.InRoom && s_State is RoundState.Active or RoundState.LegComplete;

    private static int RevealEnd => unchecked(s_LegStart + s_RevealMs);
    private static int ChaseStart => unchecked(RevealEnd + s_HeadStartMs);

    /// <summary>Role reveal + head start: chasers are frozen and blind.</summary>
    public static bool InHold => Net.InRoom && s_State == RoundState.Active && Net.IsBefore(ChaseStart);

    public static bool InReveal => InHold && Net.IsBefore(RevealEnd);

    /// <summary>Head-start countdown (after the reveal).</summary>
    public static bool InCountdown => InHold && !Net.IsBefore(RevealEnd);

    /// <summary>Chasers are hunting: freezes and captures are live.</summary>
    public static bool IsChasing => Net.InRoom && s_State == RoundState.Active && !Net.IsBefore(ChaseStart);

    public static float HoldSecondsLeft => InHold ? Net.SecondsUntil(ChaseStart) : 0f;

    public static float HoldTotalSeconds => (s_RevealMs + s_HeadStartMs) / 1000f;

    // ---------- Host ----------

    /// <summary>Host: wait until every player has spawned and woken up on the beach, then start.</summary>
    public static IEnumerator HostStartWhenReady()
    {
        float deadline = Time.time + 60f;
        while (Time.time < deadline)
        {
            if (!Net.InRoom || !Net.IsHost) yield break;
            bool ready = PhotonNetwork.PlayerList.All(p => Net.CharacterOf(p.ActorNumber) is { } c
                && !c.data.passedOut && !c.data.fullyPassedOut && c.data.fallSeconds <= 0f && c.data.passedOutOnTheBeach <= 0f);
            if (ready) break;
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(1f);
        HostStartRound();
    }

    /// <summary>Host: assign roles and start (or restart) a round.</summary>
    public static void HostStartRound()
    {
        if (!Net.IsHost) return;
        ConfigSync.Publish();
        RoleManager.AssignRandom();
        FreezeState.HostReset();
        RewardSystem.HostReset();
        PublishLeg(RoundState.Active, s_RoundId + 1, 1);
        Plugin.Log.LogInfo($"[OTL][Round] HOST started round {s_RoundId}.");
    }

    /// <summary>Host: a runner lit a campfire. Same reveal + head start as the round start.</summary>
    public static void HostStartLeg()
    {
        if (!Net.IsHost || !IsActive) return;
        FreezeState.HostReset();
        PublishLeg(RoundState.Active, s_RoundId, s_LegId + 1);
        Plugin.Log.LogInfo($"[OTL][Round] HOST started leg {s_LegId} of round {s_RoundId}.");
    }

    private static void PublishLeg(RoundState state, int roundId, int legId)
    {
        var cfg = Plugin.ModConfig;
        Publish(state, Net.Now, (int)(cfg.RoleRevealSeconds.Value * 1000f), (int)(cfg.HeadStartSeconds.Value * 1000f), roundId, legId);
    }

    private static void HostSetState(RoundState state)
    {
        if (!Net.IsHost || s_State == state) return;
        Plugin.Log.LogInfo($"[OTL][Round] HOST: {s_State} -> {state}");
        Publish(state, s_LegStart, s_RevealMs, s_HeadStartMs, s_RoundId, s_LegId);
    }

    /// <summary>Host, ~2x per second: leg completion and win conditions.</summary>
    public static void HostTick()
    {
        if (!Net.IsHost || !IsActive) return;

        var runners = PhotonNetwork.PlayerList
            .Where(p => RoleManager.RoleOf(p.ActorNumber) == Role.Runner)
            .Select(p => Net.CharacterOf(p.ActorNumber))
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();
        if (runners.Count == 0) return;

        if (runners.All(c => c.data.dead))
        {
            HostSetState(RoundState.ChasersWon);
            return;
        }

        var alive = runners.Where(c => !c.data.dead).ToList();
        MountainProgressHandler? progress = Singleton<MountainProgressHandler>.Instance;
        if (progress != null && alive.Any(c => progress.IsAtPeak(c.Center)))
        {
            HostSetState(RoundState.RunnersWon);
            return;
        }

        if (s_State == RoundState.Active && IsChasing)
        {
            float radius = Plugin.ModConfig.CampfireSafeRadius.Synced();
            foreach (Campfire fire in SafeZoneSystem.Campfires)
            {
                if (!fire.isActiveAndEnabled || fire.state != Campfire.FireState.Off) continue;
                if (alive.All(c => Vector3.Distance(c.Center, fire.transform.position) <= radius))
                {
                    HostSetState(RoundState.LegComplete);
                    return;
                }
            }
        }
    }

    private static void Publish(RoundState state, int legStart, int revealMs, int headStartMs, int roundId, int legId) =>
        Net.SetRoom(RoomKey, new[] { (int)state, legStart, revealMs, headStartMs, roundId, legId });

    // ---------- Replication ----------

    public static void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!changed.TryGetValue(RoomKey, out object? raw) || raw is not int[] { Length: 6 } v) return;

        RoundState previous = s_State;
        int previousRound = s_RoundId, previousLeg = s_LegId;
        s_State = (RoundState)v[0];
        s_LegStart = v[1];
        s_RevealMs = v[2];
        s_HeadStartMs = v[3];
        s_RoundId = v[4];
        s_LegId = v[5];

        if (s_State == RoundState.Active && (s_RoundId != previousRound || s_LegId != previousLeg || previous != RoundState.Active))
        {
            LegStarted?.Invoke(s_RoundId != previousRound);
        }
        else if (s_State == RoundState.LegComplete && previous == RoundState.Active)
        {
            LegCompleted?.Invoke();
        }
        else if (s_State is RoundState.RunnersWon or RoundState.ChasersWon && previous != s_State)
        {
            RoundEnded?.Invoke(s_State);
        }
    }

    public static void Clear()
    {
        s_State = RoundState.Idle;
        s_RoundId = 0;
        s_LegId = 0;
    }
}

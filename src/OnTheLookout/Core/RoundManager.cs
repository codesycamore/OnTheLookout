using System;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using OnTheLookout.Freeze;
using Zorro.Core;

namespace OnTheLookout.Core;

internal enum RoundState : byte
{
    Idle = 0,
    Active = 1,
    RunnersWon = 2,
    ChasersWon = 3,
}

/// <summary>
/// Round state in the room property <see cref="RoomKey"/> = int[] { state, startTimestamp, headStartMs, roundId }.
/// Timers are absolute server timestamps, so clients compute countdowns locally and a new host
/// after migration simply continues.
/// </summary>
internal static class RoundManager
{
    public const string RoomKey = "otl.round";

    private static RoundState s_State;
    private static int s_Start;
    private static int s_HeadStartMs;
    private static int s_RoundId;

    public static event Action? RoundStarted;
    public static event Action<RoundState>? RoundEnded;

    public static RoundState State => s_State;

    public static bool IsActive => Net.InRoom && s_State == RoundState.Active;

    public static bool InHeadStart => IsActive && Net.IsBefore(unchecked(s_Start + s_HeadStartMs));

    public static float HeadStartSecondsLeft => InHeadStart ? Net.SecondsUntil(unchecked(s_Start + s_HeadStartMs)) : 0f;

    public static float HeadStartTotal => s_HeadStartMs / 1000f;

    /// <summary>Host: assign roles and start (or restart) a round.</summary>
    public static void HostStartRound()
    {
        if (!Net.IsHost) return;
        ConfigSync.Publish();
        RoleManager.AssignRandom();
        FreezeState.HostReset();
        Modules.RewardSystem.HostReset();
        int headStart = (int)(Plugin.ModConfig.HeadStartSeconds.Value * 1000f);
        Publish(RoundState.Active, Net.Now, headStart, s_RoundId + 1);
        Plugin.Log.LogInfo($"[OTL][Round] HOST started round {s_RoundId + 1} with {headStart / 1000f:0}s head start.");
    }

    public static void HostEndRound(RoundState result)
    {
        if (!Net.IsHost || s_State != RoundState.Active) return;
        Plugin.Log.LogInfo($"[OTL][Round] HOST ended round {s_RoundId}: {result}");
        Publish(result, s_Start, s_HeadStartMs, s_RoundId);
    }

    /// <summary>Host, ~2x per second: win conditions.</summary>
    public static void HostTick()
    {
        if (!Net.IsHost || !IsActive || InHeadStart) return;

        var runners = PhotonNetwork.PlayerList
            .Where(p => RoleManager.RoleOf(p.ActorNumber) == Role.Runner)
            .Select(p => Net.CharacterOf(p.ActorNumber))
            .Where(c => c != null)
            .ToList();
        if (runners.Count == 0) return;

        if (runners.All(c => c!.data.dead))
        {
            HostEndRound(RoundState.ChasersWon);
            return;
        }

        MountainProgressHandler? progress = Singleton<MountainProgressHandler>.Instance;
        if (progress != null && runners.Any(c => !c!.data.dead && progress.IsAtPeak(c.Center)))
        {
            HostEndRound(RoundState.RunnersWon);
        }
    }

    private static void Publish(RoundState state, int start, int headStartMs, int id) =>
        Net.SetRoom(RoomKey, new[] { (int)state, start, headStartMs, id });

    public static void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!changed.TryGetValue(RoomKey, out object? raw) || raw is not int[] { Length: 4 } v) return;

        RoundState previous = s_State;
        int previousId = s_RoundId;
        s_State = (RoundState)v[0];
        s_Start = v[1];
        s_HeadStartMs = v[2];
        s_RoundId = v[3];

        if (s_State == RoundState.Active && (s_RoundId != previousId || previous != RoundState.Active))
        {
            RoundStarted?.Invoke();
        }
        else if (previous == RoundState.Active && s_State is RoundState.RunnersWon or RoundState.ChasersWon)
        {
            RoundEnded?.Invoke(s_State);
        }
    }

    public static void Clear()
    {
        s_State = RoundState.Idle;
        s_RoundId = 0;
    }
}

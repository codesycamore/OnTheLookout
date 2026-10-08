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

    /// <summary>
    /// Start of a run on the shore: nothing can be interacted with while everyone is waking up and for
    /// SpawnInteractLockSeconds after the round starts. Only when the host runs the mod (its config is
    /// in the room), so a vanilla host can never lock players out.
    /// </summary>
    public static bool InSpawnLock
    {
        get
        {
            if (!Net.InRoom || Net.GetRoom(ConfigSync.RoomKey) == null) return false;
            if (s_State == RoundState.Active && s_LegId == 1)
            {
                return Net.IsBefore(unchecked(s_LegStart + (int)(Plugin.ModConfig.SpawnInteractLockSeconds.Synced() * 1000f)));
            }

            return s_State == RoundState.Idle && Plugin.ModConfig.AutoStartRound.Synced()
                && RunManager.Instance != null && RunManager.Instance.runStarted;
        }
    }

    public static float HoldTotalSeconds => (s_RevealMs + s_HeadStartMs) / 1000f;

    /// <summary>Seconds since the chase of this leg began (after the head start); 0 when not chasing.</summary>
    public static float ChaseElapsedSeconds => IsChasing ? unchecked(Net.Now - ChaseStart) / 1000f : 0f;

    /// <summary>Changes whenever a new leg (or round) starts.</summary>
    public static int LegKey => unchecked(s_RoundId * 1000 + s_LegId);

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
        AdminRestart.HostForget();
        s_AwaitingLeg = false;
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

    /// <summary>Host (admin restart): a fresh leg from wherever everyone now is, keeping roles. Works after a round ended too.</summary>
    public static void HostRestartLeg()
    {
        if (!Net.IsHost) return;
        if (s_State == RoundState.Idle)
        {
            HostStartRound();
            return;
        }

        s_AwaitingLeg = false;
        FreezeState.HostReset();
        PublishLeg(RoundState.Active, s_RoundId, s_LegId + 1);
        Plugin.Log.LogInfo($"[OTL][Round] HOST restarted: leg {s_LegId} of round {s_RoundId}.");
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

    // ---------- Campfire -> biome title -> next leg (host) ----------

    private static bool s_AwaitingLeg;
    private static float s_LegDue;

    /// <summary>
    /// Host: a runner lit a campfire. The chase pauses now; the next leg (reveal, blind chasers,
    /// head start) starts after the new biome's title has played, or after a fallback delay.
    /// </summary>
    public static void HostOnCampfireLit()
    {
        if (!Net.IsHost || !IsActive) return;
        HostSetState(RoundState.LegComplete); // no captures or freezes while waiting
        s_AwaitingLeg = true;
        s_LegDue = Time.time + Plugin.ModConfig.NoTitleFallbackSeconds.Value;
        Plugin.Log.LogInfo($"[OTL][Round] HOST: campfire lit; next leg after the biome title (max {Plugin.ModConfig.NoTitleFallbackSeconds.Value:0}s).");
    }

    /// <summary>
    /// Host: some player's client just showed a biome title (PEAK shows it per player when they cross
    /// the next biome's progress point). The leg starts once the title has finished playing.
    /// </summary>
    public static void HostOnBiomeTitle(int reporter)
    {
        if (!Net.IsHost || !s_AwaitingLeg) return;
        float due = Time.time + Plugin.ModConfig.BiomeTitleSeconds.Value;
        if (due >= s_LegDue) return;
        s_LegDue = due;
        Plugin.Log.LogInfo($"[OTL][Round] HOST: {Net.NameOf(reporter)} sees the biome title; next leg in {Plugin.ModConfig.BiomeTitleSeconds.Value:0.#}s.");
    }

    /// <summary>
    /// New host after migration: if we were waiting for the next leg (a lit campfire with living
    /// runners next to it), keep waiting with the fallback delay instead of stalling.
    /// </summary>
    public static void HostResumeAfterMigration()
    {
        if (!Net.IsHost || s_State != RoundState.LegComplete || s_AwaitingLeg) return;
        float radius = Plugin.ModConfig.CampfireSafeRadius.Synced();
        bool atLitFire = SafeZoneSystem.Campfires.Any(f => f.isActiveAndEnabled && f.state == Campfire.FireState.Lit
            && Character.AllCharacters.Any(c => RoleManager.IsRunner(c) && !c.data.dead && Vector3.Distance(c.Center, f.transform.position) <= radius));
        if (!atLitFire) return;
        s_AwaitingLeg = true;
        s_LegDue = Time.time + Plugin.ModConfig.NoTitleFallbackSeconds.Value;
    }

    /// <summary>Host, ~2x per second: leg completion and win conditions.</summary>
    public static void HostTick()
    {
        if (!Net.IsHost || !IsActive) return;

        if (s_AwaitingLeg && Time.time >= s_LegDue)
        {
            s_AwaitingLeg = false;
            HostStartLeg();
            return;
        }

        var runners = PhotonNetwork.PlayerList
            .Where(p => RoleManager.RoleOf(p.ActorNumber) == Role.Runner)
            .Select(p => Net.CharacterOf(p.ActorNumber))
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();
        if (runners.Count == 0) return;

        if (runners.All(c => c.data.dead))
        {
            s_AwaitingLeg = false;
            HostSetState(RoundState.ChasersWon);
            return;
        }

        var alive = runners.Where(c => !c.data.dead).ToList();
        MountainProgressHandler? progress = Singleton<MountainProgressHandler>.Instance;
        if (progress != null && alive.Any(c => progress.IsAtPeak(c.Center)))
        {
            s_AwaitingLeg = false;
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
                    if (Plugin.ModConfig.TeleportChasersOnLegComplete.Synced()) HostBringChasers(fire.transform.position);
                    return;
                }
            }
        }
    }

    /// <summary>Host: the chase is over for this leg; living chasers are brought to the campfire too.</summary>
    private static void HostBringChasers(Vector3 campfire)
    {
        int i = 0;
        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (!RoleManager.IsChaser(c) || c.data.dead) continue;
            float angle = (i++ * 67f + 30f) * Mathf.Deg2Rad;
            Vector3 spot = campfire + new Vector3(Mathf.Cos(angle) * 5f, 2f, Mathf.Sin(angle) * 5f);
            c.view.RPC("WarpPlayerRPC", RpcTarget.All, spot, true);
        }

        Plugin.Log.LogInfo($"[OTL][Round] HOST: leg complete, {i} chaser(s) brought to the campfire.");
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
        s_AwaitingLeg = false;
        s_RoundId = 0;
        s_LegId = 0;
    }
}

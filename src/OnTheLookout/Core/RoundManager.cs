using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
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
    LegComplete = 2, // the leg was won (runners all safe, or all caught); role window at the campfire before the next leg
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

    /// <summary>
    /// Raised on every client when the game goes back to the airport (the host menu, game over or the end of a run):
    /// every module forgets its game state, so the next run starts exactly like the first one after launching.
    /// </summary>
    public static event Action? FreshStart;

    public static RoundState State => s_State;

    /// <summary>A round is in progress (roles apply).</summary>
    public static bool IsActive => Net.InRoom && (s_State is RoundState.Active or RoundState.LegComplete)
        && !(Character.localCharacter != null && Character.localCharacter.inAirport); // the airport is always vanilla

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
    /// Start of a run on the shore: nothing can be interacted with while everyone is waking up and during the
    /// role reveal after the round starts (until runners are released for their head start). Only when the host
    /// runs the mod (its config is in the room), so a vanilla host can never lock players out.
    /// </summary>
    public static bool InSpawnLock
    {
        get
        {
            if (!Net.InRoom || Net.GetRoom(ConfigSync.RoomKey) == null) return false;
            if (s_State == RoundState.Active && s_LegId == 1)
            {
                return Net.IsBefore(RevealEnd); // until the role reveal ends: runners can interact as soon as they're released
            }

            return InPreRound;
        }
    }

    public static float HoldTotalSeconds => (s_RevealMs + s_HeadStartMs) / 1000f;

    /// <summary>Seconds since the chase of this leg began (after the head start); 0 when not chasing.</summary>
    public static float ChaseElapsedSeconds => IsChasing ? unchecked(Net.Now - ChaseStart) / 1000f : 0f;

    /// <summary>Changes whenever a new leg (or round) starts.</summary>
    public static int LegKey => unchecked(s_RoundId * 1000 + s_LegId);

    // ---------- Host ----------

    /// <summary>Host: wait until every player has spawned and woken up on the beach, then start.</summary>
    /// <summary>Room property: the PEAK run (RunManager.RunId) the current round belongs to.</summary>
    public const string RunKey = "otl.run";

    private static bool s_StartPending;

    private static string CurrentRunId => RunManager.Instance != null ? RunManager.Instance.RunId.ToString() : "";

    /// <summary>
    /// Host, after RunManager.StartRun: wait until everyone has woken up, then start a round - but only for
    /// a NEW run. PEAK calls StartRun more than once per run (RunManager.Start and RunStarter, and again
    /// when a run scene loads or a quicksave resumes); if a round is already going for this run, roles
    /// (including dead and converted chasers) are kept. A fresh run from the airport has a new RunId.
    /// </summary>
    public static IEnumerator HostStartWhenReady()
    {
        if (s_StartPending) yield break; // another StartRun call is already waiting
        s_StartPending = true;
        try
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

            yield return new WaitForSeconds(1f); // PEAK assigns a fresh run its RunId ~2 s after StartRun
            if (!Net.InRoom || !Net.IsHost) yield break;

            string run = CurrentRunId;
            if (IsActive && run.Length > 0 && Net.GetRoom(RunKey) as string == run)
            {
                Plugin.Log.LogInfo($"[OTL][Round] HOST: StartRun again during the same run ({run}); keeping the round and everyone's roles.");
                yield break;
            }

            HostStartRound();
        }
        finally
        {
            s_StartPending = false;
        }
    }

    /// <summary>Host: assign roles and start (or restart) a round.</summary>
    public static void HostStartRound()
    {
        if (!Net.IsHost) return;
        ConfigSync.Publish();
        Net.SetRoom(RunKey, CurrentRunId);
        RoleManager.AssignFromPreferences();
        SafeZoneSystem.LastLitSegment = -1;
        FreezeState.HostReset();
        RewardSystem.HostReset();
        AdminRestart.HostForget();
        HostSetFlags(0);
        PublishLeg(RoundState.Active, s_RoundId + 1, 1);
        Plugin.Log.LogInfo($"[OTL][Round] HOST started round {s_RoundId}.");
    }

    /// <summary>Host: a runner lit a campfire. Same reveal + head start as the round start.</summary>
    public static void HostStartLeg()
    {
        if (!Net.IsHost || !IsActive) return;
        HostSetFlags(0); // runners unfreeze (after the reveal) for their head start
        FreezeState.HostReset();
        PublishLeg(RoundState.Active, s_RoundId, s_LegId + 1);
        RoleSwap.HostApplySnapshot(); // drop/give role items for everyone whose role changed since the last leg
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

        HostSetFlags(0);
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

    // ---------- Game won -> petrify the losers -> back to the airport ----------

    /// <summary>Room property: server timestamp (ms) at which everyone is sent back to the airport after the game was won.</summary>
    public const string AirportKey = "otl.apt";

    private static bool s_AirportSent;

    /// <summary>The game was won: the losers are petrified and the airport countdown runs.</summary>
    public static bool IsGameOver => Net.InRoom && (s_State is RoundState.RunnersWon or RoundState.ChasersWon)
        && !(Character.localCharacter != null && Character.localCharacter.inAirport);

    /// <summary>Seconds left on the airport countdown; 0 when the game isn't over.</summary>
    public static float AirportSecondsLeft => IsGameOver && Net.GetRoom(AirportKey) is int at ? Net.SecondsUntil(at) : 0f;

    /// <summary>
    /// Host: one side won the game (the last leg, to the peak). Every living scout of the losing side is petrified
    /// (<see cref="Notice.Petrify"/>: turned to stone, PEAK's statue death); with a runners' win and PeakChasersDie
    /// the chasers are brought up to the peak first. Every client shows a rainbow GAME OVER! for GameOverSeconds,
    /// then counts down GameOverAirportSeconds ("RETURNING BACK TO AIRPORT IN..."); then the host sends everyone back to the airport
    /// (<see cref="AdminRestart.HostReturnToAirport"/>), where everything resets for a fresh start.
    /// </summary>
    private static void HostGameOver(RoundState winner, Vector3? peak)
    {
        if (!Net.IsHost || IsGameOver) return;
        float seconds = Mathf.Max(0f, Plugin.ModConfig.GameOverSeconds.Synced()) + Mathf.Max(1f, Plugin.ModConfig.GameOverAirportSeconds.Synced());
        s_AirportSent = false;
        Net.SetRoom(AirportKey, unchecked(Net.Now + (int)(seconds * 1000f))); // before the state, so clients have it at once
        HostSetFlags(0);
        HostSetState(winner);
        ZombieHunt.HostDespawnAll();
        Role losers = winner == RoundState.RunnersWon ? Role.Chaser : Role.Runner;
        if (winner == RoundState.RunnersWon && peak != null && Plugin.ModConfig.PeakChasersDie.Synced())
        {
            ModNetwork.Instance?.StartCoroutine(HostChasersToPeak(peak.Value));
        }
        else
        {
            ModNetwork.Broadcast(Notice.Petrify, (int)losers, 0);
        }

        Plugin.Log.LogInfo($"[OTL][Round] HOST: game won ({winner}); {losers}s petrified, back to the airport in {seconds:0.#}s.");
    }

    /// <summary>Every client: the local scout is turned to stone if they're on the losing side (<paramref name="losers"/>).</summary>
    public static void OnPetrifyNotice(Role losers)
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || local.isBot || RoleManager.RoleOf(Net.Actor(local)) != losers) return;
        local.data.SetPetrify(100); // full petrification: PEAK turns them into a statue (Character.HandleLife)
        Plugin.Log.LogInfo("[OTL][Round] the other side won: petrified.");
    }

    // ---------- Campfire lit -> next leg (host) ----------

    /// <summary>Room property: bit flags shared with every client (see <see cref="FlagChasersWonLeg"/>, <see cref="FlagRoleWindow"/>).</summary>
    public const string FlagsKey = "otl.flags";

    /// <summary>The leg that just ended was won by the chasers (every runner died).</summary>
    private const int FlagChasersWonLeg = 2;

    /// <summary>The role window is open: everyone is at the campfire, frozen, choosing their role.</summary>
    private const int FlagRoleWindow = 4;

    private static int s_Flags;

    private static void HostSetFlags(int flags)
    {
        if (!Net.IsHost || flags == s_Flags) return;
        s_Flags = flags;
        Net.SetRoom(FlagsKey, flags);
    }

    /// <summary>
    /// Host: the campfire was lit (by the host when the role window closes). The next leg starts right
    /// away: role reveal (everyone frozen), then runners released for their head start while chasers stay
    /// frozen and blind.
    /// </summary>
    public static void HostOnCampfireLit()
    {
        if (!Net.IsHost || !IsActive) return;
        Plugin.Log.LogInfo("[OTL][Round] HOST: campfire lit; next leg starts now.");
        HostStartLeg();
    }

    /// <summary>
    /// Start of a run on the shore, before the round has started: nobody can move (every client blocks
    /// its own input) until everyone has woken up and the host starts the round with the role reveal.
    /// True while PEAK's run is going but the round in the room belongs to no run or another run; only
    /// when the host runs the mod and starts rounds automatically.
    /// </summary>
    public static bool InPreRound
    {
        get
        {
            if (!Net.InRoom || Net.GetRoom(ConfigSync.RoomKey) == null || !Plugin.ModConfig.AutoStartRound.Synced()) return false;
            RunManager? run = RunManager.Instance;
            if (run == null || !run.runStarted || Character.localCharacter == null || Character.localCharacter.inAirport) return false;
            if (s_State == RoundState.Idle) return true;
            string id = CurrentRunId;
            return id.Length > 0 && id != Guid.Empty.ToString() && Net.GetRoom(RunKey) as string != id;
        }
    }

    // ---------- Leg end -> everyone at the campfire -> role window -> next leg (host) ----------

    /// <summary>Room property: server timestamp (ms) at which the current role window closes.</summary>
    public const string WindowKey = "otl.rwin";

    private static bool s_WindowPending;
    private static float s_WindowDue;
    private static Campfire? s_WindowFire;

    /// <summary>Everyone was already brought to the campfire a second time this role window.</summary>
    private static bool s_Regathered;

    /// <summary>When the role window closes, everyone must be at most this far from the campfire (m, horizontally).</summary>
    private const float GatherCheckRadius = 5f;

    /// <summary>Wait after bringing everyone to the campfire a second time, before roles are drawn (s).</summary>
    private const float RegatherSettleSeconds = 1.5f;

    /// <summary>
    /// Between a leg's end and the next leg: everyone is at the campfire, frozen, and can update their role
    /// choice in the role menu. When it closes the host draws new roles and lights the campfire.
    /// </summary>
    public static bool InRoleWindow => IsActive && (s_Flags & FlagRoleWindow) != 0;

    public static float RoleWindowSecondsLeft => InRoleWindow && Net.GetRoom(WindowKey) is int end ? Net.SecondsUntil(end) : 0f;

    /// <summary>Who won the leg that just ended (valid while <see cref="State"/> is LegComplete).</summary>
    public static bool ChasersWonLeg => IsActive && (s_Flags & FlagChasersWonLeg) != 0;

    /// <summary>Host, ~2x per second: leg completion and win conditions.</summary>
    public static void HostTick()
    {
        if (!Net.IsHost) return;

        // Back in the airport (game over, or the host menu): the mod steps back and PEAK is vanilla again.
        if (s_State != RoundState.Idle && Character.localCharacter != null && Character.localCharacter.inAirport)
        {
            HostResetToVanilla();
            return;
        }

        if (IsGameOver)
        {
            // The airport countdown is running; when it's over everyone goes back to the airport.
            if (!s_AirportSent && Net.GetRoom(AirportKey) is int && AirportSecondsLeft <= 0f)
            {
                s_AirportSent = true;
                AdminRestart.HostReturnToAirport();
            }

            return;
        }

        if (!IsActive) return;

        if (InRoleWindow)
        {
            if (!s_WindowPending)
            {
                // A new host after migration takes over the window the old host opened.
                s_WindowPending = true;
                s_WindowFire = NextCampfire();
                s_WindowDue = Time.time + RoleWindowSecondsLeft;
            }

            if (Time.time >= s_WindowDue) HostCloseRoleWindow();
            return;
        }

        var runners = PhotonNetwork.PlayerList
            .Where(p => RoleManager.RoleOf(p.ActorNumber) == Role.Runner)
            .Select(p => Net.CharacterOf(p.ActorNumber))
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();
        if (runners.Count == 0 || s_State != RoundState.Active) return;

        // Every runner dead (passing out doesn't count): the chasers win the leg, and everyone goes to the
        // next campfire. With no campfire left to go to (the last stretch), the chasers win the game.
        if (runners.All(c => c.data.dead))
        {
            Campfire? next = NextCampfire();
            if (next == null)
            {
                HostGameOver(RoundState.ChasersWon, null);
                return;
            }

            HostEndLeg(next, chasersWon: true);
            return;
        }

        var alive = runners.Where(c => !c.data.dead).ToList();

        // Every chaser dead (during the chase): the runners win the leg, and everyone goes to the next campfire
        // (the dead brought back) for the next leg's role draw. On the last stretch the runners win the game.
        var chasers = PhotonNetwork.PlayerList
            .Where(p => RoleManager.RoleOf(p.ActorNumber) == Role.Chaser)
            .Select(p => Net.CharacterOf(p.ActorNumber))
            .Where(c => c != null)
            .Select(c => c!)
            .ToList();
        if (IsChasing && chasers.Count > 0 && chasers.All(c => c.data.dead))
        {
            Campfire? next = NextCampfire();
            if (next == null)
            {
                HostGameOver(RoundState.RunnersWon, null);
                return;
            }

            RewardSystem.HostMarkRewarded(next); // nobody reached the campfire: no first-runner reward
            HostEndLeg(next, chasersWon: false);
            return;
        }

        MountainProgressHandler? progress = Singleton<MountainProgressHandler>.Instance;
        // Every living runner made it to the peak: the runners win, and the chasers are petrified (brought up
        // there first when PeakChasersDie).
        if (progress != null && alive.Count > 0 && alive.All(c => progress.IsAtPeak(c.Center)))
        {
            HostGameOver(RoundState.RunnersWon, alive[0].Center);
            return;
        }

        // Every living runner is in the next campfire's safe zone: the runners win the leg.
        if (IsChasing)
        {
            float radius = Plugin.ModConfig.CampfireSafeRadius.Synced();
            foreach (Campfire fire in SafeZoneSystem.Campfires)
            {
                if (!fire.isActiveAndEnabled || fire.state != Campfire.FireState.Off) continue;
                if (alive.All(c => Vector3.Distance(c.Center, fire.transform.position) <= radius))
                {
                    HostEndLeg(fire, chasersWon: false);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Host: the leg is over. Everyone (the dead revived) is brought next to <paramref name="fire"/> and frozen,
    /// and the role window opens for RoleWindowSeconds; then <see cref="HostCloseRoleWindow"/> starts the next leg.
    /// </summary>
    private static void HostEndLeg(Campfire fire, bool chasersWon)
    {
        float seconds = Mathf.Max(1f, Plugin.ModConfig.RoleWindowSeconds.Synced());
        RoleSwap.HostSnapshot(); // who was what before the role choice; compared when the next leg starts
        Net.SetRoom(WindowKey, unchecked(Net.Now + (int)(seconds * 1000f)));
        // Flags before the state change, so clients see them together with LegCompleted.
        HostSetFlags(FlagRoleWindow | (chasersWon ? FlagChasersWonLeg : 0));
        HostSetState(RoundState.LegComplete);
        FreezeState.HostReset(); // every cooldown is cleared when a leg is won
        if (chasersWon) RewardSystem.HostMarkRewarded(fire); // nobody earned the first-runner reward
        ZombieHunt.HostDespawnAll();
        HostGatherAt(fire.transform.position);

        s_WindowPending = true;
        s_Regathered = false;
        s_WindowFire = fire;
        s_WindowDue = Time.time + seconds;
        Plugin.Log.LogInfo($"[OTL][Round] HOST: leg {s_LegId} won by the {(chasersWon ? "chasers" : "runners")}; role window open for {seconds:0.#}s.");
    }

    /// <summary>Host: the role window is over. New roles are drawn and the campfire lights itself, which starts the next leg.</summary>
    private static void HostCloseRoleWindow()
    {
        Campfire? fire = s_WindowFire != null ? s_WindowFire : NextCampfire();

        // Everyone must be at the campfire before roles are drawn and the timers start. Anyone who isn't (a warp
        // that didn't take, a revive still settling, someone who slid off): bring everyone there once more.
        if (fire != null && !s_Regathered && HostFarFromCampfire(fire.transform.position) is { Count: > 0 } far)
        {
            s_Regathered = true;
            Plugin.Log.LogWarning($"[OTL][Round] HOST: {string.Join(", ", far)} not within {GatherCheckRadius:0}m of the campfire; bringing everyone there again.");
            HostGatherAt(fire.transform.position);
            s_WindowDue = Time.time + RegatherSettleSeconds;
            return;
        }

        s_WindowPending = false;
        s_WindowFire = null;
        RoleManager.AssignFromPreferences();
        if (fire != null && fire.state == Campfire.FireState.Off && fire.view != null)
        {
            Plugin.Log.LogInfo("[OTL][Round] HOST: role window closed; lighting the campfire.");
            fire.view.RPC("Light_Rpc", RpcTarget.All, true, 0f); // -> SafeZoneSystem.LightPostfix -> HostOnCampfireLit
        }

        if (InRoleWindow) HostStartLeg(); // no campfire to light (or it was lit already): start the leg anyway
    }

    /// <summary>Host: the names of the players (living or dead) further than GatherCheckRadius from the campfire.</summary>
    private static List<string> HostFarFromCampfire(Vector3 campfire)
    {
        var far = new List<string>();
        foreach (Character c in Character.AllCharacters)
        {
            if (c == null || c.isBot) continue;
            Vector3 offset = c.Center - campfire;
            offset.y = 0f;
            if (c.data.dead || offset.magnitude > GatherCheckRadius) far.Add(c.characterName);
        }

        return far;
    }

    /// <summary>Host: everyone to the campfire (within 2.5-4.1 m of it). The dead are revived there (scout statues are disabled).</summary>
    private static void HostGatherAt(Vector3 campfire)
    {
        int i = 0;
        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (c == null || c.isBot) continue;
            float angle = (i++ * 47f + 20f) * Mathf.Deg2Rad;
            float distance = 2.5f + (i % 3) * 0.8f;
            Vector3 spot = campfire + new Vector3(Mathf.Cos(angle) * distance, 1f, Mathf.Sin(angle) * distance);
            if (c.data.dead || c.data.fullyPassedOut) c.view.RPC("RPCA_ReviveAtPosition", RpcTarget.All, spot, false, -1);
            else c.view.RPC("WarpPlayerRPC", RpcTarget.All, spot, true);
        }

        Plugin.Log.LogInfo($"[OTL][Round] HOST: {i} player(s) brought to the campfire.");
    }

    /// <summary>
    /// Host, back in the airport: the round, roles, freezes, rewards and role choices are cleared, so the
    /// airport plays like vanilla PEAK. The next run starts a fresh round on the shore.
    /// </summary>
    public static void HostResetToVanilla()
    {
        if (!Net.IsHost) return;
        s_WindowPending = false;
        s_WindowFire = null;
        HostSetFlags(0);
        s_Regathered = false;
        s_AirportSent = false;
        AdminRestart.HostForget();
        SafeZoneSystem.LastLitSegment = -1;
        FreezeState.HostReset();
        RewardSystem.HostReset();
        ZombieHunt.HostDespawnAll();
        RoleManager.HostClearAll();
        ChaserPreference.Clear();
        Publish(RoundState.Idle, 0, 0, 0, s_RoundId, s_LegId);
        Plugin.Log.LogInfo("[OTL][Round] HOST: back in the airport; the mode is off until the next run starts.");
    }

    /// <summary>The unlit campfire the runners are heading for (the closest one to the players if there are several).</summary>
    public static Campfire? NextCampfire()
    {
        var players = Character.AllCharacters.Where(c => c != null && !c.isBot && !c.data.dead).ToList();
        Vector3 from = players.Count > 0 ? players.Aggregate(Vector3.zero, (s, c) => s + c.Center) / players.Count : Vector3.zero;
        return SafeZoneSystem.Campfires
            .Where(f => f.isActiveAndEnabled && f.state == Campfire.FireState.Off)
            .OrderBy(f => Vector3.Distance(f.transform.position, from))
            .FirstOrDefault();
    }

    /// <summary>Host: living chasers are brought next to <paramref name="position"/>.</summary>
    private static void HostBringChasers(Vector3 position)
    {
        int i = 0;
        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (c == null || c.isBot || RoleManager.RoleOf(Net.Actor(c)) != Role.Chaser || c.data.dead) continue; // also once the game is won
            float angle = (i++ * 67f + 30f) * Mathf.Deg2Rad;
            Vector3 spot = position + new Vector3(Mathf.Cos(angle) * 5f, 2f, Mathf.Sin(angle) * 5f);
            c.view.RPC("WarpPlayerRPC", RpcTarget.All, spot, true);
        }

        Plugin.Log.LogInfo($"[OTL][Round] HOST: {i} chaser(s) brought over.");
    }

    /// <summary>Host: every living runner reached the peak. Living chasers are warped next to them, then petrified.</summary>
    private static IEnumerator HostChasersToPeak(Vector3 peak)
    {
        HostBringChasers(peak);
        yield return new WaitForSeconds(2f); // let the warp land so they turn to stone up there
        ModNetwork.Broadcast(Notice.Petrify, (int)Role.Chaser, 0);
        Plugin.Log.LogInfo("[OTL][Round] HOST: runners reached the peak; chasers brought up and petrified.");
    }

    private static void Publish(RoundState state, int legStart, int revealMs, int headStartMs, int roundId, int legId) =>
        Net.SetRoom(RoomKey, new[] { (int)state, legStart, revealMs, headStartMs, roundId, legId });

    // ---------- Replication ----------

    public static void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed.TryGetValue(FlagsKey, out object? flags) && flags is int f) s_Flags = f;
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
        else if (s_State == RoundState.Idle && previous != RoundState.Idle)
        {
            ResetForFreshStart(); // back in the airport: everything starts over, everyone's choice back to RUNNER
        }
        else if (s_State is RoundState.RunnersWon or RoundState.ChasersWon && previous != s_State)
        {
            RoundEnded?.Invoke(s_State);
        }
    }

    /// <summary>
    /// Every client, back in the airport: roles, freezes, rewards, role choices and every module's own state
    /// (cooldowns, captures, loadouts, mandrake and zombie timers, ...) are cleared. Only the round counter keeps
    /// counting, so old leg keys never match a new leg.
    /// </summary>
    private static void ResetForFreshStart()
    {
        ChaserPreference.Clear();
        RoleManager.Clear();
        FreezeState.Clear();
        FreezeSuspendPatch.End();
        RewardSystem.Clear();
        ZombieHunt.Clear();
        SafeZoneSystem.LastLitSegment = -1;
        s_Flags = 0;
        try
        {
            FreshStart?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[OTL][Round] fresh start: {e}");
        }

        Plugin.Log.LogInfo("[OTL][Round] back in the airport: everything reset for a fresh start.");
    }

    public static void Clear()
    {
        s_State = RoundState.Idle;
        s_Flags = 0;
        s_RoundId = 0;
        s_LegId = 0;
    }
}

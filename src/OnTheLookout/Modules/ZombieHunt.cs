using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// When no chaser is alive during a chase (all dead, or none at all - e.g. a host playing solo is a
/// runner), the mountain keeps hunting: the host sends mushroom
/// zombies, each after one random runner who is outside the safe zones (more zombies in bigger lobbies,
/// ZombiesByPlayerCount). Each zombie lasts ZombieLifetimeSeconds. When none are left
/// and there is still no living chaser, a new wave is sent, until the runners reach the campfire's safe
/// zone (the leg ends) or a chaser is back. Zombies already ignore chasers (<see cref="ChaserResilience"/>).
/// </summary>
internal static class ZombieHunt
{
    private const float KilledBodySeconds = 5f; // a killed zombie's body is cleaned up this long after it dies

    private sealed class Tracked
    {
        public Tracked(GameObject zombie, string target)
        {
            Zombie = zombie;
            Target = target;
            SpawnedAt = Time.time;
        }

        public GameObject Zombie { get; }
        public string Target { get; }
        public float SpawnedAt { get; }
        public float DiedAt { get; set; } = -1f;
    }

    private static readonly List<Tracked> s_Zombies = new();
    private static bool s_WaveActive;
    private static float s_NextWave;
    private static string? s_PrefabName;

    /// <summary>
    /// Host, every 0.5 s. While no chaser is alive (all dead, or none at all, e.g. a host playing solo is a
    /// runner) and no zombie of ours is left in the world, a new wave is sent once ZombieWaveDelaySeconds
    /// have passed since the previous wave was completely gone: ZombiesByPlayerCount zombies, each after ONE
    /// random runner who isn't in a safe zone yet. A wave is gone when every zombie has expired (despawned
    /// after ZombieLifetimeSeconds) or been killed and its body cleaned up.
    /// </summary>
    public static void HostTick()
    {
        var cfg = Plugin.ModConfig;
        UpdateTracked();

        if (!RoundManager.IsChasing || !cfg.ZombiesWhenChasersDead.Synced())
        {
            if (s_Zombies.Count > 0 && !RoundManager.IsChasing) DespawnAll(); // leg over: clear them out
            return;
        }

        if (Character.AllCharacters.Any(c => RoleManager.IsChaser(c) && !c.data.dead)) return; // a chaser is alive
        if (s_Zombies.Count > 0 || Time.time < s_NextWave) return;
        if (RoundManager.ChaseElapsedSeconds < cfg.ZombieStartDelaySeconds.Synced()) return; // grace period after the head start

        List<Character> targets = Character.AllCharacters
            .Where(c => RoleManager.IsRunner(c) && !c.data.dead && !c.data.passedOut && !SafeZoneSystem.IsSafe(c.Center))
            .ToList();
        if (targets.Count == 0) return;

        string? prefab = PrefabName();
        if (prefab == null) return;

        int count = Mathf.Max(1, RoleManager.CountFor(cfg.ZombiesByPlayerCount.Synced(), PhotonNetwork.PlayerList.Length));
        for (int i = 0; i < count; i++)
        {
            Spawn(prefab, targets[Random.Range(0, targets.Count)]); // each zombie picks one random runner
        }
    }

    /// <summary>Notice deaths, clean up killed zombies' bodies, drop zombies that are gone.</summary>
    private static void UpdateTracked()
    {
        foreach (Tracked t in s_Zombies.ToArray())
        {
            if (t.Zombie == null)
            {
                Remove(t, "gone");
                continue;
            }

            bool dead = t.Zombie.GetComponent<MushroomZombie>() is { } z && z.currentState == MushroomZombie.State.Dead;
            if (dead && t.DiedAt < 0f)
            {
                t.DiedAt = Time.time;
                Plugin.Log.LogInfo($"[OTL][Zombies] zombie after {t.Target} died at t={Time.time:0.0}s ({Time.time - t.SpawnedAt:0.0}s after spawning).");
            }

            if (t.DiedAt >= 0f && Time.time - t.DiedAt >= KilledBodySeconds) Despawn(t, "killed, body cleaned up");
        }
    }

    private static void Spawn(string prefab, Character runner)
    {
        // A little way from the runner, so they see it coming.
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector3 spot = runner.Center + new Vector3(dir.x, 0f, dir.y) * Plugin.ModConfig.ZombieSpawnDistance.Synced() + Vector3.up * 2f;
        if (Physics.Raycast(spot + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 40f, HelperFunctions.terrainMapMask))
        {
            spot = hit.point + Vector3.up * 1f;
        }

        GameObject go = PhotonNetwork.Instantiate(prefab, spot, Quaternion.LookRotation(runner.Center - spot), 0);
        if (go == null) return; // nothing spawned: no announcement
        var tracked = new Tracked(go, runner.characterName);
        s_Zombies.Add(tracked);
        s_WaveActive = true;
        ModNetwork.Instance?.StartCoroutine(WakeAndAnnounce(tracked, runner));

        ModNetwork.Instance?.StartCoroutine(ExpireLater(tracked, Plugin.ModConfig.ZombieLifetimeSeconds.Synced()));
        Plugin.Log.LogInfo($"[OTL][Zombies] HOST: no chaser alive; zombie spawned for {runner.characterName} at t={Time.time:0.0}s.");
    }

    /// <summary>
    /// PEAK spawns NPC zombies asleep and hidden (MushroomZombie.Start: StartSleeping + RevealZombie, a 1 s
    /// fade-in) and only wakes one when a player looks at it from close by; ZombieManager culls sleepers
    /// nobody is near (ReadyToDisable -> DestroyZombie). So once it has faded in, the host wakes it and
    /// points it at its runner, and only then is the hunt announced: the alert always matches a zombie
    /// that is up and coming.
    /// </summary>
    private static IEnumerator WakeAndAnnounce(Tracked tracked, Character runner)
    {
        yield return new WaitForSeconds(1.5f);
        if (!s_Zombies.Contains(tracked) || tracked.Zombie == null || tracked.Zombie.GetComponent<MushroomZombie>() is not { } zombie)
        {
            Plugin.Log.LogInfo("[OTL][Zombies] zombie vanished before it woke up; no announcement.");
            yield break;
        }

        if (zombie.currentState == MushroomZombie.State.Sleeping) zombie.WakeUpFromSleep();
        if (runner != null) zombie.SetCurrentTarget(runner, 10f);
        ModNetwork.Broadcast(Notice.ZombieHunt, runner != null ? Net.Actor(runner) : -1, 0);
        Plugin.Log.LogInfo($"[OTL][Zombies] zombie woke up and is hunting {tracked.Target} at t={Time.time:0.0}s.");
    }

    private static IEnumerator ExpireLater(Tracked tracked, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (s_Zombies.Contains(tracked)) Despawn(tracked, "expired");
    }

    private static void Despawn(Tracked t, string why)
    {
        if (Net.IsHost && t.Zombie != null) PhotonNetwork.Destroy(t.Zombie);
        Remove(t, why);
    }

    /// <summary>The single place zombies leave the list, so the end of a wave (and its cooldown) is never missed.</summary>
    private static void Remove(Tracked t, string why)
    {
        if (!s_Zombies.Remove(t)) return;
        Plugin.Log.LogInfo($"[OTL][Zombies] zombie after {t.Target} removed ({why}) at t={Time.time:0.0}s.");
        if (s_Zombies.Count > 0 || !s_WaveActive) return;

        s_WaveActive = false;
        float delay = Mathf.Max(0f, Plugin.ModConfig.ZombieWaveDelaySeconds.Synced());
        s_NextWave = Time.time + delay;
        Plugin.Log.LogInfo($"[OTL][Zombies] wave over; next wave possible in {delay:0}s (t={s_NextWave:0.0}s).");
    }

    /// <summary>Host: remove every zombie of ours right away (the leg is over: all runners safe, or all caught).</summary>
    public static void HostDespawnAll()
    {
        if (Net.IsHost && s_Zombies.Count > 0) DespawnAll();
    }

    private static void DespawnAll()
    {
        foreach (Tracked t in s_Zombies.ToArray()) Despawn(t, "leg over");
    }

    /// <summary>
    /// The network prefab of PEAK's NPC mushroom zombie: taken from a MushroomZombieSpawner in the loaded
    /// level (the name PEAK itself instantiates), falling back to "MushroomZombie" if it's a Resources prefab.
    /// </summary>
    private static string? PrefabName()
    {
        if (s_PrefabName != null) return s_PrefabName;
        MushroomZombieSpawner? spawner = Object.FindObjectsByType<MushroomZombieSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(s => s != null && s.mushroomZombiePrefab != null);
        if (spawner != null) s_PrefabName = spawner.mushroomZombiePrefab.gameObject.name;
        else if (Resources.Load<GameObject>("MushroomZombie") != null) s_PrefabName = "MushroomZombie";

        if (s_PrefabName == null) Plugin.Log.LogWarning("[OTL][Zombies] mushroom zombie prefab not found; no zombies.");
        else Plugin.Log.LogInfo($"[OTL][Zombies] using zombie prefab '{s_PrefabName}'.");
        return s_PrefabName;
    }

    public static void Clear()
    {
        s_Zombies.Clear();
        s_WaveActive = false;
        s_NextWave = 0f;
        s_PrefabName = null;
    }
}

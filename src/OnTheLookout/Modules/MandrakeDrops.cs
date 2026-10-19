using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Host: from MandrakeStartSeconds after the head start ends, every MandrakeIntervalSeconds a mandrake is
/// dropped on the ground at every living runner who isn't in a safe zone (not into their hands), so a
/// runner who stays put gets noisy company. Spawned directly (not through a spawner), so the item rules
/// never remove it. Drops vanish after MandrakeLifetimeSeconds and stop after MandrakeLastSegment.
/// Mandrakes never make runners drowsy, and chasers get MandrakeChaserSleepMultiplier of their drowsiness.
/// Separately (currently switched off), a dynamite boom at every runner every two minutes.
/// With MandrakesOutOfSafeZones no mandrake is ever inside a safe zone (campfires, the peak): the host removes any
/// lying in one, and a player's own client takes away a mandrake they carry into one.
/// </summary>
internal static class MandrakeDrops
{
    private static int s_LegKey = -1;
    private static int s_LastWave;

    public static bool Install(Harmony harmony)
    {
        ModNetwork.HostTicked += HostTick;
        ModNetwork.HostTicked += HostClearSafeZones;
        RoundManager.FreshStart += () =>
        {
            s_LegKey = -1;
            s_LastWave = 0;
            s_LastBoom = 0;
            s_MandrakeContext = 0;
        };
        ModNetwork.NoticeReceived += (notice, a, _) =>
        {
            if (notice == Notice.MandrakeBoom) PlayBoom(a);
        };

        const string m = "Mandrake.NoRunnerSleep";
        // The scream's drowsiness is set up in the mandrake prefabs (serialized, not in code), most likely an AOE
        // fired by Mandrake.timeEvent. Both are wrapped so anything a mandrake object applies is recognised:
        // TimeEvent.Update() and AOE.Explode() (prefix + postfix): mark "a mandrake is acting" while they run.
        bool ok = SafePatch.Prefix(harmony, typeof(TimeEvent), "Update", typeof(MandrakeDrops), nameof(TimeEventPrefix), m);
        ok &= SafePatch.Postfix(harmony, typeof(TimeEvent), "Update", typeof(MandrakeDrops), nameof(ContextPostfix), m);
        ok &= SafePatch.Prefix(harmony, typeof(AOE), nameof(AOE.Explode), typeof(MandrakeDrops), nameof(AoePrefix), m);
        ok &= SafePatch.Postfix(harmony, typeof(AOE), nameof(AOE.Explode), typeof(MandrakeDrops), nameof(ContextPostfix), m);
        // CharacterAfflictions.AddStatus (prefix): while a mandrake is acting, no drowsiness for runners, less for chasers.
        ok &= SafePatch.Prefix(harmony, typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus), typeof(MandrakeDrops), nameof(AddStatusPrefix), m);
        // CharacterAfflictions.AddAffliction (prefix): same for a drowsy-over-time affliction.
        ok &= SafePatch.Prefix(harmony, typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddAffliction), typeof(MandrakeDrops), nameof(AddAfflictionPrefix), m);
        return ok;
    }

    // ---------- No sleep for runners ----------

    private static int s_MandrakeContext;
    private static readonly Dictionary<int, bool> s_IsMandrake = new();
    private static bool s_LoggedBlock;

    private static bool IsMandrakeObject(Component c)
    {
        int id = c.GetInstanceID();
        if (!s_IsMandrake.TryGetValue(id, out bool yes))
        {
            yes = c.GetComponentInParent<Mandrake>(true) != null || c.GetComponentInParent<MandrakeScreamFX>(true) != null;
            if (s_IsMandrake.Count > 4096) s_IsMandrake.Clear();
            s_IsMandrake[id] = yes;
        }

        return yes;
    }

    public static void TimeEventPrefix(TimeEvent __instance, out bool __state)
    {
        __state = RoundManager.IsActive && IsMandrakeObject(__instance);
        if (__state) s_MandrakeContext++;
    }

    public static void AoePrefix(AOE __instance, out bool __state)
    {
        __state = RoundManager.IsActive && IsMandrakeObject(__instance);
        if (__state) s_MandrakeContext++;
    }

    public static void ContextPostfix(bool __state)
    {
        if (__state) s_MandrakeContext = Mathf.Max(0, s_MandrakeContext - 1);
    }

    private static bool InMandrakeContext(Character? c) => s_MandrakeContext > 0 && c != null;

    private static bool BlockFor(Character? c) =>
        InMandrakeContext(c) && RoleManager.IsRunner(c) && Plugin.ModConfig.MandrakeNoRunnerSleep.Synced();

    /// <summary>A chaser gets MandrakeChaserSleepMultiplier of a mandrake's drowsiness (1 = vanilla).</summary>
    private static float ChaserScale(Character? c) =>
        InMandrakeContext(c) && RoleManager.IsChaser(c) ? Mathf.Max(0f, Plugin.ModConfig.MandrakeChaserSleepMultiplier.Synced()) : 1f;

    public static bool AddStatusPrefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount, ref bool __result)
    {
        if (statusType != CharacterAfflictions.STATUSTYPE.Drowsy) return true;
        if (BlockFor(__instance.character))
        {
            LogBlock(__instance.character);
            __result = false;
            return false;
        }

        amount *= ChaserScale(__instance.character);
        return true;
    }

    public static bool AddAfflictionPrefix(CharacterAfflictions __instance, ref Peak.Afflictions.Affliction affliction)
    {
        if (affliction is not Peak.Afflictions.Affliction_AdjustDrowsyOverTime { statusPerSecond: > 0f } drowsy) return true;
        if (BlockFor(__instance.character))
        {
            LogBlock(__instance.character);
            return false;
        }

        float scale = ChaserScale(__instance.character);
        // A copy: the affliction passed in may be the prefab's own serialized instance.
        if (scale != 1f) affliction = new Peak.Afflictions.Affliction_AdjustDrowsyOverTime(drowsy.statusPerSecond * scale, drowsy.totalTime);
        return true;
    }

    private static void LogBlock(Character c)
    {
        if (s_LoggedBlock) return;
        s_LoggedBlock = true;
        Plugin.Log.LogInfo($"[OTL][Mandrake] a mandrake's drowsiness on runner {c.characterName} was blocked (logged once).");
    }

    // ---------- Explosion sounds ----------

    /// <summary>
    /// Every ExplosionIntervalSeconds of the chase (in every leg), a dynamite boom at each living runner outside a
    /// safe zone. Switched off for now; set to true to bring it back.
    /// </summary>
    private static readonly bool ExplosionsEnabled = false; // readonly, not const: no unreachable-code warning
    private const float ExplosionIntervalSeconds = 120f;

    private static int s_LastBoom;

    private static void HostTickBooms(float elapsed)
    {
        if (!ExplosionsEnabled) return;
        int boom = Mathf.FloorToInt(elapsed / ExplosionIntervalSeconds);
        if (boom <= s_LastBoom) return;
        s_LastBoom = boom;
        int booms = 0;
        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (!RoleManager.IsRunner(c) || c.data.dead || SafeZoneSystem.IsSafe(c.Center)) continue;
            ModNetwork.Broadcast(Notice.MandrakeBoom, Net.Actor(c), 0);
            booms++;
        }

        Plugin.Log.LogInfo($"[OTL][Mandrake] HOST explosion {boom}: {booms} runner(s).");
    }

    // ---------- No mandrakes in safe zones ----------

    private static float s_NextSafeSweep, s_NextLocalCheck;

    private static bool KeepOutOfSafeZones => RoundManager.IsActive && Plugin.ModConfig.MandrakesOutOfSafeZones.Synced();

    /// <summary>Host, about once a second: every mandrake lying in a safe zone is removed (the master client may destroy any networked object).</summary>
    private static void HostClearSafeZones()
    {
        if (!Net.IsHost || !KeepOutOfSafeZones || Time.time < s_NextSafeSweep) return;
        s_NextSafeSweep = Time.time + 1f;
        int removed = 0;
        foreach (Mandrake m in Object.FindObjectsByType<Mandrake>(FindObjectsSortMode.None))
        {
            Item? item = m != null ? m.GetComponentInParent<Item>() : null;
            if (item == null || item.itemState != ItemState.Ground || item.view == null || !SafeZoneSystem.IsSafe(item.transform.position)) continue;
            PhotonNetwork.Destroy(item.gameObject);
            removed++;
        }

        if (removed > 0) Plugin.Log.LogInfo($"[OTL][Mandrake] HOST removed {removed} mandrake(s) from a safe zone.");
    }

    /// <summary>Every client, each frame (from Hud): the local player loses any mandrake they carry into a safe zone.</summary>
    public static void LocalTick()
    {
        if (!KeepOutOfSafeZones || Time.time < s_NextLocalCheck) return;
        s_NextLocalCheck = Time.time + 0.5f;
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || local.player == null || !SafeZoneSystem.IsSafe(local.Center)) return;
        for (byte i = 0; i < local.player.itemSlots.Length; i++)
        {
            ItemSlot slot = local.player.itemSlots[i];
            if (slot == null || slot.IsEmpty() || slot.prefab == null || slot.prefab.GetComponentInChildren<Mandrake>(true) == null) continue;
            if (local.refs.items.currentSelectedSlot.IsSome && local.refs.items.currentSelectedSlot.Value == i)
            {
                local.refs.items.EquipSlot(Zorro.Core.Optionable<byte>.None); // put it away first: the held object goes with the slot
                s_NextLocalCheck = Time.time + 0.3f; // empty the slot on the next check
                return;
            }

            local.player.EmptySlot(Zorro.Core.Optionable<byte>.Some(i));
            Plugin.Log.LogInfo($"[OTL][Mandrake] carried a mandrake into a safe zone: removed it from slot {i}.");
        }
    }

    // ---------- Drops ----------

    private static void HostTick()
    {
        var cfg = Plugin.ModConfig;
        if (!Net.IsHost || !RoundManager.IsChasing) return;

        if (RoundManager.LegKey != s_LegKey)
        {
            s_LegKey = RoundManager.LegKey;
            s_LastWave = 0;
            s_LastBoom = 0;
        }

        float elapsed = RoundManager.ChaseElapsedSeconds;
        HostTickBooms(elapsed);

        float start = cfg.MandrakeStartSeconds.Synced(), interval = Mathf.Max(5f, cfg.MandrakeIntervalSeconds.Synced());
        if (start <= 0f || elapsed < start) return;
        int wave = Mathf.FloorToInt((elapsed - start) / interval) + 1;
        if (wave <= s_LastWave) return;
        s_LastWave = wave;

        int segment = SafeZoneSystem.LastLitSegment >= 0 ? SafeZoneSystem.LastLitSegment : (int)MapHandler.CurrentSegmentNumber;
        if (segment > cfg.MandrakeLastSegment.Synced()) return;
        Item? mandrake = ItemCatalog.FindByNames("Mandrake").FirstOrDefault();
        if (mandrake == null)
        {
            Plugin.Log.LogWarning("[OTL][Mandrake] no item named 'Mandrake'.");
            return;
        }

        int spawned = 0;
        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (!RoleManager.IsRunner(c) || c.data.dead || SafeZoneSystem.IsSafe(c.Center)) continue;
            Vector3 spot = c.Center + c.data.lookDirection_Flat * -0.8f + Vector3.up * 0.3f; // just behind them, on the ground
            GameObject go = PhotonNetwork.Instantiate("0_Items/" + mandrake.gameObject.name, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), 0);
            if (go.GetComponentInChildren<Mandrake>(true) is { } m) ModNetwork.Instance?.StartCoroutine(ShortenFirstScream(m));
            if (go.GetComponent<Item>() is { } item) ModNetwork.Instance?.StartCoroutine(RemoveLater(item, s_LegKey));
            spawned++;
        }

        Plugin.Log.LogInfo($"[OTL][Mandrake] HOST wave {wave} (segment {segment}): {spawned} mandrake(s) dropped.");
    }

    /// <summary>
    /// Host: removes a dropped mandrake MandrakeLifetimeSeconds after it appeared, if it is still lying around
    /// (one a player is holding is left alone; it is checked again until it's dropped or the leg ends).
    /// The master client may destroy any networked object (PhotonNetwork.Destroy).
    /// </summary>
    private static IEnumerator RemoveLater(Item item, int legKey)
    {
        float life = Plugin.ModConfig.MandrakeLifetimeSeconds.Synced();
        if (life <= 0f) yield break;
        yield return new WaitForSeconds(life);
        var wait = new WaitForSeconds(1f);
        while (item != null && Net.IsHost && RoundManager.LegKey == legKey && item.itemState != ItemState.Ground) yield return wait;
        if (item == null || !Net.IsHost || item.itemState != ItemState.Ground) yield break;
        PhotonNetwork.Destroy(item.gameObject);
        Plugin.Log.LogInfo("[OTL][Mandrake] HOST removed a dropped mandrake.");
    }

    private static SFX_Instance[]? s_BoomSfx;
    private static AudioSource[] s_BoomSources = System.Array.Empty<AudioSource>();

    /// <summary>
    /// Every client: the dynamite's explosion sound at runner <paramref name="actor"/>. Only the sound effects of
    /// Dynamite.explosionPrefab (its SFX_PlayOneShot components) are played; the prefab itself (damage, knockback,
    /// visuals) is never spawned.
    /// </summary>
    private static void PlayBoom(int actor)
    {
        if (Net.CharacterOf(actor) is not { } runner) return;
        if (s_BoomSfx == null)
        {
            GameObject? explosion = ItemCatalog.DynamitePrefab?.explosionPrefab;
            s_BoomSfx = explosion == null ? System.Array.Empty<SFX_Instance>() : explosion.GetComponentsInChildren<SFX_PlayOneShot>(true)
                .SelectMany(p => (p.sfxs ?? System.Array.Empty<SFX_Instance>()).Append(p.sfx))
                .Where(s => s != null).Distinct().ToArray();
            // Fallback: plain audio sources on the prefab.
            if (explosion != null) s_BoomSources = explosion.GetComponentsInChildren<AudioSource>(true).Where(s => s.clip != null).ToArray();
            Plugin.Log.LogInfo(s_BoomSfx.Length + s_BoomSources.Length > 0
                ? $"[OTL][Mandrake] explosion sound: {string.Join(", ", s_BoomSfx.Select(s => s.name).Concat(s_BoomSources.Select(s => s.clip.name)))}."
                : "[OTL][Mandrake] dynamite explosion sound not found; mandrakes drop silently.");
        }

        foreach (SFX_Instance sfx in s_BoomSfx) sfx.Play(runner.Center);
        if (s_BoomSfx.Length > 0) return;
        foreach (AudioSource source in s_BoomSources) AudioSource.PlayClipAtPoint(source.clip, runner.Center, source.volume);
    }

    /// <summary>
    /// A new mandrake waits its longest scream delay (Mandrake.screamWaitMax, set in Mandrake.Start) before its
    /// first scream; the host counts that wait (Mandrake.CheckScream). Our drops scream after
    /// MandrakeFirstScreamSeconds instead; later screams keep PEAK's random min-max gaps.
    /// </summary>
    private static IEnumerator ShortenFirstScream(Mandrake mandrake)
    {
        yield return null; // Mandrake.Start sets the first wait on its first frame
        yield return null;
        if (mandrake == null) yield break;
        float before = mandrake.waitBeforeScreamTime;
        mandrake.waitBeforeScreamTime = Mathf.Max(0f, Plugin.ModConfig.MandrakeFirstScreamSeconds.Synced());
        Plugin.Log.LogInfo($"[OTL][Mandrake] first scream in {mandrake.waitBeforeScreamTime:0.##}s (vanilla {before:0.#}s).");
    }
}

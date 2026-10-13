using System.Collections;
using System.Linq;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Host: from MandrakeStartSeconds after the head start ends, every MandrakeIntervalSeconds a mandrake is
/// dropped on the ground at every living runner who isn't in a safe zone (not into their hands), so a
/// runner who stays put gets noisy company. Spawned directly (not through a spawner), so the item rules
/// never remove it.
/// </summary>
internal static class MandrakeDrops
{
    private static int s_LegKey = -1;
    private static int s_LastWave;

    public static bool Install()
    {
        ModNetwork.HostTicked += HostTick;
        ModNetwork.NoticeReceived += (notice, a, _) =>
        {
            if (notice == Notice.MandrakeBoom) PlayBoom(a);
        };
        return true;
    }

    private static void HostTick()
    {
        var cfg = Plugin.ModConfig;
        float start = cfg.MandrakeStartSeconds.Synced(), interval = Mathf.Max(5f, cfg.MandrakeIntervalSeconds.Synced());
        if (!Net.IsHost || !RoundManager.IsChasing || start <= 0f) return;

        if (RoundManager.LegKey != s_LegKey)
        {
            s_LegKey = RoundManager.LegKey;
            s_LastWave = 0;
        }

        float elapsed = RoundManager.ChaseElapsedSeconds;
        if (elapsed < start) return;
        int wave = Mathf.FloorToInt((elapsed - start) / interval) + 1;
        if (wave <= s_LastWave) return;
        s_LastWave = wave;

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
            if (cfg.MandrakeExplosionSound.Synced()) ModNetwork.Broadcast(Notice.MandrakeBoom, Net.Actor(c), 0);
            spawned++;
        }

        Plugin.Log.LogInfo($"[OTL][Mandrake] HOST wave {wave}: {spawned} mandrake(s) dropped.");
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

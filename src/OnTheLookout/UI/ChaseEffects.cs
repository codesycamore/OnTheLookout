using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OnTheLookout.Core;
using OnTheLookout.Freeze;
using OnTheLookout.Modules;
using UnityEngine;
using Zorro.Core;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace OnTheLookout.UI;

/// <summary>
/// Chase atmosphere, computed on every client from replicated state (no extra network traffic):
/// - fireworks: every FireworkIntervalSeconds of an active chase, a burst goes off high above each
///   living chaser so runners get a sense of where they are. It's the rocket pack's mid-air explosion
///   (fireworks-like), cloned locally with its damage/knockback (AOE) and physics stripped, so it is harmless;
/// - Scoutmaster sounds: while a chaser is within freeze range of a living runner, sounds from the
///   Scoutmaster's prefab play at the chaser now and then. "His" sounds are the ones on
///   Character_Scoutmaster that a normal player (Character) doesn't have.
/// </summary>
internal sealed class ChaseEffects : MonoBehaviour
{
    private GameObject? _fireworkTemplate;
    private bool _fireworkLoaded;
    private int _lastBurst = -1;
    private int _legKey = -1;

    private List<SFX_Instance>? _smSfx;
    private List<AudioClip>? _smClips;
    private readonly Dictionary<int, float> _nextGrowl = new();

    private void Update()
    {
        if (!RoundManager.IsChasing)
        {
            _nextGrowl.Clear();
            return;
        }

        UpdateFireworks();
        UpdateScoutmasterSounds();
    }

    // ---------- Fireworks ----------

    private void UpdateFireworks()
    {
        float interval = Plugin.ModConfig.FireworkIntervalSeconds.Synced();
        if (interval <= 0f) return;

        if (RoundManager.LegKey != _legKey)
        {
            _legKey = RoundManager.LegKey;
            _lastBurst = 0;
        }

        int burst = Mathf.FloorToInt(RoundManager.ChaseElapsedSeconds / interval);
        if (burst <= _lastBurst) return;
        _lastBurst = burst;

        float height = Plugin.ModConfig.FireworkHeight.Synced();
        foreach (Character c in Character.AllCharacters)
        {
            if (RoleManager.IsChaser(c) && !c.data.dead) SpawnFirework(c.Center + Vector3.up * height);
        }
    }

    private void SpawnFirework(Vector3 position)
    {
        GameObject? template = FireworkTemplate();
        if (template == null) return;
        GameObject burst = Instantiate(template, position, Quaternion.identity);
        burst.SetActive(true);
        Destroy(burst, 10f);
    }

    /// <summary>
    /// A harmless copy of the rocket pack's mid-air explosion (CharacterMovement.explosionPrefab, spawned by
    /// RocketExplodeRPC), which looks like fireworks: visuals and sound only. Falls back to the dynamite explosion.
    /// </summary>
    private GameObject? FireworkTemplate()
    {
        if (_fireworkLoaded) return _fireworkTemplate;

        GameObject? source = Character.AllCharacters
            .Select(c => c != null && c.refs.movement != null ? c.refs.movement.explosionPrefab : null)
            .FirstOrDefault(p => p != null);
        if (source == null && Character.AllCharacters.Count == 0) return null; // no character to read it from yet; try again later
        _fireworkLoaded = true;

        if (source == null)
        {
            source = ItemCatalogDynamite()?.explosionPrefab;
            Plugin.Log.LogWarning("[OTL][Effects] rocket pack explosion not found; using the dynamite explosion instead.");
        }

        if (source == null)
        {
            Plugin.Log.LogWarning("[OTL][Effects] no explosion prefab found; no fireworks.");
            return null;
        }

        var holder = new GameObject("OTL_FireworkTemplateHolder");
        holder.SetActive(false); // nothing on the template runs
        DontDestroyOnLoad(holder);
        GameObject template = Instantiate(source, holder.transform);
        template.name = "OTL_Firework";

        int removed = 0;
        foreach (Component component in template.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform) continue;
            string type = component.GetType().Name;
            bool harmful = component is AOE || component is Collider || component is Rigidbody || component is Joint
                || new[] { "AOE", "Damage", "Knock", "Force", "Status", "Affliction", "Shake", "Break", "Fall" }
                    .Any(word => type.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!harmful) continue;
            DestroyImmediate(component);
            removed++;
        }

        _fireworkTemplate = template;
        Plugin.Log.LogInfo($"[OTL][Effects] firework built from {source.name} ({removed} damaging/physics component(s) removed).");
        return template;
    }

    private static Dynamite? ItemCatalogDynamite()
    {
        ItemDatabase db = SingletonAsset<ItemDatabase>.Instance;
        return db?.itemLookup.Values.Select(i => i != null ? i.GetComponentInChildren<Dynamite>(true) : null).FirstOrDefault(d => d != null);
    }

    // ---------- Scoutmaster sounds ----------

    private void UpdateScoutmasterSounds()
    {
        var cfg = Plugin.ModConfig;
        if (!cfg.ScoutmasterSounds.Synced()) return;
        LoadScoutmasterSounds();
        if ((_smSfx?.Count ?? 0) + (_smClips?.Count ?? 0) == 0) return;

        float range = cfg.FreezeRange.Synced();
        foreach (Character chaser in Character.AllCharacters)
        {
            if (!RoleManager.IsChaser(chaser) || chaser.data.dead) continue;
            int actor = Net.Actor(chaser);
            bool near = Character.AllCharacters.Any(r => RoleManager.IsRunner(r) && !r.data.dead
                && Vector3.Distance(r.Center, chaser.Center) <= range);
            if (!near || FreezeState.IsFrozen(actor))
            {
                _nextGrowl.Remove(actor);
                continue;
            }

            if (!_nextGrowl.TryGetValue(actor, out float next))
            {
                _nextGrowl[actor] = Time.time + Random.Range(0.2f, 1f); // first sound shortly after closing in
                continue;
            }

            if (Time.time < next) continue;
            _nextGrowl[actor] = Time.time + Random.Range(cfg.ScoutmasterSoundMinInterval.Synced(), cfg.ScoutmasterSoundMaxInterval.Synced());
            PlayScoutmasterSound(chaser.Center);
        }
    }

    private void PlayScoutmasterSound(Vector3 position)
    {
        int sfxCount = _smSfx?.Count ?? 0, clipCount = _smClips?.Count ?? 0;
        int pick = Random.Range(0, sfxCount + clipCount);
        if (pick < sfxCount)
        {
            if (SFX_Player.instance != null) _smSfx![pick].Play(position);
        }
        else
        {
            AudioSource.PlayClipAtPoint(_smClips![pick - sfxCount], position, 1f);
        }
    }

    private void LoadScoutmasterSounds()
    {
        if (_smSfx != null) return;
        _smSfx = new List<SFX_Instance>();
        _smClips = new List<AudioClip>();
        try
        {
            GameObject? scoutmaster = Resources.Load<GameObject>("Character_Scoutmaster");
            GameObject? player = Resources.Load<GameObject>("Character");
            if (scoutmaster == null)
            {
                Plugin.Log.LogWarning("[OTL][Effects] Scoutmaster prefab not found; no Scoutmaster sounds.");
                return;
            }

            (HashSet<SFX_Instance> smSfx, HashSet<AudioClip> smClips) = CollectSounds(scoutmaster);
            if (player != null)
            {
                (HashSet<SFX_Instance> pSfx, HashSet<AudioClip> pClips) = CollectSounds(player);
                smSfx.ExceptWith(pSfx);
                smClips.ExceptWith(pClips);
            }

            _smSfx.AddRange(smSfx);
            _smClips.AddRange(smClips);
            Plugin.Log.LogInfo($"[OTL][Effects] Scoutmaster sounds: {string.Join(", ", _smSfx.Select(s => s.name).Concat(_smClips.Select(c => c.name)))}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"[OTL][Effects] couldn't load Scoutmaster sounds: {e.Message}");
        }
    }

    /// <summary>Every sound asset referenced by a prefab: SFX_Instance fields (incl. arrays/lists) and AudioSource clips.</summary>
    private static (HashSet<SFX_Instance>, HashSet<AudioClip>) CollectSounds(GameObject prefab)
    {
        var sfx = new HashSet<SFX_Instance>();
        var clips = new HashSet<AudioClip>();
        foreach (AudioSource source in prefab.GetComponentsInChildren<AudioSource>(true))
        {
            if (source.clip != null) clips.Add(source.clip);
        }

        foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            for (Type? t = component.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    object? value;
                    try { value = field.GetValue(component); }
                    catch { continue; }
                    Add(value, sfx, clips);
                }
            }
        }

        return (sfx, clips);
    }

    private static void Add(object? value, HashSet<SFX_Instance> sfx, HashSet<AudioClip> clips)
    {
        switch (value)
        {
            case SFX_Instance s when s != null:
                sfx.Add(s);
                break;
            case AudioClip c when c != null:
                clips.Add(c);
                break;
            case IEnumerable list and not string:
                foreach (object? element in list)
                {
                    if (element is SFX_Instance or AudioClip) Add(element, sfx, clips);
                }

                break;
        }
    }
}

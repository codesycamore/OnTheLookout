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
/// - red glow: every living chaser's body glows red (ChaserRedOutline);
/// - chase music: PEAK's own Scoutmaster fear music for a runner with a chaser closing in (ScoutmasterChaseMusic);
/// - Scoutmaster sounds (off by default): while a chaser is within freeze range of a living runner, sounds from the
///   Scoutmaster's prefab play at the chaser now and then. "His" sounds are the ones on
///   Character_Scoutmaster that a normal player (Character) doesn't have.
/// </summary>
internal sealed class ChaseEffects : MonoBehaviour
{

    private List<SFX_Instance>? _smSfx;
    private List<AudioClip>? _smClips;
    private readonly Dictionary<int, float> _nextGrowl = new();

    private void Update()
    {
        UpdateChaserGlow();
        if (!RoundManager.IsChasing)
        {
            _nextGrowl.Clear();
            return;
        }

        UpdateScoutmasterSounds();
    }

    // ---------- Red glow on chasers ----------

    private static readonly Color ChaserGlow = new(1f, 0.08f, 0.05f);
    private float _nextGlow;

    /// <summary>
    /// Every living chaser glows red, on every client: PEAK's own status glow on the character's body
    /// (CharacterCustomization.PulseStatus), refreshed before it fades, the way the yellow invincibility glow of
    /// fortified milk is kept up (Affliction_Invincibility.UpdateEffectNetworked). A frozen chaser shows the
    /// icy freeze pulses instead, and an invincible one PEAK's own glow.
    /// </summary>
    private void UpdateChaserGlow()
    {
        if (!RoundManager.IsActive || !Plugin.ModConfig.ChaserRedOutline.Synced() || Time.time < _nextGlow) return;
        _nextGlow = Time.time + 0.1f;
        float intensity = Mathf.Clamp(Plugin.ModConfig.ChaserGlowIntensity.Synced(), 0f, 2f);
        foreach (Character c in Character.AllCharacters)
        {
            if (c == null || c.data.dead || !RoleManager.IsChaser(c) || c.data.isInvincible || FreezeState.IsFrozen(Net.Actor(c))) continue;
            c.refs.customization.PulseStatus(ChaserGlow, intensity);
        }
    }

    // ---------- Chase music (vanilla Scoutmaster fear music) ----------

    private bool _fearChecked;

    /// <summary>
    /// PEAK's own "being chased" music: every character has a MyresAmbience whose looping fearMusic fades in
    /// as the animator float "Myers Distance" drops below 50 m (louder below 25 m). The Scoutmaster drives it
    /// by writing his distance into his target's CharacterData.myersDistance each frame
    /// (Scoutmaster.DoVisuals), and CharacterAnimations copies it to the animator and resets it to 1000.
    /// We do the same for the local runner with the nearest living chaser, so it plays (continuously, with
    /// vanilla fading) exactly like being hunted by the Scoutmaster.
    /// </summary>
    private void LateUpdate()
    {
        if (!RoundManager.IsChasing || !Plugin.ModConfig.ScoutmasterChaseMusic.Synced()) return;
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || !RoleManager.IsRunner(local)) return;

        float nearest = float.MaxValue;
        foreach (Character c in Character.AllCharacters)
        {
            if (c == null || c.data.dead || !RoleManager.IsChaser(c)) continue;
            nearest = Mathf.Min(nearest, Vector3.Distance(c.Center, local.Center));
        }

        if (nearest == float.MaxValue) return;
        if (!_fearChecked)
        {
            _fearChecked = true;
            Plugin.Log.LogInfo($"[OTL][Effects] chase music: MyresAmbience on the local character = {local.GetComponentInChildren<MyresAmbience>(true) != null}.");
        }

        local.data.myersDistance = Mathf.Max(0.1f, nearest); // 0 means "off" to MyresAmbience
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

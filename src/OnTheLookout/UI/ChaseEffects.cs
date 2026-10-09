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
/// - chase music: PEAK's own Scoutmaster fear music for a runner with a chaser closing in (ScoutmasterChaseMusic).
///   Only the runner's own client plays it; chasers never get any Scoutmaster audio.
/// </summary>
internal sealed class ChaseEffects : MonoBehaviour
{
    private void Update() => UpdateChaserGlow();

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
}

using System.Collections.Generic;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OnTheLookout.Freeze;

/// <summary>
/// Rules 3-5. Host: every runner looking at a chaser (range, cone, line of sight) freezes that chaser,
/// with no stacking and a per-chaser cooldown (both enforced in <see cref="FreezeState"/>).
/// Every client: enforces its own freeze (input block in <see cref="FreezeInputPatch"/>, stamina lock,
/// optional velocity zeroing, mid-air suspension) and plays the cold pulses on frozen players.
/// Also hosts the debug keys.
/// </summary>
internal sealed class FreezeSystem : MonoBehaviour
{
    private const float HostCheckInterval = 0.1f;

    private static float s_LockedStamina = -1f;

    /// <summary>Rules 3-5 on/off (EnableFreeze). The head-start hold and debug keys work either way.</summary>
    public static bool LookChecksEnabled { get; set; } = true;

    private readonly Dictionary<int, float> _nextPulse = new();
    private float _nextHostCheck;
    private float _nextScreenFrost;

    // ---------- Look check (runs on host, uses replicated look direction) ----------

    /// <summary>
    /// True if <paramref name="looker"/> is looking at <paramref name="target"/>: within range,
    /// within the look cone, and with no terrain/map geometry in between.
    /// Remote players' data.lookDirection is rebuilt every frame from the 30 Hz replicated
    /// lookValues (CharacterSyncer -> CharacterMovement.CameraLook -> RecalculateLookDirections).
    /// </summary>
    public static bool IsLookingAt(Character looker, Character target)
    {
        Vector3 eye = looker.GetBodypart(BodypartType.Head).transform.position;
        Vector3 toTarget = target.Center - eye;
        float distance = toTarget.magnitude;
        if (distance > Plugin.ModConfig.FreezeRange.Synced()) return false;

        float angle = Vector3.Angle(looker.data.lookDirection, toTarget);
        if (angle > Plugin.ModConfig.FreezeConeDegrees.Synced()) return false;

        bool occluded = Physics.Linecast(eye, target.Center, HelperFunctions.terrainMapMask);
        if (Plugin.ModConfig.LogLookChecks.Value)
        {
            Plugin.Log.LogInfo($"[OTL][Freeze] look {Name(looker)} -> {Name(target)}: dist={distance:0.0}m angle={angle:0.0}deg occluded={occluded}");
        }

        return !occluded;
    }

    private static bool CanAct(Character c) =>
        c != null && !c.isBot && !c.data.dead && !c.data.passedOut && !c.data.fullyPassedOut;

    private static string Name(Character c) => $"{c.characterName}#{Net.Actor(c)}";

    private static bool TryFreeze(Character c, string reason) =>
        FreezeState.TryFreeze(Net.Actor(c), Plugin.ModConfig.FreezeDuration.Synced(),
            Plugin.ModConfig.FreezeCooldownSeconds.Synced(), reason);

    private void Update()
    {
        if (!Net.InRoom) return;

        if (Net.IsHost)
        {
            if (Plugin.ModConfig.DebugKeys.Value) DebugKeys();
            if (Time.time >= _nextHostCheck)
            {
                _nextHostCheck = Time.time + HostCheckInterval;
                HostLookChecks();
            }
        }

        PulseFrozenCharacters();
    }

    /// <summary>Host: rules 3-5 for every chaser/runner pair.</summary>
    private static void HostLookChecks()
    {
        if (!LookChecksEnabled || !RoundManager.IsChasing) return;

        foreach (Character chaser in Character.AllCharacters)
        {
            if (!RoleManager.IsChaser(chaser) || !CanAct(chaser)) continue;
            int actor = Net.Actor(chaser);
            if (FreezeState.IsFrozen(actor) || FreezeState.IsOnCooldown(actor)) continue; // rules 4 and 5

            foreach (Character runner in Character.AllCharacters)
            {
                if (!RoleManager.IsRunner(runner) || !CanAct(runner) || !IsLookingAt(runner, chaser)) continue;
                TryFreeze(chaser, $"runner {Name(runner)} looked at chaser");
                break;
            }
        }
    }

    private static void DebugKeys()
    {
        Keyboard? kb = Keyboard.current;
        Character local = Character.localCharacter;
        if (kb is null || local == null) return;
        var cfg = Plugin.ModConfig;

        if (kb[cfg.KeyStartRound.Value].wasPressedThisFrame)
        {
            RoundManager.HostStartRound();
        }

        if (kb[cfg.KeyToggleOwnRole.Value].wasPressedThisFrame)
        {
            if (!RoundManager.IsActive) RoundManager.HostStartRound();
            int me = Net.Actor(local);
            RoleManager.SetRole(me, RoleManager.RoleOf(me) == Role.Chaser ? Role.Runner : Role.Chaser);
        }

        if (kb[cfg.KeySelfFreeze.Value].wasPressedThisFrame && !TryFreeze(local, "debug self-freeze"))
        {
            LogRejected(local);
        }

        if (kb[cfg.KeyFreezeLookTarget.Value].wasPressedThisFrame)
        {
            Character? best = null;
            float bestAngle = float.MaxValue;
            foreach (Character other in Character.AllCharacters)
            {
                if (other == local || !CanAct(other) || !IsLookingAt(local, other)) continue;
                float angle = Vector3.Angle(local.data.lookDirection, other.Center - local.Center);
                if (angle < bestAngle) { best = other; bestAngle = angle; }
            }

            if (best is null) Plugin.Log.LogInfo("[OTL][Freeze] debug: nobody in range/cone/line of sight.");
            else if (!TryFreeze(best, $"debug: host looked at {Name(best)}")) LogRejected(best);
        }
    }

    private static void LogRejected(Character c)
    {
        int a = Net.Actor(c);
        Plugin.Log.LogInfo($"[OTL][Freeze] freeze on {Name(c)} rejected: frozen {FreezeState.FrozenSecondsLeft(a):0.0}s, cooldown {FreezeState.CooldownSecondsLeft(a):0.0}s left");
    }

    // ---------- Cold pulses (every client draws every frozen player) ----------

    /// <summary>
    /// Uses PEAK's own status flash (CharacterCustomization.PulseStatus with the game's cold colour),
    /// fast at the start of the freeze and slowing down as it runs out, so runners can read the timer.
    /// </summary>
    private void PulseFrozenCharacters()
    {
        float duration = Mathf.Max(0.1f, Plugin.ModConfig.FreezeDuration.Synced());
        float fast = Plugin.ModConfig.FreezePulseFastInterval.Synced();
        float slow = Plugin.ModConfig.FreezePulseSlowInterval.Synced();

        foreach (Character c in Character.AllCharacters)
        {
            if (c == null || c.refs.customization == null) continue;
            int actor = Net.Actor(c);
            if (!FreezeState.IsFrozen(actor))
            {
                _nextPulse.Remove(actor);
                continue;
            }

            float t = 1f - Mathf.Clamp01(FreezeState.FrozenSecondsLeft(actor) / duration); // 0 = just frozen, 1 = about to thaw
            if (_nextPulse.TryGetValue(actor, out float next) && Time.time < next) continue;

            _nextPulse[actor] = Time.time + Mathf.Lerp(fast, slow, t * t);
            c.refs.customization.PulseStatus(c.refs.afflictions.colorCold, Mathf.Lerp(1.2f, 0.5f, t));

            if (c.IsLocal && Plugin.ModConfig.FreezeScreenFrost.Value && Time.time >= _nextScreenFrost && GUIManager.instance != null)
            {
                // PEAK's cold screen effect (frost volume + shiver) without touching the cold status/stamina.
                _nextScreenFrost = Time.time + Mathf.Max(0.6f, Mathf.Lerp(fast, slow, t) * 2f);
                GUIManager.instance.ColdFX(1f);
            }
        }
    }

    // ---------- Local enforcement ----------

    public static void OnLocalFreezeStarted(Character local)
    {
        s_LockedStamina = local.data.currentStamina;
        Plugin.Log.LogInfo($"[OTL][Freeze] LOCAL frozen. climbing={local.data.isClimbing} grounded={local.data.isGrounded} stamina={s_LockedStamina:0.00}");
        FreezeSuspendPatch.TryBegin(local);
    }

    public static void OnLocalFreezeEnded(Character local)
    {
        Plugin.Log.LogInfo($"[OTL][Freeze] LOCAL unfrozen. climbing={local.data.isClimbing} stamina={local.data.currentStamina:0.00}");
        s_LockedStamina = -1f;
        FreezeSuspendPatch.End();
    }

    private static bool LocalFrozen(out Character local)
    {
        local = Character.localCharacter;
        return local != null && Net.InRoom
            && (FreezeState.IsFrozen(Net.Actor(local)) || (RoundManager.InHold && RoleManager.IsChaser(local)));
    }

    private void LateUpdate()
    {
        // Climbing drains a minimum amount of stamina even with zero input (CharacterClimbing.Update).
        if (Plugin.ModConfig.FreezeLockStamina.Synced() && s_LockedStamina >= 0f && LocalFrozen(out Character local)
            && local.data.currentStamina < s_LockedStamina)
        {
            local.data.currentStamina = s_LockedStamina;
        }
    }

    private void FixedUpdate()
    {
        if (!Plugin.ModConfig.FreezeZeroVelocity.Synced() || !LocalFrozen(out Character local)) return;

        foreach (Bodypart part in local.refs.ragdoll.partList)
        {
            if (part != null && part.Rig != null)
            {
                part.Rig.linearVelocity = Vector3.zero;
                part.Rig.angularVelocity = Vector3.zero;
            }
        }
    }

    // ---------- Debug hint ----------

    private void OnGUI()
    {
        if (!Plugin.ModConfig.DebugKeys.Value || !Net.InRoom || !PhotonNetwork.IsMasterClient || Character.localCharacter == null) return;
        var cfg = Plugin.ModConfig;
        GUI.Label(new Rect(10, Screen.height - 30, 900, 25),
            $"[OTL debug] {cfg.KeyStartRound.Value}: start round   {cfg.KeyToggleOwnRole.Value}: swap my role   {cfg.KeySelfFreeze.Value}: freeze me   {cfg.KeyFreezeLookTarget.Value}: freeze looked-at   {cfg.KeyRestartFromCampfire.Value}: restart from last campfire");
    }
}

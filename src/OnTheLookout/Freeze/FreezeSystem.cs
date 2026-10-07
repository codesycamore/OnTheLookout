using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OnTheLookout.Freeze;

/// <summary>
/// Freeze proof of concept (rules 3-5).
/// Host: decides freezes (look check, no stacking, cooldown) and replicates via <see cref="FreezeState"/>.
/// Every client: enforces its own freeze (input block in <see cref="FreezeInputPatch"/>, plus
/// optional stamina lock and velocity zeroing here) and draws a debug overlay.
/// </summary>
internal sealed class FreezeSystem : MonoBehaviourPunCallbacks
{
    private const float HostCheckInterval = 0.1f;

    private static float s_LockedStamina = -1f;

    private float _nextHostCheck;

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
        float angle = Vector3.Angle(looker.data.lookDirection, toTarget);
        bool inRange = distance <= Plugin.ModConfig.FreezeRange.Value;
        bool inCone = angle <= Plugin.ModConfig.FreezeConeDegrees.Value;
        bool occluded = inRange && inCone && Physics.Linecast(eye, target.Center, HelperFunctions.terrainMapMask);

        if (Plugin.ModConfig.LogLookChecks.Value)
        {
            Plugin.Log.LogInfo($"[OTL][Freeze] look {Name(looker)} -> {Name(target)}: dist={distance:0.0}m angle={angle:0.0}deg occluded={occluded}");
        }

        return inRange && inCone && !occluded;
    }

    private static bool CanAct(Character c) =>
        c != null && !c.isBot && !c.data.dead && !c.data.passedOut && !c.data.fullyPassedOut;

    private static int Actor(Character c) => c.view.Owner.ActorNumber;

    private static string Name(Character c) => $"{c.characterName}#{Actor(c)}";

    // ---------- Host logic ----------

    private void Update()
    {
        Keyboard? kb = Keyboard.current;
        Character local = Character.localCharacter;
        if (kb is null || local == null || !PhotonNetwork.InRoom)
        {
            return;
        }

        bool isHost = PhotonNetwork.IsMasterClient;
        ModConfigShortcuts(kb, local, isHost);

        if (isHost && Plugin.ModConfig.HostIsChaserTest.Value && Time.time >= _nextHostCheck)
        {
            _nextHostCheck = Time.time + HostCheckInterval;
            HostAsChaserCheck(local);
        }
    }

    private static void ModConfigShortcuts(Keyboard kb, Character local, bool isHost)
    {
        var cfg = Plugin.ModConfig;

        if (kb[cfg.KeySelfFreeze.Value].wasPressedThisFrame)
        {
            if (!RequireHost(isHost)) return;
            bool ok = FreezeState.TryFreeze(Actor(local), cfg.FreezeDuration.Value, cfg.FreezeCooldownSeconds.Value, "debug self-freeze");
            if (!ok) LogRejected(local);
        }

        if (kb[cfg.KeyFreezeLookTarget.Value].wasPressedThisFrame)
        {
            if (!RequireHost(isHost)) return;
            Character? best = null;
            float bestAngle = float.MaxValue;
            foreach (Character other in Character.AllCharacters)
            {
                if (other == local || !CanAct(other) || !IsLookingAt(local, other)) continue;
                float angle = Vector3.Angle(local.data.lookDirection, other.Center - local.Center);
                if (angle < bestAngle) { best = other; bestAngle = angle; }
            }

            if (best is null)
            {
                Plugin.Log.LogInfo("[OTL][Freeze] F9: nobody in range/cone/line of sight.");
            }
            else if (!FreezeState.TryFreeze(Actor(best), cfg.FreezeDuration.Value, cfg.FreezeCooldownSeconds.Value, $"host looked at {Name(best)}"))
            {
                LogRejected(best);
            }
        }

        if (kb[cfg.KeyToggleHostIsChaser.Value].wasPressedThisFrame)
        {
            if (!RequireHost(isHost)) return;
            cfg.HostIsChaserTest.Value = !cfg.HostIsChaserTest.Value;
            Plugin.Log.LogInfo($"[OTL][Freeze] HostIsChaserTest = {cfg.HostIsChaserTest.Value}");
        }
    }

    /// <summary>Rules 3-5 end to end, with the host as the only chaser and everyone else as runners.</summary>
    private static void HostAsChaserCheck(Character chaser)
    {
        int chaserActor = Actor(chaser);
        if (!CanAct(chaser) || FreezeState.IsFrozen(chaserActor) || FreezeState.IsOnCooldown(chaserActor))
        {
            return; // rule 4 (no stacking) and rule 5 (cooldown) short-circuit before any look check
        }

        foreach (Character runner in Character.AllCharacters)
        {
            if (runner == chaser || !CanAct(runner)) continue;
            if (IsLookingAt(runner, chaser))
            {
                var cfg = Plugin.ModConfig;
                FreezeState.TryFreeze(chaserActor, cfg.FreezeDuration.Value, cfg.FreezeCooldownSeconds.Value, $"runner {Name(runner)} looked at chaser");
                return;
            }
        }
    }

    private static bool RequireHost(bool isHost)
    {
        if (!isHost) Plugin.Log.LogWarning("[OTL][Freeze] Debug freeze keys are host-only.");
        return isHost;
    }

    private static void LogRejected(Character c)
    {
        int a = Actor(c);
        Plugin.Log.LogInfo($"[OTL][Freeze] freeze on {Name(c)} rejected: frozen={FreezeState.IsFrozen(a)} ({FreezeState.FrozenSecondsLeft(a):0.0}s), cooldown={FreezeState.IsOnCooldown(a)} ({FreezeState.CooldownSecondsLeft(a):0.0}s)");
    }

    // ---------- Local enforcement (every client) ----------

    public static void OnLocalFreezeStarted(Character local)
    {
        s_LockedStamina = local.data.currentStamina;
        Plugin.Log.LogInfo($"[OTL][Freeze] LOCAL frozen. climbing={local.data.isClimbing} grounded={local.data.isGrounded} rope={local.data.isRopeClimbing} vine={local.data.isVineClimbing} stamina={s_LockedStamina:0.00}");
        FreezeSuspendPatch.TryBegin(local);
    }

    public static void OnLocalFreezeEnded(Character local)
    {
        Plugin.Log.LogInfo($"[OTL][Freeze] LOCAL unfrozen. climbing={local.data.isClimbing} stamina={local.data.currentStamina:0.00} (locked at {s_LockedStamina:0.00})");
        s_LockedStamina = -1f;
        FreezeSuspendPatch.End();
    }

    private static bool LocalFrozen(out Character local)
    {
        local = Character.localCharacter;
        return local != null && PhotonNetwork.InRoom && FreezeState.IsFrozen(Actor(local));
    }

    private void LateUpdate()
    {
        // Climbing drains a minimum amount of stamina even with zero input (CharacterClimbing.Update),
        // so without this a chaser frozen on a wall would eventually fall.
        if (Plugin.ModConfig.FreezeLockStamina.Value && s_LockedStamina >= 0f && LocalFrozen(out Character local)
            && local.data.currentStamina < s_LockedStamina)
        {
            local.data.currentStamina = s_LockedStamina;
        }
    }

    private void FixedUpdate()
    {
        if (!Plugin.ModConfig.FreezeZeroVelocity.Value || !LocalFrozen(out Character local))
        {
            return;
        }

        foreach (Bodypart part in local.refs.ragdoll.partList)
        {
            if (part != null && part.Rig != null)
            {
                part.Rig.linearVelocity = Vector3.zero;
                part.Rig.angularVelocity = Vector3.zero;
            }
        }
    }

    // ---------- Replication ----------

    public override void OnJoinedRoom()
    {
        FreezeState.Clear();
        FreezeState.OnRoomPropertiesUpdate(PhotonNetwork.CurrentRoom.CustomProperties);
    }

    public override void OnLeftRoom()
    {
        FreezeState.Clear();
        FreezeSuspendPatch.End();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) =>
        FreezeState.OnRoomPropertiesUpdate(propertiesThatChanged);

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient) =>
        Plugin.Log.LogInfo($"[OTL][Freeze] master switched to actor {newMasterClient.ActorNumber}; freeze table persists in room props.");

    // ---------- Debug overlay (IMGUI; proof of concept only) ----------

    private GUIStyle? _big;

    private void OnGUI()
    {
        Character local = Character.localCharacter;
        if (local == null || !PhotonNetwork.InRoom) return;

        _big ??= new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
        int a = Actor(local);
        string text =
            FreezeState.IsFrozen(a) ? $"<color=#7fd4ff>FROZEN {FreezeState.FrozenSecondsLeft(a):0.0}s</color>"
            : FreezeState.IsOnCooldown(a) ? $"<color=#cccccc>freeze cooldown {FreezeState.CooldownSecondsLeft(a):0.0}s</color>"
            : string.Empty;
        if (text.Length > 0)
        {
            GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, 50), text, _big);
        }

        if (PhotonNetwork.IsMasterClient)
        {
            var cfg = Plugin.ModConfig;
            GUI.Label(new Rect(10, 10, 600, 25),
                $"[OTL] {cfg.KeySelfFreeze.Value}: self-freeze  {cfg.KeyFreezeLookTarget.Value}: freeze looked-at  {cfg.KeyToggleHostIsChaser.Value}: HostIsChaser={(cfg.HostIsChaserTest.Value ? "ON" : "off")}");
        }
    }
}

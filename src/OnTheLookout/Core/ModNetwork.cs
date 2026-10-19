using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;
using OnTheLookout.Freeze;
using OnTheLookout.Modules;
using Photon.Pun;

using UnityEngine;

namespace OnTheLookout.Core;

/// <summary>
/// The single Photon callback hub. Routes room-property updates, raw events and host ticks to the
/// static modules so each module stays a plain class. Also performs the version handshake.
/// </summary>
internal sealed class ModNetwork : MonoBehaviourPunCallbacks, Photon.Realtime.IOnEventCallback
{
    public const string VersionKey = "otl.ver";

    /// <summary>Room property: the host's OnTheLookout version, so a joining player with another version can tell.</summary>
    public const string HostVersionKey = "otl.hver";

    /// <summary>Host: actors warned about a missing or different mod version (cleared when they fix it or leave).</summary>
    private static readonly HashSet<int> s_Warned = new();

    /// <summary>The host's version when it differs from ours (null: same version, or no host version known).</summary>
    public static string? HostVersionMismatch =>
        Net.InRoom && !Net.IsHost && Net.GetRoom(HostVersionKey) is string host && host != Plugin.Version ? host : null;
    private const float HostTickInterval = 0.5f;

    public static ModNetwork? Instance { get; private set; }

    /// <summary>Raised on every client for host notices.</summary>
    public static event Action<Notice, int, int>? NoticeReceived;

    /// <summary>Host only, ~2x per second, after the built-in host ticks.</summary>
    public static event Action? HostTicked;

    private float _nextHostTick;

    private void Awake() => Instance = this;

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Update()
    {
        if (!Net.InRoom || !Net.IsHost || Time.time < _nextHostTick) return;
        _nextHostTick = Time.time + HostTickInterval;

        Safe(RoundManager.HostTick);
        Safe(RewardSystem.HostTick);
        Safe(ZombieHunt.HostTick);
        if (HostTicked != null) Safe(HostTicked);
    }

    // ---------- Room lifecycle ----------

    public override void OnJoinedRoom()
    {
        ClearAll();
        Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        ConfigSync.OnRoomPropertiesUpdate(props);
        RoleManager.OnRoomPropertiesUpdate(props);
        RoundManager.OnRoomPropertiesUpdate(props);
        FreezeState.OnRoomPropertiesUpdate(props);
        RewardSystem.OnRoomPropertiesUpdate(props);

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [VersionKey] = Plugin.Version });
        s_Warned.Clear();
        if (Net.IsHost)
        {
            ConfigSync.Publish();
            Net.SetRoom(HostVersionKey, Plugin.Version);
        }
        else if (HostVersionMismatch is { } host)
        {
            Plugin.Log.LogWarning($"[OTL][Net] the host has OnTheLookout {host}, this game has {Plugin.Version}.");
        }

        Plugin.Log.LogInfo($"[OTL][Net] joined room as actor {PhotonNetwork.LocalPlayer.ActorNumber} (host={Net.IsHost}).");
    }

    public override void OnLeftRoom() => ClearAll();

    private static void ClearAll()
    {
        ConfigSync.Clear();
        RoleManager.Clear();
        RoundManager.Clear();
        FreezeState.Clear();
        FreezeSuspendPatch.End();
        RewardSystem.Clear();
        ZombieHunt.Clear();
        ChaserPreference.Clear();
    }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        ConfigSync.OnRoomPropertiesUpdate(changed);
        RoleManager.OnRoomPropertiesUpdate(changed);
        RoundManager.OnRoomPropertiesUpdate(changed);
        FreezeState.OnRoomPropertiesUpdate(changed);
        RewardSystem.OnRoomPropertiesUpdate(changed);
    }

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        Plugin.Log.LogInfo($"[OTL][Net] host migrated to {newMasterClient.NickName}#{newMasterClient.ActorNumber}; round state continues from room properties.");
        if (Net.IsHost)
        {
            // The new host needs the replicated tables as its own working copies.
            Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
            FreezeState.OnRoomPropertiesUpdate(props);
            RewardSystem.OnRoomPropertiesUpdate(props);
            Net.SetRoom(HostVersionKey, Plugin.Version);
        }
    }

    // ---------- Version handshake ----------

    public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
    {
        if (Net.IsHost)
        {
            StartCoroutine(CheckVersionLater(newPlayer));
        }
    }

    /// <summary>
    /// Host, 10 s after a player joins: a player whose game didn't report the host's version (VersionKey, set in their
    /// OnJoinedRoom) is shown to the host only, with whether they have no OnTheLookout or another version. The player is
    /// read again from the room (not the join-time object), and skipped if they have left or are inactive, so a player
    /// who left without the mod and rejoined with it (a new actor, checked on its own) is never reported from the old join.
    /// </summary>
    private IEnumerator CheckVersionLater(PhotonPlayer joined)
    {
        yield return new WaitForSeconds(10f);
        if (!Net.InRoom || !Net.IsHost) yield break;
        PhotonPlayer? player = PhotonNetwork.CurrentRoom.GetPlayer(joined.ActorNumber);
        if (player == null || player.IsInactive) yield break;

        player.CustomProperties.TryGetValue(VersionKey, out object? version);
        if (version as string == Plugin.Version) yield break;

        string have = version as string ?? "none";
        Plugin.Log.LogWarning($"[OTL][Net] {player.NickName}#{player.ActorNumber} has OnTheLookout version '{have}', host has '{Plugin.Version}'.");
        s_Warned.Add(player.ActorNumber);
        NoticeReceived?.Invoke(Notice.MissingMod, player.ActorNumber, version is string ? 1 : 0); // host only
        if (Plugin.ModConfig.KickPlayersWithoutMod.Value)
        {
            PlayerHandler.Kick(player.ActorNumber);
        }
    }

    /// <summary>Host: a warned player's version arrived late or changed to the host's: the host is told it's fine now.</summary>
    public override void OnPlayerPropertiesUpdate(PhotonPlayer target, Hashtable changed)
    {
        if (!Net.IsHost || !s_Warned.Contains(target.ActorNumber)) return;
        if (!changed.TryGetValue(VersionKey, out object? version) || version as string != Plugin.Version) return;
        s_Warned.Remove(target.ActorNumber);
        Plugin.Log.LogInfo($"[OTL][Net] {target.NickName}#{target.ActorNumber} now has OnTheLookout {Plugin.Version}.");
        NoticeReceived?.Invoke(Notice.ModInstalled, target.ActorNumber, 0); // host only
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        s_Warned.Remove(otherPlayer.ActorNumber);
        RoleManager.HostForget(otherPlayer.ActorNumber); // e.g. a chaser who left no longer shows in the chaser list
    }

    // ---------- Events ----------

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != Net.EventCode || photonEvent.CustomData is not object[] { Length: > 0 } data || data[0] is not byte type)
        {
            return;
        }

        try
        {
            switch ((Msg)type)
            {
                case Msg.TagClaim when Net.IsHost && data.Length >= 3:
                    TagSystem.HostHandleClaim((int)data[1], (int)data[2], photonEvent.Sender);
                    break;
                case Msg.ChaserPreference when Net.IsHost && data.Length >= 2:
                    ChaserPreference.HostSet(photonEvent.Sender, (ChaserPref)(byte)data[1]);
                    break;
                case Msg.PickUpItem when data.Length >= 2:
                    LegLoadout.PickUpLocal((int)data[1]);
                    break;
                case Msg.ClearHands:
                    LegLoadout.ClearLocalHands();
                    break;
                case Msg.DropAllItems:
                    RoleSwap.DropAllLocal(data.Length >= 2 && data[1] is true);
                    break;
                case Msg.DropChaserKit:
                    RoleSwap.DropChaserKitLocal();
                    break;
                case Msg.CaptureProgress when data.Length >= 4:
                    UI.CaptureIndicator.OnProgress((int)data[1], (int)data[2], (int)data[3]);
                    break;
                case Msg.SnowballHit when data.Length >= 6:
                    ChaserKit.ApplySnowballHit((int)data[1], new Vector3((float)data[2], (float)data[3], (float)data[4]), (int)data[5]);
                    break;
                case Msg.StoneHit when data.Length >= 2:
                    ThrowRules.ApplyStoneHit((int)data[1]);
                    break;
                case Msg.Notice when data.Length >= 4:
                    NoticeReceived?.Invoke((Notice)(byte)data[1], (int)data[2], (int)data[3]);
                    break;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[OTL][Net] bad event payload: {e}");
        }
    }

    public static void Broadcast(Notice notice, int a, int b) => Net.SendToAll(Msg.Notice, (byte)notice, a, b);

    private static void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[OTL] host tick error: {e}");
        }
    }
}

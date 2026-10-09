using System;
using System.Collections;
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
        if (Net.IsHost)
        {
            ConfigSync.Publish();
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

    private IEnumerator CheckVersionLater(PhotonPlayer player)
    {
        yield return new WaitForSeconds(10f);
        if (!Net.InRoom || !Net.IsHost || PhotonNetwork.CurrentRoom.GetPlayer(player.ActorNumber) is null) yield break;

        player.CustomProperties.TryGetValue(VersionKey, out object? version);
        if (version as string == Plugin.Version) yield break;

        string have = version as string ?? "none";
        Plugin.Log.LogWarning($"[OTL][Net] {player.NickName}#{player.ActorNumber} has OnTheLookout version '{have}', host has '{Plugin.Version}'.");
        NoticeReceived?.Invoke(Notice.MissingMod, player.ActorNumber, 0);
        if (Plugin.ModConfig.KickPlayersWithoutMod.Value)
        {
            PlayerHandler.Kick(player.ActorNumber);
        }
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
                    RoleSwap.DropAllLocal();
                    break;
                case Msg.DropChaserKit:
                    RoleSwap.DropChaserKitLocal();
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

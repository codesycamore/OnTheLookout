using System.Collections;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OnTheLookout.Modules;

/// <summary>
/// Host "admin" quick restart: brings everyone back to the last campfire the runners lit (or PEAK's
/// base-camp spawn before any campfire), revives the dead, clears statuses and starts a fresh leg
/// (role reveal, blind/frozen chasers, head start). Roles are kept.
/// The last lit campfire is kept in the room property <see cref="RoomKey"/> so a new host can use it.
/// </summary>
internal sealed class AdminRestart : MonoBehaviour
{
    public const string RoomKey = "otl.fire";

    /// <summary>Host: new round, forget the previous run's campfire.</summary>
    public static void HostForget() => Net.SetRoom(RoomKey, null!);

    /// <summary>Host: remember the campfire that was just lit.</summary>
    public static void HostRememberCampfire(Vector3 position) =>
        Net.SetRoom(RoomKey, new[] { position.x, position.y, position.z });

    private static Vector3? RestartPoint()
    {
        if (Net.GetRoom(RoomKey) is float[] { Length: 3 } p) return new Vector3(p[0], p[1], p[2]);
        try
        {
            return MapHandler.Exists ? MapHandler.CurrentBaseCampSpawnPoint.position : null;
        }
        catch
        {
            return null; // map not ready
        }
    }

    private void Update()
    {
        if (!Net.InRoom || !Net.IsHost || !Plugin.ModConfig.AdminKeys.Value) return;
        Keyboard? kb = Keyboard.current;
        if (kb != null && kb[Plugin.ModConfig.KeyRestartFromCampfire.Value].wasPressedThisFrame)
        {
            StartCoroutine(Restart());
        }
    }

    /// <summary>Host (menu): restart from the last lit campfire, same as the hotkey.</summary>
    public static void HostRestartFromCampfire()
    {
        if (Net.IsHost) ModNetwork.Instance?.StartCoroutine(Restart());
    }

    /// <summary>Host (menu): everyone alive is moved to the next unlit campfire. The dead stay dead.</summary>
    public static void HostTeleportToNextCampfire()
    {
        if (!Net.IsHost) return;
        Campfire? next = RoundManager.NextCampfire();
        if (next == null)
        {
            Plugin.Log.LogWarning("[OTL][Admin] teleport: no unlit campfire ahead.");
            return;
        }

        int i = 0;
        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (c == null || c.isBot || c.data.dead) continue;
            float angle = i++ * 47f * Mathf.Deg2Rad;
            Vector3 spot = next.transform.position + new Vector3(Mathf.Cos(angle) * 4f, 2f, Mathf.Sin(angle) * 4f);
            c.view.RPC("WarpPlayerRPC", RpcTarget.All, spot, true);
        }

        Plugin.Log.LogInfo($"[OTL][Admin] HOST teleported {i} player(s) to the next campfire.");
    }

    /// <summary>
    /// Host (menu): everyone back to the airport, through PEAK's own networked return
    /// (GameOverHandler.LoadAirportMaster -> LoadSceneProcess("Airport", networked: true)). The next run
    /// started from there gets a new run id, so roles are rolled again.
    /// </summary>
    public static void HostReturnToAirport()
    {
        if (!Net.IsHost) return;
        GameOverHandler? handler = Object.FindFirstObjectByType<GameOverHandler>();
        if (handler == null)
        {
            Plugin.Log.LogWarning("[OTL][Admin] airport: GameOverHandler not found in this scene.");
            return;
        }

        Plugin.Log.LogInfo("[OTL][Admin] HOST sending everyone back to the airport.");
        handler.LoadAirportMaster();
    }

    private static IEnumerator Restart()
    {
        Vector3? point = RestartPoint();
        if (point == null || Character.localCharacter == null)
        {
            Plugin.Log.LogWarning("[OTL][Admin] restart: no campfire or spawn point available yet.");
            yield break;
        }

        Plugin.Log.LogInfo($"[OTL][Admin] HOST restarting from the last campfire at {point.Value}.");
        ModNetwork.Broadcast(Notice.Restarted, PhotonNetwork.LocalPlayer.ActorNumber, 0);

        int i = 0;
        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (c == null || c.isBot) continue;
            float angle = i++ * 47f * Mathf.Deg2Rad;
            Vector3 spot = point.Value + new Vector3(Mathf.Cos(angle) * 3f, 2f, Mathf.Sin(angle) * 3f);
            if (c.data.dead || c.data.fullyPassedOut) c.view.RPC("RPCA_ReviveAtPosition", RpcTarget.All, spot, false, -1);
            else c.view.RPC("WarpPlayerRPC", RpcTarget.All, spot, true);
        }

        yield return new WaitForSeconds(1.5f); // let warps and revives land before the reveal starts
        RoundManager.HostRestartLeg();
    }

    /// <summary>Every client: the host restarted; start fresh.</summary>
    public static void OnRestartNotice()
    {
        Character local = Character.localCharacter;
        if (local != null && !local.data.dead) local.refs.afflictions.ClearAllStatus(excludeCurse: false, excludePetrify: false);
    }

}

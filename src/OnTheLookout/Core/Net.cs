using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace OnTheLookout.Core;

/// <summary>Transient messages sent with <see cref="Net.EventCode"/>. First payload element is the type.</summary>
internal enum Msg : byte
{
    /// <summary>client -> host: [chaserActor, runnerActor]</summary>
    TagClaim = 1,

    /// <summary>host -> all: [Notice, actorA, actorB]</summary>
    Notice = 2,
}

internal enum Notice : byte
{
    Captured = 1, // A captured B
    Converted = 2, // A became a chaser
    Rewarded = 3, // A reached a campfire first
    MissingMod = 4, // A has no / a different OnTheLookout version (host only)
}

/// <summary>
/// Thin wrapper over Photon. Durable state lives in room custom properties (prefixed "otl."),
/// which late joiners receive automatically and which survive host migration.
/// Transient messages use a single raw event code.
/// </summary>
internal static class Net
{
    /// <summary>Our raw Photon event code. PEAK itself uses 18 (kick); Photon reserves 200+.</summary>
    public const byte EventCode = 167;

    public static bool InRoom => PhotonNetwork.InRoom;

    public static bool IsHost => PhotonNetwork.IsMasterClient;

    /// <summary>Shared server clock in ms. It is an int that can wrap, so compare by subtraction.</summary>
    public static int Now => PhotonNetwork.ServerTimestamp;

    public static bool IsBefore(int until) => unchecked(until - Now) > 0;

    public static float SecondsUntil(int until) => Math.Max(0, unchecked(until - Now)) / 1000f;

    public static int Actor(Character c) => c.view.Owner?.ActorNumber ?? -1;

    public static string NameOf(int actor) =>
        PhotonNetwork.CurrentRoom?.GetPlayer(actor)?.NickName is { Length: > 0 } n ? n : $"Player {actor}";

    public static object? GetRoom(string key) =>
        PhotonNetwork.CurrentRoom?.CustomProperties.TryGetValue(key, out object? v) == true ? v : null;

    public static void SetRoom(string key, object value)
    {
        if (PhotonNetwork.CurrentRoom is null) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [key] = value });
    }

    public static void SendToHost(Msg msg, params object[] args) => Send(msg, ReceiverGroup.MasterClient, args);

    public static void SendToAll(Msg msg, params object[] args) => Send(msg, ReceiverGroup.All, args);

    private static void Send(Msg msg, ReceiverGroup to, object[] args)
    {
        if (!InRoom) return;
        var payload = new object[args.Length + 1];
        payload[0] = (byte)msg;
        Array.Copy(args, 0, payload, 1, args.Length);
        PhotonNetwork.RaiseEvent(EventCode, payload, new RaiseEventOptions { Receivers = to }, SendOptions.SendReliable);
    }

    public static Character? CharacterOf(int actor) =>
        PlayerHandler.TryGetCharacter(actor, out Character c) ? c : null;
}

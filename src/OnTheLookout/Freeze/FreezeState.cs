using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;

namespace OnTheLookout.Freeze;

/// <summary>
/// Per-actor freeze/cooldown timestamps. The master client is the only writer; it replicates the
/// whole table through the room property <see cref="RoomKey"/>, so late joiners and a new master
/// after host migration see the same state. Timestamps are <see cref="PhotonNetwork.ServerTimestamp"/>
/// (ms, shared clock), so there is no "unfreeze" message that could be lost.
/// </summary>
internal static class FreezeState
{
    public const string RoomKey = "otl.frz";

    private struct Entry
    {
        public int FrozenUntil;
        public int CooldownUntil;
    }

    private static readonly Dictionary<int, Entry> s_Entries = new();

    public static int Now => PhotonNetwork.ServerTimestamp;

    // ServerTimestamp is an int that can wrap; compare by subtraction.
    private static bool Before(int now, int until) => unchecked(until - now) > 0;

    public static bool IsFrozen(int actor) =>
        s_Entries.TryGetValue(actor, out Entry e) && Before(Now, e.FrozenUntil);

    public static bool IsOnCooldown(int actor) =>
        s_Entries.TryGetValue(actor, out Entry e) && Before(Now, e.CooldownUntil);

    public static float FrozenSecondsLeft(int actor) =>
        s_Entries.TryGetValue(actor, out Entry e) ? System.Math.Max(0, unchecked(e.FrozenUntil - Now)) / 1000f : 0f;

    public static float CooldownSecondsLeft(int actor) =>
        s_Entries.TryGetValue(actor, out Entry e) ? System.Math.Max(0, unchecked(e.CooldownUntil - Now)) / 1000f : 0f;

    /// <summary>
    /// Host only. Rules 4 and 5: ignored while the actor is frozen or on cooldown (no stacking, no extending).
    /// </summary>
    public static bool TryFreeze(int actor, float duration, float cooldown, string reason)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Plugin.Log.LogWarning("[OTL][Freeze] TryFreeze called on a non-host client; ignored.");
            return false;
        }

        if (IsFrozen(actor) || IsOnCooldown(actor))
        {
            return false;
        }

        int now = Now;
        int frozenUntil = unchecked(now + (int)(duration * 1000f));
        s_Entries[actor] = new Entry
        {
            FrozenUntil = frozenUntil,
            CooldownUntil = unchecked(frozenUntil + (int)(cooldown * 1000f)),
        };
        Plugin.Log.LogInfo($"[OTL][Freeze] HOST froze actor {actor} for {duration:0.0}s (+{cooldown:0.0}s cooldown). Reason: {reason}");
        Publish();
        return true;
    }

    private static void Publish()
    {
        if (PhotonNetwork.CurrentRoom is null)
        {
            return;
        }

        var table = new Hashtable();
        foreach (KeyValuePair<int, Entry> kv in s_Entries)
        {
            table[kv.Key] = new[] { kv.Value.FrozenUntil, kv.Value.CooldownUntil };
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { [RoomKey] = table });
    }

    /// <summary>All clients (including host): adopt the replicated table.</summary>
    public static void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!changed.TryGetValue(RoomKey, out object? raw) || raw is not Hashtable table)
        {
            return;
        }

        s_Entries.Clear();
        foreach (System.Collections.DictionaryEntry kv in table)
        {
            if (kv.Key is int actor && kv.Value is int[] { Length: 2 } v)
            {
                s_Entries[actor] = new Entry { FrozenUntil = v[0], CooldownUntil = v[1] };
            }
        }
    }

    public static void Clear() => s_Entries.Clear();
}

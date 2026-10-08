using System.Collections.Generic;
using BepInEx.Configuration;
using ExitGames.Client.Photon;

namespace OnTheLookout.Core;

/// <summary>
/// Host-to-client config sync. The host publishes every gameplay setting into the room property
/// <see cref="RoomKey"/>; everyone reads gameplay values through <see cref="Synced{T}"/>, which
/// returns the host's value when one exists and the local value otherwise.
/// </summary>
internal static class ConfigSync
{
    public const string RoomKey = "otl.cfg";

    private static readonly List<ConfigEntryBase> s_Synced = new();
    private static readonly Dictionary<string, object> s_HostValues = new();

    public static void Register(ConfigEntryBase entry) => s_Synced.Add(entry);

    private static string KeyOf(ConfigEntryBase e) => $"{e.Definition.Section}.{e.Definition.Key}";

    public static T Synced<T>(this ConfigEntry<T> entry) =>
        Net.InRoom && !Net.IsHost && s_HostValues.TryGetValue(KeyOf(entry), out object? v) && v is T t
            ? t
            : entry.Value;

    /// <summary>Host only.</summary>
    public static void Publish()
    {
        if (!Net.InRoom || !Net.IsHost) return;
        var table = new Hashtable();
        foreach (ConfigEntryBase e in s_Synced)
        {
            table[KeyOf(e)] = e.BoxedValue;
        }

        Net.SetRoom(RoomKey, table);
        Plugin.Log.LogInfo($"[OTL][Config] host published {table.Count} settings.");
    }

    public static void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!changed.TryGetValue(RoomKey, out object? raw) || raw is not Hashtable table) return;
        s_HostValues.Clear();
        foreach (System.Collections.DictionaryEntry kv in table)
        {
            if (kv.Key is string k && kv.Value is not null) s_HostValues[k] = kv.Value;
        }
    }

    public static void Clear() => s_HostValues.Clear();
}

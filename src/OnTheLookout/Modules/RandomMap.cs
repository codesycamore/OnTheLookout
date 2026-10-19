using System.Linq;
using HarmonyLib;
using OnTheLookout.Core;
using Photon.Pun;
using Zorro.Core;

namespace OnTheLookout.Modules;

/// <summary>
/// Host option (RandomMap, host menu): a new game started at the airport kiosk loads a random map from PEAK's map
/// pool (MapBaker.ScenePaths) instead of today's daily map, never the same map twice in a row. Only the host picks:
/// PEAK's own kiosk RPC (BeginIslandLoadRPC) then tells every client which scene to load, so clients need nothing
/// extra. Off = vanilla (today's map).
/// </summary>
internal static class RandomMap
{
    private static int s_LastIndex = -1;
    private static bool s_LoggedPool;

    public static bool Install(Harmony harmony)
    {
        // AirportCheckInKiosk.LoadIslandMaster(int ascent, byte[] serializedRunSettings) (prefix, [PunRPC] on the host).
        // Why: the host picks the scene here (MapBaker.GetLevel(daily index)) and sends it to everyone.
        return SafePatch.Prefix(harmony, typeof(AirportCheckInKiosk), nameof(AirportCheckInKiosk.LoadIslandMaster),
            typeof(RandomMap), nameof(LoadIslandPrefix), "RandomMap");
    }

    public static bool LoadIslandPrefix(AirportCheckInKiosk __instance, int ascent, byte[] serializedRunSettings)
    {
        if (!Net.IsHost || !Plugin.ModConfig.RandomMap.Value) return true;
        MapBaker? baker = SingletonAsset<MapBaker>.Instance;
        string[]? pool = baker != null ? baker.ScenePaths : null;
        if (baker == null || pool == null || pool.Length < 2) return true;

        if (!s_LoggedPool)
        {
            s_LoggedPool = true;
            Plugin.Log.LogInfo($"[OTL][RandomMap] map pool: {pool.Length} map(s): {string.Join(", ", Enumerable.Range(0, pool.Length).Select(baker.GetLevel))}.");
        }

        int index = UnityEngine.Random.Range(0, pool.Length - (s_LastIndex >= 0 ? 1 : 0));
        if (s_LastIndex >= 0 && index >= s_LastIndex) index++; // never the same map twice in a row
        string scene = baker.GetLevel(index);
        if (string.IsNullOrEmpty(scene)) return true; // vanilla picks (and falls back to WilIsland)

        s_LastIndex = index;
        Plugin.Log.LogInfo($"[OTL][RandomMap] HOST: random map {index + 1} of {pool.Length}: {scene} (ascent {ascent}).");
        __instance.photonView.RPC("BeginIslandLoadRPC", RpcTarget.All, scene, ascent, serializedRunSettings);
        return false;
    }
}

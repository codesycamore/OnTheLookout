using System.Reflection;
using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;
using Zorro.Core;

namespace OnTheLookout.Modules;

/// <summary>
/// Host "restart at previous campfire": once everyone is back at the campfire, every client puts the current
/// biome's rising hazards back to how they were when that campfire was lit (<see cref="Notice.HazardsReset"/>):
/// - the fog (OrbFogHandler): this segment's fog origin again (full size, PEAK's SetFogOrigin), waiting, not moving,
///   wait timer at 0, so it starts again when the players move on or after PEAK's wait time. Only when the fog
///   belongs to this segment (PEAK doesn't move the fog on to the Kiln, so it is left alone there);
/// - rising lava and rising gloom (LavaRising of this segment): not started, lava surface back to its starting
///   height, the "rises" message and the gloom's darkening ready to show again. Rising ghosts are left alone.
/// Every client resets its own copy at the same moment; PEAK's own syncs (fog every 5 s, lava every 15 s) then
/// carry the host's values, which match.
/// </summary>
internal static class HazardReset
{
    private static readonly FieldInfo? s_StartHeight = AccessTools.Field(typeof(LavaRising), "startHeight");
    private static readonly FieldInfo? s_ShownMessage = AccessTools.Field(typeof(LavaRising), "shownLavaRisingMessage");
    private static readonly FieldInfo? s_GloomAmount = AccessTools.Field(typeof(LavaRising), "gloomAmount");

    public static void Install()
    {
        ModNetwork.NoticeReceived += (notice, _, _) =>
        {
            if (notice == Notice.HazardsReset) LocalReset();
        };
    }

    private static void LocalReset()
    {
        int segment;
        try
        {
            segment = (int)MapHandler.CurrentSegmentNumber;
        }
        catch
        {
            return; // no map
        }

        ResetFog(segment);
        ResetRisingFields(segment);
    }

    private static void ResetFog(int segment)
    {
        OrbFogHandler fog = Singleton<OrbFogHandler>.Instance;
        if (fog == null) return;
        if (fog.currentID != segment)
        {
            Plugin.Log.LogInfo($"[OTL][Hazards] fog belongs to segment {fog.currentID}, not {segment}; left as it is.");
            return;
        }

        fog.SetFogOrigin(segment);
        fog.isMoving = false;
        fog.currentWaitTime = 0f;
        FogSphere sphere = fog.GetComponentInChildren<FogSphere>();
        if (sphere != null) sphere.ENABLE = 0f; // as after the campfire's reveal; it fades in again once the fog moves
        Plugin.Log.LogInfo($"[OTL][Hazards] fog reset to segment {segment}'s start (size {fog.currentSize:0}).");
    }

    private static void ResetRisingFields(int segment)
    {
        foreach (LavaRising field in LavaRising.ALL_LAVA.ToArray())
        {
            if (field == null || (int)field.requiredSegment != segment || field.risingFieldType == LavaRising.RisingFieldType.VoidGhosts) continue;
            if (!field.started && field.secondsWaitedToStart <= 0f) continue; // never started: nothing to undo

            field.started = false;
            field.ended = false;
            field.timeTraveled = 0f;
            field.secondsWaitedToStart = 0f;
            s_ShownMessage?.SetValue(field, false);
            if (field.lava != null && s_StartHeight?.GetValue(field) is float y)
            {
                Vector3 p = field.lava.transform.position;
                field.lava.transform.position = new Vector3(p.x, y, p.z);
                field.lava.position = field.lava.transform.position;
            }

            if (field.risingFieldType == LavaRising.RisingFieldType.Gloom)
            {
                s_GloomAmount?.SetValue(field, 0f);
                Shader.SetGlobalFloat("HeightFogAmount", 0f);
            }

            Plugin.Log.LogInfo($"[OTL][Hazards] rising {field.risingFieldType} of segment {segment} reset.");
        }
    }
}

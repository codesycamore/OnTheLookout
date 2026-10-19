using System.Collections;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Every runner's own client, when a leg starts (during the role reveal): the runner's view is turned to face the next
/// campfire (RoundManager.NextCampfire: the nearest unlit one), level with a slight upward tilt, so they know which way
/// to run. If that campfire isn't loaded yet, straight up the mountain (+z, the direction PEAK's own "at the peak"
/// check uses). It only sets the starting direction; the runner can look around freely afterwards.
/// The view is PEAK's CharacterData.lookValues, turned into look directions by Character.RecalculateLookDirections.
/// </summary>
internal static class FaceNextCampfire
{
    /// <summary>Upward tilt of the view (as a fraction of the horizontal direction).</summary>
    private const float Tilt = 0.15f;

    public static void Install()
    {
        RoundManager.LegStarted += _ => ModNetwork.Instance?.StartCoroutine(FaceLater());
    }

    private static IEnumerator FaceLater()
    {
        yield return new WaitForSeconds(0.5f); // after any warp to the campfire has landed
        if (!Plugin.ModConfig.RunnersFaceNextCampfire.Synced() || !RoundManager.IsActive) yield break;
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || !RoleManager.IsRunner(local)) yield break;

        Campfire? next = RoundManager.NextCampfire();
        Vector3 direction = next != null ? next.transform.position - local.Center : Vector3.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
        direction = direction.normalized + Vector3.up * Tilt;

        local.data.lookValues = HelperFunctions.DirectionToLook(direction.normalized);
        local.RecalculateLookDirections();
        Plugin.Log.LogInfo($"[OTL][Face] facing {(next != null ? $"the next campfire ({Vector3.Distance(local.Center, next.transform.position):0} m)" : "up the mountain (next campfire not loaded)")}.");
    }
}

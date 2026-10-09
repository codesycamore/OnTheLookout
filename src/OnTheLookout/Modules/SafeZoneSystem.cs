using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Rule 8: campfires are safe zones (queried by <see cref="TagSystem"/> and the HUD).
/// Also fixes vanilla campfire logic for this mode: Campfire.EveryoneInRange requires EVERY living
/// player near the fire to light it, rest, or let it burn out. With chasers hunting, runners could
/// never light a campfire, so during a round only runners count.
/// </summary>
internal static class SafeZoneSystem
{
    private static readonly List<Campfire> s_Campfires = new();
    private static float s_NextRefresh;

    public static bool Enabled { get; private set; }

    /// <summary>The segment the last lit campfire leads into (PEAK's Campfire.advanceToSegment); -1 before any campfire this round.</summary>
    public static int LastLitSegment { get; set; } = -1;

    public static bool Install(Harmony harmony, bool safeZonesEnabled)
    {
        Enabled = safeZonesEnabled;

        // Patch targets: Campfire.EveryoneInRange(float) and EveryoneInRange(out string, float) (prefixes).
        // Why: these gate lighting (with the "can't light, X is 120m away" printout), resting and burn-out.
        bool a = SafePatch.Prefix(harmony, typeof(Campfire), nameof(Campfire.EveryoneInRange),
            typeof(SafeZoneSystem), nameof(EveryoneInRangePrefix), "Campfire", new[] { typeof(float) });
        bool b = SafePatch.Prefix(harmony, typeof(Campfire), nameof(Campfire.EveryoneInRange),
            typeof(SafeZoneSystem), nameof(EveryoneInRangePrintoutPrefix), "Campfire", new[] { typeof(string).MakeByRefType(), typeof(float) });

        // Patch targets: Campfire.IsInteractible / IsConstantlyInteractable (prefixes).
        // Why: only runners may light a campfire (chasers can still cook on a lit one).
        bool c = SafePatch.Prefix(harmony, typeof(Campfire), nameof(Campfire.IsInteractible),
            typeof(SafeZoneSystem), nameof(ChaserCantLightPrefix), "Campfire");
        bool d = SafePatch.Prefix(harmony, typeof(Campfire), nameof(Campfire.IsConstantlyInteractable),
            typeof(SafeZoneSystem), nameof(ChaserCantLightPrefix), "Campfire");

        // Patch target: Campfire.Light_Rpc(bool updateSegment, float) (postfix, [PunRPC] on all clients).
        // Why: lighting a campfire (updateSegment = true) ends the leg; the next one starts once a runner sees the next biome title.
        bool e = SafePatch.Postfix(harmony, typeof(Campfire), "Light_Rpc", typeof(SafeZoneSystem), nameof(LightPostfix), "Campfire");

        // Patch target: MountainProgressHandler.TriggerReached(ProgressPoint, bool isFurthestPoint) (postfix).
        // Why: this is where PEAK shows the biome title (GUIManager.SetHeroTitle), on each player's own
        // client when they cross the next biome's progress point. We tell the host so the next leg
        // starts after the title has played.
        bool f = SafePatch.Postfix(harmony, typeof(MountainProgressHandler), nameof(MountainProgressHandler.TriggerReached),
            typeof(SafeZoneSystem), nameof(TitlePostfix), "Campfire");
        return a && b && c && d && e && f;
    }

    public static void TitlePostfix(bool isFurthestPoint)
    {
        if (!isFurthestPoint || Time.time <= 2f || !RoundManager.IsActive) return; // same conditions as the title itself
        ReportTitle();
    }

    private static void ReportTitle()
    {
        if (Net.IsHost) RoundManager.HostOnBiomeTitle(Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber);
        else Net.SendToHost(Msg.BiomeTitle);
    }

    public static bool ChaserCantLightPrefix(Campfire __instance, Character interactor, ref bool __result)
    {
        if (!RoundManager.IsActive || __instance.state != Campfire.FireState.Off || !RoleManager.IsChaser(interactor)) return true;
        __result = false;
        return false;
    }

    public static void LightPostfix(Campfire __instance, bool updateSegment)
    {
        if (!updateSegment || !RoundManager.IsActive) return;
        LastLitSegment = (int)__instance.advanceToSegment;

        // Every player at the campfire starts the next leg fresh (each client clears its own statuses,
        // since status values are owned by the local player).
        Character local = Character.localCharacter;
        if (local != null && !local.data.dead && Plugin.ModConfig.ClearStatusesAtCampfire.Synced()
            && Vector3.Distance(local.Center, __instance.transform.position) <= Plugin.ModConfig.CampfireSafeRadius.Synced())
        {
            local.refs.afflictions.ClearAllStatus(excludeCurse: false, excludePetrify: false);
            Plugin.Log.LogInfo("[OTL][Campfire] cleared local statuses for the new leg.");
        }

        // Play goes on until a runner walks far enough to see the next biome's title (TitlePostfix); then
        // the host freezes everyone and starts the next leg after the title.
        bool? reached = NextTitleReached(__instance);
        if (Net.IsHost)
        {
            AdminRestart.HostRememberCampfire(__instance.transform.position);
            RoundManager.HostOnCampfireLit(titleAlreadySeen: reached == null); // no title ahead: freeze right away
        }

        // This runner already crossed that point before the fire was lit, so PEAK won't show the title
        // to them again: report it now so the host doesn't wait for it.
        if (reached == true && local != null && !local.data.dead && !RoleManager.IsChaser(local))
        {
            Plugin.Log.LogInfo("[OTL][Campfire] already past the next biome's title point; telling the host.");
            ReportTitle();
        }
    }

    /// <summary>
    /// Whether this client has already reached the progress point (biome title) the campfire leads to
    /// (MountainProgressHandler.progressPoints[advanceToSegment].Reached); null if there is none.
    /// </summary>
    private static bool? NextTitleReached(Campfire fire)
    {
        try
        {
            MountainProgressHandler? progress = Zorro.Core.Singleton<MountainProgressHandler>.Instance;
            int segment = (int)fire.advanceToSegment;
            if (progress == null || progress.progressPoints == null || segment < 0 || segment >= progress.progressPoints.Length) return null;
            return progress.progressPoints[segment].Reached;
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[OTL][Campfire] couldn't read the next biome title point: {e.Message}");
            return null;
        }
    }

    /// <summary>All campfires currently loaded (refreshed every 2 s; inactive segments are excluded).</summary>
    public static IReadOnlyList<Campfire> Campfires
    {
        get
        {
            if (Time.time >= s_NextRefresh)
            {
                s_NextRefresh = Time.time + 2f;
                s_Campfires.Clear();
                s_Campfires.AddRange(Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None));
            }

            s_Campfires.RemoveAll(f => f == null);
            return s_Campfires;
        }
    }

    public static Campfire? SafeZoneAt(Vector3 position)
    {
        if (!Enabled || !RoundManager.IsActive) return null;
        float radius = Plugin.ModConfig.CampfireSafeRadius.Synced();
        bool requireLit = Plugin.ModConfig.SafeZoneRequiresLit.Synced();
        foreach (Campfire fire in Campfires)
        {
            if (!fire.isActiveAndEnabled || (requireLit && fire.state != Campfire.FireState.Lit)) continue;
            if (Vector3.Distance(fire.transform.position, position) <= radius) return fire;
        }

        return null;
    }

    public static bool IsSafe(Vector3 position) => SafeZoneAt(position) != null;

    public static bool EveryoneInRangePrefix(Campfire __instance, float range, ref bool __result)
    {
        if (!RoundManager.IsActive) return true;
        __result = RunnersInRange(__instance, range, out _);
        return false;
    }

    public static bool EveryoneInRangePrintoutPrefix(Campfire __instance, ref string printout, float range, ref bool __result)
    {
        if (!RoundManager.IsActive) return true;
        __result = RunnersInRange(__instance, range, out string missing);
        printout = __result ? "" : LocalizedText.GetText("CANTLIGHT") + "\n" + missing;
        return false;
    }

    /// <summary>Same as vanilla EveryoneInRange, but chasers are ignored.</summary>
    private static bool RunnersInRange(Campfire fire, float range, out string missing)
    {
        var sb = new StringBuilder();
        Vector3 position = fire.transform.position;
        foreach (Character c in PlayerHandler.GetAllPlayerCharacters())
        {
            if (c.data.dead || RoleManager.IsChaser(c)) continue;
            float distance = Vector3.Distance(position, c.Center);
            if (distance > range)
            {
                sb.Append($"\n{c.photonView.Owner.NickName} {Mathf.RoundToInt(distance * CharacterStats.unitsToMeters)}m");
            }
        }

        missing = sb.ToString();
        return sb.Length == 0;
    }
}

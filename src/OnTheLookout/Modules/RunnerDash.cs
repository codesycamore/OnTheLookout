using System.Collections;
using System.Linq;
using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// The runner gem (Scout's Ambition by default, RunnerGemItem): using it as a runner never activates its vanilla
/// power; instead the runner dashes DashDistance horizontally in the direction they look. The dash drives the
/// runner's own ragdoll (their client owns it): for a short moment every body part's horizontal velocity is set
/// so the body covers the distance, then the extra speed is taken away again, so walls and slopes still stop
/// it like any movement. Dashing in mid-air also floats the runner for DashAirFloatSeconds (no falling).
/// From DashFallProtectDelay after the dash, the runner takes no fall damage for DashFallProtectSeconds.
/// Each dash adds DashPetrify petrification and starts a DashCooldownSeconds cooldown
/// (shown above the gem's slot). When the runners win a leg, every runner in the safe zone loses
/// SafeZonePetrifyRelief petrification.
/// Neither of the mod's gems (this one and the chaser gem) ever uses its original scout-gem power.
/// </summary>
internal static class RunnerDash
{
    private const float DashSeconds = 0.18f;

    private static float s_ReadyAt;
    private static bool s_Dashing;

    public static bool OnCooldown => Time.time < s_ReadyAt;

    public static float CooldownLeft => Mathf.Max(0f, s_ReadyAt - Time.time);

    public static void ResetCooldown() => s_ReadyAt = 0f;

    private static float s_FallSafeFrom, s_FallSafeUntil;

    /// <summary>The local runner is inside their dash's fall-damage protection window (DashFallProtectDelay .. +DashFallProtectSeconds).</summary>
    public static bool FallProtected => Time.time >= s_FallSafeFrom && Time.time < s_FallSafeUntil && RoleManager.IsRunner(Character.localCharacter);

    public static bool Install(Harmony harmony)
    {
        RoundManager.LegCompleted += LocalSafeZoneRelief;
        const string m = "Gems";
        bool ok = true;

        // The mod's two scout gems (runner dash gem, chaser boost gem) only ever do the mod's ability:
        // Action_StrangeGem.RunAction (prefix): using an amulet switches its power on (AmuletBase.ToggleGem);
        // ItemPocketBehaviorInstanceBase.behaviorEnabled getter (postfix): an amulet's carried effect (e.g. Scout's
        // Ambition's stamina aura and its petrification) only runs while its power is on.
        ok &= SafePatch.Prefix(harmony, typeof(Action_StrangeGem), nameof(Action_StrangeGem.RunAction), typeof(RunnerDash), nameof(GemActionPrefix), m);
        ok &= SafePatch.Postfix(harmony, typeof(Peak.ItemPocketBehaviorInstanceBase), "get_behaviorEnabled", typeof(RunnerDash), nameof(BehaviorEnabledPostfix), m);
        return ok;
    }

    /// <summary>One of the mod's gems, during a round: its original power is never used.</summary>
    private static bool IsModGem(Item? item) => RoundManager.IsActive && item != null && (IsRunnerGem(item) || ChaserKit.IsGem(item));

    public static bool GemActionPrefix(Action_StrangeGem __instance) => !IsModGem(__instance.item);

    public static void BehaviorEnabledPostfix(Peak.ItemPocketBehaviorInstanceBase __instance, ref bool __result)
    {
        if (__result && __instance.itemSlot != null && IsModGem(__instance.itemSlot.prefab)) __result = false;
    }

    /// <summary>The runner gem item (RunnerGemItem), e.g. Scout's Ambition.</summary>
    public static bool IsRunnerGem(Item? item) =>
        item != null && ItemCatalog.MatchesNames(ItemCatalog.Effective(item), Plugin.ModConfig.RunnerGemItem.Synced());

    public static Item? GemPrefab => ItemCatalog.FindByNames(Plugin.ModConfig.RunnerGemItem.Synced()).FirstOrDefault();

    /// <summary>
    /// Local runner using the runner gem (Item.StartUse*/ContinueUse*, via ItemRules.UsePrefix): dash when ready.
    /// Returns true when the vanilla use must be skipped (always, for the runner gem).
    /// </summary>
    public static bool HandleUse(Item item)
    {
        Character holder = item.holderCharacter;
        if (holder == null || !holder.IsLocal || !RoundManager.IsActive || !Plugin.ModConfig.RunnerGem.Synced()) return false;
        if (!IsRunnerGem(item) || !RoleManager.IsRunner(holder)) return false;
        if (s_Dashing || OnCooldown || holder.data.dead || holder.data.passedOut || RoundManager.InReveal || RoundManager.InRoleWindow) return true;
        if (holder.data.isClimbing || holder.data.isRopeClimbing || holder.data.isVineClimbing) return true; // horizontal dash: not off a wall

        var cfg = Plugin.ModConfig;
        Vector3 dir = holder.data.lookDirection_Flat;
        if (dir.sqrMagnitude < 0.01f) return true;
        dir.Normalize();

        s_ReadyAt = Time.time + Mathf.Max(1f, cfg.DashCooldownSeconds.Synced());
        int petrify = Mathf.RoundToInt(cfg.DashPetrify.Synced() * 100f);
        if (petrify > 0) holder.refs.afflictions.AddPetrify(petrify);
        float protectFrom = Time.time + Mathf.Max(0f, cfg.DashFallProtectDelay.Synced());
        s_FallSafeFrom = protectFrom;
        s_FallSafeUntil = protectFrom + Mathf.Max(0f, cfg.DashFallProtectSeconds.Synced());
        float floatSeconds = holder.data.isGrounded ? 0f : Mathf.Max(0f, cfg.DashAirFloatSeconds.Synced()); // mid-air: hang in the air a moment
        ModNetwork.Instance?.StartCoroutine(Dash(holder, dir, Mathf.Max(0f, cfg.DashDistance.Synced()), floatSeconds));
        ChaserKit.PutAwayLater(holder, item); // hands free again after the dash (the gem stays in its slot)
        Plugin.Log.LogInfo($"[OTL][Dash] dash {cfg.DashDistance.Synced():0.#}m, +{petrify}% petrify.");
        return true;
    }

    /// <summary>
    /// The dash itself; with <paramref name="floatSeconds"/> &gt; 0 (dashed in mid-air) the runner also hangs in the
    /// air for that long from the start of the dash: their vertical speed is held at zero, so they don't fall.
    /// </summary>
    private static IEnumerator Dash(Character runner, Vector3 dir, float distance, float floatSeconds)
    {
        s_Dashing = true;
        float speed = distance / DashSeconds;
        float start = Time.time;
        bool dashing = true;
        var wait = new WaitForFixedUpdate();
        while (runner != null && !runner.data.dead && (dashing || Time.time - start < floatSeconds))
        {
            if (dashing && Time.time - start >= DashSeconds)
            {
                dashing = false;
                // Take the dash's speed away again so the body doesn't slide on past the distance.
                SetHorizontalVelocity(runner, dir * Mathf.Min(speed, 3f));
            }
            else if (dashing)
            {
                SetHorizontalVelocity(runner, dir * speed);
            }

            if (Time.time - start < floatSeconds) HoldAltitude(runner);
            yield return wait;
        }

        s_Dashing = false;
    }

    /// <summary>No vertical speed for this physics step (the float).</summary>
    private static void HoldAltitude(Character c)
    {
        foreach (Bodypart part in c.refs.ragdoll.partList)
        {
            Rigidbody? rig = part != null ? part.Rig : null;
            if (rig == null || rig.isKinematic) continue;
            Vector3 v = rig.linearVelocity;
            rig.linearVelocity = new Vector3(v.x, 0f, v.z);
        }
    }

    private static void SetHorizontalVelocity(Character c, Vector3 horizontal)
    {
        foreach (Bodypart part in c.refs.ragdoll.partList)
        {
            Rigidbody? rig = part != null ? part.Rig : null;
            if (rig == null || rig.isKinematic) continue;
            rig.linearVelocity = new Vector3(horizontal.x, rig.linearVelocity.y, horizontal.z);
        }
    }

    /// <summary>Each client, when a leg ends: the runners won it and the local runner is in the safe zone -> less petrification.</summary>
    private static void LocalSafeZoneRelief()
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || RoundManager.ChasersWonLeg || !RoleManager.IsRunner(local) || !SafeZoneSystem.IsSafe(local.Center)) return;
        int relief = Mathf.RoundToInt(Plugin.ModConfig.SafeZonePetrifyRelief.Synced() * 100f);
        int current = local.data.petrifyAmount;
        if (relief <= 0 || current <= 0) return;
        local.refs.afflictions.AddPetrify(-Mathf.Min(relief, current));
        Plugin.Log.LogInfo($"[OTL][Dash] safe at the campfire: -{Mathf.Min(relief, current)}% petrify.");
    }
}

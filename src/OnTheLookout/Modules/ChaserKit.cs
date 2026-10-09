using System.Collections;
using System.Linq;
using HarmonyLib;
using OnTheLookout.Core;
using Peak.Afflictions;
using UnityEngine;
using Zorro.Core;

namespace OnTheLookout.Modules;

/// <summary>
/// Chaser and runner kit rules that act on the local player's own client:
/// - the chaser gem (Scout's Initiative by default, ChaserGemItem): using it as a chaser never activates its
///   vanilla power; instead it gives a short energy-drink speed boost and unlimited stamina (no drowsiness),
///   starting GemDelaySeconds after use, with a GemCooldownSeconds cooldown once the effects wear off, at the
///   cost of GemPetrify petrification. Each capture takes CapturePetrifyRelief of it away again;
/// - a chaser can't drop or throw their blowgun (role swaps drop the kit: <see cref="RoleSwap"/>);
/// - runners get the same short boost for the last HeadStartBoostSeconds of their head start;
/// - a thrown snowball pushes whoever it hits SnowballKnockbackBonus harder, and a runner's snowball blinds the
///   chaser it hits (blue-flower blindness);
/// - the golden Bing Bong's invincibility shield is switched off during a round.
/// </summary>
internal static class ChaserKit
{
    private static float s_GemReadyAt;
    private static int s_BoostedLeg = -1;
    private static Affliction_FasterBoi? s_EnergyDrink;
    private static bool s_EnergyDrinkLooked;

    /// <summary>True while we add our own speed boost, so other rules (energy drink shortening) leave it alone.</summary>
    public static bool ApplyingBoost { get; private set; }

    /// <summary>The gem's effects are pending or running; its cooldown starts when they wear off.</summary>
    private static bool s_GemActive;

    /// <summary>The cooldown is counting down (shown above the gem's slot).</summary>
    public static bool GemOnCooldown => Time.time < s_GemReadyAt;

    /// <summary>The gem can't be used: its effects are still going, or it is cooling down.</summary>
    public static bool GemBusy => s_GemActive || GemOnCooldown;

    public static float GemCooldownLeft => Mathf.Max(0f, s_GemReadyAt - Time.time);

    public static bool Install(Harmony harmony)
    {
        const string m = "ChaserKit";
        bool ok = true;
        System.Type t = typeof(ChaserKit);

        // CharacterItems.DoDropping() (prefix, private). Why: the drop/throw input path; a chaser's blowgun stays put.
        ok &= SafePatch.Prefix(harmony, typeof(CharacterItems), "DoDropping", t, nameof(DoDroppingPrefix), m);

        // Snowball.OnCollisionEnter(Collision) (postfix, private). Why: fires when a snowball touches a character.
        ok &= SafePatch.Postfix(harmony, typeof(Snowball), "OnCollisionEnter", t, nameof(SnowballHitPostfix), m);

        // BingBongShieldWhileHolding.TryApplyInvincibility() (prefix, private). Why: the golden Bing Bong's shield.
        ok &= SafePatch.Prefix(harmony, typeof(BingBongShieldWhileHolding), "TryApplyInvincibility", t, nameof(BingBongShieldPrefix), m);


        ModNetwork.NoticeReceived += (notice, a, _) =>
        {
            if (notice == Notice.Captured && Character.localCharacter is { } local && Net.Actor(local) == a) RelievePetrify(local);
        };
        return ok;
    }

    // ---------- Speed boost ----------

    /// <summary>The energy drink's own speed affliction (from its item), for the boost's speed values.</summary>
    private static Affliction_FasterBoi? EnergyDrink()
    {
        if (s_EnergyDrinkLooked) return s_EnergyDrink;
        s_EnergyDrinkLooked = true;
        Item? drink = ItemCatalog.FindByNames("Energy Drink").FirstOrDefault();
        s_EnergyDrink = drink?.GetComponentsInChildren<Action_ApplyAffliction>(true)
            .Select(a => a.affliction as Affliction_FasterBoi)
            .FirstOrDefault(a => a != null);
        Plugin.Log.LogInfo(s_EnergyDrink != null
            ? $"[OTL][Kit] energy drink boost: move +{s_EnergyDrink.moveSpeedMod}, climb +{s_EnergyDrink.climbSpeedMod}."
            : "[OTL][Kit] energy drink boost values not found; using PEAK's defaults.");
        return s_EnergyDrink;
    }

    /// <summary>An energy-drink speed boost for <paramref name="seconds"/>, without the drowsiness afterwards.</summary>
    public static void ApplyBoost(Character c, float seconds)
    {
        if (seconds <= 0f || c == null || !c.IsLocal) return;
        Affliction_FasterBoi? template = EnergyDrink();
        var boost = new Affliction_FasterBoi
        {
            totalTime = seconds,
            drowsyOnEnd = 0f,
        };
        if (template != null)
        {
            boost.moveSpeedMod = template.moveSpeedMod;
            boost.climbSpeedMod = template.climbSpeedMod;
            boost.climbDelay = template.climbDelay;
        }

        ApplyingBoost = true;
        try
        {
            c.refs.afflictions.AddAffliction(boost);
        }
        finally
        {
            ApplyingBoost = false;
        }
    }

    /// <summary>Every client, each frame (from Hud): runners' boost at the tail end of their head start.</summary>
    public static void LocalTick()
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || !RoleManager.IsRunner(local) || !RoundManager.InCountdown) return;
        float seconds = Plugin.ModConfig.HeadStartBoostSeconds.Synced();
        if (seconds <= 0f || RoundManager.HoldSecondsLeft > seconds || s_BoostedLeg == RoundManager.LegKey) return;
        s_BoostedLeg = RoundManager.LegKey;
        ApplyBoost(local, RoundManager.HoldSecondsLeft + 0.25f);
        Plugin.Log.LogInfo("[OTL][Kit] head start boost.");
    }

    // ---------- Chaser gem ----------

    /// <summary>The chaser gem item (ChaserGemItem), e.g. Scout's Initiative.</summary>
    public static bool IsGem(Item? item) =>
        item != null && ItemCatalog.MatchesNames(ItemCatalog.Effective(item), Plugin.ModConfig.ChaserGemItem.Synced());

    public static Item? GemPrefab => ItemCatalog.FindByNames(Plugin.ModConfig.ChaserGemItem.Synced()).FirstOrDefault();

    /// <summary>
    /// Local chaser using the chaser gem (Item.StartUsePrimary/StartUseSecondary): instead of its vanilla power, boost
    /// (when off cooldown) and add petrification. Returns true when the vanilla use must be skipped.
    /// </summary>
    public static bool HandleUse(Item item)
    {
        Character holder = item.holderCharacter;
        if (holder == null || !holder.IsLocal || !RoundManager.IsActive || !Plugin.ModConfig.ChaserGem.Synced()) return false;
        if (!RoleManager.IsChaser(holder) || !IsGem(item)) return false;
        if (!RoundManager.IsChasing || GemBusy || holder.data.dead) return true; // its vanilla power is never activated by a chaser

        var cfg = Plugin.ModConfig;
        s_GemActive = true; // the cooldown starts once the effects have worn off (GemBoostLater)
        int petrify = Mathf.RoundToInt(cfg.GemPetrify.Synced() * 100f);
        if (petrify > 0) holder.refs.afflictions.AddPetrify(petrify);
        Plugin.Log.LogInfo($"[OTL][Kit] chaser gem used: +{petrify}% petrify, boost in {cfg.GemDelaySeconds.Synced():0.##}s.");
        ModNetwork.Instance?.StartCoroutine(GemBoostLater(holder));
        return true;
    }

    /// <summary>
    /// After GemDelaySeconds: the energy-drink speed boost and, with GemInfiniteStamina, PEAK's
    /// unlimited stamina (Affliction_InfiniteStamina, the rainbow "sugar rush" bar, without its drowsiness),
    /// both for GemBoostSeconds.
    /// </summary>
    private static IEnumerator GemBoostLater(Character chaser)
    {
        var cfg = Plugin.ModConfig;
        float delay = Mathf.Max(0f, cfg.GemDelaySeconds.Synced());
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float seconds = Mathf.Max(0f, cfg.GemBoostSeconds.Synced());
        if (chaser == null || chaser.data.dead || !RoleManager.IsChaser(chaser) || !RoundManager.IsChasing)
        {
            StartGemCooldown(); // used but cancelled: the cooldown still applies
            yield break;
        }

        ApplyBoost(chaser, seconds);
        if (cfg.GemInfiniteStamina.Synced() && seconds > 0f
            && !chaser.refs.afflictions.HasAfflictionType(Affliction.AfflictionType.InfiniteStamina, out _)) // don't stack onto a lollipop's
        {
            chaser.refs.afflictions.AddAffliction(new Affliction_InfiniteStamina(seconds));
        }

        Plugin.Log.LogInfo($"[OTL][Kit] chaser gem: boost{(cfg.GemInfiniteStamina.Synced() ? " + unlimited stamina" : "")} for {seconds:0.##}s.");

        if (seconds > 0f) yield return new WaitForSeconds(seconds);
        StartGemCooldown();
    }

    private static void StartGemCooldown()
    {
        s_GemActive = false;
        s_GemReadyAt = Time.time + Mathf.Max(1f, Plugin.ModConfig.GemCooldownSeconds.Synced());
    }

    private static void RelievePetrify(Character chaser)
    {
        int relief = Mathf.RoundToInt(Plugin.ModConfig.CapturePetrifyRelief.Synced() * 100f);
        int current = chaser.data.petrifyAmount;
        if (relief <= 0 || current <= 0) return;
        chaser.refs.afflictions.AddPetrify(-Mathf.Min(relief, current));
        Plugin.Log.LogInfo($"[OTL][Kit] capture: -{Mathf.Min(relief, current)}% petrify.");
    }

    // ---------- Blowgun stays with chasers ----------

    public static bool DoDroppingPrefix(CharacterItems __instance)
    {
        Character c = __instance.character;
        if (c == null || !c.IsLocal || !RoleManager.IsChaser(c) || c.data.currentItem == null) return true;
        return !ItemCatalog.IsBlowgun(c.data.currentItem); // skip the whole drop/throw for the blowgun
    }

    // ---------- Snowballs blind chasers ----------

    public static void SnowballHitPostfix(Snowball __instance, Collision other)
    {
        if (!RoundManager.IsActive || other?.collider == null) return;
        Item item = __instance.item;
        if (item == null || item.lastThrownCharacter == null || Time.time - item.lastThrownTime > 3f) return;
        Character hit = other.collider.GetComponentInParent<Character>();
        if (hit == null || !hit.IsLocal || hit == item.lastThrownCharacter || hit.data.dead) return; // the hit scout's own client (it owns their physics)

        // Stronger knockback: an extra push of SnowballKnockbackBonus times the snowball's impact momentum,
        // on the body part it hit, away from the snowball.
        float bonus = Plugin.ModConfig.SnowballKnockbackBonus.Synced();
        if (bonus > 0f && other.rigidbody != null && item.rig != null)
        {
            Vector3 away = other.rigidbody.worldCenterOfMass - item.transform.position;
            if (away.sqrMagnitude > 0.0001f)
            {
                other.rigidbody.AddForce(away.normalized * (item.rig.mass * other.relativeVelocity.magnitude * bonus), ForceMode.Impulse);
            }
        }

        float seconds = Plugin.ModConfig.SnowballBlindSeconds.Synced();
        if (seconds <= 0f || !RoundManager.IsChasing || !RoleManager.IsRunner(item.lastThrownCharacter) || !RoleManager.IsChaser(hit)) return;
        hit.refs.afflictions.AddAffliction(new Affliction_Blind { totalTime = seconds });
        Plugin.Log.LogInfo($"[OTL][Kit] hit by {item.lastThrownCharacter.characterName}'s snowball: blind for {seconds:0.#}s.");
    }

    // ---------- Golden Bing Bong ----------

    public static bool BingBongShieldPrefix() => !(RoundManager.IsActive && Plugin.ModConfig.BanGoldenBingBong.Synced());
}

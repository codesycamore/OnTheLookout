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
/// - the chaser napberry: using a napberry as a chaser never eats it; instead it gives a short energy-drink
///   speed boost and unlimited stamina (no drowsiness), starting NapberryDelaySeconds after use, every
///   NapberryCooldownSeconds, at the cost of NapberryPetrify petrification.
///   Each capture takes CapturePetrifyRelief of it away again;
/// - a chaser can't drop or throw their blowgun (role swaps drop the kit: <see cref="RoleSwap"/>);
/// - runners get the same short boost for the last HeadStartBoostSeconds of their head start;
/// - a snowball thrown by a runner blinds the chaser it hits (blue-flower blindness);
/// - the golden Bing Bong's invincibility shield is switched off during a round.
/// </summary>
internal static class ChaserKit
{
    private static float s_NapberryReadyAt;
    private static int s_BoostedLeg = -1;
    private static Affliction_FasterBoi? s_EnergyDrink;
    private static bool s_EnergyDrinkLooked;

    /// <summary>True while we add our own speed boost, so other rules (energy drink shortening) leave it alone.</summary>
    public static bool ApplyingBoost { get; private set; }

    /// <summary>The napberry's effects are pending or running; its cooldown starts when they wear off.</summary>
    private static bool s_NapberryActive;

    /// <summary>The cooldown is counting down (shown above the napberry's slot).</summary>
    public static bool NapberryOnCooldown => Time.time < s_NapberryReadyAt;

    /// <summary>The napberry can't be used: its effects are still going, or it is cooling down.</summary>
    public static bool NapberryBusy => s_NapberryActive || NapberryOnCooldown;

    public static float NapberryCooldownLeft => Mathf.Max(0f, s_NapberryReadyAt - Time.time);

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

    // ---------- Chaser napberry ----------

    public static bool IsNapberry(Item? item) =>
        item != null && ItemCatalog.NameOf(ItemCatalog.Effective(item)).Replace(" ", "").ToLowerInvariant().Contains("napberry");

    /// <summary>
    /// Local chaser using a napberry (Item.StartUsePrimary/StartUseSecondary): instead of eating it, boost
    /// (when off cooldown) and add petrification. Returns true when the vanilla use must be skipped.
    /// </summary>
    public static bool HandleUse(Item item)
    {
        Character holder = item.holderCharacter;
        if (holder == null || !holder.IsLocal || !RoundManager.IsActive || !Plugin.ModConfig.ChaserNapberry.Synced()) return false;
        if (!RoleManager.IsChaser(holder) || !IsNapberry(item)) return false;
        if (!RoundManager.IsChasing || NapberryBusy || holder.data.dead) return true; // never eaten by a chaser

        var cfg = Plugin.ModConfig;
        s_NapberryActive = true; // the cooldown starts once the effects have worn off (NapberryBoostLater)
        int petrify = Mathf.RoundToInt(cfg.NapberryPetrify.Synced() * 100f);
        if (petrify > 0) holder.refs.afflictions.AddPetrify(petrify);
        Plugin.Log.LogInfo($"[OTL][Kit] chaser napberry used: +{petrify}% petrify, boost in {cfg.NapberryDelaySeconds.Synced():0.##}s.");
        ModNetwork.Instance?.StartCoroutine(NapberryBoostLater(holder));
        return true;
    }

    /// <summary>
    /// After NapberryDelaySeconds: the energy-drink speed boost and, with NapberryInfiniteStamina, PEAK's
    /// unlimited stamina (Affliction_InfiniteStamina, the rainbow "sugar rush" bar, without its drowsiness),
    /// both for NapberryBoostSeconds.
    /// </summary>
    private static IEnumerator NapberryBoostLater(Character chaser)
    {
        var cfg = Plugin.ModConfig;
        float delay = Mathf.Max(0f, cfg.NapberryDelaySeconds.Synced());
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float seconds = Mathf.Max(0f, cfg.NapberryBoostSeconds.Synced());
        if (chaser == null || chaser.data.dead || !RoleManager.IsChaser(chaser) || !RoundManager.IsChasing)
        {
            StartNapberryCooldown(); // used but cancelled: the cooldown still applies
            yield break;
        }

        ApplyBoost(chaser, seconds);
        if (cfg.NapberryInfiniteStamina.Synced() && seconds > 0f
            && !chaser.refs.afflictions.HasAfflictionType(Affliction.AfflictionType.InfiniteStamina, out _)) // don't stack onto a lollipop's
        {
            chaser.refs.afflictions.AddAffliction(new Affliction_InfiniteStamina(seconds));
        }

        Plugin.Log.LogInfo($"[OTL][Kit] chaser napberry: boost{(cfg.NapberryInfiniteStamina.Synced() ? " + unlimited stamina" : "")} for {seconds:0.##}s.");

        if (seconds > 0f) yield return new WaitForSeconds(seconds);
        StartNapberryCooldown();
    }

    private static void StartNapberryCooldown()
    {
        s_NapberryActive = false;
        s_NapberryReadyAt = Time.time + Mathf.Max(1f, Plugin.ModConfig.NapberryCooldownSeconds.Synced());
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
        float seconds = Plugin.ModConfig.SnowballBlindSeconds.Synced();
        if (seconds <= 0f || !RoundManager.IsChasing || other?.collider == null) return;
        Item item = __instance.item;
        if (item == null || item.lastThrownCharacter == null || Time.time - item.lastThrownTime > 3f) return;
        if (!RoleManager.IsRunner(item.lastThrownCharacter)) return;

        Character hit = other.collider.GetComponentInParent<Character>();
        if (hit == null || !hit.IsLocal || !RoleManager.IsChaser(hit) || hit.data.dead) return; // the chaser's own client applies it
        hit.refs.afflictions.AddAffliction(new Affliction_Blind { totalTime = seconds });
        Plugin.Log.LogInfo($"[OTL][Kit] hit by {item.lastThrownCharacter.characterName}'s snowball: blind for {seconds:0.#}s.");
    }

    // ---------- Golden Bing Bong ----------

    public static bool BingBongShieldPrefix() => !(RoundManager.IsActive && Plugin.ModConfig.BanGoldenBingBong.Synced());
}

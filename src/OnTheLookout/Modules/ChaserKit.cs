using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OnTheLookout.Core;
using OnTheLookout.UI;
using Peak.Afflictions;
using UnityEngine;
using Zorro.Core;

namespace OnTheLookout.Modules;

/// <summary>
/// Chaser and runner kit rules that act on the local player's own client:
/// - the chaser gem (Scout's Initiative by default, ChaserGemItem): using it as a chaser never activates its
///   vanilla power; instead it gives a short energy-drink speed boost and unlimited stamina (no drowsiness) and
///   GemXRaySeconds of x-ray vision on the runners (<see cref="XRayVision"/>),
///   starting GemDelaySeconds after use, with a GemCooldownSeconds cooldown once the effects wear off, at the
///   cost of GemPetrify petrification. Each capture takes CapturePetrifyRelief of it away again;
/// - each capture gives the chaser the Scoutmaster's bugle's morale boost (CaptureMoraleBoost), and makes their gem's
///   speed boost GemCaptureBonus stronger and its effects GemCaptureExtraSeconds longer for the rest of the leg;
/// - everyone's petrification is cleared when a leg ends (ClearPetrifyEachLeg);
/// - a chaser can't drop or throw their blowgun (role swaps drop the kit: <see cref="RoleSwap"/>);
/// - runners get the same short boost for the last HeadStartBoostSeconds of their head start;
/// - a thrown snowball pushes whoever it hits SnowballKnockbackBonus harder, and a runner's snowball blinds the
///   chaser it hits (blue-flower blindness) for SnowballBlindSeconds;
/// - every ability cooldown is refreshed at the start of each leg;
/// - using the chaser gem makes every runner smoke for GemRevealSeconds, and a runner's dash makes every chaser smoke
///   for DashRevealSeconds (flare smoke in their own colour);
/// - the golden Bing Bong's invincibility shield is switched off during a round.
/// </summary>
internal static class ChaserKit
{
    private static float s_GemReadyAt;
    private static int s_BoostedLeg = -1;
    private static Affliction_FasterBoi? s_EnergyDrink;
    private static bool s_EnergyDrinkLooked;

    /// <summary>The gem's effects are pending or running; its cooldown starts when they wear off.</summary>
    private static bool s_GemActive;

    /// <summary>The leg the gem was last used in.</summary>
    private static int s_GemLeg = -1;

    /// <summary>The cooldown is counting down (shown above the gem's slot).</summary>
    public static bool GemOnCooldown => Time.time < s_GemReadyAt;

    /// <summary>The gem can't be used: its effects are still going, or it is cooling down.</summary>
    public static bool GemBusy => s_GemActive || GemOnCooldown;

    public static float GemCooldownLeft => Mathf.Max(0f, s_GemReadyAt - Time.time);

    public static bool Install(Harmony harmony)
    {
        const string m = "ChaserKit";
        RoundManager.FreshStart += () =>
        {
            ResetCooldowns();
            s_GemLeg = -1;
            s_BoostedLeg = -1;
            s_Captures = 0;
            s_CapturesLeg = -1;
            s_SnowballHits.Clear();
        };
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
            if (notice == Notice.GemReveal) SmokeAll(RoleManager.IsRunner, Plugin.ModConfig.GemRevealSeconds.Synced(), "chaser gem");
            if (notice == Notice.DashReveal) SmokeAll(RoleManager.IsChaser, Plugin.ModConfig.DashRevealSeconds.Synced(), "runner dash");
            if (notice != Notice.Captured || Character.localCharacter is not { } local || Net.Actor(local) != a || local.data.dead) return;
            RelievePetrify(local);
            CaptureBoost(local);
            CountCapture();
        };
        RoundManager.LegStarted += _ =>
        {
            ResetCooldowns();
            ClearPetrify("new leg");
        };
        RoundManager.LegCompleted += () => ClearPetrify("leg over");
        return ok;
    }

    // ---------- Gem reveals ----------

    /// <summary>
    /// Every client: each living scout of one role gives off flare smoke in their own colour for <paramref name="seconds"/>
    /// (the blowgun's tracking smoke). Chaser gem -> all runners (GemRevealSeconds); runner dash -> all chasers (DashRevealSeconds).
    /// </summary>
    private static void SmokeAll(System.Func<Character, bool> role, float seconds, string why)
    {
        if (seconds <= 0f || !RoundManager.IsChasing) return;
        int count = 0;
        foreach (Character c in Character.AllCharacters)
        {
            if (c == null || c.data.dead || !role(c)) continue;
            TrackingSmoke.Attach(c, seconds);
            count++;
        }

        Plugin.Log.LogInfo($"[OTL][Kit] {why}: {count} scout(s) revealed by smoke for {seconds:0.#}s.");
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
    public static void ApplyBoost(Character c, float seconds, float strength = 1f)
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

        boost.moveSpeedMod *= strength;
        boost.climbSpeedMod *= strength;
        c.refs.afflictions.AddAffliction(boost);
    }

    /// <summary>
    /// An energy drink and a lollipop together, without the drowsiness afterwards: the energy-drink speed boost and
    /// PEAK's unlimited stamina (Affliction_InfiniteStamina, the rainbow "sugar rush" bar), both lasting at least
    /// <paramref name="seconds"/> from now. Effects already running are lengthened instead of stacked.
    /// </summary>
    public static void ApplySugarRush(Character c, float seconds, float strength = 1f)
    {
        if (seconds <= 0f || c == null || !c.IsLocal) return;
        CharacterAfflictions afflictions = c.refs.afflictions;
        if (afflictions.HasAfflictionType(Affliction.AfflictionType.FasterBoi, out Affliction speed))
        {
            speed.totalTime = Mathf.Max(speed.totalTime, speed.timeElapsed + seconds);
            if (strength > 1f && speed is Affliction_FasterBoi running && EnergyDrink() is { } template
                && running.moveSpeedMod < template.moveSpeedMod * strength)
            {
                // A stronger boost replaces the running one. Its speed is added in OnApplied and taken off in OnRemoved,
                // so it is swapped rather than edited; its drowsiness-on-end moves to the replacement.
                float left = running.totalTime - running.timeElapsed, drowsy = running.drowsyOnEnd;
                running.drowsyOnEnd = 0f;
                afflictions.RemoveAffliction(running);
                ApplyBoost(c, left, strength);
                if (afflictions.HasAfflictionType(Affliction.AfflictionType.FasterBoi, out Affliction replaced) && replaced is Affliction_FasterBoi boost)
                {
                    boost.drowsyOnEnd = drowsy;
                }
            }
        }
        else
        {
            ApplyBoost(c, seconds, strength);
        }

        if (afflictions.HasAfflictionType(Affliction.AfflictionType.InfiniteStamina, out Affliction stamina))
        {
            stamina.totalTime = Mathf.Max(stamina.totalTime, stamina.timeElapsed + seconds);
        }
        else
        {
            afflictions.AddAffliction(new Affliction_InfiniteStamina(seconds));
        }
    }

    // ---------- Capture boost ----------

    /// <summary>
    /// The local chaser captured a runner (host's capture notice): PEAK's campfire morale boost
    /// (Character.MoraleBoost: the MORALE BOOST text + extra stamina), with the Scoutmaster's bugle's own amount
    /// (Action_MoraleBoost.baselineStaminaBoost), for this chaser only.
    /// </summary>
    private static void CaptureBoost(Character chaser)
    {
        if (!Plugin.ModConfig.CaptureMoraleBoost.Synced()) return;
        if (ItemCatalog.MoraleBugle is not { } bugle)
        {
            Plugin.Log.LogWarning("[OTL][Kit] capture morale boost: no item with a morale boost (Scoutmaster's bugle) found.");
            return;
        }

        float stamina = bugle.baselineStaminaBoost;
        chaser.MoraleBoost(stamina, 1);
        Plugin.Log.LogInfo($"[OTL][Kit] capture morale boost ({bugle.name}): +{stamina:0.##} extra stamina.");
    }

    // ---------- Captures make the gem last longer ----------

    private static int s_Captures, s_CapturesLeg = -1;

    /// <summary>Captures by the local chaser in the current leg.</summary>
    private static int CapturesThisLeg => s_CapturesLeg == RoundManager.LegKey ? s_Captures : 0;

    private static void CountCapture()
    {
        if (s_CapturesLeg != RoundManager.LegKey)
        {
            s_CapturesLeg = RoundManager.LegKey;
            s_Captures = 0;
        }

        s_Captures++;
        Plugin.Log.LogInfo($"[OTL][Kit] capture {s_Captures} this leg: gem boost x{GemStrength:0.###} for {GemSeconds:0.##}s.");
    }

    /// <summary>The gem's speed boost strength: +GemCaptureBonus per capture this leg.</summary>
    private static float GemStrength => 1f + Mathf.Max(0f, Plugin.ModConfig.GemCaptureBonus.Synced()) * CapturesThisLeg;

    /// <summary>Length of the gem's effects: GemBoostSeconds + GemCaptureExtraSeconds per capture this leg.</summary>
    private static float GemSeconds =>
        Mathf.Max(0f, Plugin.ModConfig.GemBoostSeconds.Synced())
        + Mathf.Max(0f, Plugin.ModConfig.GemCaptureExtraSeconds.Synced()) * CapturesThisLeg;

    /// <summary>
    /// Every client, each frame (from Hud): the unlimited-stamina guard, and runners' boost at the tail end of their
    /// head start.
    /// </summary>
    public static void LocalTick()
    {
        Character local = Character.localCharacter;
        GuardInfiniteStamina(local);
        if (local == null || local.data.dead || !RoleManager.IsRunner(local) || !RoundManager.InCountdown) return;
        float seconds = Plugin.ModConfig.HeadStartBoostSeconds.Synced();
        if (seconds <= 0f || RoundManager.HoldSecondsLeft > seconds || s_BoostedLeg == RoundManager.LegKey) return;
        s_BoostedLeg = RoundManager.LegKey;
        ApplyBoost(local, RoundManager.HoldSecondsLeft + 0.25f);
        Plugin.Log.LogInfo("[OTL][Kit] head start boost.");
    }

    // ---------- Unlimited stamina guard ----------

    private static float s_NextStaminaGuard;

    /// <summary>
    /// During a round, unlimited stamina (Character.infiniteStam) must only be on while an Affliction_InfiniteStamina
    /// is running: that affliction switches it on in OnApplied and off in OnRemoved. If the flag is still on without
    /// one (the gem's stamina was seen to stay unlimited after its effects ended), it is switched off and PEAK's
    /// rainbow bar is ended, as OnRemoved would.
    /// </summary>
    private static void GuardInfiniteStamina(Character? local)
    {
        if (local == null || !local.infiniteStam || !RoundManager.IsActive || Time.time < s_NextStaminaGuard) return;
        s_NextStaminaGuard = Time.time + 0.25f;
        if (local.refs.afflictions.HasAfflictionType(Affliction.AfflictionType.InfiniteStamina, out _)) return;
        local.infiniteStam = false;
        GUIManager.instance?.EndSugarRush();
        Plugin.Log.LogWarning("[OTL][Kit] unlimited stamina was still on with no sugar rush running; switched it off.");
    }

    /// <summary>The gem's effects are over: its unlimited stamina ends now too, even if PEAK's own timer hasn't run out.</summary>
    private static void EndGemStamina(Character chaser)
    {
        if (chaser == null || !chaser.IsLocal) return;
        CharacterAfflictions afflictions = chaser.refs.afflictions;
        if (afflictions.HasAfflictionType(Affliction.AfflictionType.InfiniteStamina, out Affliction stamina)
            && stamina.totalTime - stamina.timeElapsed <= 0.5f)
        {
            afflictions.RemoveAffliction(stamina); // OnRemoved: infiniteStam off, rainbow bar ended
        }

        GuardInfiniteStamina(chaser);
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
        s_GemLeg = RoundManager.LegKey;
        int petrify = Mathf.RoundToInt(cfg.GemPetrify.Synced() * 100f);
        if (petrify > 0) holder.refs.afflictions.AddPetrify(petrify);
        Plugin.Log.LogInfo($"[OTL][Kit] chaser gem used: +{petrify}% petrify, boost in {cfg.GemDelaySeconds.Synced():0.##}s.");
        ModNetwork.Instance?.StartCoroutine(GemBoostLater(holder));
        ModNetwork.Broadcast(Notice.GemReveal, Net.Actor(holder), 0); // every runner smokes for GemRevealSeconds
        PutAwayLater(holder, item);
        return true;
    }

    /// <summary>
    /// After a gem's ability has fired: put the gem back in its hotbar slot so the hands are free (the same as
    /// pressing its slot again; it is not dropped). Done a frame later, outside the item's own use call.
    /// </summary>
    public static void PutAwayLater(Character c, Item item) => ModNetwork.Instance?.StartCoroutine(PutAway(c, item));

    private static IEnumerator PutAway(Character c, Item item)
    {
        yield return null;
        if (c == null || !c.IsLocal || c.data.dead || c.data.currentItem != item) yield break; // already switched to something else
        c.refs.items.EquipSlot(Optionable<byte>.None);
    }

    /// <summary>
    /// After GemDelaySeconds: the energy-drink speed boost and, with GemInfiniteStamina, PEAK's
    /// unlimited stamina (Affliction_InfiniteStamina, the rainbow "sugar rush" bar, without its drowsiness),
    /// both for GemBoostSeconds (+ GemCaptureExtraSeconds per capture this leg).
    /// </summary>
    private static IEnumerator GemBoostLater(Character chaser)
    {
        var cfg = Plugin.ModConfig;
        float delay = Mathf.Max(0f, cfg.GemDelaySeconds.Synced());
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float seconds = GemSeconds;
        if (chaser == null || chaser.data.dead || !RoleManager.IsChaser(chaser) || !RoundManager.IsChasing)
        {
            StartGemCooldown(); // used but cancelled: the cooldown still applies
            yield break;
        }

        float strength = GemStrength;
        if (cfg.GemInfiniteStamina.Synced()) ApplySugarRush(chaser, seconds, strength);
        else ApplyBoost(chaser, seconds, strength);
        XRayVision.Begin(cfg.GemXRaySeconds.Synced(), cfg.GemXRayRange.Synced()); // runners through walls, on this chaser's screen

        Plugin.Log.LogInfo($"[OTL][Kit] chaser gem: boost x{strength:0.###}{(cfg.GemInfiniteStamina.Synced() ? " + unlimited stamina" : "")} for {seconds:0.##}s ({CapturesThisLeg} captures this leg).");

        if (seconds > 0f) yield return new WaitForSeconds(seconds);
        EndGemStamina(chaser);
        StartGemCooldown();
    }

    private static void StartGemCooldown()
    {
        if (s_GemLeg != RoundManager.LegKey) return; // a new leg started meanwhile: it refreshed every cooldown
        s_GemActive = false;
        s_GemReadyAt = Time.time + Mathf.Max(1f, Plugin.ModConfig.GemCooldownSeconds.Synced());
    }

    /// <summary>
    /// Every client, when a leg starts: every ability cooldown of the local player is refreshed (chaser gem, blowgun,
    /// runner dash). Freeze cooldowns are the host's and are reset by RoundManager.HostStartLeg.
    /// </summary>
    private static void ResetCooldowns()
    {
        s_GemActive = false;
        s_GemReadyAt = 0f;
        BlowgunSystem.ResetCooldown();
        RunnerDash.ResetCooldown();
        Plugin.Log.LogInfo("[OTL][Kit] new leg: ability cooldowns refreshed.");
    }

    /// <summary>Every client, when a leg ends and when the next one starts: the local player's petrification is cleared.</summary>
    private static void ClearPetrify(string why)
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || !RoundManager.IsActive || !Plugin.ModConfig.ClearPetrifyEachLeg.Synced()) return;
        int current = local.data.petrifyAmount;
        if (current <= 0) return;
        local.refs.afflictions.AddPetrify(-current);
        Plugin.Log.LogInfo($"[OTL][Kit] {why}: petrification cleared (-{current}%).");
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

    // ---------- Snowballs: knockback and blindness ----------

    /// <summary>Snowball view id -> when it last hit someone, so one throw counts once (it touches several body parts).</summary>
    private static readonly Dictionary<int, float> s_SnowballHits = new();

    /// <summary>
    /// Runs on the client that simulates the snowball (its owner: the thrower), the only place where the collision
    /// has the snowball's real speed. The hit scout's own client owns their body, so the hit is sent to them
    /// (<see cref="Msg.SnowballHit"/>) and applied there (<see cref="ApplySnowballHit"/>).
    /// </summary>
    public static void SnowballHitPostfix(Snowball __instance, Collision other)
    {
        if (!RoundManager.IsActive || other?.collider == null) return;
        Item item = __instance.item;
        if (item == null || item.view == null || !item.view.IsMine || item.lastThrownCharacter == null || Time.time - item.lastThrownTime > 3f) return;
        // PEAK's own body-part collider registry first (CharacterRagdoll.RegisterBodypartColliders).
        if (!CharacterRagdoll.TryGetCharacterFromCollider(other.collider, out Character hit)) hit = other.collider.GetComponentInParent<Character>();
        if (hit == null || hit == item.lastThrownCharacter || hit.data.dead || hit.isBot) return;
        if (RoleManager.IsRunner(item.lastThrownCharacter) && RoleManager.IsRunner(hit)) return; // runners can't affect each other
        int id = item.view.ViewID;
        if (s_SnowballHits.TryGetValue(id, out float last) && Time.time - last < 1f) return;
        s_SnowballHits[id] = Time.time;

        // The extra push: SnowballKnockbackBonus of the snowball's impact momentum, away from the snowball.
        Vector3 push = Vector3.zero;
        float bonus = Plugin.ModConfig.SnowballKnockbackBonus.Synced();
        Vector3 away = (other.rigidbody != null ? other.rigidbody.worldCenterOfMass : other.collider.bounds.center) - item.transform.position;
        if (bonus > 0f && item.rig != null && away.sqrMagnitude > 0.0001f)
        {
            push = away.normalized * (item.rig.mass * other.relativeVelocity.magnitude * bonus);
        }

        Bodypart? part = other.collider.GetComponentInParent<Bodypart>();
        int partType = part != null ? (int)part.partType : (int)BodypartType.Hip;
        Plugin.Log.LogInfo($"[OTL][Kit] snowball thrown by {item.lastThrownCharacter.characterName} hit {hit.characterName} ({(BodypartType)partType}), extra push {push.magnitude:0.##}.");
        int thrower = Net.Actor(item.lastThrownCharacter);
        if (hit.IsLocal) ApplySnowballHit(thrower, push, partType);
        else Net.SendToActor(Net.Actor(hit), Msg.SnowballHit, thrower, push.x, push.y, push.z, partType);
    }

    /// <summary>
    /// The hit scout's own client: the extra knockback on the body part that was hit, and if a runner's snowball hit a
    /// chaser, SnowballBlindSeconds of blue-flower blindness (Affliction_Blind).
    /// </summary>
    public static void ApplySnowballHit(int throwerActor, Vector3 push, int partType)
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || !RoundManager.IsActive) return;
        if (push.sqrMagnitude > 0f && local.GetBodypart((BodypartType)partType) is { } part && part.Rig != null)
        {
            part.Rig.AddForce(push, ForceMode.Impulse);
        }

        float seconds = Plugin.ModConfig.SnowballBlindSeconds.Synced();
        Character? thrower = Net.CharacterOf(throwerActor);
        if (seconds <= 0f || !RoundManager.IsChasing || !RoleManager.IsChaser(local) || !RoleManager.IsRunner(thrower)) return;
        local.refs.afflictions.AddAffliction(new Affliction_Blind { totalTime = seconds });
        Plugin.Log.LogInfo($"[OTL][Kit] hit by {thrower!.characterName}'s snowball: blind for {seconds:0.#}s.");
    }

    // ---------- Golden Bing Bong ----------

    public static bool BingBongShieldPrefix() => !(RoundManager.IsActive && Plugin.ModConfig.BanGoldenBingBong.Synced());
}

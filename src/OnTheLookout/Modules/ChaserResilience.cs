using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Chasers are tougher than runners during a round:
/// - they take only a fraction (ChaserStatusMultiplier, 1/2; 3/4 in the Caldera or swamp biome) of every negative
///   status they would get in vanilla (injury, cold, poison, drowsy, ...; ascent scaling is already in the vanilla amount). Hunger is
///   left exactly as vanilla, the same as for runners;
/// - fall damage is reduced (default 1/3 of vanilla, still scaled by the ascent like vanilla);
/// - (runners) no fall damage at all shortly after a dash (<see cref="RunnerDash"/>);
/// - mushroom zombies ignore them and their bites do nothing to them.
/// Statuses are owned by each player's own client, so all of this runs on the chaser's client.
/// </summary>
internal static class ChaserResilience
{
    /// <summary>Frame in which a fall-damage check is running (its injury uses the fall multiplier).</summary>
    private static int s_FallFrame = -1;

    public static bool Install(Harmony harmony)
    {
        const string m = "Resilience";
        bool ok = true;

        // CharacterAfflictions.AddStatus(STATUSTYPE, float amount, ...) (prefix). Why: every positive status
        // change goes through here (AdjustStatus with amount > 0 calls it too).
        ok &= SafePatch.Prefix(harmony, typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus), typeof(ChaserResilience), nameof(AddStatusPrefix), m);

        // CharacterMovement.CheckFallDamage() and CharacterClimbing.CheckFallDamage(RaycastHit) (prefix + postfix, private).
        // Why: PEAK's two fall-damage paths (landing, and sliding into a wall while climbing). Both already
        // apply Ascents.fallDamageMultiplier before AddStatus(Injury), so scaling that injury keeps ascent scaling.
        ok &= SafePatch.Prefix(harmony, typeof(CharacterMovement), "CheckFallDamage", typeof(ChaserResilience), nameof(FallPrefix), m);
        ok &= SafePatch.Postfix(harmony, typeof(CharacterMovement), "CheckFallDamage", typeof(ChaserResilience), nameof(FallPostfix), m);
        ok &= SafePatch.Prefix(harmony, typeof(CharacterClimbing), "CheckFallDamage", typeof(ChaserResilience), nameof(FallPrefix), m);
        ok &= SafePatch.Postfix(harmony, typeof(CharacterClimbing), "CheckFallDamage", typeof(ChaserResilience), nameof(FallPostfix), m);

        // MushroomZombie.TargetIsValid(Character) (postfix, private). Why: decides who a zombie chases.
        ok &= SafePatch.Postfix(harmony, typeof(MushroomZombie), "TargetIsValid", typeof(ChaserResilience), nameof(ZombieTargetPostfix), m);

        // MushroomZombieBiteCollider.OnTriggerEnter(Collider) (prefix, private). Why: applies the bite (injury, spores) to the local player.
        ok &= SafePatch.Prefix(harmony, typeof(MushroomZombieBiteCollider), "OnTriggerEnter", typeof(ChaserResilience), nameof(ZombieBitePrefix), m);
        return ok;
    }

    /// <summary>
    /// Skips the whole check (no damage, no knock-down) for the local runner inside their dash's fall protection
    /// (<see cref="RunnerDash.FallProtected"/>); otherwise marks the frame so the injury can be scaled for chasers.
    /// </summary>
    public static bool FallPrefix(Character ___character)
    {
        if (___character != null && ___character.IsLocal && RunnerDash.FallProtected) return false;

        s_FallFrame = Time.frameCount;
        return true;
    }

    public static void FallPostfix() => s_FallFrame = -1;

    private static bool s_Exact;

    /// <summary>Adds a status at exactly <paramref name="amount"/>, without the chasers' toughness (e.g. a runner's stone).</summary>
    public static void AddExact(Character c, CharacterAfflictions.STATUSTYPE statusType, float amount)
    {
        s_Exact = true;
        try { c.refs.afflictions.AddStatus(statusType, amount); }
        finally { s_Exact = false; }
    }

    public static void AddStatusPrefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount, bool fromRPC)
    {
        if (s_Exact) return;
        // Weight is inventory-driven, and hunger stays the same for everyone.
        if (fromRPC || amount <= 0f || statusType is CharacterAfflictions.STATUSTYPE.Weight or CharacterAfflictions.STATUSTYPE.Hunger) return;
        Character c = __instance.character;
        if (c == null || !c.IsLocal || !RoleManager.IsChaser(c)) return;

        bool fromFall = statusType == CharacterAfflictions.STATUSTYPE.Injury && s_FallFrame == Time.frameCount;
        float multiplier = fromFall
            ? Plugin.ModConfig.ChaserFallDamageMultiplier.Synced()
            : InCalderaBiome()
                ? Plugin.ModConfig.ChaserStatusMultiplierCaldera.Synced()
                : Plugin.ModConfig.ChaserStatusMultiplier.Synced();
        amount *= Mathf.Clamp01(multiplier);
    }

    private static int s_BiomeFrame = -1;
    private static bool s_InCaldera;

    /// <summary>
    /// This leg is in the fourth biome: the Caldera (Biome.BiomeType.Volcano) or the swamp, the gloom biome some maps
    /// have in its place (Biome.BiomeType.Swamp). The Kiln is not included. Checked once per frame.
    /// </summary>
    private static bool InCalderaBiome()
    {
        if (s_BiomeFrame == Time.frameCount) return s_InCaldera;
        s_BiomeFrame = Time.frameCount;
        s_InCaldera = false;
        try
        {
            int segment = SafeZoneSystem.LastLitSegment >= 0 ? SafeZoneSystem.LastLitSegment : (int)MapHandler.CurrentSegmentNumber;
            if (segment != (int)Segment.Caldera || !MapHandler.Exists) return false;
            Biome.BiomeType biome = MapHandler.Instance.segments[segment].biome;
            s_InCaldera = biome is Biome.BiomeType.Volcano or Biome.BiomeType.Swamp;
        }
        catch
        {
            // no map
        }

        return s_InCaldera;
    }

    public static void ZombieTargetPostfix(Character target, ref bool __result)
    {
        if (__result && Plugin.ModConfig.ZombiesIgnoreChasers.Synced() && RoleManager.IsChaser(target)) __result = false;
    }

    public static bool ZombieBitePrefix(Collider other) =>
        !(Plugin.ModConfig.ZombiesIgnoreChasers.Synced()
          && CharacterRagdoll.TryGetCharacterFromCollider(other, out Character character)
          && RoleManager.IsChaser(character));
}

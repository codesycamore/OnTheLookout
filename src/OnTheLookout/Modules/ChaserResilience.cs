using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Chasers are tougher than runners during a round:
/// - they take only a fraction (default 1/3) of every negative status they would get in vanilla
///   (injury, hunger, cold, poison, drowsy, ...; ascent scaling is already in the vanilla amount);
/// - they never take fall damage (or the fall knock-down);
/// - mushroom zombies ignore them and their bites do nothing to them.
/// Statuses are owned by each player's own client, so all of this runs on the chaser's client.
/// </summary>
internal static class ChaserResilience
{
    public static bool Install(Harmony harmony)
    {
        const string m = "Resilience";
        bool ok = true;

        // CharacterAfflictions.AddStatus(STATUSTYPE, float amount, ...) (prefix). Why: every positive status
        // change goes through here (AdjustStatus with amount > 0 calls it too).
        ok &= SafePatch.Prefix(harmony, typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus), typeof(ChaserResilience), nameof(AddStatusPrefix), m);

        // CharacterMovement.CheckFallDamage() (prefix, private). Why: computes fall damage and the fall ragdoll on landing.
        ok &= SafePatch.Prefix(harmony, typeof(CharacterMovement), "CheckFallDamage", typeof(ChaserResilience), nameof(FallDamagePrefix), m);

        // MushroomZombie.TargetIsValid(Character) (postfix, private). Why: decides who a zombie chases.
        ok &= SafePatch.Postfix(harmony, typeof(MushroomZombie), "TargetIsValid", typeof(ChaserResilience), nameof(ZombieTargetPostfix), m);

        // MushroomZombieBiteCollider.OnTriggerEnter(Collider) (prefix, private). Why: applies the bite (injury, spores) to the local player.
        ok &= SafePatch.Prefix(harmony, typeof(MushroomZombieBiteCollider), "OnTriggerEnter", typeof(ChaserResilience), nameof(ZombieBitePrefix), m);
        return ok;
    }

    public static void AddStatusPrefix(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount, bool fromRPC)
    {
        if (fromRPC || amount <= 0f || statusType == CharacterAfflictions.STATUSTYPE.Weight) return;
        Character c = __instance.character;
        if (c == null || !c.IsLocal || !RoleManager.IsChaser(c)) return;
        amount *= Mathf.Clamp01(Plugin.ModConfig.ChaserStatusMultiplier.Synced());
    }

    public static bool FallDamagePrefix(CharacterMovement __instance) =>
        !(Plugin.ModConfig.ChaserNoFallDamage.Synced() && RoleManager.IsChaser(__instance.character));

    public static void ZombieTargetPostfix(Character target, ref bool __result)
    {
        if (__result && Plugin.ModConfig.ZombiesIgnoreChasers.Synced() && RoleManager.IsChaser(target)) __result = false;
    }

    public static bool ZombieBitePrefix(Collider other) =>
        !(Plugin.ModConfig.ZombiesIgnoreChasers.Synced()
          && CharacterRagdoll.TryGetCharacterFromCollider(other, out Character character)
          && RoleManager.IsChaser(character));
}

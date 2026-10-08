using HarmonyLib;
using OnTheLookout.Core;

namespace OnTheLookout.Modules;

/// <summary>
/// Chasers move a bit faster than runners (they can't use most items).
/// </summary>
internal static class ChaserSpeed
{
    public static bool Install(Harmony harmony) =>
        // Patch target: CharacterMovement.GetMovementForce() (postfix, private).
        // Why: the walking/sprinting force for this physics step; PEAK's own speed buff
        // (Affliction_FasterBoi) changes the same value through movementModifier.
        SafePatch.Postfix(harmony, typeof(CharacterMovement), "GetMovementForce", typeof(ChaserSpeed), nameof(Postfix), "Speed");

    public static void Postfix(CharacterMovement __instance, ref float __result)
    {
        if (__result > 0f && RoleManager.IsChaser(__instance.character))
        {
            __result *= Plugin.ModConfig.ChaserSpeedMultiplier.Synced();
        }
    }
}

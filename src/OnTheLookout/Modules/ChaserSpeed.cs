using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Chasers move a bit faster than runners (they can't use most items). Their capture boost is in <see cref="ChaserKit"/>.
/// </summary>
internal static class ChaserSpeed
{
    public static bool Install(Harmony harmony)
    {
        // Patch target: CharacterMovement.GetMovementForce() (postfix, private).
        // Why: the walking/sprinting force for this physics step; PEAK's own speed buff
        // (Affliction_FasterBoi) changes the same value through movementModifier.
        return SafePatch.Postfix(harmony, typeof(CharacterMovement), "GetMovementForce", typeof(ChaserSpeed), nameof(Postfix), "Speed");
    }

    public static void Postfix(CharacterMovement __instance, ref float __result)
    {
        Character c = __instance.character;
        if (__result <= 0f || !RoleManager.IsChaser(c)) return;
        __result *= Plugin.ModConfig.ChaserSpeedMultiplier.Synced();
    }
}

/// <summary>
/// Chasers climb a bit faster (walls, ropes and vines). PEAK's climbing speed is the additive
/// climbSpeedMod on CharacterClimbing / CharacterRopeHandling / CharacterVineClimbing (base 1; the energy
/// drink's Affliction_FasterBoi adds to it the same way). Climbing is simulated by the player's own
/// client, so only the local character is adjusted; the exact amount added is tracked and removed again
/// when the player stops being a chaser.
/// </summary>
internal sealed class ChaserClimbBoost : MonoBehaviour
{
    private Character? _applied;
    private float _delta;

    private void Update()
    {
        Character local = Character.localCharacter;
        float want = local != null && RoleManager.IsChaser(local)
            ? Plugin.ModConfig.ChaserClimbSpeedMultiplier.Synced() - 1f
            : 0f;

        if (_applied != local || !Mathf.Approximately(want, _delta))
        {
            Remove();
            if (local != null && want != 0f) Apply(local, want);
        }
    }

    private void Apply(Character c, float delta)
    {
        c.refs.climbing.climbSpeedMod += delta;
        c.refs.ropeHandling.climbSpeedMod += delta;
        c.refs.vineClimbing.climbSpeedMod += delta;
        _applied = c;
        _delta = delta;
    }

    private void Remove()
    {
        if (_applied != null && _delta != 0f)
        {
            _applied.refs.climbing.climbSpeedMod -= _delta;
            _applied.refs.ropeHandling.climbSpeedMod -= _delta;
            _applied.refs.vineClimbing.climbSpeedMod -= _delta;
        }

        _applied = null;
        _delta = 0f;
    }

    private void OnDisable() => Remove();
}

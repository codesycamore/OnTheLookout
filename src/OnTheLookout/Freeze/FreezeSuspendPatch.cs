using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Freeze;

/// <summary>
/// Holds a chaser in mid-air when they are frozen while airborne.
/// PEAK applies gravity itself: CharacterMovement.FixedUpdate calls Bodypart.Gravity(...) on every
/// body part while not grounded, and enables Rigidbody.useGravity when ragdoll control drops below 0.9.
/// Movement is owner-simulated, so suspending on the chaser's own client is what everyone sees.
/// </summary>
internal static class FreezeSuspendPatch
{
    private static Vector3? s_Anchor;

    /// <summary>False if either hook failed to apply (e.g. after a game update); suspension is then skipped.</summary>
    public static bool HooksAvailable { get; set; }

    public static bool Suspended => s_Anchor.HasValue;

    /// <summary>Called when the local freeze starts. Only suspends if the player is truly airborne.</summary>
    public static void TryBegin(Character local)
    {
        CharacterData d = local.data;
        bool airborne = !d.isGrounded && !d.isClimbing && !d.isRopeClimbing && !d.isVineClimbing
            && d.currentClimbHandle == null;
        if (!HooksAvailable || !Plugin.ModConfig.FreezeSuspendInAir.Synced() || !airborne)
        {
            return;
        }

        s_Anchor = local.GetBodypart(BodypartType.Hip).Rig.position;
        Plugin.Log.LogInfo($"[OTL][Freeze] suspending mid-air at {s_Anchor.Value}");
    }

    public static void End()
    {
        if (s_Anchor.HasValue)
        {
            Plugin.Log.LogInfo("[OTL][Freeze] suspension released");
        }

        s_Anchor = null;
    }

    /// <summary>
    /// Patch target: <c>Bodypart.Gravity(Vector3)</c> (prefix).
    /// Why: this is PEAK's custom per-part gravity; skipping it for the suspended local player
    /// removes gravity at the source instead of fighting it afterwards.
    /// </summary>
    public static bool GravityPrefix(Bodypart __instance) =>
        !(s_Anchor.HasValue && __instance.character == Character.localCharacter);

    /// <summary>
    /// Patch target: <c>CharacterMovement.FixedUpdate()</c> (postfix).
    /// Why: runs after the game queued this step's forces and toggled useGravity, but before the
    /// physics step, so our overrides are the last word for this step.
    /// </summary>
    public static void FixedUpdatePostfix(CharacterMovement __instance)
    {
        if (!s_Anchor.HasValue)
        {
            return;
        }

        Character character = __instance.character;
        if (character != Character.localCharacter)
        {
            return;
        }

        // Pull the whole body back toward the anchor; kills jump momentum and any drift from
        // animation forces while still letting the ragdoll hold its pose.
        Vector3 hip = character.GetBodypart(BodypartType.Hip).Rig.position;
        Vector3 pull = (s_Anchor.Value - hip) * Plugin.ModConfig.FreezeSuspendStiffness.Synced();
        foreach (Bodypart part in character.refs.ragdoll.partList)
        {
            if (part == null || part.Rig == null) continue;
            part.Rig.useGravity = false;
            part.Rig.linearVelocity = pull;
        }

        // Fall damage uses min(sinceJump, sinceGrounded); the game resets sinceGrounded the same way
        // for super-jumps and jetpacks. Hanging in the air must not count as falling.
        character.data.sinceGrounded = 0f;
    }
}

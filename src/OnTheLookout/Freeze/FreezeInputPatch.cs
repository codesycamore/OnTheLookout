using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Freeze;

/// <summary>
/// Patch target: <c>CharacterInput.Sample(bool)</c> (postfix).
/// Why: Sample is the single place the LOCAL player's input is read each frame
/// (called from CharacterMovement.Update only when character.IsLocal). Everything else
/// (walking, climbing, jumping, item use, interact) consumes the fields it fills, and movement
/// is owner-simulated, so blocking here holds the player in place for everyone.
/// Used for both the freeze (rules 3-5) and the chasers' head-start hold (rule 2).
/// </summary>
internal static class FreezeInputPatch
{
    private static bool s_WasFrozen;

    public static void SamplePostfix(CharacterInput __instance)
    {
        Character local = Character.localCharacter;
        if (local == null || local.input != __instance)
        {
            return;
        }

        // A full freeze for: a look-freeze; runners during the role reveal (so everyone is frozen while it shows);
        // chasers during the reveal + head start.
        bool frozen = Net.InRoom && (FreezeState.IsFrozen(Net.Actor(local))
            || (RoundManager.InReveal && RoleManager.IsRunner(local))
            || (RoundManager.InHold && RoleManager.IsChaser(local)));

        if (frozen && !s_WasFrozen)
        {
            FreezeSystem.OnLocalFreezeStarted(local);
        }
        else if (!frozen && s_WasFrozen)
        {
            FreezeSystem.OnLocalFreezeEnded(local);

            // We swallowed the grab-button release while frozen (to keep the grip). If the player is
            // no longer holding grab, deliver that release now so they drop like they would have.
            if (Plugin.ModConfig.FreezeHoldGrip.Synced() && local.data.isClimbing
                && !CharacterInput.action_usePrimary.IsPressed())
            {
                __instance.usePrimaryWasReleased = true;
            }
        }

        s_WasFrozen = frozen;

        // On the shore before the round starts nobody can move (input only: no mid-air suspension, so the
        // intro fall and waking up play out normally).
        // Same during the role window at the campfire after a leg.
        if (frozen || RoundManager.InPreRound || RoundManager.InRoleWindow)
        {
            Block(__instance);
        }
    }

    private static void Block(CharacterInput input)
    {
        Vector2 look = input.lookInput;
        bool released = input.usePrimaryWasReleased;

        // The game's own reset clears movement, jump, sprint, use, interact, drop, crouch, emote.
        // Clearing usePrimaryWasReleased is what keeps a climbing player on the wall.
        input.ResetInput();

        if (!Plugin.ModConfig.FreezeBlockLook.Synced())
        {
            input.lookInput = look;
        }

        if (!Plugin.ModConfig.FreezeHoldGrip.Synced())
        {
            input.usePrimaryWasReleased = released;
        }

        // Fields ResetInput doesn't cover.
        input.scrollInput = 0f;
        input.scrollBackwardWasPressed = false;
        input.scrollForwardWasPressed = false;
        input.scrollBackwardIsPressed = false;
        input.scrollForwardIsPressed = false;
        input.selectSlotForwardWasPressed = false;
        input.selectSlotBackwardWasPressed = false;
        input.unselectSlotWasPressed = false;
        input.selectBackpackWasPressed = false;
        input.sprintToggleIsPressed = false;
        input.pingWasPressed = false;
    }
}

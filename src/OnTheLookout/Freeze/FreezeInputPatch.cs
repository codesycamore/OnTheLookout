using UnityEngine;

namespace OnTheLookout.Freeze;

/// <summary>
/// Patch target: <c>CharacterInput.Sample(bool)</c> (postfix).
/// Why: Sample is the single place the LOCAL player's input is read each frame
/// (called from CharacterMovement.Update only when character.IsLocal). Everything else
/// (walking, climbing, jumping, item use, interact) consumes the fields it fills, and movement
/// is owner-simulated, so blocking here freezes the player for everyone.
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

        int actor = local.view.Owner.ActorNumber;
        bool frozen = FreezeState.IsFrozen(actor);

        if (frozen)
        {
            if (!s_WasFrozen)
            {
                FreezeSystem.OnLocalFreezeStarted(local);
            }

            Block(__instance);
        }
        else if (s_WasFrozen)
        {
            FreezeSystem.OnLocalFreezeEnded(local);

            // We swallowed the grab-button release while frozen (to keep the grip). If the player is
            // no longer holding grab, deliver that release now so they drop like they would have.
            if (Plugin.ModConfig.FreezeHoldGrip.Value && local.data.isClimbing
                && !CharacterInput.action_usePrimary.IsPressed())
            {
                __instance.usePrimaryWasReleased = true;
            }
        }

        s_WasFrozen = frozen;
    }

    private static void Block(CharacterInput input)
    {
        Vector2 look = input.lookInput;
        bool released = input.usePrimaryWasReleased;

        // The game's own reset clears movement, jump, sprint, use, interact, drop, crouch, emote.
        // Clearing usePrimaryWasReleased is what keeps a climbing player on the wall.
        input.ResetInput();

        if (!Plugin.ModConfig.FreezeBlockLook.Value)
        {
            input.lookInput = look;
        }

        if (!Plugin.ModConfig.FreezeHoldGrip.Value)
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

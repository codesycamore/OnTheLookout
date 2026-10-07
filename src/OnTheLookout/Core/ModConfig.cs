using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace OnTheLookout.Core;

/// <summary>
/// All tunables live here. Only freeze-related keys exist so far (proof of concept).
/// </summary>
internal sealed class ModConfig
{
    // Freeze (rules 3-5)
    public ConfigEntry<float> FreezeRange { get; }
    public ConfigEntry<float> FreezeConeDegrees { get; }
    public ConfigEntry<float> FreezeDuration { get; }
    public ConfigEntry<float> FreezeCooldownSeconds { get; }

    // Freeze behaviour knobs, exposed so they can be compared in-game without rebuilding.
    public ConfigEntry<bool> FreezeHoldGrip { get; }
    public ConfigEntry<bool> FreezeLockStamina { get; }
    public ConfigEntry<bool> FreezeZeroVelocity { get; }
    public ConfigEntry<bool> FreezeBlockLook { get; }
    public ConfigEntry<bool> FreezeSuspendInAir { get; }
    public ConfigEntry<float> FreezeSuspendStiffness { get; }

    // Debug / proof-of-concept controls
    public ConfigEntry<Key> KeySelfFreeze { get; }
    public ConfigEntry<Key> KeyFreezeLookTarget { get; }
    public ConfigEntry<Key> KeyToggleHostIsChaser { get; }
    public ConfigEntry<bool> HostIsChaserTest { get; }
    public ConfigEntry<bool> LogLookChecks { get; }

    public ModConfig(ConfigFile config)
    {
        const string freeze = "Freeze";
        FreezeRange = config.Bind(freeze, "FreezeRange", 25f,
            "Max distance (m) at which a runner looking at a chaser freezes them.");
        FreezeConeDegrees = config.Bind(freeze, "FreezeConeDegrees", 15f,
            "Half-angle of the look cone (degrees). Smaller = runner must aim more precisely.");
        FreezeDuration = config.Bind(freeze, "FreezeDuration", 5f,
            "Seconds a chaser stays frozen. Does not stack or extend.");
        FreezeCooldownSeconds = config.Bind(freeze, "FreezeCooldownSeconds", 8f,
            "Seconds after a freeze ENDS before that chaser can be frozen again.");
        FreezeHoldGrip = config.Bind(freeze, "FreezeHoldGrip", true,
            "If frozen while climbing, keep holding the wall even if the grab button is released.");
        FreezeLockStamina = config.Bind(freeze, "FreezeLockStamina", true,
            "Keep stamina at its value from when the freeze started (climbing drains stamina even when still).");
        FreezeZeroVelocity = config.Bind(freeze, "FreezeZeroVelocity", false,
            "Zero all ragdoll velocities every physics step while frozen (stops sliding/drifting).");
        FreezeBlockLook = config.Bind(freeze, "FreezeBlockLook", false,
            "Also block camera look while frozen.");
        FreezeSuspendInAir = config.Bind(freeze, "FreezeSuspendInAir", true,
            "If frozen while airborne (e.g. mid-jump), hang in the air at that spot until the freeze ends.");
        FreezeSuspendStiffness = config.Bind(freeze, "FreezeSuspendStiffness", 10f,
            "How strongly a suspended player is pulled back to the freeze point (1/s). Higher = stiffer.");

        const string debug = "Debug";
        KeySelfFreeze = config.Bind(debug, "KeySelfFreeze", Key.F8,
            "HOST: freeze yourself (solo test of the input block).");
        KeyFreezeLookTarget = config.Bind(debug, "KeyFreezeLookTarget", Key.F9,
            "HOST: freeze the player you are looking at (tests look-check + replication to a remote player).");
        KeyToggleHostIsChaser = config.Bind(debug, "KeyToggleHostIsChaser", Key.F10,
            "HOST: toggle HostIsChaserTest.");
        HostIsChaserTest = config.Bind(debug, "HostIsChaserTest", false,
            "HOST acts as a chaser: any other player looking at the host freezes the host (rules 3-5 end to end).");
        LogLookChecks = config.Bind(debug, "LogLookChecks", false,
            "Log distance/angle/occlusion for every look check (noisy).");
    }
}

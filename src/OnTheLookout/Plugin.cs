using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTheLookout.Core;
using OnTheLookout.Freeze;
using UnityEngine;

namespace OnTheLookout;

/// <summary>
/// The BepInEx plugin class of OnTheLookout.
/// </summary>
[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ModConfig ModConfig { get; private set; } = null!;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Log = Logger;
        ModConfig = new ModConfig(Config);
        _harmony = new Harmony(Id);

        // Freeze module (proof of concept). If the hook is missing, only this module is disabled.
        bool freezeOk = SafePatch.Postfix(_harmony, typeof(CharacterInput), nameof(CharacterInput.Sample),
            typeof(FreezeInputPatch), nameof(FreezeInputPatch.SamplePostfix), "Freeze");
        if (freezeOk)
        {
            // Mid-air suspension is optional polish: if these hooks break, freezing still works on the ground/walls.
            bool gravityOk = SafePatch.Prefix(_harmony, typeof(Bodypart), nameof(Bodypart.Gravity),
                typeof(FreezeSuspendPatch), nameof(FreezeSuspendPatch.GravityPrefix), "Freeze.Suspend");
            bool fixedOk = gravityOk && SafePatch.Postfix(_harmony, typeof(CharacterMovement), "FixedUpdate",
                typeof(FreezeSuspendPatch), nameof(FreezeSuspendPatch.FixedUpdatePostfix), "Freeze.Suspend");
            FreezeSuspendPatch.HooksAvailable = fixedOk;

            var host = new GameObject("OnTheLookout");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<FreezeSystem>();
        }

        Log.LogInfo($"[OTL] Plugin {Name} {Version} loaded. Freeze module: {(freezeOk ? "enabled" : "DISABLED")}.");
    }
}

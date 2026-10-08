using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OnTheLookout.Core;
using OnTheLookout.Freeze;
using OnTheLookout.Modules;
using OnTheLookout.UI;
using UnityEngine;

namespace OnTheLookout;

/// <summary>
/// The BepInEx plugin class of OnTheLookout: a host-authoritative chasers-vs-runners mode.
/// Every module installs its own patches and is disabled on its own if a hook is missing.
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
        var cfg = ModConfig;

        var root = new GameObject("OnTheLookout");
        DontDestroyOnLoad(root);
        root.hideFlags = HideFlags.HideAndDontSave;
        root.AddComponent<ModNetwork>();

        // Round start. Patch target: RunManager.StartRun() (postfix). Why: called once when a run begins; the round
        // itself starts once everyone has woken up on the beach (RoundManager.HostStartWhenReady).
        bool round = SafePatch.Postfix(_harmony, typeof(RunManager), nameof(RunManager.StartRun), typeof(Plugin), nameof(StartRunPostfix), "Round");

        // Input hold for head start (rule 2) and freeze (rules 3-5).
        bool input = SafePatch.Postfix(_harmony, typeof(CharacterInput), nameof(CharacterInput.Sample),
            typeof(FreezeInputPatch), nameof(FreezeInputPatch.SamplePostfix), "Freeze");
        if (input)
        {
            root.AddComponent<FreezeSystem>();
            FreezeSystem.LookChecksEnabled = cfg.EnableFreeze.Value;

            // Mid-air suspension is optional polish: if these hooks break, freezing still works on the ground/walls.
            bool gravity = SafePatch.Prefix(_harmony, typeof(Bodypart), nameof(Bodypart.Gravity),
                typeof(FreezeSuspendPatch), nameof(FreezeSuspendPatch.GravityPrefix), "Freeze.Suspend");
            FreezeSuspendPatch.HooksAvailable = gravity && SafePatch.Postfix(_harmony, typeof(CharacterMovement), "FixedUpdate",
                typeof(FreezeSuspendPatch), nameof(FreezeSuspendPatch.FixedUpdatePostfix), "Freeze.Suspend");
        }

        // Campfire rules always apply during a round (otherwise runners can't light campfires while chasers are away).
        bool campfire = SafeZoneSystem.Install(_harmony, cfg.EnableSafeZones.Value);
        bool tag = cfg.EnableTag.Value && TagSystem.Install(_harmony);
        bool fog = cfg.EnableFog.Value && FogSystem.Install(_harmony);
        bool items = cfg.EnableItemRules.Value && ItemRules.Install(_harmony);
        bool conversion = cfg.EnableConversion.Value && ConversionSystem.Install(_harmony);
        bool speed = ChaserSpeed.Install(_harmony);
        bool blowgun = BlowgunSystem.Install(_harmony);
        bool tweaks = Tweaks.Install(_harmony);
        bool resilience = ChaserResilience.Install(_harmony);
        bool shroomberry = ShroomberryRules.Install(_harmony);
        bool milk = MilkRules.Install(_harmony);
        LegLoadout.Install();
        RewardSystem.Enabled = cfg.EnableRewards.Value;

        var ui = new GameObject("OnTheLookout_UI");
        DontDestroyOnLoad(ui);
        ui.AddComponent<Hud>();
        ui.AddComponent<GhostVisibility>();
        ui.AddComponent<ChaseEffects>();
        ui.AddComponent<HostMenu>();
        ui.AddComponent<ChaserOddsMenu>();
        HostMenu.Install(_harmony);
        root.AddComponent<AdminRestart>();
        root.AddComponent<ChaserClimbBoost>();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => ItemCatalog.OnSceneLoaded();

        Log.LogInfo($"[OTL] {Name} {Version} loaded. round={round} freeze={input && cfg.EnableFreeze.Value} " +
            $"suspend={FreezeSuspendPatch.HooksAvailable} campfire={campfire} tag={tag} fog={fog} items={items} " +
            $"conversion={conversion} speed={speed} blowgun={blowgun} tweaks={tweaks} resilience={resilience} shroomberry={shroomberry} milk={milk} rewards={RewardSystem.Enabled}");
    }

    private static void StartRunPostfix()
    {
        if (Net.InRoom && Net.IsHost && ModConfig.AutoStartRound.Value)
        {
            ModNetwork.Instance?.StartCoroutine(RoundManager.HostStartWhenReady());
        }
    }
}

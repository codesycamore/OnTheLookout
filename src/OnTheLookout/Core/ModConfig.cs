using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace OnTheLookout.Core;

/// <summary>
/// All tunables. Gameplay settings are host-synced (read them with <c>.Synced()</c>);
/// debug and personal settings are local only. Module toggles take effect after a restart.
/// </summary>
internal sealed class ModConfig
{
    private readonly ConfigFile _file;

    // Modules
    public ConfigEntry<bool> EnableFreeze { get; }
    public ConfigEntry<bool> EnableTag { get; }
    public ConfigEntry<bool> EnableSafeZones { get; }
    public ConfigEntry<bool> EnableFog { get; }
    public ConfigEntry<bool> EnableItemRules { get; }
    public ConfigEntry<bool> EnableConversion { get; }
    public ConfigEntry<bool> EnableRewards { get; }

    // Round & roles
    public ConfigEntry<int> ChaserCount { get; }
    public ConfigEntry<float> RoleRevealSeconds { get; }
    public ConfigEntry<float> HeadStartSeconds { get; }
    public ConfigEntry<bool> AutoStartRound { get; }
    public ConfigEntry<float> ChaserSpeedMultiplier { get; }

    // Freeze
    public ConfigEntry<float> FreezeRange { get; }
    public ConfigEntry<float> FreezeConeDegrees { get; }
    public ConfigEntry<float> FreezeDuration { get; }
    public ConfigEntry<float> FreezeCooldownSeconds { get; }
    public ConfigEntry<bool> FreezeHoldGrip { get; }
    public ConfigEntry<bool> FreezeLockStamina { get; }
    public ConfigEntry<bool> FreezeZeroVelocity { get; }
    public ConfigEntry<bool> FreezeBlockLook { get; }
    public ConfigEntry<bool> FreezeSuspendInAir { get; }
    public ConfigEntry<float> FreezeSuspendStiffness { get; }
    public ConfigEntry<float> FreezePulseFastInterval { get; }
    public ConfigEntry<float> FreezePulseSlowInterval { get; }

    // Tag
    public ConfigEntry<float> TagMaxDistance { get; }
    public ConfigEntry<bool> TagPassedOutRunners { get; }

    // Safe zones
    public ConfigEntry<float> CampfireSafeRadius { get; }
    public ConfigEntry<bool> SafeZoneRequiresLit { get; }

    // Fog
    public ConfigEntry<float> FogSpeedMultiplier { get; }
    public ConfigEntry<bool> FogIgnoresChasers { get; }

    // Items
    public ConfigEntry<string> ChaserAllowedItems { get; }
    public ConfigEntry<bool> ChaserAutoAllowHealing { get; }
    public ConfigEntry<bool> BanAmulets { get; }
    public ConfigEntry<bool> BanGems { get; }
    public ConfigEntry<bool> BanBlowguns { get; }
    public ConfigEntry<bool> BanRescueClaws { get; }
    public ConfigEntry<bool> BanHiddenItems { get; }
    public ConfigEntry<string> AllowedHiddenItems { get; }
    public ConfigEntry<string> BannedItems { get; }

    // Conversion (statues)
    public ConfigEntry<int> GhostsConvertedPerStatue { get; }
    public ConfigEntry<bool> ReviveDeadChasers { get; }

    // Rewards
    public ConfigEntry<int> RewardItemCount { get; }

    // Network
    public ConfigEntry<bool> KickPlayersWithoutMod { get; }

    // UI (local)
    public ConfigEntry<bool> ShowChaserList { get; }
    public ConfigEntry<bool> FreezeScreenFrost { get; }
    public ConfigEntry<float> CountdownOpacity { get; }
    public ConfigEntry<bool> CaptureSound { get; }

    // Debug (local)
    public ConfigEntry<bool> DebugKeys { get; }
    public ConfigEntry<Key> KeyToggleOwnRole { get; }
    public ConfigEntry<Key> KeyStartRound { get; }
    public ConfigEntry<Key> KeySelfFreeze { get; }
    public ConfigEntry<Key> KeyFreezeLookTarget { get; }
    public ConfigEntry<bool> LogLookChecks { get; }

    public ModConfig(ConfigFile config)
    {
        _file = config;

        const string mod = "1. Modules";
        EnableFreeze = Local(mod, "EnableFreeze", true, "Runners freeze chasers by looking at them.");
        EnableTag = Local(mod, "EnableTag", true, "Chasers capture runners by colliding with them.");
        EnableSafeZones = Local(mod, "EnableSafeZones", true, "Campfires are safe zones.");
        EnableFog = Local(mod, "EnableFog", true, "Faster fog that only hurts runners.");
        EnableItemRules = Local(mod, "EnableItemRules", true, "Chaser item restrictions and banned items.");
        EnableConversion = Local(mod, "EnableConversion", true, "Using a scout statue turns one random ghost into a chaser.");
        EnableRewards = Local(mod, "EnableRewards", true, "First runner into each campfire safe zone gets ancient-luggage loot.");

        const string round = "2. Round";
        ChaserCount = Synced(round, "ChaserCount", 1, "Chasers picked at round start (always leaves at least one runner).");
        RoleRevealSeconds = Synced(round, "RoleRevealSeconds", 4f, "Seconds the role (CHASER / RUNNER) is shown at the start of each leg. Chasers stay frozen and blind during it.");
        HeadStartSeconds = Synced(round, "HeadStartSeconds", 20f, "Runner head start after the role reveal. Chasers stay frozen and blind until it ends.");
        AutoStartRound = Synced(round, "AutoStartRound", true, "Start a round automatically once everyone has woken up on the beach (host).");
        ChaserSpeedMultiplier = Synced(round, "ChaserSpeedMultiplier", 1.15f, "Chaser movement speed multiplier (they can't use most items).");

        const string freeze = "3. Freeze";
        FreezeRange = Synced(freeze, "FreezeRange", 26f, "A runner can only freeze a chaser that is within this distance (m).");
        FreezeConeDegrees = Synced(freeze, "FreezeConeDegrees", 15f, "Half-angle of the look cone (degrees).");
        FreezeDuration = Synced(freeze, "FreezeDuration", 5f, "Seconds a chaser stays frozen. Does not stack or extend.");
        FreezeCooldownSeconds = Synced(freeze, "FreezeCooldownSeconds", 8f, "Seconds after a freeze ends before that chaser can be frozen again (immunity).");
        FreezeHoldGrip = Synced(freeze, "FreezeHoldGrip", true, "Frozen while climbing: keep holding the wall.");
        FreezeLockStamina = Synced(freeze, "FreezeLockStamina", true, "Keep stamina constant while frozen.");
        FreezeZeroVelocity = Synced(freeze, "FreezeZeroVelocity", false, "Zero ragdoll velocities while frozen (anti-slide).");
        FreezeBlockLook = Synced(freeze, "FreezeBlockLook", false, "Also block camera look while frozen.");
        FreezeSuspendInAir = Synced(freeze, "FreezeSuspendInAir", true, "Frozen while airborne: hang in the air until the freeze ends.");
        FreezeSuspendStiffness = Synced(freeze, "FreezeSuspendStiffness", 10f, "How strongly a suspended player is held at the freeze point (1/s).");
        FreezePulseFastInterval = Synced(freeze, "FreezePulseFastInterval", 0.15f, "Seconds between cold pulses at the start of a freeze.");
        FreezePulseSlowInterval = Synced(freeze, "FreezePulseSlowInterval", 0.9f, "Seconds between cold pulses as the freeze runs out.");

        const string tag = "4. Tag";
        TagMaxDistance = Synced(tag, "TagMaxDistance", 4f, "Host rejects a capture if the two players are further apart than this (lag tolerance).");
        TagPassedOutRunners = Synced(tag, "TagPassedOutRunners", true, "Chasers can capture runners who are passed out.");

        const string safe = "5. SafeZones";
        CampfireSafeRadius = Synced(safe, "CampfireSafeRadius", 50f, "Radius (m) around a campfire that is a safe zone. A leg ends when every living runner is inside the next campfire's safe zone.");
        SafeZoneRequiresLit = Synced(safe, "SafeZoneRequiresLit", false, "Only lit campfires are safe zones.");

        const string fog = "6. Fog";
        FogSpeedMultiplier = Synced(fog, "FogSpeedMultiplier", 1.5f, "Fog speed multiplier during a round.");
        FogIgnoresChasers = Synced(fog, "FogIgnoresChasers", true, "Chasers don't count when deciding whether the fog starts moving. (Campfire lighting/resting always ignores chasers during a round.)");

        const string items = "7. Items";
        ChaserAllowedItems = Synced(items, "ChaserAllowedItems", "", "Extra item names chasers may pick up/use (comma separated, prefab or display name).");
        ChaserAutoAllowHealing = Synced(items, "ChaserAutoAllowHealing", true, "Chasers may pick up and use items that heal injury.");
        BanAmulets = Synced(items, "BanAmulets", true, "Remove amulets from the game.");
        BanGems = Synced(items, "BanGems", true, "Remove gems (scout gems, strange gem, healing gem) from the game.");
        BanBlowguns = Synced(items, "BanBlowguns", true, "Remove blowguns from the game.");
        BanRescueClaws = Synced(items, "BanRescueClaws", true, "Remove rescue claws from the game.");
        BanHiddenItems = Synced(items, "BanHiddenItems", true, "Remove hidden items that never spawn in normal PEAK (not in any spawn pool and not produced by another item). Check the log for what was detected.");
        AllowedHiddenItems = Synced(items, "AllowedHiddenItems", "", "Hidden items to allow anyway (comma separated, prefab or display name).");
        BannedItems = Synced(items, "BannedItems", "", "Extra banned item names (comma separated, prefab or display name).");

        const string conv = "8. Conversion";
        GhostsConvertedPerStatue = Synced(conv, "GhostsConvertedPerStatue", 1, "How many of the ghosts (dead runners) revived by a scout statue become chasers; the rest come back as runners.");
        ReviveDeadChasers = Synced(conv, "ReviveDeadChasers", true, "Chasers who died are also revived when a scout statue is used.");

        const string reward = "9. Rewards";
        RewardItemCount = Synced(reward, "RewardItemCount", 3, "Items rolled from the ancient-luggage pool for the first runner into each campfire safe zone.");

        const string net = "10. Network";
        KickPlayersWithoutMod = Local(net, "KickPlayersWithoutMod", false, "Host kicks players who don't have the same OnTheLookout version.");

        const string ui = "11. UI";
        ShowChaserList = Local(ui, "ShowChaserList", true, "Show the chaser list under the ascent label (top right).");
        FreezeScreenFrost = Local(ui, "FreezeScreenFrost", true, "Play PEAK's cold screen effect on your own screen while you are frozen.");
        CountdownOpacity = Local(ui, "CountdownOpacity", 0.35f, "Opacity of the big head-start countdown on runners' screens (0-1).");
        CaptureSound = Local(ui, "CaptureSound", true, "Play an explosion sound when a runner is captured.");

        const string debug = "12. Debug";
        DebugKeys = Local(debug, "DebugKeys", true, "Enable host debug keys.");
        KeyToggleOwnRole = Local(debug, "KeyToggleOwnRole", Key.F6, "HOST: switch your own role between runner and chaser.");
        KeyStartRound = Local(debug, "KeyStartRound", Key.F7, "HOST: start / restart a round (re-rolls roles).");
        KeySelfFreeze = Local(debug, "KeySelfFreeze", Key.F8, "HOST: freeze yourself.");
        KeyFreezeLookTarget = Local(debug, "KeyFreezeLookTarget", Key.F9, "HOST: freeze the player you are looking at.");
        LogLookChecks = Local(debug, "LogLookChecks", false, "Log distance/angle/occlusion for every successful look check.");

        config.SettingChanged += (_, _) => ConfigSync.Publish();
    }

    private ConfigEntry<T> Local<T>(string section, string key, T value, string description) =>
        _file.Bind(section, key, value, description);

    private ConfigEntry<T> Synced<T>(string section, string key, T value, string description)
    {
        ConfigEntry<T> e = _file.Bind(section, key, value, description + " (host-synced)");
        ConfigSync.Register(e);
        return e;
    }
}

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

    // Round & roles (rules 1, 2)
    public ConfigEntry<int> ChaserCount { get; }
    public ConfigEntry<float> HeadStartSeconds { get; }
    public ConfigEntry<bool> AutoStartRound { get; }

    // Freeze (rules 3-5)
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

    // Tag / capture
    public ConfigEntry<float> TagMaxDistance { get; }
    public ConfigEntry<bool> TagPassedOutRunners { get; }

    // Safe zones (rule 8)
    public ConfigEntry<float> CampfireSafeRadius { get; }
    public ConfigEntry<bool> SafeZoneRequiresLit { get; }

    // Fog (rule 7)
    public ConfigEntry<float> FogSpeedMultiplier { get; }
    public ConfigEntry<bool> FogIgnoresChasers { get; }

    // Items (rules 6, 11)
    public ConfigEntry<string> ChaserAllowedItems { get; }
    public ConfigEntry<bool> ChaserAutoAllowHealing { get; }
    public ConfigEntry<bool> BanAmulets { get; }
    public ConfigEntry<bool> BanGems { get; }
    public ConfigEntry<string> BannedItems { get; }

    // Conversion (rule 9)
    public ConfigEntry<bool> ConvertOnBiomeChange { get; }
    public ConfigEntry<int> ConversionsPerBiome { get; }
    public ConfigEntry<float> ConversionDelaySeconds { get; }
    public ConfigEntry<bool> ReviveDeadChasers { get; }

    // Rewards (rule 10)
    public ConfigEntry<int> RewardItemCount { get; }

    // Network
    public ConfigEntry<bool> KickPlayersWithoutMod { get; }

    // UI (local)
    public ConfigEntry<bool> ShowChaserList { get; }
    public ConfigEntry<bool> FreezeScreenFrost { get; }

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
        EnableFreeze = Local(mod, "EnableFreeze", true, "Rules 2-5: head start and runner look-freeze.");
        EnableTag = Local(mod, "EnableTag", true, "Chasers capture runners by colliding with them.");
        EnableSafeZones = Local(mod, "EnableSafeZones", true, "Rule 8: campfires are safe zones; campfire checks ignore chasers.");
        EnableFog = Local(mod, "EnableFog", true, "Rule 7: faster fog that only hurts runners.");
        EnableItemRules = Local(mod, "EnableItemRules", true, "Rules 6 and 11: chaser item restrictions and banned items.");
        EnableConversion = Local(mod, "EnableConversion", true, "Rule 9: dead runners convert to chasers at biome changes.");
        EnableRewards = Local(mod, "EnableRewards", true, "Rule 10: first runner to each campfire gets ancient-luggage loot.");

        const string round = "2. Round";
        ChaserCount = Synced(round, "ChaserCount", 1, "Chasers picked at round start (always leaves at least one runner).");
        HeadStartSeconds = Synced(round, "HeadStartSeconds", 20f, "Seconds chasers are held in place at round start.");
        AutoStartRound = Synced(round, "AutoStartRound", true, "Start a round automatically when the run starts (host).");

        const string freeze = "3. Freeze";
        FreezeRange = Synced(freeze, "FreezeRange", 25f, "Max distance (m) at which a runner looking at a chaser freezes them.");
        FreezeConeDegrees = Synced(freeze, "FreezeConeDegrees", 15f, "Half-angle of the look cone (degrees).");
        FreezeDuration = Synced(freeze, "FreezeDuration", 5f, "Seconds a chaser stays frozen. Does not stack or extend.");
        FreezeCooldownSeconds = Synced(freeze, "FreezeCooldownSeconds", 8f, "Seconds after a freeze ends before that chaser can be frozen again.");
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
        CampfireSafeRadius = Synced(safe, "CampfireSafeRadius", 12f, "Radius (m) around a campfire where runners can't be captured.");
        SafeZoneRequiresLit = Synced(safe, "SafeZoneRequiresLit", false, "Only lit campfires are safe zones.");

        const string fog = "6. Fog";
        FogSpeedMultiplier = Synced(fog, "FogSpeedMultiplier", 1.5f, "Fog speed multiplier during a round.");
        FogIgnoresChasers = Synced(fog, "FogIgnoresChasers", true, "Chasers don't count when deciding whether the fog starts moving. (Campfire lighting/resting always ignores chasers during a round.)");

        const string items = "7. Items";
        ChaserAllowedItems = Synced(items, "ChaserAllowedItems", "", "Extra item names chasers may pick up/use (comma separated, prefab or display name).");
        ChaserAutoAllowHealing = Synced(items, "ChaserAutoAllowHealing", true, "Chasers may use any item that heals injury.");
        BanAmulets = Synced(items, "BanAmulets", true, "Nobody can pick up amulets.");
        BanGems = Synced(items, "BanGems", true, "Nobody can pick up gems.");
        BannedItems = Synced(items, "BannedItems", "", "Extra banned item names (comma separated, prefab or display name).");

        const string conv = "8. Conversion";
        ConvertOnBiomeChange = Synced(conv, "ConvertOnBiomeChange", true, "When a campfire is lit, dead runners may become chasers.");
        ConversionsPerBiome = Synced(conv, "ConversionsPerBiome", 1, "How many random dead runners convert per campfire.");
        ConversionDelaySeconds = Synced(conv, "ConversionDelaySeconds", 6f, "Delay after lighting before the conversion happens.");
        ReviveDeadChasers = Synced(conv, "ReviveDeadChasers", true, "Chasers who died are also revived at the campfire.");

        const string reward = "9. Rewards";
        RewardItemCount = Synced(reward, "RewardItemCount", 3, "Items rolled from the ancient-luggage pool for the first runner at each campfire.");

        const string net = "10. Network";
        KickPlayersWithoutMod = Local(net, "KickPlayersWithoutMod", false, "Host kicks players who don't have the same OnTheLookout version.");

        const string ui = "11. UI";
        ShowChaserList = Local(ui, "ShowChaserList", true, "Show the chaser list under the ascent label (top right).");
        FreezeScreenFrost = Local(ui, "FreezeScreenFrost", true, "Play PEAK's cold screen effect on your own screen while you are frozen.");

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

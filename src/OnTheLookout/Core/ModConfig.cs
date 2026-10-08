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
    public ConfigEntry<string> ChasersByPlayerCount { get; }
    public ConfigEntry<float> RoleRevealSeconds { get; }
    public ConfigEntry<float> HeadStartSeconds { get; }
    public ConfigEntry<bool> AutoStartRound { get; }
    public ConfigEntry<float> BiomeTitleSeconds { get; }
    public ConfigEntry<float> SpawnInteractLockSeconds { get; }
    public ConfigEntry<float> ChaserStatusMultiplier { get; }
    public ConfigEntry<bool> ChaserNoFallDamage { get; }
    public ConfigEntry<bool> ZombiesIgnoreChasers { get; }
    public ConfigEntry<bool> TeleportChasersOnLegComplete { get; }
    public ConfigEntry<bool> CaptureMoraleBoost { get; }
    public ConfigEntry<float> EnergyDrinkDrowsyMultiplier { get; }
    public ConfigEntry<float> BlowdartDrowsy { get; }
    public ConfigEntry<string> RewardItems { get; }
    public ConfigEntry<float> NoTitleFallbackSeconds { get; }
    public ConfigEntry<float> ChaserSpeedMultiplier { get; }
    public ConfigEntry<float> CaptureBoostPercent { get; }
    public ConfigEntry<float> CaptureBoostStackPercent { get; }
    public ConfigEntry<float> CaptureBoostSeconds { get; }
    public ConfigEntry<float> RunnerStaminaRegenMultiplier { get; }
    public ConfigEntry<string> RunnerLegItems { get; }
    public ConfigEntry<bool> RunnerBackpacks { get; }
    public ConfigEntry<string> CampfireFoodItems { get; }
    public ConfigEntry<bool> ClearStatusesAtCampfire { get; }
    public ConfigEntry<bool> NoReviveCurse { get; }
    public ConfigEntry<bool> DisableScoutmaster { get; }

    // Blowgun (chasers)
    public ConfigEntry<bool> ChaserBlowgun { get; }
    public ConfigEntry<float> BlowgunCooldownSeconds { get; }
    public ConfigEntry<float> TrackingSmokeSeconds { get; }
    public ConfigEntry<float> FireworkIntervalSeconds { get; }
    public ConfigEntry<float> FireworkHeight { get; }
    public ConfigEntry<bool> ScoutmasterSounds { get; }
    public ConfigEntry<float> ScoutmasterSoundMinInterval { get; }
    public ConfigEntry<float> ScoutmasterSoundMaxInterval { get; }

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
    public ConfigEntry<bool> MilkProtectsFromCapture { get; }

    // Safe zones
    public ConfigEntry<float> CampfireSafeRadius { get; }
    public ConfigEntry<bool> SafeZoneRequiresLit { get; }

    // Fog
    public ConfigEntry<float> FogSpeedMultiplier { get; }
    public ConfigEntry<bool> FogIgnoresChasers { get; }

    // Items
    public ConfigEntry<string> ChaserAllowedItems { get; }
    public ConfigEntry<bool> ChaserAutoAllowHealing { get; }
    public ConfigEntry<bool> ChaserAutoAllowFood { get; }
    public ConfigEntry<bool> ClownLuggageChasersOnly { get; }
    public ConfigEntry<bool> ChasersOnlyOpenClownLuggage { get; }
    public ConfigEntry<bool> BanAmulets { get; }
    public ConfigEntry<bool> BanGems { get; }
    public ConfigEntry<bool> BanBlowguns { get; }
    public ConfigEntry<bool> BanRescueClaws { get; }
    public ConfigEntry<bool> AllowJetpacks { get; }
    public ConfigEntry<bool> AllowGliders { get; }
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
    public ConfigEntry<bool> ChasersSeeGhosts { get; }
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

    // Admin (local)
    public ConfigEntry<bool> AdminKeys { get; }
    public ConfigEntry<Key> KeyRestartFromCampfire { get; }

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
        ChasersByPlayerCount = Synced(round, "ChasersByPlayerCount", "1:1, 6:2", "How many chasers to pick for a given lobby size, as \"minPlayers:chasers\" pairs. \"1:1, 6:2\" = 1 chaser, 2 once there are 6+ players. Add e.g. \", 10:3\" for bigger lobbies. Always leaves at least one runner.");
        RoleRevealSeconds = Synced(round, "RoleRevealSeconds", 4f, "Seconds the role (CHASER / RUNNER) is shown at the start of each leg. Chasers stay frozen and blind during it.");
        HeadStartSeconds = Synced(round, "HeadStartSeconds", 20f, "Runner head start after the role reveal. Chasers stay frozen and blind until it ends.");
        AutoStartRound = Synced(round, "AutoStartRound", true, "Start a round automatically once everyone has woken up on the beach (host).");
        BiomeTitleSeconds = Synced(round, "BiomeTitleSeconds", 7.5f, "After a campfire is lit, the next leg (role reveal, blind chasers, head start) starts this long after the first player sees the new biome title, so it plays after the title.");
        NoTitleFallbackSeconds = Synced(round, "NoTitleFallbackSeconds", 12f, "If nobody sees a biome title after a campfire is lit, the next leg starts this long after lighting anyway.");
        SpawnInteractLockSeconds = Synced(round, "SpawnInteractLockSeconds", 7f, "At the start of a run on the shore nothing can be interacted with while everyone wakes up and for this many seconds after the round starts.");
        ChaserStatusMultiplier = Synced(round, "ChaserStatusMultiplier", 0.333f, "Chasers take this fraction of every negative status (injury, cold, poison, drowsy, ...) they would get in vanilla at the current ascent. Hunger is not reduced.");
        ChaserNoFallDamage = Synced(round, "ChaserNoFallDamage", true, "Chasers never take fall damage.");
        ZombiesIgnoreChasers = Synced(round, "ZombiesIgnoreChasers", true, "Mushroom zombies don't target or bite chasers.");
        TeleportChasersOnLegComplete = Synced(round, "TeleportChasersOnLegComplete", true, "When every living runner reaches the next campfire's safe zone, living chasers are teleported to that campfire too.");
        CaptureMoraleBoost = Synced(round, "CaptureMoraleBoost", true, "A chaser who captures a runner also gets a full morale boost (full extra-stamina bar).");
        EnergyDrinkDrowsyMultiplier = Synced(round, "EnergyDrinkDrowsyMultiplier", 1.5f, "Multiplier for the drowsiness an energy drink causes when it wears off.");
        ChaserSpeedMultiplier = Synced(round, "ChaserSpeedMultiplier", 1.15f, "Chaser movement speed multiplier (they can't use most items).");
        CaptureBoostPercent = Synced(round, "CaptureBoostPercent", 1f, "Temporary chaser speed boost (%) after a capture.");
        CaptureBoostStackPercent = Synced(round, "CaptureBoostStackPercent", 0.5f, "Extra boost (%) for each further capture while the boost is still active.");
        CaptureBoostSeconds = Synced(round, "CaptureBoostSeconds", 5f, "How long the capture boost lasts (refreshed by each capture).");
        RunnerStaminaRegenMultiplier = Synced(round, "RunnerStaminaRegenMultiplier", 1.12f, "Runner stamina regeneration multiplier (1.12 = 12% faster).");
        RunnerLegItems = Synced(round, "RunnerLegItems", "Snowball, Brown Berrynana, Fortified Milk", "At the start of each leg every runner gets ONE random item from this list (prefab or display names, comma separated). Empty = off.");
        RunnerBackpacks = Synced(round, "RunnerBackpacks", true, "After roles are assigned at the start of a round, every runner without a backpack gets one.");
        CampfireFoodItems = Synced(round, "CampfireFoodItems", "Marshmallow, Glizzy", "When the chase of a leg ends at a campfire, the host makes sure there is one of these per living player near the fire (random pick each; spawns only what is missing). Glizzy = the hot dog. Empty = off.");
        ClearStatusesAtCampfire = Synced(round, "ClearStatusesAtCampfire", true, "When a campfire is lit, every player near it is cleared of negative statuses (incl. curse).");
        NoReviveCurse = Synced(round, "NoReviveCurse", true, "Revived players don't get the revival curse / hunger.");
        DisableScoutmaster = Synced(round, "DisableScoutmaster", true, "Don't spawn the Scoutmaster (he hunts the runner furthest from the group).");

        const string blowgun = "2b. Blowgun";
        ChaserBlowgun = Synced(blowgun, "ChaserBlowgun", true, "Chasers get a blowgun with unlimited uses. A dart doesn't put runners to sleep; it marks them with flare smoke instead.");
        BlowgunCooldownSeconds = Synced(blowgun, "BlowgunCooldownSeconds", 30f, "Seconds between blowgun shots.");
        TrackingSmokeSeconds = Synced(blowgun, "TrackingSmokeSeconds", 5f, "How long the tracking smoke follows a darted runner.");
        BlowdartDrowsy = Synced(blowgun, "BlowdartDrowsy", 0.1f, "Drowsiness (sleep) a dart adds to the runner it hits (0.1 = 10%).");

        const string effects = "2c. ChaseEffects";
        FireworkIntervalSeconds = Synced(effects, "FireworkIntervalSeconds", 30f, "Every this many seconds of an active chase a firework goes off above each chaser. 0 = off.");
        FireworkHeight = Synced(effects, "FireworkHeight", 8f, "How high above the chaser the firework bursts (m).");
        ScoutmasterSounds = Synced(effects, "ScoutmasterSounds", true, "While a chaser is within freeze range of a runner, Scoutmaster sounds play at the chaser.");
        ScoutmasterSoundMinInterval = Synced(effects, "ScoutmasterSoundMinInterval", 3f, "Shortest gap between Scoutmaster sounds from one chaser (s).");
        ScoutmasterSoundMaxInterval = Synced(effects, "ScoutmasterSoundMaxInterval", 6f, "Longest gap between Scoutmaster sounds from one chaser (s).");

        const string freeze = "3. Freeze";
        FreezeRange = Synced(freeze, "FreezeRange", 26f, "A runner can only freeze a chaser that is within this distance (m).");
        FreezeConeDegrees = Synced(freeze, "FreezeConeDegrees", 15f, "Half-angle of the look cone (degrees).");
        FreezeDuration = Synced(freeze, "FreezeDuration", 6.5f, "Seconds a chaser stays frozen. Does not stack or extend.");
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
        MilkProtectsFromCapture = Synced(tag, "MilkProtectsFromCapture", true, "Runners under the effect of fortified milk can't be captured.");

        const string safe = "5. SafeZones";
        CampfireSafeRadius = Synced(safe, "CampfireSafeRadius", 50f, "Radius (m) around a campfire that is a safe zone. A leg ends when every living runner is inside the next campfire's safe zone.");
        SafeZoneRequiresLit = Synced(safe, "SafeZoneRequiresLit", false, "Only lit campfires are safe zones.");

        const string fog = "6. Fog";
        FogSpeedMultiplier = Synced(fog, "FogSpeedMultiplier", 1.5f, "Fog speed multiplier during a round.");
        FogIgnoresChasers = Synced(fog, "FogIgnoresChasers", true, "Chasers don't count when deciding whether the fog starts moving. (Campfire lighting/resting always ignores chasers during a round.)");

        const string items = "7. Items";
        ChaserAllowedItems = Synced(items, "ChaserAllowedItems", "", "Extra item names chasers may pick up/use (comma separated, prefab or display name).");
        ChaserAutoAllowHealing = Synced(items, "ChaserAutoAllowHealing", true, "Chasers may pick up and use items that heal injury.");
        ChaserAutoAllowFood = Synced(items, "ChaserAutoAllowFood", true, "Chasers may pick up and eat food.");
        ClownLuggageChasersOnly = Synced(items, "ClownLuggageChasersOnly", true, "During a round only chasers can open clown luggage, and it contains food and healing items.");
        ChasersOnlyOpenClownLuggage = Synced(items, "ChasersOnlyOpenClownLuggage", true, "During a round chasers can open clown luggage only (scout statues still work).");
        BanAmulets = Synced(items, "BanAmulets", true, "Remove amulets from the game.");
        BanGems = Synced(items, "BanGems", true, "Remove gems (scout gems, strange gem, healing gem) from the game.");
        BanBlowguns = Synced(items, "BanBlowguns", true, "Remove blowguns from the world; only chasers can hold one (see ChaserBlowgun).");
        BanRescueClaws = Synced(items, "BanRescueClaws", true, "Remove rescue claws from the game.");
        AllowJetpacks = Synced(items, "AllowJetpacks", false, "Allow jetpacks and rocket packs. Off = removed from the game (not spawned, can't be picked up).");
        AllowGliders = Synced(items, "AllowGliders", false, "Allow gliders. Off = removed from the game (not spawned, can't be picked up).");
        BanHiddenItems = Synced(items, "BanHiddenItems", true, "Remove hidden items that never spawn in normal PEAK (not in any spawn pool and not produced by another item). Check the log for what was detected.");
        AllowedHiddenItems = Synced(items, "AllowedHiddenItems", "", "Hidden items to allow anyway (comma separated, prefab or display name).");
        BannedItems = Synced(items, "BannedItems", "", "Extra banned item names (comma separated, prefab or display name).");

        const string conv = "8. Conversion";
        GhostsConvertedPerStatue = Synced(conv, "GhostsConvertedPerStatue", 1, "How many of the ghosts (dead runners) revived by a scout statue become chasers; the rest come back as runners.");
        ReviveDeadChasers = Synced(conv, "ReviveDeadChasers", true, "Chasers who died are also revived when a scout statue is used.");

        const string reward = "9. Rewards";
        RewardItems = Synced(reward, "RewardItems", "Energy Drink", "Item(s) the first runner into each campfire safe zone can get (prefab or display names, comma separated; one random pick per RewardItemCount).");
        RewardItemCount = Synced(reward, "RewardItemCount", 1, "How many reward items the first runner into each campfire safe zone gets.");

        const string net = "10. Network";
        KickPlayersWithoutMod = Local(net, "KickPlayersWithoutMod", false, "Host kicks players who don't have the same OnTheLookout version.");

        const string ui = "11. UI";
        ChasersSeeGhosts = Synced(ui, "ChasersSeeGhosts", false, "Whether chasers can see ghosts (spectators). Off = ghosts are invisible to living chasers, since a ghost floats around the runner it spectates.");
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

        const string admin = "13. Admin";
        AdminKeys = Local(admin, "AdminKeys", true, "Enable host admin keys (work even with DebugKeys off).");
        KeyRestartFromCampfire = Local(admin, "KeyRestartFromCampfire", Key.F10, "HOST: quick restart - everyone back to the last lit campfire (or the start), dead players revived, statuses cleared, fresh leg with role reveal + head start. Roles are kept.");

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

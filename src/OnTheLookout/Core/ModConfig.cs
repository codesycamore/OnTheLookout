using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace OnTheLookout.Core;

/// <summary>
/// All tunables. Gameplay settings are host-synced (read them with <c>.Synced()</c>).
/// Only the settings in <see cref="Configurable"/> are written to the config file and can be changed by players;
/// every other setting is bound to an in-memory config that is never loaded or saved, so it always has this
/// version's value and every player and host runs the same rules. The config file is also wiped and rewritten
/// with the current defaults whenever a different mod version loads it (<see cref="VersionKey"/>), so an
/// update always starts from the latest defaults.
/// </summary>
internal sealed class ModConfig
{
    /// <summary>The only settings players can change (in the config file / mod manager's config editor).</summary>
    private static readonly HashSet<string> Configurable = new()
    {
        "HeadStartSeconds",
        "RunnersPerChaser",
        "ChaserCount",
        "ZombieStartDelaySeconds",
        "ZombieWaveDelaySeconds",
        "ZombieLifetimeSeconds",
        "HeadStartBoostSeconds",
        "RandomMap",
    };

    private const string VersionSection = "0. Version";
    private const string VersionKey = "ConfigWrittenByVersion";

    private readonly ConfigFile _file;

    /// <summary>Fixed settings: in memory only (never read from or written to disk), always the built-in values.</summary>
    private readonly ConfigFile _fixed;

    // Modules
    public ConfigEntry<bool> EnableFreeze { get; }
    public ConfigEntry<bool> EnableTag { get; }
    public ConfigEntry<bool> EnableSafeZones { get; }
    public ConfigEntry<bool> EnableFog { get; }
    public ConfigEntry<bool> EnableItemRules { get; }
    public ConfigEntry<bool> EnableRewards { get; }

    // Round & roles
    public ConfigEntry<float> RunnersPerChaser { get; }
    public ConfigEntry<float> RoleRevealSeconds { get; }
    public ConfigEntry<float> HeadStartSeconds { get; }
    public ConfigEntry<bool> AutoStartRound { get; }
    public ConfigEntry<bool> ChaserPreferenceEnabled { get; }
    public ConfigEntry<float> RoleWindowSeconds { get; }
    public ConfigEntry<float> ChaserStatusMultiplier { get; }
    public ConfigEntry<float> ChaserStatusMultiplierCaldera { get; }
    public ConfigEntry<float> ChaserFallDamageMultiplier { get; }
    public ConfigEntry<bool> ZombiesIgnoreChasers { get; }
    public ConfigEntry<bool> ZombiesWhenChasersDead { get; }
    public ConfigEntry<float> ZombieLifetimeSeconds { get; }
    public ConfigEntry<float> ZombieSpawnDistance { get; }
    public ConfigEntry<string> ZombiesByPlayerCount { get; }
    public ConfigEntry<float> ZombieWaveDelaySeconds { get; }
    public ConfigEntry<float> ZombieStartDelaySeconds { get; }
    public ConfigEntry<float> MandrakeStartSeconds { get; }
    public ConfigEntry<float> MandrakeIntervalSeconds { get; }
    public ConfigEntry<float> MandrakeFirstScreamSeconds { get; }
    public ConfigEntry<float> MandrakeLifetimeSeconds { get; }
    public ConfigEntry<int> MandrakeLastSegment { get; }
    public ConfigEntry<bool> MandrakeNoRunnerSleep { get; }
    public ConfigEntry<float> MandrakeChaserSleepMultiplier { get; }
    public ConfigEntry<float> HeadStartBoostSeconds { get; }
    public ConfigEntry<bool> HeadStartInvincibility { get; }
    public ConfigEntry<bool> DisableScoutStatues { get; }
    public ConfigEntry<bool> PeakChasersDie { get; }
    public ConfigEntry<float> GameOverSeconds { get; }
    public ConfigEntry<float> GameOverAirportSeconds { get; }
    public ConfigEntry<float> StoneChaserDamage { get; }
    public ConfigEntry<bool> PeakSafeZone { get; }
    public ConfigEntry<float> ChaserCount { get; }
    public ConfigEntry<bool> ResetItemsEachLeg { get; }
    public ConfigEntry<bool> ClearItemsAtLegEnd { get; }
    public ConfigEntry<bool> MandrakesOutOfSafeZones { get; }
    public ConfigEntry<bool> NoPeakFlares { get; }
    public ConfigEntry<bool> RunnersFaceNextCampfire { get; }

    public ConfigEntry<float> EnergyDrinkDrowsyMultiplier { get; }
    public ConfigEntry<float> BlowdartDrowsy { get; }
    public ConfigEntry<string> RewardItems { get; }
    public ConfigEntry<float> ChaserSpeedMultiplier { get; }
    public ConfigEntry<float> ChaserClimbSpeedMultiplier { get; }
    public ConfigEntry<bool> CaptureMoraleBoost { get; }
    public ConfigEntry<float> GemCaptureBonus { get; }
    public ConfigEntry<float> GemCaptureExtraSeconds { get; }
    public ConfigEntry<float> RunnerStaminaRegenMultiplier { get; }
    public ConfigEntry<string> RunnerLegItems { get; }
    public ConfigEntry<string> RunnerBiomeItems { get; }
    public ConfigEntry<string> RunnerKilnItem { get; }
    public ConfigEntry<float> FortifiedMilkWeightMultiplier { get; }
    public ConfigEntry<float> FortifiedMilkInvincibilityMultiplier { get; }
    public ConfigEntry<float> HungerRateMultiplier { get; }
    public ConfigEntry<bool> RunnerBackpacks { get; }
    public ConfigEntry<string> CampfireFoodItems { get; }
    public ConfigEntry<bool> ClearStatusesAtCampfire { get; }
    public ConfigEntry<bool> NoReviveCurse { get; }
    public ConfigEntry<bool> DisableScoutmaster { get; }

    // Blowgun (chasers)
    public ConfigEntry<bool> ChaserBlowgun { get; }
    public ConfigEntry<float> BlowgunCooldownSeconds { get; }
    public ConfigEntry<float> BlowgunUseTimeMultiplier { get; }
    public ConfigEntry<float> GemRevealSeconds { get; }
    public ConfigEntry<float> DashRevealSeconds { get; }
    public ConfigEntry<bool> ChaserGem { get; }
    public ConfigEntry<string> ChaserGemItem { get; }
    public ConfigEntry<float> GemBoostSeconds { get; }
    public ConfigEntry<float> GemDelaySeconds { get; }
    public ConfigEntry<bool> GemInfiniteStamina { get; }
    public ConfigEntry<float> GemCooldownSeconds { get; }
    public ConfigEntry<float> GemPetrify { get; }
    public ConfigEntry<float> GemXRaySeconds { get; }
    public ConfigEntry<float> GemXRayRange { get; }
    public ConfigEntry<float> SnowballKnockbackBonus { get; }
    public ConfigEntry<bool> RunnerGem { get; }
    public ConfigEntry<string> RunnerGemItem { get; }
    public ConfigEntry<float> DashDistance { get; }
    public ConfigEntry<float> DashAirFloatSeconds { get; }
    public ConfigEntry<float> DashFallProtectDelay { get; }
    public ConfigEntry<float> DashFallProtectSeconds { get; }
    public ConfigEntry<float> DashCooldownSeconds { get; }
    public ConfigEntry<float> DashPetrify { get; }
    public ConfigEntry<float> DashHeal { get; }
    public ConfigEntry<float> BlowgunPetrify { get; }
    public ConfigEntry<bool> ColdAscentLantern { get; }
    public ConfigEntry<float> FaerieLanternWeightMultiplier { get; }
    public ConfigEntry<int> ColdLanternLastSegment { get; }
    public ConfigEntry<bool> ColdAscentChaserLantern { get; }
    public ConfigEntry<float> LanternWeightMultiplier { get; }
    public ConfigEntry<float> GemHeal { get; }
    public ConfigEntry<bool> ClearPetrifyEachLeg { get; }
    public ConfigEntry<float> DashLiftFactor { get; }
    public ConfigEntry<float> DashFallMomentumCancel { get; }
    public ConfigEntry<float> CapturePetrifyRelief { get; }
    public ConfigEntry<float> SnowballBlindSeconds { get; }
    public ConfigEntry<bool> ScoutmasterChaseMusic { get; }

    // Freeze
    public ConfigEntry<float> FreezeRange { get; }
    public ConfigEntry<float> FreezeConeDegrees { get; }
    public ConfigEntry<float> FreezeDuration { get; }
    public ConfigEntry<float> FreezeCooldownSeconds { get; }
    public ConfigEntry<bool> FreezeHoldGrip { get; }
    public ConfigEntry<bool> FreezeLockStamina { get; }
    public ConfigEntry<bool> FreezeBlindsChasers { get; }
    public ConfigEntry<bool> FreezeZeroVelocity { get; }
    public ConfigEntry<bool> FreezeBlockLook { get; }
    public ConfigEntry<bool> FreezeSuspendInAir { get; }
    public ConfigEntry<float> FreezeSuspendStiffness { get; }
    public ConfigEntry<float> FreezePulseFastInterval { get; }
    public ConfigEntry<float> FreezePulseSlowInterval { get; }

    // Tag
    public ConfigEntry<float> CaptureHoldSeconds { get; }
    public ConfigEntry<float> CaptureRange { get; }
    public ConfigEntry<float> TagMaxDistance { get; }
    public ConfigEntry<bool> TagPassedOutRunners { get; }
    public ConfigEntry<bool> MilkProtectsFromCapture { get; }

    // Safe zones
    public ConfigEntry<float> CampfireSafeRadius { get; }
    public ConfigEntry<bool> SafeZoneRequiresLit { get; }


    // Items
    public ConfigEntry<string> ChaserAllowedItems { get; }
    public ConfigEntry<string> ChaserForbiddenItems { get; }
    public ConfigEntry<float> ShroomberryEffectSeconds { get; }
    public ConfigEntry<bool> ChaserAutoAllowHealing { get; }
    public ConfigEntry<bool> ChaserAutoAllowFood { get; }
    public ConfigEntry<bool> ClownLuggageChasersOnly { get; }
    public ConfigEntry<string> LuggageExtraItems { get; }
    public ConfigEntry<float> LuggageExtraChance { get; }
    public ConfigEntry<string> LuggageRareExtraItems { get; }
    public ConfigEntry<float> LuggageRareExtraWeight { get; }
    public ConfigEntry<string> LuggageExcludedItems { get; }
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
    public ConfigEntry<bool> BanGoldenBingBong { get; }

    // Rewards
    public ConfigEntry<int> RewardItemCount { get; }

    // Network
    public ConfigEntry<bool> KickPlayersWithoutMod { get; }

    // UI (local)
    public ConfigEntry<bool> ChasersSeeGhosts { get; }
    public ConfigEntry<bool> ChasersSeeRunnerNames { get; }
    public ConfigEntry<bool> ChaserRedOutline { get; }
    public ConfigEntry<float> ChaserGlowIntensity { get; }
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
    public ConfigEntry<bool> RandomMap { get; }
    public ConfigEntry<Key> KeyRestartFromCampfire { get; }
    public ConfigEntry<Key> KeyHostMenu { get; }
    public ConfigEntry<Key> KeyChaserOdds { get; }
    public ConfigEntry<Key> KeyRoleHint { get; }
    public ConfigEntry<float> RoleHintSeconds { get; }
    public ConfigEntry<bool> RunnersSeeLuggage { get; }

    public ModConfig(ConfigFile config)
    {
        _file = config;
        _fixed = CreateFixedConfig();
        ResetFileOnVersionChange(config);

        const string mod = "1. Modules";
        EnableFreeze = Local(mod, "EnableFreeze", true, "Runners freeze chasers by looking at them.");
        EnableTag = Local(mod, "EnableTag", true, "Chasers capture runners by holding interact on them (like eating a scout).");
        EnableSafeZones = Local(mod, "EnableSafeZones", true, "Campfires are safe zones.");
        EnableFog = Local(mod, "EnableFog", true, "Fog never hurts chasers (fog, lava and gloom otherwise rise as in vanilla).");
        EnableItemRules = Local(mod, "EnableItemRules", true, "Chaser item restrictions and banned items.");
        EnableRewards = Local(mod, "EnableRewards", false, "First runner into each campfire safe zone gets a reward item (see 9. Rewards). Off for now.");

        const string round = "2. Round";
        RunnersPerChaser = Synced(round, "RunnersPerChaser", 4f, "At most one chaser for every this many runners (4 = 1 chaser in 5 players, 2 in 10). Chasers are drawn at random from the players who chose CHASER in the role menu; if nobody did, one random player becomes the chaser. Always leaves at least one runner. Used when ChaserCount is 0 (AUTO).");
        ChaserCount = Synced(round, "ChaserCount", 0f, "Exact number of chasers each leg (the host menu's CHASERS setting). 0 = AUTO (one per RunnersPerChaser runners). Chasers are drawn from the CHASER volunteers first, then from everyone else if there aren't enough. Always leaves at least one runner. A change applies from the next leg (or the next game).");
        RoleRevealSeconds = Synced(round, "RoleRevealSeconds", 4f, "Seconds the role (CHASER / RUNNER) is shown at the start of each leg. Chasers stay frozen and blind during it.");
        HeadStartSeconds = Synced(round, "HeadStartSeconds", 25f, "Runner head start after the role reveal. Chasers stay frozen and blind until it ends.");
        AutoStartRound = Synced(round, "AutoStartRound", true, "Start a round automatically once everyone has woken up on the beach (host).");
        ChaserPreferenceEnabled = Synced(round, "ChaserPreferenceEnabled", true, "Players choose RUNNER (default) or CHASER in the role menu (KeyChaserOdds) in the airport and in the role window after each leg. Chasers are drawn at random from the CHASER volunteers. Choices are private to the host. Off = chasers are drawn from everyone.");
        RoleWindowSeconds = Synced(round, "RoleWindowSeconds", 10f, "After a leg ends, everyone is brought to the campfire and frozen for this many seconds to update their role choice; then the campfire lights itself and the next leg starts with a new role draw.");
        ChaserStatusMultiplier = Synced(round, "ChaserStatusMultiplier", 0.5f, "Chasers take this fraction of every negative status (injury, cold, poison, drowsy, ...) they would get in vanilla at the current ascent. Hunger is not reduced. Not in the Caldera biome or the swamp (gloom) biome that can replace it: see ChaserStatusMultiplierCaldera.");
        ChaserStatusMultiplierCaldera = Synced(round, "ChaserStatusMultiplierCaldera", 0.75f, "The same fraction in the fourth biome: the Caldera, or the swamp (gloom) biome on maps that have it instead (0.75 = 75% of vanilla). Fall damage is unchanged (ChaserFallDamageMultiplier).");
        ChaserFallDamageMultiplier = Synced(round, "ChaserFallDamageMultiplier", 0.333f, "Fraction of vanilla fall damage chasers take (0.333 = 1/3). Still scales with the ascent like vanilla, and a big fall still knocks them down. 0 = no fall damage.");
        ZombiesIgnoreChasers = Synced(round, "ZombiesIgnoreChasers", true, "Mushroom zombies don't target or bite chasers.");
        ZombiesWhenChasersDead = Synced(round, "ZombiesWhenChasersDead", true, "Solo play only (you alone in the lobby, so there is no chaser): mushroom zombies hunt random runners outside the safe zones (ZombiesByPlayerCount per wave), wave after wave, until the runners reach the campfire or a chaser is back.");
        ZombieLifetimeSeconds = Synced(round, "ZombieLifetimeSeconds", 120f, "How long each of those zombies lasts.");
        ZombieSpawnDistance = Synced(round, "ZombieSpawnDistance", 15f, "How far from its runner a zombie appears (m).");
        ZombiesByPlayerCount = Synced(round, "ZombiesByPlayerCount", "1:1, 6:2, 10:3", "Zombies per wave for a given lobby size, as \"minPlayers:zombies\" pairs. Each zombie hunts one random runner outside the safe zones.");
        ZombieWaveDelaySeconds = Synced(round, "ZombieWaveDelaySeconds", 120f, "Cooldown (s) after a wave of zombies is gone (killed or expired) before the host checks again for living chasers and sends another.");
        ZombieStartDelaySeconds = Synced(round, "ZombieStartDelaySeconds", 300f, "Zombies can only start coming this many seconds after the head start ends (each leg).");
        MandrakeStartSeconds = Synced(round, "MandrakeStartSeconds", 60f, "Mandrakes start appearing at runners this many seconds after the head start ends (each leg). 0 or less = off.");
        MandrakeIntervalSeconds = Synced(round, "MandrakeIntervalSeconds", 45f, "After that, a mandrake is dropped at every living runner outside a safe zone this often (s).");
        MandrakeFirstScreamSeconds = Synced(round, "MandrakeFirstScreamSeconds", 0.75f, "A dropped mandrake screams for the first time this many seconds after it appears (PEAK's own wait for a new mandrake is much longer). Later screams keep PEAK's timing.");
        MandrakeLifetimeSeconds = Synced(round, "MandrakeLifetimeSeconds", 10f, "A dropped mandrake is removed this many seconds after it appears (if it is still lying on the ground). 0 = it stays.");
        MandrakeLastSegment = Synced(round, "MandrakeLastSegment", 2, "Mandrakes are only dropped up to this map segment (0 = Shore, 1 = the second biome, 2 = the third biome).");
        MandrakesOutOfSafeZones = Synced(round, "MandrakesOutOfSafeZones", true, "No mandrake is ever inside a safe zone (campfires, the peak): the host removes any lying there, and a player carrying one into a safe zone loses it.");
        MandrakeNoRunnerSleep = Synced(round, "MandrakeNoRunnerSleep", true, "A screaming mandrake makes no runner drowsy (chasers are still affected).");
        MandrakeChaserSleepMultiplier = Synced(round, "MandrakeChaserSleepMultiplier", 0.9f, "Drowsiness a screaming mandrake gives a chaser, relative to vanilla (0.9 = 90%).");
        HeadStartBoostSeconds = Synced(round, "HeadStartBoostSeconds", 10f, "Runners get an energy-drink speed boost (no drowsiness afterwards) for the last this-many seconds of their head start, in every biome. 0 = off.");
        HeadStartInvincibility = Synced(round, "HeadStartInvincibility", true, "Runners are invincible during their whole head start, like fortified milk (gold glow, no negative statuses or fall damage), until the head start runs out.");
        DisableScoutStatues = Synced(round, "DisableScoutStatues", true, "Scout statues do nothing during a round (no revives, no items). Everyone is brought back at the campfire after each leg instead.");
        PeakChasersDie = Synced(round, "PeakChasersDie", true, "When every living runner has reached the peak, the runners win and every living chaser is brought to the peak and petrified (turned to stone).");
        GameOverSeconds = Synced(round, "GameOverSeconds", 4f, "Once a side has won the game (last leg: every runner or every chaser dead, or every runner at the peak), the losing side is petrified and everyone sees a rainbow GAME OVER! for this long (s) ...");
        GameOverAirportSeconds = Synced(round, "GameOverAirportSeconds", 15f, "... then RETURNING BACK TO AIRPORT IN... counting down this many seconds; then everyone goes back to the airport for a fresh start.");

        EnergyDrinkDrowsyMultiplier = Synced(round, "EnergyDrinkDrowsyMultiplier", 1.5f, "Multiplier for the drowsiness an energy drink causes when it wears off.");
        ChaserSpeedMultiplier = Synced(round, "ChaserSpeedMultiplier", 1.15f, "Chaser movement speed multiplier (they can't use most items).");
        ChaserClimbSpeedMultiplier = Synced(round, "ChaserClimbSpeedMultiplier", 1.06f, "Chaser climbing speed on walls, ropes and vines (1.06 = 6% faster).");
        CaptureMoraleBoost = Synced(round, "CaptureMoraleBoost", true, "A chaser who captures a runner gets the morale boost of the Scoutmaster's bugle (the MORALE BOOST text and the bugle's extra stamina), for that chaser only.");
        GemCaptureBonus = Synced(round, "GemCaptureBonus", 0.017f, "Each capture makes that chaser's gem speed boost this much stronger for the rest of the leg (0.017 = +1.7% per capture), on top of GemCaptureExtraSeconds. Reset every leg.");
        GemCaptureExtraSeconds = Synced(round, "GemCaptureExtraSeconds", 0.25f, "Each capture makes that chaser's gem effects last this much longer (s) for the rest of the leg (2.25 -> 2.5 -> 2.75 ...). Reset every leg.");
        RunnerStaminaRegenMultiplier = Synced(round, "RunnerStaminaRegenMultiplier", 1.4f, "Runner stamina regeneration multiplier (1.4 = 40% faster). Only the refill speed: PEAK's wait before regen starts (1 s, 2 s after running dry) is unchanged.");
        RunnerLegItems = Synced(round, "RunnerLegItems", "Snowball, Brown Berrynana, Fortified Milk", "At the start of each leg every runner gets ONE random item from this list (prefab or display names, comma separated). Empty = off.");
        RunnerBiomeItems = Synced(round, "RunnerBiomeItems", "Alpine:Heat Pack, Volcano:Sports Drink, Swamp:Lantern_Faerie, Mesa:Aloe Vera", "Extra item every runner gets at the start of a leg in that biome, as \"Biome:Item\" pairs. Biomes use PEAK's names (Caldera = Volcano; The Gloom is assumed to be Swamp).");
        RunnerKilnItem = Synced(round, "RunnerKilnItem", "Sports Drink", "Biome item every runner gets at the start of the Kiln leg (the last leg on maps with the Caldera; not the Citadel on swamp maps), whatever biome PEAK files the Kiln under. Empty = the RunnerBiomeItems rule.");
        FortifiedMilkWeightMultiplier = Synced(round, "FortifiedMilkWeightMultiplier", 2f, "Fortified milk weighs this many times its vanilla weight.");
        FortifiedMilkInvincibilityMultiplier = Synced(round, "FortifiedMilkInvincibilityMultiplier", 0.35f, "Fortified milk's invincibility lasts this fraction of its vanilla time (0.35 = 65% shorter). Also shortens its capture protection.");
        HungerRateMultiplier = Synced(round, "HungerRateMultiplier", 0.7f, "During a round hunger grows at this fraction of the vanilla rate (still scaled by the ascent like vanilla). Runners in a safe zone get no hungrier at all.");
        ClearItemsAtLegEnd = Synced(round, "ClearItemsAtLegEnd", true, "When a leg ends, every living player's items are removed (hands, hotbar and backpack, with its contents), not dropped, so nothing piles up at the campfire and nothing is carried into the next leg.");
        ResetItemsEachLeg = Synced(round, "ResetItemsEachLeg", true, "At the start of every leg (once the roles are drawn) every item a player still has (e.g. food picked up during the role window) is removed, not dropped; then chasers get the chaser kit and runners their backpack, gem and runner items.");
        RunnersFaceNextCampfire = Synced(round, "RunnersFaceNextCampfire", true, "At the start of every leg (during the role reveal) every runner's view is turned to face the next campfire (or up the mountain if it isn't loaded yet), so they know which way to run. They can look around freely afterwards.");
        NoPeakFlares = Synced(round, "NoPeakFlares", true, "No flare at the peak on any ascent (PEAK only leaves it out from Ascent 4 on); the host also removes any flare lying at the peak during a round.");
        RunnerBackpacks = Synced(round, "RunnerBackpacks", true, "At the start of every leg (after roles are drawn, and after the dead are brought back), every runner without a backpack gets one, worn.");
        ColdAscentLantern = Synced(round, "ColdAscentLantern", true, "On ascents with cold nights (Ascent 5+), every runner gets a faerie lantern at the start of every leg up to ColdLanternLastSegment.");
        ColdLanternLastSegment = Synced(round, "ColdLanternLastSegment", 2, "Cold-ascent lanterns (ColdAscentLantern, ColdAscentChaserLantern) are handed out at the start of every leg up to this map segment (0 = Shore, 1 = the second biome, 2 = the third biome). None in the fourth biome (the Caldera, or the swamp, whose biome item is a faerie lantern) or the last.");
        ColdAscentChaserLantern = Synced(round, "ColdAscentChaserLantern", true, "On ascents with cold nights (Ascent 5+), every chaser gets a regular lantern (not a faerie lantern) at the start of every leg up to ColdLanternLastSegment.");
        LanternWeightMultiplier = Synced(round, "LanternWeightMultiplier", 0.333f, "Regular lanterns weigh this much of their vanilla weight during a round (0.333 = 1/3).");
        FaerieLanternWeightMultiplier = Synced(round, "FaerieLanternWeightMultiplier", 0.5f, "Faerie lanterns weigh this much of their vanilla weight during a round (0.5 = half).");
        CampfireFoodItems = Synced(round, "CampfireFoodItems", "Marshmallow, Glizzy", "When the chase of a leg ends at a campfire, the host makes sure there is one of these per living player near the fire (random pick each; spawns only what is missing). Glizzy = the hot dog. Empty = off.");
        ClearStatusesAtCampfire = Synced(round, "ClearStatusesAtCampfire", true, "When a campfire is lit, every player near it is cleared of negative statuses (incl. curse).");
        NoReviveCurse = Synced(round, "NoReviveCurse", true, "Revived players don't get the revival curse / hunger.");
        DisableScoutmaster = Synced(round, "DisableScoutmaster", true, "Don't spawn the Scoutmaster (he hunts the runner furthest from the group).");

        const string blowgun = "2b. Blowgun";
        ChaserBlowgun = Synced(blowgun, "ChaserBlowgun", true, "Chasers get a blowgun with unlimited uses. A dart doesn't knock runners out; it makes them drowsy (BlowdartDrowsy).");
        BlowgunCooldownSeconds = Synced(blowgun, "BlowgunCooldownSeconds", 72f, "Seconds between blowgun shots.");
        BlowgunUseTimeMultiplier = Synced(blowgun, "BlowgunUseTimeMultiplier", 1.65f, "How long a chaser takes to fire the blowgun, relative to vanilla (1.65 = 1.65x as long).");
        GemRevealSeconds = Synced(blowgun, "GemRevealSeconds", 3f, "When a chaser uses their gem, every living runner gives off flare smoke in their own colour for this long (s). 0 = off.");
        DashRevealSeconds = Synced(blowgun, "DashRevealSeconds", 5f, "When a runner dashes, every living chaser gives off flare smoke in their own colour for this long (s). 0 = off.");
        ChaserGem = Synced(blowgun, "ChaserGem", true, "Chasers also carry a scout gem (Scout's Initiative) that never runs out: using it gives, after a short delay, an energy-drink speed boost and unlimited stamina (no drowsiness), with a cooldown and some petrification as the cost. Its vanilla power is never activated.");
        ChaserGemItem = Synced(blowgun, "ChaserGemItem", "Amulet_SuperJump", "The scout gem item chasers carry for that ability (prefab or display name). Amulet_SuperJump = Scout's Initiative.");
        GemBoostSeconds = Synced(blowgun, "GemBoostSeconds", 2.25f, "Length of the chaser gem's effects (speed boost and unlimited stamina) (s).");
        GemDelaySeconds = Synced(blowgun, "GemDelaySeconds", 1f, "The chaser gem's effects start this long after it is used (s).");
        GemInfiniteStamina = Synced(blowgun, "GemInfiniteStamina", true, "The chaser gem also gives unlimited stamina (the rainbow stamina bar) for its duration, without drowsiness afterwards.");
        GemCooldownSeconds = Synced(blowgun, "GemCooldownSeconds", 20f, "Cooldown of the chaser gem (s). It starts when the gem's effects wear off.");
        GemPetrify = Synced(blowgun, "GemPetrify", 0.05f, "Petrification each gem use adds to the chaser (0.05 = 5%).");
        GemHeal = Synced(blowgun, "GemHeal", 0.05f, "Each gem use heals every negative status the chaser has (injury, hunger, cold, poison, drowsiness, ...) by this much (0.05 = 5% of the bar each). Not petrification or weight.");
        GemXRaySeconds = Synced(blowgun, "GemXRaySeconds", 1.65f, "The chaser gem also gives x-ray vision for this long (s), starting with its boost: the chaser sees every living runner within GemXRayRange through walls and terrain, blinking red with a solid red outline. 0 = off.");
        GemXRayRange = Synced(blowgun, "GemXRayRange", 200f, "Range of the chaser gem's x-ray vision (m).");
        SnowballKnockbackBonus = Synced(blowgun, "SnowballKnockbackBonus", 0.1f, "Extra push a thrown snowball gives the scout it hits, as a fraction of its own impact (0.1 = 10% stronger).");
        RunnerGem = Synced(blowgun, "RunnerGem", true, "Runners carry a scout gem (Scout's Ambition) in their hotbar: using it dashes them DashDistance forward (where they look, horizontally), with a cooldown and some petrification as the cost. Its vanilla power is never used; chasers can't use it.");
        RunnerGemItem = Synced(blowgun, "RunnerGemItem", "Amulet_InfiniteStamina", "The scout gem item runners carry for the dash (prefab or display name). Amulet_InfiniteStamina = Scout's Ambition.");
        DashDistance = Synced(blowgun, "DashDistance", 7f, "How far the runner gem's dash goes (m), horizontally in the direction the runner looks. Walls stop it.");
        DashAirFloatSeconds = Synced(blowgun, "DashAirFloatSeconds", 0.65f, "A dash started in mid-air also keeps the runner floating (not falling) this long (s). 0 = off.");
        DashFallProtectDelay = Synced(blowgun, "DashFallProtectDelay", 0.5f, "Seconds after a dash before the runner's fall-damage protection starts.");
        DashFallProtectSeconds = Synced(blowgun, "DashFallProtectSeconds", 1f, "How long a dash protects the runner from fall damage (s), starting DashFallProtectDelay after the dash. 0 = off.");
        DashCooldownSeconds = Synced(blowgun, "DashCooldownSeconds", 32f, "Cooldown of the runner dash (s), shown above the gem's slot.");
        DashPetrify = Synced(blowgun, "DashPetrify", 0.03f, "Petrification each dash adds to the runner (0.03 = 3%).");
        DashHeal = Synced(blowgun, "DashHeal", 0.12f, "Each dash heals every negative status the runner has (injury, hunger, cold, poison, drowsiness, ...) by this much (0.12 = 12% of the bar each). Not petrification or weight.");
        BlowgunPetrify = Synced(blowgun, "BlowgunPetrify", 0.03f, "Petrification each blowgun shot adds to the chaser (0.03 = 3%).");
        ClearPetrifyEachLeg = Synced(blowgun, "ClearPetrifyEachLeg", true, "When a leg ends (and when the next one starts), every player's petrification is cleared.");
        DashLiftFactor = Synced(blowgun, "DashLiftFactor", 0.5f, "The dash also lifts the runner up, like Scout's Initiative's double jump but reaching this fraction of its height (0.5 = half). 0 = off.");
        DashFallMomentumCancel = Synced(blowgun, "DashFallMomentumCancel", 0.8f, "Dashing while falling fast takes this fraction of the falling speed (and fall time) away (0.8 = 80%).");
        CapturePetrifyRelief = Synced(blowgun, "CapturePetrifyRelief", 0.05f, "Petrification a chaser loses for each capture (0.05 = 5%).");
        SnowballBlindSeconds = Synced(blowgun, "SnowballBlindSeconds", 3f, "A snowball thrown by a runner blinds the chaser it hits (blue-flower blindness) for this long. 0 = off.");
        BlowdartDrowsy = Synced(blowgun, "BlowdartDrowsy", 0.2f, "Drowsiness (sleep) a dart adds to the runner it hits (0.2 = 20%).");

        const string effects = "2c. ChaseEffects";

        ScoutmasterChaseMusic = Synced(effects, "ScoutmasterChaseMusic", true, "A runner hears PEAK's own Scoutmaster chase music while a chaser is close (fades in under 50 m, louder under 25 m), like being hunted by the Scoutmaster.");

        const string freeze = "3. Freeze";
        FreezeRange = Synced(freeze, "FreezeRange", 26f, "A runner can only freeze a chaser that is within this distance (m).");
        FreezeConeDegrees = Synced(freeze, "FreezeConeDegrees", 15f, "Half-angle of the look cone (degrees).");
        FreezeDuration = Synced(freeze, "FreezeDuration", 8f, "Seconds a chaser stays frozen. Does not stack or extend.");
        FreezeCooldownSeconds = Synced(freeze, "FreezeCooldownSeconds", 8f, "Seconds after a freeze ends before that chaser can be frozen again (immunity).");
        FreezeHoldGrip = Synced(freeze, "FreezeHoldGrip", true, "Frozen while climbing: keep holding the wall.");
        FreezeLockStamina = Synced(freeze, "FreezeLockStamina", true, "Keep stamina constant while frozen.");
        FreezeBlindsChasers = Synced(freeze, "FreezeBlindsChasers", true, "A chaser frozen by a runner is also blinded (PEAK's blue blindness from the Alpine flowers) while the freeze lasts.");
        FreezeZeroVelocity = Synced(freeze, "FreezeZeroVelocity", false, "Zero ragdoll velocities while frozen (anti-slide).");
        FreezeBlockLook = Synced(freeze, "FreezeBlockLook", false, "Also block camera look while frozen.");
        FreezeSuspendInAir = Synced(freeze, "FreezeSuspendInAir", true, "Frozen while airborne: hang in the air until the freeze ends.");
        FreezeSuspendStiffness = Synced(freeze, "FreezeSuspendStiffness", 10f, "How strongly a suspended player is held at the freeze point (1/s).");
        FreezePulseFastInterval = Synced(freeze, "FreezePulseFastInterval", 0.15f, "Seconds between cold pulses at the start of a freeze.");
        FreezePulseSlowInterval = Synced(freeze, "FreezePulseSlowInterval", 0.9f, "Seconds between cold pulses as the freeze runs out.");

        const string tag = "4. Tag";
        CaptureHoldSeconds = Synced(tag, "CaptureHoldSeconds", 0.45f, "How long a chaser holds interact on a runner to capture them. The runner sees the progress too.");
        CaptureRange = Synced(tag, "CaptureRange", 1.75f, "A chaser can only start (and keep) a capture this close to the runner (m, body centre to body centre), with the cursor (the centre dot) on the runner. The capture then beats anything else in view (backpacks, stuck thorns or arrows) and works whichever way the runner faces.");
        TagMaxDistance = Synced(tag, "TagMaxDistance", 5f, "Host rejects a capture if the two players are further apart than this (lag tolerance).");
        TagPassedOutRunners = Synced(tag, "TagPassedOutRunners", true, "Chasers can capture runners who are passed out.");
        MilkProtectsFromCapture = Synced(tag, "MilkProtectsFromCapture", true, "Runners under the effect of fortified milk can't be captured.");

        const string safe = "5. SafeZones";
        CampfireSafeRadius = Synced(safe, "CampfireSafeRadius", 15f, "Radius (m) around a campfire that is a safe zone: no captures, and runners inside can't freeze chasers. A leg ends when every living runner is inside the next campfire's safe zone.");
        PeakSafeZone = Synced(safe, "PeakSafeZone", true, "The peak (past PEAK's last progress point) is a safe zone too, with the same rules as a campfire's, and a glowing yellow wall at its edge.");
        SafeZoneRequiresLit = Synced(safe, "SafeZoneRequiresLit", false, "Only lit campfires are safe zones.");


        const string items = "7. Items";
        ChaserAllowedItems = Synced(items, "ChaserAllowedItems", "Remedy Fungus, Cactus, Dynamite, Mandrake, Lantern", "Extra item names chasers may pick up/use (comma separated, prefab or display name).");
        ChaserForbiddenItems = Synced(items, "ChaserForbiddenItems", "Snowball, Energy Drink, Big Lollipop, Bounce Fungus, Cloud Fungus, Shelf Fungus, Warp Fungus, Blue Shroomberry, Green Shroomberry, Purple Shroomberry, Red Shroomberry, Yellow Shroomberry", "Items chasers may never pick up or use, even though they are food or healing (comma separated, display or prefab names). Also kept out of clown luggage.");
        ShroomberryEffectSeconds = Synced(items, "ShroomberryEffectSeconds", 1f, "How long a shroomberry's effects last (s). The hunger it cures is unchanged.");
        ChaserAutoAllowHealing = Synced(items, "ChaserAutoAllowHealing", true, "Chasers may pick up and use items that heal injury.");
        ChaserAutoAllowFood = Synced(items, "ChaserAutoAllowFood", true, "Chasers may pick up and eat food.");
        ClownLuggageChasersOnly = Synced(items, "ClownLuggageChasersOnly", true, "During a round only chasers can open clown luggage, and it contains food and healing items.");
        LuggageExtraItems = Synced(items, "LuggageExtraItems", "Fortified Milk, Snowball, Brown Berrynana, Energy Drink, Stone", "Items that can also come out of regular (non-clown) luggage during a round, every leg (comma separated). Empty = off.");
        LuggageExtraChance = Synced(items, "LuggageExtraChance", 0.479f, "Chance (0-1) for each item a regular luggage spawns to be replaced by a random LuggageExtraItems item (split evenly between them: 0.479 with 5 items = about 9.6% each).");
        StoneChaserDamage = Synced(items, "StoneChaserDamage", 0.1f, "Injury a stone thrown by a runner does to the chaser it hits (0.1 = 10%; the chasers' half damage doesn't apply). Runners' snowballs and stones go through other runners.");
        LuggageRareExtraItems = Synced(items, "LuggageRareExtraItems", "Lantern_Faerie, RopeShooterAnti, Anti-Rope Spool", "Items that can also come out of regular (runner) luggage during a round, a little less often than the LuggageExtraItems (see LuggageRareExtraWeight). Comma separated. Empty = off.");
        LuggageRareExtraWeight = Synced(items, "LuggageRareExtraWeight", 0.75f, "Chance of each LuggageRareExtraItems item compared to one LuggageExtraItems item (0.75 = three quarters as likely as a snowball).");
        LuggageExcludedItems = Synced(items, "LuggageExcludedItems", "Flare, Compass, Pirate Compass, Warp Compass", "Items that never come out of any luggage during a round: each one is rerolled from the luggage's own loot pool (comma separated).");
        ChasersOnlyOpenClownLuggage = Synced(items, "ChasersOnlyOpenClownLuggage", true, "During a round chasers can open clown luggage only.");
        BanAmulets = Synced(items, "BanAmulets", true, "Remove amulets from the game.");
        BanGems = Synced(items, "BanGems", true, "Remove gems (scout gems, strange gem, healing gem) from the game.");
        BanBlowguns = Synced(items, "BanBlowguns", true, "Remove blowguns from the world; only chasers can hold one (see ChaserBlowgun).");
        BanRescueClaws = Synced(items, "BanRescueClaws", true, "Remove rescue claws from the game.");
        AllowJetpacks = Synced(items, "AllowJetpacks", false, "Allow jetpacks and rocket packs. Off = removed from the game (not spawned, can't be picked up).");
        AllowGliders = Synced(items, "AllowGliders", false, "Allow gliders. Off = removed from the game (not spawned, can't be picked up).");
        BanHiddenItems = Synced(items, "BanHiddenItems", true, "Remove hidden items that never spawn in normal PEAK (not in any spawn pool and not produced by another item). Check the log for what was detected.");
        AllowedHiddenItems = Synced(items, "AllowedHiddenItems", "Napberry, Kingberry, Clusterberry, Shroomberry, Cactus", "Items never treated as hidden, so everyone can use them (comma separated; matches any item whose name contains an entry, so \"Kingberry\" covers every colour).");
        BannedItems = Synced(items, "BannedItems", "Weird Shroom, ScoutCannonItem", "Extra banned item names (comma separated, prefab or display name).");
        BanGoldenBingBong = Synced(items, "BanGoldenBingBong", true, "Remove the golden Bing Bong (the one that makes its holder invincible) and switch off its shield.");

        const string reward = "9. Rewards";
        RewardItems = Synced(reward, "RewardItems", "Fortified Milk", "Item(s) the first runner into each campfire safe zone can get (prefab or display names, comma separated; one random pick per RewardItemCount).");
        RewardItemCount = Synced(reward, "RewardItemCount", 1, "How many reward items the first runner into each campfire safe zone gets.");

        const string net = "10. Network";
        KickPlayersWithoutMod = Local(net, "KickPlayersWithoutMod", false, "Host kicks players who don't have the same OnTheLookout version.");

        const string ui = "11. UI";
        ChasersSeeGhosts = Synced(ui, "ChasersSeeGhosts", false, "Whether chasers can see ghosts (spectators). Off = ghosts are invisible to living chasers, since a ghost floats around the runner it spectates.");
        ChasersSeeRunnerNames = Synced(ui, "ChasersSeeRunnerNames", false, "Whether chasers see the name tags above runners. Off = chasers have to spot runners by sight.");
        ChaserRedOutline = Synced(ui, "ChaserRedOutline", true, "Chasers' bodies glow red for everyone (PEAK's status glow, like the yellow glow of fortified milk's invincibility).");
        ChaserGlowIntensity = Synced(ui, "ChaserGlowIntensity", 1f, "Strength of the chasers' red glow (vanilla invincibility uses 1).");
        ShowChaserList = Local(ui, "ShowChaserList", true, "Show the chaser list under the ascent label (top right).");
        RoleHintSeconds = Synced(ui, "RoleHintSeconds", 5f, "At the start of every leg (once the roles are drawn) a short list of your role's abilities shows at the bottom left, above the stamina bar, for this long (s), then fades away. KeyRoleHint shows or hides it at any time.");
        RunnersSeeLuggage = Synced(ui, "RunnersSeeLuggage", true, "Runners see every unopened runner luggage on the map through walls and terrain, as a faint green glow that fades out as they come close (gone within reach) and disappears once the luggage is opened.");
        FreezeScreenFrost = Local(ui, "FreezeScreenFrost", true, "Play PEAK's cold screen effect on your own screen while you are frozen.");
        CountdownOpacity = Local(ui, "CountdownOpacity", 0.35f, "Opacity of the big head-start countdown on runners' screens (0-1).");
        CaptureSound = Local(ui, "CaptureSound", true, "Play an explosion sound when a runner is captured.");

        const string debug = "12. Debug";
        DebugKeys = Local(debug, "DebugKeys", false, "Enable host debug/testing keys (F6-F9). Off in normal play.");
        KeyToggleOwnRole = Local(debug, "KeyToggleOwnRole", Key.F6, "HOST: switch your own role between runner and chaser.");
        KeyStartRound = Local(debug, "KeyStartRound", Key.F7, "HOST: start / restart a round (re-rolls roles).");
        KeySelfFreeze = Local(debug, "KeySelfFreeze", Key.F8, "HOST: freeze yourself.");
        KeyFreezeLookTarget = Local(debug, "KeyFreezeLookTarget", Key.F9, "HOST: freeze the player you are looking at.");
        LogLookChecks = Local(debug, "LogLookChecks", false, "Log distance/angle/occlusion for every successful look check.");

        const string admin = "13. Admin";
        AdminKeys = Local(admin, "AdminKeys", true, "Enable host admin keys (work even with DebugKeys off).");
        RandomMap = Local(admin, "RandomMap", false, "HOST: a new game started at the airport kiosk loads a random map from PEAK's map pool (never the same one twice in a row) instead of today's daily map. Also in the host menu.");
        KeyRestartFromCampfire = Local(admin, "KeyRestartFromCampfire", Key.F10, "HOST: quick restart - everyone back to the last lit campfire (or the start), dead players revived, statuses cleared, fresh leg with role reveal + head start. Roles are kept.");
        KeyHostMenu = Local(admin, "KeyHostMenu", Key.Equals, "HOST: open/close the host menu (restart at the airport or previous campfire, teleport everyone to the next campfire, debug hotkeys on/off).");
        KeyChaserOdds = Local(admin, "KeyChaserOdds", Key.Minus, "EVERYONE, in the airport and in the role window after a leg: open/close the role menu (runner / chaser).");
        KeyRoleHint = Local(admin, "KeyRoleHint", Key.P, "EVERYONE, during a round: show/hide the hint with your role's abilities.");

        config.SettingChanged += (_, _) => ConfigSync.Publish();
    }

    /// <summary>
    /// The config file remembers which mod version wrote it. A different version (an update, or an old file)
    /// clears it, so every setting starts from this version's defaults and old, removed settings disappear.
    /// </summary>
    private static void ResetFileOnVersionChange(ConfigFile config)
    {
        ConfigEntry<string> written = config.Bind(VersionSection, VersionKey, "", "The mod version that wrote this file. When a different version starts, this file is reset to that version's defaults. Don't edit.");
        if (written.Value == Plugin.Version) return;

        string was = written.Value;
        bool save = config.SaveOnConfigSet;
        config.SaveOnConfigSet = false;
        File.WriteAllText(config.ConfigFilePath, "");
        config.Reload();
        config.Clear();
        written = config.Bind(VersionSection, VersionKey, "", "The mod version that wrote this file. When a different version starts, this file is reset to that version's defaults. Don't edit.");
        written.Value = Plugin.Version;
        config.SaveOnConfigSet = save;
        config.Save();
        Plugin.Log.LogInfo($"[OTL][Config] config written by '{(was.Length > 0 ? was : "an older version")}', now {Plugin.Version}: reset to this version's defaults.");
    }

    /// <summary>An in-memory config for the fixed settings: its file never exists, and it never saves.</summary>
    private static ConfigFile CreateFixedConfig()
    {
        string path = Path.Combine(Paths.CachePath, "OnTheLookout.fixed.cfg");
        if (File.Exists(path)) File.Delete(path);
        return new ConfigFile(path, saveOnInit: false) { SaveOnConfigSet = false };
    }

    private ConfigFile FileFor(string key) => Configurable.Contains(key) ? _file : _fixed;

    private ConfigEntry<T> Local<T>(string section, string key, T value, string description) =>
        FileFor(key).Bind(section, key, value, description);

    private ConfigEntry<T> Synced<T>(string section, string key, T value, string description)
    {
        ConfigEntry<T> e = FileFor(key).Bind(section, key, value, description + " (host-synced)");
        ConfigSync.Register(e);
        return e;
    }
}

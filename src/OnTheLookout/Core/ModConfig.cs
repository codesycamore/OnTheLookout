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
    public ConfigEntry<bool> EnableRewards { get; }

    // Round & roles
    public ConfigEntry<float> RunnersPerChaser { get; }
    public ConfigEntry<float> RoleRevealSeconds { get; }
    public ConfigEntry<float> HeadStartSeconds { get; }
    public ConfigEntry<bool> AutoStartRound { get; }
    public ConfigEntry<bool> ChaserPreferenceEnabled { get; }
    public ConfigEntry<float> RoleWindowSeconds { get; }
    public ConfigEntry<float> SpawnInteractLockSeconds { get; }
    public ConfigEntry<float> ChaserStatusMultiplier { get; }
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
    public ConfigEntry<float> HeadStartBoostSeconds { get; }
    public ConfigEntry<bool> DisableScoutStatues { get; }
    public ConfigEntry<bool> PeakChasersDie { get; }
    public ConfigEntry<bool> CaptureMoraleBoost { get; }
    public ConfigEntry<float> EnergyDrinkDrowsyMultiplier { get; }
    public ConfigEntry<float> BlowdartDrowsy { get; }
    public ConfigEntry<string> RewardItems { get; }
    public ConfigEntry<float> ChaserSpeedMultiplier { get; }
    public ConfigEntry<float> ChaserClimbSpeedMultiplier { get; }
    public ConfigEntry<float> CaptureBoostPercent { get; }
    public ConfigEntry<float> CaptureBoostStackPercent { get; }
    public ConfigEntry<float> CaptureBoostSeconds { get; }
    public ConfigEntry<float> RunnerStaminaRegenMultiplier { get; }
    public ConfigEntry<string> RunnerLegItems { get; }
    public ConfigEntry<string> RunnerBiomeItems { get; }
    public ConfigEntry<float> FortifiedMilkWeightMultiplier { get; }
    public ConfigEntry<float> FortifiedMilkInvincibilityMultiplier { get; }
    public ConfigEntry<float> EnergyDrinkDurationMultiplier { get; }
    public ConfigEntry<bool> RunnerBackpacks { get; }
    public ConfigEntry<string> CampfireFoodItems { get; }
    public ConfigEntry<bool> ClearStatusesAtCampfire { get; }
    public ConfigEntry<bool> NoReviveCurse { get; }
    public ConfigEntry<bool> DisableScoutmaster { get; }

    // Blowgun (chasers)
    public ConfigEntry<bool> ChaserBlowgun { get; }
    public ConfigEntry<float> BlowgunCooldownSeconds { get; }
    public ConfigEntry<float> TrackingSmokeSeconds { get; }
    public ConfigEntry<bool> ChaserGem { get; }
    public ConfigEntry<string> ChaserGemItem { get; }
    public ConfigEntry<float> GemBoostSeconds { get; }
    public ConfigEntry<float> GemDelaySeconds { get; }
    public ConfigEntry<bool> GemInfiniteStamina { get; }
    public ConfigEntry<float> GemCooldownSeconds { get; }
    public ConfigEntry<float> GemPetrify { get; }
    public ConfigEntry<float> SnowballKnockbackBonus { get; }
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
    public ConfigEntry<float> TagMaxDistance { get; }
    public ConfigEntry<bool> TagPassedOutRunners { get; }
    public ConfigEntry<bool> MilkProtectsFromCapture { get; }

    // Safe zones
    public ConfigEntry<float> CampfireSafeRadius { get; }
    public ConfigEntry<bool> SafeZoneRequiresLit { get; }

    // Fog
    public ConfigEntry<float> FogSpeedMultiplier { get; }
    public ConfigEntry<float> FogStartDelaySeconds { get; }

    // Items
    public ConfigEntry<string> ChaserAllowedItems { get; }
    public ConfigEntry<string> ChaserForbiddenItems { get; }
    public ConfigEntry<float> ShroomberryEffectSeconds { get; }
    public ConfigEntry<bool> ChaserAutoAllowHealing { get; }
    public ConfigEntry<bool> ChaserAutoAllowFood { get; }
    public ConfigEntry<bool> ClownLuggageChasersOnly { get; }
    public ConfigEntry<string> LuggageExtraItems { get; }
    public ConfigEntry<float> LuggageExtraChance { get; }
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
    public ConfigEntry<Key> KeyRestartFromCampfire { get; }
    public ConfigEntry<Key> KeyHostMenu { get; }
    public ConfigEntry<Key> KeyChaserOdds { get; }

    public ModConfig(ConfigFile config)
    {
        _file = config;

        const string mod = "1. Modules";
        EnableFreeze = Local(mod, "EnableFreeze", true, "Runners freeze chasers by looking at them.");
        EnableTag = Local(mod, "EnableTag", true, "Chasers capture runners by holding interact on them (like eating a scout).");
        EnableSafeZones = Local(mod, "EnableSafeZones", true, "Campfires are safe zones.");
        EnableFog = Local(mod, "EnableFog", true, "Faster fog that only hurts runners.");
        EnableItemRules = Local(mod, "EnableItemRules", true, "Chaser item restrictions and banned items.");
        EnableRewards = Local(mod, "EnableRewards", true, "First runner into each campfire safe zone gets a reward item (see 9. Rewards).");

        const string round = "2. Round";
        RunnersPerChaser = Synced(round, "RunnersPerChaser", 4f, "At most one chaser for every this many runners (4 = 1 chaser in 5 players, 2 in 10). Chasers are drawn at random from the players who chose CHASER in the role menu; if nobody did, one random player becomes the chaser. Always leaves at least one runner.");
        RoleRevealSeconds = Synced(round, "RoleRevealSeconds", 4f, "Seconds the role (CHASER / RUNNER) is shown at the start of each leg. Chasers stay frozen and blind during it.");
        HeadStartSeconds = Synced(round, "HeadStartSeconds", 20f, "Runner head start after the role reveal. Chasers stay frozen and blind until it ends.");
        AutoStartRound = Synced(round, "AutoStartRound", true, "Start a round automatically once everyone has woken up on the beach (host).");
        ChaserPreferenceEnabled = Synced(round, "ChaserPreferenceEnabled", true, "Players choose RUNNER (default) or CHASER in the role menu (KeyChaserOdds) in the airport and in the role window after each leg. Chasers are drawn at random from the CHASER volunteers. Choices are private to the host. Off = chasers are drawn from everyone.");
        RoleWindowSeconds = Synced(round, "RoleWindowSeconds", 10f, "After a leg ends, everyone is brought to the campfire and frozen for this many seconds to update their role choice; then the campfire lights itself and the next leg starts with a new role draw.");
        SpawnInteractLockSeconds = Synced(round, "SpawnInteractLockSeconds", 7f, "At the start of a run on the shore nothing can be interacted with while everyone wakes up and for this many seconds after the round starts.");
        ChaserStatusMultiplier = Synced(round, "ChaserStatusMultiplier", 0.5f, "Chasers take this fraction of every negative status (injury, cold, poison, drowsy, ...) they would get in vanilla at the current ascent. Hunger is not reduced.");
        ChaserFallDamageMultiplier = Synced(round, "ChaserFallDamageMultiplier", 0.333f, "Fraction of vanilla fall damage chasers take (0.333 = 1/3). Still scales with the ascent like vanilla, and a big fall still knocks them down. 0 = no fall damage.");
        ZombiesIgnoreChasers = Synced(round, "ZombiesIgnoreChasers", true, "Mushroom zombies don't target or bite chasers.");
        ZombiesWhenChasersDead = Synced(round, "ZombiesWhenChasersDead", true, "When no chaser is alive during a chase (all dead, or none - e.g. a host playing solo), mushroom zombies hunt random runners outside the safe zones (ZombiesByPlayerCount per wave), wave after wave, until the runners reach the campfire or a chaser is back.");
        ZombieLifetimeSeconds = Synced(round, "ZombieLifetimeSeconds", 120f, "How long each of those zombies lasts.");
        ZombieSpawnDistance = Synced(round, "ZombieSpawnDistance", 15f, "How far from its runner a zombie appears (m).");
        ZombiesByPlayerCount = Synced(round, "ZombiesByPlayerCount", "1:1, 6:2, 10:3", "Zombies per wave for a given lobby size, as \"minPlayers:zombies\" pairs. Each zombie hunts one random runner outside the safe zones.");
        ZombieWaveDelaySeconds = Synced(round, "ZombieWaveDelaySeconds", 120f, "Cooldown (s) after a wave of zombies is gone (killed or expired) before the host checks again for living chasers and sends another.");
        ZombieStartDelaySeconds = Synced(round, "ZombieStartDelaySeconds", 300f, "Zombies can only start coming this many seconds after the head start ends (each leg).");
        MandrakeStartSeconds = Synced(round, "MandrakeStartSeconds", 180f, "Mandrakes start appearing at runners this many seconds after the head start ends (each leg). 0 or less = off.");
        MandrakeIntervalSeconds = Synced(round, "MandrakeIntervalSeconds", 60f, "After that, a mandrake is dropped at every living runner outside a safe zone this often (s).");
        MandrakeFirstScreamSeconds = Synced(round, "MandrakeFirstScreamSeconds", 0.75f, "A dropped mandrake screams for the first time this many seconds after it appears (PEAK's own wait for a new mandrake is much longer). Later screams keep PEAK's timing.");
        HeadStartBoostSeconds = Synced(round, "HeadStartBoostSeconds", 3f, "Runners get an energy-drink speed boost (no drowsiness afterwards) for the last this-many seconds of their head start. 0 = off.");
        DisableScoutStatues = Synced(round, "DisableScoutStatues", true, "Scout statues do nothing during a round (no revives, no items). Everyone is brought back at the campfire after each leg instead.");
        PeakChasersDie = Synced(round, "PeakChasersDie", true, "When every living runner has reached the peak, the runners win and every living chaser is brought to the peak and dies.");
        CaptureMoraleBoost = Synced(round, "CaptureMoraleBoost", true, "A chaser who captures a runner also gets a full morale boost (full extra-stamina bar).");
        EnergyDrinkDrowsyMultiplier = Synced(round, "EnergyDrinkDrowsyMultiplier", 1.5f, "Multiplier for the drowsiness an energy drink causes when it wears off.");
        ChaserSpeedMultiplier = Synced(round, "ChaserSpeedMultiplier", 1.15f, "Chaser movement speed multiplier (they can't use most items).");
        ChaserClimbSpeedMultiplier = Synced(round, "ChaserClimbSpeedMultiplier", 1.06f, "Chaser climbing speed on walls, ropes and vines (1.06 = 6% faster).");
        CaptureBoostPercent = Synced(round, "CaptureBoostPercent", 1f, "Temporary chaser speed boost (%) after a capture.");
        CaptureBoostStackPercent = Synced(round, "CaptureBoostStackPercent", 0.5f, "Extra boost (%) for each further capture while the boost is still active.");
        CaptureBoostSeconds = Synced(round, "CaptureBoostSeconds", 5f, "How long the capture boost lasts (refreshed by each capture).");
        RunnerStaminaRegenMultiplier = Synced(round, "RunnerStaminaRegenMultiplier", 1.185f, "Runner stamina regeneration multiplier (1.185 = 18.5% faster).");
        RunnerLegItems = Synced(round, "RunnerLegItems", "Snowball, Brown Berrynana, Fortified Milk", "At the start of each leg every runner gets ONE random item from this list (prefab or display names, comma separated). Empty = off.");
        RunnerBiomeItems = Synced(round, "RunnerBiomeItems", "Alpine:Heat Pack, Volcano:Sports Drink, Swamp:EarlyWorm, Mesa:Aloe Vera", "Extra item every runner gets at the start of a leg in that biome, as \"Biome:Item\" pairs. Biomes use PEAK's names (Caldera = Volcano; The Gloom is assumed to be Swamp).");
        FortifiedMilkWeightMultiplier = Synced(round, "FortifiedMilkWeightMultiplier", 2f, "Fortified milk weighs this many times its vanilla weight.");
        FortifiedMilkInvincibilityMultiplier = Synced(round, "FortifiedMilkInvincibilityMultiplier", 0.35f, "Fortified milk's invincibility lasts this fraction of its vanilla time (0.35 = 65% shorter). Also shortens its capture protection.");
        EnergyDrinkDurationMultiplier = Synced(round, "EnergyDrinkDurationMultiplier", 0.35f, "The energy drink's speed boost lasts this fraction of its vanilla time (0.35 = 65% shorter).");
        RunnerBackpacks = Synced(round, "RunnerBackpacks", true, "After roles are assigned at the start of a round, every runner without a backpack gets one.");
        CampfireFoodItems = Synced(round, "CampfireFoodItems", "Marshmallow, Glizzy", "When the chase of a leg ends at a campfire, the host makes sure there is one of these per living player near the fire (random pick each; spawns only what is missing). Glizzy = the hot dog. Empty = off.");
        ClearStatusesAtCampfire = Synced(round, "ClearStatusesAtCampfire", true, "When a campfire is lit, every player near it is cleared of negative statuses (incl. curse).");
        NoReviveCurse = Synced(round, "NoReviveCurse", true, "Revived players don't get the revival curse / hunger.");
        DisableScoutmaster = Synced(round, "DisableScoutmaster", true, "Don't spawn the Scoutmaster (he hunts the runner furthest from the group).");

        const string blowgun = "2b. Blowgun";
        ChaserBlowgun = Synced(blowgun, "ChaserBlowgun", true, "Chasers get a blowgun with unlimited uses. A dart doesn't put runners to sleep; it marks them with flare smoke instead.");
        BlowgunCooldownSeconds = Synced(blowgun, "BlowgunCooldownSeconds", 30f, "Seconds between blowgun shots.");
        TrackingSmokeSeconds = Synced(blowgun, "TrackingSmokeSeconds", 7f, "How long the tracking smoke follows a darted runner.");
        ChaserGem = Synced(blowgun, "ChaserGem", true, "Chasers also carry a scout gem (Scout's Initiative) that never runs out: using it gives, after a short delay, an energy-drink speed boost and unlimited stamina (no drowsiness), with a cooldown and some petrification as the cost. Its vanilla power is never activated.");
        ChaserGemItem = Synced(blowgun, "ChaserGemItem", "Amulet_SuperJump", "The scout gem item chasers carry for that ability (prefab or display name). Amulet_SuperJump = Scout's Initiative.");
        GemBoostSeconds = Synced(blowgun, "GemBoostSeconds", 2.25f, "Length of the chaser gem's effects (speed boost and unlimited stamina) (s).");
        GemDelaySeconds = Synced(blowgun, "GemDelaySeconds", 1f, "The chaser gem's effects start this long after it is used (s).");
        GemInfiniteStamina = Synced(blowgun, "GemInfiniteStamina", true, "The chaser gem also gives unlimited stamina (the rainbow stamina bar) for its duration, without drowsiness afterwards.");
        GemCooldownSeconds = Synced(blowgun, "GemCooldownSeconds", 60f, "Cooldown of the chaser gem (s). It starts when the gem's effects wear off.");
        GemPetrify = Synced(blowgun, "GemPetrify", 0.08f, "Petrification each gem use adds to the chaser (0.08 = 8%).");
        SnowballKnockbackBonus = Synced(blowgun, "SnowballKnockbackBonus", 0.1f, "Extra push a thrown snowball gives the scout it hits, as a fraction of its own impact (0.1 = 10% stronger).");
        CapturePetrifyRelief = Synced(blowgun, "CapturePetrifyRelief", 0.05f, "Petrification a chaser loses for each capture (0.05 = 5%).");
        SnowballBlindSeconds = Synced(blowgun, "SnowballBlindSeconds", 2.5f, "A snowball thrown by a runner blinds the chaser it hits (blue-flower blindness) for this long. 0 = off.");
        BlowdartDrowsy = Synced(blowgun, "BlowdartDrowsy", 0.18f, "Drowsiness (sleep) a dart adds to the runner it hits (0.18 = 18%).");

        const string effects = "2c. ChaseEffects";

        ScoutmasterChaseMusic = Synced(effects, "ScoutmasterChaseMusic", true, "A runner hears PEAK's own Scoutmaster chase music while a chaser is close (fades in under 50 m, louder under 25 m), like being hunted by the Scoutmaster.");

        const string freeze = "3. Freeze";
        FreezeRange = Synced(freeze, "FreezeRange", 26f, "A runner can only freeze a chaser that is within this distance (m).");
        FreezeConeDegrees = Synced(freeze, "FreezeConeDegrees", 15f, "Half-angle of the look cone (degrees).");
        FreezeDuration = Synced(freeze, "FreezeDuration", 10f, "Seconds a chaser stays frozen. Does not stack or extend.");
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
        CaptureHoldSeconds = Synced(tag, "CaptureHoldSeconds", 0.4f, "How long a chaser holds interact on a runner to capture them. The runner sees the progress too.");
        TagMaxDistance = Synced(tag, "TagMaxDistance", 5f, "Host rejects a capture if the two players are further apart than this (lag tolerance).");
        TagPassedOutRunners = Synced(tag, "TagPassedOutRunners", true, "Chasers can capture runners who are passed out.");
        MilkProtectsFromCapture = Synced(tag, "MilkProtectsFromCapture", true, "Runners under the effect of fortified milk can't be captured.");

        const string safe = "5. SafeZones";
        CampfireSafeRadius = Synced(safe, "CampfireSafeRadius", 15f, "Radius (m) around a campfire that is a safe zone: no captures, and runners inside can't freeze chasers. A leg ends when every living runner is inside the next campfire's safe zone.");
        SafeZoneRequiresLit = Synced(safe, "SafeZoneRequiresLit", false, "Only lit campfires are safe zones.");

        const string fog = "6. Fog";
        FogSpeedMultiplier = Synced(fog, "FogSpeedMultiplier", 2.5f, "During a round the fog, the rising lava and the rising gloom move this many times faster.");
        FogStartDelaySeconds = Synced(fog, "FogStartDelaySeconds", 300f, "During a round the fog, the rising lava and the rising gloom start this many seconds after the head start ends (each leg), instead of PEAK's own timing.");

        const string items = "7. Items";
        ChaserAllowedItems = Synced(items, "ChaserAllowedItems", "Remedy Fungus, Cactus, Dynamite, Mandrake", "Extra item names chasers may pick up/use (comma separated, prefab or display name).");
        ChaserForbiddenItems = Synced(items, "ChaserForbiddenItems", "Snowball, Energy Drink, Big Lollipop, Bounce Fungus, Cloud Fungus, Shelf Fungus, Warp Fungus, Blue Shroomberry, Green Shroomberry, Purple Shroomberry, Red Shroomberry, Yellow Shroomberry", "Items chasers may never pick up or use, even though they are food or healing (comma separated, display or prefab names). Also kept out of clown luggage.");
        ShroomberryEffectSeconds = Synced(items, "ShroomberryEffectSeconds", 1f, "How long a shroomberry's effects last (s). The hunger it cures is unchanged.");
        ChaserAutoAllowHealing = Synced(items, "ChaserAutoAllowHealing", true, "Chasers may pick up and use items that heal injury.");
        ChaserAutoAllowFood = Synced(items, "ChaserAutoAllowFood", true, "Chasers may pick up and eat food.");
        ClownLuggageChasersOnly = Synced(items, "ClownLuggageChasersOnly", true, "During a round only chasers can open clown luggage, and it contains food and healing items.");
        LuggageExtraItems = Synced(items, "LuggageExtraItems", "Fortified Milk, Snowball, Brown Berrynana, Energy Drink", "Items that can also come out of regular (non-clown) luggage during a round, every leg (comma separated). Empty = off.");
        LuggageExtraChance = Synced(items, "LuggageExtraChance", 0.383f, "Chance (0-1) for each item a regular luggage spawns to be replaced by a random LuggageExtraItems item (split evenly between them: 0.383 with 4 items = about 9.6% each).");
        ChasersOnlyOpenClownLuggage = Synced(items, "ChasersOnlyOpenClownLuggage", true, "During a round chasers can open clown luggage only.");
        BanAmulets = Synced(items, "BanAmulets", true, "Remove amulets from the game.");
        BanGems = Synced(items, "BanGems", true, "Remove gems (scout gems, strange gem, healing gem) from the game.");
        BanBlowguns = Synced(items, "BanBlowguns", true, "Remove blowguns from the world; only chasers can hold one (see ChaserBlowgun).");
        BanRescueClaws = Synced(items, "BanRescueClaws", true, "Remove rescue claws from the game.");
        AllowJetpacks = Synced(items, "AllowJetpacks", false, "Allow jetpacks and rocket packs. Off = removed from the game (not spawned, can't be picked up).");
        AllowGliders = Synced(items, "AllowGliders", false, "Allow gliders. Off = removed from the game (not spawned, can't be picked up).");
        BanHiddenItems = Synced(items, "BanHiddenItems", true, "Remove hidden items that never spawn in normal PEAK (not in any spawn pool and not produced by another item). Check the log for what was detected.");
        AllowedHiddenItems = Synced(items, "AllowedHiddenItems", "Napberry, Kingberry, Clusterberry, Shroomberry, Cactus", "Items never treated as hidden, so everyone can use them (comma separated; matches any item whose name contains an entry, so \"Kingberry\" covers every colour).");
        BannedItems = Synced(items, "BannedItems", "Weird Shroom", "Extra banned item names (comma separated, prefab or display name).");
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
        ChaserGlowIntensity = Synced(ui, "ChaserGlowIntensity", 0.8f, "Strength of the chasers' red glow (vanilla invincibility uses 1).");
        ShowChaserList = Local(ui, "ShowChaserList", true, "Show the chaser list under the ascent label (top right).");
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
        KeyRestartFromCampfire = Local(admin, "KeyRestartFromCampfire", Key.F10, "HOST: quick restart - everyone back to the last lit campfire (or the start), dead players revived, statuses cleared, fresh leg with role reveal + head start. Roles are kept.");
        KeyHostMenu = Local(admin, "KeyHostMenu", Key.Equals, "HOST: open/close the host menu (restart at the airport or previous campfire, teleport everyone to the next campfire, debug hotkeys on/off).");
        KeyChaserOdds = Local(admin, "KeyChaserOdds", Key.Minus, "EVERYONE, in the airport and in the role window after a leg: open/close the role menu (runner / chaser).");

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

# Changelog

## 1.8.1

- **Cooldowns refresh every leg:** when a leg starts, the blowgun, chaser gem and dash gem cooldowns are reset for everyone (freeze immunity was already reset).
- **Hunger:** grows at **0.7×** the vanilla rate during a round (still scaled by the ascent, `HungerRateMultiplier`), and not at all for a runner in a safe zone.
- **Dash fall protection:** from 0.5 s after a dash, the runner takes no fall damage (and isn't knocked down by a fall) for 1 s (`DashFallProtectDelay`, `DashFallProtectSeconds`).
- **Energy drinks** last their vanilla time again (`EnergyDrinkDurationMultiplier` removed); the drowsiness when they wear off stays at 1.5×.
- **No energy drinks or lollipops for chasers:** besides the name list, any item that gives the energy-drink speed boost or unlimited stamina is now refused to chasers and kept out of clown luggage. The host log lists what each clown luggage was filled with.
- **Snowballs:** the hit is now detected on the thrower's client (which simulates the snowball) and sent to the scout it hit, who gets the extra **10%** push and, if a runner's snowball hit a chaser, **3 s** of blindness (was 2.5 s).
- The runners' head-start countdown is in PEAK yellow (same style and transparency).
- The host-controls hint uses a smaller font.

## 1.8.0

- **Fog, lava and gloom back to vanilla:** they start rising on PEAK's own timer and at PEAK's own speed (no more 5-minute start and 2.5x speed). `FogStartDelaySeconds` and `FogSpeedMultiplier` are removed; 6 settings remain configurable. The fog still never hurts chasers.
- **Captures fixed:** a runner's backpack or fanny pack, thorns, arrows and stuck items can no longer take over a chaser's interaction (the backpack menu could open in place of the capture when the chaser was just outside capture range). A capture starts with the cursor anywhere on the runner, from any side, and once started keeps going while the runner moves; it only breaks if they get clearly out of reach (1 m beyond `CaptureRange`), drink fortified milk or reach a safe zone.
- **Chasers can only capture runners:** no carrying a passed-out runner, no using items on them, no opening their backpack or fanny pack, no pulling thorns, arrows or stuck items off them.
- **Capture rush:** each capture now gives the chaser an energy drink + lollipop for **4 s** (speed boost and unlimited stamina, no drowsiness; `CaptureBoostSeconds`), and each further capture while it runs adds **3 s** (`CaptureBoostExtendSeconds`). Replaces the small speed boost and the morale boost.
- Chaser gem cooldown **20 s** (was 60) and **3%** petrification (was 8%).
- Runner dash cooldown **32 s** (was 70) and **3%** petrification (was 5%).
- Freezes last **8 s** (was 10).
- **Red glow on chasers fixed:** it is now kept up every frame at full strength, exactly like fortified milk's glow, instead of fading between refreshes and losing to PEAK's other status pulses.
- **Gathering at the campfire:** after a leg everyone is placed within about 4 m of the campfire. When the role window closes, anyone more than 5 m away (or still dead) gets everyone brought there once more, and roles and timers start 1.5 s later. A chaser frozen in mid-air when the leg ends is no longer pulled back to where they froze.

## 1.7.2

- **Gems put themselves away:** after a gem's ability fires (the chaser boost or the runner dash), the gem goes back to its hotbar slot so the hands are free; it isn't dropped.
- **Fix:** on the shore, runners couldn't interact with anything for the first ~3 seconds of their head start. The start-of-run interaction lock now ends exactly when the role reveal ends (`SpawnInteractLockSeconds` removed).

## 1.7.1

- Runner dash **4.3 m** (was 3.2) and **5%** petrification per dash (was 6%).
- Runners' head-start speed boost lasts **4 s** (was 3, `HeadStartBoostSeconds`).
- New Thunderstore summary (no more fireworks; mentions the chaser and runner abilities).
- The mandrake settings (`MandrakeStartSeconds`, `MandrakeIntervalSeconds`, `MandrakeFirstScreamSeconds`) can no longer be changed; 8 settings remain configurable.

## 1.7.0

- **Fewer settings, always current:** only 11 settings can still be changed (`HeadStartSeconds`, `HeadStartBoostSeconds`, `RunnersPerChaser`, `FogStartDelaySeconds`, `FogSpeedMultiplier`, `ZombieStartDelaySeconds`, `ZombieWaveDelaySeconds`, `ZombieLifetimeSeconds`, `MandrakeStartSeconds`, `MandrakeIntervalSeconds`, `MandrakeFirstScreamSeconds`). Everything else is built in, so every host runs the same rules. The config file now remembers which version wrote it; when a different version starts, the file is reset to that version's defaults, so old values never carry over.

- Capture hold **0.45 s** (was 0.4, `CaptureHoldSeconds`).

- **Runner dash:** runners carry **Scout's Ambition** (`RunnerGemItem`) in their hotbar; using it dashes them **3.2 m** horizontally where they look (`DashDistance`; walls stop it, not while climbing), and a dash in mid-air floats them for **0.65 s** (`DashAirFloatSeconds`), with a **70 s** cooldown shown above its slot (`DashCooldownSeconds`) and **6%** petrification per dash (`DashPetrify`). Its own power is never used and chasers can't use it. Given at every leg start and to chasers who become runners; runners who become chasers drop it with everything else.
- **Gems only do the mod's abilities:** during a round, Scout's Ambition (runners) and Scout's Initiative (chasers) never switch on their original scout-gem power, so no stamina aura, double jump or their petrification; only the dash and the chaser boost.
- **Safe at the campfire:** when the runners win a leg, every runner in the safe zone loses **3%** petrification (`SafeZonePetrifyRelief`).
- **Captures up close:** a chaser has to be practically touching a runner (`CaptureRange`, 1.4 m) to start or keep a capture. Up close the capture always wins over anything else in view, whichever way the runner faces: their backpack or fanny pack, and stuck thorns, arrows or cactus balls, no longer get in the way.
- **Chaser list:** a chaser's freeze countdown can never show more than the freeze length; if the game ever reports more, it is logged.

## 1.6.1

- Capture hold **0.4 s** (was 0.75, `CaptureHoldSeconds`). The runner's capture bar follows it.

## 1.6.0

### Roles
- **Role choice can't get lost:** while the role menu is available (airport or role window) every player's game re-sends its choice to the host every 2 seconds. The draw stays: chasers come **only** from players who chose CHASER; only if nobody did is a random player picked.
- **Role swaps are checked leg to leg:** the host saves everyone's role when a leg ends (before the role window) and compares it with the new roles when the next leg starts. Changed players drop or get their role items. Every runner's own game also drops any chaser item it still carries shortly after each leg starts.

### Capture
- Capture hold **0.75 s** (was 1.5, `CaptureHoldSeconds`).
- The **runner sees the capture progress** too: a red bar under their crosshair ("X IS CAPTURING YOU!").
- A runner under **fortified milk** can't even be targeted for a capture.

### Chasers
- The chaser ability item is now **Scout's Initiative** (a scout gem, `ChaserGemItem`) instead of the napberry: same 1 s delay, 2.25 s speed boost + unlimited stamina, 60 s cooldown and 8% petrify. Its own power is never used. Settings renamed `Napberry*` → `Gem*` (`ChaserGem`, `GemBoostSeconds`, …). Napberries are normal food again, for everyone.
- **Chasers can't use snowballs** (`ChaserForbiddenItems`).
- **No more Scoutmaster sounds on chasers:** the old random Scoutmaster sound effect (`ScoutmasterSounds`) is removed; it could still be switched on by an older saved config and caused audio glitches. Runners still hear the Scoutmaster chase music.

### Items
- **Regular luggage** can also hold **fortified milk, snowballs, brown berrynanas and energy drinks** during a round, every leg: each item it spawns has a 38.3% chance to be one of them, about 9.6% for each (`LuggageExtraItems`, `LuggageExtraChance`).
- **Snowballs push 10% harder** when they hit someone (`SnowballKnockbackBonus`).

### HUD
- The host sees **PRESS HOTKEY (+) FOR HOST CONTROLS** just above their stamina bar. The host menu also opens with numpad +.

## 1.5.1

- **Dropped mandrakes scream right away:** their first scream comes **0.75 s** after they appear (`MandrakeFirstScreamSeconds`). Later screams keep PEAK's timing.

## 1.5.0

### Roles and legs
- **New roles every leg.** When a leg ends, everyone (the dead revived) is brought next to the campfire and frozen for a **10-second role window** (`RoleWindowSeconds`): press **-** to choose **RUNNER** (the default) or **CHASER**. Then the campfire **lights itself** and new roles are drawn at random from the volunteers, **one chaser per 4 runners** at most (`RunnersPerChaser`); if nobody volunteered, one random player is the chaser. Replaces the airport chaser odds and `ChasersByPlayerCount`.
- **Every leg has a winner:** the runners win it when every living runner reaches the next safe zone, the chasers when every runner is dead. A notification shows who won and the time left to choose.
- **Role swaps:** a runner who becomes a chaser drops every item and their backpack, then gets the blowgun and napberry; a chaser who becomes a runner drops the blowgun and napberry and gets a backpack if they have none, plus this leg's runner items if they haven't had them. **Runners can't use napberries** any more (or blowguns). A chaser can carry only **one blowgun and one napberry**; a second can't be picked up (the cooldowns belong to the player, not the item).
- **Airport:** the **-** menu is in the airport too, for the first leg's draw: RUNNER is pre-selected, CHASER puts you in the pool. Chasers are a random pick from the pool (no odds); nobody in it = one random chaser.
- **Scout statues are off** during a round (`DisableScoutStatues`): no revives, no items. Statue conversions are gone.
- Campfires can't be lit by hand during a round.
- **Back in the airport** (game over or the host menu) the mod resets the round, roles and choices, so the airport plays like vanilla PEAK.

### Chase
- **Captures need a hold:** a chaser looks at a runner and holds interact for **1.5 s** (`CaptureHoldSeconds`), like eating a scout, with the same hold ring. Running into a runner no longer captures.
- **Freeze lasts 10 s** (`FreezeDuration`).
- **Fog, rising lava and rising gloom** start **5 minutes after the head start** every leg (`FogStartDelaySeconds`), instead of PEAK's own timing, and rise **2.5× faster** (`FogSpeedMultiplier`, was 1.5×).
- **Mandrakes** are dropped at every runner outside a safe zone every minute, from 3 minutes after the head start (`MandrakeStartSeconds`, `MandrakeIntervalSeconds`).
- Safe zone radius **20 m → 15 m**.
- **Fireworks removed:** no more bursts above players during the chase (`FireworkIntervalSeconds` and `FireworkHeight` are gone).

### Runners
- A **3-second energy-drink boost** (no drowsiness) at the end of every head start (`HeadStartBoostSeconds`).
- **Snowballs** thrown by runners **blind** the chaser they hit for 2.5 s (`SnowballBlindSeconds`).
- Stamina regeneration **+18.5%** (was +12%).

### Chasers
- **Napberry boost** (`ChaserNapberry`): chasers carry a napberry that is never eaten. Using it gives, 1 s later (`NapberryDelaySeconds`), a 2.25 s energy-drink speed boost plus unlimited stamina (rainbow bar, `NapberryInfiniteStamina`), then a 1-minute cooldown that starts when the effects wear off, with a cooldown number above its slot, and adds 8% petrification; each capture removes 5% (`NapberryBoostSeconds`, `NapberryCooldownSeconds`, `NapberryPetrify`, `CapturePetrifyRelief`).
- The **blowgun can't be dropped or thrown**, and players who stop being chasers lose it. Every chaser, including a runner who just became one, is kept supplied with the blowgun and napberry: the host checks every half second and hands out whatever is missing (making room if their slots are full).
- Take **half** of vanilla negative statuses (was 1/3, `ChaserStatusMultiplier`).
- Blowgun tracking smoke lasts **7 s** (was 5).
- **Chasers glow red** for everyone: PEAK's own status glow on their body, like the yellow glow of milk's invincibility (`ChaserRedOutline`, `ChaserGlowIntensity`).
- Can use **dynamite** and **mandrakes** (`ChaserAllowedItems`).

### Items
- The **golden Bing Bong** is removed and its invincibility shield is switched off (`BanGoldenBingBong`).

## 1.4.0

### Round flow
- **Lighting a campfire freezes everyone and starts the next leg right away**: role reveal → runners' head start. The biome title no longer matters (`BiomeTitleSeconds` and `NoTitleFallbackSeconds` are gone).
- **Shore start:** nobody can move until every player has woken up and loaded in; then the role reveal and timers start.
- **Peak:** the runners win once **every living runner** is at the peak (was: the first one). The chasers are then brought up there and die (`PeakChasersDie`).
- After **every runner was caught** and the chasers were sent ahead, the next campfire gives **no first-runner reward**.

### Runners
- The first-runner reward is now a **fortified milk** (`RewardItems`).
- **Leg, biome and reward items no longer get "stuck" in a hotbar slot.** They are dropped at the runner's feet and their own game picks them up the vanilla way (into a free slot and into their hands). With every slot full, the item stays on the ground in front of them.
- Blowgun darts add **18%** drowsiness (was 10%, `BlowdartDrowsy`).
- **Chase music:** a runner with a chaser closing in hears PEAK's own Scoutmaster chase music, fading in under 50 m and louder under 25 m (`ScoutmasterChaseMusic`). The old random Scoutmaster sounds at chasers kept cutting out and are now off by default (`ScoutmasterSounds`).

### Chasers
- Take **1/3** of vanilla fall damage (was 1/4, `ChaserFallDamageMultiplier`); a big fall still knocks them down.
- **Cactus balls** can be picked up by chasers (and by everyone: they were wrongly treated as a hidden item). Added to `ChaserAllowedItems` and `AllowedHiddenItems`.
- **Can't see runners' name tags** (`ChasersSeeRunnerNames`).
- **Blinded while frozen:** a runner's freeze also gives the chaser PEAK's blue-flower blindness until it ends (`FreezeBlindsChasers`).

## 1.3.2

- Everyone stays frozen only **3 s** after a runner sees the next biome title, instead of 7.5 s (`BiomeTitleSeconds`).
- The role reveal now reads *YOU ARE A... RUNNER / CHASER*, and the runners' countdown reads *YOU HAVE A HEADSTART... RUN!*

## 1.3.1

### Round flow
- **Lighting a campfire no longer freezes everyone right away.** The chase ends and everyone can move until a runner walks far enough to see the next biome's title. Then everyone freezes while it plays, and the next leg starts: role reveal → runners' head start. If the title was already seen (or there is none), everyone freezes at lighting and the leg starts `NoTitleFallbackSeconds` (now 5 s) later.

### Balance
- Safe zone radius **30 m → 20 m** (`CampfireSafeRadius`).

### Items
- **Energy drink** speed boost lasts 65% less (`EnergyDrinkDurationMultiplier` = 0.35).

### HUD
- The airport chaser-odds hint is readable again: yellow text on a dark plate instead of a thick outline that turned it black.

## 1.3.0

### Runners
- **Biome items:** at the start of each leg every runner also gets an item for the biome ahead: a **heat pack** in the Alpine, a **sports drink** in the Caldera, an **early worm** in the Gloom and **aloe vera** in the Mesa (`RunnerBiomeItems`). These items are never treated as hidden.

### Airport
- **Chaser odds:** in the airport every player can press **-** to pick *I want to be a chaser* / *no preference* / *I'd rather be a runner*. The choice goes privately to the host's game only and is never shown to anyone, weights the **initial** role draw (`ChaserOddsWantChaser` 3, `ChaserOddsNoPreference` 1, `ChaserOddsRatherRun` 0.25) and is cleared after every draw. Statue conversions stay fully random. The host can switch it off with `ChaserPreferenceEnabled`.

### Balance
- Safe zone radius **30 m → 20 m** (`CampfireSafeRadius`).

### Items
- **Fortified milk** is twice as heavy (`FortifiedMilkWeightMultiplier`) and its invincibility (and so its capture protection) lasts 65% less (`FortifiedMilkInvincibilityMultiplier` = 0.35).


## 1.2.0

### Round flow
- **Lighting a campfire freezes everyone** and shows the next biome's title right away (it used to need someone to walk past the biome's progress point). Once the title has played, the next leg starts: role reveal (everyone frozen) → runners released for their head start → chasers frozen and blind until it ends.
- **Every runner dead** (passing out doesn't count): instead of ending the round, the chasers are sent to the next campfire. The scout statue there revives everyone and turns **one extra** revived runner into a chaser (`WipeExtraConversions`). The chasers only win this way when there's no campfire left to go to.
- **No chaser alive** (all dead, or none - e.g. a host playing solo is a runner): mushroom zombies hunt runners outside the safe zones. Each wave sends `ZombiesByPlayerCount` zombies (1, then 2 from 6 players, 3 from 10), each after one random runner; each lasts 2 minutes; after a wave is gone (killed or expired) there is a **2-minute cooldown** (`ZombieWaveDelaySeconds`) before the next, until the runners reach the campfire or a chaser is back. Each spawn is announced to everyone in the middle of the screen ("A ZOMBIE IS ON THE HUNT") with PEAK's lava-rises alarm and a screen shake (`ZombiesWhenChasersDead`, `ZombieLifetimeSeconds`, `ZombieSpawnDistance`). All zombies are removed the moment the leg ends: every runner safe at the campfire, or every runner caught and the chasers sent ahead. Zombies only start 5 minutes after the head start ends (`ZombieStartDelaySeconds`).

### Balance
- Safe zone radius **50 m → 30 m**, and runners inside a safe zone can no longer freeze chasers.
- Runner stamina regeneration back to **+12%** (from +18%).

### Balance
- Safe zone radius **30 m → 20 m** (`CampfireSafeRadius`).

### Items
- **Shroomberries** can be eaten again (runners), but every effect they cause lasts only **1 second** (`ShroomberryEffectSeconds`): timed effects end, knock-downs are cut short and negative statuses are taken back. The hunger they cure is unchanged.

### Host menu
- New **host menu** on the **=** key (`KeyHostMenu`), built from PEAK's own menu buttons and fonts: restart at the airport, restart at the previous campfire, teleport everyone to the next campfire, and debug hotkeys on/off.

### Fixes
- **Zombies were spawned asleep and hidden** (how PEAK spawns its own), so the alert showed but no zombie came, and PEAK culled the sleeping ones. They are now woken up and pointed at their runner once they have faded in, and the alert is only sent when that has happened.
- **Zombie cooldown was skipped when a zombie expired**, so a new zombie (and its announcement) came the moment the old one ran out. A wave now only ends once all its zombies are gone (expired, or killed and their body cleaned up 5 s later), and the 2-minute cooldown always starts then.
- Items given at the start of a leg could land in the selected empty-hand slot without showing in the player's hands, making the slot look blocked. The player now holds the new item if their hands were empty.
- **Shroomberries, Napberries, Kingberries, Clusterberries and other naturally placed items could not be picked up by anyone.** Napberry, Kingberry, Clusterberry and Shroomberry are now always allowed (`AllowedHiddenItems`, any colour); Weird Shroom stays removed (`BannedItems`). The hidden-item check only knew spawn pools and spawners; it now scans everything in the level that places or references an item (scenery items included), so only real test/unused items are removed.


## 1.1.0

### Roles
- **Roles now last for the whole run.** PEAK re-triggers its run start during a run (scene loads, quicksave resume), which could re-roll roles mid-run and turn dead or converted chasers back into runners. A chaser who dies stays a chaser when revived, and so does anyone who became a chaser. Roles are only re-rolled when a new run starts from the airport.
- Chasers revived by a scout statue get their blowgun back right away.

### Chasers
- **Fall damage is back at 1/4 of vanilla** (still scaled by the ascent) instead of none. `ChaserFallDamageMultiplier` (0.25) replaces `ChaserNoFallDamage`.
- **6% faster climbing** on walls, ropes and vines (`ChaserClimbSpeedMultiplier`).
- **Can no longer use** energy drinks, lollipops, shroomberries or the Roots fungi (Bounce, Cloud, Shelf, Warp), which counted as food (`ChaserForbiddenItems`). They are also kept out of clown luggage. The healing **Remedy Fungus** stays allowed (`ChaserAllowedItems`).

### Runners
- Stamina regeneration raised from **+12% to +18%** (`RunnerStaminaRegenMultiplier`).

### HUD
- **Blowgun cooldown** is now a seconds countdown above the blowgun's hotbar slot, in the head-start countdown's font, replacing the ring that wasn't showing.

## 1.0.0

First release of the **Chasers vs Runners** game mode.

- Random chasers per lobby size; role reveal, head start with frozen and blind chasers.
- Runners freeze chasers by looking at them (range, cooldown/immunity, mid-air and climbing freezes, icy pulses).
- Captures by collision, host-validated; campfire safe zones; campfire-to-campfire legs that restart after each biome title.
- Chaser kit: unlimited blowgun with tracking smoke, speed and capture boosts, reduced statuses (not hunger), no fall damage, fog and zombie immunity, clown-luggage access.
- Runner kit: backpack, per-leg random item, faster stamina regeneration, fortified-milk protection, energy-drink reward.
- Scout statues convert a ghost into a chaser; campfire food for everyone; fireworks and Scoutmaster sounds for chasers.
- Removed items: amulets, gems, rescue claws, jetpacks, gliders, world blowguns, hidden test items.
- Host quick restart (F10), host-synced config, version check for players without the mod.

# On The Lookout

**Chasers vs Runners for PEAK.** Every leg, a few scouts become **chasers** and hunt everyone else up the mountain. **Runners** have to make it from campfire to campfire, and finally to the peak, without getting caught. A runner's best defence is to turn around and **look at a chaser** to freeze them in place.

> **New in 1.6.1:** captures take only **0.4 s** of holding interact. **1.6.0:** chasers carry **Scout's Initiative** (a scout gem) instead of the napberry for their boost, captures take **0.75 s** and the runner sees the capture bar, milk-protected runners can't be captured, regular luggage holds more runner items (milk, snowballs, brown berrynanas, energy drinks), snowballs push harder and are runner-only, role choices are re-sent so none get lost, and the host sees a hint for the host menu (**+**). **1.5.1:** mandrakes scream 0.75 s after appearing. **1.5.0:** a new role every leg (volunteer as chaser in the **-** menu at the campfire), captures by **holding interact** on a runner, the campfire lights itself after a 10-second role window, scout statues are off, chasers carry a reusable **napberry boost**, snowballs blind chasers, mandrakes start dropping at runners late in a leg, and plenty of balance changes. See the changelog.

> **Everyone in the lobby needs this mod** (same version). The host runs the game: roles, freezes, captures and rewards are all decided by the host and synced to everyone.

---

## How a game works

0. **In the airport**, press **-** to choose **RUNNER** (pre-selected) or **CHASER** for the first leg. Chasers are drawn at random from the players who chose CHASER.
1. **Wake up on the shore.** Nobody can move or interact until **everyone** has woken up and loaded in, and interacting stays locked for a few seconds after (no grabbing items before the roles are out).
2. **Roles are drawn and revealed.** Each player sees **CHASER** (red) or **RUNNER** (yellow) in the middle of the screen. Everyone is frozen during the reveal. Chasers are picked at random from the airport volunteers, **one chaser for every 4 runners** at most; if nobody volunteered, **one random player** is the chaser.
3. **Head start.** Runners are released and get a 20-second head start with a big countdown, and a **3-second speed boost** at the end of it. Chasers stay **frozen and blind** the whole time; their screen fades back in near the end.
4. **The chase.** Chasers hunt the runners up the current biome.
   - A chaser **captures** a runner by looking at them and **holding interact for 0.4 seconds**, the same way a hungry scout eats another one. The runner sees the same progress bar under their crosshair, so they know how close it is. A runner under **fortified milk** can't be captured at all. The runner dies with a bang.
   - A runner **freezes** a chaser by **looking at them** within 26 m, from **outside** a safe zone. The chaser can't move, act or fall and is **blinded** for 10 seconds, then is immune to freezing for 8 seconds.
   - The **fog** (and the rising **lava** in the Caldera and the rising **gloom**) starts **5 minutes** after the head start ends, and rises **2.5× faster** than vanilla.
   - From **3 minutes** after the head start, a **mandrake** is dropped at every runner outside a safe zone **every minute**.
   - If **no chaser is alive** (all dead, or none at all, e.g. a host playing solo), **mushroom zombies** hunt the runners from 5 minutes after the head start (one wave at a time, 2 minutes each, 2-minute cooldown between waves).
5. **The leg is won.**
   - **Runners win the leg** when **every living runner** is in the next campfire's **safe zone** (15 m around it: no captures there, and runners inside can't freeze chasers).
   - **Chasers win the leg** when **every runner is dead** (passing out doesn't count).
6. **Role window.** Everyone, the dead included, is brought next to the campfire and frozen for **10 seconds**. Press **-** to choose **RUNNER** (the default) or **CHASER** for the next leg. Food is laid out so everyone gets something to eat.
7. **Next leg.** The campfire lights itself, clearing everyone's negative statuses, and new roles are drawn: chasers are picked at random from the players who chose CHASER, **one chaser for every 4 runners** at most. If nobody volunteered, one random player is the chaser. Then the reveal and head start start again.
8. **Win the game.**
   - **Runners win** when **every living runner** reaches **the peak**. The chasers are brought up there and **die**.
   - **Chasers win** when every runner is dead on the last stretch, with no campfire left to go to.

### Roles and choices
- Your choice in the **-** menu is private: only the host's game receives it. It stays until you change it, and resets to RUNNER when a run ends and everyone is back in the airport. There are no odds: the chasers are a random pick from the volunteers.
- The menu opens in the **airport** (for the first leg) and during the **role window** at the campfire (for every leg after that).
- **Switching roles:** a runner who becomes a chaser **drops every item and their backpack**, then gets the **blowgun** and the **scout gem**. A chaser who becomes a runner **drops the blowgun and gem**, gets a **backpack** if they have none, and gets the leg's **runner item** (and biome item) if they haven't had them yet.
- A chaser carries **one blowgun and one scout gem** at most; a second can't be picked up. The cooldowns belong to the chaser, not the item.
- **Scout statues are switched off**: no revives and no items. The dead come back at the campfire after each leg instead.
- Back in the **airport** the round and roles are cleared, so the airport plays like vanilla PEAK (only the role menu and its hint are there).

---

## Chasers

**Perks**
- **15% faster** than runners, and **6% faster climbing** (walls, ropes, vines).
- **Blowgun** with **unlimited** darts (30 s cooldown, counted down above its hotbar slot). It can't be dropped or thrown. A dart doesn't knock the runner out: it adds **18% drowsiness** and marks them with **flare smoke in their own skin colour** for 7 seconds. Darts don't affect other chasers.
- **Scout gem boost:** chasers carry **Scout's Initiative** (a scout gem) whose own power is never used. Using it gives, **1 second later**, a **2.25-second energy-drink speed boost plus unlimited stamina** (the rainbow stamina bar; no drowsiness). It then has a **1-minute cooldown**, starting when the effects wear off (counted down above its hotbar slot). Each use adds **8% petrification**; each capture takes **5%** away again.
- **Capture rush:** each capture gives a short **+1% speed** boost (5 s; each further capture during the boost adds +0.5%) and a **full morale boost** (extra-stamina bar).
- **Tough:** only **half** of every negative status (injury, cold, poison, drowsiness, …), only **1/3 fall damage** (still scaled by the ascent; a big fall still knocks them down), **immune to fog**, ignored by **mushroom zombies**. *Hunger works the same as for runners.*
- **Clown luggage** is theirs: only chasers can open it, and it's full of food and healing items.
- Their **body glows red** for everyone, so they're easy to tell apart (PEAK's own status glow, like milk's yellow invincibility glow).

**Drawbacks**
- Can only pick up and use **food and healing items** (plus their blowgun), **dynamite**, **mandrakes** and **cactus balls**, and **never energy drinks, lollipops, shroomberries or the Roots fungi** (Bounce, Cloud, Shelf, Warp; the healing Remedy Fungus is allowed).
- Can only open **clown luggage**.
- **Can't see ghosts**, since a ghost floats around the runner it spectates.
- **Can't see runners' name tags.**
- Frozen and **blind** during every head start.
- Can be **frozen** by any runner who looks at them: they **pulse icy blue** so everyone can tell, and they're **blinded** (PEAK's blue-flower blindness) for as long as the freeze lasts.
- A **snowball** thrown by a runner **blinds** a chaser for 2.5 seconds.
- **Can't use snowballs.**

## Runners

**Perks**
- **Freeze chasers** by looking at them (26 m, 10 s; no stacking, 8 s immunity afterwards), but not from inside a safe zone.
- **Head start** at every leg, with a **3-second speed boost** at its end.
- **Safe zones** (15 m) around every campfire.
- **18.5% faster stamina regeneration.**
- A **backpack** at the start of the round, **one random item** at the start of every leg (a snowball, a brown berrynana or a fortified milk), plus a **biome item**: a heat pack in the Alpine, a sports drink in the Caldera, an early worm in the Gloom, aloe vera in the Mesa.
- **Snowballs blind chasers** they hit, and every thrown snowball pushes **10% harder**.
- Runners **can't use the blowgun or the scout gem** (those are chaser items).
- **Regular luggage** can also hold **fortified milk, snowballs, brown berrynanas and energy drinks**, every leg.
- **Fortified milk** protects you from being captured while it's active (milk is twice as heavy and its invincibility is 65% shorter than vanilla).
- First runner into each campfire's safe zone gets a **fortified milk** (not when the chasers won the leg).
- Leg and biome items are dropped at your feet and picked up for you, straight into your hands; if your slots are full they stay on the ground.
- A runner with a chaser closing in hears PEAK's own **Scoutmaster chase music**.

**Drawbacks**
- **Fog, lava and gloom rise 2.5× faster** (starting 5 minutes after the head start); the fog only hurts runners.
- **Energy drinks** last 65% less and make you 1.5× sleepier when they wear off.
- A blowgun dart marks you with smoke and makes you a bit drowsy.
- **Mandrakes** start dropping at your feet late in a leg.

---

## Rules for everyone
- **Removed from the game:** amulets (incl. Scout's Honor), gems, rescue claws, jetpacks/rocket packs, gliders, the golden Bing Bong, world blowguns and hidden test items that never appear in normal PEAK.
- **Dynamite and mandrakes** can be used by everyone.
- **Shroomberries** can be eaten (not by chasers), but their effects only last **1 second**; the hunger they cure is the same as vanilla.
- **No Scoutmaster.** He would hunt the runner who's furthest from the group.
- **No revival curse.** Revived players don't get the curse or extra hunger.

## HUD
- **Role reveal** and **head-start countdown** in the middle of the screen (translucent for runners).
- **Leg result** and the **role window** countdown (with the **-** hint) at the campfire.
- **Chaser list** in the top right, under the ascent label. Frozen chasers show in ice blue with their remaining time; dead chasers are crossed out.
- **Blowgun** and **scout gem** cooldowns in seconds above their hotbar slots.
- A **capture bar** for a runner who is being captured.
- For the host: **PRESS HOTKEY (+) FOR HOST CONTROLS** above their stamina bar.
- Short notices for captures and rewards.

## Host controls
- **+ (the =/+ key, or numpad +): host menu.** A PEAK-style menu with:
  - **Restart at the airport**: everyone goes back to the airport, where PEAK plays like vanilla. The next run starts a fresh round.
  - **Restart at previous campfire**: everyone back to the last lit campfire (or the start), the dead revived, statuses cleared, and a fresh leg begins. Roles are kept.
  - **Teleport everyone to next campfire**: moves every living player to the next unlit campfire.
  - **Debug hotkeys: on/off** (see below).
  
  The two restarts ask you to click again to confirm. Press = or Esc to close.
- **F10:** the same "restart at previous campfire" as a direct key.
- Debug/testing keys (F6 swap own role, F7 start/restart round with new roles, F8 freeze yourself, F9 freeze the player you look at) are **off** by default. Turn them on in the host menu or with `DebugKeys`.

---

## Installation
1. Install with a mod manager (r2modman / Thunderstore Mod Manager). **BepInExPack_PEAK** is installed automatically.
2. **Every player** installs the same version.
3. Host a lobby and start a run. The round starts by itself once everyone has woken up on the shore.

**More players:** works with lobby-size mods such as *PEAK Unlimited* (not tested together yet). Chaser count scales with `RunnersPerChaser`.

---

## Configuration

Settings live in `BepInEx/config/codesycamore.OnTheLookout.cfg` (or the mod manager's **Config editor**). Change them with the game closed.

**Host-synced** settings: only the **host's** values matter; they're sent to everyone when they join. Settings marked *(local)* are personal to each player.

### 1. Modules *(local; needs a restart)*
| Setting | Default | What it does |
|---|---|---|
| `EnableFreeze` | true | Runners freeze chasers by looking at them. |
| `EnableTag` | true | Chasers capture runners by holding interact on them. |
| `EnableSafeZones` | true | Campfires are safe zones. |
| `EnableFog` | true | Faster fog that only hurts runners. |
| `EnableItemRules` | true | Chaser item restrictions and removed items. |
| `EnableRewards` | true | Reward for the first runner into each safe zone. |

### 2. Round
| Setting | Default | What it does |
|---|---|---|
| `RunnersPerChaser` | 4 | At most one chaser per this many runners (1 in 5 players, 2 in 10). Drawn from volunteers; nobody volunteered = one random player. Always leaves a runner. |
| `RoleRevealSeconds` | 4 | How long the role is shown at the start of each leg. |
| `HeadStartSeconds` | 20 | Runner head start (chasers frozen + blind). |
| `AutoStartRound` | true | Start automatically once everyone has woken up. |
| `ChaserPreferenceEnabled` | true | Players choose RUNNER (default) or CHASER in the airport and in the role window; chasers are drawn at random from the volunteers. Off = drawn from everyone. |
| `RoleWindowSeconds` | 10 | After a leg ends: everyone at the campfire, frozen, choosing their role; then the campfire lights itself. |
| `SpawnInteractLockSeconds` | 7 | No interacting on the shore until this long after the round starts. |
| `ChaserSpeedMultiplier` | 1.15 | Chaser movement speed. |
| `ChaserClimbSpeedMultiplier` | 1.06 | Chaser climbing speed (walls, ropes, vines). |
| `CaptureBoostPercent` | 1 | Speed boost (%) after a capture. |
| `CaptureBoostStackPercent` | 0.5 | Extra boost (%) per further capture during the boost. |
| `CaptureBoostSeconds` | 5 | Capture boost duration. |
| `CaptureMoraleBoost` | true | Capturing gives a full morale boost. |
| `ChaserStatusMultiplier` | 0.5 | Fraction of negative statuses chasers take (not hunger). |
| `ChaserFallDamageMultiplier` | 0.333 | Fraction of vanilla fall damage chasers take (ascent-scaled; big falls still knock them down). 0 = none. |
| `ZombiesIgnoreChasers` | true | Mushroom zombies ignore chasers. |
| `ZombiesWhenChasersDead` | true | Zombies hunt runners while no chaser is alive (incl. solo). |
| `ZombieLifetimeSeconds` | 120 | How long each of those zombies lasts. |
| `ZombieSpawnDistance` | 15 | How far from its runner a zombie appears (m). |
| `ZombiesByPlayerCount` | `1:1, 6:2, 10:3` | Zombies per wave by lobby size (`minPlayers:zombies`); each hunts one random runner. |
| `ZombieWaveDelaySeconds` | 120 | Cooldown after a wave is gone (killed or expired) before another can come. |
| `ZombieStartDelaySeconds` | 300 | Zombies only start coming this long after the head start ends (each leg). |
| `MandrakeStartSeconds` | 180 | Mandrakes start dropping at runners this long after the head start. 0 = off. |
| `MandrakeIntervalSeconds` | 60 | Then one at every runner outside a safe zone this often (s). |
| `MandrakeFirstScreamSeconds` | 0.75 | A dropped mandrake first screams this many seconds after it appears. |
| `HeadStartBoostSeconds` | 3 | Runners' speed boost (no drowsiness) at the end of the head start. 0 = off. |
| `DisableScoutStatues` | true | Scout statues do nothing during a round. |
| `PeakChasersDie` | true | When every living runner reaches the peak, the chasers are brought up there and die. |
| `RunnerStaminaRegenMultiplier` | 1.185 | Runner stamina regeneration. |
| `RunnerBackpacks` | true | Runners get a backpack at round start. |
| `RunnerLegItems` | `Snowball, Brown Berrynana, Fortified Milk` | One random item per runner each leg. Empty = off. |
| `RunnerBiomeItems` | `Alpine:Heat Pack, Volcano:Sports Drink, Swamp:EarlyWorm, Mesa:Aloe Vera` | Extra item per runner at the start of a leg in that biome (Caldera = Volcano, Gloom = Swamp). |
| `FortifiedMilkWeightMultiplier` | 2 | Fortified milk weight vs. vanilla. |
| `FortifiedMilkInvincibilityMultiplier` | 0.35 | Fortified milk invincibility vs. vanilla (65% shorter). |
| `CampfireFoodItems` | `Marshmallow, Glizzy` | Food laid out (one per player) when a leg ends. `Glizzy` is the hot dog. |
| `ClearStatusesAtCampfire` | true | Lighting a campfire clears negative statuses nearby. |
| `NoReviveCurse` | true | No curse/hunger after being revived. |
| `EnergyDrinkDrowsyMultiplier` | 1.5 | Energy drink drowsiness when it wears off. |
| `EnergyDrinkDurationMultiplier` | 0.35 | Energy drink speed boost duration vs. vanilla (65% shorter). |
| `DisableScoutmaster` | true | No Scoutmaster. |

### 2b. Blowgun
| Setting | Default | What it does |
|---|---|---|
| `ChaserBlowgun` | true | Chasers get an unlimited blowgun. |
| `BlowgunCooldownSeconds` | 30 | Time between shots. |
| `TrackingSmokeSeconds` | 7 | How long the smoke follows a darted runner. |
| `BlowdartDrowsy` | 0.18 | Drowsiness a dart adds (0.18 = 18%). |
| `ChaserGem` | true | Chasers carry a reusable scout gem that gives a speed boost instead of its own power. |
| `ChaserGemItem` | `Amulet_SuperJump` | The gem item (Amulet_SuperJump = Scout's Initiative). |
| `GemBoostSeconds` | 2.25 | Gem effect length: speed boost + unlimited stamina (s). |
| `GemDelaySeconds` | 1 | The gem's effects start this long after use (s). |
| `GemInfiniteStamina` | true | The gem also gives unlimited stamina (rainbow bar). |
| `GemCooldownSeconds` | 60 | Gem cooldown; starts when its effects wear off. |
| `GemPetrify` | 0.08 | Petrification per gem use (0.08 = 8%). |
| `SnowballKnockbackBonus` | 0.1 | Thrown snowballs push this much harder (0.1 = 10%). |
| `CapturePetrifyRelief` | 0.05 | Petrification a capture takes away (0.05 = 5%). |
| `SnowballBlindSeconds` | 2.5 | A runner's snowball blinds the chaser it hits this long. 0 = off. |

### 2c. Chase effects
| Setting | Default | What it does |
|---|---|---|
| `ScoutmasterChaseMusic` | true | Runners hear PEAK's Scoutmaster chase music when a chaser is close (under 50 m, louder under 25 m). |

### 3. Freeze
| Setting | Default | What it does |
|---|---|---|
| `FreezeRange` | 26 | Max distance (m) a runner can freeze a chaser from. |
| `FreezeConeDegrees` | 15 | How precisely the runner must look at the chaser. |
| `FreezeDuration` | 10 | Freeze length (doesn't stack or extend). |
| `FreezeCooldownSeconds` | 8 | Immunity after a freeze ends. |
| `FreezeHoldGrip` | true | Frozen while climbing: keep holding on. |
| `FreezeLockStamina` | true | Stamina doesn't drain while frozen. |
| `FreezeBlindsChasers` | true | A frozen chaser is also blinded while the freeze lasts. |
| `FreezeSuspendInAir` | true | Frozen mid-jump: hang in the air. |
| `FreezeSuspendStiffness` | 10 | How firmly a mid-air chaser is held. |
| `FreezeZeroVelocity` | false | Extra anti-slide while frozen. |
| `FreezeBlockLook` | false | Also block looking around while frozen. |
| `FreezePulseFastInterval` / `SlowInterval` | 0.15 / 0.9 | Icy pulse rate at the start / end of a freeze. |

### 4. Tag
| Setting | Default | What it does |
|---|---|---|
| `CaptureHoldSeconds` | 0.4 | How long a chaser holds interact on a runner to capture them (the runner sees it too). |
| `TagMaxDistance` | 5 | Lag tolerance: the host rejects captures from further away. |
| `TagPassedOutRunners` | true | Passed-out runners can be captured. |
| `MilkProtectsFromCapture` | true | Fortified milk protects from capture. |

### 5. Safe zones
| Setting | Default | What it does |
|---|---|---|
| `CampfireSafeRadius` | 15 | Safe zone radius (m): no captures, no freezing from inside; also when a leg counts as complete. |
| `SafeZoneRequiresLit` | false | Only lit campfires are safe. |

### 6. Fog
| Setting | Default | What it does |
|---|---|---|
| `FogSpeedMultiplier` | 2.5 | Fog, rising lava and rising gloom speed during a round. |
| `FogStartDelaySeconds` | 300 | Fog, lava and gloom start rising this long after the head start ends (each leg). |

### 7. Items
| Setting | Default | What it does |
|---|---|---|
| `ChaserAutoAllowHealing` | true | Chasers can use healing items. |
| `ChaserAutoAllowFood` | true | Chasers can eat food. |
| `ChaserAllowedItems` | `Remedy Fungus, Cactus, Dynamite, Mandrake` | Extra items chasers may always use (names, comma separated). |
| `ChaserForbiddenItems` | `Snowball, Energy Drink, Big Lollipop, Bounce Fungus, Cloud Fungus, Shelf Fungus, Warp Fungus, Blue Shroomberry, Green Shroomberry, Purple Shroomberry, Red Shroomberry, Yellow Shroomberry` | Items chasers can never use, even if they are food or healing. |
| `ClownLuggageChasersOnly` | true | Only chasers open clown luggage (food/healing inside). |
| `ChasersOnlyOpenClownLuggage` | true | Chasers can't open other luggage. |
| `LuggageExtraItems` | `Fortified Milk, Snowball, Brown Berrynana, Energy Drink` | Items that can also come out of regular luggage during a round. |
| `LuggageExtraChance` | 0.383 | Chance for each item a regular luggage spawns to be one of those (split evenly: about 9.6% each). |
| `BanAmulets` / `BanGems` / `BanRescueClaws` | true | Remove these items. |
| `BanBlowguns` | true | No blowguns in the world (chasers still get theirs). |
| `AllowJetpacks` / `AllowGliders` | false | Turn on to allow them again. |
| `BanHiddenItems` | true | Remove items that never spawn in normal PEAK. |
| `AllowedHiddenItems` | `Napberry, Kingberry, Clusterberry, Shroomberry, Cactus` | Never treated as hidden (name contains, so all colours count). |
| `BannedItems` | `Weird Shroom` | Extra items to remove. |
| `BanGoldenBingBong` | true | Remove the golden Bing Bong and switch off its invincibility shield. |
| `ShroomberryEffectSeconds` | 1 | How long a shroomberry's effects last (hunger cure unchanged). |

### 9. Rewards
| Setting | Default | What it does |
|---|---|---|
| `RewardItems` | `Fortified Milk` | Reward for the first runner into each safe zone. |
| `RewardItemCount` | 1 | How many. |

### 10–13. Network, UI, Debug, Admin
| Setting | Default | What it does |
|---|---|---|
| `KickPlayersWithoutMod` *(local, host)* | false | Kick players with a missing/different mod version. |
| `ChasersSeeGhosts` | false | Let chasers see ghosts. |
| `ChasersSeeRunnerNames` | false | Let chasers see the name tags above runners. |
| `ChaserRedOutline` | true | Chasers' bodies glow red for everyone. |
| `ChaserGlowIntensity` | 0.8 | Strength of that glow (vanilla invincibility = 1). |
| `ShowChaserList` *(local)* | true | Chaser list in the top right. |
| `FreezeScreenFrost` *(local)* | true | Frost on your screen while you're frozen. |
| `CountdownOpacity` *(local)* | 0.35 | Opacity of the runners' countdown. |
| `CaptureSound` *(local)* | true | Explosion sound on capture. |
| `DebugKeys` *(local)* | false | Host testing keys F6–F9. |
| `LogLookChecks` *(local)* | false | Log every freeze look check. |
| `AdminKeys` *(local)* | true | Host admin keys. |
| `KeyRestartFromCampfire` *(local)* | F10 | Quick-restart key. |
| `KeyHostMenu` *(local)* | = | Opens the host menu (the =/+ key; numpad + works too). |
| `KeyChaserOdds` *(local)* | - | Opens the role menu (airport, and the role window at the campfire). |

**Item names:** item settings accept PEAK's display or internal names (spaces and case don't matter). The log (`BepInEx/LogOutput.log`) lists every item name once per session, under `[OTL][Items] all items`.

---

## Notes
- Built for **PEAK v2.6.b** (build 25739797). A PEAK update can break mods; if a part of this mod can't hook into the game after an update, only that part switches off and the log says which (`[OTL]` lines).
- Bug reports: please include your `BepInEx/LogOutput.log`.

## Credits
Made by codesycamore. Inspired by the hide-and-seek idea of *Hide and PEAK* by glarmer (no code reused). Built on BepInEx and HarmonyX with the PEAK modding community's project template.

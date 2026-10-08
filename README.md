# On The Lookout

**Chasers vs Runners for PEAK.** A few scouts are secretly chosen as **chasers** and hunt everyone else up the mountain. **Runners** have to make it from campfire to campfire, and finally to the peak, without getting caught. A runner's best defence is to turn around and **look at a chaser** to freeze them in place.

> **New in 1.2.0:** lighting a campfire freezes everyone while the next biome's title plays, then roles are shown and runners get their head start; if every runner is caught the chasers move to the next campfire and the statue there adds an extra chaser; if no chaser is alive (or you play solo) mushroom zombies hunt the runners, with an alarm; 30 m safe zones you can't freeze from; a host menu on the **=** key; shroomberries edible with 1-second effects; and fixes for items that couldn't be picked up. See the changelog.

> **Everyone in the lobby needs this mod** (same version). The host runs the game: roles, freezes, captures and rewards are all decided by the host and synced to everyone.

---

## How a game works

1. **Wake up on the shore.** Nothing can be interacted with while everyone wakes up, and for a few seconds after (no grabbing items before the roles are out).
2. **Roles are revealed.** Each player sees **CHASER** (red) or **RUNNER** (yellow) in the middle of the screen. The number of chasers depends on lobby size (1 chaser, 2 from 6 players, configurable). Everyone is frozen during the reveal.
3. **Head start.** Runners are released and get a 20-second head start with a big countdown. Chasers stay **frozen and blind** the whole time; their screen fades back in near the end.
4. **The chase.** Chasers hunt the runners up the current biome.
   - A chaser **captures** a runner by **running into them**. The runner dies with a bang.
   - A runner **freezes** a chaser by **looking at them** within 26 m, from **outside** a safe zone. The chaser can't move, act or fall for 6.5 seconds, then is immune to freezing for 8 seconds.
   - If **no chaser is alive** (all dead, or none at all, e.g. a host playing solo is a runner), the mountain takes over: from **5 minutes after the head start ends**, **mushroom zombies** are sent, each after one random runner who isn't in a safe zone yet (1 zombie per wave, more in bigger lobbies). Each lasts 2 minutes. When a wave is gone (killed or expired) there's a **2-minute cooldown** before the next one can come, until the runners reach the campfire or a chaser is back. Every spawn is announced to all players with an alarm and a screen shake, and all zombies vanish the moment every runner is safe (or every runner is caught).
5. **Reach the campfire.** Within **30 m of a campfire** is a **safe zone**: no captures there, and runners inside can't freeze chasers. When **every living runner** is in the next campfire's safe zone, the chase for that leg is over: everyone sees *ALL RUNNERS ARE SAFE*, the chasers are brought to the campfire, and food is laid out so everyone gets something to eat.
6. **Light the campfire.** Only a **runner** can light it. Lighting clears every negative status of everyone at the fire and **freezes everyone** while the next biome's title plays. Then the next leg starts: role reveal → runners released for their head start → chasers frozen and blind until it ends.
7. **Win.**
   - **Runners win** when a runner reaches **the peak**.
   - **Chasers win** when **every runner is dead** and there's no campfire left to go to (the last stretch).
   - **Caught them all?** If every runner dies earlier (passing out doesn't count), the chasers are sent to the **next campfire**. The **scout statue** there revives everyone, and **one extra** revived runner becomes a chaser on top of the usual conversion (at least one runner always remains).

### Captures, ghosts and new chasers
- Captured runners (and runners who die any other way) become **ghosts**. Passing out is **not** death: teammates can still revive or carry a passed-out runner.
- **Roles last for the whole run.** A chaser who dies is still a chaser when revived, and so is anyone who became a chaser. Roles are only re-rolled when a new run starts from the airport.
- Using a **scout statue** revives everyone who is dead, as in vanilla, but **one revived ghost is randomly turned into an extra chaser** and gets a blowgun. With no ghosts, the statue works as normal.

---

## Chasers

**Perks**
- **15% faster** than runners, and **6% faster climbing** (walls, ropes, vines).
- **Blowgun** with **unlimited** darts (30 s cooldown, counted down above its hotbar slot). A dart doesn't knock the runner out: it adds **10% drowsiness** and marks them with **flare smoke in their own skin colour** for 5 seconds, so everyone can see where they are.
- **Capture rush:** each capture gives a short **+1% speed** boost (5 s; each further capture during the boost adds +0.5%) and a **full morale boost** (extra-stamina bar).
- **Tough:** only **1/3** of every negative status (injury, cold, poison, drowsiness, …), only **1/4 fall damage** (still scaled by the ascent), **immune to fog**, ignored by **mushroom zombies**. *Hunger works the same as for runners.*
- **Clown luggage** is theirs: only chasers can open it, and it's full of food and healing items.

**Drawbacks**
- Can only pick up and use **food and healing items** (plus their blowgun), and **never energy drinks, lollipops, shroomberries or the Roots fungi** (Bounce, Cloud, Shelf, Warp; the healing Remedy Fungus is allowed).
- Can only open **clown luggage** (scout statues still work).
- **Can't light campfires.**
- **Can't see ghosts**, since a ghost floats around the runner it spectates.
- Frozen and **blind** during every head start.
- Can be **frozen** by any runner who looks at them, and they **pulse icy blue** while frozen so everyone can tell.
- **Fireworks** go off above every chaser every 30 seconds of the chase, and they sound like the **Scoutmaster** when they're close to a runner. Runners always have a clue where they are.

## Runners

**Perks**
- **Freeze chasers** by looking at them (26 m, 6.5 s; no stacking, 8 s immunity afterwards), but not from inside a safe zone.
- **Head start** at every leg.
- **Safe zones** (30 m) around every campfire.
- **12% faster stamina regeneration.**
- A **backpack** at the start of the round, and **one random item** at the start of every leg: a snowball, a brown berrynana or a fortified milk.
- **Fortified milk** protects you from being captured while it's active.
- First runner into each campfire's safe zone gets an **energy drink**.

**Drawbacks**
- **Fog rises 1.5× faster** (and starts sooner), and only hurts runners.
- **Energy drinks** make you 1.5× sleepier when they wear off.
- A blowgun dart marks you with smoke and makes you a bit drowsy.

---

## Rules for everyone
- **Removed from the game:** amulets (incl. Scout's Honor), gems, rescue claws, jetpacks/rocket packs, gliders, world blowguns and hidden test items that never appear in normal PEAK.
- **Shroomberries** can be eaten (not by chasers), but their effects only last **1 second**; the hunger they cure is the same as vanilla.
- **No Scoutmaster.** He would hunt the runner who's furthest from the group.
- **No revival curse.** Revived players don't get the curse or extra hunger.

## HUD
- **Role reveal** and **head-start countdown** in the middle of the screen (translucent for runners).
- **Chaser list** in the top right, under the ascent label. Frozen chasers show in ice blue with their remaining time; dead chasers are crossed out.
- **Blowgun cooldown** in seconds above the blowgun's hotbar slot.
- Short notices for captures, new chasers and rewards.

## Host controls
- **= (equals): host menu.** A PEAK-style menu with:
  - **Restart at the airport**: everyone goes back to the airport. The next run rolls new roles.
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

**More players:** works with lobby-size mods such as *PEAK Unlimited* (not tested together yet). Chaser count scales with `ChasersByPlayerCount`.

---

## Configuration

Settings live in `BepInEx/config/codesycamore.OnTheLookout.cfg` (or the mod manager's **Config editor**). Change them with the game closed.

**Host-synced** settings: only the **host's** values matter; they're sent to everyone when they join. Settings marked *(local)* are personal to each player.

### 1. Modules *(local; needs a restart)*
| Setting | Default | What it does |
|---|---|---|
| `EnableFreeze` | true | Runners freeze chasers by looking at them. |
| `EnableTag` | true | Chasers capture runners by running into them. |
| `EnableSafeZones` | true | Campfires are safe zones. |
| `EnableFog` | true | Faster fog that only hurts runners. |
| `EnableItemRules` | true | Chaser item restrictions and removed items. |
| `EnableConversion` | true | Scout statues turn a ghost into a chaser. |
| `EnableRewards` | true | Reward for the first runner into each safe zone. |

### 2. Round
| Setting | Default | What it does |
|---|---|---|
| `ChasersByPlayerCount` | `1:1, 6:2` | Chasers per lobby size as `minPlayers:chasers` (add e.g. `, 10:3`). Always leaves at least one runner. |
| `RoleRevealSeconds` | 4 | How long the role is shown at the start of each leg. |
| `HeadStartSeconds` | 20 | Runner head start (chasers frozen + blind). |
| `AutoStartRound` | true | Start automatically once everyone has woken up. |
| `BiomeTitleSeconds` | 7.5 | After lighting a campfire, the next leg starts this long after the biome title appears. |
| `NoTitleFallbackSeconds` | 12 | …or this long after lighting if no title shows. |
| `SpawnInteractLockSeconds` | 7 | No interacting on the shore until this long after the round starts. |
| `ChaserSpeedMultiplier` | 1.15 | Chaser movement speed. |
| `ChaserClimbSpeedMultiplier` | 1.06 | Chaser climbing speed (walls, ropes, vines). |
| `CaptureBoostPercent` | 1 | Speed boost (%) after a capture. |
| `CaptureBoostStackPercent` | 0.5 | Extra boost (%) per further capture during the boost. |
| `CaptureBoostSeconds` | 5 | Capture boost duration. |
| `CaptureMoraleBoost` | true | Capturing gives a full morale boost. |
| `ChaserStatusMultiplier` | 0.333 | Fraction of negative statuses chasers take (not hunger). |
| `ChaserFallDamageMultiplier` | 0.25 | Fraction of vanilla fall damage chasers take (ascent-scaled). 0 = none. |
| `ZombiesIgnoreChasers` | true | Mushroom zombies ignore chasers. |
| `ZombiesWhenChasersDead` | true | Zombies hunt runners while no chaser is alive (incl. solo). |
| `ZombieLifetimeSeconds` | 120 | How long each of those zombies lasts. |
| `ZombieSpawnDistance` | 15 | How far from its runner a zombie appears (m). |
| `ZombiesByPlayerCount` | `1:1, 6:2, 10:3` | Zombies per wave by lobby size (`minPlayers:zombies`); each hunts one random runner. |
| `ZombieWaveDelaySeconds` | 120 | Cooldown after a wave is gone (killed or expired) before another can come. |
| `ZombieStartDelaySeconds` | 300 | Zombies only start coming this long after the head start ends (each leg). |
| `TeleportChasersOnLegComplete` | true | Bring chasers to the campfire when the runners are all safe. |
| `RunnerStaminaRegenMultiplier` | 1.12 | Runner stamina regeneration. |
| `RunnerBackpacks` | true | Runners get a backpack at round start. |
| `RunnerLegItems` | `Snowball, Brown Berrynana, Fortified Milk` | One random item per runner each leg. Empty = off. |
| `CampfireFoodItems` | `Marshmallow, Glizzy` | Food laid out (one per player) when a leg ends. `Glizzy` is the hot dog. |
| `ClearStatusesAtCampfire` | true | Lighting a campfire clears negative statuses nearby. |
| `NoReviveCurse` | true | No curse/hunger after being revived. |
| `EnergyDrinkDrowsyMultiplier` | 1.5 | Energy drink drowsiness when it wears off. |
| `DisableScoutmaster` | true | No Scoutmaster. |

### 2b. Blowgun
| Setting | Default | What it does |
|---|---|---|
| `ChaserBlowgun` | true | Chasers get an unlimited blowgun. |
| `BlowgunCooldownSeconds` | 30 | Time between shots. |
| `TrackingSmokeSeconds` | 5 | How long the smoke follows a darted runner. |
| `BlowdartDrowsy` | 0.1 | Drowsiness a dart adds (0.1 = 10%). |

### 2c. Chase effects
| Setting | Default | What it does |
|---|---|---|
| `FireworkIntervalSeconds` | 30 | Firework above each chaser every N seconds of chase. 0 = off. |
| `FireworkHeight` | 8 | Firework height above the chaser (m). |
| `ScoutmasterSounds` | true | Chasers make Scoutmaster sounds near runners. |
| `ScoutmasterSoundMinInterval` / `MaxInterval` | 3 / 6 | Random gap between those sounds (s). |

### 3. Freeze
| Setting | Default | What it does |
|---|---|---|
| `FreezeRange` | 26 | Max distance (m) a runner can freeze a chaser from. |
| `FreezeConeDegrees` | 15 | How precisely the runner must look at the chaser. |
| `FreezeDuration` | 6.5 | Freeze length (doesn't stack or extend). |
| `FreezeCooldownSeconds` | 8 | Immunity after a freeze ends. |
| `FreezeHoldGrip` | true | Frozen while climbing: keep holding on. |
| `FreezeLockStamina` | true | Stamina doesn't drain while frozen. |
| `FreezeSuspendInAir` | true | Frozen mid-jump: hang in the air. |
| `FreezeSuspendStiffness` | 10 | How firmly a mid-air chaser is held. |
| `FreezeZeroVelocity` | false | Extra anti-slide while frozen. |
| `FreezeBlockLook` | false | Also block looking around while frozen. |
| `FreezePulseFastInterval` / `SlowInterval` | 0.15 / 0.9 | Icy pulse rate at the start / end of a freeze. |

### 4. Tag
| Setting | Default | What it does |
|---|---|---|
| `TagMaxDistance` | 4 | Lag tolerance: the host rejects captures from further away. |
| `TagPassedOutRunners` | true | Passed-out runners can be captured. |
| `MilkProtectsFromCapture` | true | Fortified milk protects from capture. |

### 5. Safe zones
| Setting | Default | What it does |
|---|---|---|
| `CampfireSafeRadius` | 30 | Safe zone radius (m): no captures, no freezing from inside; also when a leg counts as complete. |
| `SafeZoneRequiresLit` | false | Only lit campfires are safe. |

### 6. Fog
| Setting | Default | What it does |
|---|---|---|
| `FogSpeedMultiplier` | 1.5 | Fog speed (and how fast it starts) during a round. |
| `FogIgnoresChasers` | true | Lagging chasers don't hold the fog back. |

### 7. Items
| Setting | Default | What it does |
|---|---|---|
| `ChaserAutoAllowHealing` | true | Chasers can use healing items. |
| `ChaserAutoAllowFood` | true | Chasers can eat food. |
| `ChaserAllowedItems` | `Remedy Fungus` | Extra items chasers may always use (names, comma separated). |
| `ChaserForbiddenItems` | `Energy Drink, Big Lollipop, Bounce Fungus, Cloud Fungus, Shelf Fungus, Warp Fungus, Blue Shroomberry, Green Shroomberry, Purple Shroomberry, Red Shroomberry, Yellow Shroomberry` | Items chasers can never use, even if they are food or healing. |
| `ClownLuggageChasersOnly` | true | Only chasers open clown luggage (food/healing inside). |
| `ChasersOnlyOpenClownLuggage` | true | Chasers can't open other luggage. |
| `BanAmulets` / `BanGems` / `BanRescueClaws` | true | Remove these items. |
| `BanBlowguns` | true | No blowguns in the world (chasers still get theirs). |
| `AllowJetpacks` / `AllowGliders` | false | Turn on to allow them again. |
| `BanHiddenItems` | true | Remove items that never spawn in normal PEAK. |
| `AllowedHiddenItems` | `Napberry, Kingberry, Clusterberry, Shroomberry` | Never treated as hidden (name contains, so all colours count). |
| `BannedItems` | `Weird Shroom` | Extra items to remove. |
| `ShroomberryEffectSeconds` | 1 | How long a shroomberry's effects last (hunger cure unchanged). |

### 8. Conversion
| Setting | Default | What it does |
|---|---|---|
| `GhostsConvertedPerStatue` | 1 | Revived ghosts that become chasers at a statue. |
| `WipeExtraConversions` | 1 | Extra conversions at the statue after every runner died. |
| `ReviveDeadChasers` | true | Statues also revive dead chasers. |

### 9. Rewards
| Setting | Default | What it does |
|---|---|---|
| `RewardItems` | `Energy Drink` | Reward for the first runner into each safe zone. |
| `RewardItemCount` | 1 | How many. |

### 10–13. Network, UI, Debug, Admin
| Setting | Default | What it does |
|---|---|---|
| `KickPlayersWithoutMod` *(local, host)* | false | Kick players with a missing/different mod version. |
| `ChasersSeeGhosts` | false | Let chasers see ghosts. |
| `ShowChaserList` *(local)* | true | Chaser list in the top right. |
| `FreezeScreenFrost` *(local)* | true | Frost on your screen while you're frozen. |
| `CountdownOpacity` *(local)* | 0.35 | Opacity of the runners' countdown. |
| `CaptureSound` *(local)* | true | Explosion sound on capture. |
| `DebugKeys` *(local)* | false | Host testing keys F6–F9. |
| `LogLookChecks` *(local)* | false | Log every freeze look check. |
| `AdminKeys` *(local)* | true | Host admin keys. |
| `KeyRestartFromCampfire` *(local)* | F10 | Quick-restart key. |
| `KeyHostMenu` *(local)* | = | Opens the host menu. |

**Item names:** item settings accept PEAK's display or internal names (spaces and case don't matter). The log (`BepInEx/LogOutput.log`) lists every item name once per session, under `[OTL][Items] all items`.

---

## Notes
- Built for **PEAK v2.6.b** (build 25739797). A PEAK update can break mods; if a part of this mod can't hook into the game after an update, only that part switches off and the log says which (`[OTL]` lines).
- Bug reports: please include your `BepInEx/LogOutput.log`.

## Credits
Made by codesycamore. Inspired by the hide-and-seek idea of *Hide and PEAK* by glarmer (no code reused). Built on BepInEx and HarmonyX with the PEAK modding community's project template.

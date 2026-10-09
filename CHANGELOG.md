# Changelog

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

# Changelog

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

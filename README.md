# On The Lookout

**Chasers vs Runners for PEAK.** Every leg, a few scouts become **chasers** and hunt everyone else up the mountain. **Runners** have to make it from campfire to campfire, and finally to the peak, without getting caught. A runner's best defence is to turn around and **look at a chaser** to freeze them in place.

> **New in 1.8.3:** mandrakes come **every 45 seconds**, vanish after **10 s**, only drop in the first two biomes, never make runners drowsy and make chasers 10% less drowsy than vanilla; the explosion sounds are off for now; **everyone's petrification is cleared after each leg**; the blowgun no longer marks runners with smoke but makes them **20% drowsy**, takes **1.65×** as long to fire and has a **72 s** cooldown; each capture makes that chaser's gem boost **1.7% stronger** for the rest of the leg, and the capture rush no longer adds up; the chaser gem costs **5%** petrification and reveals runners for **3 s**; the runner dash goes **7 m**, **lifts you up** (half of Scout's Initiative's jump), **cancels 80% of a fast fall** and reveals chasers for **5 s**. **1.8.2:** mandrakes start dropping **1 minute** after the head start and come **every 20 seconds**, each with a **dynamite boom** at the runner; using the **chaser gem** makes every runner give off **flare smoke for 5 s**, and a **runner dash** makes every chaser smoke for **3 s**. **1.8.1:** every ability cooldown is refreshed at the start of each leg; hunger grows **30% slower**, and not at all for runners in a safe zone; the runner dash gives **1 s without fall damage**, starting 0.5 s after the dash; energy drinks **last as long as in vanilla** again (still 1.5× sleepier afterwards); chasers can never get energy drinks or lollipops (not even from clown luggage); a runner's snowball reliably pushes **10% harder** and **blinds a chaser for 3 s**; the head-start countdown is in PEAK yellow; and the host hint is smaller. **1.8.0:** the **fog, lava and gloom are back to vanilla** (PEAK's own timing and speed); captures can't be blocked by a runner's backpack, thorns or arrows and keep going while the runner moves; chasers can only capture runners (no carrying, no opening their packs); each capture gives the chaser an **energy drink + lollipop rush** (4 s, +3 s per further capture); gem cooldowns **20 s** (chaser) and **32 s** (runner dash) for **3%** petrification; freezes last **8 s**; the chasers' red glow now shows like milk's glow; and everyone is checked to be at the campfire before each leg starts. **1.7.2:** gems put themselves away after use, and runners can interact as soon as their head start begins. **1.7.1:** the runner dash goes **4.3 m** for **5%** petrification, the head-start boost lasts **4 s**, and the mandrake timings are fixed. **1.7.0:** runners get a **dash gem** (Scout's Ambition: 3.2 m dash, mid-air float, 70 s cooldown), captures need the chaser **practically touching** the runner (0.45 s hold, works from any side), the gems never use their original powers, and **only 11 settings can be changed**; every update resets the config to the new defaults. **1.6.0:** chasers carry **Scout's Initiative** (a scout gem) instead of the napberry for their boost, captures take **0.75 s** and the runner sees the capture bar, milk-protected runners can't be captured, regular luggage holds more runner items (milk, snowballs, brown berrynanas, energy drinks), snowballs push harder and are runner-only, role choices are re-sent so none get lost, and the host sees a hint for the host menu (**+**). **1.5.1:** mandrakes scream 0.75 s after appearing. **1.5.0:** a new role every leg (volunteer as chaser in the **-** menu at the campfire), captures by **holding interact** on a runner, the campfire lights itself after a 10-second role window, scout statues are off, chasers carry a reusable **napberry boost**, snowballs blind chasers, mandrakes start dropping at runners late in a leg, and plenty of balance changes. See the changelog.

> **Everyone in the lobby needs this mod** (same version). The host runs the game: roles, freezes, captures and rewards are all decided by the host and synced to everyone.

---

## How a game works

0. **In the airport**, press **-** to choose **RUNNER** (pre-selected) or **CHASER** for the first leg. Chasers are drawn at random from the players who chose CHASER.
1. **Wake up on the shore.** Nobody can move or interact until **everyone** has woken up and loaded in, and interacting stays locked through the role reveal (no grabbing items before the roles are out). Runners can interact the moment their head start begins.
2. **Roles are drawn and revealed.** Each player sees **CHASER** (red) or **RUNNER** (yellow) in the middle of the screen. Everyone is frozen during the reveal. Chasers are picked at random from the airport volunteers, **one chaser for every 4 runners** at most; if nobody volunteered, **one random player** is the chaser.
3. **Head start.** Runners are released and get a 20-second head start with a big countdown, and a **4-second speed boost** at the end of it. Chasers stay **frozen and blind** the whole time; their screen fades back in near the end.
4. **The chase.** Chasers hunt the runners up the current biome.
   - A chaser **captures** a runner by looking at them and **holding interact for 0.45 seconds**, the same way a hungry scout eats another one. The chaser has to be **practically touching** the runner (1.4 m), and then it works **whichever way the runner faces** with the cursor anywhere on them; their backpack, fanny pack or stuck thorns, arrows and cactus balls never get in the way. Once started, the hold **keeps going while the runner moves**, unless they get clearly out of reach, drink fortified milk or reach a safe zone. Chasers can't do anything else to a runner: no carrying them, no opening their backpack or fanny pack, no pulling thorns or arrows out of them. The runner sees the same progress bar under their crosshair, so they know how close it is. A runner under **fortified milk** can't be captured at all. The runner dies with a bang.
   - A runner **freezes** a chaser by **looking at them** within 26 m, from **outside** a safe zone. The chaser can't move, act or fall and is **blinded** for 8 seconds, then is immune to freezing for 8 seconds.
   - The **fog**, the rising **lava** in the Caldera and the rising **gloom** work as in vanilla (PEAK's own start timing and speed). The fog never hurts chasers.
   - From **1 minute** after the head start, a **mandrake** is dropped at every runner outside a safe zone **every 45 seconds**, in the **first two biomes** only. Mandrakes disappear **10 s** after they appear, never make runners drowsy, and make chasers only **90%** as drowsy as in vanilla.
   - If **no chaser is alive** (all dead, or none at all, e.g. a host playing solo), **mushroom zombies** hunt the runners from 5 minutes after the head start (one wave at a time, 2 minutes each, 2-minute cooldown between waves).
5. **The leg is won.**
   - **Runners win the leg** when **every living runner** is in the next campfire's **safe zone** (15 m around it: no captures there, and runners inside can't freeze chasers).
   - **Chasers win the leg** when **every runner is dead** (passing out doesn't count).
6. **Role window.** Everyone, the dead included, is brought next to the campfire (within 5 m; anyone who didn't make it is brought there once more before roles are drawn) and frozen for **10 seconds**. Press **-** to choose **RUNNER** (the default) or **CHASER** for the next leg. Food is laid out so everyone gets something to eat.
7. **Next leg.** The campfire lights itself, clearing everyone's negative statuses, and new roles are drawn: chasers are picked at random from the players who chose CHASER, **one chaser for every 4 runners** at most. If nobody volunteered, one random player is the chaser. Then the reveal and head start start again.
8. **Win the game.**
   - **Runners win** when **every living runner** reaches **the peak**. The chasers are brought up there and **die**.
   - **Chasers win** when every runner is dead on the last stretch, with no campfire left to go to.

### Roles and choices
- Your choice in the **-** menu is private: only the host's game receives it. It stays until you change it, and resets to RUNNER when a run ends and everyone is back in the airport. There are no odds: the chasers are a random pick from the volunteers.
- The menu opens in the **airport** (for the first leg) and during the **role window** at the campfire (for every leg after that).
- **Switching roles:** a runner who becomes a chaser **drops every item and their backpack**, then gets the **blowgun** and the **chasers' scout gem**. A chaser who becomes a runner **drops the blowgun and gem**, gets the **runners' dash gem**, gets a **backpack** if they have none, and gets the leg's **runner item** (and biome item) if they haven't had them yet.
- A chaser carries **one blowgun and one scout gem** at most; a second can't be picked up. The cooldowns belong to the chaser, not the item.
- **Scout statues are switched off**: no revives and no items. The dead come back at the campfire after each leg instead.
- Back in the **airport** the round and roles are cleared, so the airport plays like vanilla PEAK (only the role menu and its hint are there).

---

## Chasers

**Perks**
- **15% faster** than runners, and **6% faster climbing** (walls, ropes, vines).
- **Blowgun** with **unlimited** darts (**72 s** cooldown, counted down above its hotbar slot). It can't be dropped or thrown, and takes **1.65×** as long to fire as in vanilla. A dart doesn't knock the runner out: it adds **20% drowsiness**. Darts don't affect other chasers.
- **Scout gem boost:** chasers carry **Scout's Initiative** (a scout gem) whose own power is never used. Using it gives, **1 second later**, a **2.25-second energy-drink speed boost plus unlimited stamina** (the rainbow stamina bar; no drowsiness), and the gem goes back to its slot so your hands are free. It then has a **20-second cooldown**, starting when the effects wear off (counted down above its hotbar slot). Each use adds **5% petrification**; each capture takes **5%** away again, and makes the gem's speed boost **1.7% stronger** (not longer) for the rest of the leg. Using it makes **every runner give off flare smoke** in their own colour for **3 s**.
- **Capture rush:** each capture gives the chaser an **energy drink + lollipop** at once for **4 seconds** (speed boost and unlimited stamina, no drowsiness afterwards); a further capture while it runs starts the 4 seconds over (it doesn't add up).
- **Tough:** only **half** of every negative status (injury, cold, poison, drowsiness, …), only **1/3 fall damage** (still scaled by the ascent; a big fall still knocks them down), **immune to fog**, ignored by **mushroom zombies**. *Hunger works the same as for everyone.*
- **Clown luggage** is theirs: only chasers can open it, and it's full of food and healing items.
- Their **body glows red** for everyone, so they're easy to tell apart (PEAK's own status glow, like milk's yellow invincibility glow).

**Drawbacks**
- Can only pick up and use **food and healing items** (plus their blowgun), **dynamite**, **mandrakes** and **cactus balls**, and **never energy drinks, lollipops, shroomberries or the Roots fungi** (Bounce, Cloud, Shelf, Warp; the healing Remedy Fungus is allowed).
- Can only open **clown luggage**.
- **Can't see ghosts**, since a ghost floats around the runner it spectates.
- **Can't see runners' name tags.**
- Frozen and **blind** during every head start.
- Can be **frozen** by any runner who looks at them: they **pulse icy blue** so everyone can tell, and they're **blinded** (PEAK's blue-flower blindness) for as long as the freeze lasts.
- A **snowball** thrown by a runner **blinds** a chaser for 3 seconds (and pushes them 10% harder).
- **Can't use snowballs.**

## Runners

**Perks**
- **Freeze chasers** by looking at them (26 m, 8 s; no stacking, 8 s immunity afterwards), but not from inside a safe zone.
- **Head start** at every leg, with a **4-second speed boost** at its end.
- **Safe zones** (15 m) around every campfire.
- **18.5% faster stamina regeneration.**
- A **backpack** at the start of the round, **one random item** at the start of every leg (a snowball, a brown berrynana or a fortified milk), plus a **biome item**: a heat pack in the Alpine, a sports drink in the Caldera, an early worm in the Gloom, aloe vera in the Mesa.
- **Dash gem:** runners carry **Scout's Ambition** (a scout gem) in their hotbar, whose own power is never used. Using it **dashes you 7 m** horizontally where you look (walls stop it; not while climbing) and **lifts you up** a little (half the height of Scout's Initiative's jump), and the gem goes back to its slot so your hands are free; dashing in mid-air also lets you **float for 0.65 s**. **32-second cooldown**, counted down above its slot, and **3% petrification** per dash. Dashing while falling fast **cancels 80% of the fall**, and from **0.5 s** after a dash you take **no fall damage for 1 s**. Each dash makes **every chaser give off flare smoke** in their own colour for **5 s**, so you can see where they are.
- **Snowballs blind chasers** they hit, and every thrown snowball pushes **10% harder**.
- Runners **can't use the blowgun or the chasers' scout gem** (chaser items), and chasers can't use the runners' dash gem.
- **Regular luggage** can also hold **fortified milk, snowballs, brown berrynanas and energy drinks**, every leg.
- **Fortified milk** protects you from being captured while it's active (milk is twice as heavy and its invincibility is 65% shorter than vanilla).
- First runner into each campfire's safe zone gets a **fortified milk** (not when the chasers won the leg).
- Leg and biome items are dropped at your feet and picked up for you, straight into your hands; if your slots are full they stay on the ground.
- A runner with a chaser closing in hears PEAK's own **Scoutmaster chase music**.

**Drawbacks**
- **Fog, lava and gloom** rise as in vanilla; the fog only hurts runners.
- **Energy drinks** last as long as in vanilla but make you 1.5× sleepier when they wear off.
- A blowgun dart makes you 20% drowsy.
- **Mandrakes** start dropping at your feet a minute into the chase, every 45 seconds (only in the first two biomes). They vanish after 10 s and never make you drowsy.
- When a chaser uses their **gem**, every runner gives off **flare smoke** in their own colour for **3 s**.

---

## Rules for everyone
- **Removed from the game:** amulets (incl. Scout's Honor), gems, rescue claws, jetpacks/rocket packs, gliders, the golden Bing Bong, world blowguns and hidden test items that never appear in normal PEAK.
- **Dynamite and mandrakes** can be used by everyone.
- **Shroomberries** can be eaten (not by chasers), but their effects only last **1 second**; the hunger they cure is the same as vanilla.
- **No Scoutmaster.** He would hunt the runner who's furthest from the group.
- **No revival curse.** Revived players don't get the curse or extra hunger.
- **Hunger** grows **30% slower** than in vanilla (still scaled by the ascent). Runners in a safe zone don't get hungrier at all.
- **Cooldowns refresh every leg:** the blowgun, the chaser gem and the dash gem are ready again when a leg starts.
- **Petrification is cleared** for everyone after each leg.

## HUD
- **Role reveal** and **head-start countdown** in the middle of the screen (translucent, in PEAK yellow, for runners).
- **Leg result** and the **role window** countdown (with the **-** hint) at the campfire.
- **Chaser list** in the top right, under the ascent label. Frozen chasers show in ice blue with their remaining time; dead chasers are crossed out.
- **Blowgun**, **chaser gem** and **dash gem** cooldowns in seconds above their hotbar slots.
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
- Debug/testing keys (F6 swap own role, F7 start/restart round with new roles, F8 freeze yourself, F9 freeze the player you look at) are **off** by default. Turn them on in the host menu.

---

## Installation
1. Install with a mod manager (r2modman / Thunderstore Mod Manager). **BepInExPack_PEAK** is installed automatically.
2. **Every player** installs the same version.
3. Host a lobby and start a run. The round starts by itself once everyone has woken up on the shore.

**More players:** works with lobby-size mods such as *PEAK Unlimited* (not tested together yet). Chaser count scales with `RunnersPerChaser`.

---

## Configuration

Settings live in `BepInEx/config/codesycamore.OnTheLookout.cfg` (or the mod manager's **Config editor**). Change them with the game closed.

- **Only these settings can be changed.** Everything else in this README is built into the mod, so every host and player runs exactly the same rules for a given version.
- **Only the host's values matter**; they're sent to everyone when they join.
- **Every update starts fresh:** when a new version of the mod first runs, the config file is reset to that version's defaults (your old values are not kept).

| Setting | Default | What it does |
|---|---|---|
| `HeadStartSeconds` | 20 | Runner head start each leg (chasers frozen and blind). |
| `HeadStartBoostSeconds` | 4 | Runners' speed boost (no drowsiness) at the end of the head start. 0 = off. |
| `RunnersPerChaser` | 4 | At most one chaser per this many runners (1 in 5 players, 2 in 10). Drawn from the CHASER volunteers; nobody volunteered = one random player. Always leaves a runner. |
| `ZombieStartDelaySeconds` | 300 | With no chaser alive, zombies start coming this long after the head start ends (each leg). |
| `ZombieWaveDelaySeconds` | 120 | Cooldown after a zombie wave is gone (killed or expired) before the next one can come. |
| `ZombieLifetimeSeconds` | 120 | How long each zombie lasts. |

---

## Notes
- Built for **PEAK v2.6.b** (build 25739797). A PEAK update can break mods; if a part of this mod can't hook into the game after an update, only that part switches off and the log says which (`[OTL]` lines).
- Bug reports: please include your `BepInEx/LogOutput.log`.

## Credits
Made by codesycamore. Inspired by the hide-and-seek idea of *Hide and PEAK* by glarmer (no code reused). Built on BepInEx and HarmonyX with the PEAK modding community's project template.

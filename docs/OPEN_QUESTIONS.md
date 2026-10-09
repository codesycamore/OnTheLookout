# Open questions

## Decided (2026-10-07)

- **GUID:** `codesycamore.OnTheLookout`, the current assembly name. It must never change after release.
- **Tag:** a chaser's body colliding with a runner. Detected on the host (prior art: postfix on `Bodypart.OnCollisionEnter`).
- **Captured:** the collision captures (kills) the runner. Every runner who **dies**, whether captured or killed by the environment, enters a conversion pool. One is picked at random to become a chaser in a later biome. **Passing out doesn't count**: teammates can still revive or carry a passed-out runner.
- **Reward (rule 10):** ancient-luggage loot goes to the **first runner to reach the safe zone at each campfire**, not only the summit.
- **Order of work:** skip the hello-world and probe phase. Prove the freeze first, then build the rest.

## Decisions still needed (human)

1. **License.** `LICENSE` is still a TODO. Copying any Hide-and-PEAK code would force GPL-3.0. The current plan avoids that.
2. **Conversion timing (rule 9).** Decided: one random dead runner per conversion. Still open: does a conversion happen on every `GoToSegment` (campfire lit)? Where does the new chaser spawn: at the new campfire, or near the other chasers?
3. **Win conditions.** Do runners win if any runner reaches the summit? Do chasers win when all runners are captured? What if every runner dies to the environment?
4. **Campfire safe zone.** Only lit campfires, or any campfire? Does it also stop freezes from inside the zone?
5. **Fog interactions.** Should chasers be excluded from the "players have moved on" fog trigger and the campfire-rest check? If they aren't, chasers who lag behind delay the fog.
6. **Healing gem conflict.** `Action_HealingGem` is both "healing" (rule 6 allows it) and a "gem" (rule 11 bans it). The ban should probably win.
7. **Backpacks for chasers.** Allowed or not?
8. **Late joiners.** Join as a runner, a chaser, or a spectator until the next biome?
9. **New config keys** introduced by this analysis: `FreezeConeDegrees` (default 15°), the freeze behaviour toggles (`FreezeHoldGrip`, `FreezeLockStamina`, `FreezeZeroVelocity`, `FreezeBlockLook`), and per-module `Enable*` toggles. Confirm the defaults.

## In-game test checklist

Run these with Phase-1 probe logging, as host plus at least one client.

- [ ] Hello-world plugin loads on build 25739797 (BepInEx 5.4.23.3 + PEAKLib after the 2026-10-06 update).
- [ ] `RunManager.StartRun` fires once per run, on the host and on clients.
- [ ] `MapHandler.GoToSegment` fires on all clients when a campfire is lit, with the expected `Segment`.
- [ ] Which fog is active in a normal run: `FogSphere.SetSharderVars` damage path or `Fog.MakePlayerCold`? Is it the same in every biome?
- [ ] Changing `OrbFogHandler.speed` on all clients produces a visibly faster fog with no snapping at the 5 s resync.
- [ ] Freeze input block (postfix `CharacterInput.Sample`):
  - [ ] walking: stops
  - [ ] mid-jump: lands and stays
  - [ ] **mid-climb**: hangs without sliding or falling; stamina drain is acceptable
  - [ ] on a rope or vine
  - [ ] if any of these fail, test pinning the torso and zeroing velocity
- [ ] Host computes a remote runner's `data.lookDirection` accurately enough. Log the angle to the chaser while the runner aims at them.
- [ ] Occlusion linecast with `HelperFunctions.terrainMapMask` is blocked by rocks and terrain but not by foliage. Check which layer foliage is on.
- [ ] `Item.RequestPickup` prefix on the host can deny a pickup cleanly. The item reappears for the client (`DenyPickupRPC`).
- [ ] Item dump: list every item with `AmuletBase`, `Action_*Gem`, `ItemTags.ScoutAmulet`, `Action_ModifyStatus{Injury<0}`, or `Affliction_HealAll`, to finalise the ban and allow lists.
- [ ] Host sends `RPCA_Die` to a client's character and the client dies normally.
- [ ] Host sends `RPCA_ReviveAtPosition(pos,false,-1)` to a dead client **during or after** `GoToSegment`, and the client respawns at the right place.
- [ ] `MountainProgressHandler.IsAtPeak` turns true at the summit, and not before (for example, not at the Kiln).
- [ ] `LootData.GetRandomItems(SpawnPool.LuggageAncient, 3)` + `PhotonNetwork.Instantiate("0_Items/"+name)` spawns pickable items for every client.
- [ ] Host migration: the host quits mid-round, the new master reads the `otl.*` room props, and roles, timers and freezes continue.
- [ ] A client without the mod joins, and the host detects it through the missing `otl.ver`.

## Unverified facts (from research)

- Licenses of Vasodilation, SpongePEAKLib, PhotonCustomPropsUtils, PEAKNetworkingLibrary, and the Lethal Company Hide_And_Seek mod.
- Whether an actual ancient-luggage **object** can be network-spawned (its prefab path is unknown).
- Content Warning (PUN) mods haven't been researched yet.

## Freeze proof of concept: test procedure

Code: `src/OnTheLookout/Freeze/`. Config file: `BepInEx/config/codesycamore.OnTheLookout.cfg` in the r2modman profile. It's created on the first launch, and edits take effect after a restart. Logs use the `[OTL][Freeze]` prefix in `BepInEx/LogOutput.log`.

**Solo (host only)**
- [ ] `[OTL] Plugin OnTheLookout ... loaded. Freeze module: enabled.` appears in the log, and the F8/F9/F10 hint shows top-left in a run.
- [ ] **F8 while walking**: can't move, jump, sprint, crouch, or use items for `FreezeDuration`. Camera still turns. "FROZEN x.xs" shows, then "freeze cooldown".
- [ ] **F8 again during the freeze or the cooldown**: rejected. The log shows `rejected: frozen=True` or `cooldown=True` (rules 4 and 5).
- [ ] **F8 mid-jump**: hangs in the air at the freeze point (`FreezeSuspendInAir`), with no fall damage from the hang itself. On unfreeze, falls normally from that spot. If the body jitters or sags, tune `FreezeSuspendStiffness`.
- [ ] **F8 while climbing a wall, then release the mouse**: keeps hanging (`FreezeHoldGrip`). Stamina doesn't drop (`FreezeLockStamina`). On unfreeze, the player drops if grab isn't held, and keeps climbing if it is.
- [ ] Repeat the climb test with `FreezeHoldGrip=false` and `FreezeLockStamina=false` to see the baseline behaviour.
- [ ] **F8 on a slope, rope, or vine**: if the player slides or drifts, retry with `FreezeZeroVelocity=true`.
- [ ] Does the game's solo mode count as "in a room" (Photon offline mode)? If F8 does nothing, check the log. **Unverified.**

**Two players (host + friend, both with the mod)**
- [ ] **Host F9 while looking at the friend within 25 m**: the friend is frozen on their own screen. This tests the look-check and the room-property replication to a remote player.
- [ ] **Host F9 with the friend behind a rock**: "nobody in range/cone/line of sight". This tests the occlusion mask.
- [ ] **Host F10 (HostIsChaserTest ON), friend looks at the host**: the host freezes. This tests the friend's replicated look direction being judged on the host, which is rules 3–5 end to end. Turn on `LogLookChecks` to see the distances and angles.
- [ ] Friend keeps staring after the freeze ends: no re-freeze until the cooldown expires.
- [ ] Host quits mid-freeze: the friend's timer continues and the new master logs `master switched`.

## Full mode: test plan (first build of all rules, 2026-10-07)

Debug keys (host, bottom-left hint): **F7** start or restart a round, **F6** swap your own role (solo testing as chaser), **F8** freeze yourself, **F9** freeze the player you're looking at. Look in `LogOutput.log` for `[OTL]` lines. The first line lists which modules loaded.

**Solo (F6 to become chaser, F7 to restart)**
- [ ] The round starts automatically when the run starts, and "YOU ARE A RUNNER / CHASER" shows as the hero title.
- [ ] Head start: as a chaser you can't move. A "RELEASED IN" ring and countdown show, then release.
- [ ] Freeze (F8): you pulse icy blue, fast at first and slower near the end. Screen frost with stamina unchanged. Then the "FROZEN" ring, then the "FREEZE IMMUNE" ring.
- [ ] HUD: the ring looks like PEAK's own item ring and scales with resolution. The chaser list sits under the ascent label (top right). Check with ascent 0 and with ascent > 0.
- [ ] Fog as a chaser: no cold damage, and `[OTL][Fog] gated 1 fog damage call(s)` appears in the log. Fog visibly faster (`FogSpeedMultiplier`).
- [ ] Items as a chaser: picking up a non-healing item is denied (the item pops back). Bandages and first-aid can be picked up and used. The log shows `classified ...` lines; copy them here to finalise the lists.
- [ ] Amulets and gems are denied for everyone.
- [ ] As a runner, a campfire can be lit while the chaser is far away (no "can't light" message naming the chaser).

**Two or more players**
- [ ] A runner looking at a chaser freezes them. The chaser pulses on the runner's screen too. No re-freeze during immunity.
- [ ] Chaser runs into a runner: the runner dies, and every client shows "X was caught by Y!". Not possible inside a campfire safe zone (runner sees "SAFE").
- [ ] A passed-out runner can still be revived or carried by teammates (and is capturable if `TagPassedOutRunners`).
- [ ] Lighting a campfire: after `ConversionDelaySeconds`, one dead runner revives near the fire as a chaser ("X has joined the chasers"), and the chaser list updates.
- [ ] The first runner into an unlit campfire's radius gets ancient-loot items at their feet ("reached the campfire first").
- [ ] All runners dead shows "THE CHASERS WIN". A runner at the peak shows "THE RUNNERS ESCAPED".
- [ ] A friend without the mod: the host sees "X doesn't have OnTheLookout".
- [ ] Host leaves mid-round: roles, freezes and the round continue under the new host.

## Decided: playtest feedback 1 (2026-10-07)

- **Round flow is campfire-to-campfire legs.** Each leg begins with a 4 s role reveal (red CHASER / yellow RUNNER) and then the head start. Chasers are **frozen and blind** for both; the blackness fades over the last 40%. Runners see a big translucent white countdown.
- **A leg ends** when every living runner is within **50 m** of the next (unlit) campfire. Everyone sees "ALL RUNNERS ARE SAFE". Captures and freezes pause until the next leg.
- **Only runners can light campfires.** Lighting one starts the next leg (reveal, blind chasers, head start).
- **The first round waits** until everyone has woken up on the beach. The old head start ran out during the wake-up animation, which is why chasers could move.
- **Conversion moved to scout statues.** Using one during a round revives **everyone who is dead** (as in vanilla). One random revived ghost (a dead runner) becomes an extra chaser, and the rest come back as runners. With no ghosts, the statue works as in vanilla.
- **No captures** inside a safe zone (50 m).
- **Freeze range 26 m.** A runner can only freeze a chaser within 26 m.
- **Chasers are 15% faster** (`ChaserSpeedMultiplier`).
- **Removed from the game:** amulets, gems, blowguns, rescue claws, and hidden items that never spawn in normal play. They're destroyed on spawn and can't be picked up or used by anyone.
- **Chasers can only use healing items:** blocked at hover, pickup (client and host) and use.
- **Capture sound:** PEAK's dynamite explosion sound plays at the captured runner.
- **Timer bar:** a light-blue bar above the stamina bar shows FROZEN and IMMUNE time. The progress ring is gone.

**Assumptions to confirm:**
- "All live *chasers* made it into the safe zone" was read as **runners**.
- After a leg completes, captures stay paused until a runner lights the campfire **and the new head start has run out** (confirmed 2026-10-07).
- ~~Other ghosts stay dead~~ → corrected: everyone is revived and one random ghost becomes a chaser (confirmed 2026-10-07).

## Test plan: playtest 2

- [ ] The round starts about 1 s after the last player wakes up on the beach.
- [ ] Reveal: red CHASER / yellow RUNNER in the centre for 4 s. Chasers' screens are black.
- [ ] Countdown: runners see a big translucent number. Chasers see "THE HUNT BEGINS IN" over black that fades near the end. Chasers can't move at all, including mid-air.
- [ ] Timer bar above the stamina bar: light-blue FROZEN, then paler IMMUNE. Check its position and size on your resolution.
- [ ] A chaser further than 26 m can't be frozen. Turn on `LogLookChecks` to see distances.
- [ ] Chasers feel faster than runners.
- [ ] Items: as a chaser, non-healing items show no pickup prompt and can't be used. Healing items work. **Copy the `[OTL][Items]` log lines**, especially the "hidden" list: if a normal item shows up there, add it to `AllowedHiddenItems`.
- [ ] No amulets, gems, blowguns or rescue claws in luggage. If one appears, it can't be picked up.
- [ ] Capture: explosion sound plus toast. No capture within 50 m of a campfire.
- [ ] All living runners within 50 m of the next campfire: "ALL RUNNERS ARE SAFE".
- [ ] A chaser can't light the campfire. A runner can, and that starts the reveal, blindness and head start again.
- [ ] Scout statue with ghosts: all ghosts revive, one random one as a chaser and appears in the chaser list with "CHASER - YOU HAVE JOINED THE CHASERS". With no ghosts, vanilla behaviour.

## Decided: change list 3 (2026-10-07)

- **Chaser blowgun.** Chasers get one at each leg start and on conversion. Unlimited uses, 30 s cooldown (`BlowgunCooldownSeconds`) with a ring above its hotbar slot. A dart doesn't cause sleep; the runner is marked with red flare smoke for 5 s (`TrackingSmokeSeconds`). Blowguns stay removed from the world, and runners can't hold one.
- **Chasers can eat.** Food is detected via `Action_RestoreHunger`, food tags, or hunger reduction. Healing is still allowed.
- **Fog.** 1.5× faster rise **and** 1.5× faster countdown to the rise (`OrbFogHandler.WaitToMove`). Chasers are immune (unchanged).
- **Freeze duration 6.5 s.**
- **Scout amulets.** Already banned. PEAK's "scenery" item pickup path (`FakeItemManager`) bypassed every item rule and is now covered. This was the likely cause of chasers picking up anything.
- **Clown luggage.** During a round only chasers can open it, and it contains food and healing items only.
- **Runner stamina regen +7%** (`RunnerStaminaRegenMultiplier`).
- **No revival curse/hunger** during a round. Lighting a campfire clears every negative status, curse included, for players within the safe-zone radius.
- **Capture boost.** +1% speed for 5 s per capture, and each further capture during the boost adds +0.5% and refreshes the timer. Shown as "SPEED +x%" in the timer bar.
- **Scoutmaster disabled.**
- **Leg loadout.** Each living runner gets one random item from Snowball, Banana or Fortified Milk (`RunnerLegItems`).
- **Fortified milk** protects a runner from capture while its effect is active.
- **More players.** Compatible by design with PEAK Unlimited (glarmer, GPL-3.0; its patch targets don't overlap ours). Not yet tested together.
- **Chaser count by lobby size:** `ChasersByPlayerCount = "1:1, 6:2"` (2 chasers at 6+ players).

**To verify in playtest 3:**
- The `[OTL][Items]` log lines resolve "Snowball", "Banana" and "Fortified Milk". If one logs `no item named ...`, use the name shown in the classification lines.
- The flare smoke looks right following a runner.
- Clown luggage is detected. Look for the "clown luggage filled" log line.
- ~~**Clown luggage 2× (`ClownLuggageMultiplier`).**~~ **Reverted 2026-10-07** (swapping baked map luggage at runtime judged too risky).  Was: PEAK's maps are baked in the editor (`MapGenerator`/`PropSpawner` never run at runtime), so there's no spawn rate to change. Instead, at round start the host turns random plain suitcases into real clown luggage: a copy of an existing clown luggage with its own network view takes the suitcase's place. The count is (multiplier − 1) × the clown luggage already on the map. It's sent as one cached event so late joiners match. If a map has no clown luggage at all, nothing changes.
- **Chasers can only open clown luggage** (`ChasersOnlyOpenClownLuggage`). Scout statues still work for everyone.
- **To verify:** chasers get "Chasers can only open clown luggage" on any other luggage; scout statues still work.
- **Next leg starts after the biome title (2026-10-07).** Before, lighting a campfire started the next leg immediately. PEAK shows the biome title on each player's own screen when they cross the next biome's progress point (`MountainProgressHandler.TriggerReached`, a Z-position check), not when the fire is lit. New order:
  1. Lighting pauses the chase (state `LegComplete`) and clears statuses.
  2. The first client to show the title tells the host.
  3. The next leg (reveal, blind/frozen chasers, head start) starts `BiomeTitleSeconds` (7.5 s) later. If no title is reported, it starts `NoTitleFallbackSeconds` (12 s) after lighting.
  4. A new host after migration resumes the wait.
  - **To verify:** where the progress point sits relative to each campfire, which decides whether the title appears at lighting or only once runners walk on. Watch the `[OTL][Round]` lines: "sees the biome title; next leg in 7.5s".

## Decided: change list 4 (2026-10-07)

- **Bug: items not interactable** (snowballs from the leg loadout, Roots mushrooms). The hidden-item detection only counted spawn pools, but many real items are placed by single-item spawners in the level (`Spawner.spawnedObjectPrefab`: shelf shrooms, snow piles, berry bushes). Fix:
  - The legit set now also includes every item referenced by spawners in the loaded level.
  - It's rebuilt on every scene load.
  - Items named in our own settings (`RunnerLegItems`, `ChaserAllowedItems`, `AllowedHiddenItems`) are never hidden.
  - Scenery items are only hidden for explicit bans, never for "hidden".
- **Admin quick restart** (host, **F10** = `KeyRestartFromCampfire`, `AdminKeys`):
  - Everyone goes back to the last campfire the runners lit; before any campfire, PEAK's base-camp spawn.
  - Dead players are revived, and everyone's statuses are cleared.
  - A fresh leg starts (reveal, blind chasers, head start). Roles are kept.
  - It also works after a round has ended.
- **Jetpacks / rocket packs and gliders** are off by default (`AllowJetpacks`, `AllowGliders`): removed from spawns and unpickable.
- **To verify:** snowball, banana and milk are pickable and usable; Roots shelf mushrooms are pickable. The log line "N item type(s) are placed by this level's spawners" should appear once per level. F10 restart works mid-leg and after a win.

## Decided: change list 5 (2026-10-08) ("killer" = chaser)

- **Shore spawn lock.** Nothing is interactable while everyone wakes up on the shore and for `SpawnInteractLockSeconds` (7) after the round starts. It only applies when the host runs the mod.
- **Chasers take 1/3 of negative statuses** (`ChaserStatusMultiplier` = 0.333, applied on top of the vanilla ascent amounts via `CharacterAfflictions.AddStatus`). They never take fall damage or the fall knock-down (`ChaserNoFallDamage`).
- **Leg complete → chasers teleported** to that campfire (`TeleportChasersOnLegComplete`).
- **Reward** is now an **energy drink** (`RewardItems = "Energy Drink"`, `RewardItemCount = 1`) given to the first runner into each campfire safe zone. Ancient-luggage loot removed.
- **Energy drinks cause 1.5× drowsiness** when they wear off (`EnergyDrinkDrowsyMultiplier`, on `Affliction_FasterBoi.drowsyOnEnd`).
- **Capture → full morale boost** for the chaser: PEAK's morale animation plus a full extra-stamina bar (`CaptureMoraleBoost`).
- **Blowdart adds 10% drowsiness** to the runner hit (`BlowdartDrowsy`), on top of the tracking smoke.
- **Zombies ignore chasers.** They don't target them, and bites do nothing to them (`ZombiesIgnoreChasers`).
- **Runner stamina regen +12%** (saved config updated).
- **To verify:**
  - The item name "Energy Drink" resolves. Check for `no item named` in the log.
  - The morale-boost animation shows for the chaser.
  - Chasers' injury from a fall is 0, and other statuses build up about 3× slower.

## Decided: change list 6 (2026-10-08)

- **Chasers can't see ghosts** (`ChasersSeeGhosts = false`). On a living chaser's client every other ghost's renderers are switched off, the same mechanism PEAK uses to hide your own ghost (`PlayerGhost.RPCA_InitGhost`). Visual only; ghost voices are left alone (decided).
- **Chaser hands cleared before a new item.** Before giving a chaser their blowgun, the host tells their client to drop the held item in front of them (vanilla `DropItemRpc` + un-equip). If nothing is held but all slots are full, slot 1 is dropped. Then the item is given.
- **Banana fix.** The log showed `no item named 'Banana'` every leg: PEAK's fruit isn't called "Banana". Name lookup now falls back to aliases ("banana" → Berrynana, unverified) and then to "name contains", skipping peels. The resolved item is logged, and the full item list (display [prefab]) is logged once per session.
- **To verify:** the log line `'Banana' resolved to ...`. If it picks the wrong fruit, put the exact name from the item list into `RunnerLegItems`.
- **Brown Berrynana** replaces "Banana" in the runner leg items: `RunnerLegItems = "Snowball, Brown Berrynana, Fortified Milk"` (default and saved config). Applied to the **runner** list, the one that was missing the banana; chasers only get the blowgun.
- **Removed:** the custom frozen/immune bar above the stamina bar (`StaminaTimerBar`); it didn't match PEAK's HUD. Remaining freeze feedback: the cold pulses on the body, the frost on the chaser's own screen, and the countdown next to frozen chasers in the chaser list. There's no immunity indicator for now.

## Decided: change list 7 (2026-10-08)

- **Tracking smoke matches the darted runner's skin colour** (`CharacterCustomization.PlayerColor`, the same colour PEAK uses for a player's warp poof).
- **Fireworks above chasers** every `FireworkIntervalSeconds` (30) of an active chase, `FireworkHeight` (8 m) above each living chaser. Revised: it uses the **rocket pack's mid-air explosion** (`CharacterMovement.explosionPrefab`, spawned by `RocketExplodeRPC`), which looks like fireworks; the dynamite explosion is only a fallback. It's cloned locally with `AOE` (knockback/fall damage), colliders, rigidbodies and camera shake stripped. Every client computes the timing from the shared chase start, so it needs no network traffic.
- **Scoutmaster sounds** (`ScoutmasterSounds`) while a chaser is within freeze range of a living runner, at random 3–6 s gaps, played at the chaser. The sounds are the ones on the `Character_Scoutmaster` prefab minus those the normal player prefab `Character` also has. Code doesn't name them, so this is **unverified** until playtest. The log lists what was found ("Scoutmaster sounds: …").
- **To verify:**
  - The firework looks like a burst and hurts nobody (log: "firework built … N damaging/physics component(s) removed").
  - The Scoutmaster sound list in the log isn't empty, and the sounds fit.
- **Hunger is not reduced for chasers** (2026-10-08): `ChaserStatusMultiplier` now skips hunger, so hunger works the same for chasers and runners.
- **Runner backpacks** (`RunnerBackpacks`, 2026-10-08): after roles are assigned at round start, every runner without a backpack gets the ordinary backpack (not fanny/jet/rocket pack) in their backpack slot.
- **Campfire food** (`CampfireFoodItems = "Marshmallow, Glizzy"`; Glizzy is PEAK's prefab name for the Hot Dog, which has a cattail variant too): when a leg's chase ends at a campfire, the host counts marshmallows and hot dogs on the ground within 15 m and spawns only the shortfall, so there's one per living player (random pick each). Hot Dog was in the "hidden" list, so items in this setting are now exempt from the hidden-item ban.
- **To verify:** the log line "campfire food: N player(s), M already there, spawned K". It's unknown whether the campfire's own food spawner adds more when the fire is lit; if so there may be extra food.

## Decided: change list 1.3.1 (2026-10-08)

- **Chaser odds hint readable.** The 1.3.0 outline (0.3) ate the glyphs of PEAK's bold font and the text looked black. Now: yellow text, no outline, on a dark translucent plate sized to the text.
- **Energy drink lasts 65% less** (`EnergyDrinkDurationMultiplier` = 0.35). Applied to every `Affliction_FasterBoi` (the only other source found in code is the random mushroom effect, already cut to 1 s); items that apply it are configured on prefabs, so another item using it would be shortened too (unverified).
- **Campfire → title → freeze order revised.** Lighting a campfire no longer forces the biome title or freezes everyone:
  1. Lighting ends the chase (`LegComplete`), clears statuses; nobody is frozen (flag `AwaitingTitle`).
  2. When a **living runner** walks far enough to see the next biome's title (PEAK's own `TriggerReached`), the host freezes everyone and starts the next leg `BiomeTitleSeconds` (7.5 s) later: role reveal → head start.
  3. A runner who already crossed that point before the fire was lit reports it right away. If the campfire leads to a segment with no progress point, everyone freezes at lighting and the leg starts `NoTitleFallbackSeconds` (now 5 s) later.
  - **To verify:** `[OTL][Round]` lines "waiting for a runner to reach the next biome's title" then "X sees the biome title; everyone frozen, next leg in 7.5s". Chasers seeing the title first must not trigger it.
- **Safe zone radius 30 m → 20 m** (`CampfireSafeRadius`; saved dev config updated).

## Decided: change list 1.3.2 (2026-10-08)

- Freeze after the biome title: `BiomeTitleSeconds` 7.5 → **3** (saved dev config updated).
- HUD texts: reveal subtitle "YOUR ROLE" → "YOU ARE A..."; runner countdown subtitle "HEAD START" → "YOU HAVE A HEADSTART... RUN!" (chasers keep "THE HUNT BEGINS IN").

## Decided: change list 1.4.0 (2026-10-08)

- **Campfire → next leg immediately.** Lighting starts the next leg on the spot (`RoundManager.HostOnCampfireLit` → `HostStartLeg`): the reveal freezes everyone, then the head start. The biome-title wait (flags, `TriggerReached` patch, `Msg.BiomeTitle`, `BiomeTitleSeconds`, `NoTitleFallbackSeconds`) is removed.
- **Shore pre-round freeze** (`RoundManager.InPreRound`): input is blocked (no mid-air suspension, so the intro fall and waking up play out) while PEAK's run is going but the room's round belongs to no run or another run (`otl.run` ≠ `RunManager.RunId`), when the host runs the mod with `AutoStartRound`. Ends when the host starts the round (everyone awake, max 60 s). Also drives the shore interaction lock.
  - **Risk:** if the host leaves before the round starts, the new host doesn't start it; use the host menu / F7.
- **No reward after a wipe:** the campfire the chasers are sent to is marked as already rewarded (`RewardSystem.HostMarkRewarded`).
- **Reward = Fortified Milk** (`RewardItems`; saved dev config updated).
- **Item handout via vanilla pickup** (`LegLoadout.Give`): the host spawns the item at the player's feet and sends `Msg.PickUpItem` (replaces `RefreshSlot`); the player's client calls `Item.Interact` (→ `RequestPickup` → `Player.AddItem` + `OnPickupAccepted`, which equips it). Retries for 4 s (vanilla ignores pickups within 0.25 s of an equip). Slots full → left on the ground. Backpacks still go straight into the backpack slot.
- **Chase music** (`ScoutmasterChaseMusic`): vanilla `MyresAmbience` fades a looping `fearMusic` by the animator float "Myers Distance", which `CharacterAnimations` copies from `CharacterData.myersDistance` (reset to 1000 each frame) and the Scoutmaster sets on his target (`Scoutmaster.DoVisuals`). We set it on the local runner to the nearest living chaser during the chase. Old random sounds off (`ScoutmasterSounds = false`).
  - **Unverified:** that every player character has a `MyresAmbience` (log: "chase music: MyresAmbience on the local character = True").
- **Cactus balls:** "Cactus" (prefab `CactusBall`) was classified *hidden* and so banned for everyone; added to `AllowedHiddenItems` and `ChaserAllowedItems`.
- **Runner name tags hidden from chasers** (`UIPlayerNames.UpdateName` prefix forces `visible = false`).
- **Chaser fall damage 1/3** (`ChaserFallDamageMultiplier = 0.333`). The knockdown is unaffected: `CharacterMovement.CheckFallDamage` calls `character.Fall(a * 5)` before the injury we scale.
- **Freeze blindness** (`FreezeBlindsChasers`): on a look-freeze the chaser's own client adds `Affliction_Blind` (totalTime = freeze time left) and removes it when unfrozen.
- **Peak:** runners win when every living runner `IsAtPeak`; chasers are warped next to a runner and killed 2 s later (`PeakChasersDie`).
- **Blowdart drowsiness 18%.**
- **To verify:** shore freeze ends when the reveal starts; items land in hands (watch "spawned a … for X to pick up"); chase music audible and continuous; cactus pickup; blindness during freeze; peak ending.

## Decided: fireworks on runners (2026-10-08)

- The periodic firework (`ChaseEffects.UpdateFireworks`) now bursts above every **living runner** instead of every chaser. Same interval (30 s), height (8 m) and harmless rocket-pack explosion; still computed on every client from the shared chase start.

## Decided: change list 2026-10-09 (roles every leg)

- **Leg end → role window → next leg** (`RoundManager.HostTick` / `HostEndLeg` / `HostCloseRoleWindow`). Runners win a leg when every living runner is in an unlit campfire's safe zone; chasers win it when every runner is dead (no campfire left = chasers win the game). The host then warps everyone (revives the dead with `RPCA_ReviveAtPosition`) next to the campfire, sets flag `FlagRoleWindow` + room key `otl.rwin` (window end, server time), and everyone's input is blocked (input only, no mid-air suspension). After `RoleWindowSeconds` the host draws roles (`RoleManager.AssignFromPreferences`) and lights the campfire itself (`Campfire.Light_Rpc(true, 0)` → `LightPostfix` → next leg).
- **Role draw:** volunteers (CHASER in the menu) shuffled, at most `floor(players / (RunnersPerChaser + 1))` (min 1, always ≥ 1 runner). No volunteers → one random player. Choices: event to the host only (`Msg.ChaserPreference`), kept until changed, cleared when the round goes Idle (airport).
- **Interpretation:** "remove the fireworks interval on chasers" = no fireworks above chasers; the runner fireworks (previous change) stay.
- **Statues:** `Luggage.IsInteractible`/`Interact_CastFinished` refuse `RespawnChest` during a round. `ConversionSystem` removed.
- **Capture:** prefixes on `CharacterInteractible` (`IsInteractible`, `IsPrimaryInteractible`, `IsConstantlyInteractable`, `GetInteractionText`, `GetInteractTime`, `Interact`, `Interact_CastFinished`) make a runner a 1.5 s hold target ("CAPTURE") for a local chaser; on finish the chaser claims (`Msg.TagClaim`), host validates as before (sender must be the chaser). The `Bodypart.OnCollisionEnter` capture is gone.
- **Fog:** `OrbFogHandler.WaitToMove` prefix during a round: the host sends `StartMovingRPC` once the chase has run `FogStartDelaySeconds`; PEAK's own timer / "players moved on" check is skipped.
- **Mandrakes:** host spawns `0_Items/Mandrake` behind each living runner outside a safe zone (`MandrakeDrops`).
- **Chaser napberry:** `Item.StartUse*/ContinueUse*` (via `ItemRules.UsePrefix`) for a local chaser holding a napberry never eats it; off cooldown it adds an `Affliction_FasterBoi` copied from the Energy Drink's `Action_ApplyAffliction` (drowsyOnEnd 0) and `AddPetrify(8)`. Captures `AddPetrify(-5)`. Our boosts skip the energy-drink shortening.
- **Blowgun kept:** `CharacterItems.DoDropping` skipped for a chaser holding it; at a leg start a non-chaser's client empties any blowgun slot (`Player.EmptySlot`).
- **Snowball blind:** `Snowball.OnCollisionEnter` postfix; if `lastThrownCharacter` is a runner (≤ 3 s ago) and the hit character is the local chaser, add `Affliction_Blind` 2.5 s.
- **Golden Bing Bong:** items with `BingBongShieldWhileHolding` are banned; `TryApplyInvincibility` is skipped during a round.
- **Red outline:** red copy of `StaminaBar.shield` (the yellow invincibility outline), shown for a chaser when the yellow one isn't.
- **Airport = vanilla:** `RoundManager.IsActive` is false while the local player is in the airport; the host resets round/roles/flags/choices there (`HostResetToVanilla`, also called by the host menu's airport restart).
- **Already true:** blowgun darts never affect chasers (every chaser-fired dart is neutralised on impact; only runners get smoke + drowsiness).
- **To verify in playtest:** capture hold prompt and ring; role window freeze + menu + auto-light; runners/chasers win notifications; fog start time; mandrake drops; napberry boost + petrify + cooldown number; snowball blind; red outline looks right; golden Bing Bong log line (`banned=golden Bing Bong`); airport has no roles/HUD.
- **Open:** runners who were chasers last leg don't get a backpack (only given at round start); a host leaving during the role window hands over the countdown via `otl.rwin`.
- **Revised (same day):** the red outline is a **red body glow** instead (`ChaseEffects.UpdateChaserGlow`: `CharacterCustomization.PulseStatus(red, ChaserGlowIntensity)` on every living chaser every 0.1 s, on every client, the way `Affliction_Invincibility` keeps its gold glow; skipped while frozen or invincible so those glows show). Stamina-bar outline removed.
- **Fireworks removed entirely** (they were above runners after the previous change; the user wants the feature gone). Settings `FireworkIntervalSeconds`/`FireworkHeight` deleted.
- **Revised (same day):** the role menu (**-**) is also available in the **airport**, for the first draw on the shore (`ChaserPreference.Available` = airport or role window). Pool draw unchanged: random pick from CHASER volunteers, no weights. Choices are cleared when the host resets the round on arriving in the airport (first host tick there), so a choice made in the first half-second after loading could be lost; untested.
- **Revised (2026-10-09):** role window **10 s** (`RoleWindowSeconds`, saved dev config updated). Hint text (airport and role window): "PRESS HOTKEY (-) TO SELECT YOUR ROLE: RUNNER/CHASER" (the key name follows `KeyChaserOdds`); the window countdown stays in the centre announcement.
- **Chaser kit guarantee (2026-10-09):** `LegLoadout.HostEnsureChaserKits` runs on every host tick (and at each leg start): any living chaser missing the blowgun or napberry gets it via the spawn-and-pickup path; hands are cleared for the blowgun, and for the napberry only if every slot is full. One hand-out per chaser at a time, retried at most every 15 s. Covers runner→chaser at the role draw, debug role swaps mid-leg and kits lost on death.
- **Role swaps (2026-10-09):** `RoleSwap` (host) compares each role update with the last one while a round is active (the first draw of a round and the airport reset only take a snapshot). Runner → chaser: `Msg.DropAllItems` → the player's client runs PEAK's `CharacterItems.DropAllItems(includeBackpack: true)` (as on death); the kit hand-out is held off 2.5 s, then `HostEnsureChaserKits` gives blowgun + napberry. Chaser → runner: `Msg.DropChaserKit` → the client puts away a held kit item and drops blowgun/napberry slots with `DropItemFromSlotRPC`; 2.5 s later the host gives a plain backpack if the backpack slot is empty. Napberries are now refused to runners (pickup and use; `ItemRules.Refusal`), like the blowgun. Replaces the leg-start blowgun strip. Untested: a new host after migration has no snapshot, so the first role change after a migration is not treated as a swap.
- **Swap follow-ups (2026-10-09):** runner items are given once per runner per leg (`LegLoadout.HostGiveRunnerLegItems`, keyed by leg); a chaser who becomes a runner gets them ~3.5 s after the swap unless the leg-start hand-out already covered them. Chasers can't pick up a second blowgun or napberry (`ItemRules.PickupRefusal`, used by every pickup path; using a held one is unaffected). Both cooldowns are static per local player, so they were already shared.
- **Napberry revised (2026-10-09):** effects start `NapberryDelaySeconds` (0.75 s) after use (cooldown and petrification apply at use); they last `NapberryBoostSeconds` (2.25 s) and add `Affliction_InfiniteStamina` (PEAK's rainbow sugar-rush bar, no drowsyAffliction) next to the speed boost. Not added if an unlimited-stamina effect is already running (its Stack would combine drowsiness). Saved dev config updated.
- **Napberry cooldown (2026-10-09):** the cooldown now starts when the effects end (use + 0.75 s delay + 2.25 s), not at use; the napberry can't be used again in between. If the boost is cancelled during the delay (death, role change, chase over), the cooldown starts then.
- **Revised (2026-10-09):** mandrakes from **3 min** (`MandrakeStartSeconds` 180); runner stamina regen **+18.5- **Revised (2026-10-09):** mandrakes from **3 min** (`MandrakeStartSeconds` 180); runner stamina regen **+18.5%** (1.185); napberry delay **1 s**; fog start **5 min** after the head start (`FogStartDelaySeconds` 300) and speed **2.5x** (`FogSpeedMultiplier`). **Lava and gloom** were not covered before: they are `LavaRising` (types Lava and Gloom), started by the host after `initialWaitTime` and moved by `timeTraveled`. Now `LavaRising.Update` prefix (host): during a round the wait is held at 0 until the chase has run `FogStartDelaySeconds`, then `started = true` + `RPC_SyncLava`; postfix (every client): `timeTraveled` advances `FogSpeedMultiplier` times as fast. Respects the lava/gloom hazard run settings; rising souls (VoidGhosts) untouched. Saved dev config updated. **Unverified:** that the gloom biome really uses LavaRising (the Gloom type exists in code) and how the lava looks at 2.5x.
- **Mandrake first scream (2026-10-09):** `Mandrake.Start` sets a new mandrake's first wait to `screamWaitMax` (later screams: random `screamWaitMin`..`screamWaitMax`); the host counts it in `CheckScream` only while someone conscious is within `nearPlayersDistance` (20 m) and the mandrake is held or lying non-kinematic. Our dropped mandrakes get `waitBeforeScreamTime *= MandrakeScreamDelayMultiplier` (0.5) two frames after spawning. The prefab's actual seconds are unknown from code; the host log prints them ("first scream in Xs (vanilla Ys)").
- **Revised:** the first scream of a dropped mandrake is now a fixed **0.75 s** after it appears (`MandrakeFirstScreamSeconds`, replaces the 0.5 multiplier). The 20 m / non-kinematic conditions of `CheckScream` still apply; a mandrake dropped right behind a runner and still falling meets both.

## Decided: change list 2026-10-09 (after 1.5.1)

- **Luggage extras:** "regular (chaser) luggage" read as **regular, non-clown luggage** (the clown luggage is the chasers', and chasers can't use snowballs). `Spawner.GetObjectsToSpawn` postfix: during a round each item a regular luggage is about to spawn has `LuggageExtraChance` (0.25) to become a random `LuggageExtraItems` item.
- **Runner capture indicator:** the chaser's client sends `Msg.CaptureProgress` [chaser, start server time, duration ms] to the runner when the hold starts (`CharacterInteractible.Interact` prefix) and duration 0 when it stops (`CancelCast` prefix; vanilla calls it on release, look-away and after finishing). `UI.CaptureIndicator` fills a red bar over the same time. Hold time 0.75 s.
- **Milk:** `CanCapture` now refuses a milk-protected runner on the chaser's client too (no prompt / ring); the host check stays.
- **Chaser odds:** the draw code was already volunteers-only (`RoleManager.AssignFromPreferences`; random pick only with zero volunteers). The only way a non-volunteer could be picked was a choice that never reached the host (e.g. made just as the host arrived in the airport and cleared choices). Fix: non-host clients re-send their choice every 2 s while it can be changed (`ChaserPreference.LocalTick`). Draw lines in the host log: "drew N chaser(s) of max M (V volunteer(s))".
- **Role swaps by snapshot:** `RoleSwap.HostSnapshot` in `HostEndLeg` (before the window), `HostApplySnapshot` in `HostStartLeg` (after the draw); debug swaps via `RoleManager.SetRole` -> `HostOnSwap`. Replaces the RolesChanged-based detection. Safety net: each runner's client drops any blowgun/chaser gem 3.5 s after a leg starts.
- **Snowball:** extra impulse = snowball mass x impact speed x `SnowballKnockbackBonus` (0.1) on the body part hit, applied by the hit scout's own client (it owns their ragdoll). Vanilla snowballs have no knockback code of their own, so "10% stronger" is this extra push on top of the physical collision. Snowball added to `ChaserForbiddenItems`.
- **Host hint:** "PRESS HOTKEY (+) FOR HOST CONTROLS" placed each frame at the top-left of `GUIManager.bar.fullBar` (the host's stamina bar), translucent plate, 22 pt. The menu key stays `=` (same physical key as +); numpad + also opens it.
- **Scoutmaster audio on chasers:** the chase music only ever played on the runner's own client. The likely cause of the chasers' audio glitches was the old random `ScoutmasterSounds` effect, played at chasers, which a host's older saved config could still have on (it is host-synced). That effect and its settings are removed.
- **Chaser gem:** Scout's Initiative = prefab `Amulet_SuperJump` (`DoubleJumpAmulet`; the wiki's "lifts scouts within 12.8 m, then wind walk"). Using an amulet normally runs `Action_StrangeGem` -> `ToggleGem` (activates its power, which then works passively while carried). Our `ItemRules.UsePrefix` -> `ChaserKit.HandleUse` intercepts the chaser's use first, so the power is never activated. Amulets stay banned in the world; the chaser gem is allowed for chasers only. Napberries are normal food again (runners included).
- **Unverified:** that "red gem" = Scout's Initiative's colour (prefab data); that `Amulet_SuperJump` doesn't start with its power on (`AmuletBase.startActive`); luggage contents really go through `GetObjectsToSpawn` for regular luggage (it does for clown luggage).
- **Luggage odds raised (2026-10-09):** Energy Drink added to `LuggageExtraItems`; `LuggageExtraChance` 0.25 -> 0.383 so each item keeps a 15% higher per-slot chance (0.25/3 = 8.33% -> 0.383/4 = 9.58%). Read "increase by 15%" as relative (x1.15), not +15 percentage points.
- **Capture hold 0.4 s (2026-10-09):** `CaptureHoldSeconds` 0.75 -> 0.4 (saved dev config updated). The runner indicator uses the same value from the chaser's message.

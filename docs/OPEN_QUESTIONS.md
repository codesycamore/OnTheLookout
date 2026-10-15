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
- **Capture targeting (2026-10-11):** `Interaction.DoInteractableRaycasts` postfix (`TagSystem.RaycastPostfix`): for a local chaser during the chase, the nearest capturable runner within `CaptureRange` (1.4 m, centre to centre; +0.4 m while already holding on them) and within 80° of the camera (any angle within 0.9 m) replaces whatever the look ray found (backpack, `TestItemsStuckToMe` thorns/arrows/cactus balls, items). `CanCapture` also requires that distance. The runner's facing never mattered for the primary interaction; the backpack collider on their back was what got hit first from behind.
- **Chaser-list freeze timer:** no stacking path found in code: the host ignores looks while a chaser is frozen or on cooldown (`FreezeState.TryFreeze`), and the list reads the single replicated end time. Display now ceil-rounded and clamped to `FreezeDuration`; a warning is logged ("shows Xs of freeze left, more than FreezeDuration") if the replicated value is ever larger. **Need from playtest:** that log line, or a clip of the list, if it still looks wrong.
- **Runner dash gem (2026-10-11):** `RunnerDash`. Scout's Ambition = `Amulet_InfiniteStamina` (InfiniteStamAmulet; wiki: infinite-stamina aura). Using it as a runner (intercepted in `ItemRules.UsePrefix` like the chaser gem, so its power is never activated): horizontal dash along `lookDirection_Flat`, done on the runner's own client by setting every body part's horizontal velocity to DashDistance / 0.18 s for 0.18 s (WaitForFixedUpdate), then capping it at 3 m/s so the ragdoll doesn't slide on. Not while climbing / on ropes / vines, during the reveal or the role window (no cooldown spent then). Cooldown 70 s from use, +6% petrify. `LegLoadout.HostGiveRunnerGem` at every leg start and after chaser->runner swaps; refused to chasers (`ItemRules.Refusal`); one per runner; chasers drop it in the leg-start safety check. Safe-zone relief: each client on `LegCompleted`, if the runners won and the local runner is in the safe zone, `AddPetrify(-3)`.
- **Interpretation:** "part of the role selection logic of it being equipped when becoming a chaser" read as: becoming a **runner** equips it, becoming a chaser drops it.
- **Unverified:** that Scout's Ambition is the yellow gem; the dash distance on slopes / in the air (air drag and ragdoll friction may make it a bit shorter or longer than 2.5 m); that `Amulet_InfiniteStamina` doesn't start with its power on (its aura would otherwise run while carried).
- **Original gem powers off (2026-10-11):** `Action_StrangeGem.RunAction` prefix (skips `ToggleGem` for the mod gems) and `ItemPocketBehaviorInstanceBase.behaviorEnabled` getter postfix (false for a slot holding a mod gem), both only during a round. Covers Ambition's carried stamina aura + 1%/0.5 s petrify and Initiative's double jump, even if a gem spawned with its power already on.
- **Dash revised (2026-10-11):** `DashDistance` 3.2 m; a dash started while not grounded (`data.isGrounded` false) holds every body part's vertical velocity at 0 each physics step for `DashAirFloatSeconds` (0.65 s) from the start of the dash. Gravity still acts within each step, so a tiny sink is possible (a few cm).
- **Capture hold 0.45 s (2026-10-11):** `CaptureHoldSeconds` 0.4 -> 0.45 (saved dev config updated).

## Decided: locked-down config (2026-10-11)

- **Only 11 settings are configurable** (`ModConfig.Configurable`): HeadStartSeconds, HeadStartBoostSeconds, RunnersPerChaser, FogStartDelaySeconds, FogSpeedMultiplier, ZombieStartDelaySeconds, ZombieWaveDelaySeconds, ZombieLifetimeSeconds, MandrakeStartSeconds, MandrakeIntervalSeconds, MandrakeFirstScreamSeconds. "Zombie spawn timer" read as both the first-wave delay (ZombieStartDelaySeconds) and the wave cooldown (ZombieWaveDelaySeconds).
- **Everything else** is bound to a second, in-memory `ConfigFile` (path in BepInEx/cache, deleted at start, `saveOnInit: false`, `SaveOnConfigSet = false`), so it always has the built-in value; code is unchanged (same `ConfigEntry` properties, `.Synced()` still works). Personal/debug settings (keys, chaser list, countdown opacity, capture sound, kick without mod, debug keys) are fixed too; the host menu's debug-keys toggle still works for the session.
- **Version reset:** the config file stores `[0. Version] ConfigWrittenByVersion`. If it differs from the running version (any update, or files from before this change), the file is emptied and rewritten with the current defaults before anything is bound. Consequence: a host's custom values for the 11 settings are also reset on every update (as asked).
- **Untested:** the reset on a real r2modman profile (check the log line "[OTL][Config] config written by ..., now X: reset to this version's defaults" and that the config editor only lists the 11 settings + the version line).
- **Revised (2026-10-11):** `DashDistance` 4.3 m, `DashPetrify` 0.05, `HeadStartBoostSeconds` 4 s; mandrake settings removed from `ModConfig.Configurable` (8 configurable settings left). Dev profile config set to reset on its next launch (`ConfigWrittenByVersion` blanked).
- **Shore interaction lock (2026-10-11):** `RoundManager.InSpawnLock` on leg 1 now lasts until the reveal ends (`RevealEnd`) instead of 7 s from the leg start (which ran ~3 s into the head start). `SpawnInteractLockSeconds` removed.
- **Gems auto put away (2026-10-11):** after the chaser boost or the runner dash actually fires, the user's client calls `CharacterItems.EquipSlot(None)` one frame later (only if they still hold that gem), same as pressing the slot again. Not on a press during cooldown.

### 1.8.0 (2026-10-11)

- **Fog, lava and gloom vanilla again** (user: "just want it to be like vanilla ... default start rising timer and default speed"): the `OrbFogHandler.Move`/`WaitToMove`, `Fog.Move` and `LavaRising.Update` patches are gone, and so are `FogStartDelaySeconds`/`FogSpeedMultiplier`. Kept: fog damage gate for chasers (`FogSphere.SetSharderVars` transpiler, `Fog.MakePlayerCold`).
- **Capture "bug" cause:** vanilla interaction reaches 2 m from the camera, capture only 1.4 m between bodies, so between the two the runner's `BackpackOnBackVisuals` (0.25 s hold, shorter than the capture) won and opened the backpack wheel. Now, for a chaser, anything owned by a runner (`CharacterInteractible`, `BackpackOnBackVisuals`, `ThornOnMe`/`ArrowOnMe`, items stuck to them) is never a target except the capture itself. Target selection: hold target first (reach +1 m while holding), then a look ray onto the runner's body, then the old cone. `IsInteractible` / `IsPrimaryInteractible` / `IsConstantlyInteractable` / `IsSecondaryInteractible` return false chaser→runner when no capture is possible, and `Interact` never carries.
- **Red glow:** milk calls `PulseStatus(gold, 1)` every frame (`Affliction_Invincibility.UpdateEffectNetworked`). Ours ran every 0.1 s at 0.8, each pulse fading over 0.5 s, and lost to PEAK's own status pulses. It now runs every frame in `LateUpdate` at 1.0.
- **Capture rush:** `ChaserKit.ApplySugarRush`: lengthens an existing `Affliction_FasterBoi` / `Affliction_InfiniteStamina` (`totalTime = timeElapsed + seconds`) instead of stacking. `InfiniteStamina.Stack` would NRE on a lollipop's drowsy part.
- **Gather check:** `HostGatherAt` spots 2.5-4.1 m; at window close any player >5 m horizontally (or dead) triggers one more gather and a 1.5 s settle. `FreezeSuspendPatch` releases the mid-air anchor when not chasing.

**Test checklist (1.8.0)**
- [ ] Chaser 1.5-2 m behind a runner with a backpack: no "open" prompt; walking closer shows CAPTURE and the hold completes.
- [ ] Capture while the runner strafes/turns: hold completes; runner sprinting away (>2.4 m) breaks it.
- [ ] Chaser on a passed-out runner: only CAPTURE, never carry; in a safe zone: no prompt at all.
- [ ] Runner with thorns/arrows: chaser gets no "remove" prompt.
- [ ] Capture: speed + rainbow stamina bar for 4 s; second capture within it adds 3 s; no drowsiness after.
- [ ] Chasers visibly glow red to runners (compare with milk).
- [ ] Fog starts on vanilla timing/speed; chasers take no fog damage; Caldera lava rises on vanilla timing.
- [ ] Leg end: log shows no "not within 5m" warning normally; if it does, everyone is regathered and roles follow 1.5 s later.

### 1.8.1 (2026-10-12)

- **Cooldowns per leg:** on `RoundManager.LegStarted` every client resets its own blowgun, chaser gem and dash cooldowns (`ChaserKit.ResetCooldowns`). A gem boost still running across the leg change no longer starts a cooldown when it ends (`s_GemLeg`). Freeze immunity was already reset by `FreezeState.HostReset` in `HostStartLeg`.
- **Hunger:** the vanilla tick is `AddStatus(Hunger, deltaTime * hungerPerSecond * Ascents.hungerRateMultiplier)` in `CharacterAfflictions.UpdateNormalStatuses`. A prefix/postfix flag around that method lets an `AddStatus` prefix scale only that tick (×`HungerRateMultiplier` 0.7, ×0 for a runner in `SafeZoneSystem.IsSafe`). Food, revival etc. untouched. Applies to everyone during a round.
- **Dash fall protection:** `CharacterMovement.CheckFallDamage` / `CharacterClimbing.CheckFallDamage` are skipped entirely (no injury, no `Character.Fall` knock-down) for the local runner between +0.5 s and +1.5 s after a dash.
- **Energy drink:** the `MilkRules` FasterBoi shortening is gone (vanilla length); `Tweaks` still scales `drowsyOnEnd` ×1.5 in a round.
- **Chaser sugar ban:** `ItemCatalog.IsSugar` (item has `Action_ApplyInfiniteStamina`, or an `Action_ApplyAffliction` with `Affliction_FasterBoi` / `Affliction_InfiniteStamina`) is refused to chasers and filtered out of clown loot, on top of `ChaserForbiddenItems`. No log line ever showed a chaser getting one, so the reported case is unexplained; the clown-luggage log now lists the items it chose.
- **Snowball:** no "blind" line in any log, so the victim-side hit (`Snowball.OnCollisionEnter` on the hit scout's client) likely never fired. Now detected on the snowball owner's client (`item.view.IsMine`, the thrower, real collision speed), once per throw (1 s debounce per view id), and sent as `Msg.SnowballHit` to the hit scout, who applies the extra impulse on the hit body part and, if runner→chaser, `Affliction_Blind` for 3 s.

**Test checklist (1.8.1)**
- [ ] Use gem/dash/blowgun just before a leg ends: all three are ready when the next leg starts (log: "ability cooldowns refreshed").
- [ ] Hunger bar grows noticeably slower than vanilla; standing in the safe zone as a runner it doesn't grow; as a chaser in the safe zone it does.
- [ ] Dash off a ledge and land within ~1.5 s: no fall damage, no knock-down. Land after 2 s+: normal damage.
- [ ] Runner energy drink: vanilla length, more drowsy at the end.
- [ ] Chaser: can't pick up energy drink or lollipop; check the log for "gives a speed boost / unlimited stamina" on items you didn't expect (e.g. healing items).
- [ ] Open clown luggage as a chaser: log line lists no energy drink / lollipop.
- [ ] Runner snowball on a chaser: thrower's log "snowball ... hit"; chaser's screen goes blue-blind for 3 s and they get pushed.
- [ ] Head-start countdown (runner) is yellow and still translucent; host hint smaller.

### 1.8.2 (2026-10-12)

- **Mandrakes:** `MandrakeStartSeconds` 60, `MandrakeIntervalSeconds` 20.
- **Mandrake boom:** the host broadcasts `Notice.MandrakeBoom` (runner actor) with each drop; every client plays the sound at that runner. Only the sounds of `Dynamite.explosionPrefab` are played (its `SFX_PlayOneShot.sfx/sfxs`, falling back to its `AudioSource` clips); the prefab itself is never spawned, so there is no damage, knockback or visual. Read "sync with the mandrakes" as "at the moment the mandrake appears" (its first scream follows 0.75 s later).
- **Gem reveals:** the user of the gem broadcasts `Notice.GemReveal` / `Notice.DashReveal`; every client attaches the blowgun's `TrackingSmoke` (flare VFX, player colour) to every living runner (5 s) / chaser (3 s). Only while chasing.

**Test checklist (1.8.2)**
- [ ] First mandrake ~60 s after the head start ends, then every 20 s; runners in a safe zone get none.
- [ ] A boom is heard at each runner when their mandrake appears; nobody takes damage. Log: "explosion sound: ..." names the sound (or says it wasn't found).
- [ ] Chaser uses the gem: every runner (seen by everyone) smokes in their colour for 5 s.
- [ ] Runner dashes: every chaser smokes in their colour for 3 s.

### 1.8.3 (2026-10-13)

- **Mandrakes:** `MandrakeIntervalSeconds` 40. Each dropped mandrake is removed by the host (`PhotonNetwork.Destroy`) `MandrakeLifetimeSeconds` (10) after it appears, if it lies on the ground; one a player holds is re-checked every second until dropped (or the leg ends). One stashed in a pack is no longer a scene object, so it stays.
- **No mandrake sleep for runners:** how the scream applies drowsiness lives in the mandrake prefab (serialized, not in the decompiled code), probably an `AOE` fired through `Mandrake.timeEvent`. While a `TimeEvent.Update` or `AOE.Explode` on an object under a `Mandrake` / `MandrakeScreamFX` runs, `CharacterAfflictions.AddStatus(Drowsy)` and `AddAffliction(Affliction_AdjustDrowsyOverTime)` are skipped for runners. Applies to every mandrake during a round, not only our drops. Unverified: if the sleep comes from somewhere else, the "drowsiness ... was blocked" log line never appears.
- **Mandrakes only in the first two biomes:** "after the second biome" read as map segments 0-1 (Shore + Tropics/Roots), `MandrakeLastSegment` = 1, using the same leg segment as `LegLoadout` (last lit campfire's `advanceToSegment`). Waves still run in every leg and still broadcast `MandrakeBoom` per runner outside a safe zone.
- Chaser gem `GemPetrify` 0.05, `GemRevealSeconds` 3; runner `DashDistance` 7, `DashRevealSeconds` 5.

**Test checklist (1.8.3)**
- [ ] Mandrake waves every 40 s; each mandrake disappears ~10 s after it appears (log "removed a dropped mandrake").
- [ ] A runner next to a screaming mandrake doesn't get drowsy (log "drowsiness on runner ... was blocked"); a chaser next to one does.
- [ ] Shore and Tropics/Roots: mandrakes + booms. Alpine/Mesa onwards: booms only (log "segment N: 0 mandrake(s) dropped, X explosion(s)").
- [ ] Chaser gem: +5% petrification; runners smoke for 3 s.
- [ ] Runner dash: ~7 m, walls still stop it; chasers smoke for 5 s.

**1.8.3, second change list (2026-10-13)**

- **Explosion sounds:** split from the mandrake waves into their own host timer (`MandrakeDrops.HostTickBooms`: every 120 s of the chase, per living runner outside a safe zone, every leg). Off via `ExplosionsEnabled = false` (static readonly in code, deliberately not a config key). Flip it to true to bring them back.
- **Mandrake drowsiness on chasers ×0.9:** same mandrake context as the runner block; `AddStatus(Drowsy)` amount ×`MandrakeChaserSleepMultiplier`, and an `Affliction_AdjustDrowsyOverTime` is replaced by a scaled copy (the original may be the prefab's serialized instance). Same unverified assumption about where the sleep comes from.
- **Capture rush:** no more extension: `ApplySugarRush(4 s)` makes it last at least 4 s from now.
- **Gem bonus per capture:** "enhance the gem's effects by 1.7%" read as the gem's energy-drink speed (move + climb `Affliction_FasterBoi` mods ×(1 + 0.017 × captures this leg)); unlimited stamina can't be "stronger", and duration stays. Counted on the chaser's own client from the `Captured` notice, keyed to `LegKey` (so it resets every leg). If a speed boost is already running, a stronger one replaces it (remove + add, since FasterBoi adds its speed in `OnApplied` and subtracts in `OnRemoved`; editing it would leave speed behind). The old one's `drowsyOnEnd` carries over.
- **Petrification cleared every leg:** each client clears its own (`AddPetrify(-petrifyAmount)`) on `RoundManager.LegCompleted` and again on `LegStarted`. Replaces `SafeZonePetrifyRelief`. Capture relief (-5%) stays.
- **Blowgun:** `TrackingSmoke` no longer attached on a dart hit (still used by the gem reveals). `BlowdartDrowsy` 0.2 ("20% stamina" read as 20% drowsiness, the same bar the dart already fills). Use time: `Item.ContinueUsePrimary` prefix/postfix multiplies `usingTimePrimary` ×1.65 during the call for a chaser's blowgun. That is the only place the cast time is used (`Item.cs`). **Unverified:** the blowgun's vanilla `usingTimePrimary` (prefab data). If it's 0 (instant shot), there is nothing to lengthen; the log prints "vanilla use time …" on the first shot. `BlowgunCooldownSeconds` 72.
- **Dash lift:** Scout's Initiative = `Amulet_SuperJump` / `DoubleJumpAmulet`, whose power is an extra jump with `jumpMult` 2 (`CharacterMovement.JumpRpc`: `part.AddForce(up * jumpImpulse * jumpMult, Acceleration)`). The dash applies `up * jumpImpulse * 2 * sqrt(0.5)` the same way (height ∝ kick², so half the height), sets `isJumping` and resets `sinceGrounded`/`sinceJump` like a jump. The mid-air float now only stops falling, not rising.
- **Fall cancel:** if the runner falls at ≥ 8 m/s (`avarageVelocity.y`; PEAK's landing-speed damage starts at 10 m/s, `MaxVelDmg`), downward speed of every part ×0.2 and `sinceGrounded`/`sinceJump` ×0.2, before the dash and lift.

**Test checklist (1.8.3, second list)**
- [ ] No explosion sounds at all; mandrake waves every 45 s (Shore, Tropics/Roots only).
- [ ] Chaser near a screaming mandrake gets a bit less drowsy than vanilla; runner gets none.
- [ ] Two captures in one leg: log "capture 2 this leg: gem boost x1.034"; gem boost noticeably same length. Next leg: back to x1.
- [ ] Capture rush: a second capture during it doesn't lengthen it beyond 4 s.
- [ ] Petrify up via gem/dash, finish the leg: petrification gone for everyone (log "petrification cleared").
- [ ] Blowgun: no smoke on the hit runner; runner +20% drowsy; shot takes longer to charge (check the "vanilla use time" log line); cooldown 72 s.
- [ ] Dash on flat ground: small hop up + 7 m. Dash while falling off a cliff: fall visibly slows (log "falling at … m/s"), landing hurts much less.

### 1.8.4 (2026-10-14)

- **Capture needs the cursor:** `TagSystem.CaptureTarget` had a fallback that, with no runner under the look ray, picked the capturable runner nearest the middle of the view, and up close (< 0.9 m from the camera) in any direction. That is why captures worked without looking. Removed: only the first thing the look ray hits (ignoring the chaser's own body and held item) counts, and it must belong to a runner. A hold that has started still follows the runner (the 1.8.0 request), so the cursor only matters for starting it. `CaptureRange` 1.75.
- **Frozen in mid-air, no regen:** `FreezeSuspendPatch.FixedUpdatePostfix` sets `sinceGrounded = 0` every step so the hold doesn't count as a fall. `Character.CanRegenStamina` treats `sinceGrounded <= 0.2` as standing, so stamina refilled. New prefix on `CanRegenStamina` returns false while the local player is suspended. Frozen on the ground or on a wall, regen is unchanged (the freeze already stops stamina from dropping).
- **Safe zone wall:** `UI/SafeZoneWall`, every client, shown while `RoundState.LegComplete` (role window), hidden as soon as the next leg starts. Placed 1.5 s into the role window around the campfire nearest the local player (the host gathers everyone there). The zone is a 3D sphere (`SafeZoneSystem.SafeZoneAt`: distance ≤ `CampfireSafeRadius`), so per direction (96 segments) the wall stands where the sphere meets the ground: raycast down on `TerrainMap`, shrink the horizontal radius by the height difference, 4 iterations. Generated mesh: a bright rim at the ground plus a 3.5 m wall fading upwards, pulsing slightly. Shader `Sprites/Default` (vertex colour, transparent, depth-tested), falling back to `UI/Default` with `unity_GUIZTestMode` = LessEqual; the log says which (or that none was found). Not an actual copy of PEAK's biome barrier: I didn't find a script for it in the decompiled code (it's likely a shader/material in the scene), so this is our own look in the same spirit. **Unverified:** that the shader is in the build, that it reads well in fog/at night, and that `TerrainMap` hits every ground type around campfires.
- **Settings page:** `HostMenu` gets a SETTINGS page (same panel, the main buttons hidden) with -/+ rows for the six `ModConfig.Configurable` keys (steps: head start 5 s, boost 1 s, runners per chaser 1, zombie start 30 s, wave cooldown 15 s, lifetime 15 s). Setting `ConfigEntry.Value` saves the file and fires `SettingChanged` -> `ConfigSync.Publish`, so clients get it at once. Values are still reset on a version change.
- **Fresh start at the airport:** new `RoundManager.FreshStart` event, raised on every client when the round state goes back to Idle (the host publishes Idle on any airport return: menu, game over, run end). Clears roles, role choices, freeze state + mid-air hold, rewards, zombies, the last lit segment and campfire, flags, and through the event: chaser gem/capture/snowball state, blowgun and dash cooldowns, dash fall window, leg loadout records, mandrake timers, tag debounce/hold, role-swap snapshot, capture indicator. The round counter keeps counting (so leg keys never repeat). Unclear what the user saw going wrong after an airport restart; if something still carries over, the log line "back in the airport: everything reset" shows the reset ran.

**Test checklist (1.8.4)**
- [ ] Chaser right next to a runner, looking away: no CAPTURE prompt. Centre dot on the runner within 1.75 m: prompt appears.
- [ ] Chaser frozen mid-jump: hangs in the air, stamina bar doesn't refill until the freeze ends.
- [ ] End a leg: yellow wall around the campfire (log "wall shader: …", "wall shown"), roughly 15 m out, following the ground; gone when the next leg's reveal starts.
- [ ] Host menu -> SETTINGS: -/+ change values; a client's log shows the synced value; next leg uses the new head start. Reset to defaults works.
- [ ] Play a leg or two, restart at the airport: role menu back to RUNNER, no cooldowns or petrification carried, new run starts at leg 1 with a fresh role draw (log "everything reset for a fresh start").

**1.8.4, luggage (2026-10-14)**

- **Excluded from all luggage:** `LuggageExcludedItems` = "Flare, Compass, Pirate Compass" + "Warp Compass" (exact names from the item list in the log: `Flare`, `Compass`, `Pirate Compass` = "Pirate's Compass", `Warp Compass`; the hidden `Cheat Compass` variants never spawn anyway). Done in `Spawner.GetObjectsToSpawn` (postfix) for every luggage during a round: the spot is rerolled with `LootData.GetRandomItems(luggage.GetSpawnPool(), 1, …)` (up to 8 tries), else left empty. Luggage only, not a ban: flares and compasses found elsewhere still work, and the gem reveals' flare smoke still uses the flare prefab.
- **New runner luggage items:** `Lantern_Faerie` (Faerie Lantern), `RopeShooterAnti` (anti-rope cannon), `Anti-Rope Spool`. "A little lower than snowballs" = 0.75× a snowball's share: snowball (and milk, berrynana, energy drink) stay at 0.383 / 4 ≈ 9.6% per item slot; each new item ≈ 7.2%. So about 60% of regular luggage slots are now runner extras (was 38%). Lower `LuggageExtraChance` if that's too much.

**Test checklist (1.8.4, luggage)**
- [ ] Open lots of regular luggage: no flares, compasses or pirate's compasses (log "excluded item(s) rerolled"); faerie lanterns and anti-rope cannons/spools show up, a bit rarer than snowballs.
- [ ] Clown luggage still food/healing only.
- **Runner-only:** during a round `ItemRules.Refusal` refuses any `LuggageRareExtraItems` item to anyone who isn't a runner ("Only runners can use this item"), so chasers can't pick them up (real and scenery items, host-checked too) or use them. Outside a round they're vanilla. A runner who becomes a chaser drops everything anyway (RoleSwap). An anti-rope a runner has already placed is part of the world; chasers can climb it like any rope.
- [ ] Chaser can't pick up a faerie lantern / anti-rope cannon / anti-rope spool ("Only runners can use this item"); a runner can. No warp compasses from luggage.

**1.8.4, safe zone wall shown all the time (2026-10-14)**

- Feedback: the wall only showed at the end and start of legs (by design: role window only). Now shown for the whole round (`RoundManager.IsActive`: from the first reveal until back in the airport), around **every** loaded campfire that is a safe zone (all of them; only lit ones if `SafeZoneRequiresLit`). Rechecked once a second; walls follow the radius setting and disappear with their campfire (unloaded segment).
- Far-away terrain may not have its colliders yet when a wall is first built: any direction that finds no ground marks the wall incomplete, and it's rebuilt every 5 s, at most 3 times (a real drop never finds ground, and then the wall stays at the campfire's height there).
- [ ] During a chase, the next campfire's wall is visible from a distance (and the one behind); the log lists each wall once ("wall around the campfire at …").

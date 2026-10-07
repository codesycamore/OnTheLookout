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

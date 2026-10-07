# Feasibility: "Chasers vs Runners" for PEAK

Game build **25739797 (v2.6.b)**. All `File.cs:line` references are relative to `decompiled/Assembly-CSharp/`, which is local only.
Verdicts are based on reading the code. **Nothing here has been tested in-game yet.** Items that need a live test are listed in `OPEN_QUESTIONS.md`.

**Overall: GO.** Every rule has a concrete hook point. No rule is "Not feasible". The riskiest one is the freeze (rules 3–5), because it combines a networked look-check with movement suppression during climbing.

---

## Part B: Map of core systems

### B1. Player character, movement and input

- **`Character`** (`Character.cs`) is the per-player root `MonoBehaviourPun`. Useful members:
  - `Character.localCharacter` (static, :118)
  - `Character.AllCharacters` (static list, :128)
  - `IsLocal` (:241)
  - `Center` (torso position, :273)
  - `view` / `photonView`
  - `refs.{input, items, afflictions, ragdoll, ...}`
  - `data` (`CharacterData`)
- **Input pipeline:** `CharacterMovement.Update` (`CharacterMovement.cs:176`) calls `character.input.Sample(character.CanDoInput())`, **but only when `character.IsLocal`** (:183-191).
  - `CharacterInput.Sample(bool playerMovementActive)` (`CharacterInput.cs:212`) fills `movementInput`, `jumpWasPressed`, `sprint*`, `usePrimary*`, `useSecondary*`, `interact*` and so on.
  - Climbing, jumping and item use all read those fields.
- **Movement is owner-simulated** and replicated through `CharacterSyncer`/`CharacterSyncData`. That means **a freeze has to be enforced on the frozen player's own client.**
- **Recommended freeze mechanism:** a Harmony **postfix on `CharacterInput.Sample`**. When the local player is frozen, zero out `movementInput` and `lookInput` if we want, and clear jump, sprint, use and interact.
  - This is narrow (one method) and covers walking, climbing, jumping and item use.
  - Hide-and-PEAK (prior art) additionally pins the torso's Y position and zeroes rigidbody velocity, so a frozen player hangs in place instead of sliding. We might need the same. See the test checklist.
- `Character.CanDoInput()` (:1714) only checks GUI windows. It isn't a good freeze flag.

### B2. Network identity, enumeration and host

- Players: `PlayerHandler.GetAllPlayerCharacters()` (`PlayerHandler.cs:68`) and `Character.AllCharacters`. Bots are listed separately in `Character.AllBotCharacters`.
- Stable ID: the **Photon `ActorNumber`**, taken from `character.view.Owner.ActorNumber`.
  - `PlayerHandler.TryGetCharacter(int actorID, out Character)` (:301) and `PlayerHandler.GetPlayer(int actorNumber)` (:218) map an ID back to a character or player.
  - A platform user ID is also available through `PlayerHandler.GetUserId(actorNumber)` (:223). That's useful if a player reconnects and gets a new actor number.
- Host: `PhotonNetwork.IsMasterClient` or the game's own wrapper `NetCode.Session.IsHost` (`OrbFogHandler.cs:211`).
- Shared clock: `PhotonNetwork.ServerTimestamp`, which the game already uses (`ReconnectHandler.cs:31`).
- **Look direction is replicated.** `CharacterSyncData.lookValues` is sent at 30 Hz as half-precision values (`CharacterSyncData.cs:25,62`) and interpolated on remote clients (`CharacterSyncer.cs:134-141`). `Character.RecalculateLookDirections()` (:1347) turns it into `data.lookDirection`.
  - **So the host can evaluate every runner's look-check by itself.** That's the best possible outcome for host authority.

### B3. Item system

- `Item` (`Item.cs`): `itemID` (ushort, :205), `UIData.itemName` (:41), the `itemTags` flags enum (:20; includes `ScoutAmulet = 0x200`), and `ItemAction` components such as `Action_ModifyStatus`, `Action_HealingGem` and `Action_StrangeGem`.
- Lookup: `ItemDatabase.TryGetItem(ushort, out Item)` and `TryGetItem(string nameOnFile, out Item)` (`ItemDatabase.cs:77,82`).
- **Pickup is host-validated:**
  1. `Item.Interact` (:578) sends `RequestPickup` to `RpcTarget.MasterClient` (:591).
  2. `Item.RequestPickup` (:606) runs on the host. It calls `player.AddItem` and then either accepts (`OnPickupAccepted`) or rejects (`DenyPickupRPC`).
  - **This is the ideal host-authoritative choke point for item bans and restrictions.**
- Use: `CharacterItems.DoUsing` (`CharacterItems.cs:197`) runs on the local client and calls `currentItem.StartUsePrimary` and related methods.
- Granting items: `Player.AddItem(ushort itemID, ItemInstanceData, out ItemSlot)` (`Player.cs:87`) is **host-only** (it returns false if not master) and syncs the inventory itself.
- Other item entry points that bypass pickup: feeding (`Character.FeedItem` / `GetFedItemRPC`, :1640-1646) and `CharacterItems.SpawnItemInHand` (:1102).

### B4. Fog and rising hazard

There are two implementations, and both apply damage **client-locally to `Character.localCharacter`**.

- **`OrbFogHandler` + `FogSphere`** (the shrinking-sphere fog):
  - Speed is a public field, `OrbFogHandler.speed = 0.3f` (`OrbFogHandler.cs:11`).
  - `Move()` (:188) shrinks `currentSize` locally **on every client**. The host also re-syncs size every 5 s through `RPCA_SyncFog` (:116-131).
  - Damage happens in `FogSphere.SetSharderVars()` (`FogSphere.cs`): if the local player is outside the sphere, it calls `AddStatus(Cold, 0.0105*dt)` (or Injury for skeletons) and sets `data.isInFog`.
- **`Fog`** (the legacy rising plane): `fogSpeed = 0.4f` (`Fog.cs:8`). Damage is in `MakePlayerCold()` (:89) and also targets `localCharacter`.
- Fog start conditions: `OrbFogHandler.WaitToMove()` / `PlayersHaveMovedOn()` / `TimeToMove()`. The "resting" pause is `PlayersAreResting`, set by `Campfire.ApplyCampfireProtection` (`Campfire.cs:225`).
- Which of the two is active in a normal run on this build is **unverified**. We'll patch both.

### B5. Campfires

- `Campfire` (`Campfire.cs`) has a private static registry `ALL_CAMPFIRES` (:31), `moraleBoostRadius` (:57), `PlayerCharactersInRadius(float)` (:358), and `advanceToSegment` (:27).
- There's no trigger volume. Proximity is checked with `Vector3.Distance` against `Character.Center`, and we can do the same thing.
- `MapHandler.CurrentCampfire` (`MapHandler.cs:279`) and `PreviousCampfire` (:251) give the relevant fires.

### B6. Biome and level progression

- Lighting a campfire calls `Light_Rpc` (`Campfire.cs:462`, sent to All), which calls **`MapHandler.GoToSegment(Segment)`** (`MapHandler.cs:578`). That's the "biome changed" hook, and it runs on every client. A postfix that acts only on the master is enough.
- `enum Segment : byte { Beach, Tropics, Alpine, Caldera, TheKiln, Peak, Void }` (`Segment.cs`). Current segment: `MapHandler.CurrentSegmentNumber` (:334).
- Run start: `RunManager.StartRun()` (`RunManager.cs:152`). Hide-and-PEAK patches this method for the same purpose.

### B7. Summit

- `MountainProgressHandler.IsAtPeak(Vector3)` (`MountainProgressHandler.cs:198`) returns `position.z > lastProgressPoint.z`. It's cheap enough for the host to poll for every runner.
- The vanilla win check is `Character.CheckWinCondition` (`Character.cs:946`): alive and (at peak or on the helicopter rope).
- The run ends through `RPCEndGame` (:893) and `GlobalEvents.OnSomeoneWonRun` / `OnRunEnded`.

### B8. Death, downed and revive

- States are `data.passedOut`, `data.fullyPassedOut` and `data.dead`.
  - `RPCA_PassOut` (:993) puts a player into the downed state.
  - `RPCA_Die` (:736) kills them. It drops all items, spawns a skeleton, and the player then spectates.
- Events with no patch needed: `GlobalEvents.OnCharacterDied`, `OnCharacterPassedOut`, `OnCharacterSpawned`, `OnPlayerConnected/Disconnected` (`GlobalEvents.cs`).
- **Revive is supported by the game:**
  - `RPCA_ReviveAtPosition(Vector3 pos, bool applyStatus, int statueSegment)` (`Character.cs:1802`) clears the dead state and warps the player. It's sent to `RpcTarget.All` on the target character's view.
  - The game's own debug `Character.Revive()` (:1766) and Hide-and-PEAK (`Plugin.cs:112`) both use it.
- The vanilla game ends when **everyone** is dead (`Character.CheckEndGame`, :838). Our mode needs its own end conditions.

### B9. Spawning items and luggage

- "Ancient luggage" is a loot pool, `SpawnPool.LuggageAncient = 0x8000` (`SpawnPool.cs:22`), not an item.
- `LootData.GetRandomItems(SpawnPool, int count, ...)` (`LootData.cs:170`) rolls prefabs from a pool.
- The host can spawn them with `PhotonNetwork.Instantiate("0_Items/" + prefab.name, pos, rot, 0)`, the same pattern the game uses in `Breakable.cs:99` and `CharacterItems.cs:530`.
- Spawning an actual luggage **object** would need its prefab path, which is **unverified**.

### B10. UI

- `GUIManager.instance` has `SetHeroTitle(string, AudioClip, bool)` (`GUIManager.cs:484`), the big centre-screen title. It's good for announcements like "YOU ARE A CHASER".
- TMP text references are exposed, for example `interactPromptText`.
- For the overlay (role label, freeze or cooldown, countdown) there are two options: our own `Canvas` + `TextMeshProUGUI`, or **PEAKLib.UI** (MIT). IMGUI `OnGUI` is acceptable for prototypes only.

---

## Part C: Per-rule verdicts

| # | Rule | Verdict | Evidence | Hook plan |
|---|---|---|---|---|
| 1 | X random chasers at round start | **Feasible** | `RunManager.StartRun` (`RunManager.cs:152`); actor numbers (B2) | Host postfix on `RunManager.StartRun`, or a host button/hotkey. It shuffles `PhotonNetwork.PlayerList`, takes `ChaserCount` (clamped to players−1), and writes roles to the room property `otl.roles`. |
| 2 | Runner head start | **Feasible** | `CharacterInput.Sample` (:212) runs only on the owner | Host writes `otl.round.start = ServerTimestamp`. Each chaser's client blocks its own input until `start + HeadStartSeconds`. The host also rejects tags during the head start. |
| 3 | Look-at-chaser freezes chaser | **Feasible with caveats** | `CharacterSyncData.lookValues` replicated (:25); `data.lookDirection`; `HelperFunctions.terrainMapMask` (:27) | Host, about every 0.1 s, for each live runner R and each chaser C within `FreezeRange`: check `angle(R.data.lookDirection, C.Center − R.head) < FreezeConeDegrees` **and** that `Physics.Linecast(R.head, C.Center, terrainMapMask)` is clear. On success, set `frozenUntil[C]`. Caveats: (a) "looking at" needs a cone angle, so this adds a config key; (b) remote look values are half precision and lag about 33–100 ms; (c) the right layer mask for occlusion needs an in-game test. |
| 4 | Freeze doesn't stack | **Feasible** | Host-owned state | Host ignores freeze triggers while `now < frozenUntil[C]`. A single authority makes this trivial. |
| 5 | Per-chaser cooldown | **Feasible** | Host-owned state | `cooldownUntil[C] = frozenUntil[C] + FreezeCooldownSeconds`. Triggers are ignored until then. |
| 6 | Chasers can only use healing items | **Feasible with caveats** | Host-run `Item.RequestPickup` (`Item.cs:606`); `CharacterItems.DoUsing` (:197) | (1) Host prefix on `RequestPickup`: if the picker is a chaser and the item isn't allowed, send `DenyPickupRPC` and skip. (2) Client-side backup: prefix `DoUsing` to block use of non-allowed items. (3) Strip inventory on becoming a chaser (`Player.RPCRemoveItemFromSlot`, `Player.cs:202`). Caveat: "healing" isn't a game category. We'd auto-detect `Action_ModifyStatus{Injury, changeAmount<0}` or `Action_HealingGem`, plus a configurable allowlist `ChaserAllowedItems`. Feeding and backpacks are edge cases. |
| 7 | Faster fog, affects runners only | **Feasible** | `OrbFogHandler.speed` (:11); `Fog.fogSpeed` (:8); damage in `FogSphere.SetSharderVars` and `Fog.MakePlayerCold` targets `localCharacter` | Speed: every client multiplies the speed fields by the host-synced `FogSpeedMultiplier` when the fog handler starts (the host 5 s resync keeps them aligned). Exemption: on a chaser's client, skip the damage. `SetSharderVars` mixes shader setup with damage, so the options are a **transpiler** that wraps the `AddStatus` call, or a postfix that subtracts the same Cold and clears `isInFog`. `Fog.MakePlayerCold` can be a simple skip prefix. Side effects: chasers count toward `PlayersHaveMovedOn` and `EveryoneInRange` (campfire rest), so lagging chasers could delay the fog. We might need to filter chasers out (see open questions). |
| 8 | Campfires are safe zones | **Feasible** | `Campfire` registry; distance-based (B5) | Host tag check: reject a tag if the runner is within `CampfireSafeRadius` of any active `Campfire` (cache the result of `FindObjectsByType<Campfire>`, or read `ALL_CAMPFIRES` via `AccessTools`). Whether it counts only when lit is a design question. |
| 9 | Captured or dead runners convert to chasers in later biomes | **Feasible with caveats** | `GlobalEvents.OnCharacterDied`; `MapHandler.GoToSegment` (:578); `RPCA_ReviveAtPosition` (`Character.cs:1802`) | Capture means the host sends `RPCA_Die` to the runner (decided). Every dead runner goes into a pool, whether captured or killed by the environment. Passed-out runners are **not** added. On `GoToSegment` (host only, after the segment coroutine finishes), pick **one** random runner from the pool, flip their role in `otl.roles`, and send `RPCA_ReviveAtPosition(spawnPos, false, -1)`. Caveats: revive inside the segment-transition coroutine is **untested**. Also, `CheckEndGame` (:838) ends the run if *everyone* is dead. That's fine because chasers are alive. |
| 10 | First runner into **each campfire safe zone** gets ancient-luggage loot (decided 2026-10-07) | **Feasible** | `Campfire` positions (B5); `MountainProgressHandler.IsAtPeak` (:198); `LootData.GetRandomItems` (:170); `PhotonNetwork.Instantiate("0_Items/…")` | Host polls runners against the current campfire's `CampfireSafeRadius` (and `IsAtPeak` for the summit). The first runner at each campfire triggers `GetRandomItems(SpawnPool.LuggageAncient, n)`, and the host spawns those items at the runner's feet. The host tracks which campfires have paid out, so each pays out once. |
| 11 | Amulets and gems banned for everyone | **Feasible** | `Peak.AmuletBase`, `Action_StrangeGem`, `Peak.Action_HealingGem`, `ItemTags.ScoutAmulet` | Same `RequestPickup` host prefix: deny if the item matches the ban set. Matching is by component type (`AmuletBase`, `Action_*Gem`), tag, or name in `BannedItems`. Optionally, the host destroys banned items when they spawn (postfix `Spawner.SpawnItems`, `Spawner.cs:220`) so they never appear. Which in-game items count as "gems" needs an in-game item dump. |
| – | Tag/capture: a chaser's body collides with a runner (decided) | **Feasible** | `Bodypart.OnCollisionEnter` (`Bodypart.cs:239`, private). Prior art: Hide-and-PEAK's host-side postfix on the same method | Host-only postfix. If the two bodies belong to a chaser and a runner, the chaser isn't frozen, the head start is over, and the runner isn't in a safe zone, the host sends `RPCA_Die` to the runner. Debounce per runner. |

---

## Part D: Networking design

**What existing mods do**

- **Hide-and-PEAK** (GPL-3.0, learn only): Photon **player custom properties** for team (`"Team"`) and stats, **RPCs** on a `MonoBehaviour` that it adds to the **host's Character GameObject** to reuse that PhotonView, and room-like flags on the master's properties.
- **Vasodilation**: host config in a **room property**; clients adopt it on join and on change.
- **PEAKLib.Core** (MIT): `Networking.HostPluginGuids` and `AddHostHasPluginListeners`, which tell a client whether the host has a given plugin GUID.
- **SpongePEAKLib**: a mod handshake through player properties.
- **PhotonCustomPropsUtils** (Snosz): a property-sync helper. Its license is **unverified**.

**Proposal: room properties for state, one event code for transient messages.** Only the master writes.

| Data | Transport | Key | Why |
|---|---|---|---|
| Protocol/mod version | player prop, set by each client | `otl.ver` | Host compares on join. |
| Host config snapshot | room prop | `otl.cfg` (serialized) | Late joiners get it automatically, and it survives host migration. |
| Round state + start time | room prop | `otl.round` = {state, startTs} | Clients compute countdowns locally from `ServerTimestamp`. |
| Roles | room prop | `otl.roles` = {actor → role} | Durable and rejoin-safe. Keyed by actor, with the userId mapped on rejoin. |
| Freeze/cooldown | room prop | `otl.frz` = {actor → frozenUntil, cdUntil} | Timestamps instead of on/off flags, so there's no "unfreeze" message to lose. |
| Tag, reward, conversion notices | `PhotonNetwork.RaiseEvent` | one custom code (e.g. 167; **never 18**, avoid ≥200) | Cosmetic and transient. No PhotonView needed. |
| Kill/revive/deny pickup | the game's own RPCs (`RPCA_Die`, `RPCA_ReviveAtPosition`, `DenyPickupRPC`) | n/a | Reuses existing replication. |

**Handshake.**
- Each client publishes `otl.ver`.
- The host checks it in `OnPlayerEnteredRoom` / `OnPlayerPropertiesUpdate`. If it's missing or mismatched, the host shows a warning and, if configured, kicks with `PlayerHandler.Kick(actorNumber)` (`PlayerHandler.cs:185`).
- Clients use PEAKLib's `HostPluginGuids` to disable themselves if the host lacks the mod.

**Desync risks and edge cases**

1. **Host migration.** PUN switches master on its own. Room props persist, so the new master's `RoundManager` reloads state from `otl.*` and continues. Timers are absolute timestamps, so nothing restarts. Needs a test.
2. **Join mid-round.** The player gets room props on join. The default role is runner, or spectator (open question).
3. **Leave mid-round.** The host removes the actor from roles. If the last chaser leaves, the host promotes a dead runner or ends the round.
4. **Rejoin with a new actor number.** Map roles by `PlayerHandler.GetUserId` as well.
5. **Freeze latency.** The chaser's client applies the freeze about one round trip after the host decides. The chaser may move a few frames. That's acceptable.
6. **Fog drift.** Speed is applied on every client. A client without the synced multiplier would visibly drift for up to 5 s between resyncs.
7. **Trust.** Freeze and fog exemption are enforced client-side by necessity (owner-simulated movement), so a modified client could ignore them. Acceptable for a friends-only mod.
8. **Game updates.** Private members (`Bodypart.OnCollisionEnter`, `Campfire.ALL_CAMPFIRES`, `CharacterInput.Sample` is `internal`) may change. Every patch must resolve through `AccessTools` and fail soft (see BUILD_PLAN).

---

## Risks (ranked)

1. **Freeze feel (rules 3–5).** Zeroing input mid-climb may let the chaser slide or fall, or keep draining stamina. We may need Hide-and-PEAK's approach of pinning the torso and zeroing velocity. Look-check tuning (cone, occlusion mask, half-precision look) also needs play-testing.
2. **Fog damage exemption.** It needs a transpiler or a compensation postfix on a method that also drives shaders. Chasers also affect the fog-start and campfire-rest conditions.
3. **Revive timing during a segment transition (rule 9).** Untested. `GoToSegment` runs a coroutine that deactivates segments.
4. **Game update churn.** The game updated the day before this survey. The EasyBackpack mod broke on the 2026-08-12 update when a field was removed. Expect to re-verify hooks after every patch.
5. **The "healing" and "gem" item definitions** need an in-game item dump to finalise.

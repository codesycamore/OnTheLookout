# CLAUDE.md — Peak "Chasers vs Runners" Mod

## Mission (read this first)

This project is in a **feasibility and research phase**. Do NOT start building the full mod yet.
Your job right now:

1. Use https://peak.modding-community.com/ as a reference on understanding how to mod for PEAK.
2. Verify, against the real game code, which of the planned rules are feasible.
3. Research what existing Peak mods and the modding community have already done that we can learn from or reuse.
4. Produce written findings (see "Deliverables") and a recommended build plan.

Be honest about uncertainty. If something can't be confirmed from the code or a source, say "unverified" rather than guessing. Never invent class names, method names, or APIs. Every claim about the game's internals must cite the decompiled file/class/method it came from.

## About the developer

- Chris: C# and Unity background, building toward AI engineering. Limited prior game-modding experience.
- Prefers practical, working code and concrete steps over academic explanation.
- Explain Harmony/BepInEx/Photon specifics briefly when first used; assume Unity/C# fluency.

## The game and the mod concept

**Game:** Peak, a cooperative Unity climbing game. Players climb a mountain across multiple biomes. A rising fog hazard, campfires (rest points), items, and ancient luggage exist in the game.

**Mod concept:** A hide-and-seek / tag style game mode. X randomly chosen players are **chasers** who try to tag or capture **runners** before the runners reach the summit.

### Rule set (all values must be configurable, never hardcoded)

| # | Rule | Config key (proposed) |
|---|------|-----------------------|
| 1 | X players are randomly selected as chasers at round start | `ChaserCount` |
| 2 | Runners get a head start before chasers can move | `HeadStartSeconds` = 20 |
| 3 | **Runner freeze perk:** if a runner looks at a chaser within a set distance, the chaser is frozen | `FreezeRange`, `FreezeDuration` = 5 |
| 4 | Freeze timer is per chaser and does NOT stack. Multiple runners looking at the same chaser do not extend it. A new look during an active freeze does nothing | (behavior) |
| 5 | Per-chaser cooldown after a freeze ends before any runner can freeze that chaser again (prevents infinite stun-locking) | `FreezeCooldownSeconds` |
| 6 | Chasers cannot use items except healing items | `ChaserAllowedItems` |
| 7 | Fog rises faster than normal and affects only runners, not chasers | `FogSpeedMultiplier` |
| 8 | Campfires are safe zones: chasers cannot tag runners inside them | `CampfireSafeRadius` |
| 9 | When runners are captured or die, one is randomly selected to become a chaser in later biomes | `ConvertOnBiomeChange` |
| 10 | The first runner to reach the summit gets an ancient luggage reward | (behavior) |
| 11 | Amulets and gems are banned for all players | `BannedItems` |

More rules will be added later. Architecture must make adding a rule cheap: one module per rule, a central config, a central role manager.

## Assumed tech stack (VERIFY, do not trust blindly)

- BepInEx plugin framework (check version needed: Mono vs IL2CPP; determine which Peak uses)
- HarmonyX for runtime patching
- Photon (PUN 2 or Fusion?) for networking. **Confirm which**
- Distribution via Thunderstore, local testing via r2modman or manual BepInEx install
- Target a specific game version; record it

Everything in this list is an assumption until confirmed from the installed game directory.

## Architecture requirements (non-negotiable design constraints)

1. **Host-authoritative.** The lobby host decides roles, freezes, tags, conversions, and rewards, then replicates to clients. Clients never decide game outcomes.
2. **All players need the mod.** Plan for version/handshake checks so mismatched clients are detected.
3. **Data-driven.** All tunables live in a config class (BepInEx `ConfigEntry`), ideally host-synced to clients.
4. **Modular.** `RoleManager`, `FreezeSystem`, `TagSystem`, `FogSystem`, `SafeZoneSystem`, `ItemRestrictions`, `RewardSystem`, `RoundManager`. Each is independent and can be toggled.
5. **Minimal, targeted Harmony patches.** Prefer postfix/prefix on narrow methods. Document every patch with the target method and why.
6. **Fail safe.** If a hook can't be found after a game update, log a clear error and disable only that module, not crash the game.

## Feasibility verification tasks

Work through these in order. For each, answer: **Feasible / Feasible with caveats / Not feasible / Unverified**, with evidence (file, class, method) and the proposed hook point.

### A. Environment
- Locate the Peak install directory and identify the Unity backend (Mono vs IL2CPP). If IL2CPP, note what that does to the plan.
- Identify the main game assemblies (likely `Assembly-CSharp.dll`) and the networking library in use.
- Record the game version and build ID.

### B. Core systems to map (decompile with ILSpy/dnSpy output or `ilspycmd`)
- Player character class, movement/input pipeline, and how to suppress movement for a freeze (input block vs physics vs state flag).
- Network identity of players: how to enumerate players, get a stable player ID, and tell who is the host.
- Item system: where item use is triggered and how items are identified (for chaser item restriction and banning amulets/gems/items by type).
- Fog/rising hazard: how it is driven, how its speed can be changed, and how its damage/effect is applied per player (needed for "runners only").
- Campfires: how they are represented, whether there is a trigger volume or a player-near-campfire check we can reuse.
- Biome/level progression: is there an event or state change we can hook for "biome changed"?
- Summit: is there an existing "reached the top" / win trigger we can hook?
- Death/downed/knocked-out state: how a "captured" player could be represented (downed? dead? spectator?), and how a dead player could be revived as a chaser.
- Spawning/granting items: how to give a player an ancient luggage (or spawn it).
- Existing UI layer: how to add a simple overlay (role label, freeze/cooldown indicators, head-start countdown).

### C. Per-rule feasibility (map rules 1-11 above to the findings)
Give a short verdict and hook plan for each rule. Pay special attention to:
- **Rule 3-5 (freeze):** line-of-sight/look check (camera forward dot product + raycast), per-chaser timer + cooldown state on the host, and how to replicate the frozen state to all clients.
- **Rule 7 (fog):** can fog exposure be exempted per player?
- **Rule 9 (conversion):** can a dead player be returned to play as a chaser, and when?

### D. Networking
- Determine how existing mods send custom messages (custom Photon events, RPCs, room/player custom properties).
- Propose how to replicate: role assignment, freeze state, round state, conversions.
- Identify desync risks and edge cases (host migration, player join/leave mid-round).

## Research tasks (existing mods and community knowledge)

Search the web (Thunderstore, GitHub, Nexus, Discord-linked wikis, modding guides) and report on:

1. **Peak modding basics:** the community's recommended setup, templates, and known pitfalls. Is there an official or community modding API or library?
2. **Open-source Peak mods worth studying.** For each, give: name, link, license, what it does, and what we could learn or reuse. Prioritize mods that:
   - Add game modes, roles, teams, or rounds
   - Change lobby size or networking behavior
   - Add or restrict items, or give items to players
   - Modify fog, hazards, or campfires
   - Add custom UI overlays
   - Detect summit/biome events or player death
3. **Other games with similar mods** (e.g., hide-and-seek or infection modes in other BepInEx/Unity games, like Lethal Company or Content Warning) whose architecture we could borrow, especially role systems and host-authoritative state.
4. **Shared libraries** commonly used for BepInEx networking, config sync, or custom UI that we should adopt instead of reinventing.
5. **Known breaking-update history:** how often Peak updates break mods and how mod authors cope.

**Licensing rule:** note each repo's license. Do not copy code from a repo without a compatible license. Prefer learning patterns and writing our own implementation. Credit sources in `FINDINGS.md`.

## Deliverables

Create these files in a `docs/` folder:

1. `docs/ENVIRONMENT.md`: game version, Unity backend, assemblies, networking library, tooling used.
2. `docs/FEASIBILITY.md`: the per-rule verdict table (rules 1-11) with evidence and hook points, plus the list of risks.
3. `docs/EXISTING_MODS.md`: research findings on existing mods, libraries, and reusable patterns, with links and licenses.
4. `docs/BUILD_PLAN.md`: recommended phased plan (hello-world plugin, role system, networking, then rules in order of risk), with the first 3 concrete next steps.
5. `docs/OPEN_QUESTIONS.md`: anything that needs a human decision or in-game testing.

End with a short summary: overall go/no-go, the single riskiest rule, and what to prototype first.

## Working conventions

- Keep decompiled game code **local only**. Never commit game DLLs, decompiled source, or copyrighted assets to the repo. Add them to `.gitignore`.
- Put decompiled output in a `decompiled/` folder (ignored by git). Reference it by path in findings.
- Keep the mod's own code in `src/`. Use a clear namespace and a plugin GUID like `com.chris.peakchasers` (placeholder).
- Log liberally with a consistent prefix so in-game testing is easy to debug.
- Ask before running anything destructive or installing system-wide tools. Prefer local, reversible steps.
- When something requires in-game testing, write a short test checklist in `docs/OPEN_QUESTIONS.md` instead of assuming the result.
- Don't implement features yet unless asked. Prototype only the minimum needed to prove a risky hook works (for example, a patch that logs when a player reaches the summit).

## Suggested first steps

1. Confirm the game path and Unity backend; set up BepInEx and verify a hello-world plugin loads.
2. Decompile the game assemblies into `decompiled/` and write `docs/ENVIRONMENT.md`.
3. Run the research tasks in parallel with code mapping, then fill in `FEASIBILITY.md` rule by rule.

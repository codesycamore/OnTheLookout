# Build plan

Ordered so the riskiest unknowns are proven first, using throwaway logging patches before any real feature code.

## Architecture (target)

```
src/OnTheLookout/
  Plugin.cs                  // Awake: config, module registry, Harmony
  Core/
    ModConfig.cs             // all ConfigEntry<T> (rules table keys + TagRange, FreezeConeDegrees)
    ConfigSync.cs            // host -> room prop "otl.cfg"; clients read effective values from here
    NetState.cs              // typed wrapper over room/player custom props + RaiseEvent (code 167)
    Handshake.cs             // "otl.ver" player prop; host warns/kicks mismatches; PEAKLib HostPluginGuids
    RoleManager.cs           // otl.roles; IsChaser(Character); role-change events
    RoundManager.cs          // otl.round; start/head-start/end; host-migration resume
    ModuleBase.cs            // Enable/Disable, TryPatch() via AccessTools -> disable self on failure
  Modules/
    FreezeSystem.cs          // rules 2-5 (host look-check + client input block)
    TagSystem.cs             // capture
    SafeZoneSystem.cs        // rule 8 (queried by TagSystem)
    FogSystem.cs             // rule 7
    ItemRestrictions.cs      // rules 6, 11
    ConversionSystem.cs      // rule 9
    RewardSystem.cs          // rule 10
  UI/Overlay.cs              // role, countdown, freeze/cooldown
```

Rules for every module:
- Each module has its own `Enable<Module>` config toggle.
- Each module resolves its patch targets with `AccessTools.Method(...)` at startup. If a target is null, it logs `[OTL][<Module>] hook <Type.Method> not found - module disabled` and disables itself without throwing.
- Patches are applied per module with `harmony.Patch(...)`, not a global `PatchAll`, so one failing hook can't break the others.
- Log prefix is `[OTL]`. Host-side decisions also log the actor numbers involved.

## Phases

> **Status 2026-10-07:** Phases 0–1 skipped by decision. The freeze PoC was proven in-game, then all rules plus the HUD were implemented in one pass (see `src/OnTheLookout/Core`, `Freeze`, `Modules`, `UI`). Next: run the full-mode test plan in OPEN_QUESTIONS.md.

**Phase 0: Hello world on the current build (½ day)**
- Copy `Config.Build.user.props` from the template and point it at the game and the r2modman `Default` profile's plugins folder.
- Build, then launch through r2modman and confirm `Plugin OnTheLookout is loaded!` appears in `LogOutput.log` on **build 25739797**. This also re-confirms that BepInEx and PEAKLib still load after the 2026-10-06 update.
- Decide the plugin GUID (see open questions).

**Phase 1: Hook probes (logging only) (1 day)**
- Add one patch or event subscription per hook. Each one only logs:
  - `RunManager.StartRun`
  - `MapHandler.GoToSegment`
  - `GlobalEvents.OnCharacterDied` / `OnCharacterPassedOut`
  - `Item.RequestPickup` (on the host: item name, ID, components, picker)
  - The host polling `MountainProgressHandler.IsAtPeak` for each character
  - `FogSphere.SetSharderVars` (is it the active fog?)
  - `Fog.MakePlayerCold`
- Also dump `ItemDatabase` (ID, name, ItemAction types) once, to finalise the healing and gem lists.
- Run a 2-player test (two Steam accounts or a friend). Fill in the results in `OPEN_QUESTIONS.md`.

**Phase 2: Core framework (2–3 days)**
- `ModConfig`, `ConfigSync`, `NetState`, `Handshake`, `RoleManager`, `RoundManager`, and a minimal overlay that shows your role.
- A host hotkey starts the round and assigns roles.
- Test: roles show up on every client, a late joiner sees the correct state, and when the host quits, the new master continues.

**Phase 3: Rules, highest risk first**
1. **Freeze + head start (rules 2–5).**
   - Client input block via postfix on `CharacterInput.Sample`. Test it with a debug hotkey first: does it hold on a wall?
   - Then the host look-check, then the freeze and cooldown timestamps.
2. **Fog (rule 7).** Speed multiplier, then the chaser damage exemption (transpiler or compensation).
3. **Tag + safe zones (rule 8 + capture).**
4. **Conversion (rule 9).** Revive dead runners as chasers after `GoToSegment`.
5. **Item restrictions + bans (rules 6, 11).**
6. **Reward (rule 10).**

**Phase 4: Polish and ship**
- PEAKLib.ModConfig menu, overlay polish, README, and a Thunderstore package listing the target build.
- An "update checklist" script that re-decompiles and diffs the hooked methods.

## First 3 concrete next steps

1. **Create `Config.Build.user.props`** from the template. Set `GameDirectory` to `Z:\SteamLibrary\steamapps\common\PEAK` and the plugins directory to `%APPDATA%\r2modmanPlus-local\PEAK\profiles\Default\BepInEx\plugins`. Then run `dotnet build` and launch through r2modman to confirm the hello-world log line on build 25739797.
2. **Add the Phase-1 probe patches.** These are logging-only postfixes on `RunManager.StartRun`, `MapHandler.GoToSegment`, `Item.RequestPickup`, `FogSphere.SetSharderVars` and `Fog.MakePlayerCold`, plus `GlobalEvents.OnCharacterDied` and an `ItemDatabase` dump. Play one solo run to the first campfire and collect the log.
3. **Prototype the freeze input block** with a debug hotkey: postfix `CharacterInput.Sample` to zero all input while toggled. Test it while walking, mid-jump, and **mid-climb**. This answers whether pinning the torso and velocity is also needed, which is the biggest unknown in the riskiest rule.

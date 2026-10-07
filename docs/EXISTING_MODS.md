# Existing mods, libraries and community knowledge

Researched 2026-10-07. **Licensing rule:** we copy no code from GPL or unlicensed repos. We learn patterns and write our own. Our own `LICENSE` file is still the template placeholder (`TODO: Choose an appropriate license`), so that decision is open.

## 1. PEAK modding basics

- **Community wiki:** https://peak.modding-community.com/
  - Getting started, project template, packaging and publishing, and "Useful BepInEx APIs".
  - The wiki lists **MonoMod or HarmonyX** for hooks. It calls MonoDetour "not fit for production usage yet".
  - The "Writing your first hook" and "Custom UI" pages are still TODO placeholders. Custom UI points to PEAKLib.UI and ModConfig as examples.
- **Template:** the PEAKModding project template, which **this repo already uses**. It provides BepInEx 5, `netstandard2.1`, `BepInAutoPlugin`, and ThunderPipe packaging. The wiki defers to https://lethal.wiki for general patching and config tutorials.
- **Community hub:** PEAK Modding Discord (https://discord.gg/SAw86z24rB), with a `#peak-lib` channel. GitHub org: https://github.com/PEAKModding
- **Loader:** BepInExPack_PEAK on Thunderstore. The usual setup is an r2modman / Thunderstore Mod Manager profile, which is what this machine has.

## 2. Open-source PEAK mods worth studying

| Mod | Link | License | What it does | What we learn or reuse |
|---|---|---|---|---|
| **Hide and PEAK** (glarmer) | https://github.com/glarmer/Hide-and-PEAK · [Thunderstore](https://thunderstore.io/c/peak/p/glarmer/Hide_and_PEAK_/) | **GPL-3.0** (learn only, unless we also license as GPL-3.0) | Hide-and-seek mode: hiders and seekers, configurable grace period (20 s), catches, dead hiders respawn as seekers, scoreboard, team-selection UI, in-game config menu (F9). Last push 2026-08-12. | **The closest prior art to our mode.** Patterns: team in Photon **player custom properties**; host-side catch detection via postfix on `Bodypart.OnCollisionEnter` (only `IsMasterClient` acts); a `MonoBehaviour` with `[PunRPC]` methods attached to the **host's Character GameObject** so it reuses an existing PhotonView; freeze via postfix on `CharacterMovement.Update` that zeroes input, pins the torso Y and zeroes rigidbody velocity; respawn via `RPCA_ReviveAtPosition`; `RunManager.StartRun` patch for round start. We'll write our own versions, using room properties and a cleaner module split. |
| **PEAKLib** (PEAKModding) | https://github.com/PEAKModding/PEAKLib | **MIT** | Community API. Modules: **Core** (network prefab API, content registry, bundle loader, **host mod-list listener**), **Items**, **UI**, **ModConfig**, **Stats** (status effects). Already installed in the local r2modman profile (Core 1.7.2, Items 1.6.2). | **Adopt.** `PEAKLib.Core.Networking.HostPluginGuids` / `AddHostHasPluginListeners` for the "does the host have the mod" check. `PEAKLib.UI` for overlays. `PEAKLib.ModConfig` for an in-game config menu over our BepInEx `ConfigEntry`s. |
| **Vasodilation** (tony4twenty) | https://thunderstore.io/c/peak/p/tony4twenty/Vasodilation/ | unverified | Changelog says the host config is synced through a Photon **room property**, and clients adopt it on join and on update. | Confirms our config-sync design. |
| **SpongePEAKLib** | https://thunderstore.io/c/peak/p/SpongeMods/SpongePEAKLib/ | unverified | Includes a `ModHandshake` that publishes loaded plugin GUIDs in Photon **player properties**. | Pattern for our version handshake. |
| **PhotonCustomPropsUtils** (Snosz) | listed as a dependency of PEAK_Blow_Darts, PeakChatOps, GreenDemonChallenge | unverified (source repo not found) | Helper for syncing room and player custom properties. | Possible dependency, but only if the license and source check out. Otherwise we write a thin wrapper ourselves (it's about 50 lines). |
| **PEAKNetworkingLibrary** (DAa) | https://www.nuget.org/packages/PEAKNetworkingLibrary | unverified | Networking over **Steam** P2P instead of PUN. | Not needed. PUN properties and events cover our needs. |
| **EasyBackpack Fix** (khalil) | https://thunderstore.io/c/peak/p/khalil/EasyBackpack_Fix/ | unverified | Fork that fixes a crash after the 2026-08-12 game update removed `BackpackSlot.hasBackpack`. | A concrete example of update breakage (section 5). |

Mod categories from CLAUDE.md with **no strong open-source example found yet**: lobby-size changers, fog or campfire modifiers, and summit/biome event mods. These need a follow-up search on Thunderstore and in the Discord.

## 3. Similar modes in other BepInEx / Unity games

| Mod / library | Game | License | Takeaway |
|---|---|---|---|
| Hide_And_Seek (Gogozooom) — [Thunderstore](https://thunderstore.io/c/lethal-company/p/Gogozooom/Hide_And_Seek/), GitHub `gogozooom/Hide-and-Seek-LC-` | Lethal Company | unverified | Role-based mode built on **LethalNetworkAPI** (a messaging abstraction). Same structure as ours: host assigns roles and broadcasts them. Worth reading for round and role flow. |
| **CSync** — https://github.com/lc-sigurd/CSync | Lethal Company | **CC BY-NC-SA 4.0** | Host-to-client BepInEx config sync. It's built on Unity Netcode, **not Photon**, so it can't be used directly. The idea is the same as our `otl.cfg` room property. |
| LethalNetworkAPI | Lethal Company | unverified | Shows the value of a small typed-message wrapper. We'll write a tiny one over `RaiseEvent`. |

Content Warning (also Photon PUN) would be the closest technical cousin. Its mods were **not** researched yet (follow-up).

## 4. Shared libraries: recommendation

| Need | Choice |
|---|---|
| Patching | **HarmonyX** (ships with BepInEx). This is the wiki-recommended option. Use `AccessTools` for private members. |
| Config | BepInEx `ConfigEntry` + **PEAKLib.ModConfig** for the in-game menu. Our own room-property sync for the host's values. |
| Networking | Plain PUN 2: room/player custom properties + `RaiseEvent`. **PEAKLib.Core** for the host-has-mod check. |
| UI | **PEAKLib.UI** (MIT), or a plain uGUI Canvas + TMP. |

## 5. Breaking-update history

- PEAK updates often. The local install updated on **2026-10-06** (build 25739797, v2.6.b).
- A known breakage: the **2026-08-12** update removed `BackpackSlot.hasBackpack`, which crashed EasyBackpack 1.1.2. A community fork fixed it.
- Mods often state the game version they target (for example "PEAK v1.63.a" on PEAK-MX), so record our target build in every release.
- How authors cope:
  - Quick community forks.
  - PEAKLib's "never break public API" policy.
  - The template's easy rebuild.
- **For us:**
  - Resolve every hook through `AccessTools` at startup.
  - Disable only the module whose hook is missing.
  - Log the build ID at startup.
  - Keep a one-command "re-decompile and diff" step for each update.

## Sources

- https://peak.modding-community.com/ (overview, writing-code, custom-ui pages)
- https://github.com/glarmer/Hide-and-PEAK (GPL-3.0; source read for patterns only, not copied)
- https://github.com/PEAKModding/PEAKLib (MIT)
- https://thunderstore.io/c/peak/p/glarmer/Hide_and_PEAK_/
- https://thunderstore.io/c/peak/p/tony4twenty/Vasodilation/changelog
- https://thunderstore.io/c/peak/p/SpongeMods/SpongePEAKLib/v/1.0.0/
- https://thunderstore.io/c/peak/p/OracleTeam/CriticalBlowgun/v/2.0.2/required (PhotonCustomPropsUtils listing)
- https://www.nuget.org/packages/PEAKNetworkingLibrary
- https://thunderstore.io/c/peak/p/khalil/EasyBackpack_Fix/
- https://github.com/lc-sigurd/CSync
- https://thunderstore.io/c/lethal-company/p/Gogozooom/Hide_And_Seek/v/1.4.1/required
- https://peak.wiki.gg/wiki/Modifications

# Environment

Recorded 2026-10-07. Everything below was checked against the local install unless it says **unverified**.

## Game

| Item | Value | Source |
|---|---|---|
| Install path | `Z:\SteamLibrary\steamapps\common\PEAK` | Steam `libraryfolders.vdf` |
| Steam App ID | 3527290 | `appmanifest_3527290.acf` |
| Game version | **2.6.b** (commit `82be08a25`) | `PEAK\version.txt` |
| Steam build ID | **25739797** | `appmanifest_3527290.acf` |
| Last updated | 2026-10-06 (the day before this survey) | `LastUpdated` in the appmanifest |
| Developer / product | LandCrab / PEAK | `PEAK_Data\app.info` |
| Unity version | **6000.3.15f1** (`c1aa84e375f6`) | `UnityPlayer.dll` ProductVersion |
| Scripting backend | **Mono** (not IL2CPP) | `PEAK_Data\Managed\*.dll` and `MonoBleedingEdge\` are present, and there is no `GameAssembly.dll` |

**Mono matters.** Because the game runs on Mono, we can use normal BepInEx 5, HarmonyX patches on real .NET methods, and ILSpy decompilation that produces readable C#. IL2CPP interop isn't needed.

## Assemblies of interest (`PEAK_Data\Managed`)

| Assembly | Contents |
|---|---|
| `Assembly-CSharp.dll` | Almost all game logic (1,149 decompiled files). It includes the `Peak`, `Peak.Network`, and `Zorro.*` namespaces. |
| `Assembly-CSharp-firstpass.dll` | Small; Steam helpers |
| `Managers.dll` | Small |
| `PhotonUnityNetworking.dll`, `PhotonRealtime.dll`, `Photon3Unity3D.dll` | **Photon PUN 2** |
| `PhotonVoice*.dll` | Photon Voice (proximity chat) |
| `com.rlabrecque.steamworks.net.dll`, `SteamCommon.dll` | Steam lobbies and matchmaking |
| `Unity.InputSystem.dll` | New Input System (`CharacterInput` uses `InputAction`s) |
| `Unity.Localization.dll`, `Unity.TextMeshPro` | UI and text |

## Networking library: Photon PUN 2 (confirmed)

- `[PunRPC]` methods and `photonView.RPC(...)` appear throughout, for example `Character.RPCA_Die` at `Character.cs:736`.
- The game wraps PUN in its own thin layer under `Peak.Network`: `NetCode.Session.IsHost`, `PhotonShim`, and `SteamLobbyAPI`. Lobbies come from Steam, and gameplay traffic goes over Photon.
- Send and serialization rate are both **30 Hz** (`Peak.Network/NetworkingUtilities.cs:121-122`).
- The game already uses **raw event code 18** for kicking (`Peak.Network/PhotonShim.cs:156`). We must avoid that code.
- The game already uses player custom properties (`Player.cs:341-363`, `NetworkingUtilities.cs:151`). We'll namespace our keys with an `otl.` prefix.
- Fusion is **not** used.

## Mod loader

| Item | Value |
|---|---|
| BepInEx | **5.4.23.3** (Mono), from `LogOutput.log` in the r2modman profile |
| Thunderstore pack | `BepInEx-BepInExPack_PEAK` 5.4.75301, the dependency already in our `.csproj` |
| Mod manager | r2modman profile `Default` at `%APPDATA%\r2modmanPlus-local\PEAK\profiles\Default` |
| Already installed in that profile | PEAKLib.Core 1.7.2, PEAKLib.Items 1.6.2, MonoDetour (+ BepInEx 5 shim), SoftDependencyFix, TrueFinalAscent |
| Game folder | `winhttp.dll` and `doorstop_config.ini` are present, but there is no `BepInEx\` folder. The game is launched through r2modman, which points Doorstop at the profile. |

**Caveat:** the last BepInEx log is from 2026-09-02, which is **before** the 2026-10-06 game update. We still need to confirm that BepInEx and PEAKLib load on build 25739797. That's step 1 of the build plan.

## Project template (already in repo)

- PEAKModding community template: `netstandard2.1`, `BepInAutoPlugin` (Hamunii.BepInEx.AutoPlugin), and ThunderPipe packaging.
- Assembly name: `codesycamore.OnTheLookout`. CLAUDE.md suggests the GUID `com.chris.peakchasers` as a placeholder. Pick one before the first release, because the GUID must never change after that. See OPEN_QUESTIONS.
- `Config.Build.user.props` doesn't exist yet. Copy it from the template and set the game path and plugins path to the r2modman profile.
- .NET SDK on this machine: 10.0.401 (`global.json` requests 10.0.100 with `latestMajor` roll-forward).

## Tooling used for this survey

- `ilspycmd` 11.1.0, installed **repo-locally** at `.tools/` (gitignored). Command: `./.tools/ilspycmd -p -o decompiled/<asm> <dll>`.
- Decompiled output is in `decompiled/Assembly-CSharp`, `decompiled/Assembly-CSharp-firstpass`, and `decompiled/Managers`. These folders are **gitignored and never committed**.
- `.gitignore` now also excludes `*.dll`, `.tools/`, and `Config.Build.user.props`.
- Every `File.cs:line` reference in these docs is relative to `decompiled/Assembly-CSharp/` and applies to game build 25739797 only.

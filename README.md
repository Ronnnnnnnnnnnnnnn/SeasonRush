# Season Rush

A fast, competitive **2.5D isometric** versus game for two players (Host + Client). Both players
control the same warrior (blue or red) on a floating island that changes **every 2 minutes**:
**Spring -> Fall -> Winter**. Each season has its own map and its own monsters. Points come only from
killing monsters; the highest score after 6 minutes wins. If you die you wait 5 seconds to respawn,
and your rival can score freely while you are gone.

* Engine: **Unity 6 (6000.3.22f1)**, Universal Render Pipeline 2D, new Input System
* Multiplayer: **Netcode for GameObjects 2.13.3** + Unity Transport (Host / Client, 2 players)
* Course milestones: see [`docs/`](docs)
* How the networking works (IP address, port, host / client, matchmaking): [`docs/Networking_Explained.md`](docs/Networking_Explained.md)
* How to explain Weeks 1 to 3 (what to say, show and demo): [`docs/Weeks_1_to_3_Explained.md`](docs/Weeks_1_to_3_Explained.md)

## Open the project

1. Install Unity **6000.3.22f1** (Unity Hub).
2. Clone this repository and add the folder in Unity Hub, then open it.
3. Open `Assets/Scenes/SeasonRush.unity`.

If the scene or prefabs ever look broken, run **Season Rush > Rebuild Project Setup** from the Unity
menu. It re-imports the art with the right settings and rebuilds the prefabs and the scene (map themes and
materials that already exist are kept, so hand-edited maps are not lost).

The font, button colours and texts of the start menu are in `Assets/Resources/MenuStyle.asset` (see
[`docs/Menu_Style.md`](docs/Menu_Style.md)). To play on an Android phone see [`docs/Mobile_Testing.md`](docs/Mobile_Testing.md).

Your Winter pictures and the Spring cloud enemies are used automatically, see [`docs/Season_Art.md`](docs/Season_Art.md).

The two warriors use `Assets/2D Asset/Players/player1.png` (blue, host) and `player2.png` (red); Unity applies them by
itself, see [`docs/Character_Sprites.md`](docs/Character_Sprites.md).

To change a map, use **Season Rush > Map Editor**: drag trees and rocks in the Scene view (the mirror twin follows,
so both players keep the same cover), place new ones, move the start points. Other changes (other art, the ground) are
in [`docs/Map_Editing_Guide.md`](docs/Map_Editing_Guide.md).

## Play online against a random player

The main menu has three buttons: **PLAY** (find a random online opponent), **PROFILE** (your name and stats) and
**LEADERBOARD**; a small **LOCAL NETWORK** button opens HOST / JOIN. **PLAY** puts you in the matchmaking queue. A **SpacetimeDB** database keeps the matchmaking queue: it pairs the next two players who search and
decides who hosts; the two then connect their Netcode game. The database also stores **player names and stats**, a
**leaderboard** and the **match history**, shown in the menu.

Two one-time setups switch the online parts on (the game works without them, using HOST / JOIN):

1. **Database** - run `tools\Publish-SpacetimeDB.ps1` once (needs a free SpacetimeDB login). See
   [`docs/SpacetimeDB_Setup.md`](docs/SpacetimeDB_Setup.md).
2. **Connecting players on different networks** - link the project to Unity Cloud and enable Relay and Lobby. See
   [`docs/Online_Matchmaking_Setup.md`](docs/Online_Matchmaking_Setup.md). Without it, two players on the *same*
   network still connect directly.

## Play and test on a local network or one PC

You need **two game windows**. The easiest way:

1. **Season Rush > Build Windows Player (for a second game window)**. This writes
   `Builds/Windows/SeasonRush.exe`.
2. Press **Play** in the Unity Editor, click **LOCAL NETWORK**, then **HOST A MATCH**.
3. Start `SeasonRush.exe`, click **LOCAL NETWORK**, then **JOIN MATCH** (address `127.0.0.1`, port `7777`).
4. The match starts automatically with a 3-2-1 countdown as soon as both players are connected.
   (Alone? Click **START SOLO PRACTICE** on the host.)

Two PCs on the same network: the host tells the client the IP address shown in the lobby
(allow the game through the Windows firewall for **UDP port 7777**).

You can also skip the menu from a shortcut or terminal:

```
SeasonRush.exe -host
SeasonRush.exe -client -ip 192.168.1.20 -port 7777
```

### Controls

| Action | Keyboard | On screen (touch or mouse) |
| --- | --- | --- |
| Move | `W A S D` or arrow keys | Press and drag anywhere on the **left** side: a floating **joystick** appears |
| Attack | `Space`, `J` or `K` | Hold the **ATTACK** button on the right (it keeps swinging while held) |
| Pause / quit | `Esc` | **PAUSE** button (top right): the pause screen has **RESUME** and **QUIT TO MENU** |

The match is online, so **pausing does not stop the clock for your rival**: your warrior just stands still while the pause
screen is open. The waiting lobby and the result screen have a **BACK** button that returns to the main menu.

Up moves up the screen along the isometric diagonal. The joystick is analog: a small push walks slowly.
You can use the keyboard and the on-screen controls at the same time. On a touch screen you can move with one
finger and attack with another. Start the game with `-nocontrols` to hide the on-screen controls.

The camera is fixed and always shows the whole island.

## What is networked

| Netcode feature | Where |
| --- | --- |
| `NetworkManager` + `UnityTransport`, Host / Client, connection approval (max 2 players) | scene object `NetworkManager`, `Scripts/Network/ConnectionMenu.cs` |
| Random online matchmaking: SpacetimeDB queue pairs two players, then Unity Relay (or a direct connection) connects them. Fallback: Unity quick join | `Scripts/Network/OnlineMatchmaker.cs`, `Scripts/Online/SpacetimeManager.cs` |
| SpacetimeDB database: player profiles + lifetime stats, queue, pairings, match history | `spacetimedb/Lib.cs` (module, C#), `Scripts/Online/Generated/` (client files) |
| Player name and database identity travel to the server as synced values; the host reports the finished match | `Scripts/Player/PlayerStats.cs`, `Scripts/Match/MatchManager.cs` |
| `NetworkObject` + ownership: each player owns their warrior | `Prefabs/Player.prefab`, `Scripts/Player/PlayerController.cs` |
| `NetworkTransform` (owner authority on players, server authority on enemies) + `NetworkRigidbody2D` | `Player.prefab`, `Enemy.prefab` |
| `NetworkVariable`s: health, score, kills, deaths, dead flag, respawn time, colour slot | `Scripts/Player/PlayerStats.cs` |
| `NetworkVariable`s: match phase, season, phase end time, winner; enemy type + health | `Scripts/Match/MatchManager.cs`, `Scripts/Enemies/Enemy.cs` |
| RPCs: attack (`ServerRpc`), hit / teleport / knock-back / slash / season banner / score popup / match result (`ClientRpc`) | `PlayerCombat.cs`, `PlayerController.cs`, `PlayerStats.cs`, `Enemy.cs`, `MatchManager.cs` |
| Network spawn / despawn: the server spawns the match manager and every enemy and despawns them on death or season change | `ConnectionMenu.cs`, `MatchManager.cs`, `Enemy.cs` |

The server is authoritative for everything that matters: damage, kills, score, respawn and the
season timer. Clients only send "I swung in this direction" and their own movement.

## Project layout

```
Assets/
  2D Asset/    Spring, Falll (Fall), ConceptView, Placeholders (generated, replace with real art)
  Data/        one SeasonTheme asset per season: look, obstacle layout, spawns, the 3 monsters
  Prefabs/     Player, Enemy, MatchManager, network prefab list
  Scenes/      SeasonRush.unity
  Scripts/     Core, Data, Map, Network, Online (SpacetimeDB), Player, Enemies, Match, UI, Fx, Debug
  Resources/   SpacetimeConfig.asset (server address + database name)
  Editor/      setup tool, map editor (MapEditor/), placeholder art generator, build script
spacetimedb/   the database module (C#, Lib.cs) and its ready-built dist/seasonrush.wasm
tools/         Publish-SpacetimeDB.ps1
docs/          weekly milestone documents, setup guides, test evidence
```

### How the isometric view works

Gameplay runs on a flat 12 x 12 square (so collisions and distances are simple). Every sprite is
then drawn at its projected isometric position (`Scripts/Core/Iso.cs`) and sorted by depth. The island
itself is a mesh built from the flat tile textures (`Scripts/Map/IsoMapBuilder.cs`).

## Tuning

* Match length, damage, speed, spawn rates: `Scripts/Core/GameTypes.cs` (`GameConfig`).
* Map look, obstacle positions, enemy stats and points: the three assets in `Assets/Data`.
* Quick tests: `-seasonSeconds 20` shortens each season, `-autotest` runs a simple bot (see `Scripts/Debug`).

## Known limitations (planned)

* The SpacetimeDB database and matchmaking queue are tested with two game windows against a **local** SpacetimeDB
  server. Publishing to SpacetimeDB Maincloud (your login) and Unity Relay for players on different networks (needs
  a Unity Cloud project) are **not tested yet**. Local Host / Join is tested.
* The on-screen joystick and button are tested with a mouse-style pointer; real touch screens are not tested.
  The code is prepared for phones (landscape lock, safe-area margins), but an Android/iPhone build has not been
  made yet - that comes after the game itself is finished.
* Warrior and monsters use **placeholder art** (no animations yet); Winter ground and pines are placeholders too.
* HUD uses Unity's immediate-mode GUI; sound, animations and polish are planned for Week 4.
* The tie-break is "fewer deaths, otherwise draw"; sudden death is not implemented.
* Colour is assigned automatically (host = blue, client = red) instead of picked in a menu.

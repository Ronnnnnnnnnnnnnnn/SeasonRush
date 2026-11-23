# Season Rush

A fast, competitive **2.5D isometric** versus game for two players (Host + Client). Both players
control the same warrior (blue or red) on a floating island that changes **every minute**:
**Spring -> Fall -> Winter**. Each season has its own map and its own monsters. Points come only from
killing monsters; the highest score after 3 minutes wins. If you die you wait 5 seconds to respawn,
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

**The "Season Rush" menu in Unity is hidden** (it was only for building and setup; the tools still run by themselves when Unity loads the project).
To show it again: Project Settings > Player > Other Settings > Scripting Define Symbols, add `SEASON_RUSH_MENU`.

### The "Season Rush" menu in Unity

The menu in Unity's top bar is made with `[MenuItem("Season Rush/...")]` on a `public static` or `static` method in a script inside an
`Editor` folder. It exists only in the Unity Editor (editor code is not part of the built game).

| Menu item | What it does | Script |
| --- | --- | --- |
| Map Editor (move trees and rocks) | visual map editor in the Scene view | `Assets/Editor/MapEditor/SeasonRushMapEditor.cs` |
| Build Windows Player (for a second game window) | builds `Builds/Windows/SeasonRush.exe` | `Assets/Editor/BuildScript.cs` |
| Build Android APK (for a phone) | builds `Builds/Android/SeasonRush.apk` (needs the Android module) | `Assets/Editor/BuildScript.cs` |
| Apply Pixel UI Skin | uses the wooden buttons, panels and pixel fonts (Buttonborder, Panelborder, Font folders) in the menu | `Assets/Editor/UiSkinSetup.cs` |
| Apply Game Title And Icon | game name "Season Rush", app icon (Game logo.png), title picture on the menu (Game title.png) | `Assets/Editor/BrandingSetup.cs` |
| Apply Sounds | sets up the sounds in Assets/Sound (menu music, sword slash, damage) | `Assets/Editor/SoundSetup.cs` |
| Set Up Character Animations | fills your empty Idle / Walk / Attack clips and controllers and hooks the player and enemy prefabs into the game | `Assets/Editor/CharacterAnimSetup.cs` |
| Update Maps In Scene | regenerates the map and prop prefabs after you edited a Season Theme | `Assets/Editor/SceneBaker.cs` |
| Create UI Canvas In Scene | rebuilds the Canvas (menu, HUD, controls) in the open scene | `Assets/Editor/UiBuilder.cs` |
| Rebuild Project Setup | recreates layers, materials, prefabs and the scene; keeps your maps | `Assets/Editor/SeasonRushSetup.cs` |
| Reset Maps To Defaults (loses your map edits) | puts the three maps back to the generated layout | `Assets/Editor/SeasonRushSetup.cs` |
| Prepare Selected Pictures As Map Props | imports the selected pictures as trees / rocks / bushes | `Assets/Editor/SeasonRushSetup.cs` |
| Apply Winter And Cloud Art | uses the group's Winter and cloud pictures | `Assets/Editor/SeasonArtSetup.cs` |
| Apply Player Sprites (player1.png + player2.png) | uses the two knight pictures | `Assets/Editor/PlayerSkinsSetup.cs` |
| Select Menu Style (edit menu font, texts, colours) | opens the menu style asset | `Assets/Editor/MenuStyleSetup.cs` |

To add your own button: create a script in `Assets/Editor`, add `using UnityEditor;`, and write
`[MenuItem("Season Rush/My Button")] static void MyButton() { ... }`.

To change a map, use **Season Rush > Map Editor**: drag trees and rocks in the Scene view (the mirror twin follows,
so both players keep the same cover), place new ones, move the start points. Other changes (other art, the ground) are
in [`docs/Map_Editing_Guide.md`](docs/Map_Editing_Guide.md).

**What is in the scene (Hierarchy):** `Managers` (MapManager, SpacetimeManager, OnlineMatchmaker, ConnectionMenu, GameHud,
VirtualControls, AutomatedTestDriver, each its own object) and `Environment` (ArenaWalls and `Maps`: Map_Spring, Map_Fall,
Map_Winter). Every map is a prefab in `Assets/Prefabs/Maps`, and every tree / rock / bush on it is an instance of a prop prefab
in `Assets/Prefabs/Props`. The Season Theme assets stay the source of truth: after you change one (Map Editor or Inspector) run
**Season Rush > Update Maps In Scene** so the prefabs match. If you forget, the game notices the baked map is out of date and builds
the map from the theme instead. The MatchManager is not in the scene on purpose: the server spawns it as a network prefab when a match starts.

**Sound:** the menu music plays in the menu and the lobby and fades out when the battle starts (the 3-2-1 countdown); it comes back on the result screen.
Each season has its own battle music (Spring, Fall, Winter), the 3-2-1 countdown and the last 10 seconds of a season have their own sounds, and you hear your own death. A short sound plays when a new season starts (both players) and when any button is pressed. You hear your own sword slash and your own "taking damage" sound only (not your rival's, not the enemies'). The files are in `Assets/Sound`,
the volumes are in `Assets/Resources/Sounds.asset`, the `AudioManager` plays them. Automated test windows are muted (`-sound` turns it on).

**NetworkAnimator:** each warrior has an Animator and a `NetworkAnimator` (owner authority) on the Player prefab. The player's own machine sets the parameters (IsMoving, IsDead, trigger Attack) and Netcode replays the animation on the other screen. Red uses the same controller with its own clips (an Animator Override Controller built at runtime). Season Rush > Add NetworkAnimator To Player upgrades an older Player prefab (it also happens by itself).

**Animated characters:** `Assets/Prefabs/Players/Player 1.prefab` / `Player 2.prefab` (Idle, Walk, Attack, Death) and `Assets/Prefabs/Enemy/Cloud` (Spring), `Dryleaves` (Fall),
`Snowman` (Winter, with Attack and Death) are used by the game. Unity fills the empty clips and controllers by itself (menu: **Set Up Character Animations**),
and only what is still empty, so your own changes stay. The player prefab needs a Bool `IsMoving`, a Bool `IsDead` and a Trigger `Attack`; an enemy may have Triggers `Attack` and `Die`.

**The user interface is a Canvas in the Hierarchy** (`UI`): `MainMenu` (HomePanel, ProfilePanel, LeaderboardPanel, LocalPanel,
SearchingPanel), `HUD` (top bar, player boxes, PAUSE, lobby, result, death and pause screens) and `Controls` (joystick + ATTACK).
The menu buttons switch panels on and off with `SetActive` (select a button and look at its **On Click** list in the Inspector); the
ConnectionMenu / GameHud scripts only fill in the texts and run the network logic. Colours, font and button words still come from
`MenuStyle.asset` (untick **Use Menu Style** on the ConnectionMenu to style the canvas by hand instead). If the canvas is missing or
broken run **Season Rush > Create UI Canvas In Scene**.

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
| Pause / surrender | `Esc` | **PAUSE** button (top right): the pause screen has **RESUME** and **SURRENDER** |

**PAUSE freezes the whole game for both players** (match clock, enemies, warriors, respawn timers). Only the player who paused can resume,
and a pause ends by itself after 60 seconds. **SURRENDER** gives the rival the win: both players get the result screen
("YOU SURRENDERED" / "YOU WIN!") and press **BACK** to return to the main menu. Playing alone, SURRENDER just leaves.
The waiting lobby and the result screen have a **BACK** button that returns to the main menu.

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

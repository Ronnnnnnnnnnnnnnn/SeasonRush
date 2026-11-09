# SpacetimeDB - player stats, match history and the matchmaking queue

Season Rush uses a **SpacetimeDB** database for everything that must outlive one game session or be shared
between players. The Netcode game itself (movement, combat, enemies, score during a match) is unchanged.

| Stored in SpacetimeDB | Used for |
| --- | --- |
| **Player** - name, online flag, matches, wins / losses / draws, total points, kills, deaths | profile panel and leaderboard in the menu |
| **QueueEntry** - players waiting for a rival | the matchmaking queue (PLAY) |
| **Pairing** - two matched players, who hosts, a match code, how to connect | connecting the two players |
| **MatchResult** - history of finished matches | "Recent matches" list |

Code: the database module is **C#** - `spacetimedb/Lib.cs` (it runs inside SpacetimeDB, compiled to WebAssembly).
The game talks to it through `Assets/Scripts/Online/SpacetimeManager.cs`, using client files generated from the
module (`Assets/Scripts/Online/Generated/`).

## How a random match works

```
Player A presses PLAY                Player B presses PLAY
        |                                     |
        v                                     v
  JoinQueue  ------------ SpacetimeDB ------------  JoinQueue
                   (the database pairs the two:
                    A waited longest, so A is the host)
        |                                     |
        v                                     v
   Netcode HOST  --- address published via the Pairing row --->  Netcode CLIENT
   (Unity Relay if the project is linked to Unity Cloud, otherwise a direct connection on the same network)
        |
        v
   match is played (all Netcode: NetworkVariables, RPCs, spawning)
        |
        v
   the host sends ReportMatch  ->  MatchResult row + both players' lifetime stats updated
```

SpacetimeDB decides **who plays whom** and keeps the records. It cannot carry the game traffic itself, so players on
different home networks still need **Unity Relay** (see `Online_Matchmaking_Setup.md`). On the same network
(school Wi-Fi, one PC) the direct connection works with no Unity Cloud setup.

## One-time setup (about 5 minutes)

You need a free SpacetimeDB account (you log in through the browser). The module is already built
(`spacetimedb/dist/seasonrush.wasm`), so **no .NET or other build tools are needed just to publish it**.

1. Open a normal **PowerShell** window (not inside Unity) in the project folder and run:

   ```bash
   powershell -ExecutionPolicy Bypass -File tools\Publish-SpacetimeDB.ps1
   ```

2. It installs the SpacetimeDB command line tool if needed, opens your browser to log in, asks for a database name
   (lowercase letters, numbers and dashes, for example `seasonrush-yourname`) and publishes the module.
3. It writes that name into `Assets/Resources/SpacetimeConfig.asset`. Open the game: the menu shows
   **Online database: connected**.

Every team member who plays uses the **same** database name. Commit `SpacetimeConfig.asset` so the whole team shares it.

### Doing it by hand

```bash
spacetime login
spacetime publish --server maincloud --bin-path spacetimedb/dist/seasonrush.wasm seasonrush-yourname
```

Then type the name into the **Database** field of `Assets/Resources/SpacetimeConfig.asset`.

## Using it in the game

The main menu has three buttons:

* **PROFILE** - type a **name** and press *Save name*. Your stats (matches, wins, points, kills) are kept under that name on
  this computer. Without a name every launch is a throw-away guest ("Guest4821").
* **PLAY** - finds a random opponent through the database queue. While you wait the screen shows how long you have been
  searching.
* **LEADERBOARD** - the best players and the most recent matches. After a match the result screen also shows your record.
* A small **LOCAL NETWORK** button opens HOST / JOIN. Without a database name the game works exactly as before
  (HOST / JOIN and the Unity-only quick match).

## Local testing (optional, no account)

```bash
spacetime start                                             # a local server on http://127.0.0.1:3000
spacetime publish --server local --bin-path spacetimedb/dist/seasonrush.wasm seasonrush-local
SeasonRush.exe -sdbUri http://127.0.0.1:3000 -sdbDb seasonrush-local -name Ana
```

`-sdbUri`, `-sdbDb` and `-name` override the config and the saved name for that run only.

## Changing the module

Edit `spacetimedb/Lib.cs`, then rebuild, copy the result to `dist` and publish. Rebuilding needs two things that
publishing does not:

* the **.NET 10 SDK** (the folder has a `global.json` that asks for it), and
* the **WASI SDK 29** (about 510 MB, from <https://github.com/WebAssembly/wasi-sdk/releases/tag/wasi-sdk-29>).
  Unzip it anywhere and point the environment variable `WASI_SDK_PATH` at the unzipped folder. Download it first in a
  browser if the build tool's own download keeps failing on a slow connection.

```bash
spacetime build --module-path spacetimedb
copy spacetimedb\bin\Release\net10.0\wasi-wasm\publish\StdbModule.wasm spacetimedb\dist\seasonrush.wasm
spacetime publish --server maincloud --bin-path spacetimedb/dist/seasonrush.wasm seasonrush-yourname
spacetime generate --lang csharp --out-dir Assets/Scripts/Online/Generated --bin-path spacetimedb/dist/seasonrush.wasm
```

Notes:

* Keep the project file name `StdbModule.csproj`; the SpacetimeDB tool looks for the finished file under that name.
* Commit the new `spacetimedb/dist/seasonrush.wasm`. It is the file `tools/Publish-SpacetimeDB.ps1` uploads, so your
  teammates publish your change too.
* If you add, rename or remove a table or reducer, regenerate the client files with the last command (delete the old
  files in `Assets/Scripts/Online/Generated/` first so nothing stale is left) and commit them.
* Reducer names must not start with `On`. In the database, reducers and tables use snake_case names (`join_queue`,
  `match_result`); the generated C# client turns them into `JoinQueue()` and `MatchResult`.

## Safety notes (class project level)

* The tables are public: anyone with the database name can read player names and stats. Do not put private data in them.
* A modified game client could report a fake result. The database checks that the reporter is one of the two players,
  that scores are in a sane range and that a queued match is only stored once, but it cannot verify the fight itself.
* Players are anonymous (no passwords). The identity is stored on the player's computer.

## Troubleshooting

| What you see | Cause / fix |
| --- | --- |
| "Online database: not set up (optional)" | `SpacetimeConfig.asset` has no database name yet. Run the publish script. |
| "Online database: unavailable" + a message | The name is wrong, the module was not published, or there is no internet. The game still works. |
| PLAY says "Could not reach the host at ..." | The two players are on different networks and the project is not linked to Unity Cloud, so Relay is unavailable. Use the same network or follow `Online_Matchmaking_Setup.md`. |
| Stats do not change after a match | Both players need to be connected to the database, and the match must be a real two-player match (solo practice is not stored). |
| `spacetime build` says it cannot find the WASI SDK | Set `WASI_SDK_PATH` to the unzipped `wasi-sdk-29...` folder (see "Changing the module"). |

## Files

```
spacetime.json                       module location + default server
spacetimedb/Lib.cs                   the database module (tables + reducers), C#
spacetimedb/StdbModule.csproj        build settings for the module (.NET 10, WebAssembly)
spacetimedb/dist/seasonrush.wasm     the ready-built module that gets published
tools/Publish-SpacetimeDB.ps1        one-command publish + config
Assets/Resources/SpacetimeConfig.asset   server address + database name
Assets/Scripts/Online/               SpacetimeManager, plain data classes, generated client files
```

# Online random matchmaking - setup guide

Click **PLAY** in the menu and you are paired with the next random player who is also searching. No IP addresses, no port forwarding.

Online play has **two parts**, and each needs a one-time setup that only you can do (both need *your* accounts):

| Part | What it does | Needed for |
| --- | --- | --- |
| **A. SpacetimeDB database** | the waiting room: queue, pairing (who hosts), player names, stats, leaderboard | the PLAY button to find a rival |
| **B. Unity Cloud project (Relay + Lobby)** | carries the game data between two players on *different* networks | players who are **not** on the same Wi-Fi |

* Two players on the **same Wi-Fi / the same PC**: part A is enough. The two games connect directly with the host's IP and port 7777.
* Players on **different networks (internet)**: you need A **and** B.
* Without part A the PLAY button falls back to a Unity-only quick join, which needs part B.
* HOST / JOIN under **LOCAL NETWORK** always works and is the best backup for a presentation.

## Checklist

1. [ ] **A. Publish the database** (5 minutes): see below, then the menu shows "Online database: connected".
2. [ ] **B. Link the Unity Cloud project** and enable Relay + Lobby (10 minutes) - only for play across the internet.
3. [ ] **Build the Windows game again** *after* A and B (*Season Rush > Build Windows Player*), because the build carries the database name and the project ID.
4. [ ] **Test with two windows**, then with a friend. Everyone must use the **same database name**.

## A. Publish the SpacetimeDB database

You need a free SpacetimeDB account (you log in through your browser; GitHub login works).

1. Open a normal **PowerShell** window (not inside Unity) and go to the project folder (quotes, because of the spaces):

   ```bash
   cd "C:\Users\ANGEL\Desktop\Season Rush_GameNetworking_Project\SeasonRush_2D Isometric Game"
   ```

2. Run the publish script:

   ```bash
   powershell -ExecutionPolicy Bypass -File tools\Publish-SpacetimeDB.ps1
   ```

3. The script does everything in order: installs the SpacetimeDB command line tool if it is missing, opens your browser to **log in**,
   asks for a **database name** (lowercase letters, numbers and dashes, for example `seasonrush-ana`; press Enter to accept the suggestion),
   publishes `spacetimedb\dist\seasonrush.wasm` to **Maincloud**, and writes the name into `Assets/Resources/SpacetimeConfig.asset`.
4. Open the game. The menu should say **Online database: connected** (green). If it says "unavailable", see the table below.

Every player uses the **same database name**: share the build you made (or commit `SpacetimeConfig.asset`). If the name is taken by
someone else, run the script again with another name. More detail: [`SpacetimeDB_Setup.md`](SpacetimeDB_Setup.md).

## B. Link the Unity Cloud project (for play across the internet)

1. **Sign in to Unity** in the Editor (account icon, top right) with your Unity ID.
2. **Create a project in the Unity Dashboard:** go to <https://cloud.unity.com>, choose **Home > Projects > Create project**, give it a name
   (for example *Season Rush*), answer the age-group question about your game truthfully, and press **Create**.
3. **Link it in the Editor:** *Edit > Project Settings > Services*, choose **Use an existing Unity project ID** (or *Create a new cloud
   project* there), pick your **Organization** and the project, and press **Link project ID**. The Services page then shows the Project ID.
4. **Check Relay and Lobby in the Dashboard:** open the project at cloud.unity.com, then **Multiplayer** in the left menu, then **Lobby**
   and **Relay**. Each has a **Setup guide**. When the project is linked it shows **"Setup complete - Lobby has been successfully set up
   and automatically enabled"**, so there is no switch to press (the optional "Import a sample" step is not needed). If a service
   does not say this, open its Setup guide and finish the steps. The free tier is enough for a class project. The game also tells you
   ("Could not find or create an online match... Relay and Lobby") if a service is really missing.
5. The packages are already installed (`com.unity.services.multiplayer` 2.4.1 and Netcode for GameObjects).
6. Build the Windows player **after** linking, so the Project ID is included.

## How a PLAY match is made

```
Both players press PLAY
        |
        v
Each game joins the SpacetimeDB queue      (A: the database pairs the two, the one who waited longest is the HOST)
        |
        v
HOST starts the Netcode host, then writes its address into the database
        |        same network:      "192.168.0.104:7777"   -> the guest connects directly
        |        project linked:    "relay"                -> both join one Unity Relay session (B)
        v
GUEST reads the address from the database and connects
        |
        v
3-2-1 countdown, the match is played, the host reports the result to the database (stats + leaderboard)
```

How the connection itself works (IP address, port, host / client) is explained in [`Networking_Explained.md`](Networking_Explained.md).
Code: `Assets/Scripts/Network/OnlineMatchmaker.cs`, `Assets/Scripts/Online/SpacetimeManager.cs`, `spacetimedb/Lib.cs`.

## Test it (two game windows)

1. Press **Play** in the Editor, then click **PLAY** in the menu.
2. Start `Builds/Windows/SeasonRush.exe` and click **PLAY** there too.
3. After a few seconds both windows leave the search screen: one becomes the host ("waiting for rival" for a moment), the other joins it,
   and the 3-2-1 countdown starts.

Each window signs in as its own anonymous player, so one PC can act as two players. Use `-name Ana` on the command line to choose the
name of a window. Two PCs on different networks work the same way once A and B are done.

If Windows asks whether the game may use the network, choose **Allow** (private networks). A blocked game cannot be reached by the other player.

## Messages you may see

| Message | Meaning / fix |
| --- | --- |
| "Online database: not set up (optional)" | Part A is not done: `SpacetimeConfig.asset` has no database name. Run the publish script. |
| "Online database: unavailable" | Wrong database name, the module was not published, or there is no internet. |
| "Waiting for a rival..." that never ends | Nobody else is searching **in the same database**. Open a second window, or ask a friend with the same build. |
| "Could not reach the host at ..." | The two players are on different networks and part B is not done (no Relay). Link the Unity Cloud project, or play on the same Wi-Fi. |
| "Online play is not set up yet..." | No database name (part A) **and** the project is not linked (part B). |
| "Could not sign in to Unity online services..." | No internet, or the project is not linked / the services are not enabled (part B). |
| "Could not find or create an online match..." | Relay or Lobby is not enabled for the project (part B step 3). |
| "Disconnected from the host." | The other player left. Press BACK and search again. |

## Turning it off

* **Search automatically when the game opens** is an option on the **PROFILE** screen. It is off by default and remembered;
  `-nomatchmaking` also switches it off for one run.
* `-host`, `-client` and the automated test flags never start a search.

## Limits

* The pairing is "first in the queue": there is no skill or region matching.
* Colours are assigned automatically (the first player is blue, the second red).
* If the **host** leaves, the other player is returned to the menu with a message. If the **client** leaves, the host stays in the
  match alone until they press BACK or Leave.
* **Tested:** the database queue and pairing with two game windows against a local SpacetimeDB server, and the direct connection on one
  PC. **Not tested yet:** the real SpacetimeDB Maincloud and Unity Relay between two networks (they need your accounts).

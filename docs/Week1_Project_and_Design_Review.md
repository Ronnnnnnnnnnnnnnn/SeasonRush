# Season Rush - Week 1: Project and Design Review

**Milestone due:** 27 Oct (Planning milestone)
**Engine:** Unity 6 (6000.3.22f1), Universal Render Pipeline 2D, Netcode for GameObjects 2.13.3
**Genre / platform:** 2.5D isometric competitive action (arena brawler), PC, two players (Host + Client)

> Two rivals fight season monsters on a floating island that changes every 2 minutes
> (Spring -> Fall -> Winter). Score only comes from killing monsters. Highest score after
> 6 minutes wins. Dying costs a 5 second respawn during which the rival scores for free.

## Required deliverables (checklist)

| Required for Week 1 | Status | Where |
| --- | --- | --- |
| Initial map / level designs for at least two levels | Done - **three** levels designed (Spring, Fall, Winter) | Section 1 |
| Team roles and individual responsibilities | Done - fill in names | Section 2 |
| Planned use of assets from the group's own previous projects only | Done | Section 3 |
| Initial Unity project and GitHub repository | Project done, `.gitignore` + repo structure ready, push to GitHub still needs the team's account | Section 4 |

---

## 1. Level (map) designs

All three maps share one **floating island** shape: a square 12 x 12 arena drawn as an isometric
diamond (the camera is fixed and shows the whole island, it never follows a player). The two front
sides of the island are dirt cliffs, the two back sides have a ragged grass edge, exactly as in the
concept views (`Assets/2D Asset/ConceptView`).

Gameplay is simulated on the flat 12 x 12 square and then projected to the isometric view, so the
tables below use arena coordinates `(x, y)` from 0 to 12.

```
            north corner (12,12)
                 /\
   back-left   /    \   back-right        Blue (player 1) spawns on the LEFT corner  (1.8, 10.2)
   (ragged)  /        \  (ragged)         Red  (player 2) spawns on the RIGHT corner (10.2, 1.8)
  west     <     map     >     east
 (0,12)      \        /      (12,0)       Every obstacle has a mirror twin across the vertical
   cliff       \    /       cliff         axis, so both players have identical routes and cover.
   (front-left)  \/  (front-right)
            south corner (0,0)
```

### Level 1 - Spring ("Blooming Meadow")  - playable

| | |
| --- | --- |
| Mood | Fresh, colourful |
| Environment | Bright grass island, sand clearing in the middle, sakura and oak trees on the back edges, rocks, bushes and flowers |
| Enemies | **Cloud monsters:** Puff Cloud (weak), Rain Cloud (medium), Storm Cloud (strong) |
| Obstacles | 12 blockers (2 big trees on the centre line, 2 small trees, 2 sakura, 6 rocks) + 9 non-blocking decorations |
| Design intent | The most open map. Long clear lines, few places to hide. Teaches the loop: chase, hit, score. |
| Enemy spawns | South corner, north corner, and the middle of each back edge |

### Level 2 - Fall ("Foggy Grove")  - playable prototype

| | |
| --- | --- |
| Mood | Eerie, shadowy, cozy |
| Environment | Tan / golden ground, bare dead trees, orange trees, rocks, pale clearing in the middle |
| Enemies | **Tree monsters:** Sapling (weak), Stump Guard (medium), Elder Treant (strong) |
| Obstacles | 16 blockers (4 dead trees, 6 orange trees, 6 rocks) |
| Design intent | Denser than Spring. More cover means more chances to dodge and to steal a kill, but also to get cornered. |

### Level 3 - Winter ("Frozen Ridge")  - playable prototype (placeholder art)

| | |
| --- | --- |
| Mood | Cold, slippery |
| Environment | Snow-covered island, ice patch in the middle, pine trees, ice-blue rocks |
| Enemies | **Snow monsters:** Snowling (weak), Snowman (medium), Frost Golem (strong) |
| Obstacles | 14 blockers (8 pines, 6 ice rocks) |
| Design intent | Open again but with pines forming lanes. The ice patch is planned to be slippery (Week 4 polish). |

### Enemy tiers (same difficulty curve every season)

| Tier | Health | Contact damage | Points | Spawn chance |
| --- | --- | --- | --- | --- |
| Weak | 20 | 6 | 10 | 60 % |
| Medium | 45-50 | 10-11 | 25 | 30 % |
| Strong | 105-115 | 16-18 | 60-65 | 10 % |

The warrior deals 15 damage per hit with a 0.35 s cooldown and has 100 health.
All numbers live in `GameConfig.cs` and in the three `SeasonTheme` assets (`Assets/Data`).

### Map screenshots

Screenshots of the three maps captured from the running game are in `docs/images/`
(`spring.png`, `fall.png`, `winter.png`).

---

## 2. Team roles and individual responsibilities

**Fill in the names.** The roles below match the work that exists in the project; if the team has
fewer people, merge the roles in the order shown.

| Role | Member | Responsibilities | Main files / folders |
| --- | --- | --- | --- |
| Project lead / integration | `[Name]` | Task board, merges to `main`, weekly milestone check, final presentation | `docs/`, GitHub |
| Network programmer | `[Name]` | NetworkManager setup, Host / Client connection, spawning, ownership, NetworkTransform, NetworkVariables, RPCs, connection approval | `Scripts/Network`, `Scripts/Match`, `Scripts/Player/PlayerStats.cs` |
| Gameplay programmer | `[Name]` | Warrior movement and attack, enemy AI, scoring, respawn rule, win condition | `Scripts/Player`, `Scripts/Enemies`, `Scripts/Match` |
| Level designer | `[Name]` | The three map layouts, obstacle placement, spawn points, balance of enemy numbers | `Assets/Data` (Season Theme assets), `Scripts/Map` |
| Artist | `[Name]` | Tiles, props, warrior and monster sprites, animations, concept views | `Assets/2D Asset` |
| UI / audio / QA | `[Name]` | HUD, menus, sound effects and music, Host + Client test passes, bug list | `Scripts/UI`, test notes |

Working rules: one feature per branch, pull request into `main`, at least one teammate reviews
before merge, never commit the `Library/` folder (see `.gitignore`), and **only one person edits a
given scene at a time** (scene merges are painful in Unity).

---

## 3. Asset plan - group's own assets only

Rule: **no third-party or downloaded assets.** Everything in the game comes from the group's own
art pack and previous projects, or is a temporary placeholder generated inside this project.

| Asset | Used for | Source | Status |
| --- | --- | --- | --- |
| Spring tile set (grass, side, cliff edge, sand, trees, sakura, bush, flowers, rocks) - `Assets/2D Asset/Spring` | Level 1, rocks reused in Fall and Winter | Group art pack `[confirm author / project]` | In use |
| Fall tile set (ground, side, cliff edge, dead tree, orange trees) - `Assets/2D Asset/Falll` | Level 2 | Group art pack `[confirm author / project]` | In use |
| Concept views (`spring_concept.jpg`, `fall_concept.jpg`) - `Assets/2D Asset/ConceptView` | Reference for the island look and layout | Group art pack `[confirm author / project]` | Reference only |
| Warrior (body + colour tunic), 3 monsters, Winter ground, ice patch, pine - `Assets/2D Asset/Placeholders` | Characters and Winter map | Generated by `Assets/Editor/PlaceholderArt.cs`, temporary | **To be replaced by the artist** |
| Fall extras seen in the concept view (pumpkins, fence, spider web, leaf pile) | Fall decoration | Not drawn yet | Planned (Week 4) |
| Sound effects and music | Feedback, season change | Not created yet | Planned (Week 4), group-recorded or group-made only |

Replacing a placeholder is a one-step change: drop the new PNG over the file with the same name
(or assign a different sprite in the `SeasonTheme` asset / prefab). Nothing is regenerated over an
existing file.

---

## 4. Unity project and GitHub repository

### Project
* Unity **6000.3.22f1**, 2D URP template, new Input System.
* Packages added for multiplayer: **Netcode for GameObjects 2.13.3** (pulls in Unity Transport 2.7.4) and
  **Multiplayer Services 2.4.1** (anonymous sign-in, Lobby and Relay for connecting players across networks) and the
  **SpacetimeDB client SDK 2.14.1** (player profiles, leaderboard, match history and the matchmaking queue).
* Main scene: `Assets/Scenes/SeasonRush.unity` (built and refreshed by *Season Rush > Rebuild Project Setup*).

### Folder structure
```
Assets/
  2D Asset/        Spring, Falll, ConceptView, Placeholders
  Data/            SeasonTheme assets (one per season)
  Materials/       island materials
  Prefabs/         Player, Enemy, MatchManager, network prefab list
  Scenes/          SeasonRush.unity
  Scripts/
    Core/ Data/ Map/ Network/ Player/ Enemies/ Match/ UI/ Fx/ Debug/
  Editor/          setup, placeholder art, build script
docs/              this document and weekly notes
```

### GitHub
`.gitignore` (Unity standard) is in the project root. To publish:

```bash
cd "SeasonRush_2D Isometric Game"
git init -b main
git add .
git commit -m "Initial Unity project: Season Rush"
git remote add origin https://github.com/<your-account>/<repo-name>.git
git push -u origin main
```

Each teammate then clones the repository and opens it with Unity Hub using editor 6000.3.22f1.
Branch names: `feature/<short-name>`, `fix/<short-name>`.

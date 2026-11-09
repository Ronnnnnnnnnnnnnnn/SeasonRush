# Season art: Winter pictures and the cloud enemies

The game is made to use your own pictures. Some pictures are already wired in; the rest are generated placeholders.

| Picture | Where | Used for |
| --- | --- | --- |
| `Assets/2D Asset/Winter/center_with_grass.jpg` | Winter | ground of the Winter island |
| `.../Winter/side2_winter.png` | Winter | the ragged snow edge on the island's right side |
| `.../Winter/edge_winter.jpg` | Winter | the stone cliff on the two front sides |
| `.../Winter/ice.jpg` | Winter | the ice patch in the middle |
| `.../Winter/rock1_winter.png`, `rock2_winter.png`, `rock3_winter.png` | Winter | the three rocks (blocking) |
| `.../Winter/bush_winter.png`, `bush_winter (1).png` | Winter | two bush types (decoration, you can walk through) |
| `Assets/2D Asset/SpringEnemy/cloud1.png` + `Assets/2D Asset/cloud2.png` | Spring | the three cloud enemies (Puff, Rain, Storm) |
| `Assets/2D Asset/Players/player1.png`, `player2.png` | players | the two knights (see `Character_Sprites.md`) |

Not replaced yet (still generated placeholders): the **pine tree** of Winter, the **Fall** monsters and the **Winter** monsters
(snowmen). `side1_winter.png` is not used.

## It is applied by itself

When Unity opens the project (or recompiles), the tool `Assets/Editor/SeasonArtSetup.cs` looks at the maps and changes only what
still uses a placeholder:

* the Winter ground, side, cliff and ice materials get the Winter pictures,
* the Winter rocks (`IceRock1-3`) become the Winter rocks, and two bush types are added,
* on a Winter map that was **never edited by hand** four bushes are placed too; an edited map only gets the bush *types* (place them
  with the **Map Editor**),
* the three Spring cloud enemies show the cloud pictures.

Anything you changed by hand (a prop you moved, a picture you chose yourself) is left alone. The tool runs again whenever Unity loads,
and **Season Rush > Apply Winter And Cloud Art** runs it on demand. **Reset Maps To Defaults** builds the maps with these pictures too.

## How the clouds animate

`cloud1.png` and `cloud2.png` are two pictures of the same cloud, one floating a little higher with a smaller shadow. The game shows
them one after the other (2.5 pictures per second; each enemy starts at a different moment), and the shadow is part of the picture.
Each enemy keeps its own size and colour (Puff = white, Rain = blue, Storm = dark and big).

In the Spring Theme asset (`Assets/Data/SpringTheme.asset`, **Enemies**) every enemy now has:

| Field | Meaning |
| --- | --- |
| Sprite | the first picture |
| Frames | the pictures shown in turn (2 or more); empty = just the Sprite |
| Frame Rate | pictures per second |
| Head Height | how high the top of the picture is above its ground point; the health bar floats above it |

You can use the same fields for the other seasons' monsters: put your pictures in **Frames** (or just one in **Sprite**).

## Adding more of your own pictures

* A prop (tree, rock, bush): select the picture and run **Season Rush > Prepare Selected Pictures As Map Props**, then drag it into a
  prop's **Sprite** (see `Map_Editing_Guide.md`).
* Ground / cliff / patch: change the **Base Map** of the matching material in `Assets/Materials`.
* A monster or character: any picture whose "feet" or shadow are at the bottom works; set the pivot there and use a pixels-per-unit
  that makes it the size you want (about 1 to 1.5 map units tall).

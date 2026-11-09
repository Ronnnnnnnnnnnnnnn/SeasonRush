# Editing the maps (trees, rocks, ground, spawns)

Each season map is **data**, not a hand-built scene: one *Season Theme* asset per season. You edit the asset in the
Unity Inspector, press Play, and the map is rebuilt from it. No code is needed.

| Map | Asset |
| --- | --- |
| Spring | `Assets/Data/SpringTheme.asset` |
| Fall | `Assets/Data/FallTheme.asset` |
| Winter | `Assets/Data/WinterTheme.asset` |

Click one in the Project window and the Inspector shows everything below. The quickest way to change a map, though, is
the visual **Map Editor**:

## The easy way: the Map Editor

Menu **Season Rush > Map Editor (move trees and rocks)**. Make sure the game is **not** running.

1. The window opens and the map appears in the Scene view (which switches to 2D and frames the island). Choose
   **Spring / Fall / Winter** at the top of the window.
2. **Move:** drag any tree or rock in the Scene view. With **Keep the mirror twin in step** ticked, the matching prop on
   the other side moves too, so both players keep the same cover.
3. **Select:** click a prop. The window then shows its type, exact position and Flip X, with **Duplicate**,
   **Delete** (the Delete key works too) and **Select twin**. Clicking empty space deselects.
4. **Add:** under **Add a prop** press **Place with mouse** and click in the Scene view (Esc or right-click stops), or
   press **Add** to drop a mirrored pair near the centre. Untick the mirror option to add a single prop.
5. **Change type:** use the **Type** list of the selected prop (for example turn a rock into a pine). To use art
   from another season you first add it as a prop type, see section 3 below.
6. **Start points:** the blue and red squares are the players' starts and the purple ones are enemy spawn points. They
   can be dragged too (blue and red move together while the mirror option is on).
7. **Map check** at the bottom of the window warns when a blocking prop is too close to a player's start, or has no mirror
   twin.

What the markers mean in the Scene view:

| Marker | Meaning |
| --- | --- |
| White circle | tree or rock that blocks movement, with its orange **blocking circle** |
| Light green circle | decoration you can walk through (bush, flowers) |
| Yellow / cyan | the selected prop / its mirror twin |
| Blue, red, purple squares | blue start, red start, enemy spawns |
| Dotted blue line | the mirror line (X = Y) |

The editor saves into the theme asset (`Assets/Data/...Theme.asset`) when you let go of the mouse, and **Ctrl+Z** undoes a
whole drag. The map you see in the Scene view is only a preview: it is never saved into the scene, it disappears when you
close the window or press Play, and the game builds the same map from the asset when it starts. **Snap to grid** (default
0.1) can be switched off for free placement.

The rest of this guide explains the numbers behind it and how to do the same by hand in the Inspector.

## 1. Where is (x, y) on the screen?

The game is played on a flat 12 x 12 square and drawn as an isometric diamond. A position is `(X, Y)` from 0 to 12:

```
                     back  (12, 12)
                    /               \
   left  (0, 12)                        right  (12, 0)
                    \               /
                     front  (0, 0)
```

* Increasing **X** moves a prop **up and to the right** on the screen.
* Increasing **Y** moves it **up and to the left**.
* On screen: `screen x = (X - Y) * 0.8`, `screen y = (X + Y) * 0.4`. The centre of the map is `(6, 6)`.
* The two players start at `(1.8, 10.2)` (left corner, blue) and `(10.2, 1.8)` (right corner, red).
* Enemies appear at the four points listed in **Enemy Spawns** (front, back and the two sides).

**Keep the map fair.** Both players must have the same cover. Every prop has a **mirror twin**: the same prop at
`(Y, X)` (X and Y swapped) with **Flip X ticked**. Move or delete both together. Keep the middle open for fighting
and keep props about 1.5 units away from the spawn points.

## 2. Move, add or delete a tree or rock by hand (Inspector)

In the theme's Inspector there are two lists:

* **Props** - the *types* of decoration (BigTree, Rock1, ...): picture, size, and blocking radius.
* **Placements** - every single prop on the map: **Prop** (the number of its type in the Props list, starting at 0),
  **Position** and **Flip X**.

To move a rock: open **Placements**, find the entries whose **Prop** number is the rock's number, change
**Position** X / Y. Then move its mirror twin to the swapped values (X and Y exchanged).

Prop numbers (the order of the Props list):

| Spring | Fall | Winter |
| --- | --- | --- |
| 0 BigTree, 1 SmallTree, 2 Sakura, 3 Sakura2, 4 Bush, 5 Flowers, 6 Rock1, 7 Rock2, 8 Rock3 | 0 DeadTree, 1 OrangeTree1, 2 OrangeTree2, 3 Rock1, 4 Rock2, 5 Rock3 | 0 Pine, 1 IceRock1, 2 IceRock2, 3 IceRock3 |

* **Add** a prop: press **+** at the bottom of **Placements** (it copies the last entry), then set Prop, Position
  and Flip X. Add the mirror twin as well.
* **Delete** a prop: click its entry, then press **-**.
* **Undo** works (Ctrl+Z) while the asset is selected.

Things to know about a prop *type* (in **Props**):

| Field | Meaning |
| --- | --- |
| Sprite | the picture |
| Scale | size multiplier, applies to **every** placement of that type (make a second type with the same sprite for a bigger copy) |
| Collider Radius | the blocking circle at the prop's feet, in map units. `0` = decoration you can walk through (bushes, flowers). Rocks and trees use 0.3 - 0.5 |
| Tint | colour multiplier (the Winter rocks use a blue tint) |

Depth sorting is automatic: a prop further "up" the screen is drawn behind the ones in front of it.

## 3. Use art from another map (or your own pictures)

**Swap the picture of an existing prop type:** in **Props**, open the element and drag a picture from the Project window
into **Sprite**. All placements of that type change.

**Add a new prop type** (for example Fall's orange tree in the Spring map):

1. **Props** -> press **+** at the bottom.
2. Fill in Name, drag the picture into **Sprite**, set Scale (try 1.2) and Collider Radius (try 0.35).
3. Its number is the new last position in the list (Spring has 9 types, so the new one is `9`). Use that number in
   **Placements**.

**Pictures that are ready to use as props** (already set up): Spring `big_tree`, `small_tree`, `sakura_tree`,
`sakura_tree2`, `bush_spring`, `flowers_spring`, `rock1_spring`, `rock2_spring`, `rock3_spring`; Fall `big_tree (1)`,
`small_tree_1`, `small_tree_2`; Winter placeholder `pine_winter`.

**Any other picture** (for example `rock2_fall.png`, `rock3_fall.png`, `bush_fall.png`, or a new one you drew) first needs the
right import settings, otherwise it appears about five times too big and floats above the ground:

1. Put the `.png` in `Assets/2D Asset/...` (it needs a transparent background).
2. Click the picture in the Project window.
3. Menu **Season Rush > Prepare Selected Pictures As Map Props**. (You can select several at once.)
4. Drag it into a prop's **Sprite** slot.

That sets Texture Type = Sprite, 500 pixels per unit (like the other props) and the pivot at the bottom-centre of the
drawn object, so the position you enter is where the object stands. Adjust **Scale** if it is still too big or small.

## 4. Change the ground, cliff and sand/ice patch

* The textures live in the materials in `Assets/Materials` (`Spring_Ground`, `Spring_Side`, `Spring_Cliff`, `Sand_Patch`,
  and the Fall / Winter ones). Click a material and replace its **Base Map** texture. The ground is tiled from a square
  texture, so use a square tile that repeats nicely.
* Or, in the theme, put a different material in **Ground Material**, **Side Material**, **Cliff Material** or
  **Patch Material** (for example give the Spring map `Fall_Ground`).
* **Has Patch / Patch Center / Patch Radius** control the sand or ice area in the middle, **Cliff Height** the dirt wall
  at the front, **Sky Color** the background.

## 5. See your change

With the Map Editor the map is already visible in the Scene view while you edit. To try it in the game:

1. Press **Play** (close the Map Editor or leave it; the preview is removed automatically).
2. Press **HOST**, then **START SOLO PRACTICE**. Spring is the first map, Fall comes after 2 minutes, Winter after 4.
3. The map is built when the game starts, so stop and press Play again after each change.

To look at Fall or Winter sooner, temporarily change `SeasonSeconds = 120f` to `15f` in
`Assets/Scripts/Core/GameTypes.cs` (line 22) and set it back to `120f` before you commit.

## 6. Keeping your edits safe

* **Rebuild Project Setup** (Season Rush menu) no longer touches themes or materials that already exist, so your
  edits survive it.
* **Season Rush > Reset Maps To Defaults** puts all three maps back to the generated layout. It asks first, and
  your edits are gone afterwards.
* The theme assets are text files. If two teammates edit the same theme at the same time, Git may show a conflict:
  agree who edits which season, and commit after each map change.

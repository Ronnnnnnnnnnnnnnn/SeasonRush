# Player sprites (player 1 and player 2)

| Player | Colour | Picture |
| --- | --- | --- |
| Player 1 (the host) | blue | `Assets/2D Asset/Players/player1.png` |
| Player 2 (the guest) | red | `Assets/2D Asset/Players/player2.png` |

## It is automatic

Put the two pictures in that folder (with exactly these names) and open the project. Unity then, by itself:

1. gives the pictures the right import settings (Sprite, one picture, mipmaps),
2. sets their size so a knight 1300 px tall on a 2100 px canvas is about **1.5 map units** tall (the size of the old
   placeholder warrior; `880 pixels per unit`),
3. puts the pivot at the **feet** (the bottom centre of the lowest part of the drawing, not the sword), so the knight stands
   exactly on its position and is sorted correctly behind and in front of trees,
4. creates `Assets/Resources/PlayerSkins.asset` and fills in the two slots.

The game then shows player 1 and player 2 with these pictures on every screen. No menu click is needed. The same happens
whenever one of the two pictures is replaced.

## Replacing the art

* Overwrite `player1.png` / `player2.png` (same names). Keep a **transparent background**.
* Another canvas size? Change `Ppu` in `Assets/Editor/PlayerSkinsSetup.cs` (a bigger number makes the character smaller),
  then run **Season Rush > Apply Player Sprites (player1.png + player2.png)**.
* Different file names, or another picture for one slot: open `Assets/Resources/PlayerSkins.asset` and drag a sprite
  into **Slot Sprites** (element 0 = player 1, element 1 = player 2). Give that picture its pivot at the feet
  (Sprite Editor > Pivot: Bottom, or Custom) and a pixels-per-unit that makes it about 1.5 units tall.
* An empty slot keeps the old grey placeholder warrior, so the game never breaks without art.

## Notes

* **Which way the character looks.** The knight pictures look to the **left** (the sword is on the left), so the game
  mirrors them while the player walks to the right. If you draw a character that looks to the **right**, untick
  **Art Faces Left** in `Assets/Resources/PlayerSkins.asset` and the game stops mirroring in the opposite way.
* The picture also flashes red when the player is hit.
* Each player is one still picture for now. Walk or attack animation frames would be a Week 4 addition.
* The tool never overwrites a picture you assigned by hand in `PlayerSkins.asset` during the automatic run; only the menu item
  **Apply Player Sprites** resets the two slots to player1.png / player2.png.

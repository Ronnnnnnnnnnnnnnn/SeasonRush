# Changing the look of the start menu (font, colours, texts)

All of it is in one asset: **`Assets/Resources/MenuStyle.asset`**. In Unity you can open it with **Season Rush > Select Menu
Style (edit menu font, texts, colours)**, or just click it in the Project window. The default look is **pastel** (light cream
panels, soft mint / sky-blue / coral buttons, dark blue-grey text) to go with the pastel art of the island.

The Inspector shows these groups:

| Group | What you can change |
| --- | --- |
| **Font** | Drag a font file (`.ttf` / `.otf`) into **Font** and the whole menu uses it. Empty = Unity's default font. |
| **Texts** | The game title, the line under it, and the text of every button: **PLAY**, **PROFILE**, **LEADERBOARD**, **LOCAL NETWORK**, **HOST A MATCH**, **JOIN MATCH**, **BACK**, **CANCEL** (and the small hint under PLAY). |
| **Button colours** | **Solid Buttons** (ticked = flat buttons in exactly the colour you pick, unticked = Unity's grey gradient buttons with the colour as a tint), **Hover Lighten** (how much lighter a button gets under the mouse), and the colours of the **PLAY** button, the **other buttons** (PROFILE, LEADERBOARD, BACK, ...) and the **CANCEL** button. |
| **Panels and backdrop** | **Panel Color** (the boxes behind the buttons, the profile and the leaderboard), **Field Color** (the name / IP / port input boxes) and **Backdrop Color** (a veil over the map behind the menu; its **A** value = how strong, 0 = no veil). |
| **Text colours** | The title, the button text, normal text, small help text, your own name / row (**Highlight**), and the colours of the "connected" (**Good**), "connecting" (**Pending**) and error (**Warn**) messages. |
| **Text sizes** | Title, PLAY button, normal buttons, the small buttons and labels. The menu is laid out for a screen 1080 pixels high, so the sizes scale with the window. |

* Changes show **at once**, also while the game is running in the Editor (press Play, keep the asset selected, and edit).
* The asset is created automatically the first time Unity opens the project. If you delete it, it comes back with the default look.
* A menu asset made by an earlier version (dark gradient buttons) is moved to the pastel colours automatically. Colours you
  had changed by hand are kept.
* To use a custom font: put the font file anywhere in `Assets/`, then drag it into **Font**.
* Want a different mood? Change the colours together: light panel + dark text works on pastel art; a dark panel needs light text.

**Not covered:** the in-game screen (score boxes, health bars, the result screen) is drawn in `Assets/Scripts/UI/GameHud.cs` and
the joystick in `Assets/Scripts/UI/VirtualControls.cs`; their colours, texts and sizes are still in the code.

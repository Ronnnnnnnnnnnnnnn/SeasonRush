# Playing Season Rush on an Android phone

The game is ready for phones: it locks to landscape, keeps the controls away from rounded corners and the gesture bar,
and has the on-screen joystick and ATTACK button. What is still missing on a fresh PC is **Unity's Android module**, so
you cannot make the app (an `.apk` file) until you install it. This is done once.

> **Status:** the Android build code is in the project, but an APK has **not been built or run on a real phone yet**
> (the module is not installed on the development PC). Expect to fix small things on the first try.
> An iPhone needs a Mac, so only Android is covered here.

## 1. Install Android Build Support (once, about 3-5 GB)

1. Close Unity. Open **Unity Hub > Installs**.
2. Click the gear of **6000.3.22f1** > **Add modules**.
3. Tick **Android Build Support** and, below it, **OpenJDK** and **Android SDK & NDK Tools**. Click **Install**.
4. Wait for it to finish, then open the project again.

## 2. Build the APK

Menu **Season Rush > Build Android APK (for a phone)**. The first build takes several minutes (Unity downloads Gradle).
The result is `Builds/Android/SeasonRush.apk`. If something is missing Unity shows a message saying what.

(The package name is `com.seasonrush.game`. You can change it in **Edit > Project Settings > Player > Android**.)

## 3. Put it on the phone

Easiest, no cable tools:

1. Copy `SeasonRush.apk` to the phone (USB cable, Google Drive, e-mail to yourself).
2. Open it on the phone. Android asks to allow **installing apps from this source** - allow it for that app (Files, Chrome, ...).
3. Install and open **Season Rush**.

With a cable and USB debugging (Settings > About phone > tap *Build number* 7 times > Developer options > USB debugging):

```bash
adb install -r Builds\Android\SeasonRush.apk
```

## 4. Playing between the phone and the PC

* **Same Wi-Fi:** on the PC start the game (Editor or `SeasonRush.exe`), tap **LOCAL NETWORK > HOST A MATCH**. On the phone tap
  **LOCAL NETWORK**, type the PC's address (the one shown in the host's lobby, for example `192.168.0.104`), port `7777`, and tap
  **JOIN MATCH**. If it does not connect, allow *SeasonRush / Unity* through the Windows firewall for private networks.
* **Over the internet (PLAY button):** needs the one-time online setup: the database (`docs/SpacetimeDB_Setup.md`) and the Unity
  Cloud project with Relay and Lobby (`docs/Online_Matchmaking_Setup.md`). Without them PLAY says online play is not set up.
* You do not need to touch the phone's rotation lock: the game forces landscape by itself.

## If something goes wrong

| What you see | Fix |
| --- | --- |
| "Android Build Support is not installed" | Do step 1 (the module must be added to the same Unity version, 6000.3.22f1). |
| Build error about JDK / SDK / NDK | In Unity: **Edit > Preferences > External Tools** > tick the *Installed with Unity* boxes for JDK, SDK and NDK. |
| The phone says "App not installed" | Uninstall an older copy first, or build again after changing nothing in the package name. |
| Controls feel too small or too big | Change `StickRadius` / `AttackRadius` in `Assets/Scripts/UI/VirtualControls.cs`. |
| Text is too small to read on the phone | Raise the text sizes in `Assets/Resources/MenuStyle.asset` (see `docs/Menu_Style.md`). |

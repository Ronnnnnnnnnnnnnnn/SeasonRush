using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SeasonRush.EditorTools
{
    public static class UiBuilder
    {
        const string SpriteDir = "Assets/UI/Sprites";

        enum A { TL, TC, TR, BL, BR, Wide, Full, TLCentre }

        static Font _font;
        static Sprite _round, _disc, _ring;

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Create UI Canvas In Scene")]
#endif
        public static void CreateFromMenu()
        {
            ConnectionMenu menu = Object.FindFirstObjectByType<ConnectionMenu>();
            GameHud hud = Object.FindFirstObjectByType<GameHud>();
            VirtualControls controls = Object.FindFirstObjectByType<VirtualControls>();
            if (menu == null || hud == null || controls == null)
            {
                EditorUtility.DisplayDialog("Season Rush", "Open Assets/Scenes/SeasonRush.unity first (the scene with the Managers), then run this again.\n\nOr run Season Rush > Rebuild Project Setup.", "OK");
                return;
            }
            Build(menu, hud, controls);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorUtility.DisplayDialog("Season Rush", "The UI canvas is in the scene (Hierarchy: UI).\n\nSave the scene (Ctrl+S).", "OK");
        }

        public static void Build(ConnectionMenu menu, GameHud hud, VirtualControls controls)
        {
            GameObject old = GameObject.Find("/UI");
            if (old != null) Object.DestroyImmediate(old);

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _disc = EnsureCircleSprite("JoystickDisc", true);
            _ring = EnsureCircleSprite("JoystickRing", false);

            var canvasGo = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            EnsureEventSystem();

            menu.canvas = canvas;
            menu.useMenuStyle = true;
            BuildMainMenu(canvas.transform, menu);
            BuildHud(canvas.transform, menu, hud);
            BuildControls(canvas.transform, controls);

            MenuStyle style = MenuStyle.Current;
            foreach (StyledElement element in canvas.GetComponentsInChildren<StyledElement>(true)) element.Apply(style);

            menu.profilePanel.SetActive(false);
            menu.leaderboardPanel.SetActive(false);
            menu.localPanel.SetActive(false);
            menu.searchingPanel.SetActive(false);
            hud.hudRoot.SetActive(false);
            controls.controlsRoot.SetActive(false);

            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(controls);
        }

        static void EnsureEventSystem()
        {
            EventSystem events = Object.FindFirstObjectByType<EventSystem>();
            if (events == null) events = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            events.sendNavigationEvents = false;
            var module = events.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        static void BuildMainMenu(Transform canvas, ConnectionMenu menu)
        {
            RectTransform root = Node("MainMenu", canvas, A.Full);
            menu.mainMenu = root.gameObject;

            Img("Backdrop", root, Color.white, A.Full, raycast: true).gameObject.AddComponent<StyledElement>().role = UiRole.Backdrop;
            Txt("Title", root, "SEASON RUSH", 64, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 100, 0, 90, bold: true, role: UiRole.LabelTitle, src: UiText.GameTitle);
            Image logo = Img("TitleLogo", root, Color.white, A.TC, 0, 24, 440, 184);
            logo.gameObject.AddComponent<StyledElement>().role = UiRole.LogoImage;
            Txt("Tagline", root, "", 20, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 958, 0, 36, role: UiRole.LabelText, src: UiText.Tagline);

            RectTransform home = Node("HomePanel", root, A.Full);
            menu.homePanel = home.gameObject;
            menu.homeDatabaseStatus = Txt("DatabaseStatus", home, "", 18, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 222, 0, 26, role: UiRole.LabelHint);
            PanelPicture(Img("Panel", home, Color.white, A.TC, 0, 270, 620, 610, round: true, raycast: true), UiPanelKind.Menu);
            Button play = Btn("PlayButton", home, "PLAY", UiRole.ButtonPrimary, UiRole.LabelPlayButton, UiText.Play, A.TC, 0, 300, 560, 120, 44, UiSkinColor.Spring, UiSkinSize.Large);
            Txt("PlayHint", home, "", 18, Color.white, TextAnchor.MiddleCenter, A.TC, 0, 424, 560, 28, role: UiRole.LabelHint, src: UiText.PlayHint);
            Button profile = Btn("ProfileButton", home, "PROFILE", UiRole.ButtonNormal, UiRole.LabelButton, UiText.Profile, A.TC, 0, 474, 560, 90, 28, UiSkinColor.Autumn, UiSkinSize.Medium);
            Button leaderboard = Btn("LeaderboardButton", home, "LEADERBOARD", UiRole.ButtonNormal, UiRole.LabelButton, UiText.Leaderboard, A.TC, 0, 584, 560, 90, 28, UiSkinColor.Winter, UiSkinSize.Medium);
            Button local = Btn("LocalNetworkButton", home, "LOCAL NETWORK", UiRole.ButtonNormal, UiRole.LabelSmallButton, UiText.LocalNetwork, A.TC, 0, 704, 440, 60, 22, UiSkinColor.Spring, UiSkinSize.Small);
            menu.homeMessage = Txt("Message", home, "", 19, Color.white, TextAnchor.UpperCenter, A.TC, 0, 780, 600, 90, wrap: true, role: UiRole.LabelText);
            Txt("ControlsHelp", home, "Move: joystick / WASD / Arrows     Attack: button / Space / J", 18, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 1018, 0, 30, role: UiRole.LabelText);

            RectTransform prof = PanelPicture(Img("ProfilePanel", root, Color.white, A.TC, 0, 250, 540, 700, round: true, raycast: true), UiPanelKind.Profile).rectTransform;
            menu.profilePanel = prof.gameObject;
            Txt("Title", prof, "YOUR PROFILE", 28, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 16, 484, 40, role: UiRole.LabelHeading);
            Txt("NameLabel", prof, "Name", 22, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 72, 100, 36, role: UiRole.LabelBody);
            menu.nameField = Field("NameField", prof, "Your name", LocalPlayer.MaxLength, A.TL, 130, 64, 382, 52);
            menu.nameHint = Txt("NameHint", prof, "", 16, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 122, 484, 26, role: UiRole.LabelHint);
            Button saveName = Btn("SaveNameButton", prof, "Save name", UiRole.ButtonNormal, UiRole.LabelSmallButton, UiText.None, A.TL, 312, 152, 200, 46, 22, UiSkinColor.Winter, UiSkinSize.Small);
            menu.profileDatabaseStatus = Txt("DatabaseStatus", prof, "", 20, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 208, 484, 28, role: UiRole.LabelHint);
            menu.profileDatabaseError = Txt("DatabaseError", prof, "", 15, Color.white, TextAnchor.UpperLeft, A.TL, 28, 236, 484, 40, wrap: true, role: UiRole.LabelText);
            Divider(prof, 28, 276, 484);

            RectTransform stats = Node("Stats", prof, A.TL, 0, 288, 540, 220);
            menu.profileStats = stats.gameObject;
            menu.profileName = Txt("PlayerName", stats, "", 26, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 0, 484, 34, role: UiRole.LabelHighlight);
            string[] statNames = { "Matches played", "Wins / Losses / Draws", "Total points", "Kills / Deaths" };
            menu.profileStatValues = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                float y = 44 + 42 * i;
                Txt("Label" + (i + 1), stats, statNames[i], 20, Color.white, TextAnchor.MiddleLeft, A.TL, 28, y, 280, 34, role: UiRole.LabelHint);
                menu.profileStatValues[i] = Txt("Value" + (i + 1), stats, "", 22, Color.white, TextAnchor.MiddleRight, A.TL, 292, y, 220, 34, role: UiRole.LabelText);
                RowLine(stats, 28, y + 36, 484);
            }
            menu.profileEmpty = Txt("NoProfile", prof, "", 18, Color.white, TextAnchor.UpperLeft, A.TL, 28, 292, 484, 70, wrap: true, role: UiRole.LabelHint);
            menu.autoFindToggle = MakeToggle("AutoSearchToggle", prof, "Search automatically when the game opens", A.TL, 28, 568, 484, 34);
            Button profileBack = Btn("BackButton", prof, "BACK", UiRole.ButtonNormal, UiRole.LabelSmallButton, UiText.Back, A.TL, 150, 626, 240, 56, 28, UiSkinColor.Winter, UiSkinSize.Small);

            RectTransform board = PanelPicture(Img("LeaderboardPanel", root, Color.white, A.TC, 0, 250, 540, 700, round: true, raycast: true), UiPanelKind.Leaderboard).rectTransform;
            menu.leaderboardPanel = board.gameObject;
            Txt("Title", board, "LEADERBOARD", 28, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 16, 484, 40, role: UiRole.LabelHeading);
            menu.leaderboardNotConnected = Txt("NotConnected", board, "Not connected to the online database.", 18, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 76, 484, 30, role: UiRole.LabelHint);
            RectTransform content = Node("Content", board, A.TL, 0, 0, 540, 700);
            menu.leaderboardContent = content.gameObject;
            Txt("HeaderRank", content, "#", 16, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 64, 40, 24, role: UiRole.LabelHint);
            Txt("HeaderPlayer", content, "Player", 16, Color.white, TextAnchor.MiddleLeft, A.TL, 68, 64, 220, 24, role: UiRole.LabelHint);
            Txt("HeaderWins", content, "Wins", 16, Color.white, TextAnchor.MiddleRight, A.TL, 292, 64, 70, 24, role: UiRole.LabelHint);
            Txt("HeaderPoints", content, "Points", 16, Color.white, TextAnchor.MiddleRight, A.TL, 366, 64, 146, 24, role: UiRole.LabelHint);
            RowLine(content, 28, 90, 484);
            menu.leaderboardRows = new LeaderboardRow[8];
            for (int i = 0; i < 8; i++)
            {
                RectTransform row = Node("Row" + (i + 1), content, A.TL, 0, 94 + 32 * i, 540, 32);
                var r = row.gameObject.AddComponent<LeaderboardRow>();
                r.rank = Txt("Rank", row, "", 20, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 0, 40, 30, role: UiRole.LabelText);
                r.playerName = Txt("Player", row, "", 20, Color.white, TextAnchor.MiddleLeft, A.TL, 68, 0, 226, 30, role: UiRole.LabelText);
                r.wins = Txt("Wins", row, "", 20, Color.white, TextAnchor.MiddleRight, A.TL, 292, 0, 70, 30, role: UiRole.LabelText);
                r.points = Txt("Points", row, "", 20, Color.white, TextAnchor.MiddleRight, A.TL, 366, 0, 146, 30, role: UiRole.LabelText);
                RowLine(row, 28, 30, 484);
                menu.leaderboardRows[i] = r;
            }
            menu.leaderboardEmpty = Txt("Empty", content, "No matches played yet - be the first!", 18, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 98, 484, 30, role: UiRole.LabelHint);
            Txt("OnlineLegend", content, "* = online now", 14, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 358, 484, 22, role: UiRole.LabelHint);
            Divider(content, 28, 392, 484);
            Txt("RecentTitle", content, "RECENT MATCHES", 20, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 408, 484, 34, role: UiRole.LabelHeading);
            menu.recentNone = Txt("RecentNone", content, "None yet.", 16, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 450, 484, 26, role: UiRole.LabelHint);
            menu.recentMatchRows = new Text[5];
            for (int i = 0; i < 5; i++)
                menu.recentMatchRows[i] = Txt("Recent" + (i + 1), content, "", 17, Color.white, TextAnchor.MiddleLeft, A.TL, 28, 450 + 28 * i, 484, 26, role: UiRole.LabelText);
            Button boardBack = Btn("BackButton", board, "BACK", UiRole.ButtonNormal, UiRole.LabelSmallButton, UiText.Back, A.TL, 150, 626, 240, 56, 28, UiSkinColor.Winter, UiSkinSize.Small);

            RectTransform lan = PanelPicture(Img("LocalPanel", root, Color.white, A.TC, 0, 250, 620, 640, round: true, raycast: true), UiPanelKind.LocalNetwork).rectTransform;
            menu.localPanel = lan.gameObject;
            Txt("Title", lan, "LOCAL NETWORK", 30, Color.white, TextAnchor.MiddleCenter, A.TL, 30, 18, 560, 44, role: UiRole.LabelHeading);
            Txt("Hint", lan, "Play with someone on the same Wi-Fi, or on this PC.", 17, Color.white, TextAnchor.MiddleCenter, A.TL, 30, 66, 560, 28, role: UiRole.LabelHint);
            menu.hostButton = Btn("HostButton", lan, "HOST A MATCH", UiRole.ButtonPrimary, UiRole.LabelButton, UiText.Host, A.TL, 30, 104, 560, 90, 28, UiSkinColor.Spring, UiSkinSize.Medium);
            Txt("HostIpLabel", lan, "Host IP", 22, Color.white, TextAnchor.MiddleLeft, A.TL, 30, 216, 150, 52, role: UiRole.LabelBody);
            menu.addressField = Field("HostIpField", lan, "192.168.1.20", 40, A.TL, 180, 216, 410, 52);
            Txt("PortLabel", lan, "Port", 22, Color.white, TextAnchor.MiddleLeft, A.TL, 30, 282, 150, 52, role: UiRole.LabelBody);
            menu.portField = Field("PortField", lan, "7777", 5, A.TL, 180, 282, 410, 52);
            menu.joinButton = Btn("JoinButton", lan, "JOIN MATCH", UiRole.ButtonCancel, UiRole.LabelButton, UiText.Join, A.TL, 30, 352, 560, 90, 28, UiSkinColor.Autumn, UiSkinSize.Medium);
            menu.joinLabel = menu.joinButton.GetComponentInChildren<Text>();
            menu.cancelButton = Btn("CancelButton", lan, "CANCEL", UiRole.ButtonCancel, UiRole.LabelSmallButton, UiText.Cancel, A.TL, 210, 456, 200, 46, 22, UiSkinColor.Autumn, UiSkinSize.Small);
            menu.localMessage = Txt("Message", lan, "", 19, Color.white, TextAnchor.UpperCenter, A.TL, 10, 506, 600, 40, wrap: true, role: UiRole.LabelText);
            menu.localBackButton = Btn("BackButton", lan, "BACK", UiRole.ButtonNormal, UiRole.LabelSmallButton, UiText.Back, A.TL, 190, 560, 240, 56, 28, UiSkinColor.Winter, UiSkinSize.Small);

            RectTransform search = Node("SearchingPanel", root, A.Full);
            menu.searchingPanel = search.gameObject;
            PanelPicture(Img("Panel", search, Color.white, A.TC, 0, 250, 620, 640, round: true, raycast: true), UiPanelKind.LocalNetwork);
            menu.searchTitle = Txt("Searching", search, "SEARCHING...", 38, Color.white, TextAnchor.MiddleCenter, A.TC, 0, 330, 560, 60, bold: true, role: UiRole.LabelHeading);
            menu.searchStage = Txt("Stage", search, "", 24, Color.white, TextAnchor.MiddleCenter, A.TC, 0, 420, 560, 70, wrap: true, role: UiRole.LabelText);
            menu.searchTimer = Txt("Timer", search, "0:00", 24, Color.white, TextAnchor.MiddleCenter, A.TC, 0, 500, 560, 40, role: UiRole.LabelText);
            Txt("Info", search, "You will be paired with the next player who presses PLAY.", 24, Color.white, TextAnchor.MiddleCenter, A.TC, 0, 560, 560, 110, wrap: true, role: UiRole.LabelText);
            Button searchCancel = Btn("CancelButton", search, "CANCEL", UiRole.ButtonCancel, UiRole.LabelButton, UiText.Cancel, A.TC, 0, 700, 300, 80, 28, UiSkinColor.Autumn, UiSkinSize.Medium);

            Switch(profile, home, prof);
            Switch(leaderboard, home, board);
            Switch(local, home, lan);
            Switch(profileBack, prof, home);
            Switch(boardBack, board, home);
            Switch(menu.localBackButton, lan, home);
            UnityEventTools.AddPersistentListener(play.onClick, menu.FindMatch);
            UnityEventTools.AddPersistentListener(saveName.onClick, menu.ApplyName);
            UnityEventTools.AddPersistentListener(menu.hostButton.onClick, menu.StartHost);
            UnityEventTools.AddPersistentListener(menu.joinButton.onClick, menu.StartClient);
            UnityEventTools.AddPersistentListener(menu.cancelButton.onClick, menu.CancelConnection);
            UnityEventTools.AddPersistentListener(searchCancel.onClick, menu.CancelConnection);
        }

        static Image PanelPicture(Image image, UiPanelKind kind)
        {
            var styled = image.gameObject.AddComponent<StyledElement>();
            styled.role = UiRole.Panel;
            styled.panelKind = kind;
            return image;
        }

        static void Divider(Transform parent, float x, float y, float w)
        {
            Image line = Img("Divider", parent, Color.white, A.TL, x, y, w, 8);
            line.gameObject.AddComponent<StyledElement>().role = UiRole.Divider;
        }

        static void RowLine(Transform parent, float x, float y, float w)
        {
            Img("RowLine", parent, new Color(0.86f, 0.78f, 0.64f, 0.9f), A.TL, x, y, w, 2);
        }

        static void BuildHud(Transform canvas, ConnectionMenu menu, GameHud hud)
        {
            RectTransform root = Node("HUD", canvas, A.Full);
            hud.hudRoot = root.gameObject;

            RectTransform statusSafe = Node("StatusSafeArea", root, A.Full);
            menu.roleText = Light("RoleText", statusSafe, "", 18, Color.white, TextAnchor.MiddleRight, A.BR, 24, 12, 420, 36, heading: false);

            RectTransform connecting = Node("ConnectingPanel", root, A.Full);
            hud.connectingText = connecting.gameObject;
            Light("Text", connecting, "Connecting...", 48, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 460, 0, 80);
            Button connectingCancel = Btn("CancelButton", connecting, "CANCEL", UiRole.ButtonCancel, UiRole.LabelButton, UiText.Cancel, A.TC, 0, 560, 300, 80, 28, UiSkinColor.Autumn, UiSkinSize.Medium);
            UnityEventTools.AddPersistentListener(connectingCancel.onClick, menu.LeaveToMenu);

            RectTransform match = Node("MatchUi", root, A.Full);
            hud.matchUi = match.gameObject;

            RectTransform overlays = Node("WorldOverlays", match, A.Full);
            hud.overlayRoot = overlays;
            hud.barTemplate = BuildBarTemplate(overlays);
            hud.labelTemplate = Light("LabelTemplate", overlays, "", 20, Color.white, TextAnchor.MiddleCenter, A.TLCentre, 0, 0, 160, 48);
            hud.labelTemplate.gameObject.SetActive(false);

            RectTransform safe = Node("SafeArea", match, A.Full);
            hud.safeAreas = new[] { statusSafe, safe };

            RectTransform top = HudBox("TopBar", safe, A.TC, 0, 10, 380, 104, UiPanelKind.Menu).rectTransform;
            hud.topTitle = Ink("Title", top, "", 28, TextAnchor.MiddleCenter, A.Wide, 0, 10, 0, 40);
            hud.topTime = Ink("Time", top, "--:--", 54, TextAnchor.MiddleCenter, A.Wide, 0, 42, 0, 56);

            hud.playerPanels = new[] { BuildPlayerPanel(safe, "PlayerPanel1", true), BuildPlayerPanel(safe, "PlayerPanel2", false) };

            Button pause = Btn("PauseButton", safe, "PAUSE", UiRole.ButtonNormal, UiRole.LabelButton, UiText.None, A.TR, 24, 130, 170, 56, 28, UiSkinColor.Winter, UiSkinSize.Small);
            hud.pauseButton = pause.gameObject;
            UnityEventTools.AddPersistentListener(pause.onClick, hud.PauseGame);

            hud.controlsHint = Light("ControlsHint", safe, "Move: joystick / WASD / Arrows     Attack: button / Space / J", 17, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleLeft, A.BL, 24, 8, 900, 28, heading: false).gameObject;

            RectTransform banner = Node("Banner", match, A.Full);
            hud.banner = banner.gameObject.AddComponent<CanvasGroup>();
            hud.banner.blocksRaycasts = false;
            Img("Band", banner, new Color(0, 0, 0, 0.5f), A.Wide, 0, 300, 0, 170);
            hud.bannerTitle = Light("Title", banner, "", 100, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 300, 0, 120);
            hud.bannerSub = Light("Subtitle", banner, "", 24, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 418, 0, 44, heading: false);
            banner.gameObject.SetActive(false);

            RectTransform lobby = Node("LobbyPanel", match, A.Full);
            hud.lobbyPanel = lobby.gameObject;
            HudBox("Box", lobby, A.TC, 0, 370, 780, 290, UiPanelKind.LocalNetwork);
            hud.lobbyTitle = Ink("Title", lobby, "WAITING FOR YOUR RIVAL...", 34, TextAnchor.MiddleCenter, A.Wide, 0, 392, 0, 60);
            hud.lobbyLine1 = Ink("Line1", lobby, "", 24, TextAnchor.MiddleCenter, A.Wide, 0, 468, 0, 40, heading: false);
            hud.lobbyLine2 = Ink("Line2", lobby, "", 20, TextAnchor.MiddleCenter, A.Wide, 0, 516, 0, 36, heading: false);
            hud.lobbyHostLabel = Ink("HostLabel", lobby, "Tell your friend to join this address:", 20, TextAnchor.MiddleCenter, A.Wide, 0, 456, 0, 34, heading: false);
            Text address = Txt("Address", lobby, "", 30, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 490, 0, 44, role: UiRole.LabelHighlight);
            hud.lobbyAddress = address;
            Button solo = Btn("SoloButton", lobby, "START SOLO PRACTICE", UiRole.ButtonPrimary, UiRole.LabelButton, UiText.None, A.TC, 0, 560, 480, 80, 28, UiSkinColor.Spring, UiSkinSize.Medium);
            hud.soloButton = solo.gameObject;
            UnityEventTools.AddPersistentListener(solo.onClick, hud.StartSoloPractice);
            Button lobbyBack = Btn("BackButton", lobby, "BACK", UiRole.ButtonNormal, UiRole.LabelButton, UiText.Back, A.TC, 0, 684, 300, 56, 28, UiSkinColor.Winter, UiSkinSize.Small);
            UnityEventTools.AddPersistentListener(lobbyBack.onClick, hud.LobbyBack);
            lobby.gameObject.SetActive(false);

            hud.countdownText = Light("Countdown", match, "3", 200, new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleCenter, A.Wide, 0, 330, 0, 260);
            hud.countdownText.gameObject.SetActive(false);

            RectTransform result = Node("ResultPanel", match, A.Full);
            hud.resultPanel = result.gameObject;
            Img("Dim", result, new Color(0, 0, 0, 0.65f), A.Full);
            hud.resultHeadline = Light("Headline", result, "", 110, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 200, 0, 150);
            hud.resultNote = Light("Note", result, "", 30, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleCenter, A.Wide, 0, 336, 0, 40, heading: false);
            hud.resultLines = new Text[2];
            for (int i = 0; i < 2; i++)
                hud.resultLines[i] = Light("PlayerLine" + (i + 1), result, "", 32, Color.white, TextAnchor.MiddleCenter, A.TC, 0, 380 + 62 * i, 1500, 52);
            hud.resultRecord = Light("Record", result, "", 24, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleCenter, A.TC, 0, 0, 1500, 40, heading: false);
            Button rematch = Btn("RematchButton", result, "REMATCH", UiRole.ButtonPrimary, UiRole.LabelButton, UiText.None, A.TC, -170, 600, 300, 80, 28, UiSkinColor.Spring, UiSkinSize.Medium);
            hud.rematchButton = (RectTransform)rematch.transform;
            UnityEventTools.AddPersistentListener(rematch.onClick, hud.Rematch);
            Button resultBack = Btn("BackButton", result, "BACK", UiRole.ButtonNormal, UiRole.LabelButton, UiText.Back, A.TC, 170, 600, 300, 80, 28, UiSkinColor.Winter, UiSkinSize.Medium);
            hud.resultBackButton = (RectTransform)resultBack.transform;
            UnityEventTools.AddPersistentListener(resultBack.onClick, hud.ResultBack);
            hud.rematchWaiting = Light("WaitingForHost", result, "Waiting for the host to start a rematch...", 24, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleCenter, A.TC, 0, 600, 1500, 40, heading: false);
            result.gameObject.SetActive(false);

            RectTransform death = Node("DeathPanel", match, A.Full);
            hud.deathPanel = death.gameObject;
            Img("Tint", death, new Color(0.6f, 0f, 0f, 0.22f), A.Full);
            Light("Title", death, "YOU WERE DEFEATED", 70, new Color(1f, 0.85f, 0.85f), TextAnchor.MiddleCenter, A.Wide, 0, 400, 0, 100);
            hud.deathSub = Light("Respawn", death, "", 28, Color.white, TextAnchor.MiddleCenter, A.Wide, 0, 500, 0, 50, heading: false);
            death.gameObject.SetActive(false);

            RectTransform pausePanel = Node("PausePanel", match, A.Full);
            hud.pausePanel = pausePanel.gameObject;
            Img("Dim", pausePanel, new Color(0, 0, 0, 0.55f), A.Full, raycast: true);
            RectTransform pauseBox = HudBox("Box", pausePanel, A.TC, 0, 270, 640, 520, UiPanelKind.Menu).rectTransform;
            Ink("Title", pauseBox, "PAUSED", 64, TextAnchor.MiddleCenter, A.Wide, 0, 26, 0, 90);
            hud.pauseSubtitle = Ink("Subtitle", pauseBox, "The game is frozen.", 20, TextAnchor.MiddleCenter, A.TL, 40, 124, 560, 64, heading: false);
            hud.pauseSubtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            Button resume = Btn("ResumeButton", pauseBox, "RESUME", UiRole.ButtonPrimary, UiRole.LabelButton, UiText.None, A.TC, 0, 200, 460, 90, 28, UiSkinColor.Spring, UiSkinSize.Medium);
            UnityEventTools.AddPersistentListener(resume.onClick, hud.ResumeGame);
            Button surrender = Btn("SurrenderButton", pauseBox, "SURRENDER", UiRole.ButtonCancel, UiRole.LabelButton, UiText.None, A.TC, 0, 312, 460, 90, 28, UiSkinColor.Autumn, UiSkinSize.Medium);
            UnityEventTools.AddPersistentListener(surrender.onClick, hud.Surrender);
            Ink("Hint", pauseBox, "Surrender: your rival wins the match.   Esc: resume", 18, TextAnchor.MiddleCenter, A.Wide, 0, 430, 0, 32, heading: false);
            pausePanel.gameObject.SetActive(false);

            RectTransform rivalPause = Node("PausedByRival", match, A.Full);
            hud.pausedByRival = rivalPause.gameObject;
            Img("Dim", rivalPause, new Color(0, 0, 0, 0.45f), A.Full, raycast: true);
            RectTransform rivalBox = HudBox("Box", rivalPause, A.TC, 0, 400, 640, 220, UiPanelKind.LocalNetwork).rectTransform;
            hud.pausedByRivalText = Ink("Text", rivalBox, "", 32, TextAnchor.MiddleCenter, A.TL, 30, 30, 580, 160);
            hud.pausedByRivalText.horizontalOverflow = HorizontalWrapMode.Wrap;
            rivalPause.gameObject.SetActive(false);
        }

        static Image HudBox(string name, Transform parent, A a, float x, float y, float w, float h, UiPanelKind kind)
        {
            Image box = Img(name, parent, new Color(0, 0, 0, 0.55f), a, x, y, w, h, round: true);
            var styled = box.gameObject.AddComponent<StyledElement>();
            styled.role = UiRole.HudBox;
            styled.panelKind = kind;
            return box;
        }

        static Text Light(string name, Transform parent, string text, int size, Color colour, TextAnchor anchor, A a,
                          float x, float y, float w, float h, bool heading = true)
        {
            return Txt(name, parent, text, size, colour, anchor, a, x, y, w, h, shadow: true, role: heading ? UiRole.HudLight : UiRole.HudLightBody);
        }

        static Text Ink(string name, Transform parent, string text, int size, TextAnchor anchor, A a,
                        float x, float y, float w, float h, bool heading = true)
        {
            return Txt(name, parent, text, size, Color.white, anchor, a, x, y, w, h, role: heading ? UiRole.HudInk : UiRole.HudInkBody);
        }

        static PlayerPanelUi BuildPlayerPanel(Transform parent, string name, bool left)
        {
            Image box = HudBox(name, parent, left ? A.TL : A.TR, 24, 14, 380, 104, UiPanelKind.Menu);
            var panel = box.gameObject.AddComponent<PlayerPanelUi>();
            panel.strip = Img("ColourStrip", box.transform, Color.white, left ? A.TL : A.TR, 10, 12, 8, 80);
            panel.who = Ink("Name", box.transform, "", 24, TextAnchor.MiddleLeft, A.TL, 28, 10, 200, 32);
            panel.waiting = Ink("Waiting", box.transform, "waiting for player...", 20, TextAnchor.MiddleLeft, A.TL, 28, 48, 320, 40, heading: false);

            RectTransform details = Node("Details", box.transform, A.Full);
            panel.details = details.gameObject;
            panel.score = Ink("Score", details, "0", 48, TextAnchor.MiddleRight, A.TL, 190, 6, 160, 56);
            Image barBg = Img("HealthBar", details, new Color(0.2f, 0.12f, 0.07f, 0.9f), A.TL, 28, 62, 324, 16);
            panel.healthFillImage = Img("Fill", barBg.transform, Color.green, A.Full);
            panel.healthFill = panel.healthFillImage.rectTransform;
            panel.kills = Ink("Kills", details, "", 16, TextAnchor.MiddleLeft, A.TL, 28, 80, 324, 22, heading: false);
            return panel;
        }

        static WorldBarUi BuildBarTemplate(Transform parent)
        {
            Image bg = Img("BarTemplate", parent, new Color(0, 0, 0, 0.75f), A.TLCentre, 0, 0, 68, 12);
            var bar = bg.gameObject.AddComponent<WorldBarUi>();
            RectTransform inner = Node("Inner", bg.transform, A.Full);
            inner.offsetMin = new Vector2(2f, 2f);
            inner.offsetMax = new Vector2(-2f, -2f);
            bar.fillImage = Img("Fill", inner, Color.red, A.Full);
            bar.fill = bar.fillImage.rectTransform;
            bg.gameObject.SetActive(false);
            return bar;
        }

        static void BuildControls(Transform canvas, VirtualControls controls)
        {
            RectTransform root = Node("Controls", canvas, A.Full);
            controls.controlsRoot = root.gameObject;
            controls.stickRing = Img("StickRing", root, Color.white, A.TLCentre, 230, 850, 240, 240, sprite: _ring);
            controls.stickKnob = Img("StickKnob", root, Color.white, A.TLCentre, 230, 850, 108, 108, sprite: _disc);
            controls.attackDisc = Img("AttackDisc", root, Color.white, A.TLCentre, 1700, 860, 200, 200, sprite: _disc);
            controls.attackRing = Img("AttackRing", root, Color.white, A.TLCentre, 1700, 860, 200, 200, sprite: _ring);
            controls.attackLabel = Txt("AttackLabel", controls.attackRing.transform, "ATTACK", 30, Color.white, TextAnchor.MiddleCenter, A.Full, bold: true, role: UiRole.HudLight);
        }

        static RectTransform Node(string name, Transform parent, A a, float x = 0, float y = 0, float w = 0, float h = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            switch (a)
            {
                case A.TL: Anchor(rt, 0, 1, 0, 1, 0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); break;
                case A.TC: Anchor(rt, .5f, 1, .5f, 1, .5f, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); break;
                case A.TR: Anchor(rt, 1, 1, 1, 1, 1, 1); rt.anchoredPosition = new Vector2(-x, -y); rt.sizeDelta = new Vector2(w, h); break;
                case A.BL: Anchor(rt, 0, 0, 0, 0, 0, 0); rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h); break;
                case A.BR: Anchor(rt, 1, 0, 1, 0, 1, 0); rt.anchoredPosition = new Vector2(-x, y); rt.sizeDelta = new Vector2(w, h); break;
                case A.TLCentre: Anchor(rt, 0, 1, 0, 1, .5f, .5f); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); break;
                case A.Wide: Anchor(rt, 0, 1, 1, 1, .5f, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(0, h); break;
                default: Anchor(rt, 0, 0, 1, 1, .5f, .5f); rt.offsetMin = rt.offsetMax = Vector2.zero; break;
            }
            return rt;
        }

        static void Anchor(RectTransform rt, float minX, float minY, float maxX, float maxY, float pivotX, float pivotY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.pivot = new Vector2(pivotX, pivotY);
        }

        static Image Img(string name, Transform parent, Color colour, A a, float x = 0, float y = 0, float w = 0, float h = 0,
                         bool round = false, bool raycast = false, Sprite sprite = null)
        {
            RectTransform rt = Node(name, parent, a, x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = raycast;
            if (sprite != null) image.sprite = sprite;
            else if (round) { image.sprite = _round; image.type = Image.Type.Sliced; }
            return image;
        }

        static Text Txt(string name, Transform parent, string text, int size, Color colour, TextAnchor anchor, A a,
                        float x = 0, float y = 0, float w = 0, float h = 0,
                        bool bold = false, bool shadow = false, bool wrap = false, UiRole role = UiRole.None, UiText src = UiText.None)
        {
            RectTransform rt = Node(name, parent, a, x, y, w, h);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.alignment = anchor;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var outline = rt.gameObject.AddComponent<Shadow>();
                outline.effectColor = new Color(0, 0, 0, 0.85f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            if (role != UiRole.None || src != UiText.None)
            {
                var styled = rt.gameObject.AddComponent<StyledElement>();
                styled.role = role;
                styled.text = src;
            }
            return label;
        }

        static Button Btn(string name, Transform parent, string label, UiRole role, UiRole labelRole, UiText src, A a,
                          float x, float y, float w, float h, int labelSize,
                          UiSkinColor skinColor = UiSkinColor.Auto, UiSkinSize skinSize = UiSkinSize.Medium)
        {
            Image image = Img(name, parent, Color.white, a, x, y, w, h, round: true, raycast: true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var styled = image.gameObject.AddComponent<StyledElement>();
            styled.role = role;
            styled.skinColor = skinColor;
            styled.skinSize = skinSize;
            Txt("Label", image.transform, label, labelSize, Color.black, TextAnchor.MiddleCenter, A.Full, bold: true, role: labelRole, src: src);
            return button;
        }

        static InputField Field(string name, Transform parent, string placeholder, int limit, A a, float x, float y, float w, float h)
        {
            Image image = Img(name, parent, Color.white, a, x, y, w, h, round: true, raycast: true);
            image.gameObject.AddComponent<StyledElement>().role = UiRole.Field;
            var input = image.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.navigation = new Navigation { mode = Navigation.Mode.None };

            Text text = Txt("Text", image.transform, "", 24, Color.black, TextAnchor.MiddleLeft, A.Full, role: UiRole.LabelField);
            text.rectTransform.offsetMin = new Vector2(22f, 4f);
            text.rectTransform.offsetMax = new Vector2(-22f, -4f);
            Text hint = Txt("Placeholder", image.transform, placeholder, 22, new Color(0.3f, 0.3f, 0.4f, 0.5f), TextAnchor.MiddleLeft, A.Full);
            hint.fontStyle = FontStyle.Italic;
            hint.rectTransform.offsetMin = new Vector2(22f, 4f);
            hint.rectTransform.offsetMax = new Vector2(-22f, -4f);

            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = limit;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        static Toggle MakeToggle(string name, Transform parent, string label, A a, float x, float y, float w, float h)
        {
            RectTransform root = Node(name, parent, a, x, y, w, h);
            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            Image box = Img("Box", root, Color.white, A.TL, 0, 3, 28, 28, round: true, raycast: true);
            box.gameObject.AddComponent<StyledElement>().role = UiRole.CheckBox;
            Image check = Img("Checkmark", box.transform, new Color(0.18f, 0.55f, 0.32f), A.Full, round: true);
            check.gameObject.AddComponent<StyledElement>().role = UiRole.CheckMark;
            Txt("Label", root, label, 18, Color.white, TextAnchor.MiddleLeft, A.TL, 40, 0, w - 40, h, role: UiRole.LabelText);
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        static void Switch(Button button, Component hide, Component show)
        {
            UnityEventTools.AddBoolPersistentListener(button.onClick, hide.gameObject.SetActive, false);
            UnityEventTools.AddBoolPersistentListener(button.onClick, show.gameObject.SetActive, true);
        }

        static Sprite EnsureCircleSprite(string name, bool filled)
        {
            string path = $"{SpriteDir}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(SpriteDir);
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float alpha = filled
                        ? Mathf.Clamp01((1f - d) * 18f)
                        : Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) / 0.06f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}

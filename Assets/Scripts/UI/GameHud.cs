using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace SeasonRush
{
    /// <summary>
    /// The heads-up display of a match. Everything is a real object of the UI canvas in the scene (Hierarchy: UI > HUD):
    /// top bar, the two player boxes, PAUSE button, season banner, lobby, countdown, result screen, death screen and pause screen.
    /// This script only switches those objects on and off (SetActive) and fills in their texts and bars.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public static GameHud Instance { get; private set; }

        /// <summary>True while the game is frozen by a PAUSE (for both players). The state lives on the server (MatchManager.Paused).</summary>
        public static bool Paused => MatchManager.Instance != null && MatchManager.Instance.Paused.Value;

        /// <summary>Asks the server to freeze / unfreeze the game.</summary>
        public static void SetPaused(bool value)
        {
            MatchManager mm = MatchManager.Instance;
            bool inMatch = mm != null && (mm.Phase.Value == MatchPhase.Countdown || mm.Phase.Value == MatchPhase.Playing);
            if (mm == null || (value && !inMatch)) return;
            mm.RequestPause(value);
        }

        /// <summary>Colour of the text on the boxes: dark brown with the wooden skin, white on the dark boxes of the flat look.</summary>
        static Color Ink => MenuStyle.Current.SkinActive ? MenuStyle.Current.skinTextColor : Color.white;

        static Color Alert => MenuStyle.Current.SkinActive ? MenuStyle.Current.skinWarnColor : new Color(1f, 0.45f, 0.4f);

        static int LocalSlot => PlayerStats.Local != null ? PlayerStats.Local.Slot.Value : -1;

        /// <summary>True when the pause was started by this player (only this player can resume it).</summary>
        public static bool PausedByMe => Paused && MatchManager.Instance.PausedBySlot.Value == LocalSlot;

        /// <summary>Automated tests ignore the mouse so that a stray real click cannot press PAUSE / BACK / QUIT.</summary>
        static bool TestBlocksClicks => GameConfig.TestMode && !GameConfig.HasFlag("-allowclicks");

        struct Popup
        {
            public Vector2 plane;
            public string text;
            public Color colour;
            public float born;
        }

        [Header("Canvas (made by Season Rush > Create UI Canvas In Scene)")]
        [Tooltip("Switched on while a match / lobby is running, off in the main menu.")]
        public GameObject hudRoot;
        [Tooltip("Containers that keep the HUD clear of notches and rounded corners on phones.")]
        public RectTransform[] safeAreas;
        public GameObject connectingText;
        [Tooltip("Everything that needs the MatchManager (hidden while connecting).")]
        public GameObject matchUi;

        [Header("Top bar and player boxes")]
        public Text topTitle;
        public Text topTime;
        public PlayerPanelUi[] playerPanels;       // slot 0 (left) and slot 1 (right)
        public GameObject controlsHint;

        [Header("PAUSE button and pause screen")]
        public GameObject pauseButton;
        public GameObject pausePanel;
        public Text pauseSubtitle;
        [Tooltip("Shown to the other player while a rival has the game paused.")]
        public GameObject pausedByRival;
        public Text pausedByRivalText;

        [Header("Season banner")]
        public CanvasGroup banner;
        public Text bannerTitle;
        public Text bannerSub;

        [Header("Lobby")]
        public GameObject lobbyPanel;
        public Text lobbyTitle;
        public Text lobbyLine1;                    // online / client lines
        public Text lobbyLine2;
        public Text lobbyHostLabel;                // "Tell your friend to join this address:"
        public Text lobbyAddress;
        public GameObject soloButton;

        [Header("Countdown")]
        public Text countdownText;

        [Header("Result screen")]
        public GameObject resultPanel;
        public Text resultHeadline;
        [Tooltip("A line under the headline: \"Your rival surrendered.\"")]
        public Text resultNote;
        public Text[] resultLines;                 // one per player
        public Text resultRecord;
        public RectTransform rematchButton;
        public RectTransform resultBackButton;
        public Text rematchWaiting;

        [Header("Death screen")]
        public GameObject deathPanel;
        public Text deathSub;

        [Header("World overlays (health bars, YOU, +points)")]
        public RectTransform overlayRoot;
        public WorldBarUi barTemplate;
        public Text labelTemplate;

        readonly List<Popup> _popups = new List<Popup>();
        readonly List<WorldBarUi> _bars = new List<WorldBarUi>();
        readonly List<Text> _labels = new List<Text>();
        int _barsUsed, _labelsUsed;
        MatchManager _subscribedTo;
        string _banner = "";
        string _bannerSub = "";
        Color _bannerColour = Color.white;
        float _bannerUntil;
        string _localAddresses;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        public void AddPopup(Vector2 planePosition, string text, Color colour)
        {
            _popups.Add(new Popup { plane = planePosition, text = text, colour = colour, born = Time.time });
        }

        // ------------------------------------------------------------------ buttons (On Click lists in the canvas)

        public void PauseGame() { if (!TestBlocksClicks) SetPaused(true); }

        public void ResumeGame() { if (!TestBlocksClicks) SetPaused(false); }

        /// <summary>SURRENDER (pause menu): the rival wins. Playing alone there is nobody to win, so it just goes back to the menu.</summary>
        public void Surrender()
        {
            if (TestBlocksClicks) return;
            MatchManager mm = MatchManager.Instance;
            if (mm == null || PlayerStats.All.Count < 2) { LeaveToMenu(); return; }
            mm.RequestSurrender();
        }

        public void StartSoloPractice()
        {
            if (TestBlocksClicks) return;
            if (MatchManager.Instance != null) MatchManager.Instance.ServerBeginCountdown();
        }

        /// <summary>BACK in the lobby: leave and return to the main menu.</summary>
        public void LobbyBack()
        {
            if (TestBlocksClicks) return;
            Debug.Log("[UI] BACK clicked in the lobby");
            LeaveToMenu();
        }

        /// <summary>REMATCH on the result screen (host only).</summary>
        public void Rematch()
        {
            // automated test runs ignore the button (a stray mouse click must not start a second match)
            bool blocked = GameConfig.TestMode && !GameConfig.HasFlag("-allowrematch");
            var mouse = UnityEngine.InputSystem.Mouse.current;
            Debug.Log($"[UI] REMATCH clicked at mouse {(mouse != null ? mouse.position.ReadValue().ToString() : "n/a")} focused={Application.isFocused}{(blocked ? " (ignored in test mode)" : "")}");
            if (!blocked && MatchManager.Instance != null) MatchManager.Instance.ServerRematch();
        }

        /// <summary>BACK on the result screen: the player does not want another game.</summary>
        public void ResultBack()
        {
            bool blocked = TestBlocksClicks;
            Debug.Log("[UI] BACK clicked" + (blocked ? " (ignored in test mode)" : ""));
            if (!blocked) LeaveToMenu();
        }

        /// <summary>Leaves the match (or lobby) and shows the main menu.</summary>
        public static void LeaveToMenu()
        {
            if (ConnectionMenu.Instance != null) ConnectionMenu.Instance.LeaveToMenu();
            else if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        }

        // ------------------------------------------------------------------ event hookup

        void Update()
        {
            MatchManager current = MatchManager.Instance;

            // Escape (or the Android back button) opens / closes the pause screen during a match; it closes by itself afterwards
            bool inMatch = current != null && (current.Phase.Value == MatchPhase.Countdown || current.Phase.Value == MatchPhase.Playing);
            if (inMatch)
            {
                // Escape (or the Android back button) pauses; it resumes only for the player who paused
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && (Application.isFocused || GameConfig.TestMode))
                {
                    if (!Paused) SetPaused(true);
                    else if (PausedByMe) SetPaused(false);
                }
            }

            if (current != _subscribedTo)
            {
                Unsubscribe();
                if (current != null)
                {
                    current.SeasonChanged += OnSeasonChanged;
                    _subscribedTo = current;
                }
            }

            UpdateCanvas(current, inMatch);
        }

        void Unsubscribe()
        {
            if (_subscribedTo != null) _subscribedTo.SeasonChanged -= OnSeasonChanged;
            _subscribedTo = null;
        }

        void OnSeasonChanged(int season)
        {
            SeasonTheme theme = MapManager.Instance.Theme(season);
            _banner = theme.displayName.ToUpper();
            _bannerSub = theme.mood;
            _bannerColour = Color.Lerp(Color.white, theme.skyColor, 0.15f);
            _bannerUntil = Time.time + 2.8f;
        }

        // ------------------------------------------------------------------ canvas

        static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        static void Show(Component c, bool on)
        {
            if (c != null) Show(c.gameObject, on);
        }

        void UpdateCanvas(MatchManager mm, bool inMatch)
        {
            if (hudRoot == null) return;
            NetworkManager nm = NetworkManager.Singleton;
            bool session = nm != null && nm.IsListening && (nm.IsServer || nm.IsConnectedClient);
            Show(hudRoot, session);
            if (!session) { ClearOverlays(); return; }

            if (safeAreas != null && safeAreas.Length > 0)
            {
                Vector4 ins = MobileSupport.SafeInsets();                 // left, top, right, bottom in canvas units
                foreach (RectTransform area in safeAreas)
                {
                    if (area == null) continue;
                    area.offsetMin = new Vector2(ins.x, ins.w);
                    area.offsetMax = new Vector2(-ins.z, -ins.y);
                }
            }

            Show(connectingText, mm == null);
            Show(matchUi, mm != null);
            if (mm == null) return;
            Show(controlsHint, !MobileSupport.IsMobile);

            UpdateWorldOverlays();
            UpdateTopBar(mm);
            for (int slot = 0; slot < playerPanels.Length; slot++) UpdatePlayerPanel(playerPanels[slot], slot);
            UpdateBanner();
            UpdatePhaseScreens(nm, mm);
            UpdateDeathScreen();

            bool paused = inMatch && mm.Paused.Value;
            bool byMe = paused && PausedByMe;
            Show(pauseButton, inMatch && !paused);
            Show(pausePanel, byMe);
            Show(pausedByRival, paused && !byMe);
            if (paused)
            {
                int left = Mathf.Max(0, Mathf.CeilToInt(MatchManager.MaxPauseSeconds - (float)(nm.ServerTime.Time - mm.PausedAt.Value)));
                if (byMe && pauseSubtitle != null)
                    pauseSubtitle.text = PlayerStats.All.Count >= 2 ? $"The game is frozen for both players.   Resumes by itself in {left}s." : "The game is frozen.";
                if (!byMe && pausedByRivalText != null)
                {
                    PlayerStats rival = FindBySlot(mm.PausedBySlot.Value);
                    pausedByRivalText.text = $"{(rival != null ? rival.DisplayName : "Your rival")} paused the game\nResumes by itself in {left}s";
                }
            }
        }

        void UpdateTopBar(MatchManager mm)
        {
            string title;
            string time;
            switch (mm.Phase.Value)
            {
                case MatchPhase.Lobby: title = "LOBBY"; time = "--:--"; break;
                case MatchPhase.Countdown: title = "GET READY"; time = "--:--"; break;
                case MatchPhase.Finished: title = "MATCH OVER"; time = "00:00"; break;
                default:
                    title = mm.SeasonName.ToUpper() + "   " + (mm.SeasonIndex.Value + 1) + "/" + GameConfig.SeasonCount;
                    int sec = Mathf.CeilToInt(mm.TimeRemaining);
                    time = (sec / 60) + ":" + (sec % 60).ToString("00");
                    break;
            }
            topTitle.text = title;
            topTime.text = time;
            topTime.color = mm.Phase.Value == MatchPhase.Playing && mm.TimeRemaining <= 10f ? Alert : Ink;
        }

        void UpdatePlayerPanel(PlayerPanelUi panel, int slot)
        {
            Color slotColour = GameConfig.SlotColor(slot);
            panel.strip.color = slotColour;

            PlayerStats p = FindBySlot(slot);
            bool isLocal = p != null && p.IsOwner;
            panel.who.text = (p != null ? p.DisplayName : GameConfig.SlotName(slot)) + (isLocal ? "  (YOU)" : "");
            panel.who.color = MenuStyle.Current.SkinActive ? Color.Lerp(slotColour, Color.black, 0.3f) : slotColour;   // darker on the light box

            Show(panel.waiting, p == null);
            Show(panel.details, p != null);
            if (p == null) return;

            panel.score.text = p.Score.Value.ToString();
            float healthFraction = Mathf.Clamp01(p.Health.Value / (float)GameConfig.PlayerMaxHealth);
            panel.healthFill.anchorMax = new Vector2(healthFraction, 1f);
            panel.healthFillImage.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.35f, 0.9f, 0.35f), healthFraction);
            panel.kills.text = "Kills " + p.Kills.Value + "    Deaths " + p.Deaths.Value;
        }

        void UpdateBanner()
        {
            if (banner == null) return;
            if (Time.time >= _bannerUntil) { Show(banner.gameObject, false); return; }
            Show(banner.gameObject, true);
            float left = _bannerUntil - Time.time;
            float alpha = Mathf.Clamp01(left / 0.7f);
            float slide = Mathf.Clamp01((2.8f - left) / 0.25f);
            banner.alpha = alpha * slide;
            bannerTitle.text = _banner;
            bannerTitle.color = _bannerColour;
            bannerSub.text = _bannerSub;
        }

        void UpdatePhaseScreens(NetworkManager nm, MatchManager mm)
        {
            MatchPhase phase = mm.Phase.Value;
            Show(lobbyPanel, phase == MatchPhase.Lobby);
            Show(countdownText, phase == MatchPhase.Countdown);
            Show(resultPanel, phase == MatchPhase.Finished);

            switch (phase)
            {
                case MatchPhase.Lobby: UpdateLobby(nm); break;
                case MatchPhase.Countdown:
                    countdownText.text = Mathf.Max(1, Mathf.CeilToInt(mm.TimeRemaining)).ToString();
                    break;
                case MatchPhase.Finished: UpdateResult(nm, mm); break;
            }
        }

        void UpdateLobby(NetworkManager nm)
        {
            bool online = OnlineMatchmaker.Instance != null && OnlineMatchmaker.Instance.InSession;
            bool host = nm.IsServer;
            lobbyTitle.text = online ? "SEARCHING FOR A RIVAL ONLINE..." : "WAITING FOR YOUR RIVAL...";

            Show(lobbyLine1, online || !host);
            Show(lobbyLine2, online);
            Show(lobbyHostLabel, !online && host);
            Show(lobbyAddress, !online && host);
            Show(soloButton, !online && host);

            if (online)
            {
                lobbyLine1.text = host ? "The match starts by itself as soon as a random player joins." : "Rival found! The match is about to start.";
                lobbyLine2.text = "Press BACK to cancel.";
            }
            else if (host)
            {
                lobbyAddress.text = LocalAddresses();
            }
            else
            {
                lobbyLine1.text = "Connected! Waiting for the host to start.";
            }
        }

        void UpdateResult(NetworkManager nm, MatchManager mm)
        {
            int winner = mm.WinnerSlot.Value;
            int mySlot = PlayerStats.Local != null ? PlayerStats.Local.Slot.Value : -1;
            bool solo = PlayerStats.All.Count < 2;
            string headline;
            string note = "";
            Color colour;
            int surrendered = mm.SurrenderSlot.Value;
            if (surrendered >= 0 && !solo)
            {
                bool me = surrendered == mySlot;
                headline = me ? "YOU SURRENDERED" : "YOU WIN!";
                note = me ? "You gave up - your rival wins." : "Your rival surrendered.";
                colour = me ? new Color(1f, 0.5f, 0.45f) : new Color(1f, 0.9f, 0.35f);
            }
            else if (solo) { headline = "TIME!"; colour = Color.white; }
            else if (winner < 0) { headline = "DRAW"; colour = Color.white; }
            else if (winner == mySlot) { headline = "YOU WIN!"; colour = new Color(1f, 0.9f, 0.35f); }
            else { headline = "YOU LOSE"; colour = new Color(1f, 0.5f, 0.45f); }
            resultHeadline.text = headline;
            resultHeadline.color = colour;
            if (resultNote != null)
            {
                Show(resultNote, note.Length > 0);
                resultNote.text = note;
            }

            float y = 380;
            for (int i = 0; i < resultLines.Length; i++)
            {
                PlayerStats p = FindBySlot(i);
                Show(resultLines[i], p != null);
                if (p == null) continue;
                resultLines[i].text = p.DisplayName + (p.IsOwner ? " (you)" : "") + "     " + p.Score.Value + " pts     " + p.Kills.Value + (p.Kills.Value == 1 ? " kill     " : " kills     ") + p.Deaths.Value + (p.Deaths.Value == 1 ? " death" : " deaths");
                resultLines[i].color = GameConfig.SlotColor(i);
                PlaceFromTop(resultLines[i].rectTransform, 0f, y);
                y += 62;
            }

            // lifetime record from the online database (updates a moment after the host reports the match)
            SpacetimeManager db = SpacetimeManager.Instance;
            bool record = db != null && db.State == SpacetimeManager.ConnState.Connected && db.LocalProfile != null;
            Show(resultRecord, record);
            if (record)
            {
                ProfileInfo me = db.LocalProfile;
                resultRecord.text = $"Your record: {me.Wins} W  -  {me.Losses} L  -  {me.Draws} D     {me.Score} points in {me.Matches} {(me.Matches == 1 ? "match" : "matches")}";
                PlaceFromTop(resultRecord.rectTransform, 0f, y);
                y += 46;
            }

            if (nm.IsServer)
            {
                // REMATCH on the left, BACK (to the main menu) on the right
                Show(rematchButton, true);
                Show(rematchWaiting, false);
                PlaceFromTop(rematchButton, -170f, y + 40);
                PlaceFromTop(resultBackButton, 170f, y + 40);
            }
            else
            {
                Show(rematchButton, false);
                Show(rematchWaiting, true);
                PlaceFromTop(rematchWaiting.rectTransform, 0f, y + 40);
                PlaceFromTop(resultBackButton, 0f, y + 100);
            }
        }

        /// <summary>Puts a top-centre anchored element x units right of the centre, y units below the top of the screen.</summary>
        static void PlaceFromTop(RectTransform rt, float x, float y) => rt.anchoredPosition = new Vector2(x, -y);

        void UpdateDeathScreen()
        {
            PlayerStats local = PlayerStats.Local;
            bool dead = local != null && local.IsDead.Value && MatchManager.Instance != null && MatchManager.Instance.Phase.Value == MatchPhase.Playing;
            Show(deathPanel, dead);
            if (!dead) return;
            double remaining = local.RespawnAt.Value - NetworkManager.Singleton.ServerTime.Time;
            deathSub.text = "Respawning in " + Mathf.Max(0f, (float)remaining).ToString("0.0") + "s  -  your rival can score freely!";
        }

        // ------------------------------------------------------------------ world-anchored overlays

        void ClearOverlays()
        {
            _popups.Clear();
            _barsUsed = _labelsUsed = 0;
        }

        void UpdateWorldOverlays()
        {
            _barsUsed = 0;
            _labelsUsed = 0;
            Camera cam = Camera.main;
            if (cam != null && overlayRoot != null)
            {
                float scale = overlayRoot.GetComponentInParent<Canvas>().scaleFactor;

                foreach (Enemy e in Enemy.All)
                {
                    if (e == null || e.Def == null || e.Health.Value >= e.MaxHealth) continue;
                    float barHeight = e.Def.headHeight > 0f ? e.Def.headHeight * e.Def.scale + 0.2f : 1.45f * e.Def.scale + 0.25f;     // just above the picture
                    Vector2 g = ToCanvas(cam, Iso.ToScreen3(e.PlanePosition) + new Vector3(0f, barHeight, 0f), scale);
                    PlaceBar(g, 64, 8, e.HealthFraction, new Color(0.95f, 0.3f, 0.3f));
                }

                foreach (PlayerStats p in PlayerStats.All)
                {
                    if (p.Controller == null || p.IsDead.Value) continue;
                    Vector2 g = ToCanvas(cam, Iso.ToScreen3(p.Controller.PlanePosition) + new Vector3(0f, 1.95f, 0f), scale);
                    Color slotColour = GameConfig.SlotColor(Mathf.Max(0, p.Slot.Value));
                    PlaceBar(g, 74, 9, p.Health.Value / (float)GameConfig.PlayerMaxHealth, slotColour);
                    if (p.IsOwner) PlaceLabel(g + new Vector2(0f, 22f), "YOU", 20, slotColour);
                }

                for (int i = _popups.Count - 1; i >= 0; i--)
                {
                    Popup pop = _popups[i];
                    float age = Time.time - pop.born;
                    if (age > 1.1f) { _popups.RemoveAt(i); continue; }
                    Vector2 g = ToCanvas(cam, Iso.ToScreen3(pop.plane) + new Vector3(0f, 1.3f + age * 1.3f, 0f), scale);
                    Color c = pop.colour; c.a = Mathf.Clamp01((1.1f - age) / 0.5f);
                    PlaceLabel(g, pop.text, 44, c);
                }
            }

            for (int i = _barsUsed; i < _bars.Count; i++) Show(_bars[i], false);
            for (int i = _labelsUsed; i < _labels.Count; i++) Show(_labels[i], false);
        }

        /// <summary>World position -> position inside the overlay root (top-left anchored, y down, canvas units).</summary>
        static Vector2 ToCanvas(Camera cam, Vector3 world, float canvasScale)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            return new Vector2(sp.x / canvasScale, -(Screen.height - sp.y) / canvasScale);
        }

        void PlaceBar(Vector2 centre, float width, float height, float fraction, Color colour)
        {
            if (_barsUsed == _bars.Count)
            {
                WorldBarUi created = Instantiate(barTemplate, overlayRoot);
                created.name = "Bar";
                _bars.Add(created);
            }
            WorldBarUi bar = _bars[_barsUsed++];
            Show(bar, true);
            var rt = (RectTransform)bar.transform;
            rt.anchoredPosition = centre;
            rt.sizeDelta = new Vector2(width + 4f, height + 4f);
            bar.fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            bar.fillImage.color = colour;
        }

        void PlaceLabel(Vector2 centre, string text, int size, Color colour)
        {
            if (_labelsUsed == _labels.Count)
            {
                Text created = Instantiate(labelTemplate, overlayRoot);
                created.name = "Label";
                _labels.Add(created);
            }
            Text label = _labels[_labelsUsed++];
            Show(label, true);
            label.rectTransform.anchoredPosition = centre;
            label.fontSize = size;
            label.text = text;
            label.color = colour;
        }

        // ------------------------------------------------------------------ helpers

        static PlayerStats FindBySlot(int slot)
        {
            foreach (PlayerStats p in PlayerStats.All) if (p.Slot.Value == slot) return p;
            return null;
        }

        string LocalAddresses()
        {
            if (_localAddresses != null) return _localAddresses;
            var found = new List<string>();
            try
            {
                foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork) found.Add(ip.ToString());
            }
            catch { /* offline: fall through */ }
            found.Add("127.0.0.1 (same PC)");
            _localAddresses = string.Join("   |   ", found) + "    port " + GameConfig.DefaultPort;
            return _localAddresses;
        }
    }
}

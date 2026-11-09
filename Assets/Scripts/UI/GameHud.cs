using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// On-screen information, drawn with Unity's immediate-mode GUI so it needs no scene setup:
    /// both scores and health bars, the season + timer, season banners, the respawn countdown,
    /// enemy health bars, floating "+points" popups and the final result.
    /// Everything shown here is read from synchronized NetworkVariables, so both screens agree.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public static GameHud Instance { get; private set; }

        /// <summary>
        /// True while the pause screen is open. The match itself keeps running (it is a 2 player online game, so nobody can
        /// stop the clock for the rival), but the local player's controls are switched off.
        /// </summary>
        public static bool Paused { get; private set; }

        public static void SetPaused(bool value)
        {
            MatchManager mm = MatchManager.Instance;
            bool inMatch = mm != null && (mm.Phase.Value == MatchPhase.Countdown || mm.Phase.Value == MatchPhase.Playing);
            Paused = value && inMatch;
        }

        /// <summary>Automated tests ignore the mouse so that a stray real click cannot press PAUSE / BACK / QUIT.</summary>
        static bool TestBlocksClicks => GameConfig.TestMode && !GameConfig.HasFlag("-allowclicks");

        struct Popup
        {
            public Vector2 plane;
            public string text;
            public Color colour;
            public float born;
        }

        readonly List<Popup> _popups = new List<Popup>();
        MatchManager _subscribedTo;
        string _banner = "";
        string _bannerSub = "";
        Color _bannerColour = Color.white;
        float _bannerUntil;
        string _localAddresses;

        Vector4 _ins;                      // safe-area insets in virtual units: left, top, right, bottom
        GUIStyle _text;
        GUIStyle _button;
        Texture2D _white;

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

        // ------------------------------------------------------------------ event hookup

        void Update()
        {
            MatchManager current = MatchManager.Instance;

            // Escape (or the Android back button) opens / closes the pause screen during a match; it closes by itself afterwards
            bool inMatch = current != null && (current.Phase.Value == MatchPhase.Countdown || current.Phase.Value == MatchPhase.Playing);
            if (!inMatch) Paused = false;
            else
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && (Application.isFocused || GameConfig.TestMode)) Paused = !Paused;
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

        // ------------------------------------------------------------------ drawing

        void OnGUI()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || (!nm.IsServer && !nm.IsConnectedClient)) return;

            if (_text == null)
            {
                _text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false };
                _text.normal.textColor = Color.white;
                _white = Texture2D.whiteTexture;
            }
            if (_button == null)
            {
                _button = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;

            MatchManager mm = MatchManager.Instance;
            if (mm == null)
            {
                Label(new Rect(0, 480, w, 80), "Connecting...", 48, Color.white);
                return;
            }

            _ins = MobileSupport.SafeInsets();                 // keep clear of notches and rounded corners on phones
            DrawWorldOverlays(s);
            DrawTopBar(mm, w);
            DrawPlayerPanel(0, 24 + _ins.x, 14 + _ins.y, true);
            DrawPlayerPanel(1, w - 24 - 380 - _ins.z, 14 + _ins.y, false);
            DrawBanner(w);
            DrawPhaseMessages(nm, mm, w);
            DrawDeathOverlay(w);
            DrawPauseUi(mm, w);
            if (!MobileSupport.IsMobile)
                Label(new Rect(24, 1044, 900, 28), "Move: joystick / WASD / Arrows     Attack: button / Space / J", 17, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft);
        }

        /// <summary>A button in the menu's look (falls back to the plain Unity button when the menu is missing).</summary>
        bool MenuButton(Rect rect, string text, bool primary, bool danger = false)
        {
            ConnectionMenu menu = ConnectionMenu.Instance;
            return menu != null ? menu.HudButton(rect, text, primary, danger) : GUI.Button(rect, text, _button);
        }

        /// <summary>The PAUSE button (top right, during a match) and the pause screen with RESUME and QUIT.</summary>
        void DrawPauseUi(MatchManager mm, float w)
        {
            bool inMatch = mm.Phase.Value == MatchPhase.Countdown || mm.Phase.Value == MatchPhase.Playing;
            if (!inMatch) return;

            if (!Paused)
            {
                var pauseRect = new Rect(w - 24 - 170 - _ins.z, 130 + _ins.y, 170, 52);
                if (MenuButton(pauseRect, "PAUSE", false) && !TestBlocksClicks) Paused = true;
                return;
            }

            Fill(new Rect(0, 0, w, 1080), new Color(0, 0, 0, 0.62f));
            Label(new Rect(0, 300, w, 110), "PAUSED", 96, Color.white);
            bool twoPlayers = PlayerStats.All.Count >= 2;
            Label(new Rect(0, 410, w, 40), twoPlayers ? "The match keeps running - your rival is still playing." : "The match keeps running.", 24, new Color(1, 1, 1, 0.8f));

            if (MenuButton(new Rect(w * 0.5f - 200, 490, 400, 76), "RESUME", true) && !TestBlocksClicks) Paused = false;
            if (MenuButton(new Rect(w * 0.5f - 200, 590, 400, 76), "QUIT TO MENU", false, true) && !TestBlocksClicks)
            {
                Paused = false;
                LeaveToMenu();
            }
            Label(new Rect(0, 690, w, 32), "Esc: resume", 20, new Color(1, 1, 1, 0.55f));
        }

        void DrawTopBar(MatchManager mm, float w)
        {
            Rect box = new Rect(w * 0.5f - 190, 10 + _ins.y, 380, 104);
            Fill(box, new Color(0, 0, 0, 0.55f));

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
            Label(new Rect(box.x, box.y + 6, box.width, 40), title, 30, Color.white);
            Color timeColour = mm.Phase.Value == MatchPhase.Playing && mm.TimeRemaining <= 10f ? new Color(1f, 0.45f, 0.4f) : Color.white;
            Label(new Rect(box.x, box.y + 40, box.width, 62), time, 58, timeColour);
        }

        void DrawPlayerPanel(int slot, float x, float y, bool leftSide)
        {
            Rect box = new Rect(x, y, 380, 104);
            Fill(box, new Color(0, 0, 0, 0.55f));
            Color slotColour = GameConfig.SlotColor(slot);
            Fill(new Rect(leftSide ? box.x : box.xMax - 8, box.y, 8, box.height), slotColour);

            PlayerStats p = FindBySlot(slot);
            bool isLocal = p != null && p.IsOwner;
            string who = (p != null ? p.DisplayName : GameConfig.SlotName(slot)) + (isLocal ? "  (YOU)" : "");
            Label(new Rect(box.x + 20, box.y + 6, 220, 32), who, 24, slotColour, TextAnchor.MiddleLeft);

            if (p == null)
            {
                Label(new Rect(box.x + 20, box.y + 44, 340, 40), "waiting for player...", 22, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
                return;
            }

            Label(new Rect(box.x + 200, box.y + 2, 160, 56), p.Score.Value.ToString(), 52, Color.white, TextAnchor.MiddleRight);

            float healthFraction = Mathf.Clamp01(p.Health.Value / (float)GameConfig.PlayerMaxHealth);
            Rect bar = new Rect(box.x + 20, box.y + 62, 340, 16);
            Fill(bar, new Color(0, 0, 0, 0.6f));
            Fill(new Rect(bar.x, bar.y, bar.width * healthFraction, bar.height), Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.35f, 0.9f, 0.35f), healthFraction));
            Label(new Rect(box.x + 20, box.y + 80, 340, 22), "Kills " + p.Kills.Value + "    Deaths " + p.Deaths.Value, 16, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleLeft);
        }

        void DrawBanner(float w)
        {
            if (Time.time >= _bannerUntil) return;
            float left = _bannerUntil - Time.time;
            float alpha = Mathf.Clamp01(left / 0.7f);
            float slide = Mathf.Clamp01((2.8f - left) / 0.25f);
            Fill(new Rect(0, 300, w, 170), new Color(0, 0, 0, 0.5f * alpha * slide));
            Color c = _bannerColour; c.a = alpha * slide;
            Label(new Rect(0, 300, w, 120), _banner, 110, c);
            Color sub = Color.white; sub.a = alpha * slide;
            Label(new Rect(0, 415, w, 44), _bannerSub, 26, sub);
        }

        void DrawPhaseMessages(NetworkManager nm, MatchManager mm, float w)
        {
            switch (mm.Phase.Value)
            {
                case MatchPhase.Lobby:
                {
                    Fill(new Rect(w * 0.5f - 380, 380, 760, 270), new Color(0, 0, 0, 0.6f));
                    bool online = OnlineMatchmaker.Instance != null && OnlineMatchmaker.Instance.InSession;
                    Label(new Rect(0, 395, w, 60), online ? "SEARCHING FOR A RIVAL ONLINE..." : "WAITING FOR YOUR RIVAL...", 44, Color.white);
                    if (online)
                    {
                        Label(new Rect(0, 470, w, 40), nm.IsServer ? "The match starts by itself as soon as a random player joins." : "Rival found! The match is about to start.", 26, Color.white);
                        Label(new Rect(0, 520, w, 36), "Press Leave (bottom right) to cancel.", 20, new Color(1, 1, 1, 0.75f));
                    }
                    else if (nm.IsServer)
                    {
                        Label(new Rect(0, 460, w, 34), "Tell your friend to join this address:", 22, new Color(1, 1, 1, 0.85f));
                        Label(new Rect(0, 496, w, 44), LocalAddresses(), 34, new Color(1f, 0.92f, 0.5f));
                        if (MenuButton(new Rect(w * 0.5f - 230, 570, 460, 60), "START SOLO PRACTICE", true) && !TestBlocksClicks)
                            mm.ServerBeginCountdown();
                    }
                    else
                    {
                        Label(new Rect(0, 470, w, 40), "Connected! Waiting for the host to start.", 26, Color.white);
                    }

                    // BACK: leave the lobby and return to the main menu
                    if (MenuButton(new Rect(w * 0.5f - 150, 672, 300, 60), MenuStyle.Current.backText, false) && !TestBlocksClicks)
                    {
                        Debug.Log("[UI] BACK clicked in the lobby");
                        LeaveToMenu();
                    }
                    break;
                }
                case MatchPhase.Countdown:
                {
                    int n = Mathf.Max(1, Mathf.CeilToInt(mm.TimeRemaining));
                    Label(new Rect(0, 330, w, 260), n.ToString(), 220, new Color(1f, 0.95f, 0.6f));
                    break;
                }
                case MatchPhase.Finished:
                    DrawResult(nm, mm, w);
                    break;
            }
        }

        void DrawResult(NetworkManager nm, MatchManager mm, float w)
        {
            Fill(new Rect(0, 0, w, 1080), new Color(0, 0, 0, 0.65f));

            int winner = mm.WinnerSlot.Value;
            int mySlot = PlayerStats.Local != null ? PlayerStats.Local.Slot.Value : -1;
            bool solo = PlayerStats.All.Count < 2;
            string headline;
            Color colour;
            if (solo) { headline = "TIME!"; colour = Color.white; }
            else if (winner < 0) { headline = "DRAW"; colour = Color.white; }
            else if (winner == mySlot) { headline = "YOU WIN!"; colour = new Color(1f, 0.9f, 0.35f); }
            else { headline = "YOU LOSE"; colour = new Color(1f, 0.5f, 0.45f); }

            Label(new Rect(0, 200, w, 150), headline, 130, colour);

            float y = 380;
            for (int slot = 0; slot < GameConfig.MaxPlayers; slot++)
            {
                PlayerStats p = FindBySlot(slot);
                if (p == null) continue;
                string line = p.DisplayName + (p.IsOwner ? " (you)" : "") + "     " + p.Score.Value + " pts     " + p.Kills.Value + (p.Kills.Value == 1 ? " kill     " : " kills     ") + p.Deaths.Value + (p.Deaths.Value == 1 ? " death" : " deaths");
                Label(new Rect(0, y, w, 52), line, 36, GameConfig.SlotColor(slot));
                y += 62;
            }

            // lifetime record from the online database (updates a moment after the host reports the match)
            SpacetimeManager db = SpacetimeManager.Instance;
            if (db != null && db.State == SpacetimeManager.ConnState.Connected && db.LocalProfile != null)
            {
                ProfileInfo me = db.LocalProfile;
                Label(new Rect(0, y, w, 40), $"Your record: {me.Wins} W  -  {me.Losses} L  -  {me.Draws} D     {me.Score} points in {me.Matches} {(me.Matches == 1 ? "match" : "matches")}", 24, new Color(1f, 1f, 1f, 0.85f));
                y += 46;
            }

            ConnectionMenu menu = ConnectionMenu.Instance;
            Rect backRect;
            if (nm.IsServer)
            {
                // REMATCH on the left, BACK (to the main menu) on the right
                var rematchRect = new Rect(w * 0.5f - 320, y + 40, 300, 64);
                backRect = new Rect(w * 0.5f + 20, y + 40, 300, 64);
                bool rematch = menu != null ? menu.HudButton(rematchRect, "REMATCH", true) : GUI.Button(rematchRect, "REMATCH", _button);
                if (rematch)
                {
                    // automated test runs ignore the button (a stray mouse click must not start a second match)
                    bool blocked = GameConfig.TestMode && !GameConfig.HasFlag("-allowrematch");
                    var mouse = UnityEngine.InputSystem.Mouse.current;
                    Debug.Log($"[UI] REMATCH clicked at mouse {(mouse != null ? mouse.position.ReadValue().ToString() : "n/a")} focused={Application.isFocused}{(blocked ? " (ignored in test mode)" : "")}");
                    if (!blocked) mm.ServerRematch();
                }
            }
            else
            {
                Label(new Rect(0, y + 40, w, 40), "Waiting for the host to start a rematch...", 24, new Color(1, 1, 1, 0.8f));
                backRect = new Rect(w * 0.5f - 150, y + 100, 300, 64);
            }

            // BACK: the player does not want another game, so leave the match and go to the main menu
            string backText = MenuStyle.Current.backText;
            bool back = menu != null ? menu.HudButton(backRect, backText, false) : GUI.Button(backRect, backText, _button);
            if (back)
            {
                bool blocked = GameConfig.TestMode && !GameConfig.HasFlag("-allowclicks");      // a stray mouse click must not end a test run
                Debug.Log("[UI] BACK clicked" + (blocked ? " (ignored in test mode)" : ""));
                if (!blocked) LeaveToMenu();
            }
        }

        /// <summary>Leaves the match (or lobby) and shows the main menu.</summary>
        public static void LeaveToMenu()
        {
            Paused = false;
            if (ConnectionMenu.Instance != null) ConnectionMenu.Instance.LeaveToMenu();
            else if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        }

        void DrawDeathOverlay(float w)
        {
            PlayerStats local = PlayerStats.Local;
            if (local == null || !local.IsDead.Value) return;
            if (MatchManager.Instance == null || MatchManager.Instance.Phase.Value != MatchPhase.Playing) return;

            double remaining = local.RespawnAt.Value - NetworkManager.Singleton.ServerTime.Time;
            Fill(new Rect(0, 0, w, 1080), new Color(0.6f, 0f, 0f, 0.22f));
            Label(new Rect(0, 400, w, 100), "YOU WERE DEFEATED", 78, new Color(1f, 0.85f, 0.85f));
            Label(new Rect(0, 500, w, 50), "Respawning in " + Mathf.Max(0f, (float)remaining).ToString("0.0") + "s  -  your rival can score freely!", 30, Color.white);
        }

        // ------------------------------------------------------------------ world-anchored overlays

        void DrawWorldOverlays(float guiScale)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            foreach (Enemy e in Enemy.All)
            {
                if (e == null || e.Def == null || e.Health.Value >= e.MaxHealth) continue;
                float barHeight = e.Def.headHeight > 0f ? e.Def.headHeight * e.Def.scale + 0.2f : 1.45f * e.Def.scale + 0.25f;     // just above the picture
                Vector2 g = ToGui(cam, Iso.ToScreen3(e.PlanePosition) + new Vector3(0f, barHeight, 0f), guiScale);
                DrawBar(g, 64, 8, e.HealthFraction, new Color(0.95f, 0.3f, 0.3f));
            }

            foreach (PlayerStats p in PlayerStats.All)
            {
                if (p.Controller == null || p.IsDead.Value) continue;
                Vector2 g = ToGui(cam, Iso.ToScreen3(p.Controller.PlanePosition) + new Vector3(0f, 1.95f, 0f), guiScale);
                DrawBar(g, 74, 9, p.Health.Value / (float)GameConfig.PlayerMaxHealth, GameConfig.SlotColor(Mathf.Max(0, p.Slot.Value)));
                if (p.IsOwner) Label(new Rect(g.x - 40, g.y - 34, 80, 24), "YOU", 20, GameConfig.SlotColor(Mathf.Max(0, p.Slot.Value)));
            }

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup pop = _popups[i];
                float age = Time.time - pop.born;
                if (age > 1.1f) { _popups.RemoveAt(i); continue; }
                Vector2 g = ToGui(cam, Iso.ToScreen3(pop.plane) + new Vector3(0f, 1.3f + age * 1.3f, 0f), guiScale);
                Color c = pop.colour; c.a = Mathf.Clamp01((1.1f - age) / 0.5f);
                Label(new Rect(g.x - 80, g.y - 24, 160, 48), pop.text, 44, c);
            }
        }

        static Vector2 ToGui(Camera cam, Vector3 world, float guiScale)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            return new Vector2(sp.x / guiScale, (Screen.height - sp.y) / guiScale);
        }

        void DrawBar(Vector2 centre, float width, float height, float fraction, Color colour)
        {
            Rect r = new Rect(centre.x - width * 0.5f, centre.y - height * 0.5f, width, height);
            Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), new Color(0, 0, 0, 0.75f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), colour);
        }

        // ------------------------------------------------------------------ helpers

        static PlayerStats FindBySlot(int slot)
        {
            foreach (PlayerStats p in PlayerStats.All) if (p.Slot.Value == slot) return p;
            return null;
        }

        void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        void Label(Rect r, string text, int size, Color colour, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            _text.fontSize = size;
            _text.alignment = anchor;
            // cheap outline so text stays readable on any background
            Color shadow = new Color(0, 0, 0, colour.a * 0.85f);
            _text.normal.textColor = shadow;
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, _text);
            _text.normal.textColor = colour;
            GUI.Label(r, text, _text);
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

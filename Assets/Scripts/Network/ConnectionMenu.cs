using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// Start menu and the server-side connection rules.
    ///
    ///  * The main menu has three buttons: PLAY (random online matchmaking, see <see cref="OnlineMatchmaker"/>),
    ///    PROFILE (your name and stats) and LEADERBOARD. Each opens its own screen with a BACK button.
    ///  * A small LOCAL NETWORK button opens HOST / JOIN: play on a local network (or the same PC) by typing the
    ///    host's address.
    ///  * Connection approval allows exactly two players and chooses each warrior's spawn point.
    ///  * When the server starts it spawns the <see cref="MatchManager"/> as a network object.
    ///
    /// Command line:  -host  |  -client -ip 127.0.0.1 -port 7777  |  -findmatch  |  -nomatchmaking
    ///                -menuScreen home|profile|leaderboard|local   (opens the menu on that screen, handy for tests)
    /// </summary>
    public class ConnectionMenu : MonoBehaviour
    {
        const string AutoFindKey = "sr_autofind";

        enum MenuScreen { Home, Profile, Leaderboard, Local }

        [Tooltip("Network prefab of the MatchManager (spawned by the server).")]
        public GameObject matchManagerPrefab;

        string _address = GameConfig.DefaultAddress;
        string _port = GameConfig.DefaultPort.ToString();
        string _nameField = "";
        string _message = "";
        bool _starting;
        bool _autoFind;
        MenuScreen _screen = MenuScreen.Home;
        NetworkManager _nm;
        OnlineMatchmaker _online;
        GUIStyle _title, _label, _button, _field, _small, _box, _toggle, _playButton, _smallButton;

        public static ConnectionMenu Instance { get; private set; }

        void Awake() => Instance = this;

        /// <summary>Leaves the running match (or online session) and shows the main menu. Used by the result screen's BACK button.</summary>
        public void LeaveToMenu()
        {
            _message = "";
            Disconnect();                    // also switches the menu to its home screen
        }

        /// <summary>
        /// A button in the menu's look for the in-game screens (lobby, pause, result). primary = the PLAY colour,
        /// danger = the CANCEL colour (for "quit"), otherwise the normal button colour.
        /// </summary>
        public bool HudButton(Rect rect, string text, bool primary, bool danger = false)
        {
            BuildStyles();
            Color colour = danger ? _ms.cancelButtonColor : primary ? _ms.playButtonColor : _ms.buttonColor;
            return TintedButton(rect, text, _button, colour);
        }

        void Start()
        {
            // Two copies of the game on one PC must both keep running while the other window has focus.
            Application.runInBackground = true;

            _nm = NetworkManager.Singleton;
            if (_nm == null) { Debug.LogError("[Net] No NetworkManager in the scene."); return; }
            _online = GetComponent<OnlineMatchmaker>();

            _nm.ConnectionApprovalCallback = ApproveConnection;
            _nm.OnServerStarted += OnServerStarted;
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;

            // searching as soon as the game opens is now an option (Profile screen) and is off by default: the menu has a PLAY button
            _autoFind = PlayerPrefs.GetInt(AutoFindKey, 0) == 1;
            if (GameConfig.TryGetArg("-menuScreen", out string screenArg))
            {
                switch (screenArg.ToLowerInvariant())
                {
                    case "profile": _screen = MenuScreen.Profile; break;
                    case "leaderboard": _screen = MenuScreen.Leaderboard; break;
                    case "local": _screen = MenuScreen.Local; break;
                }
            }
            if (GameConfig.TryGetArg("-name", out string nameArg)) LocalPlayer.Override = nameArg;   // for tests: -name Ana (never saved)
            _nameField = LocalPlayer.HasSavedName || !string.IsNullOrEmpty(LocalPlayer.Override) ? LocalPlayer.Name : "";

            if (GameConfig.TryGetArg("-ip", out string ip)) _address = ip;
            if (GameConfig.TryGetArg("-port", out string port)) _port = port;

            if (GameConfig.HasFlag("-host")) StartHost();
            else if (GameConfig.HasFlag("-client")) StartClient();
            else if (GameConfig.HasFlag("-findmatch")) FindMatch();
            else if (_autoFind && !GameConfig.TestMode && !GameConfig.HasFlag("-nomatchmaking")) FindMatch();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_nm == null) return;
            _nm.OnServerStarted -= OnServerStarted;
            _nm.OnClientConnectedCallback -= OnClientConnected;
            _nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // ------------------------------------------------------------------ starting / stopping

        ushort ParsePort() => ushort.TryParse(_port, out ushort p) ? p : GameConfig.DefaultPort;

        public void FindMatch()
        {
            if (_online == null) { _message = "Online matchmaking is not available in this build."; return; }
            ApplyName();
            _message = "";
            _online.FindMatch();
        }

        public void StartHost() => StartHostDirect(ParsePort());

        /// <summary>Starts a Netcode host that other players on the network can join (also used by matchmaking).</summary>
        public bool StartHostDirect(ushort port)
        {
            ApplyName();
            var transport = _nm.GetComponent<UnityTransport>();
            // 0.0.0.0 = accept connections from other PCs on the LAN as well as from this PC
            transport.SetConnectionData(GameConfig.DefaultAddress, port, "0.0.0.0");
            _message = "";
            _starting = true;
            if (!_nm.StartHost())
            {
                _message = "Could not start the host (is the port already in use?)";
                _starting = false;
                return false;
            }
            Debug.Log($"[Net] Host started on port {port}");
            return true;
        }

        public void StartClient() => StartClientDirect(_address.Trim(), ParsePort());

        /// <summary>Connects to a host by address (also used by matchmaking).</summary>
        public bool StartClientDirect(string address, ushort port)
        {
            ApplyName();
            var transport = _nm.GetComponent<UnityTransport>();
            transport.SetConnectionData(address, port);
            _message = "";
            _starting = true;
            if (!_nm.StartClient())
            {
                _message = "Could not start the client.";
                _starting = false;
                return false;
            }
            Debug.Log($"[Net] Connecting to {address}:{port}");
            return true;
        }

        /// <summary>Remembers the typed name and tells the database (if connected).</summary>
        void ApplyName()
        {
            string clean = LocalPlayer.Clean(_nameField);
            if (clean.Length == 0) return;
            LocalPlayer.Save(clean);
            if (SpacetimeManager.Instance != null) SpacetimeManager.Instance.SetName(clean);
        }

        void Disconnect()
        {
            if (_online != null && _online.IsBusy) _online.Leave();      // leaves the online session and shuts Netcode down
            else _nm.Shutdown();
            _starting = false;
            _screen = MenuScreen.Home;
        }

        // ------------------------------------------------------------------ server rules

        /// <summary>Runs on the server (and for the host itself) for every connection request.</summary>
        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool full = PlayerStats.All.Count >= GameConfig.MaxPlayers;
            response.Approved = !full;
            response.Reason = full ? "The match is full (2 players maximum)." : "";
            response.CreatePlayerObject = true;
            response.Pending = false;

            // the warrior appears on its own starting spot (slot 0 = blue / left, slot 1 = red / right)
            int slot = PlayerStats.LowestFreeSlot(null);
            int season = MatchManager.Instance != null ? MatchManager.Instance.SeasonIndex.Value : 0;
            Vector2 spawn = MapManager.Instance != null ? MapManager.Instance.GetPlayerSpawn(slot, season) : Vector2.one * 6f;
            response.Position = new Vector3(spawn.x, spawn.y, 0f);
            response.Rotation = Quaternion.identity;
        }

        void OnServerStarted()
        {
            // The referee is a normal network object: only the server creates it, clients receive it automatically.
            if (matchManagerPrefab != null && MatchManager.Instance == null)
            {
                GameObject go = Instantiate(matchManagerPrefab);
                go.GetComponent<NetworkObject>().Spawn();
                Debug.Log("[Net] MatchManager spawned");
            }
        }

        void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[Net] Client connected: id={clientId} (this peer: {(_nm.IsHost ? "Host" : _nm.IsServer ? "Server" : "Client")}, players={_nm.ConnectedClientsIds.Count})");
            if (clientId == _nm.LocalClientId) _starting = false;
        }

        void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[Net] Client disconnected: id={clientId}");
            if (!_nm.IsServer && clientId == _nm.LocalClientId)
            {
                string reason = _nm.DisconnectReason;
                _message = string.IsNullOrEmpty(reason) ? "Disconnected from the host." : reason;
                _starting = false;
                _screen = MenuScreen.Home;
                if (_online != null && _online.IsBusy) _online.Leave();
            }
        }

        // ------------------------------------------------------------------ IMGUI

        /// <summary>The editable look of the menu (Assets/Resources/MenuStyle.asset).</summary>
        MenuStyle _ms;
        int _styleVersion = -1;

        void BuildStyles()
        {
            MenuStyle m = MenuStyle.Current;
            if (_title != null && _ms == m && _styleVersion == MenuStyle.Version) return;     // rebuilt when the asset is edited
            _ms = m;
            _styleVersion = MenuStyle.Version;
            Font f = m.font;

            _title = new GUIStyle(GUI.skin.label) { fontSize = m.titleSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = f };
            _title.normal.textColor = m.titleColor;
            _label = new GUIStyle(GUI.skin.label) { fontSize = m.labelSize, alignment = TextAnchor.MiddleLeft, font = f };
            _label.normal.textColor = m.textColor;
            _button = new GUIStyle(GUI.skin.button) { fontSize = m.buttonSize, fontStyle = FontStyle.Bold, font = f };
            _playButton = new GUIStyle(GUI.skin.button) { fontSize = m.playButtonSize, fontStyle = FontStyle.Bold, font = f };
            _smallButton = new GUIStyle(GUI.skin.button) { fontSize = m.smallButtonSize, font = f };
            foreach (GUIStyle b in new[] { _button, _playButton, _smallButton })
            {
                b.normal.textColor = b.hover.textColor = b.active.textColor = b.focused.textColor = m.buttonTextColor;
                b.onNormal.textColor = b.onHover.textColor = b.onActive.textColor = b.onFocused.textColor = m.buttonTextColor;
                if (m.solidButtons) MakeFlat(b);
            }
            _field = new GUIStyle(GUI.skin.textField) { fontSize = 24, alignment = TextAnchor.MiddleLeft, font = f };
            _field.normal.textColor = _field.hover.textColor = _field.focused.textColor = _field.active.textColor = m.textColor;
            _field.onNormal.textColor = _field.onHover.textColor = _field.onFocused.textColor = _field.onActive.textColor = m.textColor;
            MakeFlat(_field);
            GUI.skin.settings.cursorColor = m.textColor;         // the text cursor must be visible on the light input boxes
            _small = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleRight, font = f };
            _small.normal.textColor = m.textColor;
            _box = new GUIStyle(GUI.skin.box) { font = f };
            MakeFlat(_box);
            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 20, font = f };
            _toggle.normal.textColor = m.textColor;
            _toggle.onNormal.textColor = m.textColor;
            _toggle.hover.textColor = m.textColor;
            _toggle.onHover.textColor = m.textColor;
            _txt = null;                                         // the generic text style is rebuilt with the new font
        }

        static Texture2D _flat;

        /// <summary>A plain white rounded square with a slightly darker edge: tinted, it gives a solid-colour button.</summary>
        static Texture2D FlatTexture()
        {
            if (_flat != null) return _flat;
            const int n = 8;
            _flat = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool edgeX = x == 0 || x == n - 1, edgeY = y == 0 || y == n - 1;
                    float v = edgeX || edgeY ? 0.72f : 1f;
                    _flat.SetPixel(x, y, edgeX && edgeY ? Color.clear : new Color(v, v, v, 1f));
                }
            _flat.Apply();
            return _flat;
        }

        static void MakeFlat(GUIStyle s)
        {
            Texture2D t = FlatTexture();
            s.normal.background = s.hover.background = s.active.background = s.focused.background = t;
            s.onNormal.background = s.onHover.background = s.onActive.background = s.onFocused.background = t;
            s.border = new RectOffset(3, 3, 3, 3);
        }

        /// <summary>Draws a button in the given colour (flat buttons: exactly that colour; otherwise a tint on the grey picture).</summary>
        bool TintedButton(Rect rect, string text, GUIStyle style, Color tint)
        {
            if (_ms.solidButtons && GUI.enabled && rect.Contains(Event.current.mousePosition))
                tint = Color.Lerp(tint, Color.white, _ms.hoverLighten);                // lighter while the mouse is over it
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool pressed = GUI.Button(rect, text, style);
            GUI.backgroundColor = previous;
            return pressed;
        }

        /// <summary>A box in the panel colour.</summary>
        void DrawPanel(Rect rect)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = _ms.panelColor;
            GUI.Box(rect, GUIContent.none, _box);
            GUI.backgroundColor = previous;
        }

        /// <summary>A one-line text input in the input-box colour.</summary>
        string Field(Rect rect, string value, int maxLength)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = _ms.fieldColor;
            string result = GUI.TextField(rect, value, maxLength, _field);
            GUI.backgroundColor = previous;
            return result;
        }

        bool InGame => _nm.IsListening && (_nm.IsServer || _nm.IsConnectedClient);

        void OnGUI()
        {
            if (_nm == null) return;
            BuildStyles();

            // Automated test windows ignore the mouse: a real mouse that happens to be over such a window must not press
            // PLAY / BACK / CANCEL in the middle of a test (use -allowclicks to click in a test run on purpose).
            if (GameConfig.TestMode && Event.current.isMouse && !GameConfig.HasFlag("-allowclicks")) Event.current.Use();

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;

            if (!InGame)
            {
                DrawMenu(w);
                return;
            }

            // small status line in the bottom right while playing
            Vector4 ins = MobileSupport.SafeInsets();
            string role = _online != null && _online.InSession ? "ONLINE" : _nm.IsHost ? "HOST" : "CLIENT";
            GUI.Label(new Rect(w - 560 - ins.z, 1038 - ins.w, 440, 30), $"{role}  -  players {PlayerStats.All.Count}/{GameConfig.MaxPlayers}", _small);
            if (GUI.Button(new Rect(w - 110 - ins.z, 1038 - ins.w, 96, 30), "Leave")) Disconnect();
        }

        void DrawMenu(float w)
        {
            GUI.color = _ms.backdropColor;
            GUI.DrawTexture(new Rect(0, 0, w, 1080), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(0, 100, w, 90), _ms.gameTitle, _title);
            GUI.Label(new Rect(0, 185, w, 40), _ms.tagline, _small.WithAlign(TextAnchor.MiddleCenter));

            float x = w * 0.5f - 280f;                                  // left edge of the 560 px wide centre column
            bool searching = _online != null && _online.IsBusy;
            bool connecting = _starting || (_nm.IsListening && !_nm.IsConnectedClient && !_nm.IsServer);

            if (searching || (connecting && _online != null && _online.InSession))
            {
                DrawPanel(new Rect(x - 30, 250, 620, 640));
                DrawSearching(x);
                return;
            }

            switch (_screen)
            {
                case MenuScreen.Profile: DrawProfileScreen(w); break;
                case MenuScreen.Leaderboard: DrawLeaderboardScreen(w); break;
                case MenuScreen.Local: DrawLocalScreen(w, x, connecting); break;
                default: DrawHome(w, x); break;
            }
        }

        // ------------------------------------------------------------------ the screens of the menu

        /// <summary>Main menu: PLAY, PROFILE, LEADERBOARD (and a small button for local network play).</summary>
        void DrawHome(float w, float x)
        {
            Text(new Rect(0, 222, w, 26), DatabaseStatusText(), 18, DatabaseStatusColour(), TextAnchor.MiddleCenter);
            DrawPanel(new Rect(x - 30, 270, 620, 610));

            if (TintedButton(new Rect(x, 300, 560, 120), _ms.playText, _playButton, _ms.playButtonColor)) FindMatch();      // the main button stands out
            Text(new Rect(x, 424, 560, 28), _ms.playHint, 18, _ms.hintTextColor, TextAnchor.MiddleCenter);

            if (TintedButton(new Rect(x, 474, 560, 90), _ms.profileText, _button, _ms.buttonColor)) _screen = MenuScreen.Profile;
            if (TintedButton(new Rect(x, 584, 560, 90), _ms.leaderboardText, _button, _ms.buttonColor)) _screen = MenuScreen.Leaderboard;
            if (TintedButton(new Rect(x + 60, 704, 440, 56), _ms.localNetworkText, _smallButton, _ms.buttonColor)) _screen = MenuScreen.Local;

            DrawMessage(x, 774f);
            GUI.Label(new Rect(0, 1000, w, 30), "Move: joystick / WASD / Arrows     Attack: button / Space / J", _small.WithAlign(TextAnchor.MiddleCenter));
        }

        void DrawProfileScreen(float w)
        {
            float px = w * 0.5f - 270f;
            DrawProfilePanel(px, 250f, 540f, 640f);

            bool auto = GUI.Toggle(new Rect(px + 24f, 250f + 640f - 56f, 540f - 48f, 34f), _autoFind, "  Search automatically when the game opens", _toggle);
            if (auto != _autoFind)
            {
                _autoFind = auto;
                PlayerPrefs.SetInt(AutoFindKey, auto ? 1 : 0);
                PlayerPrefs.Save();
            }
            DrawBackButton(w, 910f);
        }

        void DrawLeaderboardScreen(float w)
        {
            DrawLeaderboardPanel(w * 0.5f - 270f, 250f, 540f, 640f);
            DrawBackButton(w, 910f);
        }

        /// <summary>HOST A MATCH / JOIN MATCH on a local network (or the same PC).</summary>
        void DrawLocalScreen(float w, float x, bool connecting)
        {
            DrawPanel(new Rect(x - 30, 250, 620, 640));
            Text(new Rect(x, 268, 560, 44), "LOCAL NETWORK", 30, _ms.textColor, TextAnchor.MiddleCenter);
            Text(new Rect(x, 314, 560, 28), "Play with someone on the same Wi-Fi, or on this PC.", 18, _ms.hintTextColor, TextAnchor.MiddleCenter);

            GUI.enabled = !connecting;
            if (TintedButton(new Rect(x, 366, 560, 70), _ms.hostText, _button, _ms.buttonColor)) StartHost();

            GUI.Label(new Rect(x, 470, 150, 40), "Host IP", _label);
            _address = Field(new Rect(x + 150, 470, 410, 40), _address, 40);
            GUI.Label(new Rect(x, 520, 150, 40), "Port", _label);
            _port = Field(new Rect(x + 150, 520, 410, 40), _port, 5);

            if (TintedButton(new Rect(x, 580, 560, 70), connecting ? "CONNECTING..." : _ms.joinText, _button, _ms.buttonColor)) StartClient();
            GUI.enabled = true;

            if (connecting && TintedButton(new Rect(x + 180, 664, 200, 40), _ms.cancelText, _smallButton, _ms.cancelButtonColor)) Disconnect();

            DrawMessage(x, 716f);
            if (!connecting) DrawBackButton(w, 800f);
        }

        void DrawBackButton(float w, float y)
        {
            if (TintedButton(new Rect(w * 0.5f - 140f, y, 280f, 64f), _ms.backText, _button, _ms.buttonColor)) _screen = MenuScreen.Home;
        }

        /// <summary>The last error (for example "online play is not set up yet") in orange.</summary>
        void DrawMessage(float x, float y)
        {
            string message = !string.IsNullOrEmpty(_message) ? _message
                : (_online != null && _online.Current == OnlineMatchmaker.State.Failed ? _online.Error : "");
            if (string.IsNullOrEmpty(message)) return;
            var warn = new GUIStyle(_label) { alignment = TextAnchor.UpperCenter, wordWrap = true, fontSize = 19 };
            warn.normal.textColor = _ms.warnColor;
            GUI.Label(new Rect(x - 20, y, 600, 140), message, warn);
        }

        // ------------------------------------------------------------------ profile + leaderboard panels

        GUIStyle _txt;

        void Text(Rect r, string text, int size, Color colour, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (_txt == null) _txt = new GUIStyle(GUI.skin.label) { wordWrap = false, font = _ms != null ? _ms.font : null };
            _txt.fontSize = size;
            _txt.alignment = anchor;
            _txt.normal.textColor = colour;
            GUI.Label(r, text, _txt);
        }

        string DatabaseStatusText()
        {
            SpacetimeManager db = SpacetimeManager.Instance;
            if (!SpacetimeSettings.Enabled || db == null) return "Online database: not set up (optional)";
            switch (db.State)
            {
                case SpacetimeManager.ConnState.Connecting: return "Online database: connecting...";
                case SpacetimeManager.ConnState.Connected: return "Online database: connected";
                case SpacetimeManager.ConnState.Failed: return "Online database: unavailable";
                default: return "Online database: off";
            }
        }

        Color DatabaseStatusColour()
        {
            SpacetimeManager db = SpacetimeManager.Instance;
            if (!SpacetimeSettings.Enabled || db == null) return _ms.hintTextColor;
            switch (db.State)
            {
                case SpacetimeManager.ConnState.Connected: return _ms.goodColor;
                case SpacetimeManager.ConnState.Failed: return _ms.warnColor;
                default: return _ms.pendingColor;
            }
        }

        void DrawProfilePanel(float x, float y, float width, float height)
        {
            DrawPanel(new Rect(x, y, width, height));
            Text(new Rect(x + 24, y + 14, width - 48, 40), "YOUR PROFILE", 28, _ms.textColor);

            Text(new Rect(x + 24, y + 64, 100, 36), "Name", 22, _ms.textColor);
            _nameField = Field(new Rect(x + 130, y + 62, width - 154, 40), _nameField, LocalPlayer.MaxLength);
            Text(new Rect(x + 24, y + 106, width - 48, 26),
                LocalPlayer.HasSavedName ? "Your stats are saved under this name." : "Type a name to keep your stats between games.",
                16, _ms.hintTextColor);
            if (TintedButton(new Rect(x + width - 170, y + 134, 146, 34), "Save name", _smallButton, _ms.buttonColor)) ApplyName();

            Text(new Rect(x + 24, y + 176, width - 48, 28), DatabaseStatusText(), 20, DatabaseStatusColour());

            SpacetimeManager db = SpacetimeManager.Instance;
            if (db != null && db.State == SpacetimeManager.ConnState.Failed && !string.IsNullOrEmpty(db.Error))
            {
                var style = new GUIStyle(_label) { fontSize = 15, wordWrap = true, alignment = TextAnchor.UpperLeft };
                style.normal.textColor = _ms.warnColor;
                GUI.Label(new Rect(x + 24, y + 206, width - 48, 110), db.Error, style);
            }

            ProfileInfo me = db != null && db.State == SpacetimeManager.ConnState.Connected ? db.LocalProfile : null;
            float row = y + 250;
            if (me != null)
            {
                Text(new Rect(x + 24, row, width - 48, 30), me.Name, 26, _ms.highlightColor); row += 44;
                StatRow(x, ref row, width, "Matches played", me.Matches.ToString());
                StatRow(x, ref row, width, "Wins / Losses / Draws", $"{me.Wins} / {me.Losses} / {me.Draws}");
                StatRow(x, ref row, width, "Total points", me.Score.ToString());
                StatRow(x, ref row, width, "Kills / Deaths", $"{me.Kills} / {me.Deaths}");
            }
            else
            {
                Text(new Rect(x + 24, row, width - 48, 60),
                    SpacetimeSettings.Enabled ? "Play a match to start your record." : "Set up the database (docs/SpacetimeDB_Setup.md)\nto save names, stats and a leaderboard.",
                    18, _ms.hintTextColor);
            }
        }

        void StatRow(float x, ref float row, float width, string label, string value)
        {
            Text(new Rect(x + 24, row, width * 0.55f, 30), label, 20, _ms.hintTextColor);
            Text(new Rect(x + width * 0.55f, row, width * 0.45f - 24, 30), value, 22, _ms.textColor, TextAnchor.MiddleRight);
            row += 38;
        }

        void DrawLeaderboardPanel(float x, float y, float width, float height)
        {
            DrawPanel(new Rect(x, y, width, height));
            Text(new Rect(x + 24, y + 14, width - 48, 40), "LEADERBOARD", 28, _ms.textColor);

            SpacetimeManager db = SpacetimeManager.Instance;
            bool ready = db != null && db.State == SpacetimeManager.ConnState.Connected;
            if (!ready)
            {
                Text(new Rect(x + 24, y + 70, width - 48, 30), "Not connected to the online database.", 18, _ms.hintTextColor);
                return;
            }

            Text(new Rect(x + 24, y + 62, 40, 24), "#", 16, _ms.hintTextColor);
            Text(new Rect(x + 64, y + 62, 220, 24), "Player", 16, _ms.hintTextColor);
            Text(new Rect(x + 290, y + 62, 70, 24), "Wins", 16, _ms.hintTextColor, TextAnchor.MiddleRight);
            Text(new Rect(x + 370, y + 62, 146, 24), "Points", 16, _ms.hintTextColor, TextAnchor.MiddleRight);

            float row = y + 90;
            if (db.Leaderboard.Count == 0)
                Text(new Rect(x + 24, row, width - 48, 30), "No matches played yet - be the first!", 18, _ms.hintTextColor);
            for (int i = 0; i < db.Leaderboard.Count && i < 8; i++)
            {
                ProfileInfo p = db.Leaderboard[i];
                bool mine = db.LocalProfile != null && p.IdHex == db.LocalProfile.IdHex;
                Color c = mine ? _ms.highlightColor : _ms.textColor;
                Text(new Rect(x + 24, row, 40, 30), (i + 1).ToString(), 20, c);
                Text(new Rect(x + 64, row, 226, 30), p.Name + (p.Online ? "  *" : ""), 20, c);
                Text(new Rect(x + 290, row, 70, 30), p.Wins.ToString(), 20, c, TextAnchor.MiddleRight);
                Text(new Rect(x + 370, row, 146, 30), p.Score.ToString(), 20, c, TextAnchor.MiddleRight);
                row += 32;
            }
            Text(new Rect(x + 24, y + height - 232, width - 48, 22), "* = online now", 14, _ms.hintTextColor);

            float recent = y + height - 205;
            Text(new Rect(x + 24, recent, width - 48, 30), "RECENT MATCHES", 20, _ms.textColor);
            recent += 34;
            if (db.RecentMatches.Count == 0)
                Text(new Rect(x + 24, recent, width - 48, 26), "None yet.", 16, _ms.hintTextColor);
            for (int i = 0; i < db.RecentMatches.Count && i < 5; i++)
            {
                MatchInfo m = db.RecentMatches[i];
                string left = m.BlueName + (m.Winner == 0 ? " *" : "");
                string right = (m.Winner == 1 ? "* " : "") + m.RedName;
                Text(new Rect(x + 24, recent, width - 48, 26), $"{left}  {m.BlueScore} - {m.RedScore}  {right}", 17, _ms.textColor);
                recent += 28;
            }
        }

        void DrawSearching(float x)
        {
            var big = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 38, fontStyle = FontStyle.Bold };
            var mid = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, wordWrap = true };

            int dots = 1 + (int)(Time.unscaledTime * 2f) % 3;
            GUI.Label(new Rect(x, 330, 560, 60), "SEARCHING" + new string('.', dots), big);
            GUI.Label(new Rect(x, 420, 560, 70), _online.Stage, mid);

            int sec = Mathf.FloorToInt(_online.SearchSeconds);
            GUI.Label(new Rect(x, 500, 560, 40), $"{sec / 60}:{sec % 60:00}", mid);
            GUI.Label(new Rect(x, 560, 560, 110), "You will be paired with the next player who presses PLAY.", mid);

            if (TintedButton(new Rect(x + 140, 700, 280, 64), _ms.cancelText, _button, _ms.cancelButtonColor)) Disconnect();
        }
    }

    static class GuiStyleExtensions
    {
        public static GUIStyle WithAlign(this GUIStyle style, TextAnchor anchor)
        {
            var copy = new GUIStyle(style) { alignment = anchor };
            copy.normal.textColor = style.normal.textColor;
            return copy;
        }
    }
}

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SeasonRush
{
    /// <summary>
    /// The main menu and the network start-up (host / join / find a match).
    ///
    /// The menu is a real canvas in the scene (Hierarchy: UI > MainMenu > HomePanel / ProfilePanel / LeaderboardPanel / LocalPanel /
    /// SearchingPanel). The navigation buttons simply switch panels on and off (see their On Click lists in the Inspector); this script
    /// fills the texts, runs the host / join / matchmaking logic and shows the main menu only while no match is running.
    /// </summary>
    public class ConnectionMenu : MonoBehaviour
    {
        const string AutoFindKey = "sr_autofind";

        enum MenuScreen { Home, Profile, Leaderboard, Local, Searching }

        [Tooltip("Network prefab of the MatchManager (spawned by the server).")]
        public GameObject matchManagerPrefab;

        [Header("Canvas (made by Season Rush > Create UI Canvas In Scene)")]
        public Canvas canvas;
        [Tooltip("On: colours, font, text sizes and button words come from Assets/Resources/MenuStyle.asset. Off: the canvas keeps whatever you set on its objects.")]
        public bool useMenuStyle = true;

        [Header("Panels (switched with SetActive)")]
        public GameObject mainMenu;
        public GameObject homePanel;
        public GameObject profilePanel;
        public GameObject leaderboardPanel;
        public GameObject localPanel;
        public GameObject searchingPanel;

        [Header("Home")]
        public Text homeDatabaseStatus;
        public Text homeMessage;

        [Header("Profile")]
        public InputField nameField;
        public Text nameHint;
        public Text profileDatabaseStatus;
        public Text profileDatabaseError;
        public GameObject profileStats;           // name + the four stat rows (shown when there is a profile)
        public Text profileName;
        public Text[] profileStatValues;          // 4 values
        public Text profileEmpty;                 // shown instead of the stats when there is no profile
        public Toggle autoFindToggle;

        [Header("Leaderboard")]
        public Text leaderboardNotConnected;
        public GameObject leaderboardContent;
        public LeaderboardRow[] leaderboardRows;  // 8 rows
        public Text leaderboardEmpty;
        public Text[] recentMatchRows;            // 5 rows
        public Text recentNone;

        [Header("Local network")]
        public InputField addressField;
        public InputField portField;
        public Button hostButton;
        public Button joinButton;
        public Text joinLabel;
        public Button cancelButton;
        public Button localBackButton;
        public Text localMessage;

        [Header("Searching")]
        public Text searchTitle;
        public Text searchStage;
        public Text searchTimer;

        [Header("While a match runs (bottom right)")]
        public Text roleText;

        string _message = "";
        bool _starting;
        bool _autoFind;
        bool _wasInGame;
        bool _wasOnlineBusy;
        NetworkManager _nm;
        OnlineMatchmaker _online;
        MenuStyle _ms;
        int _styleVersion = -1;

        public static ConnectionMenu Instance { get; private set; }

        void Awake() => Instance = this;

        /// <summary>Leaves the running match (or online session) and shows the main menu. Used by the result screen's BACK button.</summary>
        public void LeaveToMenu()
        {
            _message = "";
            Disconnect();                    // also switches the menu to its home screen
        }

        void Start()
        {
            // Two copies of the game on one PC must both keep running while the other window has focus.
            Application.runInBackground = true;

            _nm = NetworkManager.Singleton;
            if (_nm == null) { Debug.LogError("[Net] No NetworkManager in the scene."); return; }
            _online = OnlineMatchmaker.Instance;

            if (canvas == null || mainMenu == null)
                Debug.LogError("[UI] The menu canvas is missing in this scene. Run Season Rush > Create UI Canvas In Scene (or Rebuild Project Setup).");
            else
                PrepareCanvas();

            _nm.ConnectionApprovalCallback = ApproveConnection;
            _nm.OnServerStarted += OnServerStarted;
            _nm.OnClientConnectedCallback += OnClientConnected;
            _nm.OnClientDisconnectCallback += OnClientDisconnected;

            // searching as soon as the game opens is now an option (Profile screen) and is off by default: the menu has a PLAY button
            _autoFind = PlayerPrefs.GetInt(AutoFindKey, 0) == 1;
            if (autoFindToggle != null)
            {
                autoFindToggle.SetIsOnWithoutNotify(_autoFind);
                autoFindToggle.onValueChanged.AddListener(OnAutoFindChanged);
            }
            if (GameConfig.TryGetArg("-menuScreen", out string screenArg))
            {
                switch (screenArg.ToLowerInvariant())
                {
                    case "profile": ShowScreen(MenuScreen.Profile); break;
                    case "leaderboard": ShowScreen(MenuScreen.Leaderboard); break;
                    case "local": ShowScreen(MenuScreen.Local); break;
                }
            }
            if (GameConfig.TryGetArg("-name", out string nameArg)) LocalPlayer.Override = nameArg;   // for tests: -name Ana (never saved)
            if (nameField != null)
                nameField.text = LocalPlayer.HasSavedName || !string.IsNullOrEmpty(LocalPlayer.Override) ? LocalPlayer.Name : "";

            if (addressField != null) addressField.text = GameConfig.DefaultAddress;
            if (portField != null) portField.text = GameConfig.DefaultPort.ToString();
            if (GameConfig.TryGetArg("-ip", out string ip) && addressField != null) addressField.text = ip;
            if (GameConfig.TryGetArg("-port", out string port) && portField != null) portField.text = port;

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

        /// <summary>Things the canvas needs to work: an input module with actions, no keyboard navigation, no clicks in automated tests.</summary>
        void PrepareCanvas()
        {
            EventSystem events = EventSystem.current != null ? EventSystem.current : FindFirstObjectByType<EventSystem>();
            if (events == null)
            {
                events = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                Debug.LogWarning("[UI] There was no EventSystem in the scene, one was created.");
            }
            events.sendNavigationEvents = false;                          // Space / Enter in the game must never "press" a selected button

            var module = events.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();

            // Automated tests ignore the mouse so that a stray real click cannot press PAUSE / BACK / QUIT.
            if (GameConfig.TestMode && !GameConfig.HasFlag("-allowclicks"))
            {
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
            }
        }

        // ------------------------------------------------------------------ buttons (also called from the On Click lists)

        ushort ParsePort() => portField != null && ushort.TryParse(portField.text, out ushort p) ? p : GameConfig.DefaultPort;
        string Address() => addressField != null ? addressField.text.Trim() : GameConfig.DefaultAddress;

        public void FindMatch()
        {
            if (_online == null) { _message = "Online matchmaking is not available in this build."; return; }
            ApplyName();
            _message = "";
            _online.FindMatch();
        }

        public void StartHost() => StartHostDirect(ParsePort());

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

        public void StartClient() => StartClientDirect(Address(), ParsePort());

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

        /// <summary>Saves the name typed in the Profile screen (the "Save name" button).</summary>
        public void ApplyName()
        {
            if (nameField == null) return;
            string clean = LocalPlayer.Clean(nameField.text);
            if (clean.Length == 0) return;
            LocalPlayer.Save(clean);
            if (SpacetimeManager.Instance != null) SpacetimeManager.Instance.SetName(clean);
        }

        /// <summary>The CANCEL buttons: stop searching / connecting and go back to the main menu.</summary>
        public void CancelConnection() => Disconnect();

        void OnAutoFindChanged(bool value)
        {
            _autoFind = value;
            PlayerPrefs.SetInt(AutoFindKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        void Disconnect()
        {
            if (_online != null && _online.IsBusy) _online.Leave();      // leaves the online session and shuts Netcode down
            else _nm.Shutdown();
            _starting = false;
            ShowScreen(MenuScreen.Home);
        }

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool full = PlayerStats.All.Count >= GameConfig.MaxPlayers;
            response.Approved = !full;
            response.Reason = full ? "The match is full (2 players maximum)." : "";
            response.CreatePlayerObject = true;
            response.Pending = false;

            int slot = PlayerStats.LowestFreeSlot(null);
            int season = MatchManager.Instance != null ? MatchManager.Instance.SeasonIndex.Value : 0;
            Vector2 spawn = MapManager.Instance != null ? MapManager.Instance.GetPlayerSpawn(slot, season) : Vector2.one * 6f;
            response.Position = new Vector3(spawn.x, spawn.y, 0f);
            response.Rotation = Quaternion.identity;
        }

        void OnServerStarted()
        {
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
                ShowScreen(MenuScreen.Home);
                if (_online != null && _online.IsBusy) _online.Leave();
            }
        }

        // ------------------------------------------------------------------ canvas

        bool InGame => _nm.IsListening && (_nm.IsServer || _nm.IsConnectedClient);

        static void Show(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        /// <summary>Shows exactly one screen of the main menu (everything else is switched off).</summary>
        void ShowScreen(MenuScreen screen)
        {
            Show(homePanel, screen == MenuScreen.Home);
            Show(profilePanel, screen == MenuScreen.Profile);
            Show(leaderboardPanel, screen == MenuScreen.Leaderboard);
            Show(localPanel, screen == MenuScreen.Local);
            Show(searchingPanel, screen == MenuScreen.Searching);
        }

        void ApplyStyle()
        {
            MenuStyle m = MenuStyle.Current;
            if (_ms == m && _styleVersion == MenuStyle.Version) return;     // applied again when the asset is edited
            _ms = m;
            _styleVersion = MenuStyle.Version;
            if (!useMenuStyle || canvas == null) return;
            foreach (StyledElement element in canvas.GetComponentsInChildren<StyledElement>(true)) element.Apply(m);
        }

        void Update()
        {
            if (_nm == null || canvas == null || mainMenu == null) return;
            ApplyStyle();

            bool inGame = InGame;
            if (inGame != _wasInGame)
            {
                _wasInGame = inGame;
                if (!inGame) ShowScreen(MenuScreen.Home);
            }
            Show(mainMenu, !inGame);

            if (inGame)
            {
                if (roleText != null)
                {
                    string role = _online != null && _online.InSession ? "ONLINE" : _nm.IsHost ? "HOST" : "CLIENT";
                    roleText.text = $"{role}  -  players {PlayerStats.All.Count}/{GameConfig.MaxPlayers}";
                }
                return;
            }

            bool searching = _online != null && _online.IsBusy;
            bool connecting = _starting || (_nm.IsListening && !_nm.IsConnectedClient && !_nm.IsServer);
            bool onlineBusy = searching || (connecting && _online != null && _online.InSession);
            if (onlineBusy != _wasOnlineBusy)
            {
                _wasOnlineBusy = onlineBusy;
                ShowScreen(onlineBusy ? MenuScreen.Searching : MenuScreen.Home);
            }

            if (Active(searchingPanel)) UpdateSearching();
            else if (Active(homePanel)) UpdateHome();
            else if (Active(profilePanel)) UpdateProfile();
            else if (Active(leaderboardPanel)) UpdateLeaderboard();
            else if (Active(localPanel)) UpdateLocal(connecting);
        }

        static bool Active(GameObject go) => go != null && go.activeSelf;

        void UpdateHome()
        {
            SetText(homeDatabaseStatus, DatabaseStatusText(), DatabaseStatusColour());
            SetText(homeMessage, CurrentMessage(), _ms.Warn);
        }

        void UpdateLocal(bool connecting)
        {
            if (hostButton != null) hostButton.interactable = !connecting;
            if (joinButton != null) joinButton.interactable = !connecting;
            if (addressField != null) addressField.interactable = !connecting;
            if (portField != null) portField.interactable = !connecting;
            if (joinLabel != null) joinLabel.text = connecting ? "CONNECTING..." : _ms.joinText;
            Show(cancelButton != null ? cancelButton.gameObject : null, connecting);
            Show(localBackButton != null ? localBackButton.gameObject : null, !connecting);
            SetText(localMessage, CurrentMessage(), _ms.Warn);
        }

        void UpdateSearching()
        {
            int dots = 1 + (int)(Time.unscaledTime * 2f) % 3;
            if (searchTitle != null) searchTitle.text = "SEARCHING" + new string('.', dots);
            if (_online == null) return;
            if (searchStage != null) searchStage.text = _online.Stage;
            int sec = Mathf.FloorToInt(_online.SearchSeconds);
            if (searchTimer != null) searchTimer.text = $"{sec / 60}:{sec % 60:00}";
        }

        void UpdateProfile()
        {
            SetText(profileDatabaseStatus, DatabaseStatusText(), DatabaseStatusColour());
            if (nameHint != null)
                nameHint.text = LocalPlayer.HasSavedName ? "Your stats are saved under this name." : "Type a name to keep your stats between games.";

            SpacetimeManager db = SpacetimeManager.Instance;
            bool failed = db != null && db.State == SpacetimeManager.ConnState.Failed && !string.IsNullOrEmpty(db.Error);
            SetText(profileDatabaseError, failed ? db.Error : "", _ms.Warn);

            ProfileInfo me = db != null && db.State == SpacetimeManager.ConnState.Connected ? db.LocalProfile : null;
            Show(profileStats, me != null);
            if (profileEmpty != null)
            {
                Show(profileEmpty.gameObject, me == null);
                profileEmpty.text = SpacetimeSettings.Enabled ? "Play a match to start your record."
                    : "Set up the database (docs/SpacetimeDB_Setup.md)\nto save names, stats and a leaderboard.";
            }
            if (me != null && profileStatValues != null && profileStatValues.Length >= 4)
            {
                if (profileName != null) { profileName.text = me.Name; profileName.color = _ms.Highlight; }
                profileStatValues[0].text = me.Matches.ToString();
                profileStatValues[1].text = $"{me.Wins} / {me.Losses} / {me.Draws}";
                profileStatValues[2].text = me.Score.ToString();
                profileStatValues[3].text = $"{me.Kills} / {me.Deaths}";
            }
        }

        void UpdateLeaderboard()
        {
            SpacetimeManager db = SpacetimeManager.Instance;
            bool ready = db != null && db.State == SpacetimeManager.ConnState.Connected;
            Show(leaderboardNotConnected != null ? leaderboardNotConnected.gameObject : null, !ready);
            Show(leaderboardContent, ready);
            if (!ready) return;

            Show(leaderboardEmpty != null ? leaderboardEmpty.gameObject : null, db.Leaderboard.Count == 0);
            for (int i = 0; i < leaderboardRows.Length; i++)
            {
                LeaderboardRow row = leaderboardRows[i];
                bool has = i < db.Leaderboard.Count;
                Show(row.gameObject, has);
                if (!has) continue;
                ProfileInfo p = db.Leaderboard[i];
                bool mine = db.LocalProfile != null && p.IdHex == db.LocalProfile.IdHex;
                Color c = mine ? _ms.Highlight : _ms.Text;
                SetText(row.rank, (i + 1).ToString(), c);
                SetText(row.playerName, p.Name + (p.Online ? "  *" : ""), c);
                SetText(row.wins, p.Wins.ToString(), c);
                SetText(row.points, p.Score.ToString(), c);
            }

            Show(recentNone != null ? recentNone.gameObject : null, db.RecentMatches.Count == 0);
            for (int i = 0; i < recentMatchRows.Length; i++)
            {
                bool has = i < db.RecentMatches.Count;
                Show(recentMatchRows[i].gameObject, has);
                if (!has) continue;
                MatchInfo m = db.RecentMatches[i];
                string left = m.BlueName + (m.Winner == 0 ? " *" : "");
                string right = (m.Winner == 1 ? "* " : "") + m.RedName;
                recentMatchRows[i].text = $"{left}  {m.BlueScore} - {m.RedScore}  {right}";
            }
        }

        static void SetText(Text label, string text, Color colour)
        {
            if (label == null) return;
            label.text = text;
            label.color = colour;
        }

        string CurrentMessage()
        {
            return !string.IsNullOrEmpty(_message) ? _message
                : (_online != null && _online.Current == OnlineMatchmaker.State.Failed ? _online.Error : "");
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
            if (!SpacetimeSettings.Enabled || db == null) return _ms.Hint;
            switch (db.State)
            {
                case SpacetimeManager.ConnState.Connected: return _ms.Good;
                case SpacetimeManager.ConnState.Failed: return _ms.Warn;
                default: return _ms.Pending;
            }
        }
    }
}

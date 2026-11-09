using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// Random online matchmaking for two players.
    ///
    /// WHO plays WHOM is decided by the SpacetimeDB database (see <see cref="SpacetimeManager"/>):
    ///   1. FIND MATCH puts the player in the database queue.
    ///   2. As soon as a second player joins, the database pairs the two. The one who waited longest is the Host.
    ///   3. Both players now connect their Netcode game:
    ///        * Unity Relay (when the project is linked to Unity Cloud): works across the internet, no ports to open.
    ///        * a direct connection to the host's address (otherwise, or with -direct): works on the same network.
    ///   4. When two players are connected the normal Season Rush match starts by itself.
    ///
    /// If the database is not configured or cannot be reached, the older Unity-only quick join is used instead,
    /// so FIND MATCH still works once Unity Cloud is set up.
    ///
    /// The Multiplayer Services package starts the Netcode NetworkManager (host or client) when a Relay session
    /// is created or joined. See docs/SpacetimeDB_Setup.md and docs/Online_Matchmaking_Setup.md.
    /// </summary>
    public class OnlineMatchmaker : MonoBehaviour
    {
        public enum State { Idle, Searching, WaitingForRival, Matched, Failed }

        public static OnlineMatchmaker Instance { get; private set; }

        public State Current { get; private set; } = State.Idle;
        /// <summary>Short status line for the UI ("Waiting for a rival...").</summary>
        public string Stage { get; private set; } = "";
        /// <summary>Why the last search failed, in plain language.</summary>
        public string Error { get; private set; } = "";
        /// <summary>Name of the rival once the database has paired the players.</summary>
        public string Opponent { get; private set; } = "";
        /// <summary>True while this player is part of an online match (waiting for, or playing against, a rival).</summary>
        public bool InSession => Current == State.WaitingForRival || Current == State.Matched;
        public bool IsBusy => Current == State.Searching || InSession;
        public float SearchSeconds => IsBusy ? Time.unscaledTime - _startedAt : 0f;

        ISession _session;
        CancellationTokenSource _cts;
        float _startedAt;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _cts?.Cancel();
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Start looking for a random rival. Safe to call from a button.</summary>
        public async void FindMatch()
        {
            if (IsBusy) return;

            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                Fail("You are already in a game. Leave it first.");
                return;
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            await RunAsync(_cts.Token);
        }

        /// <summary>Stop searching / leave the online match and return to the menu.</summary>
        public async void Leave()
        {
            _cts?.Cancel();
            SpacetimeManager db = SpacetimeManager.Instance;
            if (db != null) { db.LeaveQueue(); db.ClearPairing(); }
            await LeaveSessionAsync(shutdownNetwork: true);
            Current = State.Idle;
            Stage = "";
            Opponent = "";
        }

        // ------------------------------------------------------------------ the search

        async Task RunAsync(CancellationToken token)
        {
            try
            {
                Current = State.Searching;
                Error = "";
                Opponent = "";
                _startedAt = Time.unscaledTime;
                Debug.Log("[Online] find match started");

                SpacetimeManager db = SpacetimeManager.Instance;
                if (db != null && SpacetimeSettings.Enabled)
                {
                    Stage = "Connecting to the database...";
                    db.Connect();
                    await WaitUntilAsync(() => db.State != SpacetimeManager.ConnState.Connecting, 12f, token);

                    if (db.State == SpacetimeManager.ConnState.Connected)
                    {
                        await RunDatabaseMatchAsync(db, token);
                        return;
                    }
                    Debug.LogWarning("[Online] database unavailable (" + db.Error + "), falling back to Unity quick join");
                }
                else
                {
                    Debug.Log("[Online] no database configured, using Unity quick join");
                }

                await RunUnityQuickJoinAsync(token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[Online] search cancelled");
                await CleanUpAsync();
                Current = State.Idle;
                Stage = "";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Online] matchmaking failed: " + e);
                await CleanUpAsync();
                Fail(Describe(e));
            }
        }

        // ------------------------------------------------------------------ database queue

        async Task RunDatabaseMatchAsync(SpacetimeManager db, CancellationToken token)
        {
            db.ClearPairing();
            db.JoinQueue();
            Debug.Log("[Online] joined the database queue as " + LocalPlayer.Name);

            // wait here until the database pairs us with a rival (as long as it takes, the player can cancel)
            PairingInfo pairing;
            while ((pairing = db.CurrentPairing) == null)
            {
                token.ThrowIfCancellationRequested();
                if (db.State != SpacetimeManager.ConnState.Connected)
                    throw new Exception("Lost the connection to the database.");
                Stage = db.QueueCount > 1 ? $"Waiting for a rival...  ({db.QueueCount} players searching)" : "Waiting for a rival...";
                await Task.Delay(200, token);
            }

            Opponent = pairing.OpponentName;
            Debug.Log($"[Online] paired with {Opponent}. code={pairing.Code} host={pairing.IAmHost}");
            Stage = $"Rival found: {Opponent}!";

            bool relay = !GameConfig.HasFlag("-direct") && !string.IsNullOrEmpty(Application.cloudProjectId);
            if (relay) await ConnectViaRelayAsync(db, pairing, token);
            else await ConnectDirectAsync(db, pairing, token);
        }

        /// <summary>Unity Relay: both players open the session named by the database's match code.</summary>
        async Task ConnectViaRelayAsync(SpacetimeManager db, PairingInfo pairing, CancellationToken token)
        {
            Stage = $"Rival found: {Opponent}! Connecting through Unity Relay...";
            await EnsureSignedInAsync();
            var options = new SessionOptions { MaxPlayers = GameConfig.MaxPlayers }.WithRelayNetwork();

            if (pairing.IAmHost)
            {
                // the host creates the session; the guest joins it by the same id after the host says it is ready
                _session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(pairing.Code, options, token);
                HookSession(_session);
                db.PublishHostAddress("relay");
                Current = State.WaitingForRival;
                Stage = $"Waiting for {Opponent} to connect...";
                await WaitUntilAsync(() => PlayerStats.All.Count >= GameConfig.MaxPlayers, 45f, token, "Your rival did not connect in time.");
            }
            else
            {
                await WaitUntilAsync(() => db.CurrentPairing != null && db.CurrentPairing.HostAddress.Length > 0, 45f, token, "The host did not get ready in time.");
                _session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(pairing.Code, options, token);
                HookSession(_session);
                Current = State.Matched;
                Stage = "Connected! The match is about to start.";
            }
            Current = State.Matched;
        }

        /// <summary>Direct connection: the host listens, publishes its address in the database, the guest connects to it.</summary>
        async Task ConnectDirectAsync(SpacetimeManager db, PairingInfo pairing, CancellationToken token)
        {
            ConnectionMenu menu = GetComponent<ConnectionMenu>();
            if (menu == null) throw new Exception("No connection menu in the scene.");
            ushort port = GameConfig.DefaultPort;

            if (pairing.IAmHost)
            {
                Stage = $"Rival found: {Opponent}! Starting the game...";
                if (!menu.StartHostDirect(port)) throw new Exception("Could not start the host (is port " + port + " in use?).");
                await WaitUntilAsync(() => NetworkManager.Singleton.IsListening, 10f, token);

                string address = LocalAddress() + ":" + port;
                db.PublishHostAddress(address);
                Current = State.WaitingForRival;
                Stage = $"Waiting for {Opponent} to connect...";
                Debug.Log("[Online] hosting directly at " + address);
                await WaitUntilAsync(() => PlayerStats.All.Count >= GameConfig.MaxPlayers, 45f, token, "Your rival did not connect in time.");
            }
            else
            {
                Stage = $"Rival found: {Opponent}! Waiting for the host...";
                await WaitUntilAsync(() => db.CurrentPairing != null && db.CurrentPairing.HostAddress.Length > 0, 45f, token, "The host did not get ready in time.");

                string address = db.CurrentPairing.HostAddress;
                int colon = address.LastIndexOf(':');
                string ip = colon > 0 ? address.Substring(0, colon) : address;
                ushort hostPort = colon > 0 && ushort.TryParse(address.Substring(colon + 1), out ushort p) ? p : port;
                Debug.Log("[Online] connecting directly to " + address);

                Stage = "Connecting to " + Opponent + "...";
                if (!menu.StartClientDirect(ip, hostPort)) throw new Exception("Could not start the client.");
                await WaitUntilAsync(() => NetworkManager.Singleton.IsConnectedClient, 20f, token, "Could not reach the host at " + address + ". Direct connections only work on the same network; link the project to Unity Cloud to use Relay.");
                Current = State.Matched;
                Stage = "Connected! The match is about to start.";
            }
            Current = State.Matched;
        }

        /// <summary>The address other PCs on the network can use to reach this one.</summary>
        static string LocalAddress()
        {
            try
            {
                string fallback = null;
                foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] b = ip.GetAddressBytes();
                    bool isPrivate = b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
                    if (isPrivate) return ip.ToString();
                    if (b[0] != 127 && b[0] != 169) fallback ??= ip.ToString();
                }
                if (fallback != null) return fallback;
            }
            catch { /* offline: use loopback */ }
            return "127.0.0.1";
        }

        // ------------------------------------------------------------------ Unity-only quick join (fallback)

        async Task RunUnityQuickJoinAsync(CancellationToken token)
        {
            Stage = "Connecting to online services...";

            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                Fail(NotLinkedMessage);
                return;
            }

            await EnsureSignedInAsync();
            token.ThrowIfCancellationRequested();

            // Two players who press Play at the same moment can each create their own session, so a host that
            // waits too long with nobody joining leaves and searches again (after a random pause).
            for (int attempt = 1; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                Current = State.Searching;
                Stage = attempt == 1 ? "Looking for a rival..." : $"Looking for a rival... (attempt {attempt})";

                var quickJoin = new QuickJoinOptions { Timeout = TimeSpan.FromSeconds(5), CreateSession = true };
                var sessionOptions = new SessionOptions { MaxPlayers = GameConfig.MaxPlayers }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, sessionOptions, token);
                HookSession(_session);
                Debug.Log($"[Online] session ready: host={_session.IsHost} players={_session.PlayerCount}");

                if (!_session.IsHost)
                {
                    Current = State.Matched;
                    Stage = "Rival found! Connecting...";
                    return;
                }

                Current = State.WaitingForRival;
                Stage = "Waiting for a rival to join...";
                float giveUpAt = Time.unscaledTime + UnityEngine.Random.Range(10f, 18f);
                while (_session != null && _session.PlayerCount < GameConfig.MaxPlayers && Time.unscaledTime < giveUpAt)
                    await Task.Delay(400, token);

                if (_session != null && _session.PlayerCount >= GameConfig.MaxPlayers)
                {
                    Current = State.Matched;
                    Stage = "Rival found!";
                    return;
                }

                Debug.Log("[Online] nobody joined, searching again");
                await LeaveSessionAsync(shutdownNetwork: true);
            }
        }

        static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
                options.SetProfile(Profile());
                await UnityServices.InitializeAsync(options);
            }
            else
            {
                while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Delay(100);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("[Online] signed in anonymously");
            }
        }

        /// <summary>
        /// Each game window signs in as its own anonymous player. A random profile name lets two copies of the
        /// game on one PC (Editor + build) be two different players. Use -profile name to choose one.
        /// </summary>
        static string Profile()
        {
            if (GameConfig.TryGetArg("-profile", out string profile) && !string.IsNullOrWhiteSpace(profile)) return profile;
            return "sr" + UnityEngine.Random.Range(100000, 999999);
        }

        // ------------------------------------------------------------------ helpers

        static async Task WaitUntilAsync(Func<bool> condition, float timeoutSeconds, CancellationToken token, string timeoutMessage = "Timed out.")
        {
            float deadline = Time.unscaledTime + timeoutSeconds;
            while (!condition())
            {
                token.ThrowIfCancellationRequested();
                if (Time.unscaledTime > deadline) throw new TimeoutException(timeoutMessage);
                await Task.Delay(150, token);
            }
        }

        void HookSession(ISession session)
        {
            session.PlayerJoined += id => Debug.Log("[Online] player joined: " + id);
            session.PlayerHasLeft += id => Debug.Log("[Online] player left: " + id);
            session.RemovedFromSession += OnSessionEnded;
            session.Deleted += OnSessionEnded;
        }

        void OnSessionEnded()
        {
            Debug.Log("[Online] session ended");
            _session = null;
            if (Current != State.Failed) { Current = State.Idle; Stage = ""; }
        }

        async Task CleanUpAsync()
        {
            SpacetimeManager db = SpacetimeManager.Instance;
            if (db != null) { db.LeaveQueue(); db.ClearPairing(); }
            await LeaveSessionAsync(shutdownNetwork: true);
        }

        async Task LeaveSessionAsync(bool shutdownNetwork)
        {
            ISession session = _session;
            _session = null;
            if (session != null)
            {
                try { await session.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning("[Online] leaving the session: " + e.Message); }
            }

            NetworkManager nm = NetworkManager.Singleton;
            if (shutdownNetwork && nm != null && nm.IsListening) nm.Shutdown();
        }

        // ------------------------------------------------------------------ errors

        const string NotLinkedMessage =
            "Online play is not set up yet. In Unity: Edit > Project Settings > Services, link this project to a Unity " +
            "Cloud project and turn on Relay and Lobby (see docs/Online_Matchmaking_Setup.md). " +
            "You can still play on a local network with HOST / JOIN.";

        void Fail(string message)
        {
            Current = State.Failed;
            Stage = "";
            Error = message;
            Debug.LogWarning("[Online] " + message);
        }

        static string Describe(Exception e)
        {
            switch (e)
            {
                case TimeoutException t:
                    return t.Message;
                case ServicesInitializationException _:
                    return NotLinkedMessage;
                case AuthenticationException auth:
                    return "Could not sign in to Unity online services (" + auth.Message + "). Check your internet connection and that the project is linked.";
                case SessionException session:
                    return "Could not find or create an online match (" + session.Message + "). Check that Relay and Lobby are enabled for the project.";
                default:
                    return "Online matchmaking failed: " + e.Message;
            }
        }
    }
}

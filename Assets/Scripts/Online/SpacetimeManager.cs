using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// The game's connection to the SpacetimeDB database (see spacetimedb/Lib.cs for the tables and reducers).
    ///
    /// It keeps a local copy of the data the UI needs (this player's profile, the leaderboard, recent matches, the
    /// matchmaking queue, the pairing for this player) and offers a few simple calls:
    /// SetName, JoinQueue, LeaveQueue, PublishHostAddress, ReportMatch.
    ///
    /// Only this class touches the generated SpacetimeDB.Types; everything else uses the plain classes in
    /// SpacetimeData.cs. If no database is configured (SpacetimeSettings.Enabled is false) it does nothing and the
    /// game works exactly as before.
    /// </summary>
    public class SpacetimeManager : MonoBehaviour
    {
        public enum ConnState { Disabled, Connecting, Connected, Failed }

        public static SpacetimeManager Instance { get; private set; }

        public ConnState State { get; private set; } = ConnState.Disabled;
        /// <summary>Plain-language reason when State is Failed.</summary>
        public string Error { get; private set; } = "";
        /// <summary>This player's database identity as 64 lowercase hex characters ("" until connected).</summary>
        public string IdentityHex { get; private set; } = "";
        /// <summary>This player's profile row (null until it has arrived from the database).</summary>
        public ProfileInfo LocalProfile { get; private set; }
        public IReadOnlyList<ProfileInfo> Leaderboard => _leaderboard;
        public IReadOnlyList<MatchInfo> RecentMatches => _recent;
        public int QueueCount { get; private set; }
        public bool InQueue { get; private set; }
        /// <summary>The newest pairing this player is part of (set once the database has found a rival).</summary>
        public PairingInfo CurrentPairing { get; private set; }

        DbConnection _conn;
        string _tokenKey;
        bool _dirty;
        bool _subscribed;
        string _lastSetName = "";
        readonly List<ProfileInfo> _leaderboard = new List<ProfileInfo>();
        readonly List<MatchInfo> _recent = new List<MatchInfo>();
        readonly HashSet<ulong> _ignoredPairings = new HashSet<ulong>();

        void Awake()
        {
            Instance = this;
            State = ConnState.Disabled;      // becomes Connecting in Start() when a database is configured
        }

        void Start()
        {
            if (SpacetimeSettings.Enabled && !GameConfig.HasFlag("-nosdb")) Connect();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            try { _conn?.Disconnect(); } catch { /* shutting down */ }
        }

        void Update()
        {
            // the SDK delivers every callback from FrameTick, on the main thread
            _conn?.FrameTick();
            if (_dirty) Rebuild();
        }

        // ------------------------------------------------------------------ connecting

        /// <summary>Connects to the database. Safe to call repeatedly.</summary>
        public void Connect()
        {
            if (!SpacetimeSettings.Enabled) { State = ConnState.Disabled; return; }
            if (State == ConnState.Connecting || State == ConnState.Connected) return;

            State = ConnState.Connecting;
            Error = "";
            _subscribed = false;

            // A player with a saved name keeps the same database identity (and so the same stats) between launches.
            // Guests and test runs get a fresh identity each time.
            bool persist = LocalPlayer.HasSavedName && string.IsNullOrEmpty(LocalPlayer.Override);
            _tokenKey = "sdb_token_" + SpacetimeSettings.Database + "_" + LocalPlayer.Name.ToLowerInvariant();
            string token = persist ? PlayerPrefs.GetString(_tokenKey, "") : "";

            Debug.Log($"[DB] connecting to {SpacetimeSettings.Uri} database '{SpacetimeSettings.Database}' as '{LocalPlayer.Name}'");
            try
            {
                _conn = DbConnection.Builder()
                    .WithUri(SpacetimeSettings.Uri)
                    .WithDatabaseName(SpacetimeSettings.Database)
                    .WithToken(string.IsNullOrEmpty(token) ? null : token)
                    .OnConnect(HandleConnect)
                    .OnConnectError(HandleConnectError)
                    .OnDisconnect(HandleDisconnect)
                    .Build();
            }
            catch (Exception e)
            {
                Fail("Could not start the database connection: " + e.Message);
            }
        }

        public void Disconnect()
        {
            try { _conn?.Disconnect(); } catch { /* already closed */ }
            _conn = null;
            if (State != ConnState.Failed) State = ConnState.Disabled;
        }

        void HandleConnect(DbConnection conn, Identity identity, string token)
        {
            IdentityHex = identity.ToString().ToLowerInvariant();
            if (LocalPlayer.HasSavedName && string.IsNullOrEmpty(LocalPlayer.Override)) PlayerPrefs.SetString(_tokenKey, token);
            // State stays Connecting until the first data has arrived (see OnApplied below), so nothing is
            // sent to the database before it knows who we are.
            Debug.Log("[DB] connected, identity " + IdentityHex.Substring(0, 12) + "...");

            conn.Db.Player.OnInsert += (ctx, row) => _dirty = true;
            conn.Db.Player.OnUpdate += (ctx, oldRow, newRow) => _dirty = true;
            conn.Db.Player.OnDelete += (ctx, row) => _dirty = true;
            conn.Db.QueueEntry.OnInsert += (ctx, row) => _dirty = true;
            conn.Db.QueueEntry.OnDelete += (ctx, row) => _dirty = true;
            conn.Db.Pairing.OnInsert += (ctx, row) => _dirty = true;
            conn.Db.Pairing.OnUpdate += (ctx, oldRow, newRow) => _dirty = true;
            conn.Db.Pairing.OnDelete += (ctx, row) => _dirty = true;
            conn.Db.MatchResult.OnInsert += (ctx, row) => _dirty = true;

            conn.SubscriptionBuilder()
                .OnApplied(ctx =>
                {
                    _subscribed = true;
                    State = ConnState.Connected;          // ready: the local copy of the data is filled
                    _dirty = true;
                    Debug.Log("[DB] ready: " + conn.Db.Player.Count + " players, " + conn.Db.MatchResult.Count + " matches");
                    PushName();                           // the database now has our name before any queue call
                })
                .OnError((ctx, ex) => Fail("Database subscription failed: " + ex.Message))
                .SubscribeToAllTables();
        }

        void HandleConnectError(Exception error)
        {
            Fail($"Could not reach the database '{SpacetimeSettings.Database}' at {SpacetimeSettings.Uri}. {error.Message}");
        }

        void HandleDisconnect(DbConnection conn, Exception error)
        {
            if (State == ConnState.Connected || State == ConnState.Connecting)
            {
                Debug.LogWarning("[DB] disconnected" + (error != null ? ": " + error.Message : ""));
                if (error != null) Fail("Lost the connection to the database. " + error.Message);
                else State = ConnState.Disabled;
            }
            InQueue = false;
        }

        void Fail(string message)
        {
            State = ConnState.Failed;
            Error = message;
            Debug.LogWarning("[DB] " + message);
        }

        // ------------------------------------------------------------------ calls

        /// <summary>Tells the database this player's display name (also called automatically after connecting).</summary>
        public void SetName(string name)
        {
            string clean = LocalPlayer.Clean(name);
            if (clean.Length == 0 || State != ConnState.Connected || !_subscribed) return;
            if (clean == _lastSetName) return;
            _lastSetName = clean;
            _conn.Reducers.SetName(clean);
        }

        void PushName() => SetName(LocalPlayer.Name);

        /// <summary>Enter the matchmaking queue. Pairings from before this call are ignored.</summary>
        public void JoinQueue()
        {
            if (State != ConnState.Connected || _conn == null) return;
            PushName();
            IgnoreCurrentPairings();
            CurrentPairing = null;
            InQueue = true;
            _conn.Reducers.JoinQueue();
        }

        public void LeaveQueue()
        {
            if (State == ConnState.Connected && _conn != null && InQueue) _conn.Reducers.LeaveQueue();
            InQueue = false;
        }

        /// <summary>Forget the current pairing (used when a match search ends).</summary>
        public void ClearPairing()
        {
            if (CurrentPairing != null) _ignoredPairings.Add(CurrentPairing.Id);
            CurrentPairing = null;
        }

        /// <summary>The host announces how the guest can connect: "relay" or "ip:port".</summary>
        public void PublishHostAddress(string address)
        {
            if (State != ConnState.Connected || CurrentPairing == null) return;
            _conn.Reducers.PublishHostAddress(CurrentPairing.Id, address);
        }

        /// <summary>The Netcode host stores the finished match. Identities are 64-character hex strings.</summary>
        public void ReportMatch(string matchCode, string blueHex, int blueScore, int blueKills, int blueDeaths,
                                string redHex, int redScore, int redKills, int redDeaths, int winnerSlot)
        {
            if (State != ConnState.Connected || _conn == null) return;
            _conn.Reducers.ReportMatch(matchCode ?? "",
                Identity.FromHexString(blueHex), blueScore, blueKills, blueDeaths,
                Identity.FromHexString(redHex), redScore, redKills, redDeaths, winnerSlot);
            ClearPairing();                    // the code can only be used once; a rematch is stored without it
        }

        // ------------------------------------------------------------------ local copy for the UI

        void IgnoreCurrentPairings()
        {
            if (_conn == null) return;
            foreach (Pairing p in _conn.Db.Pairing.Iter())
                if (IsMine(p)) _ignoredPairings.Add(p.Id);
        }

        bool IsMine(Pairing p)
        {
            return _conn?.Identity is Identity me && (p.Host == me || p.Guest == me);
        }

        void Rebuild()
        {
            _dirty = false;
            if (_conn == null || State != ConnState.Connected) return;

            Identity? me = _conn.Identity;

            _leaderboard.Clear();
            LocalProfile = null;
            foreach (Player p in _conn.Db.Player.Iter())
            {
                ProfileInfo info = ToInfo(p);
                if (me.HasValue && p.Identity == me.Value) LocalProfile = info;
                if (p.MatchesPlayed > 0) _leaderboard.Add(info);
            }
            _leaderboard.Sort((a, b) =>
            {
                int c = b.Wins.CompareTo(a.Wins);
                if (c == 0) c = b.Score.CompareTo(a.Score);
                if (c == 0) c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return c;
            });
            if (_leaderboard.Count > 10) _leaderboard.RemoveRange(10, _leaderboard.Count - 10);

            _recent.Clear();
            foreach (MatchResult m in _conn.Db.MatchResult.Iter().OrderByDescending(x => x.Id).Take(6))
            {
                _recent.Add(new MatchInfo
                {
                    Id = m.Id,
                    Code = m.MatchCode,
                    BlueName = m.BlueName,
                    RedName = m.RedName,
                    BlueScore = m.BlueScore,
                    RedScore = m.RedScore,
                    Winner = m.WinnerSlot,
                    PlayedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(m.PlayedAt.MicrosecondsSinceUnixEpoch / 1000).UtcDateTime
                });
            }

            QueueCount = (int)_conn.Db.QueueEntry.Count;

            // the newest pairing for this player that we have not ignored
            Pairing newest = null;
            foreach (Pairing p in _conn.Db.Pairing.Iter())
            {
                if (!IsMine(p) || _ignoredPairings.Contains(p.Id) || p.Reported) continue;
                if (newest == null || p.Id > newest.Id) newest = p;
            }
            if (newest != null)
            {
                Pairing found = newest;
                bool host = me.HasValue && found.Host == me.Value;
                CurrentPairing = new PairingInfo
                {
                    Id = found.Id,
                    Code = found.MatchCode,
                    IAmHost = host,
                    HostName = found.HostName,
                    GuestName = found.GuestName,
                    HostIdHex = found.Host.ToString().ToLowerInvariant(),
                    GuestIdHex = found.Guest.ToString().ToLowerInvariant(),
                    HostAddress = found.HostAddress ?? ""
                };
                InQueue = false;
            }
        }

        static ProfileInfo ToInfo(Player p)
        {
            return new ProfileInfo
            {
                IdHex = p.Identity.ToString().ToLowerInvariant(),
                Name = p.Name,
                Online = p.Online,
                Matches = p.MatchesPlayed,
                Wins = p.Wins,
                Losses = p.Losses,
                Draws = p.Draws,
                Score = p.TotalScore,
                Kills = p.TotalKills,
                Deaths = p.TotalDeaths
            };
        }
    }
}

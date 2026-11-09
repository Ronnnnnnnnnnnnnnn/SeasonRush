using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// The referee. Spawned by the server when the host starts, it runs the 6 minute match:
    /// Lobby -> Countdown -> Playing (Spring, Fall, Winter, 2 minutes each) -> Finished.
    ///
    /// What is synchronized (NetworkVariables, server-writable, readable by everyone):
    ///   Phase, SeasonIndex, PhaseEndTime, WinnerSlot
    /// What the server tells clients to react to (RPCs):
    ///   SeasonChangedClientRpc, ScorePopupClientRpc, MatchFinishedClientRpc
    /// What the server spawns / despawns over the network:
    ///   enemies (see <see cref="Enemy"/>)
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Tooltip("Network prefab for enemies (registered in the NetworkManager prefab list).")]
        public GameObject enemyPrefab;

        public readonly NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(MatchPhase.Lobby);
        public readonly NetworkVariable<int> SeasonIndex = new NetworkVariable<int>(0);
        /// <summary>Server time at which the current phase ends. Clients derive the countdown from it.</summary>
        public readonly NetworkVariable<double> PhaseEndTime = new NetworkVariable<double>(0d);
        public readonly NetworkVariable<int> WinnerSlot = new NetworkVariable<int>(-1);

        /// <summary>Raised on every peer when a new season begins (used for the big banner).</summary>
        public event Action<int> SeasonChanged;
        /// <summary>Raised on every peer when the match is over (argument = winning slot, -1 = draw).</summary>
        public event Action<int> MatchFinished;

        float _nextEnemySpawn;

        public float TimeRemaining
        {
            get
            {
                if (NetworkManager == null || !NetworkManager.IsListening) return 0f;
                return Mathf.Max(0f, (float)(PhaseEndTime.Value - NetworkManager.ServerTime.Time));
            }
        }

        public string SeasonName => MapManager.Instance != null ? MapManager.Instance.Theme(SeasonIndex.Value).displayName : "";

        // ------------------------------------------------------------------ lifecycle

        public override void OnNetworkSpawn()
        {
            Instance = this;
            SeasonIndex.OnValueChanged += OnSeasonIndexChanged;
            if (MapManager.Instance != null) MapManager.Instance.ShowSeason(SeasonIndex.Value);
        }

        public override void OnNetworkDespawn()
        {
            SeasonIndex.OnValueChanged -= OnSeasonIndexChanged;
            if (Instance == this) Instance = null;
        }

        void OnSeasonIndexChanged(int previous, int current)
        {
            // every peer swaps the map and the sky when the networked season changes
            if (MapManager.Instance != null) MapManager.Instance.ShowSeason(current);
        }

        // ------------------------------------------------------------------ server state machine

        void Update()
        {
            if (!IsServer) return;

            double now = NetworkManager.ServerTime.Time;
            switch (Phase.Value)
            {
                case MatchPhase.Lobby:
                    // the match starts by itself as soon as both players are connected
                    if (PlayerStats.All.Count >= GameConfig.MaxPlayers) ServerBeginCountdown();
                    break;

                case MatchPhase.Countdown:
                    if (now >= PhaseEndTime.Value) ServerStartSeason(0);
                    break;

                case MatchPhase.Playing:
                    if (now >= PhaseEndTime.Value)
                    {
                        if (SeasonIndex.Value + 1 < GameConfig.SeasonCount) ServerStartSeason(SeasonIndex.Value + 1);
                        else ServerFinishMatch();
                    }
                    else
                    {
                        TickEnemySpawner();
                    }
                    break;
            }
        }

        /// <summary>Starts the 3-2-1 countdown. Called automatically with 2 players, or by the host for solo practice.</summary>
        public void ServerBeginCountdown()
        {
            if (!IsServer || Phase.Value != MatchPhase.Lobby) return;

            ClearEnemies();
            SeasonIndex.Value = 0;
            WinnerSlot.Value = -1;
            foreach (PlayerStats p in PlayerStats.All) p.ServerResetForMatch();

            PhaseEndTime.Value = NetworkManager.ServerTime.Time + GameConfig.CountdownSeconds;
            Phase.Value = MatchPhase.Countdown;
        }

        void ServerStartSeason(int season)
        {
            ClearEnemies();                                   // every season is a fresh start
            SeasonIndex.Value = season;
            PhaseEndTime.Value = NetworkManager.ServerTime.Time + GameConfig.SeasonSeconds;
            Phase.Value = MatchPhase.Playing;
            _nextEnemySpawn = Time.time + 1.0f;

            // new map, new obstacles: put both warriors back on their starting spots
            foreach (PlayerStats p in PlayerStats.All) p.MoveToSpawn();

            SeasonChangedClientRpc(season);
        }

        void ServerFinishMatch()
        {
            ClearEnemies();

            // higher score wins, tie-break: fewer deaths, otherwise it is a draw
            int winner = -1;
            PlayerStats best = null;
            bool tie = false;
            foreach (PlayerStats p in PlayerStats.All)
            {
                if (best == null) { best = p; continue; }
                int cmp = p.Score.Value.CompareTo(best.Score.Value);
                if (cmp == 0) cmp = best.Deaths.Value.CompareTo(p.Deaths.Value);   // fewer deaths is better
                if (cmp > 0) { best = p; tie = false; }
                else if (cmp == 0) tie = true;
            }
            if (best != null && !tie) winner = best.Slot.Value;

            WinnerSlot.Value = winner;
            Phase.Value = MatchPhase.Finished;
            MatchFinishedClientRpc(winner);
            ReportToDatabase(winner);
        }

        /// <summary>
        /// The host stores the finished match in SpacetimeDB (result history + both players' lifetime stats).
        /// Only done for a real two-player match where both players have a database identity.
        /// </summary>
        void ReportToDatabase(int winner)
        {
            SpacetimeManager db = SpacetimeManager.Instance;
            if (db == null || db.State != SpacetimeManager.ConnState.Connected) return;

            PlayerStats blue = null, red = null;
            foreach (PlayerStats p in PlayerStats.All)
            {
                if (p.Slot.Value == 0) blue = p;
                else if (p.Slot.Value == 1) red = p;
            }
            if (blue == null || red == null) return;                       // solo practice: nothing to store

            string blueId = blue.SdbIdentity.Value.ToString();
            string redId = red.SdbIdentity.Value.ToString();
            if (blueId.Length == 0 || redId.Length == 0 || blueId == redId) return;

            string code = db.CurrentPairing != null ? db.CurrentPairing.Code : "";
            db.ReportMatch(code, blueId, blue.Score.Value, blue.Kills.Value, blue.Deaths.Value,
                                 redId, red.Score.Value, red.Kills.Value, red.Deaths.Value, winner);
            Debug.Log($"[DB] reported match {(code.Length > 0 ? code : "(no code)")}: blue {blue.Score.Value} vs red {red.Score.Value}, winner slot {winner}");
        }

        /// <summary>Host presses "Rematch": back to the lobby (which immediately restarts with two players).</summary>
        public void ServerRematch()
        {
            if (!IsServer || Phase.Value != MatchPhase.Finished) return;
            ClearEnemies();
            Phase.Value = MatchPhase.Lobby;
        }

        // ------------------------------------------------------------------ enemies (server only)

        void TickEnemySpawner()
        {
            if (enemyPrefab == null || Time.time < _nextEnemySpawn) return;
            _nextEnemySpawn = Time.time + GameConfig.EnemySpawnInterval;

            int cap = PlayerStats.All.Count >= 2 ? GameConfig.MaxEnemiesAlive : Mathf.Max(3, GameConfig.MaxEnemiesAlive * 5 / 8);
            if (Enemy.All.Count >= cap) return;

            int season = SeasonIndex.Value;
            SeasonTheme theme = MapManager.Instance.Theme(season);
            int tier = PickTier(theme);

            var playerPositions = new List<Vector2>();
            foreach (PlayerStats p in PlayerStats.All)
                if (!p.IsDead.Value && p.Controller != null) playerPositions.Add(p.Controller.PlanePosition);

            Vector2 spot = MapManager.Instance.PickEnemySpawn(season, playerPositions) + UnityEngine.Random.insideUnitCircle * 0.5f;
            GameObject go = Instantiate(enemyPrefab, new Vector3(spot.x, spot.y, 0f), Quaternion.identity);
            go.GetComponent<Enemy>().PendingTypeId = season * 3 + tier;
            go.GetComponent<NetworkObject>().Spawn(true);     // creates it on every client
        }

        static int PickTier(SeasonTheme theme)
        {
            int total = 0;
            foreach (EnemyDef d in theme.enemies) total += Mathf.Max(0, d.spawnWeight);
            int roll = UnityEngine.Random.Range(0, Mathf.Max(1, total));
            for (int i = 0; i < theme.enemies.Length; i++)
            {
                roll -= Mathf.Max(0, theme.enemies[i].spawnWeight);
                if (roll < 0) return i;
            }
            return 0;
        }

        void ClearEnemies()
        {
            foreach (Enemy e in Enemy.All.ToArray())
                if (e != null && e.NetworkObject != null && e.NetworkObject.IsSpawned) e.NetworkObject.Despawn(true);
        }

        /// <summary>Called by an enemy when it dies: the killer gets the points.</summary>
        public void ServerAwardKill(ulong killerClientId, int points, Vector2 planePosition, string enemyName)
        {
            if (!IsServer) return;
            foreach (PlayerStats p in PlayerStats.All)
            {
                if (p.OwnerClientId != killerClientId) continue;
                p.Score.Value += points;
                p.Kills.Value += 1;
                ScorePopupClientRpc(planePosition, points, p.Slot.Value);
                return;
            }
        }

        // ------------------------------------------------------------------ server -> client events

        [ClientRpc]
        void SeasonChangedClientRpc(int season)
        {
            SeasonChanged?.Invoke(season);
        }

        [ClientRpc]
        void ScorePopupClientRpc(Vector2 planePosition, int points, int slot)
        {
            if (GameHud.Instance != null)
                GameHud.Instance.AddPopup(planePosition, "+" + points, GameConfig.SlotColor(Mathf.Max(0, slot)));
        }

        [ClientRpc]
        void MatchFinishedClientRpc(int winnerSlot)
        {
            MatchFinished?.Invoke(winnerSlot);
        }
    }
}

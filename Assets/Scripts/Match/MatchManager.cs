using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SeasonRush
{

    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Tooltip("Network prefab for enemies (registered in the NetworkManager prefab list).")]
        public GameObject enemyPrefab;

        public readonly NetworkVariable<MatchPhase> Phase = new NetworkVariable<MatchPhase>(MatchPhase.Lobby);
        public readonly NetworkVariable<int> SeasonIndex = new NetworkVariable<int>(0);
        public readonly NetworkVariable<double> PhaseEndTime = new NetworkVariable<double>(0d);
        public readonly NetworkVariable<int> WinnerSlot = new NetworkVariable<int>(-1);

        /// <summary>True while the game is frozen by a player's PAUSE (the match clock, the enemies, both warriors).</summary>
        public readonly NetworkVariable<bool> Paused = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<int> PausedBySlot = new NetworkVariable<int>(-1);
        /// <summary>Server time at which the pause started (so everybody can show how long it will last).</summary>
        public readonly NetworkVariable<double> PausedAt = new NetworkVariable<double>(0d);
        /// <summary>The match time that was left when the pause started (shown while frozen).</summary>
        public readonly NetworkVariable<float> PausedRemaining = new NetworkVariable<float>(0f);
        /// <summary>Slot of the player who gave up (-1 = nobody). The other player wins.</summary>
        public readonly NetworkVariable<int> SurrenderSlot = new NetworkVariable<int>(-1);

        /// <summary>A pause ends by itself after this many seconds, so a player cannot freeze the rival's game forever.</summary>
        public const float MaxPauseSeconds = 60f;
        public event Action<int> SeasonChanged;
        public event Action<int> MatchFinished;

        float _nextEnemySpawn;

        public float TimeRemaining
        {
            get
            {
                if (NetworkManager == null || !NetworkManager.IsListening) return 0f;
                if (Paused.Value) return PausedRemaining.Value;
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
            if (MapManager.Instance != null) MapManager.Instance.ShowSeason(current);
        }

        // ------------------------------------------------------------------ server state machine

        void Update()
        {
            if (!IsServer) return;

            double now = NetworkManager.ServerTime.Time;

            if (Paused.Value)
            {
                // nothing moves, the clocks do not run; it ends when the player resumes, leaves, or after MaxPauseSeconds
                bool pauserHere = false;
                foreach (PlayerStats p in PlayerStats.All) if (p.Slot.Value == PausedBySlot.Value) pauserHere = true;
                bool inMatch = Phase.Value == MatchPhase.Countdown || Phase.Value == MatchPhase.Playing;
                if (!pauserHere || !inMatch || now - PausedAt.Value > MaxPauseSeconds) ServerResume();
                return;
            }

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

        public void ServerBeginCountdown()
        {
            if (!IsServer || Phase.Value != MatchPhase.Lobby) return;

            ClearEnemies();
            SeasonIndex.Value = 0;
            WinnerSlot.Value = -1;
            SurrenderSlot.Value = -1;
            Paused.Value = false;
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

        // ------------------------------------------------------------------ pause and surrender

        /// <summary>Freezes (true) or unfreezes (false) the game for everybody. Only the player who paused can resume.</summary>
        public void RequestPause(bool pause) => SetPausedServerRpc(pause);

        /// <summary>Gives up: the rival wins the match.</summary>
        public void RequestSurrender() => SurrenderServerRpc();

        PlayerStats PlayerOf(ulong clientId)
        {
            foreach (PlayerStats p in PlayerStats.All) if (p.OwnerClientId == clientId) return p;
            return null;
        }

        [ServerRpc(RequireOwnership = false)]
        void SetPausedServerRpc(bool pause, ServerRpcParams rpcParams = default)
        {
            PlayerStats who = PlayerOf(rpcParams.Receive.SenderClientId);
            if (who == null) return;
            if (pause)
            {
                bool inMatch = Phase.Value == MatchPhase.Countdown || Phase.Value == MatchPhase.Playing;
                if (!inMatch || Paused.Value) return;
                PausedAt.Value = NetworkManager.ServerTime.Time;
                PausedRemaining.Value = TimeRemaining;
                PausedBySlot.Value = who.Slot.Value;
                Paused.Value = true;
            }
            else if (Paused.Value && PausedBySlot.Value == who.Slot.Value)
            {
                ServerResume();
            }
        }

        /// <summary>Ends the pause and moves every running clock forward by the time that was frozen.</summary>
        void ServerResume()
        {
            if (!IsServer || !Paused.Value) return;
            double frozen = NetworkManager.ServerTime.Time - PausedAt.Value;
            if (Phase.Value == MatchPhase.Countdown || Phase.Value == MatchPhase.Playing) PhaseEndTime.Value += frozen;
            foreach (PlayerStats p in PlayerStats.All)
                if (p.IsDead.Value) p.RespawnAt.Value += frozen;
            _nextEnemySpawn += (float)frozen;
            Paused.Value = false;
            PausedBySlot.Value = -1;
        }

        [ServerRpc(RequireOwnership = false)]
        void SurrenderServerRpc(ServerRpcParams rpcParams = default)
        {
            PlayerStats who = PlayerOf(rpcParams.Receive.SenderClientId);
            if (who == null) return;
            if (Phase.Value != MatchPhase.Countdown && Phase.Value != MatchPhase.Playing) return;

            int rival = -1;
            foreach (PlayerStats p in PlayerStats.All) if (p != who) rival = p.Slot.Value;
            if (rival < 0) { ServerFinishMatch(); return; }          // nobody to win: just end it

            SurrenderSlot.Value = who.Slot.Value;
            ServerFinishMatch(rival);
        }

        void ServerFinishMatch(int forcedWinner = -2)
        {
            ClearEnemies();
            Paused.Value = false;
            PausedBySlot.Value = -1;

            int winner = -1;
            PlayerStats best = null;
            bool tie = false;
            foreach (PlayerStats p in PlayerStats.All)
            {
                if (best == null) { best = p; continue; }
                int cmp = p.Score.Value.CompareTo(best.Score.Value);
                if (cmp == 0) cmp = best.Deaths.Value.CompareTo(p.Deaths.Value);  
                if (cmp > 0) { best = p; tie = false; }
                else if (cmp == 0) tie = true;
            }
            if (best != null && !tie) winner = best.Slot.Value;
            if (forcedWinner != -2) winner = forcedWinner;           // the rival gave up

            WinnerSlot.Value = winner;
            Phase.Value = MatchPhase.Finished;
            MatchFinishedClientRpc(winner);
            ReportToDatabase(winner);
        }

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
            if (blue == null || red == null) return;                       

            string blueId = blue.SdbIdentity.Value.ToString();
            string redId = red.SdbIdentity.Value.ToString();
            if (blueId.Length == 0 || redId.Length == 0 || blueId == redId) return;

            string code = db.CurrentPairing != null ? db.CurrentPairing.Code : "";
            db.ReportMatch(code, blueId, blue.Score.Value, blue.Kills.Value, blue.Deaths.Value,
                                 redId, red.Score.Value, red.Kills.Value, red.Deaths.Value, winner);
            Debug.Log($"[DB] reported match {(code.Length > 0 ? code : "(no code)")}: blue {blue.Score.Value} vs red {red.Score.Value}, winner slot {winner}");
        }

        public void ServerRematch()
        {
            if (!IsServer || Phase.Value != MatchPhase.Finished) return;
            ClearEnemies();
            SurrenderSlot.Value = -1;
            Phase.Value = MatchPhase.Lobby;
        }

        
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
            go.GetComponent<NetworkObject>().Spawn(true);    
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

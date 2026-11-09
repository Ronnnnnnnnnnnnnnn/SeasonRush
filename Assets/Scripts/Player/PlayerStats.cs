using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// The synchronized state of one player: health, score, kills, deaths, alive/dead and which
    /// colour slot (0 = blue, 1 = red) they were given. Only the server changes these
    /// NetworkVariables; every client can read them to draw the HUD.
    /// </summary>
    public class PlayerStats : NetworkBehaviour
    {
        public static readonly List<PlayerStats> All = new List<PlayerStats>();
        public static PlayerStats Local { get; private set; }

        public readonly NetworkVariable<int> Health = new NetworkVariable<int>(GameConfig.PlayerMaxHealth);
        public readonly NetworkVariable<int> Score = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Kills = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Deaths = new NetworkVariable<int>(0);
        public readonly NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);
        /// <summary>Server time (seconds) at which a dead player comes back.</summary>
        public readonly NetworkVariable<double> RespawnAt = new NetworkVariable<double>(0d);
        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>(-1);

        /// <summary>The name this player chose (empty = not told yet). Shown in the HUD and saved with the result.</summary>
        public readonly NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>(default);
        /// <summary>The player's SpacetimeDB identity as 64 hex characters (empty = no database connection).</summary>
        public readonly NetworkVariable<FixedString128Bytes> SdbIdentity = new NetworkVariable<FixedString128Bytes>(default);

        public PlayerController Controller { get; private set; }

        bool _profileSent;
        float _spawnedAt;

        /// <summary>Name for the HUD: the chosen name, or BLUE / RED until it arrives.</summary>
        public string DisplayName => PlayerName.Value.IsEmpty
            ? GameConfig.SlotName(Mathf.Max(0, Slot.Value))
            : PlayerName.Value.ToString();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
            Local = null;
        }

        public override void OnNetworkSpawn()
        {
            Controller = GetComponent<PlayerController>();
            All.Add(this);
            _spawnedAt = Time.time;
            if (IsOwner) Local = this;
            if (IsServer) Slot.Value = LowestFreeSlot(this);
        }

        // ------------------------------------------------------------------ profile (owner -> server)

        /// <summary>The owner tells the server its name and database identity once both are known.</summary>
        void TrySendProfile()
        {
            SpacetimeManager db = SpacetimeManager.Instance;
            bool stillConnecting = db != null && db.State == SpacetimeManager.ConnState.Connecting && Time.time - _spawnedAt < 5f;
            if (stillConnecting) return;

            _profileSent = true;
            string identity = db != null && db.State == SpacetimeManager.ConnState.Connected ? db.IdentityHex : "";
            SubmitProfileServerRpc(LocalPlayer.Name, identity);
        }

        [ServerRpc]
        void SubmitProfileServerRpc(string playerName, string identityHex)
        {
            string clean = LocalPlayer.Clean(playerName);
            PlayerName.Value = new FixedString64Bytes(clean);

            // an identity is exactly 64 hex characters; anything else is ignored
            bool valid = identityHex != null && identityHex.Length == 64;
            if (valid)
                foreach (char c in identityHex)
                    if (!Uri.IsHexDigit(c)) { valid = false; break; }
            SdbIdentity.Value = new FixedString128Bytes(valid ? identityHex.ToLowerInvariant() : "");
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
        }

        /// <summary>Lowest colour slot nobody else is using. Used for the spawn position and the colour.</summary>
        public static int LowestFreeSlot(PlayerStats ignore)
        {
            for (int slot = 0; slot < GameConfig.MaxPlayers; slot++)
            {
                bool used = false;
                foreach (PlayerStats p in All)
                {
                    if (p == ignore) continue;
                    if (p.Slot.Value == slot) { used = true; break; }
                }
                if (!used) return slot;
            }
            return GameConfig.MaxPlayers - 1;
        }

        void Update()
        {
            if (IsOwner && IsSpawned && !_profileSent) TrySendProfile();
            if (!IsServer) return;
            if (IsDead.Value && NetworkManager.ServerTime.Time >= RespawnAt.Value) ServerRespawn();
        }

        // ------------------------------------------------------------------ server-only API

        public void ServerTakeDamage(int amount)
        {
            if (!IsServer || IsDead.Value || amount <= 0) return;

            Health.Value = Mathf.Max(0, Health.Value - amount);
            HitClientRpc();

            if (Health.Value <= 0)
            {
                IsDead.Value = true;
                Deaths.Value += 1;
                // Concept doc: dying costs a 5 second wait while the rival scores for free.
                RespawnAt.Value = NetworkManager.ServerTime.Time + GameConfig.RespawnSeconds;
            }
        }

        public void ServerRespawn()
        {
            if (!IsServer) return;
            Health.Value = GameConfig.PlayerMaxHealth;
            IsDead.Value = false;
            MoveToSpawn();
        }

        /// <summary>Fresh start for a new match: zero the score and put the player on their spawn.</summary>
        public void ServerResetForMatch()
        {
            if (!IsServer) return;
            Score.Value = 0;
            Kills.Value = 0;
            Deaths.Value = 0;
            Health.Value = GameConfig.PlayerMaxHealth;
            IsDead.Value = false;
            MoveToSpawn();
        }

        public void MoveToSpawn()
        {
            if (!IsServer || Controller == null || MapManager.Instance == null) return;
            int season = MatchManager.Instance != null ? MatchManager.Instance.SeasonIndex.Value : 0;
            Controller.ServerTeleport(MapManager.Instance.GetPlayerSpawn(Mathf.Max(0, Slot.Value), season));
        }

        [ClientRpc]
        void HitClientRpc()
        {
            if (Controller != null) Controller.FlashHit();
        }
    }
}

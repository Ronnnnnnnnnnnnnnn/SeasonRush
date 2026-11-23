using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SeasonRush
{
    
    public class PlayerStats : NetworkBehaviour
    {
        public static readonly List<PlayerStats> All = new List<PlayerStats>();
        public static PlayerStats Local { get; private set; }

        public readonly NetworkVariable<int> Health = new NetworkVariable<int>(GameConfig.PlayerMaxHealth);
        public readonly NetworkVariable<int> Score = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Kills = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Deaths = new NetworkVariable<int>(0);
        public readonly NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false);
        public readonly NetworkVariable<double> RespawnAt = new NetworkVariable<double>(0d);
        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>(-1);

        public readonly NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>(default);
        public readonly NetworkVariable<FixedString128Bytes> SdbIdentity = new NetworkVariable<FixedString128Bytes>(default);

        public PlayerController Controller { get; private set; }

        bool _profileSent;
        float _spawnedAt;

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
            IsDead.OnValueChanged += OnDeadChanged;
        }

        void OnDeadChanged(bool previous, bool dead)
        {
            if (dead && IsOwner) AudioManager.PlayDeath();
        }

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

            bool valid = identityHex != null && identityHex.Length == 64;
            if (valid)
                foreach (char c in identityHex)
                    if (!Uri.IsHexDigit(c)) { valid = false; break; }
            SdbIdentity.Value = new FixedString128Bytes(valid ? identityHex.ToLowerInvariant() : "");
        }

        public override void OnNetworkDespawn()
        {
            IsDead.OnValueChanged -= OnDeadChanged;
            All.Remove(this);
            if (Local == this) Local = null;
        }

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

        public void ServerTakeDamage(int amount)
        {
            if (!IsServer || IsDead.Value || amount <= 0) return;

            Health.Value = Mathf.Max(0, Health.Value - amount);
            HitClientRpc();

            if (Health.Value <= 0)
            {
                IsDead.Value = true;
                Deaths.Value += 1;
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
            if (IsOwner) AudioManager.PlayHurt();
        }
    }
}

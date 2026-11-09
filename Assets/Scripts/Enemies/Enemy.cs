using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// A season monster. Enemies are spawned and despawned by the server only. The server runs their
    /// AI and replicates the position with a server-authority NetworkTransform; health is a
    /// NetworkVariable so every client can draw the health bar.
    ///
    /// TypeId encodes which monster this is: season * 3 + tier (weak / medium / strong). Both the
    /// server and the clients look the stats and sprite up in the matching <see cref="SeasonTheme"/>.
    /// </summary>
    public class Enemy : NetworkBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        public readonly NetworkVariable<int> TypeId = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Health = new NetworkVariable<int>(1);

        [Header("Visuals (assigned on the prefab)")]
        public Transform visual;
        public SpriteRenderer body;

        /// <summary>Set by the spawner on the server before Spawn() is called.</summary>
        [System.NonSerialized] public int PendingTypeId;

        EnemyDef _def;
        Rigidbody2D _rb;
        CircleCollider2D _collider;
        bool _dying;
        float _nextHit;
        float _knockUntil;
        float _flashUntil;
        Vector2 _knockVelocity;
        Vector2 _lastPlane;
        float _facingSign = 1f;
        float _bobPhase;
        float _stuckTime;
        float _sideUntil;
        float _sideSign = 1f;
        bool _inReach;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public EnemyDef Def => _def;
        public Vector2 PlanePosition => transform.position;
        public float Radius => _def != null ? _def.radius : 0.4f;
        public int MaxHealth => _def != null ? Mathf.Max(1, _def.maxHealth) : 1;
        public float HealthFraction => Mathf.Clamp01(Health.Value / (float)MaxHealth);
        public int Season => TypeId.Value / 3;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            TypeId.OnValueChanged += OnTypeChanged;
            _lastPlane = transform.position;

            if (IsServer)
            {
                TypeId.Value = PendingTypeId;
                ApplyDefinition();
                Health.Value = MaxHealth;
            }
            else
            {
                ApplyDefinition();
            }
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            TypeId.OnValueChanged -= OnTypeChanged;
        }

        void OnTypeChanged(int previous, int current) => ApplyDefinition();

        void ApplyDefinition()
        {
            if (MapManager.Instance == null) return;
            int season = TypeId.Value / 3;
            int tier = TypeId.Value % 3;
            _def = MapManager.Instance.Theme(season).GetEnemy(tier);
            if (_def == null) return;

            body.sprite = _def.sprite;
            body.color = _def.tint;
            visual.localScale = Vector3.one * _def.scale;
            if (_collider != null) _collider.radius = _def.radius * 0.8f;
            gameObject.name = $"Enemy_{_def.displayName}";
        }

        // ------------------------------------------------------------------ server AI

        void FixedUpdate()
        {
            if (!IsServer || _def == null) return;

            if (_dying)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 position = _rb.position;
            Vector2 velocity = Vector2.zero;

            bool playing = MatchManager.Instance != null && MatchManager.Instance.Phase.Value == MatchPhase.Playing;
            PlayerStats target = playing ? NearestLivingPlayer(position) : null;
            if (target != null)
            {
                Vector2 to = target.Controller.PlanePosition - position;
                float distance = to.magnitude;
                float reach = _def.radius + 0.55f;
                if (distance > reach)
                {
                    velocity = to / distance * _def.speed;
                    _inReach = false;
                }
                else
                {
                    // short wind-up the first time it reaches you, so a swarm does not hit in the same instant
                    if (!_inReach)
                    {
                        _inReach = true;
                        _nextHit = Mathf.Max(_nextHit, Time.time + 0.5f);
                    }
                    if (Time.time >= _nextHit)
                    {
                        _nextHit = Time.time + 1.1f;
                        target.ServerTakeDamage(_def.contactDamage);
                    }
                }
            }

            // No pathfinding: if the enemy keeps pushing against a tree or rock without making progress,
            // it side-steps for a moment so it slides around the obstacle instead of getting stuck behind it.
            if (velocity.sqrMagnitude > 0.01f)
            {
                float actualSpeed = _rb.linearVelocity.magnitude;
                if (actualSpeed < velocity.magnitude * 0.35f) _stuckTime += Time.fixedDeltaTime;
                else _stuckTime = Mathf.Max(0f, _stuckTime - Time.fixedDeltaTime * 2f);

                if (_stuckTime > 0.3f && Time.time >= _sideUntil)
                {
                    _sideSign = Random.value < 0.5f ? -1f : 1f;
                    _sideUntil = Time.time + 0.7f;
                    _stuckTime = 0f;
                }
                if (Time.time < _sideUntil) velocity = (Vector2)(Quaternion.Euler(0f, 0f, 75f * _sideSign) * velocity);
            }

            // keep enemies from stacking on top of each other
            foreach (Enemy other in All)
            {
                if (other == this) continue;
                Vector2 away = position - (Vector2)other.transform.position;
                float d = away.magnitude;
                if (d > 0.001f && d < 0.8f) velocity += away / d * (0.8f - d) * 3.5f;
            }

            if (Time.time < _knockUntil) velocity = _knockVelocity;
            _rb.linearVelocity = velocity;
        }

        static PlayerStats NearestLivingPlayer(Vector2 from)
        {
            PlayerStats best = null;
            float bestSqr = float.MaxValue;
            foreach (PlayerStats p in PlayerStats.All)
            {
                if (p.IsDead.Value || p.Controller == null) continue;
                float sqr = (p.Controller.PlanePosition - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }

        /// <summary>Called on the server by a player's attack. Handles damage, knock-back and the kill reward.</summary>
        public void ServerTakeDamage(int damage, ulong attackerClientId, Vector2 hitDirection)
        {
            if (!IsServer || _dying || _def == null) return;

            Health.Value = Mathf.Max(0, Health.Value - damage);
            _knockVelocity = hitDirection.normalized * GameConfig.EnemyKnockbackSpeed;
            _knockUntil = Time.time + 0.14f;

            if (Health.Value <= 0)
            {
                _dying = true;
                if (MatchManager.Instance != null)
                    MatchManager.Instance.ServerAwardKill(attackerClientId, _def.points, PlanePosition, _def.displayName);
                NetworkObject.Despawn(true);      // removes it on the server and on every client
            }
            else
            {
                HitClientRpc();
            }
        }

        [ClientRpc]
        void HitClientRpc() => _flashUntil = Time.time + 0.12f;

        // ------------------------------------------------------------------ drawing (all peers)

        void LateUpdate()
        {
            if (visual == null || body == null) return;

            Vector2 plane = transform.position;
            Vector3 screen = Iso.ToScreen3(plane);

            Vector2 delta = plane - _lastPlane;
            _lastPlane = plane;
            float screenDx = Iso.ToScreen(delta).x;
            if (Mathf.Abs(screenDx) > 0.0005f) _facingSign = screenDx >= 0f ? 1f : -1f;

            Sprite[] frames = _def != null ? _def.frames : null;
            if (frames != null && frames.Length >= 2)
            {
                // the artist's own animation (for example a cloud floating up and down); every enemy starts at a different moment
                int index = Mathf.FloorToInt(Time.time * Mathf.Max(0.1f, _def.frameRate) + (GetInstanceID() & 3) * 0.37f) % frames.Length;
                if (frames[index] != null) body.sprite = frames[index];
            }
            else
            {
                // little hover/hop so the monsters feel alive without a full animation set
                _bobPhase += Time.deltaTime * 6f;
                screen.y += Mathf.Abs(Mathf.Sin(_bobPhase + GetInstanceID())) * 0.08f;
            }

            visual.position = screen;
            body.flipX = _facingSign < 0f;
            body.sortingOrder = Iso.SortOrder(plane, 3);

            Color baseColour = _def != null ? _def.tint : Color.white;
            body.color = Time.time < _flashUntil ? Color.Lerp(baseColour, Color.white, 0.8f) : baseColour;
        }
    }
}

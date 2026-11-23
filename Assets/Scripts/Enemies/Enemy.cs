using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SeasonRush
{
   
    public class Enemy : NetworkBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        public readonly NetworkVariable<int> TypeId = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Health = new NetworkVariable<int>(1);

        [Header("Visuals (assigned on the prefab)")]
        public Transform visual;
        public SpriteRenderer body;

        [System.NonSerialized] public int PendingTypeId;

        GameObject _prefabInstance, _prefabSource;
        SpriteRenderer _prefabBody;
        Animator _animator;
        bool _hasMovingParam, _hasAttackParam;
        static readonly int MovingHash = Animator.StringToHash("IsMoving");
        static readonly int AttackHash = Animator.StringToHash("Attack");

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

        SpriteRenderer Body => _prefabBody != null ? _prefabBody : body;

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

            UseVisualPrefab(_def.visualPrefab);
            if (_prefabBody == null) body.sprite = _def.sprite;
            Body.color = _def.tint;
            visual.localScale = Vector3.one * _def.scale;
            if (_collider != null) _collider.radius = _def.radius * 0.8f;
            gameObject.name = $"Enemy_{_def.displayName}";
        }

        void UseVisualPrefab(GameObject prefab)
        {
            if (prefab == _prefabSource && (prefab == null || _prefabInstance != null)) return;
            if (_prefabInstance != null) Destroy(_prefabInstance);
            _prefabInstance = null;
            _prefabSource = prefab;
            _prefabBody = null;
            _animator = null;
            _hasMovingParam = _hasAttackParam = false;
            body.enabled = true;
            if (prefab == null) return;

            _prefabInstance = Instantiate(prefab, visual);
            _prefabInstance.name = prefab.name;
            _prefabInstance.transform.localPosition = Vector3.zero;
            _prefabInstance.transform.localRotation = Quaternion.identity;
            _prefabBody = _prefabInstance.GetComponentInChildren<SpriteRenderer>();
            _animator = _prefabInstance.GetComponentInChildren<Animator>();
            if (_prefabBody != null) body.enabled = false;
            if (_animator != null)
                foreach (AnimatorControllerParameter p in _animator.parameters)
                {
                    if (p.nameHash == MovingHash && p.type == AnimatorControllerParameterType.Bool) _hasMovingParam = true;
                    if (p.nameHash == AttackHash && p.type == AnimatorControllerParameterType.Trigger) _hasAttackParam = true;
                }
        }

        void FixedUpdate()
        {
            if (!IsServer || _def == null) return;

            if (MatchManager.Instance != null && MatchManager.Instance.Paused.Value)
            {
                _rb.linearVelocity = Vector2.zero;
                _nextHit = Mathf.Max(_nextHit, Time.time + 0.4f);
                return;
            }

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
                    if (!_inReach)
                    {
                        _inReach = true;
                        _nextHit = Mathf.Max(_nextHit, Time.time + 0.5f);
                    }
                    if (Time.time >= _nextHit)
                    {
                        _nextHit = Time.time + 1.1f;
                        AttackClientRpc();
                        target.ServerTakeDamage(_def.contactDamage);
                    }
                }
            }

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

        public void ServerTakeDamage(int damage, ulong attackerClientId, Vector2 hitDirection)
        {
            if (!IsServer || _dying || _def == null) return;

            Health.Value = Mathf.Max(0, Health.Value - damage);
            _knockVelocity = hitDirection.normalized * GameConfig.EnemyKnockbackSpeed;
            _knockUntil = Time.time + 0.14f;

            if (Health.Value <= 0)
            {
                _dying = true;
                DieClientRpc();
                if (MatchManager.Instance != null)
                    MatchManager.Instance.ServerAwardKill(attackerClientId, _def.points, PlanePosition, _def.displayName);
                NetworkObject.Despawn(true);
            }
            else
            {
                HitClientRpc();
            }
        }

        [ClientRpc]
        void HitClientRpc() => _flashUntil = Time.time + 0.12f;

        [ClientRpc]
        void AttackClientRpc()
        {
            if (_animator != null && _hasAttackParam) _animator.SetTrigger(AttackHash);
        }

        [ClientRpc]
        void DieClientRpc()
        {
            if (_def == null || _def.visualPrefab == null || _prefabBody == null) return;
            EnemyDeathFx.Play(_def.visualPrefab, visual.position, visual.localScale, _prefabBody.flipX, _prefabBody.sortingOrder, _def.tint);
        }

        void LateUpdate()
        {
            if (visual == null || body == null) return;
            SpriteRenderer shown = Body;

            Vector2 plane = transform.position;
            Vector3 screen = Iso.ToScreen3(plane);

            Vector2 delta = plane - _lastPlane;
            _lastPlane = plane;
            float screenDx = Iso.ToScreen(delta).x;
            if (Mathf.Abs(screenDx) > 0.0005f) _facingSign = screenDx >= 0f ? 1f : -1f;

            Sprite[] frames = _def != null ? _def.frames : null;
            bool paused = GameHud.Paused;
            if (_animator != null) _animator.speed = paused ? 0f : 1f;
            if (_animator != null && _hasMovingParam) _animator.SetBool(MovingHash, delta.sqrMagnitude > 0.00002f);

            bool pictureFrames = frames != null && frames.Length >= 2;
            if (_animator == null && pictureFrames)
            {
                int index = Mathf.FloorToInt(Time.time * Mathf.Max(0.1f, _def.frameRate) + (GetInstanceID() & 3) * 0.37f) % frames.Length;
                if (frames[index] != null) shown.sprite = frames[index];
            }
            if (!pictureFrames)
            {
                if (!paused) _bobPhase += Time.deltaTime * 6f;
                screen.y += Mathf.Abs(Mathf.Sin(_bobPhase + GetInstanceID())) * 0.08f;
            }

            visual.position = screen;
            bool artLooksLeft = _def != null && _def.artFacing == ArtFacing.Left;
            shown.flipX = artLooksLeft ? _facingSign > 0f : _facingSign < 0f;
            shown.sortingOrder = Iso.SortOrder(plane, 3);

            Color baseColour = _def != null ? _def.tint : Color.white;
            shown.color = Time.time < _flashUntil ? Color.Lerp(baseColour, Color.white, 0.8f) : baseColour;
        }
    }
}

using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonRush
{
    
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Visuals (assigned on the prefab)")]
        public Transform visual;
        public SpriteRenderer body;
        public SpriteRenderer tunic;

        public static Vector2? TestPlaneMove;

        readonly NetworkVariable<Vector2> _facing = new NetworkVariable<Vector2>(
            Vector2.right, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Rigidbody2D _rb;
        PlayerStats _stats;
        NetworkTransform _netTransform;
        Vector2 _moveDir;
        Vector2 _knock;
        Vector2 _lastPlane;
        float _flashUntil;
        float _bobPhase;
        bool _hasSkin;

        public Animator animator;
        public NetworkAnimator netAnimator;

        RuntimeAnimatorController _baseController;
        bool _hasMovingParam, _hasAttackParam, _hasDeadParam;
        static readonly int DeadHash = Animator.StringToHash("IsDead");
        static readonly int MovingHash = Animator.StringToHash("IsMoving");
        static readonly int AttackHash = Animator.StringToHash("Attack");

        bool Animated => animator != null && animator.runtimeAnimatorController != null;
        bool Authority => netAnimator != null && netAnimator.IsOwner;

        public Vector2 Facing => _facing.Value.sqrMagnitude > 0.01f ? _facing.Value.normalized : Vector2.right;
        public Vector2 PlanePosition => transform.position;

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _stats = GetComponent<PlayerStats>();
            _netTransform = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            _stats.Slot.OnValueChanged += OnSlotChanged;
            ApplyColour(_stats.Slot.Value);
            _lastPlane = transform.position;

            if (IsOwner)
            {
                Vector2 centre = Vector2.one * (GameConfig.ArenaSize * 0.5f);
                Vector2 toCentre = centre - (Vector2)transform.position;
                _facing.Value = toCentre.sqrMagnitude > 0.01f ? toCentre.normalized : Vector2.right;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_stats != null) _stats.Slot.OnValueChanged -= OnSlotChanged;
        }

        void OnSlotChanged(int previous, int current) => ApplyColour(current);

        void ApplyColour(int slot)
        {
            GameObject skinPrefab = slot >= 0 ? PlayerSkins.PrefabFor(slot) : null;
            Animator slotAnimator = skinPrefab != null ? skinPrefab.GetComponentInChildren<Animator>() : null;
            RuntimeAnimatorController slotController = slotAnimator != null ? slotAnimator.runtimeAnimatorController : null;
            SpriteRenderer slotRenderer = skinPrefab != null ? skinPrefab.GetComponentInChildren<SpriteRenderer>() : null;

            bool animated = animator != null && slotController != null;
            if (animated) UseController(slotController);

            Sprite skin = slot >= 0 ? PlayerSkins.For(slot) : null;
            if (slotRenderer != null && slotRenderer.sprite != null) skin = slotRenderer.sprite;
            _hasSkin = animated || (skin != null && body != null);

            if (_hasSkin)
            {
                if (skin != null && body != null) body.sprite = skin;
                if (tunic != null) tunic.gameObject.SetActive(false);
            }
            else if (tunic != null && slot >= 0)
            {
                tunic.color = GameConfig.SlotColor(slot);
            }
            gameObject.name = slot >= 0 ? $"Player_{GameConfig.SlotName(slot)}" : "Player";
        }

        void UseController(RuntimeAnimatorController slotController)
        {
            if (_baseController == null) _baseController = animator.runtimeAnimatorController;
            RuntimeAnimatorController target = _baseController;
            if (slotController != _baseController)
            {
                var overrides = new AnimatorOverrideController(_baseController);
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                foreach (AnimationClip clip in _baseController.animationClips)
                {
                    AnimationClip replacement = null;
                    foreach (AnimationClip candidate in slotController.animationClips)
                        if (candidate.name == clip.name) { replacement = candidate; break; }
                    pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip, replacement != null ? replacement : clip));
                }
                overrides.ApplyOverrides(pairs);
                target = overrides;
            }
            if (animator.runtimeAnimatorController != target) animator.runtimeAnimatorController = target;

            _hasMovingParam = _hasAttackParam = _hasDeadParam = false;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == MovingHash && p.type == AnimatorControllerParameterType.Bool) _hasMovingParam = true;
                if (p.nameHash == AttackHash && p.type == AnimatorControllerParameterType.Trigger) _hasAttackParam = true;
                if (p.nameHash == DeadHash && p.type == AnimatorControllerParameterType.Bool) _hasDeadParam = true;
            }
        }

        public void PlayAttack()
        {
            if (Authority && _hasAttackParam) netAnimator.SetTrigger(AttackHash);
        }

        void Update()
        {
            if (!IsOwner) return;

            Vector2 dir = Vector2.zero;
            if (!_stats.IsDead.Value)
            {
                if (TestPlaneMove.HasValue)
                {
                    dir = TestPlaneMove.Value;
                    if (dir.sqrMagnitude > 1f) dir.Normalize();
                }
                else
                {
                    Vector2 input = Vector2.zero;

                    if ((Application.isFocused || GameConfig.TestMode) && Keyboard.current != null)
                    {
                        Keyboard kb = Keyboard.current;
                        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                    }

                    input += VirtualControls.Move;

                    dir = Iso.InputToPlane(input) * Mathf.Clamp01(input.magnitude);
                }
            }
            if (GameHud.Paused) dir = Vector2.zero;
            _moveDir = dir;

            if (dir.sqrMagnitude > 0.01f && Vector2.Angle(dir, _facing.Value) > 4f) _facing.Value = dir;
        }

        void FixedUpdate()
        {
            if (!IsOwner) return;

            if (_stats.IsDead.Value || GameHud.Paused)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            _rb.linearVelocity = _moveDir * GameConfig.PlayerSpeed + _knock;
            _knock = Vector2.MoveTowards(_knock, Vector2.zero, 28f * Time.fixedDeltaTime);
        }

        void LateUpdate()
        {
            if (visual == null) return;

            Vector2 plane = transform.position;
            Vector3 screen = Iso.ToScreen3(plane);

            float speed = Time.deltaTime > 0f ? (plane - _lastPlane).magnitude / Time.deltaTime : 0f;
            _lastPlane = plane;
            if (speed > 0.6f) _bobPhase += Time.deltaTime * 14f;
            else _bobPhase = 0f;
            screen.y += Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.07f;
            if (Animated) animator.speed = GameHud.Paused ? 0f : 1f;
            if (Authority && _hasMovingParam) animator.SetBool(MovingHash, speed > 0.6f && !_stats.IsDead.Value);

            visual.position = screen;

            int order = Iso.SortOrder(plane, 5);
            SpriteRenderer shown = body;
            shown.sortingOrder = order;
            if (tunic.gameObject.activeSelf) tunic.sortingOrder = order + 1;

            bool facingLeft = Iso.ToScreen(Facing).x < 0f;
            shown.flipX = _hasSkin && PlayerSkins.ArtFacesLeft ? !facingLeft : facingLeft;
            tunic.flipX = facingLeft;

            bool dead = _stats.IsDead.Value;
            if (Authority && _hasDeadParam) animator.SetBool(DeadHash, dead);
            shown.enabled = !dead || (Animated && _hasDeadParam);
            if (tunic.gameObject.activeSelf) tunic.enabled = !dead;

            shown.color = Time.time < _flashUntil ? new Color(1f, 0.45f, 0.45f) : Color.white;
        }

        public void FlashHit() => _flashUntil = Time.time + 0.18f;

        public void ServerTeleport(Vector2 position)
        {
            if (!IsServer) return;
            TeleportClientRpc(position, OwnerOnly());
        }

        public void ServerKnockback(Vector2 direction)
        {
            if (!IsServer) return;
            KnockbackClientRpc(direction, OwnerOnly());
        }

        ClientRpcParams OwnerOnly()
        {
            return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };
        }

        [ClientRpc]
        void TeleportClientRpc(Vector2 position, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            _knock = Vector2.zero;
            _rb.linearVelocity = Vector2.zero;
            _rb.position = position;
            transform.position = position;
            _netTransform.Teleport(position, Quaternion.identity, Vector3.one);
            _lastPlane = position;
        }

        [ClientRpc]
        void KnockbackClientRpc(Vector2 direction, ClientRpcParams rpcParams = default)
        {
            if (!IsOwner) return;
            _knock = direction.normalized * GameConfig.KnockbackSpeed;
            FlashHit();
        }
    }
}

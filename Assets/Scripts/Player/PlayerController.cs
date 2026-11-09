using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonRush
{
    /// <summary>
    /// Movement and presentation of the warrior.
    ///
    /// Networking model: each player OWNS their warrior. The owner reads the keyboard and moves their
    /// own Rigidbody2D; the NetworkTransform (set to Owner authority) replicates the position to
    /// everyone else, where a NetworkRigidbody2D keeps the body kinematic so remote copies just
    /// follow the interpolated network position.
    ///
    /// The transform of this object is on the flat gameplay plane. The drawn sprite (the child
    /// "Visual") is projected to the isometric screen position every frame.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("Visuals (assigned on the prefab)")]
        public Transform visual;
        public SpriteRenderer body;
        public SpriteRenderer tunic;      // tinted with the player's colour

        /// <summary>Automated tests set these to drive the local player without a keyboard.</summary>
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
        bool _hasSkin;                    // true while the body shows a PlayerSkins picture instead of the placeholder

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
                // start by looking at the middle of the arena
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
            // Player 1 / player 2 have their own full-colour pictures (PlayerSkins). Without one the placeholder
            // warrior is kept: a grey body with a tunic that is tinted in the player's colour.
            Sprite skin = slot >= 0 ? PlayerSkins.For(slot) : null;
            _hasSkin = skin != null && body != null;
            if (_hasSkin)
            {
                body.sprite = skin;
                if (tunic != null) tunic.gameObject.SetActive(false);
            }
            else if (tunic != null && slot >= 0)
            {
                tunic.color = GameConfig.SlotColor(slot);
            }
            gameObject.name = slot >= 0 ? $"Player_{GameConfig.SlotName(slot)}" : "Player";
        }

        // ------------------------------------------------------------------ input (owner only)

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

                    // keyboard
                    if ((Application.isFocused || GameConfig.TestMode) && Keyboard.current != null)
                    {
                        Keyboard kb = Keyboard.current;
                        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                    }

                    // on-screen joystick (analog: a small push walks slower)
                    input += VirtualControls.Move;

                    dir = Iso.InputToPlane(input) * Mathf.Clamp01(input.magnitude);
                }
            }
            if (GameHud.Paused) dir = Vector2.zero;            // the pause screen is open: the warrior stands still
            _moveDir = dir;

            // keep the replicated facing up to date (only write when it really changed to save bandwidth)
            if (dir.sqrMagnitude > 0.01f && Vector2.Angle(dir, _facing.Value) > 4f) _facing.Value = dir;
        }

        void FixedUpdate()
        {
            if (!IsOwner) return;

            if (_stats.IsDead.Value)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            _rb.linearVelocity = _moveDir * GameConfig.PlayerSpeed + _knock;
            _knock = Vector2.MoveTowards(_knock, Vector2.zero, 28f * Time.fixedDeltaTime);
        }

        // ------------------------------------------------------------------ drawing (all peers)

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

            visual.position = screen;

            int order = Iso.SortOrder(plane, 5);
            body.sortingOrder = order;
            tunic.sortingOrder = order + 1;

            bool facingLeft = Iso.ToScreen(Facing).x < 0f;
            // The knight pictures look to the left (sword on the left): mirror them when the player walks to the right.
            body.flipX = _hasSkin && PlayerSkins.ArtFacesLeft ? !facingLeft : facingLeft;
            tunic.flipX = facingLeft;

            bool dead = _stats.IsDead.Value;
            body.enabled = !dead;
            tunic.enabled = !dead;

            body.color = Time.time < _flashUntil ? new Color(1f, 0.45f, 0.45f) : Color.white;
        }

        public void FlashHit() => _flashUntil = Time.time + 0.18f;

        // ------------------------------------------------------------------ server -> owner RPCs

        /// <summary>Server asks the owning client to move their own warrior (spawn, respawn, new season).</summary>
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
            // Position is owner-authoritative, so only the owner may move the object.
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

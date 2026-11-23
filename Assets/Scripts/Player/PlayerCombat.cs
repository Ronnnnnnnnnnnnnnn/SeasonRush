using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonRush
{
   
    public class PlayerCombat : NetworkBehaviour
    {
        public static bool TestAttack;

        PlayerController _controller;
        PlayerStats _stats;
        float _nextLocalAttack;
        float _nextServerAttack;

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _stats = GetComponent<PlayerStats>();
        }

        void Update()
        {
            if (!IsOwner || _stats.IsDead.Value || GameHud.Paused) return;

            bool pressed = TestAttack;

            if (VirtualControls.AttackHeld) pressed = true;

            if (Application.isFocused || GameConfig.TestMode)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame))
                    pressed = true;

                Mouse mouse = Mouse.current;
                if (!VirtualControls.Visible && mouse != null && mouse.leftButton.wasPressedThisFrame) pressed = true;
            }

            if (!pressed || Time.time < _nextLocalAttack) return;
            _nextLocalAttack = Time.time + GameConfig.AttackCooldown;

            Vector2 dir = _controller.Facing;
            SlashFx.Spawn(_controller.PlanePosition, dir, GameConfig.SlotColor(Mathf.Max(0, _stats.Slot.Value)));
            _controller.PlayAttack();
            AudioManager.PlaySlash();
            if (GameConfig.TestMode) Debug.Log($"[TEST] attack input on owner, slot {_stats.Slot.Value}, facing ({dir.x:0.00},{dir.y:0.00})");
            AttackServerRpc(dir);
        }

        [ServerRpc]
        void AttackServerRpc(Vector2 direction)
        {
            if (_stats.IsDead.Value) return;
            if (MatchManager.Instance != null && MatchManager.Instance.Paused.Value) return;
            if (Time.time < _nextServerAttack) return;
            _nextServerAttack = Time.time + GameConfig.AttackCooldown * 0.8f;

            Vector2 dir = direction.sqrMagnitude > 0.01f ? direction.normalized : _controller.Facing;
            Vector2 origin = _controller.PlanePosition;

            int hits = 0;
            foreach (Enemy enemy in Enemy.All.ToArray())
            {
                if (enemy == null || !InSwing(origin, dir, enemy.PlanePosition, enemy.Radius)) continue;
                enemy.ServerTakeDamage(GameConfig.AttackDamage, OwnerClientId, dir);
                hits++;
            }
            if (GameConfig.TestMode) Debug.Log($"[TEST] server received swing from client {OwnerClientId}: enemies hit = {hits}");

            if (GameConfig.AttackShovesRival)
            {
                foreach (PlayerStats other in PlayerStats.All)
                {
                    if (other == _stats || other.IsDead.Value || other.Controller == null) continue;
                    if (InSwing(origin, dir, other.Controller.PlanePosition, 0.4f))
                        other.Controller.ServerKnockback(dir);
                }
            }

            AttackFxClientRpc(dir);
        }

        static bool InSwing(Vector2 origin, Vector2 dir, Vector2 target, float targetRadius)
        {
            Vector2 to = target - origin;
            float distance = to.magnitude;
            if (distance > GameConfig.AttackRange + targetRadius) return false;
            if (distance < 0.05f) return true;
            return Vector2.Angle(dir, to) <= GameConfig.AttackHalfArcDegrees;
        }

        [ClientRpc]
        void AttackFxClientRpc(Vector2 direction)
        {
            if (IsOwner) return;
            SlashFx.Spawn(_controller.PlanePosition, direction, GameConfig.SlotColor(Mathf.Max(0, _stats.Slot.Value)));
        }
    }
}

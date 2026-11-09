using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonRush
{
    /// <summary>
    /// The warrior's melee attack. The owner presses the attack key and plays the slash immediately
    /// (so it feels instant), then asks the SERVER to work out what was hit. The server is the only
    /// one that damages enemies and awards points, so neither player can cheat their score.
    /// </summary>
    public class PlayerCombat : NetworkBehaviour
    {
        /// <summary>Automated tests set this to make the local player attack without a keyboard.</summary>
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

            // on-screen ATTACK button: hold it to keep swinging (the cooldown below limits the speed)
            if (VirtualControls.AttackHeld) pressed = true;

            if (Application.isFocused || GameConfig.TestMode)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame))
                    pressed = true;

                // a mouse click also attacks, unless the on-screen controls are showing (then clicks drive the joystick)
                Mouse mouse = Mouse.current;
                if (!VirtualControls.Visible && mouse != null && mouse.leftButton.wasPressedThisFrame) pressed = true;
            }

            if (!pressed || Time.time < _nextLocalAttack) return;
            _nextLocalAttack = Time.time + GameConfig.AttackCooldown;

            Vector2 dir = _controller.Facing;
            SlashFx.Spawn(_controller.PlanePosition, dir, GameConfig.SlotColor(Mathf.Max(0, _stats.Slot.Value)));
            if (GameConfig.TestMode) Debug.Log($"[TEST] attack input on owner, slot {_stats.Slot.Value}, facing ({dir.x:0.00},{dir.y:0.00})");
            AttackServerRpc(dir);
        }

        /// <summary>Owner -> server: "I swung in this direction." The server decides what was hit.</summary>
        [ServerRpc]
        void AttackServerRpc(Vector2 direction)
        {
            if (_stats.IsDead.Value) return;
            if (Time.time < _nextServerAttack) return;           // ignore swings faster than the cooldown
            _nextServerAttack = Time.time + GameConfig.AttackCooldown * 0.8f;

            Vector2 dir = direction.sqrMagnitude > 0.01f ? direction.normalized : _controller.Facing;
            Vector2 origin = _controller.PlanePosition;

            // enemies: copy the list because a kill despawns the enemy and removes it from Enemy.All
            int hits = 0;
            foreach (Enemy enemy in Enemy.All.ToArray())
            {
                if (enemy == null || !InSwing(origin, dir, enemy.PlanePosition, enemy.Radius)) continue;
                enemy.ServerTakeDamage(GameConfig.AttackDamage, OwnerClientId, dir);
                hits++;
            }
            if (GameConfig.TestMode) Debug.Log($"[TEST] server received swing from client {OwnerClientId}: enemies hit = {hits}");

            // the rival is never damaged (score only comes from enemies) but a hit shoves them away
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

        /// <summary>Server -> everyone: show the slash. The swinger already played it locally.</summary>
        [ClientRpc]
        void AttackFxClientRpc(Vector2 direction)
        {
            if (IsOwner) return;
            SlashFx.Spawn(_controller.PlanePosition, direction, GameConfig.SlotColor(Mathf.Max(0, _stats.Slot.Value)));
        }
    }
}

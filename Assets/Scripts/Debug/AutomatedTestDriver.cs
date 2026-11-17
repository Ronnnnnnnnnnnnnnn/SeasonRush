using System.IO;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SeasonRush
{
   
    public class AutomatedTestDriver : MonoBehaviour
    {
        bool _bot;
        bool _simKeys;
        bool _simJoystick;
        string _lastStickLabel = "";
        float _playStart = -1f;
        string _lastKeysLabel = "";
        bool _active;
        float _time;
        float _nextLog;
        float _nextShot;
        float _shotEvery;
        float _quitAfter = 60f;
        string _outDir = "TestOutput";
        string _tag = "peer";
        int _shots;
        bool _soloStarted;
        float _finishedAt = -1f;
        bool _backPressed;
        float _playingAt = -1f;
        bool _pauseOpened, _quitPressed, _lobbyBackPressed, _pauseLogged, _surrendered;
        float _pausedRemaining;

        void Start()
        {
            _active = GameConfig.HasFlag("-autotest");
            if (!_active) { enabled = false; return; }

            if (GameConfig.TryGetArg("-quitAfter", out string q) && float.TryParse(q, out float qa)) _quitAfter = qa;
            if (GameConfig.TryGetArg("-shotEvery", out string se) && float.TryParse(se, out float sev)) _shotEvery = sev;
            if (GameConfig.TryGetArg("-outDir", out string od)) _outDir = od;
            if (GameConfig.TryGetArg("-tag", out string tg)) _tag = tg;
            Directory.CreateDirectory(_outDir);
            _nextShot = _shotEvery > 0f ? 4f : float.MaxValue;
            _bot = GameConfig.HasFlag("-bot");
            _simKeys = GameConfig.HasFlag("-simkeys");
            _simJoystick = GameConfig.HasFlag("-simjoystick");
            Debug.Log($"[TEST] driver active tag={_tag} quitAfter={_quitAfter} bot={_bot} simkeys={_simKeys} simjoystick={_simJoystick}");
        }

        void Update()
        {
            _time += Time.unscaledDeltaTime;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null) return;

            // solo practice: the host starts the match itself after a short pause
            if (!_soloStarted && GameConfig.HasFlag("-solo") && nm.IsServer && MatchManager.Instance != null
                && MatchManager.Instance.Phase.Value == MatchPhase.Lobby && _time > 3f)
            {
                _soloStarted = true;
                Debug.Log("[TEST] host starts solo practice");
                MatchManager.Instance.ServerBeginCountdown();
            }

            // test of the result screen's BACK button: press it (through the same code) 2.5 s after the match is finished
            if (GameConfig.HasFlag("-backAfterFinish") && MatchManager.Instance != null && MatchManager.Instance.Phase.Value == MatchPhase.Finished)
            {
                if (_finishedAt < 0f) _finishedAt = _time;
                else if (!_backPressed && _time - _finishedAt > 2.5f && ConnectionMenu.Instance != null)
                {
                    _backPressed = true;
                    Debug.Log("[TEST] pressing BACK on the result screen");
                    ConnectionMenu.Instance.LeaveToMenu();
                }
            }

            // test of PAUSE: pause 3 s into the match, check that the match clock stands still, resume at 7 s; -surrenderTest then surrenders at 9 s
            if (GameConfig.HasFlag("-pauseTest") && MatchManager.Instance != null && MatchManager.Instance.Phase.Value == MatchPhase.Playing)
            {
                if (_playingAt < 0f) _playingAt = _time;
                float t = _time - _playingAt;
                MatchManager mm = MatchManager.Instance;
                if (!_pauseOpened && t > 3f)
                {
                    _pauseOpened = true;
                    _pausedRemaining = mm.TimeRemaining;
                    GameHud.SetPaused(true);
                    Debug.Log($"[TEST] pause requested, match time left {_pausedRemaining:0.0}");
                }
                else if (_pauseOpened && !_pauseLogged && t > 5f)
                {
                    _pauseLogged = true;
                    Debug.Log($"[TEST] 2 s after PAUSE: Paused={GameHud.Paused} byMe={GameHud.PausedByMe} match time left {mm.TimeRemaining:0.0} (was {_pausedRemaining:0.0}) enemies={Enemy.All.Count}");
                }
                else if (_pauseLogged && !_quitPressed && t > 7f)
                {
                    _quitPressed = true;
                    GameHud.SetPaused(false);
                    Debug.Log("[TEST] resume requested");
                }
                else if (_quitPressed && !_surrendered && t > 9.5f)
                {
                    Debug.Log($"[TEST] after RESUME: Paused={GameHud.Paused} match time left {mm.TimeRemaining:0.0}");
                    _surrendered = true;
                    if (GameConfig.HasFlag("-surrenderTest")) { Debug.Log("[TEST] surrendering"); mm.RequestSurrender(); }
                }
            }

            // test of the BACK button in the waiting lobby
            if (GameConfig.HasFlag("-lobbyBack") && !_lobbyBackPressed && _time > 6f && MatchManager.Instance != null && MatchManager.Instance.Phase.Value == MatchPhase.Lobby)
            {
                _lobbyBackPressed = true;
                Debug.Log("[TEST] pressing BACK in the lobby");
                GameHud.LeaveToMenu();
            }

            if (_bot) Drive();
            if (_simKeys) SimulateKeys();
            if (_simJoystick) SimulateJoystick();

            if (_time >= _nextLog)
            {
                _nextLog = _time + (_simKeys || _simJoystick ? 1f : 2f);
                LogStatus(nm);
            }

            if (_time >= _nextShot)
            {
                _nextShot = _time + _shotEvery;
                string path = Path.Combine(_outDir, $"{_tag}_{_shots++:00}.png");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log("[TEST] screenshot " + path);
            }

            if (_time >= _quitAfter)
            {
                LogStatus(nm);
                Debug.Log("[TEST] done, quitting");
                if (nm.IsListening) nm.Shutdown();
                Application.Quit();
            }
        }

        /// <summary>
        /// Presses keys the way a human would, but through the Input System instead of the OS keyboard.
        /// Timeline (seconds after the match starts): D, W, A, S held 2 s each, then D+W, then Space taps.
        /// </summary>
        void SimulateKeys()
        {
            Keyboard kb = Keyboard.current;
            MatchManager mm = MatchManager.Instance;
            if (kb == null || mm == null || mm.Phase.Value != MatchPhase.Playing) return;

            if (_playStart < 0f) _playStart = _time;
            float t = _time - _playStart;

            var state = new KeyboardState();
            string label = "none";
            if (t >= 1f && t < 3f) { state.Set(Key.D, true); label = "D"; }
            else if (t >= 3f && t < 5f) { state.Set(Key.W, true); label = "W"; }
            else if (t >= 5f && t < 7f) { state.Set(Key.A, true); label = "A"; }
            else if (t >= 7f && t < 9f) { state.Set(Key.S, true); label = "S"; }
            else if (t >= 9f && t < 11f) { state.Set(Key.D, true); state.Set(Key.W, true); label = "D+W"; }
            else if (t >= 11f)
            {
                // tap Space for 0.1 s every 0.5 s
                if ((t - 11f) % 0.5f < 0.1f) { state.Set(Key.Space, true); label = "Space"; }
            }

            InputSystem.QueueStateEvent(kb, state);
            string key = label == "Space" ? "Space-taps" : label;
            if (key != _lastKeysLabel)
            {
                _lastKeysLabel = key;
                Debug.Log($"[TEST] simkeys t+{t:0.0}: keys held = {key}");
            }
        }

        /// <summary>
        /// Uses the on-screen controls like a finger would, but through a simulated pointer:
        /// press on the left side and drag right, then up, release, then press and release the ATTACK button.
        /// </summary>
        void SimulateJoystick()
        {
            Mouse mouse = Mouse.current;
            VirtualControls vc = VirtualControls.Instance;
            MatchManager mm = MatchManager.Instance;
            if (mouse == null || vc == null || mm == null || mm.Phase.Value != MatchPhase.Playing || !VirtualControls.Visible) return;

            if (_playStart < 0f) _playStart = _time;
            float t = _time - _playStart;

            Vector2 start = vc.StickStartPixel;
            float drag = vc.StickRadiusPixels * 1.4f;
            bool down = false;
            Vector2 position = start;
            string label = "released";

            if (t >= 1f && t < 1.1f) { down = true; position = start; label = "press stick"; }
            else if (t >= 1.1f && t < 3f) { down = true; position = start + new Vector2(drag, 0f); label = "drag RIGHT"; }
            else if (t >= 3.3f && t < 3.4f) { down = true; position = start; label = "press stick"; }
            else if (t >= 3.4f && t < 5f) { down = true; position = start + new Vector2(0f, drag); label = "drag UP"; }
            else if (t >= 5.6f && t < 5.7f) { down = true; position = start; label = "press stick"; }
            else if (t >= 5.7f && t < 7f) { down = true; position = start + new Vector2(-drag * 0.35f, 0f); label = "drag LEFT (small push)"; }
            else if (t >= 8f && (t - 8f) % 0.6f < 0.25f) { down = true; position = vc.AttackButtonPixel; label = "hold ATTACK button"; }

            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left, down));
            if (label != _lastStickLabel)
            {
                _lastStickLabel = label;
                Debug.Log($"[TEST] simjoystick t+{t:0.0}: {label}  (Move=({VirtualControls.Move.x:0.00},{VirtualControls.Move.y:0.00}) attackHeld={VirtualControls.AttackHeld})");
            }
        }

        /// <summary>Bot: run to the closest enemy and keep swinging; otherwise wander to the centre.</summary>
        void Drive()
        {
            PlayerStats me = PlayerStats.Local;
            PlayerController.TestPlaneMove = null;
            PlayerCombat.TestAttack = false;
            if (me == null || me.Controller == null || me.IsDead.Value) return;

            Vector2 pos = me.Controller.PlanePosition;
            Enemy nearest = null;
            float best = float.MaxValue;
            foreach (Enemy e in Enemy.All)
            {
                float d = (e.PlanePosition - pos).sqrMagnitude;
                if (d < best) { best = d; nearest = e; }
            }

            if (nearest != null)
            {
                Vector2 to = nearest.PlanePosition - pos;
                float dist = to.magnitude;
                PlayerController.TestPlaneMove = dist > 1.0f ? to / dist : Vector2.zero;
                PlayerCombat.TestAttack = dist < 2.0f;
                if (dist <= 1.0f) PlayerController.TestPlaneMove = to / Mathf.Max(0.01f, dist) * 0.12f;  // keep facing it
            }
            else
            {
                Vector2 centre = Vector2.one * GameConfig.ArenaSize * 0.5f;
                Vector2 to = centre - pos;
                PlayerController.TestPlaneMove = to.magnitude > 1f ? to.normalized * 0.6f : Vector2.zero;
            }
        }

        void LogStatus(NetworkManager nm)
        {
            var sb = new StringBuilder("[TEST] ");
            sb.Append($"t={_time:0.0} role={(nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client")} connected={nm.IsConnectedClient || nm.IsServer} ");
            MatchManager mm = MatchManager.Instance;
            if (mm != null)
                sb.Append($"phase={mm.Phase.Value} season={mm.SeasonIndex.Value} remaining={mm.TimeRemaining:0.0} ");
            else
                sb.Append("phase=none ");

            sb.Append($"players={PlayerStats.All.Count} enemies={Enemy.All.Count} ");

            SpacetimeManager db = SpacetimeManager.Instance;
            if (db != null && db.State != SpacetimeManager.ConnState.Disabled)
            {
                ProfileInfo me = db.LocalProfile;
                sb.Append($"db={db.State} queue={db.QueueCount} ");
                if (me != null) sb.Append($"me={me.Name}(W{me.Wins}/L{me.Losses}/D{me.Draws},{me.Score}pts) ");
                if (db.CurrentPairing != null) sb.Append($"pairing={db.CurrentPairing.Code}(host={db.CurrentPairing.IAmHost},vs={db.CurrentPairing.OpponentName}) ");
            }
            OnlineMatchmaker om = OnlineMatchmaker.Instance;
            if (om != null && om.Current != OnlineMatchmaker.State.Idle) sb.Append($"online={om.Current}('{om.Stage}') ");
            foreach (PlayerStats p in PlayerStats.All)
            {
                Vector2 pos = p.Controller != null ? p.Controller.PlanePosition : Vector2.zero;
                sb.Append($"| slot{p.Slot.Value}{(p.IsOwner ? "(me)" : "")} pos=({pos.x:0.00},{pos.y:0.00}) hp={p.Health.Value} score={p.Score.Value} kills={p.Kills.Value} deaths={p.Deaths.Value} dead={p.IsDead.Value} ");
            }
            Debug.Log(sb.ToString());
        }
    }
}

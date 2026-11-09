using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonRush
{
    /// <summary>
    /// On-screen controls: a floating joystick (touch or press anywhere in the left part of the screen
    /// and drag) and a big ATTACK button on the right (hold it to keep swinging).
    ///
    /// Works with fingers (multi-touch: move with one finger, attack with another) and with a mouse.
    /// The keyboard keeps working at the same time. The joystick direction is "screen aligned":
    /// pushing it up moves the warrior up the screen, exactly like the W key.
    /// </summary>
    public class VirtualControls : MonoBehaviour
    {
        public static VirtualControls Instance { get; private set; }

        /// <summary>Joystick direction on the screen: right = +x, up = +y, length 0..1 (0 inside the dead zone).</summary>
        public static Vector2 Move { get; private set; }
        /// <summary>True while a finger / the mouse is holding the attack button.</summary>
        public static bool AttackHeld { get; private set; }
        /// <summary>True while the on-screen controls are shown (in a match, after the warrior has spawned).</summary>
        public static bool Visible => Instance != null && Instance._visible;

        // sizes are in "virtual pixels" of a 1080 pixel high screen
        const float StickRadius = 120f;
        const float KnobRadius = 54f;
        const float AttackRadius = 100f;
        const float DeadZone = 0.18f;
        const int None = int.MinValue;

        struct Pointer
        {
            public int id;
            public Vector2 position;       // pixels, origin bottom-left (Input System convention)
            public bool down;
            public bool pressedThisFrame;
        }

        readonly List<Pointer> _pointers = new List<Pointer>();
        int _stickPointer = None;
        int _attackPointer = None;
        Vector2 _stickCentrePx;
        Vector2 _knob;                     // -1..1, y up
        bool _visible;
        Texture2D _disc, _ring;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Move = Vector2.zero;
            AttackHeld = false;
        }

        float Scale => Screen.height / 1080f;
        float VirtualWidth => Screen.width / Scale;

        // ---- layout helpers (virtual GUI coordinates: origin top-left, y down) -----------------

        // on phones the controls stay clear of rounded corners, notches and the gesture bar (zero on a PC)
        static Vector4 Ins => MobileSupport.SafeInsets();

        Vector2 AttackCentreGui => new Vector2(VirtualWidth - 190f - Ins.z, 860f - Ins.w);
        Vector2 IdleStickCentreGui => new Vector2(230f + Ins.x, 850f - Ins.w);

        bool InStickZone(Vector2 gui)
        {
            return gui.x < VirtualWidth * 0.45f && gui.y > 260f && gui.y < 1060f;
        }

        public Vector2 GuiToPixel(Vector2 gui) => new Vector2(gui.x * Scale, Screen.height - gui.y * Scale);
        public Vector2 PixelToGui(Vector2 px) => new Vector2(px.x / Scale, (Screen.height - px.y) / Scale);

        /// <summary>Where to press to start the joystick (used by the automated tests).</summary>
        public Vector2 StickStartPixel => GuiToPixel(new Vector2(300f, 700f));
        /// <summary>Where the attack button is (used by the automated tests).</summary>
        public Vector2 AttackButtonPixel => GuiToPixel(AttackCentreGui);
        public float StickRadiusPixels => StickRadius * Scale;

        // ---- input ------------------------------------------------------------------------------

        static bool InMatch()
        {
            NetworkManager nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening && (nm.IsServer || nm.IsConnectedClient) && PlayerStats.Local != null;
        }

        void Update()
        {
            _visible = GameConfig.ShowOnScreenControls && InMatch();
            if (!_visible)
            {
                ResetState();
                return;
            }

            GatherPointers();

            // forget pointers that were lifted
            bool stickAlive = false, attackAlive = false;
            foreach (Pointer p in _pointers)
            {
                if (!p.down) continue;
                if (p.id == _stickPointer) stickAlive = true;
                if (p.id == _attackPointer) attackAlive = true;
            }
            if (!stickAlive) { _stickPointer = None; _knob = Vector2.zero; }
            if (!attackAlive) _attackPointer = None;

            // new presses: the attack button wins if the press is on it, otherwise the left side starts the joystick
            foreach (Pointer p in _pointers)
            {
                if (!p.down || !p.pressedThisFrame) continue;
                Vector2 gui = PixelToGui(p.position);
                if (_attackPointer == None && Vector2.Distance(gui, AttackCentreGui) <= AttackRadius * 1.25f)
                {
                    _attackPointer = p.id;
                }
                else if (_stickPointer == None && InStickZone(gui))
                {
                    _stickPointer = p.id;
                    float margin = (StickRadius + 20f) * Scale;
                    _stickCentrePx = new Vector2(Mathf.Max(p.position.x, margin + Ins.x * Scale), Mathf.Clamp(p.position.y, margin + Ins.w * Scale, Screen.height - 260f * Scale));
                }
            }

            // follow the joystick finger
            if (_stickPointer != None)
            {
                foreach (Pointer p in _pointers)
                {
                    if (p.id != _stickPointer) continue;
                    Vector2 delta = (p.position - _stickCentrePx) / (StickRadius * Scale);
                    _knob = Vector2.ClampMagnitude(delta, 1f);
                }
            }

            float magnitude = _knob.magnitude;
            Move = magnitude < DeadZone ? Vector2.zero : _knob.normalized * Mathf.InverseLerp(DeadZone, 1f, magnitude);
            AttackHeld = _attackPointer != None;
        }

        void ResetState()
        {
            _stickPointer = None;
            _attackPointer = None;
            _knob = Vector2.zero;
            Move = Vector2.zero;
            AttackHeld = false;
        }

        void GatherPointers()
        {
            _pointers.Clear();

            Touchscreen touchscreen = Touchscreen.current;
            bool anyTouch = false;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.isInProgress) continue;
                    anyTouch = true;
                    _pointers.Add(new Pointer
                    {
                        id = touch.touchId.ReadValue(),
                        position = touch.position.ReadValue(),
                        down = true,
                        pressedThisFrame = touch.press.wasPressedThisFrame
                    });
                }
            }

            // a mouse is used when no finger is on the screen (touch screens can also emulate a mouse)
            Mouse mouse = Mouse.current;
            if (!anyTouch && mouse != null)
            {
                _pointers.Add(new Pointer
                {
                    id = -1,
                    position = mouse.position.ReadValue(),
                    down = mouse.leftButton.isPressed,
                    pressedThisFrame = mouse.leftButton.wasPressedThisFrame
                });
            }
        }

        // ---- drawing ----------------------------------------------------------------------------

        void OnGUI()
        {
            if (!_visible || Event.current.type != EventType.Repaint) return;
            EnsureTextures();

            float s = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            GUI.depth = -100;

            // joystick
            bool stickActive = _stickPointer != None;
            Vector2 centre = stickActive ? PixelToGui(_stickCentrePx) : IdleStickCentreGui;
            Disc(_ring, centre, StickRadius, new Color(1f, 1f, 1f, stickActive ? 0.55f : 0.28f));
            Vector2 knob = centre + new Vector2(_knob.x, -_knob.y) * StickRadius;
            Disc(_disc, knob, KnobRadius, new Color(1f, 1f, 1f, stickActive ? 0.7f : 0.32f));

            // attack button
            bool pressed = _attackPointer != None || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            Vector2 a = AttackCentreGui;
            Disc(_disc, a, AttackRadius, pressed ? new Color(1f, 0.55f, 0.35f, 0.85f) : new Color(1f, 0.35f, 0.25f, 0.45f));
            Disc(_ring, a, AttackRadius, new Color(1f, 1f, 1f, 0.6f));
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(1f, 1f, 1f, pressed ? 1f : 0.85f);
            GUI.Label(new Rect(a.x - AttackRadius, a.y - 24f, AttackRadius * 2f, 48f), "ATTACK", style);
        }

        static void Disc(Texture2D texture, Vector2 centre, float radius, Color colour)
        {
            GUI.DrawTexture(new Rect(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f),
                texture, ScaleMode.StretchToFill, true, 0f, colour, 0f, 0f);
        }

        void EnsureTextures()
        {
            if (_disc != null) return;
            _disc = MakeCircleTexture(filled: true);
            _ring = MakeCircleTexture(filled: false);
        }

        static Texture2D MakeCircleTexture(bool filled)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;       // 0 centre .. 1 edge
                    float alpha = filled
                        ? Mathf.Clamp01((1f - d) * 18f)                                     // soft edge
                        : Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) / 0.06f);                 // thin ring
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}

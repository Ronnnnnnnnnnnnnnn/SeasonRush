using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

        [Header("Canvas (made by Season Rush > Create UI Canvas In Scene)")]
        [Tooltip("Switched on while the controls are shown (in a match).")]
        public GameObject controlsRoot;
        public Image stickRing;
        public Image stickKnob;
        public Image attackDisc;
        public Image attackRing;
        public Text attackLabel;

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
            if (controlsRoot != null && controlsRoot.activeSelf != _visible) controlsRoot.SetActive(_visible);
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
            UpdateVisuals();
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

        // ---- drawing (the pictures are real objects of the UI canvas: Hierarchy > UI > Controls) -----------------

        void UpdateVisuals()
        {
            if (controlsRoot == null) return;

            // joystick
            bool stickActive = _stickPointer != None;
            Vector2 centre = stickActive ? PixelToGui(_stickCentrePx) : IdleStickCentreGui;
            Place(stickRing, centre, StickRadius, new Color(1f, 1f, 1f, stickActive ? 0.55f : 0.28f));
            Vector2 knob = centre + new Vector2(_knob.x, -_knob.y) * StickRadius;
            Place(stickKnob, knob, KnobRadius, new Color(1f, 1f, 1f, stickActive ? 0.7f : 0.32f));

            // attack button
            bool pressed = _attackPointer != None || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            Vector2 a = AttackCentreGui;
            Place(attackDisc, a, AttackRadius, pressed ? new Color(1f, 0.55f, 0.35f, 0.85f) : new Color(1f, 0.35f, 0.25f, 0.45f));
            Place(attackRing, a, AttackRadius, new Color(1f, 1f, 1f, 0.6f));
            if (attackLabel != null) attackLabel.color = new Color(1f, 1f, 1f, pressed ? 1f : 0.85f);
        }

        /// <summary>Puts a round picture at a position given in virtual GUI units (origin top-left, y down = canvas units).</summary>
        static void Place(Image image, Vector2 centre, float radius, Color colour)
        {
            if (image == null) return;
            RectTransform rt = image.rectTransform;
            rt.anchoredPosition = new Vector2(centre.x, -centre.y);
            rt.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            image.color = colour;
        }
    }
}

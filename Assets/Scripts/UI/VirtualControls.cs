using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SeasonRush
{
    public class VirtualControls : MonoBehaviour
    {
        public static VirtualControls Instance { get; private set; }

        public static Vector2 Move { get; private set; }
        public static bool AttackHeld { get; private set; }
        public static bool Visible => Instance != null && Instance._visible;

        const float StickRadius = 120f;
        const float KnobRadius = 54f;
        const float AttackRadius = 100f;
        const float DeadZone = 0.18f;
        const int None = int.MinValue;

        struct Pointer
        {
            public int id;
            public Vector2 position;
            public bool down;
            public bool pressedThisFrame;
        }

        readonly List<Pointer> _pointers = new List<Pointer>();
        int _stickPointer = None;
        int _attackPointer = None;
        Vector2 _stickCentrePx;
        Vector2 _knob;
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

        static Vector4 Ins => MobileSupport.SafeInsets();

        Vector2 AttackCentreGui => new Vector2(VirtualWidth - 190f - Ins.z, 860f - Ins.w);
        Vector2 IdleStickCentreGui => new Vector2(230f + Ins.x, 850f - Ins.w);

        bool InStickZone(Vector2 gui)
        {
            return gui.x < VirtualWidth * 0.45f && gui.y > 260f && gui.y < 1060f;
        }

        public Vector2 GuiToPixel(Vector2 gui) => new Vector2(gui.x * Scale, Screen.height - gui.y * Scale);
        public Vector2 PixelToGui(Vector2 px) => new Vector2(px.x / Scale, (Screen.height - px.y) / Scale);

        public Vector2 StickStartPixel => GuiToPixel(new Vector2(300f, 700f));
        public Vector2 AttackButtonPixel => GuiToPixel(AttackCentreGui);
        public float StickRadiusPixels => StickRadius * Scale;

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

            bool stickAlive = false, attackAlive = false;
            foreach (Pointer p in _pointers)
            {
                if (!p.down) continue;
                if (p.id == _stickPointer) stickAlive = true;
                if (p.id == _attackPointer) attackAlive = true;
            }
            if (!stickAlive) { _stickPointer = None; _knob = Vector2.zero; }
            if (!attackAlive) _attackPointer = None;

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

        void UpdateVisuals()
        {
            if (controlsRoot == null) return;

            bool stickActive = _stickPointer != None;
            Vector2 centre = stickActive ? PixelToGui(_stickCentrePx) : IdleStickCentreGui;
            Place(stickRing, centre, StickRadius, new Color(1f, 1f, 1f, stickActive ? 0.55f : 0.28f));
            Vector2 knob = centre + new Vector2(_knob.x, -_knob.y) * StickRadius;
            Place(stickKnob, knob, KnobRadius, new Color(1f, 1f, 1f, stickActive ? 0.7f : 0.32f));

            bool pressed = _attackPointer != None || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            Vector2 a = AttackCentreGui;
            Place(attackDisc, a, AttackRadius, pressed ? new Color(1f, 0.55f, 0.35f, 0.85f) : new Color(1f, 0.35f, 0.25f, 0.45f));
            Place(attackRing, a, AttackRadius, new Color(1f, 1f, 1f, 0.6f));
            if (attackLabel != null) attackLabel.color = new Color(1f, 1f, 1f, pressed ? 1f : 0.85f);
        }

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

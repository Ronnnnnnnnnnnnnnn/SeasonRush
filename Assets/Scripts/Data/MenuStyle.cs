using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// Everything you may want to change about the look of the start menu: the font, the button texts, the colours
    /// and the text sizes. The asset is Assets/Resources/MenuStyle.asset - select it and edit the values in the
    /// Inspector. The change shows the next time the menu is drawn (also while the game is running in the Editor).
    /// Delete the asset to get the default look back (it is created again automatically).
    /// The default look is pastel, to go with the pastel art of the island.
    /// </summary>
    [CreateAssetMenu(menuName = "Season Rush/Menu Style", fileName = "MenuStyle")]
    public class MenuStyle : ScriptableObject
    {
        [Header("Font")]
        [Tooltip("Drag a font file (.ttf / .otf) here to change the font of the whole menu. Empty = Unity's default font.")]
        public Font font;

        [Header("Texts")]
        public string gameTitle = "SEASON RUSH";
        public string tagline = "Two rivals. Three seasons. One score.";
        public string playText = "PLAY";
        public string playHint = "Find a random opponent online";
        public string profileText = "PROFILE";
        public string leaderboardText = "LEADERBOARD";
        public string localNetworkText = "LOCAL NETWORK  (host / join)";
        public string hostText = "HOST A MATCH";
        public string joinText = "JOIN MATCH";
        public string backText = "BACK";
        public string cancelText = "CANCEL";

        [Header("Button colours")]
        [Tooltip("Ticked: flat buttons in exactly the colour you pick. Unticked: Unity's grey gradient buttons with the colour as a tint on top.")]
        public bool solidButtons = true;
        [Tooltip("How much lighter a button gets while the mouse is over it (0 = not at all, 1 = white).")]
        [Range(0f, 0.6f)] public float hoverLighten = 0.12f;
        public Color playButtonColor = new Color(0.62f, 0.89f, 0.70f);
        public Color buttonColor = new Color(0.68f, 0.81f, 0.97f);
        public Color cancelButtonColor = new Color(0.98f, 0.72f, 0.72f);

        [Header("Panels and backdrop")]
        [Tooltip("The boxes that hold the buttons, the profile and the leaderboard.")]
        public Color panelColor = new Color(1f, 0.97f, 0.94f, 0.90f);
        [Tooltip("The input boxes (name, host IP, port).")]
        public Color fieldColor = new Color(1f, 1f, 1f, 0.95f);
        [Tooltip("A veil over the map behind the menu. Alpha = how strong (0 = the map is not covered).")]
        public Color backdropColor = new Color(1f, 0.97f, 0.95f, 0.30f);

        [Header("Text colours")]
        public Color titleColor = new Color(0.30f, 0.30f, 0.52f);
        public Color buttonTextColor = new Color(0.20f, 0.24f, 0.40f);
        public Color textColor = new Color(0.24f, 0.27f, 0.42f);
        [Tooltip("Small grey-ish help texts.")]
        public Color hintTextColor = new Color(0.24f, 0.27f, 0.42f, 0.70f);
        [Tooltip("Your own name and your row in the leaderboard.")]
        public Color highlightColor = new Color(0.85f, 0.42f, 0.15f);
        [Tooltip("\"connected\" messages.")]
        public Color goodColor = new Color(0.18f, 0.55f, 0.32f);
        [Tooltip("\"connecting...\" messages.")]
        public Color pendingColor = new Color(0.70f, 0.50f, 0.10f);
        [Tooltip("Errors and warnings.")]
        public Color warnColor = new Color(0.80f, 0.30f, 0.25f);

        [Header("Text sizes (the menu is laid out for a screen 1080 pixels high)")]
        public int titleSize = 64;
        public int playButtonSize = 44;
        public int buttonSize = 28;
        public int smallButtonSize = 22;
        public int labelSize = 22;

        /// <summary>Which set of default colours this asset was last updated to (used by the editor tool, not shown).</summary>
        [HideInInspector] public int defaultsVersion;
        public const int CurrentDefaultsVersion = 3;

        /// <summary>
        /// Earlier versions of the menu used other default colours (dark grey buttons on a dark backdrop). An asset made back
        /// then is moved to the pastel defaults, but only the colours that were never changed by hand.
        /// </summary>
        public void MigrateDefaults()
        {
            if (defaultsVersion >= CurrentDefaultsVersion) return;
            var fresh = CreateInstance<MenuStyle>();
            Color white = Color.white;
            Replace(ref playButtonColor, fresh.playButtonColor, new Color(0.4f, 1f, 0.55f), new Color(0.15f, 0.60f, 0.30f));
            Replace(ref buttonColor, fresh.buttonColor, white, new Color(0.27f, 0.31f, 0.40f));
            Replace(ref cancelButtonColor, fresh.cancelButtonColor, new Color(1f, 0.55f, 0.5f), new Color(0.78f, 0.30f, 0.28f));
            Replace(ref buttonTextColor, fresh.buttonTextColor, new Color(0.92f, 0.92f, 0.92f), white);
            Replace(ref titleColor, fresh.titleColor, white, white);
            Replace(ref textColor, fresh.textColor, white, white);
            Replace(ref backdropColor, fresh.backdropColor, new Color(0f, 0f, 0f, 0.55f), new Color(0f, 0f, 0f, 0.55f));
            if (Mathf.Approximately(hoverLighten, 0.15f)) hoverLighten = fresh.hoverLighten;
            DestroyImmediate(fresh);
            defaultsVersion = CurrentDefaultsVersion;
        }

        static void Replace(ref Color value, Color newDefault, Color oldDefaultA, Color oldDefaultB)
        {
            if (Same(value, oldDefaultA) || Same(value, oldDefaultB)) value = newDefault;
        }

        static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f && Mathf.Abs(a.a - b.a) < 0.002f;

        /// <summary>Goes up whenever a value is edited in the Inspector, so the open menu rebuilds its styles at once.</summary>
        public static int Version { get; private set; }

        void OnValidate() => Version++;

        static MenuStyle _loaded;
        static bool _triedLoading;

        /// <summary>The menu style asset, or a default one when the asset is missing.</summary>
        public static MenuStyle Current
        {
            get
            {
                if (_loaded == null)
                {
                    if (!_triedLoading)
                    {
                        _triedLoading = true;
                        _loaded = Resources.Load<MenuStyle>("MenuStyle");
                    }
                    if (_loaded == null) _loaded = CreateInstance<MenuStyle>();
                }
                return _loaded;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = null;
            _triedLoading = false;
            Version = 0;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace SeasonRush
{
    /// <summary>What an element of the menu canvas is, so it can get its pictures, colours, font and text from the MenuStyle asset.</summary>
    public enum UiRole
    {
        None,
        Backdrop,          // the veil over the map behind the menu
        Panel,             // the boxes that hold buttons / profile / leaderboard
        Field,             // input boxes (name, host IP, port)
        ButtonPrimary,     // PLAY, RESUME ...        (green button picture)
        ButtonNormal,      //                         (blue)
        ButtonCancel,      // CANCEL, QUIT ...        (orange)
        LabelTitle,        // the big game title
        LabelHeading,      // a panel title (YOUR PROFILE, LEADERBOARD ...)
        LabelText,         // normal text colour, size left as it is on the object
        LabelBody,         // normal text colour + the "label size" of the style
        LabelHint,         // small faded help text
        LabelHighlight,    // your own name
        LabelPlayButton,   // text on a button, sizes from the style
        LabelButton,
        LabelSmallButton,
        LabelField,        // text typed into an input box
        Divider,           // the wooden line between sections
        CheckBox,          // the box of a checkbox
        CheckMark,         // the tick of a checkbox
        LogoImage,         // the picture of the game's name (MenuStyle > Title Image); the plain title text shows when there is none
        HudBox,            // a box of the in-game screen (top bar, player boxes, lobby, pause): the panel picture, or a dark box without the skin
        HudInk,            // pixel-font text on a HudBox (dark brown with the skin, otherwise it keeps its own colour)
        HudInkBody,        // the same in the small font
        HudLight,          // pixel-font text straight over the map (keeps its own colour, has a shadow)
        HudLightBody
    }

    /// <summary>Which text of the MenuStyle asset an element shows (so the words of a button can be changed in one place).</summary>
    public enum UiText { None, GameTitle, Tagline, Play, PlayHint, Profile, Leaderboard, LocalNetwork, Host, Join, Back, Cancel }

    /// <summary>Colour / size of a button picture. Auto = from the role (Primary green, Normal blue, Cancel orange).</summary>
    public enum UiSkinColor { Auto, Spring, Autumn, Winter }

    public enum UiSkinSize { Large, Medium, Small }

    public enum UiPanelKind { Menu, Profile, Leaderboard, LocalNetwork }

    /// <summary>
    /// Put on an object of the UI canvas to make it follow the MenuStyle asset (Assets/Resources/MenuStyle.asset): pictures (wooden
    /// buttons, panels), colours, fonts, text sizes and button words. The ConnectionMenu applies every StyledElement again whenever the
    /// asset is edited. Turn "Use Menu Style" off on the ConnectionMenu to style the canvas by hand instead.
    /// </summary>
    [DisallowMultipleComponent]
    public class StyledElement : MonoBehaviour
    {
        public UiRole role;
        public UiText text;
        [Tooltip("Buttons: which colour of button picture. Auto = from the role.")] public UiSkinColor skinColor;
        [Tooltip("Buttons: Large, Medium or Small picture.")] public UiSkinSize skinSize = UiSkinSize.Medium;
        [Tooltip("Panels: which panel picture.")] public UiPanelKind panelKind;

        // what the object looked like before the skin was applied (so the flat look can come back when the skin is switched off)
        [SerializeField, HideInInspector] Sprite plainSprite;
        [SerializeField, HideInInspector] Font plainFont;
        [SerializeField, HideInInspector] int baseSize;
        [SerializeField, HideInInspector] Color plainColor = Color.white;
        [SerializeField, HideInInspector] bool remembered;

        public void Apply(MenuStyle m)
        {
            Remember();
            bool skin = m.SkinActive;
            switch (role)
            {
                case UiRole.Backdrop: SetPlain(m.backdropColor); break;
                case UiRole.Panel: if (skin) SetPicture(m.PanelSprite(panelKind)); else SetPlain(m.panelColor); break;
                case UiRole.Field: if (skin) SetPicture(m.inputField); else SetPlain(m.fieldColor); break;
                case UiRole.Divider: if (skin) SetPicture(m.divider); else SetPlain(m.Hint); break;
                case UiRole.CheckBox: if (skin) SetPicture(m.checkboxOff); else SetPlain(m.fieldColor); break;
                case UiRole.CheckMark: if (skin) SetPicture(m.checkboxOn); else SetPlain(m.Good); break;
                case UiRole.HudBox: if (skin) SetPicture(m.PanelSprite(panelKind)); else SetPlain(plainColor); break;
                case UiRole.HudInk: SetText(m, skin ? (Color?)m.skinTextColor : null, 0, false, true); break;
                case UiRole.HudInkBody: SetText(m, skin ? (Color?)m.skinHintColor : null, 0, false, false); break;
                case UiRole.HudLight: SetText(m, null, 0, false, true); break;
                case UiRole.HudLightBody: SetText(m, null, 0, false, false); break;
                case UiRole.ButtonPrimary: SetButton(m, UiSkinColor.Spring, m.playButtonColor); break;
                case UiRole.ButtonNormal: SetButton(m, UiSkinColor.Winter, m.buttonColor); break;
                case UiRole.ButtonCancel: SetButton(m, UiSkinColor.Autumn, m.cancelButtonColor); break;
                case UiRole.LabelTitle:
                    gameObject.SetActive(m.titleImage == null);                       // the picture replaces the plain text
                    SetText(m, skin ? m.skinTextColor : m.titleColor, m.titleSize, true, true);
                    break;
                case UiRole.LogoImage: SetLogo(m); break;
                case UiRole.LabelHeading: SetText(m, m.Text, 0, true, true); break;
                case UiRole.LabelText: SetText(m, m.Text, 0, false, false); break;
                case UiRole.LabelBody: SetText(m, m.Text, m.labelSize, false, false); break;
                case UiRole.LabelHint: SetText(m, m.Hint, 0, false, false); break;
                case UiRole.LabelHighlight: SetText(m, m.Highlight, 0, true, true); break;
                case UiRole.LabelPlayButton: SetText(m, m.ButtonText, m.playButtonSize, true, true); break;
                case UiRole.LabelButton: SetText(m, m.ButtonText, m.buttonSize, true, true); break;
                case UiRole.LabelSmallButton: SetText(m, m.ButtonText, m.smallButtonSize, false, true); break;
                case UiRole.LabelField: SetText(m, m.Text, 0, false, false); break;
            }

            if (text != UiText.None)
            {
                var label = GetComponent<Text>();
                if (label != null) label.text = Words(m, text);
            }
        }

        static string Words(MenuStyle m, UiText which)
        {
            switch (which)
            {
                case UiText.GameTitle: return m.gameTitle;
                case UiText.Tagline: return m.tagline;
                case UiText.Play: return m.playText;
                case UiText.PlayHint: return m.playHint;
                case UiText.Profile: return m.profileText;
                case UiText.Leaderboard: return m.leaderboardText;
                case UiText.LocalNetwork: return m.localNetworkText;
                case UiText.Host: return m.hostText;
                case UiText.Join: return m.joinText;
                case UiText.Back: return m.backText;
                case UiText.Cancel: return m.cancelText;
                default: return "";
            }
        }

        void Remember()
        {
            if (remembered) return;
            remembered = true;
            var image = GetComponent<Image>();
            if (image != null) { plainSprite = image.sprite; plainColor = image.color; }
            var label = GetComponent<Text>();
            if (label != null) { plainFont = label.font; baseSize = label.fontSize; }
        }

        // ---- pictures

        /// <summary>A picture with borders (9-slice): the corners keep their size, the middle stretches.</summary>
        void SetPicture(Sprite sprite)
        {
            var image = GetComponent<Image>();
            if (image == null) return;
            if (sprite == null) { SetPlain(Color.white); return; }
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Color.white;
        }

        void SetLogo(MenuStyle m)
        {
            var image = GetComponent<Image>();
            if (image == null) return;
            image.sprite = m.titleImage;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            gameObject.SetActive(m.titleImage != null);
        }

        void SetPlain(Color colour)
        {
            var image = GetComponent<Image>();
            if (image == null) return;
            image.sprite = plainSprite;
            image.type = plainSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = colour;
        }

        void SetButton(MenuStyle m, UiSkinColor autoColour, Color flatColour)
        {
            var image = GetComponent<Image>();
            var button = GetComponent<Button>();
            if (image == null) return;

            ColorBlock block = button != null ? button.colors : ColorBlock.defaultColorBlock;
            if (m.SkinActive)
            {
                UiSkinColor colour = skinColor == UiSkinColor.Auto ? autoColour : skinColor;
                SetPicture(m.ButtonSprite(colour, skinSize));
                block.normalColor = new Color(0.94f, 0.94f, 0.94f);                 // the picture is already coloured: only a light tint change
                block.selectedColor = block.normalColor;
                block.highlightedColor = Color.white;                                // lighter while the mouse is over it
                block.pressedColor = new Color(0.78f, 0.78f, 0.78f);
                block.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.75f);
            }
            else
            {
                SetPlain(Color.white);                                               // the tint below is multiplied with this
                block.normalColor = flatColour;
                block.selectedColor = flatColour;
                block.highlightedColor = Color.Lerp(flatColour, Color.white, m.hoverLighten);
                block.pressedColor = Color.Lerp(flatColour, Color.black, 0.12f);
                block.disabledColor = new Color(flatColour.r, flatColour.g, flatColour.b, 0.5f);
            }
            block.colorMultiplier = 1f;
            block.fadeDuration = 0.06f;
            if (button != null) button.colors = block;
        }

        // ---- text

        void SetText(MenuStyle m, Color? colour, int size, bool bold, bool heading)
        {
            var label = GetComponent<Text>();
            if (label == null) return;

            bool skin = m.SkinActive;
            Font skinFont = skin ? (heading ? m.headingFont : m.bodyFont) : null;
            Font font = skinFont != null ? skinFont : (m.font != null ? m.font : plainFont);
            if (font != null) label.font = font;

            float scale = skinFont != null ? (heading ? m.headingFontScale : m.bodyFontScale) : 1f;
            int basis = size > 0 ? size : (baseSize > 0 ? baseSize : label.fontSize);
            label.fontSize = Mathf.Max(1, Mathf.RoundToInt(basis * scale));
            label.fontStyle = bold && skinFont == null ? FontStyle.Bold : FontStyle.Normal;      // a pixel font is never "bolded" (it would smear)
            if (colour.HasValue) label.color = colour.Value;
        }
    }
}

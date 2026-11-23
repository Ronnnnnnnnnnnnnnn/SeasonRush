using UnityEngine;
using UnityEngine.UI;

namespace SeasonRush
{
    public enum UiRole
    {
        None,
        Backdrop,
        Panel,
        Field,
        ButtonPrimary,
        ButtonNormal,
        ButtonCancel,
        LabelTitle,
        LabelHeading,
        LabelText,
        LabelBody,
        LabelHint,
        LabelHighlight,
        LabelPlayButton,
        LabelButton,
        LabelSmallButton,
        LabelField,
        Divider,
        CheckBox,
        CheckMark,
        LogoImage,
        HudBox,
        HudInk,
        HudInkBody,
        HudLight,
        HudLightBody
    }

    public enum UiText { None, GameTitle, Tagline, Play, PlayHint, Profile, Leaderboard, LocalNetwork, Host, Join, Back, Cancel }

    public enum UiSkinColor { Auto, Spring, Autumn, Winter }

    public enum UiSkinSize { Large, Medium, Small }

    public enum UiPanelKind { Menu, Profile, Leaderboard, LocalNetwork }

    [DisallowMultipleComponent]
    public class StyledElement : MonoBehaviour
    {
        public UiRole role;
        public UiText text;
        [Tooltip("Buttons: which colour of button picture. Auto = from the role.")] public UiSkinColor skinColor;
        [Tooltip("Buttons: Large, Medium or Small picture.")] public UiSkinSize skinSize = UiSkinSize.Medium;
        [Tooltip("Panels: which panel picture.")] public UiPanelKind panelKind;

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
                    gameObject.SetActive(m.titleImage == null);
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
                block.normalColor = new Color(0.94f, 0.94f, 0.94f);
                block.selectedColor = block.normalColor;
                block.highlightedColor = Color.white;
                block.pressedColor = new Color(0.78f, 0.78f, 0.78f);
                block.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.75f);
            }
            else
            {
                SetPlain(Color.white);
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
            label.fontStyle = bold && skinFont == null ? FontStyle.Bold : FontStyle.Normal;
            if (colour.HasValue) label.color = colour.Value;
        }
    }
}

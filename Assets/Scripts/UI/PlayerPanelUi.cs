using UnityEngine;
using UnityEngine.UI;

namespace SeasonRush
{
    /// <summary>One of the two player boxes at the top of the screen (name, score, health bar, kills). Part of the HUD canvas.</summary>
    public class PlayerPanelUi : MonoBehaviour
    {
        public Image strip;                 // the coloured edge
        public Text who;
        public Text score;
        public Text waiting;                // "waiting for player..."
        public GameObject details;          // score + health bar + kills (hidden while nobody is in the slot)
        public RectTransform healthFill;    // stretched by its anchors
        public Image healthFillImage;
        public Text kills;
    }
}

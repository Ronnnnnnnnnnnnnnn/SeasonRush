using UnityEngine;
using UnityEngine.UI;

namespace SeasonRush
{
    /// <summary>A health bar that follows something in the world (an enemy or a warrior). Template + pool, see GameHud.</summary>
    public class WorldBarUi : MonoBehaviour
    {
        public RectTransform fill;
        public Image fillImage;
    }
}

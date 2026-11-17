using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// Put on the root of a map that was baked into the scene (Environment > Maps > Map_Spring ...).
    /// It remembers which season it is and a fingerprint of the theme it was made from, so the game can tell when the theme was
    /// edited afterwards and the map must be rebuilt (Season Rush > Update Maps In Scene).
    /// </summary>
    public class BakedMap : MonoBehaviour
    {
        public int season;
        public int themeHash;
    }
}

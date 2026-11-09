using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// Isometric (2:1) projection helpers.
    ///
    /// All gameplay (movement, collision, attacks, enemy AI, networking) runs on a flat "plane"
    /// where the arena is the square [0, ArenaSize] x [0, ArenaSize]. Only the *drawing* is projected
    /// onto the screen as an isometric diamond. This keeps the physics simple (circles stay circles)
    /// while the player sees the isometric island from the concept art.
    ///
    ///   plane (0,0)  -> south (bottom) corner of the diamond
    ///   plane (N,0)  -> east  (right)  corner
    ///   plane (0,N)  -> west  (left)   corner
    ///   plane (N,N)  -> north (top)    corner
    /// </summary>
    public static class Iso
    {
        /// <summary>Screen units moved sideways per plane unit along a diagonal.</summary>
        public const float HalfW = 0.8f;
        /// <summary>Screen units moved upward per plane unit along a diagonal (2:1 ratio).</summary>
        public const float HalfH = 0.4f;

        public static Vector2 ToScreen(Vector2 plane)
        {
            return new Vector2((plane.x - plane.y) * HalfW, (plane.x + plane.y) * HalfH);
        }

        public static Vector3 ToScreen3(Vector2 plane, float z = 0f)
        {
            Vector2 s = ToScreen(plane);
            return new Vector3(s.x, s.y, z);
        }

        public static Vector2 ToPlane(Vector2 screen)
        {
            float a = screen.x / HalfW;   // x - y
            float b = screen.y / HalfH;   // x + y
            return new Vector2((a + b) * 0.5f, (b - a) * 0.5f);
        }

        /// <summary>
        /// Maps a screen-aligned input direction (right = +x, up = +y) to a plane direction,
        /// so pressing "up" moves the character up the screen along the isometric diagonal.
        /// </summary>
        public static Vector2 InputToPlane(Vector2 screenInput)
        {
            Vector2 p = new Vector2(screenInput.x + screenInput.y, screenInput.y - screenInput.x);
            return p.sqrMagnitude > 0.0001f ? p.normalized : Vector2.zero;
        }

        /// <summary>
        /// Draw order. Objects further "back" (larger x+y on the plane) are drawn first.
        /// Ground meshes use a fixed very low order so everything else sits on top of them.
        /// </summary>
        public static int SortOrder(Vector2 plane, int bias = 0)
        {
            return -Mathf.RoundToInt((plane.x + plane.y) * 100f) + bias;
        }

        public const int GroundSortOrder = -20000;
    }
}

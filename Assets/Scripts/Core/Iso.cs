using UnityEngine;

namespace SeasonRush
{
    public static class Iso
    {
        public const float HalfW = 0.8f;
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

        public static Vector2 InputToPlane(Vector2 screenInput)
        {
            Vector2 p = new Vector2(screenInput.x + screenInput.y, screenInput.y - screenInput.x);
            return p.sqrMagnitude > 0.0001f ? p.normalized : Vector2.zero;
        }

        public static int SortOrder(Vector2 plane, int bias = 0)
        {
            return -Mathf.RoundToInt((plane.x + plane.y) * 100f) + bias;
        }

        public const int GroundSortOrder = -20000;
    }
}

using UnityEngine;

namespace SeasonRush
{
    /// <summary>Short-lived crescent that shows the warrior's swing. Created from code, no prefab needed.</summary>
    public class SlashFx : MonoBehaviour
    {
        static Sprite _arc;
        float _age;
        const float Life = 0.18f;
        SpriteRenderer _sr;
        Color _colour;
        Vector3 _baseScale;

        public static void Spawn(Vector2 planeOrigin, Vector2 planeDirection, Color tint)
        {
            if (planeDirection.sqrMagnitude < 0.01f) planeDirection = Vector2.right;
            planeDirection.Normalize();

            Vector2 spot = planeOrigin + planeDirection * 0.9f;
            var go = new GameObject("SlashFx");
            go.transform.position = Iso.ToScreen3(spot + Vector2.zero) + new Vector3(0f, 0.55f, 0f);

            Vector2 screenDir = Iso.ToScreen(planeDirection);
            float angle = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var fx = go.AddComponent<SlashFx>();
            fx._sr = go.AddComponent<SpriteRenderer>();
            fx._sr.sprite = GetArc();
            fx._sr.sortingOrder = Iso.SortOrder(spot, 50);
            fx._colour = Color.Lerp(Color.white, tint, 0.5f);
            fx._baseScale = new Vector3(1.1f, 0.8f, 1f);
            go.transform.localScale = fx._baseScale;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Life);
            _sr.color = new Color(_colour.r, _colour.g, _colour.b, 1f - t);
            transform.localScale = _baseScale * (1f + t * 0.35f);
            if (_age >= Life) Destroy(gameObject);
        }

        static Sprite GetArc()
        {
            if (_arc != null) return _arc;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);       // 0 = facing right
                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(r - 22f) / 8f);
                    float arc = 1f - Mathf.Clamp01((ang - 45f) / 25f);
                    byte a = (byte)(255f * Mathf.Clamp01(ring * arc * 1.4f));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _arc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 36f);
            _arc.name = "SlashArc";
            return _arc;
        }
    }
}

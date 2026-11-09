using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// Generates simple, clearly-temporary pixel art for everything the art pack does not have yet:
    /// the warrior, the three season monsters and the Winter ground / pine tree. Files are only
    /// created when they do not exist, so artists can drop in real sprites with the same file
    /// names (or just reassign the sprites in the Season Theme assets / prefabs) without anything
    /// being overwritten.
    /// </summary>
    public static class PlaceholderArt
    {
        public const string Dir = "Assets/2D Asset/Placeholders";

        class Canvas
        {
            public readonly int W, H;
            public readonly Color32[] Px;
            public Canvas(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }

            public void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) Px[y * W + x] = c; }

            public void Rect(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) Set(x, y, c);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
            {
                for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                    for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                    {
                        float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                        if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                    }
            }

            public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color32 col)
            {
                int minX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), maxX = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                int minY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), maxY = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
                        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                        if (!(neg && pos)) Set(x, y, col);
                    }
            }

            static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

            /// <summary>Adds a 1 pixel outline around everything that is already drawn.</summary>
            public void Outline(Color32 colour)
            {
                var copy = (Color32[])Px.Clone();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (copy[y * W + x].a != 0) continue;
                        if (Opaque(copy, x - 1, y) || Opaque(copy, x + 1, y) || Opaque(copy, x, y - 1) || Opaque(copy, x, y + 1))
                            Px[y * W + x] = colour;
                    }
            }

            bool Opaque(Color32[] src, int x, int y) => x >= 0 && y >= 0 && x < W && y < H && src[y * W + x].a != 0;

            public void Save(string assetPath)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.SetPixels32(Px);
                tex.Apply();
                string full = Path.GetFullPath(assetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        }

        static Color32 C(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);

        static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        public static void EnsureAll()
        {
            Directory.CreateDirectory(Path.GetFullPath(Dir));
            Make("warrior_body.png", DrawWarriorBody);
            Make("warrior_tunic.png", DrawWarriorTunic);
            Make("enemy_cloud.png", DrawCloud);
            Make("enemy_treemonster.png", DrawTreeMonster);
            Make("enemy_snowmonster.png", DrawSnowMonster);
            Make("pine_winter.png", DrawPine);
            Make("center_snow_winter.png", DrawSnowGround);
            Make("side_snow_winter.png", DrawSnowSide);
            Make("edge_winter.png", DrawIceCliff);
            Make("ice_patch_winter.png", DrawIcePatch);
            AssetDatabase.Refresh();
        }

        static void Make(string file, System.Func<Canvas> draw)
        {
            string path = Dir + "/" + file;
            if (File.Exists(Path.GetFullPath(path))) return;
            draw().Save(path);
            Debug.Log("[Setup] generated placeholder art: " + path);
        }

        // ----------------------------------------------------------------- characters (64 x 64)

        static Canvas DrawWarriorBody()
        {
            var c = new Canvas(64, 64);
            Color32 steel = C(176, 184, 200), dark = C(74, 64, 56), skin = C(240, 200, 160), leather = C(110, 74, 48),
                    blade = C(232, 238, 245), gold = C(214, 170, 50), eye = C(30, 30, 40);
            c.Rect(23, 2, 31, 7, dark); c.Rect(33, 2, 41, 7, dark);              // boots
            c.Rect(24, 7, 30, 17, leather); c.Rect(34, 7, 40, 17, leather);     // legs
            c.Rect(21, 17, 43, 38, steel);                                       // armour
            c.Rect(21, 17, 43, 20, leather);                                     // belt
            c.Rect(15, 22, 21, 36, steel); c.Rect(43, 22, 49, 36, steel);       // arms
            c.Rect(15, 19, 21, 23, skin); c.Rect(43, 19, 49, 23, skin);         // hands
            c.Ellipse(32, 45, 9, 9, skin);                                       // head
            c.Ellipse(32, 50, 10.5f, 8, steel);                                  // helmet
            c.Rect(22, 46, 42, 49, dark);                                        // visor band
            c.Rect(27, 43, 30, 46, eye); c.Rect(35, 43, 38, 46, eye);            // eyes
            c.Rect(51, 20, 54, 52, blade); c.Rect(52, 52, 53, 56, blade);        // sword blade
            c.Rect(48, 22, 57, 25, gold); c.Rect(51, 16, 54, 22, leather);       // guard + grip
            c.Outline(C(22, 20, 30));
            return c;
        }

        static Canvas DrawWarriorTunic()
        {
            // pure white so it can be tinted blue / red at runtime ("one warrior, two colours")
            var c = new Canvas(64, 64);
            Color32 white = C(255, 255, 255);
            c.Rect(26, 20, 38, 37, white);                 // tabard
            c.Ellipse(18, 37, 5, 3, white);                // shoulder pads
            c.Ellipse(46, 37, 5, 3, white);
            c.Rect(31, 49, 33, 56, white);                 // helmet plume
            c.Ellipse(32, 56, 4, 3, white);
            return c;
        }

        static Canvas DrawCloud()
        {
            var c = new Canvas(64, 64);
            Color32 cloud = C(244, 247, 255), shade = C(196, 208, 232), ink = C(40, 40, 80);
            c.Ellipse(20, 26, 11, 9, cloud); c.Ellipse(32, 33, 15, 12, cloud); c.Ellipse(45, 26, 11, 9, cloud);
            c.Rect(14, 15, 50, 27, cloud);
            c.Ellipse(32, 19, 17, 5, shade);
            c.Rect(24, 28, 29, 34, ink); c.Rect(36, 28, 41, 34, ink);                       // eyes
            c.Rect(22, 35, 30, 37, ink); c.Rect(35, 36, 43, 38, ink);                       // angry brows
            c.Rect(27, 20, 38, 23, C(70, 40, 90));                                          // mouth
            c.Outline(C(78, 90, 140));
            return c;
        }

        static Canvas DrawTreeMonster()
        {
            var c = new Canvas(64, 64);
            Color32 trunk = C(122, 78, 45), bark = C(86, 52, 30), leaf = C(217, 130, 43), leaf2 = C(240, 168, 70), glow = C(255, 232, 90);
            c.Rect(18, 4, 27, 10, trunk); c.Rect(37, 4, 46, 10, trunk);                     // root feet
            c.Rect(22, 8, 42, 40, trunk);                                                    // trunk
            c.Rect(26, 12, 28, 34, bark); c.Rect(36, 16, 38, 36, bark);
            c.Rect(11, 26, 23, 30, trunk); c.Rect(41, 26, 53, 30, trunk);                    // arms
            c.Rect(9, 30, 12, 37, trunk); c.Rect(52, 30, 55, 37, trunk);
            c.Ellipse(32, 47, 19, 12, leaf); c.Ellipse(22, 45, 9, 7, leaf2); c.Ellipse(43, 45, 9, 7, leaf2);
            c.Rect(26, 26, 31, 32, glow); c.Rect(34, 26, 39, 32, glow);                     // glowing eyes
            c.Rect(28, 17, 37, 21, C(40, 20, 10));                                          // mouth
            c.Outline(C(45, 28, 18));
            return c;
        }

        static Canvas DrawSnowMonster()
        {
            var c = new Canvas(64, 64);
            Color32 white = C(244, 250, 255), shade = C(190, 210, 234), coal = C(30, 34, 46), stick = C(110, 74, 48), carrot = C(255, 140, 30);
            c.Ellipse(32, 16, 17, 14, white); c.Ellipse(32, 35, 12, 11, white); c.Ellipse(32, 51, 9, 8, white);
            c.Ellipse(38, 11, 8, 6, shade);
            c.Rect(8, 34, 22, 37, stick); c.Rect(42, 34, 56, 37, stick);                    // stick arms
            c.Rect(28, 51, 31, 55, coal); c.Rect(34, 51, 37, 55, coal);                     // eyes
            c.Rect(27, 56, 31, 57, coal); c.Rect(34, 56, 38, 57, coal);                     // brows
            c.Rect(32, 47, 41, 50, carrot);                                                  // nose
            c.Rect(31, 38, 34, 41, coal); c.Rect(31, 31, 34, 34, coal);                     // buttons
            c.Rect(28, 45, 36, 46, coal);                                                    // grumpy mouth
            c.Outline(C(90, 120, 170));
            return c;
        }

        // ----------------------------------------------------------------- winter environment

        static Canvas DrawPine()
        {
            var c = new Canvas(96, 128);
            Color32 trunk = C(110, 72, 44), g1 = C(30, 100, 70), g2 = C(38, 122, 86), g3 = C(50, 146, 100), snow = C(240, 248, 255);
            c.Rect(44, 0, 52, 18, trunk);
            c.Triangle(new Vector2(10, 14), new Vector2(86, 14), new Vector2(48, 60), g1);
            c.Triangle(new Vector2(20, 42), new Vector2(76, 42), new Vector2(48, 88), g2);
            c.Triangle(new Vector2(28, 70), new Vector2(68, 70), new Vector2(48, 122), g3);
            c.Triangle(new Vector2(30, 40), new Vector2(66, 40), new Vector2(48, 60), snow);
            c.Triangle(new Vector2(34, 68), new Vector2(62, 68), new Vector2(48, 88), snow);
            c.Triangle(new Vector2(38, 98), new Vector2(58, 98), new Vector2(48, 122), snow);
            c.Outline(C(20, 50, 40));
            return c;
        }

        static Canvas DrawSnowGround()
        {
            var c = new Canvas(128, 128);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    uint h = Hash(x / 2, y / 2, 3) % 100;
                    Color32 col = C(232, 243, 255);
                    if (h < 8) col = C(212, 229, 248);
                    else if (h > 97) col = C(255, 255, 255);
                    c.Set(x, y, col);
                }
            return c;
        }

        static Canvas DrawSnowSide()
        {
            Canvas c = DrawSnowGround();
            for (int y = 0; y < 128; y++)
            {
                int cut = 2 + (int)(Hash(0, y / 2, 9) % 6);      // ragged right edge
                for (int x = 128 - cut; x < 128; x++) c.Set(x, y, C(0, 0, 0, 0));
            }
            return c;
        }

        static Canvas DrawIceCliff()
        {
            var c = new Canvas(128, 128);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    int edge = 112 + Mathf.RoundToInt(4f * Mathf.Sin(x * 0.35f) + 3f * Mathf.Sin(x * 0.9f));   // texture y grows upward
                    uint h = Hash(x / 2, y / 2, 5) % 100;
                    Color32 col;
                    if (y >= edge) col = y == edge ? C(190, 208, 232) : C(232, 243, 255);                     // snowy lip on top
                    else
                    {
                        col = C(104, 128, 154);
                        if (h < 7) col = C(80, 102, 128);
                        else if (h > 96) col = C(160, 184, 208);
                    }
                    c.Set(x, y, col);
                }
            return c;
        }

        static Canvas DrawIcePatch()
        {
            var c = new Canvas(128, 128);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    uint h = Hash(x / 3, y / 2, 7) % 100;
                    Color32 col = C(176, 222, 246);
                    if (h < 7 && (y % 7) < 2) col = C(224, 244, 255);
                    c.Set(x, y, col);
                }
            return c;
        }
    }
}

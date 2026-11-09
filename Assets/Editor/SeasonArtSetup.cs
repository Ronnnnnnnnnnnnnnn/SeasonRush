using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// Puts the artist's own pictures in place of the generated placeholders:
    ///  * Winter: Assets/2D Asset/Winter (ground, side, cliff, ice patch, rocks, bushes),
    ///  * the Spring cloud enemies: cloud1.png + cloud2.png (two pictures of a floating cloud; the game shows them one
    ///    after the other).
    /// It runs by itself when Unity loads the project (and when "Rebuild Project Setup" / "Reset Maps" build the defaults), changes
    /// only what still uses a placeholder, and never touches maps, props or enemies you changed by hand.
    /// Menu: Season Rush > Apply Winter And Cloud Art runs it again.
    /// </summary>
    [InitializeOnLoad]
    public static class SeasonArtSetup
    {
        public const string WinterDir = "Assets/2D Asset/Winter";
        public const string ThemeDir = "Assets/Data";
        public const string MaterialDir = "Assets/Materials";
        /// <summary>Pixels per unit of the cloud pictures: the cloud (without its shadow) becomes about 0.9 map units wide.</summary>
        public const float CloudPpu = 1100f;

        /// <summary>The Winter map as generated has this many props (8 pines + 6 rocks); more or fewer means it was edited by hand.</summary>
        const int DefaultWinterPlacementCount = 14;

        static SeasonArtSetup()
        {
            EditorApplication.update += WaitThenApply;
        }

        static void WaitThenApply()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitThenApply;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { ApplyToExistingThemes(); }
            catch (Exception e) { Debug.LogWarning("[Setup] Winter / cloud art could not be applied: " + e.Message); }
        }

        [MenuItem("Season Rush/Apply Winter And Cloud Art")]
        public static void ApplyFromMenu()
        {
            bool changed = ApplyToExistingThemes();
            EditorUtility.DisplayDialog("Season Rush", changed
                ? "Done: your Winter pictures and the cloud enemy pictures are now used."
                : "Nothing to change: everything that can use your pictures already does (or the pictures are not in Assets/2D Asset yet).", "OK");
        }

        /// <summary>Batch mode: Unity -batchmode -executeMethod SeasonRush.EditorTools.SeasonArtSetup.ApplyFromCommandLine</summary>
        public static void ApplyFromCommandLine()
        {
            try { ApplyToExistingThemes(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        // ------------------------------------------------------------------ what is available

        static bool Exists(string path) => File.Exists(Path.GetFullPath(path));

        public static bool HasWinterArt =>
            Exists(WinterDir + "/center_with_grass.jpg") && Exists(WinterDir + "/side2_winter.png") && Exists(WinterDir + "/edge_winter.jpg") && Exists(WinterDir + "/ice.jpg");

        public static bool HasWinterProps =>
            Exists(WinterDir + "/rock1_winter.png") && Exists(WinterDir + "/rock2_winter.png") && Exists(WinterDir + "/rock3_winter.png")
            && Exists(WinterDir + "/bush_winter.png") && Exists(WinterDir + "/bush_winter (1).png");

        /// <summary>Finds a picture by file name anywhere under Assets/2D Asset (the artist may have put it in any folder).</summary>
        static string FindPicture(string fileName)
        {
            foreach (string guid in AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName) + " t:Texture2D", new[] { "Assets/2D Asset" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase)) return path;
            }
            return null;
        }

        // ------------------------------------------------------------------ for the generated default maps

        /// <summary>The Winter prop types with the artist's pictures: pine (placeholder), 3 rocks, 2 bushes.</summary>
        internal static PropDef[] WinterPropDefs(Sprite pine)
        {
            return new[]
            {
                SeasonRushSetup.Prop("Pine", pine, 1.0f, 0.35f),
                SeasonRushSetup.Prop("Rock1", SeasonRushSetup.PropSprite(WinterDir + "/rock1_winter.png"), 0.9f, 0.50f),
                SeasonRushSetup.Prop("Rock2", SeasonRushSetup.PropSprite(WinterDir + "/rock2_winter.png"), 1.8f, 0.30f),
                SeasonRushSetup.Prop("Rock3", SeasonRushSetup.PropSprite(WinterDir + "/rock3_winter.png"), 1.4f, 0.30f),
                SeasonRushSetup.Prop("Bush", SeasonRushSetup.PropSprite(WinterDir + "/bush_winter.png"), 1.2f, 0f),
                SeasonRushSetup.Prop("Bush2", SeasonRushSetup.PropSprite(WinterDir + "/bush_winter (1).png"), 1.2f, 0f)
            };
        }

        /// <summary>Gives the Spring cloud enemies (as generated) the artist's cloud pictures. Does nothing without the pictures.</summary>
        internal static void ApplyCloudSprites(EnemyDef[] enemies)
        {
            if (!PrepareCloudFrames(out Sprite[] frames, out float headHeight)) return;
            foreach (EnemyDef def in enemies) UseCloud(def, frames, headHeight);
        }

        static void UseCloud(EnemyDef def, Sprite[] frames, float headHeight)
        {
            def.sprite = frames[0];
            def.frames = frames.Length >= 2 ? frames : null;
            if (def.frameRate <= 0f) def.frameRate = 2.5f;
            def.headHeight = headHeight;
        }

        // ------------------------------------------------------------------ for maps that already exist

        /// <summary>Applies the pictures to the existing Winter and Spring maps. Returns true when something changed.</summary>
        public static bool ApplyToExistingThemes()
        {
            bool changed = false;
            if (HasWinterArt) changed |= ApplyWinter();
            changed |= ApplyClouds();
            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Setup] Your Winter / cloud pictures are now used by the maps.");
            }
            return changed;
        }

        static bool ApplyWinter()
        {
            bool changed = false;
            changed |= ReplacePlaceholderTexture("Winter_Ground", WinterDir + "/center_with_grass.jpg");
            changed |= ReplacePlaceholderTexture("Winter_Side", WinterDir + "/side2_winter.png");
            changed |= ReplacePlaceholderTexture("Winter_Cliff", WinterDir + "/edge_winter.jpg");
            changed |= ReplacePlaceholderTexture("Ice_Patch", WinterDir + "/ice.jpg");

            var theme = AssetDatabase.LoadAssetAtPath<SeasonTheme>(ThemeDir + "/WinterTheme.asset");
            if (theme == null || theme.props == null || !HasWinterProps) return changed;

            // rocks that still use the Spring rock pictures become the Winter rocks
            for (int i = 0; i < theme.props.Length; i++)
            {
                PropDef def = theme.props[i];
                string name = def.name ?? "";
                if (!name.StartsWith("IceRock", StringComparison.Ordinal) || name.Length < 8) continue;
                char n = name[name.Length - 1];
                if (n < '1' || n > '3') continue;
                string current = def.sprite != null ? AssetDatabase.GetAssetPath(def.sprite) : "";
                if (current.Length > 0 && !current.Contains("/Spring/")) continue;                    // not the generated one: leave it
                def.sprite = SeasonRushSetup.PropSprite($"{WinterDir}/rock{n}_winter.png");
                def.tint = Color.white;                                                                   // the Winter rocks already have snow on them
                def.name = "Rock" + n;
                changed = true;
            }

            // the two bush types (added at the end, so no existing prop number changes)
            bool hasBush = false;
            foreach (PropDef def in theme.props) if (def.name == "Bush" || def.name == "Bush2") hasBush = true;
            if (!hasBush)
            {
                int first = theme.props.Length;
                var list = new List<PropDef>(theme.props)
                {
                    SeasonRushSetup.Prop("Bush", SeasonRushSetup.PropSprite(WinterDir + "/bush_winter.png"), 1.2f, 0f),
                    SeasonRushSetup.Prop("Bush2", SeasonRushSetup.PropSprite(WinterDir + "/bush_winter (1).png"), 1.2f, 0f)
                };
                theme.props = list.ToArray();

                // a map that was never edited by hand also gets a few bushes; an edited one only gets the new types to place itself
                if (theme.placements != null && theme.placements.Length == DefaultWinterPlacementCount)
                {
                    var placements = new List<PropPlacement>(theme.placements);
                    AddPair(placements, first, 1.2f, 3.4f);
                    AddPair(placements, first + 1, 11.0f, 3.6f);
                    theme.placements = placements.ToArray();
                }
                changed = true;
            }

            if (changed) EditorUtility.SetDirty(theme);
            return changed;
        }

        static void AddPair(List<PropPlacement> list, int prop, float x, float y)
        {
            list.Add(new PropPlacement { prop = prop, position = new Vector2(x, y), flipX = false });
            list.Add(new PropPlacement { prop = prop, position = new Vector2(y, x), flipX = true });
        }

        /// <summary>Gives a material the artist's picture when it still shows a generated placeholder.</summary>
        static bool ReplacePlaceholderTexture(string materialName, string picturePath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{materialName}.mat");
            if (material == null) return false;
            string current = material.mainTexture != null ? AssetDatabase.GetAssetPath(material.mainTexture) : "";
            if (current.Length > 0 && !current.Contains("/Placeholders/")) return false;                // already the artist's (or your own)
            material.mainTexture = SeasonRushSetup.Tex(picturePath);
            EditorUtility.SetDirty(material);
            return true;
        }

        static bool ApplyClouds()
        {
            var theme = AssetDatabase.LoadAssetAtPath<SeasonTheme>(ThemeDir + "/SpringTheme.asset");
            if (theme == null || theme.enemies == null || theme.enemies.Length == 0) return false;

            bool needed = false;
            foreach (EnemyDef def in theme.enemies)
                if (def.sprite == null || AssetDatabase.GetAssetPath(def.sprite).Contains("/Placeholders/enemy_cloud")) needed = true;
            if (!needed || !PrepareCloudFrames(out Sprite[] frames, out float headHeight)) return false;

            foreach (EnemyDef def in theme.enemies)
            {
                string current = def.sprite != null ? AssetDatabase.GetAssetPath(def.sprite) : "";
                if (current.Length == 0 || current.Contains("/Placeholders/enemy_cloud")) UseCloud(def, frames, headHeight);
            }
            EditorUtility.SetDirty(theme);
            return true;
        }

        // ------------------------------------------------------------------ the cloud pictures

        /// <summary>
        /// cloud1.png / cloud2.png are two pictures of the same floating cloud, each with its own shadow underneath. They
        /// get their pivot in the middle of the shadow, so the shadow stays on the cloud's ground position in both.
        /// </summary>
        static bool PrepareCloudFrames(out Sprite[] frames, out float headHeight)
        {
            frames = null;
            headHeight = 0f;
            string first = FindPicture("cloud1.png");
            if (first == null) return false;
            string second = FindPicture("cloud2.png");

            var list = new List<Sprite>();
            foreach (string path in new[] { first, second })
            {
                if (path == null) continue;
                Vector2 pivot = GroundPivot(path, out float top);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                bool ready = importer != null && importer.spriteImportMode == SpriteImportMode.Single
                    && Mathf.Approximately(importer.spritePixelsPerUnit, CloudPpu) && Vector2.Distance(importer.spritePivot, pivot) < 0.002f;
                if (!ready) SeasonRushSetup.ImportSprite(path, CloudPpu, pivot, FilterMode.Bilinear, true);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) return false;
                list.Add(sprite);
                headHeight = Mathf.Max(headHeight, top);
            }
            frames = list.ToArray();
            return true;
        }

        /// <summary>Pivot in the middle of the lowest part of the drawing (the shadow), and how high the drawing reaches above it.</summary>
        static Vector2 GroundPivot(string path, out float topUnits)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(path)));
                Color32[] px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                int minY = h, maxY = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 24) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
                topUnits = 0f;
                if (maxY < 0) return new Vector2(0.5f, 0.1f);

                int band = Mathf.Max(2, (int)((maxY - minY + 1) * 0.08f));
                int minX = w, maxX = -1;
                for (int y = minY; y < minY + band && y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 24) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
                float pivotX = maxX >= 0 ? (minX + maxX) * 0.5f / w : 0.5f;
                float pivotY = minY + band * 0.5f;
                topUnits = (maxY + 1 - pivotY) / CloudPpu;
                return new Vector2(pivotX, pivotY / h);
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }
    }
}

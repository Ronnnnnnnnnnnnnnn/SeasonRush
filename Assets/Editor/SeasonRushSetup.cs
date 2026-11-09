using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// One-click project setup (menu: Season Rush > Rebuild Project Setup).
    /// It is safe to run again at any time: it re-imports the art with the right settings, creates the
    /// three Season Theme assets if they are missing, and rebuilds the network prefabs and the game scene
    /// (Assets/Scenes/SeasonRush.unity).
    ///
    /// Season Theme assets that already exist are NOT touched, so maps you edited by hand (props, positions,
    /// enemies, materials) survive. Use "Season Rush > Reset Maps To Defaults" to throw your edits away.
    /// </summary>
    public static class SeasonRushSetup
    {
        const string SpringDir = "Assets/2D Asset/Spring";
        const string FallDir = "Assets/2D Asset/Falll";          // (sic) the folder name in the art pack
        const string PlaceDir = PlaceholderArt.Dir;
        const string DataDir = "Assets/Data";
        const string MatDir = "Assets/Materials";
        const string PrefabDir = "Assets/Prefabs";
        public const string ScenePath = "Assets/Scenes/SeasonRush.unity";

        const float PropPpu = 500f;      // 2100px canvases: a big tree ends up about 2.3 world units wide
        const float CharPpu = 40f;

        const int PlayerLayer = 8, EnemyLayer = 9, ObstacleLayer = 10;

        /// <summary>True only while "Reset Maps To Defaults" runs: then existing themes and materials are rewritten.</summary>
        static bool _resetMaps;

        [MenuItem("Season Rush/Reset Maps To Defaults (loses your map edits)")]
        public static void ResetMapsToDefaults()
        {
            if (!EditorUtility.DisplayDialog("Reset maps to defaults",
                    "This puts the Spring, Fall and Winter maps (props, positions, spawns, enemies and the ground materials) back to the generated defaults.\n\nEverything you changed by hand in Assets/Data and Assets/Materials is lost.",
                    "Reset maps", "Cancel")) return;
            ResetMapsNow();
        }

        static void ResetMapsNow()
        {
            _resetMaps = true;
            try { BuildAll(); }
            finally { _resetMaps = false; }
        }

        /// <summary>Batch mode: Unity -batchmode -executeMethod SeasonRush.EditorTools.SeasonRushSetup.ResetMapsFromCommandLine</summary>
        public static void ResetMapsFromCommandLine()
        {
            try { ResetMapsNow(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Makes the selected pictures usable as map props (trees, rocks, bushes): Sprite type, 500 pixels per unit like the
        /// other props, and the pivot at the bottom-centre of the drawn object so it stands where you place it.
        /// </summary>
        [MenuItem("Season Rush/Prepare Selected Pictures As Map Props")]
        public static void PrepareSelectedAsProps()
        {
            var done = new List<string>();
            foreach (string guid in Selection.assetGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                PropSprite(path);
                done.Add(Path.GetFileName(path));
            }
            AssetDatabase.SaveAssets();
            string message = done.Count == 0
                ? "Select one or more .png pictures in the Project window first (for example Assets/2D Asset/Falll/rock2_fall.png), then run this again."
                : "Ready to use as map props:\n\n" + string.Join("\n", done) + "\n\nDrag them into the Sprite slot of a prop in a Season Theme asset.";
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Season Rush", message, "OK");
            Debug.Log("[Setup] " + message.Replace("\n\n", " ").Replace("\n", ", "));
        }

        [MenuItem("Season Rush/Rebuild Project Setup")]
        public static void BuildAll()
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string d in new[] { DataDir, MatDir, PrefabDir, "Assets/Scenes" }) Directory.CreateDirectory(Path.GetFullPath(d));
            }
            finally { AssetDatabase.StopAssetEditing(); }

            EnsureLayers();
            ConfigurePlayerSettings();
            ConfigureAndroidSettings();
            EnsureSpacetimeConfig();
            PlaceholderArt.EnsureAll();
            PlayerSkinsSetup.Ensure(false);          // player1.png / player2.png become the two warriors (if the pictures exist)
            MenuStyleSetup.Ensure();                 // Assets/Resources/MenuStyle.asset: the menu's font, texts and colours

            // ---------------- art ----------------
            // island ground textures (used on meshes, so only the texture matters)
            Texture2D springGround = Tex(SpringDir + "/center_grass_spring.png");
            Texture2D springSide = Tex(SpringDir + "/side2_grass_spring.png");
            Texture2D springCliff = Tex(SpringDir + "/edge_spring.jpg");
            Texture2D sand = Tex(SpringDir + "/sand_spring.jpg");
            Texture2D fallGround = Tex(FallDir + "/center_grass_fall.png");
            Texture2D fallSide = Tex(FallDir + "/side2_grass_fall.png");
            Texture2D fallCliff = Tex(FallDir + "/edge_fall.jpg");
            // Winter uses the artist's pictures from Assets/2D Asset/Winter when they are there, otherwise the generated placeholders
            bool winterArt = SeasonArtSetup.HasWinterArt;
            string winterDir = SeasonArtSetup.WinterDir;
            Texture2D winterGround = winterArt ? Tex(winterDir + "/center_with_grass.jpg") : Tex(PlaceDir + "/center_snow_winter.png", FilterMode.Point, false);
            Texture2D winterSide = winterArt ? Tex(winterDir + "/side2_winter.png") : Tex(PlaceDir + "/side_snow_winter.png", FilterMode.Point, false);
            Texture2D winterCliff = winterArt ? Tex(winterDir + "/edge_winter.jpg") : Tex(PlaceDir + "/edge_winter.png", FilterMode.Point, false);
            Texture2D ice = winterArt ? Tex(winterDir + "/ice.jpg") : Tex(PlaceDir + "/ice_patch_winter.png", FilterMode.Point, false);

            Material mSpringGround = Mat("Spring_Ground", springGround), mSpringSide = Mat("Spring_Side", springSide), mSpringCliff = Mat("Spring_Cliff", springCliff);
            Material mSand = Mat("Sand_Patch", sand);
            Material mFallGround = Mat("Fall_Ground", fallGround), mFallSide = Mat("Fall_Side", fallSide), mFallCliff = Mat("Fall_Cliff", fallCliff);
            Material mWinterGround = Mat("Winter_Ground", winterGround), mWinterSide = Mat("Winter_Side", winterSide), mWinterCliff = Mat("Winter_Cliff", winterCliff);
            Material mIce = Mat("Ice_Patch", ice);

            // props
            Sprite bigTree = PropSprite(SpringDir + "/big_tree.png"), smallTree = PropSprite(SpringDir + "/small_tree.png");
            Sprite sakura = PropSprite(SpringDir + "/sakura_tree.png"), sakura2 = PropSprite(SpringDir + "/sakura_tree2.png");
            Sprite bush = PropSprite(SpringDir + "/bush_spring.png"), flowers = PropSprite(SpringDir + "/flowers_spring.png");
            Sprite rock1 = PropSprite(SpringDir + "/rock1_spring.png"), rock2 = PropSprite(SpringDir + "/rock2_spring.png"), rock3 = PropSprite(SpringDir + "/rock3_spring.png");
            Sprite deadTree = PropSprite(FallDir + "/big_tree (1).png");
            Sprite orange1 = PropSprite(FallDir + "/small_tree_1.png"), orange2 = PropSprite(FallDir + "/small_tree_2.png");
            Sprite pine = CharSprite(PlaceDir + "/pine_winter.png", 64f, new Vector2(0.5f, 0.02f));

            // characters (placeholders)
            Sprite warriorBody = CharSprite(PlaceDir + "/warrior_body.png", CharPpu, new Vector2(0.5f, 0.04f));
            Sprite warriorTunic = CharSprite(PlaceDir + "/warrior_tunic.png", CharPpu, new Vector2(0.5f, 0.04f));
            Sprite cloud = CharSprite(PlaceDir + "/enemy_cloud.png", CharPpu, new Vector2(0.5f, 0.08f));
            Sprite treeMonster = CharSprite(PlaceDir + "/enemy_treemonster.png", CharPpu, new Vector2(0.5f, 0.08f));
            Sprite snowMonster = CharSprite(PlaceDir + "/enemy_snowmonster.png", CharPpu, new Vector2(0.5f, 0.08f));

            // ---------------- themes ----------------
            var cloudFoes = new[]
            {
                Foe("Puff Cloud", EnemyTier.Weak, cloud, 0.8f, new Color(1f, 1f, 1f), 20, 2.2f, 6, 10, 6, 0.35f),
                Foe("Rain Cloud", EnemyTier.Medium, cloud, 1.05f, new Color(0.72f, 0.82f, 1f), 45, 2.6f, 10, 25, 3, 0.45f),
                Foe("Storm Cloud", EnemyTier.Strong, cloud, 1.35f, new Color(0.52f, 0.52f, 0.72f), 105, 3.0f, 16, 60, 1, 0.60f)
            };
            SeasonArtSetup.ApplyCloudSprites(cloudFoes);                 // the artist's cloud pictures (cloud1.png + cloud2.png), if they are there

            var spring = MakeTheme("SpringTheme", Season.Spring, "Spring", "Fresh and colourful - the clouds are rolling in.",
                new Color(0.80f, 0.86f, 1.00f), mSpringGround, mSpringSide, mSpringCliff, mSand, true,
                new[]
                {
                    Prop("BigTree", bigTree, 0.85f, 0.45f), Prop("SmallTree", smallTree, 1.2f, 0.35f), Prop("Sakura", sakura, 0.85f, 0.45f),
                    Prop("Sakura2", sakura2, 0.9f, 0.40f), Prop("Bush", bush, 1.2f, 0f), Prop("Flowers", flowers, 0.8f, 0f),
                    Prop("Rock1", rock1, 0.9f, 0.50f), Prop("Rock2", rock2, 1.8f, 0.30f), Prop("Rock3", rock3, 1.4f, 0.30f)
                },
                SpringLayout(),
                cloudFoes);

            var fall = MakeTheme("FallTheme", Season.Fall, "Fall", "Cozy but eerie - something moves in the fog.",
                new Color(0.71f, 0.72f, 0.87f), mFallGround, mFallSide, mFallCliff, mSand, true,
                new[]
                {
                    Prop("DeadTree", deadTree, 1.5f, 0.35f), Prop("OrangeTree1", orange1, 1.2f, 0.35f), Prop("OrangeTree2", orange2, 1.2f, 0.35f),
                    Prop("Rock1", rock1, 0.9f, 0.50f), Prop("Rock2", rock2, 1.8f, 0.30f), Prop("Rock3", rock3, 1.4f, 0.30f)
                },
                FallLayout(),
                new[]
                {
                    Foe("Sapling", EnemyTier.Weak, treeMonster, 0.8f, new Color(1f, 1f, 1f), 20, 2.4f, 6, 10, 6, 0.35f),
                    Foe("Stump Guard", EnemyTier.Medium, treeMonster, 1.05f, new Color(0.85f, 0.75f, 0.9f), 50, 2.6f, 10, 25, 3, 0.45f),
                    Foe("Elder Treant", EnemyTier.Strong, treeMonster, 1.4f, new Color(0.7f, 0.6f, 0.75f), 110, 2.8f, 18, 65, 1, 0.60f)
                });

            bool winterProps = SeasonArtSetup.HasWinterProps;
            var winter = MakeTheme("WinterTheme", Season.Winter, "Winter", "Cold and slippery - keep moving.",
                new Color(0.80f, 0.90f, 0.97f), mWinterGround, mWinterSide, mWinterCliff, mIce, true,
                winterProps
                    ? SeasonArtSetup.WinterPropDefs(pine)                // the artist's rocks and bushes (the pine is still a placeholder)
                    : new[]
                    {
                        Prop("Pine", pine, 1.0f, 0.35f),
                        Prop("IceRock1", rock1, 0.9f, 0.50f, new Color(0.78f, 0.88f, 1f)), Prop("IceRock2", rock2, 1.8f, 0.30f, new Color(0.78f, 0.88f, 1f)),
                        Prop("IceRock3", rock3, 1.4f, 0.30f, new Color(0.78f, 0.88f, 1f))
                    },
                WinterLayout(winterProps),
                new[]
                {
                    Foe("Snowling", EnemyTier.Weak, snowMonster, 0.8f, new Color(1f, 1f, 1f), 22, 2.3f, 6, 10, 6, 0.35f),
                    Foe("Snowman", EnemyTier.Medium, snowMonster, 1.05f, new Color(0.82f, 0.92f, 1f), 50, 2.7f, 11, 25, 3, 0.45f),
                    Foe("Frost Golem", EnemyTier.Strong, snowMonster, 1.4f, new Color(0.62f, 0.78f, 1f), 115, 3.0f, 18, 65, 1, 0.60f)
                });
            // ---------------- prefabs ----------------
            GameObject enemyPrefab = MakeEnemyPrefab();
            GameObject playerPrefab = MakePlayerPrefab(warriorBody, warriorTunic);
            GameObject matchPrefab = MakeMatchManagerPrefab(enemyPrefab);

            var prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            prefabList.Add(new NetworkPrefab { Prefab = enemyPrefab });
            prefabList.Add(new NetworkPrefab { Prefab = matchPrefab });
            string listPath = PrefabDir + "/SeasonRushNetworkPrefabs.asset";
            AssetDatabase.DeleteAsset(listPath);
            AssetDatabase.CreateAsset(prefabList, listPath);

            // ---------------- scene ----------------
            BuildScene(playerPrefab, matchPrefab, prefabList, new[] { spring, fall, winter });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Setup] Season Rush setup finished. Open Assets/Scenes/SeasonRush.unity and press Play.");
        }

        /// <summary>Entry point for batch mode: Unity -batchmode -executeMethod SeasonRush.EditorTools.SeasonRushSetup.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        // ================================================================== project settings

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayerName(layers, PlayerLayer, "Player");
            SetLayerName(layers, EnemyLayer, "Enemy");
            SetLayerName(layers, ObstacleLayer, "Obstacle");
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Creates Assets/Resources/SpacetimeConfig.asset the first time. It is never overwritten afterwards, so the
        /// database name you type into it (after publishing the module) is kept when this tool runs again.
        /// </summary>
        static void EnsureSpacetimeConfig()
        {
            const string path = "Assets/Resources/SpacetimeConfig.asset";
            Directory.CreateDirectory(Path.GetFullPath("Assets/Resources"));
            if (AssetDatabase.LoadAssetAtPath<SpacetimeConfig>(path) != null) return;
            var config = ScriptableObject.CreateInstance<SpacetimeConfig>();
            AssetDatabase.CreateAsset(config, path);
            Debug.Log("[Setup] created " + path + " (set its Database field after publishing the module)");
        }

        static void SetLayerName(SerializedProperty layers, int index, string name)
        {
            SerializedProperty element = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(element.stringValue) || element.stringValue == name) element.stringValue = name;
            else Debug.LogWarning($"[Setup] Layer {index} is already named '{element.stringValue}', expected '{name}'.");
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.runInBackground = true;                       // two game windows on one PC must both keep running
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
        }

        /// <summary>
        /// Settings for an Android phone build. They are stored in the project and only matter when you build for
        /// Android (they do not change the Windows game). Needs the Android module only at build time.
        /// </summary>
        static void ConfigureAndroidSettings()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.seasonrush.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);   // required for 64-bit phones
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.forceInternetPermission = true;                                          // multiplayer needs the network

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        // ================================================================== art import

        internal static Texture2D Tex(string path, FilterMode filter = FilterMode.Bilinear, bool mipmaps = true)
        {
            ImportSprite(path, 100f, null, filter, mipmaps);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Sprite PropSprite(string path)
        {
            // pivot at the base of the object so it can be placed by where it stands on the ground
            Vector2 pivot = ComputeBasePivot(path);
            ImportSprite(path, PropPpu, pivot, FilterMode.Bilinear, true);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite CharSprite(string path, float ppu, Vector2 pivot)
        {
            ImportSprite(path, ppu, pivot, FilterMode.Point, false);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        internal static void ImportSprite(string path, float ppu, Vector2? pivot, FilterMode filter, bool mipmaps)
        {
            if (!File.Exists(Path.GetFullPath(path))) throw new FileNotFoundException("Missing art file: " + path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = mipmaps;
            importer.filterMode = filter;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = filter == FilterMode.Point ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (pivot.HasValue)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot.Value;
            }
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        /// <summary>Finds the drawn part of a sprite on its big transparent canvas and returns its bottom-centre as a pivot.</summary>
        static Vector2 ComputeBasePivot(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(path)));
            Color32[] px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            int minX = w, maxX = -1, minY = h, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 24)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            UnityEngine.Object.DestroyImmediate(tex);
            if (maxX < 0) return new Vector2(0.5f, 0.1f);
            Debug.Log($"[Setup] {Path.GetFileName(path)}: drawn size {(maxX - minX + 1) / PropPpu:0.00} x {(maxY - minY + 1) / PropPpu:0.00} units at scale 1");
            return new Vector2((minX + maxX) * 0.5f / w, minY / (float)h);
        }

        static Material Mat(string name, Texture2D texture)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) throw new Exception("URP 2D Sprite-Unlit-Default shader not found.");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (!_resetMaps) return mat;          // keep a ground texture that was swapped by hand
            mat.mainTexture = texture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ================================================================== themes

        internal static PropDef Prop(string name, Sprite sprite, float scale, float collider, Color? tint = null)
        {
            return new PropDef { name = name, sprite = sprite, scale = scale, colliderRadius = collider, tint = tint ?? Color.white };
        }

        static EnemyDef Foe(string name, EnemyTier tier, Sprite sprite, float scale, Color tint, int hp, float speed, int damage, int points, int weight, float radius)
        {
            return new EnemyDef
            {
                displayName = name, tier = tier, sprite = sprite, scale = scale, tint = tint, maxHealth = hp, speed = speed,
                contactDamage = damage, points = points, spawnWeight = weight, radius = radius
            };
        }

        static SeasonTheme MakeTheme(string assetName, Season season, string display, string mood, Color sky,
            Material ground, Material side, Material cliff, Material patch, bool hasPatch,
            PropDef[] props, List<PropPlacement> placements, EnemyDef[] enemies)
        {
            string path = DataDir + "/" + assetName + ".asset";
            var theme = AssetDatabase.LoadAssetAtPath<SeasonTheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<SeasonTheme>();
                AssetDatabase.CreateAsset(theme, path);
            }
            else if (!_resetMaps)
            {
                return theme;                          // already exists: keep every hand edit (props, positions, spawns, enemies)
            }
            theme.season = season;
            theme.displayName = display;
            theme.mood = mood;
            theme.skyColor = sky;
            theme.groundMaterial = ground;
            theme.sideMaterial = side;
            theme.cliffMaterial = cliff;
            theme.patchMaterial = patch;
            theme.hasPatch = hasPatch;
            theme.patchCenter = new Vector2(6f, 6f);
            theme.patchRadius = 2.3f;
            theme.cliffHeight = 2.0f;
            theme.props = props;
            theme.placements = placements.ToArray();
            theme.playerSpawns = new[] { new Vector2(1.8f, 10.2f), new Vector2(10.2f, 1.8f) };
            theme.enemySpawns = new[] { new Vector2(1.8f, 1.8f), new Vector2(10.2f, 10.2f), new Vector2(10.6f, 6.0f), new Vector2(6.0f, 10.6f) };
            theme.enemies = enemies;
            EditorUtility.SetDirty(theme);
            return theme;
        }

        /// <summary>Adds a placement and its mirror image (swapping x and y flips the map left-right on screen), so the map is fair.</summary>
        static void Sym(List<PropPlacement> list, int prop, float x, float y)
        {
            list.Add(new PropPlacement { prop = prop, position = new Vector2(x, y), flipX = false });
            if (Mathf.Abs(x - y) > 0.01f)
                list.Add(new PropPlacement { prop = prop, position = new Vector2(y, x), flipX = true });
        }

        // Prop indices follow the order of the PropDef arrays above.
        static List<PropPlacement> SpringLayout()
        {
            var l = new List<PropPlacement>();
            // 0 BigTree 1 SmallTree 2 Sakura 3 Sakura2 4 Bush 5 Flowers 6 Rock1 7 Rock2 8 Rock3
            // The middle stays open for fighting; cover sits on the lanes and along the edges.
            Sym(l, 0, 3.4f, 3.4f);   // big tree in front
            Sym(l, 2, 9.4f, 9.4f);   // sakura at the back
            Sym(l, 1, 2.2f, 6.6f);
            Sym(l, 3, 10.8f, 7.4f);
            Sym(l, 6, 4.6f, 8.6f);
            Sym(l, 7, 2.6f, 4.2f);
            Sym(l, 8, 8.4f, 2.8f);
            Sym(l, 4, 1.6f, 5.4f);
            Sym(l, 4, 9.8f, 5.2f);
            Sym(l, 5, 4.4f, 6.9f);
            Sym(l, 5, 7.9f, 8.6f);
            Sym(l, 5, 6.0f, 6.0f);
            return l;
        }

        static List<PropPlacement> FallLayout()
        {
            var l = new List<PropPlacement>();
            // 0 DeadTree 1 OrangeTree1 2 OrangeTree2 3 Rock1 4 Rock2 5 Rock3
            Sym(l, 0, 3.4f, 3.4f);
            Sym(l, 0, 8.8f, 8.8f);
            Sym(l, 1, 2.2f, 5.8f);
            Sym(l, 2, 4.6f, 9.2f);
            Sym(l, 0, 7.0f, 2.2f);
            Sym(l, 2, 10.4f, 6.6f);
            Sym(l, 3, 4.4f, 8.2f);
            Sym(l, 4, 8.4f, 4.2f);
            Sym(l, 5, 5.6f, 3.0f);
            return l;
        }

        static List<PropPlacement> WinterLayout(bool withBushes = false)
        {
            var l = new List<PropPlacement>();
            // 0 Pine 1 Rock1 2 Rock2 3 Rock3 (4 Bush 5 Bush2 when the artist's bush pictures are there)
            Sym(l, 0, 3.2f, 3.2f);
            Sym(l, 0, 9.2f, 9.2f);
            Sym(l, 0, 2.0f, 5.6f);
            Sym(l, 0, 10.4f, 7.0f);
            Sym(l, 0, 5.0f, 9.8f);
            Sym(l, 1, 4.4f, 8.2f);
            Sym(l, 2, 8.0f, 3.6f);
            Sym(l, 3, 4.0f, 4.8f);
            if (withBushes)
            {
                Sym(l, 4, 1.2f, 3.4f);
                Sym(l, 5, 11.0f, 3.6f);
            }
            return l;
        }

        // ================================================================== prefabs

        static void ConfigureNetworkTransform(NetworkTransform nt, NetworkTransform.AuthorityModes mode)
        {
            nt.AuthorityMode = mode;
            nt.SyncPositionX = true;
            nt.SyncPositionY = true;
            nt.SyncPositionZ = false;             // 2D game: z never changes
            nt.SyncRotAngleX = false; nt.SyncRotAngleY = false; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false; nt.SyncScaleY = false; nt.SyncScaleZ = false;
            nt.Interpolate = true;
        }

        static Rigidbody2D AddBody(GameObject go)
        {
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            return rb;
        }

        static GameObject MakePlayerPrefab(Sprite body, Sprite tunic)
        {
            var go = new GameObject("Player") { layer = PlayerLayer };
            AddBody(go);
            go.AddComponent<CircleCollider2D>().radius = 0.4f;

            // Netcode: NetworkObject + NetworkTransform (owner authority) + NetworkRigidbody2D
            go.AddComponent<NetworkObject>();
            ConfigureNetworkTransform(go.AddComponent<NetworkTransform>(), NetworkTransform.AuthorityModes.Owner);
            go.AddComponent<NetworkRigidbody2D>();

            go.AddComponent<PlayerStats>();
            var controller = go.AddComponent<PlayerController>();
            go.AddComponent<PlayerCombat>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(visual, false);
            var bodyRenderer = bodyGo.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = body;
            var tunicGo = new GameObject("Tunic");
            tunicGo.transform.SetParent(visual, false);
            var tunicRenderer = tunicGo.AddComponent<SpriteRenderer>();
            tunicRenderer.sprite = tunic;

            controller.visual = visual;
            controller.body = bodyRenderer;
            controller.tunic = tunicRenderer;

            return SavePrefab(go, PrefabDir + "/Player.prefab");
        }

        static GameObject MakeEnemyPrefab()
        {
            var go = new GameObject("Enemy") { layer = EnemyLayer };
            AddBody(go);
            go.AddComponent<CircleCollider2D>().radius = 0.3f;

            go.AddComponent<NetworkObject>();
            ConfigureNetworkTransform(go.AddComponent<NetworkTransform>(), NetworkTransform.AuthorityModes.Server);
            go.AddComponent<NetworkRigidbody2D>();

            var enemy = go.AddComponent<Enemy>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            enemy.visual = visual.transform;
            enemy.body = sr;

            return SavePrefab(go, PrefabDir + "/Enemy.prefab");
        }

        static GameObject MakeMatchManagerPrefab(GameObject enemyPrefab)
        {
            var go = new GameObject("MatchManager");
            go.AddComponent<NetworkObject>();
            go.AddComponent<MatchManager>().enemyPrefab = enemyPrefab;
            return SavePrefab(go, PrefabDir + "/MatchManager.prefab");
        }

        static GameObject SavePrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ================================================================== scene

        static void BuildScene(GameObject playerPrefab, GameObject matchPrefab, NetworkPrefabsList prefabList, SeasonTheme[] themes)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // fixed camera (MapManager frames the island; it never follows a player)
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = themes[0].skyColor;
            camGo.transform.position = new Vector3(0f, 5f, -10f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // Netcode: NetworkManager + UnityTransport
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", GameConfig.DefaultPort);
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.ConnectionApproval = true;           // used to cap the match at two players
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            nm.NetworkConfig.TickRate = 30;
            nm.RunInBackground = true;

            // game scripts
            var game = new GameObject("Game");
            game.AddComponent<MapManager>().themes = themes;
            game.AddComponent<SpacetimeManager>();
            game.AddComponent<OnlineMatchmaker>();
            game.AddComponent<ConnectionMenu>().matchManagerPrefab = matchPrefab;
            game.AddComponent<GameHud>();
            game.AddComponent<VirtualControls>();
            game.AddComponent<AutomatedTestDriver>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}

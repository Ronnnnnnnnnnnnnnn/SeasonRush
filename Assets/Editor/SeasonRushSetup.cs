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
    public static class SeasonRushSetup
    {
        const string SpringDir = "Assets/2D Asset/Spring";
        const string FallDir = "Assets/2D Asset/Falll";
        const string PlaceDir = PlaceholderArt.Dir;
        const string DataDir = "Assets/Data";
        const string MatDir = "Assets/Materials";
        const string PrefabDir = "Assets/Prefabs";
        public const string ScenePath = "Assets/Scenes/SeasonRush.unity";

        const float PropPpu = 500f;
        const float CharPpu = 40f;

        const int PlayerLayer = 8, EnemyLayer = 9, ObstacleLayer = 10;

        static bool _resetMaps;

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Reset Maps To Defaults (loses your map edits)")]
#endif
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

        public static void ResetMapsFromCommandLine()
        {
            try { ResetMapsNow(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Prepare Selected Pictures As Map Props")]
#endif
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

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Rebuild Project Setup")]
#endif
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
            PlayerSkinsSetup.Ensure(false);
            MenuStyleSetup.Ensure();
            UiSkinSetup.Apply(false);
            BrandingSetup.Apply(false);
            SoundSetup.Apply(false);

            Texture2D springGround = Tex(SpringDir + "/center_grass_spring.png");
            Texture2D springSide = Tex(SpringDir + "/side2_grass_spring.png");
            Texture2D springCliff = Tex(SpringDir + "/edge_spring.jpg");
            Texture2D sand = Tex(SpringDir + "/sand_spring.jpg");
            Texture2D fallGround = Tex(FallDir + "/center_grass_fall.png");
            Texture2D fallSide = Tex(FallDir + "/side1_grass_fall.png");
            Texture2D fallCliff = Tex(FallDir + "/edge_fall.jpg");
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

            Sprite bigTree = PropSprite(SpringDir + "/big_tree.png"), smallTree = PropSprite(SpringDir + "/small_tree.png");
            Sprite sakura = PropSprite(SpringDir + "/sakura_tree.png"), sakura2 = PropSprite(SpringDir + "/sakura_tree2.png");
            Sprite bush = PropSprite(SpringDir + "/bush_spring.png"), flowers = PropSprite(SpringDir + "/flowers_spring.png");
            Sprite rock1 = PropSprite(SpringDir + "/rock1_spring.png"), rock2 = PropSprite(SpringDir + "/rock2_spring.png"), rock3 = PropSprite(SpringDir + "/rock3_spring.png");
            Sprite deadTree = PropSprite(FallDir + "/big_tree (1).png");
            Sprite orange1 = PropSprite(FallDir + "/small_tree_1.png"), orange2 = PropSprite(FallDir + "/small_tree_2.png");
            Sprite pine = CharSprite(PlaceDir + "/pine_winter.png", 64f, new Vector2(0.5f, 0.02f));

            Sprite warriorBody = CharSprite(PlaceDir + "/warrior_body.png", CharPpu, new Vector2(0.5f, 0.04f));
            Sprite warriorTunic = CharSprite(PlaceDir + "/warrior_tunic.png", CharPpu, new Vector2(0.5f, 0.04f));
            Sprite cloud = CharSprite(PlaceDir + "/enemy_cloud.png", CharPpu, new Vector2(0.5f, 0.08f));
            Sprite treeMonster = CharSprite(PlaceDir + "/enemy_treemonster.png", CharPpu, new Vector2(0.5f, 0.08f));
            Sprite snowMonster = CharSprite(PlaceDir + "/enemy_snowmonster.png", CharPpu, new Vector2(0.5f, 0.08f));

            var cloudFoes = new[]
            {
                Foe("Puff Cloud", EnemyTier.Weak, cloud, 0.8f, new Color(1f, 1f, 1f), 20, 2.2f, 6, 10, 6, 0.35f),
                Foe("Rain Cloud", EnemyTier.Medium, cloud, 1.05f, new Color(0.72f, 0.82f, 1f), 45, 2.6f, 10, 25, 3, 0.45f),
                Foe("Storm Cloud", EnemyTier.Strong, cloud, 1.35f, new Color(0.52f, 0.52f, 0.72f), 105, 3.0f, 16, 60, 1, 0.60f)
            };
            SeasonArtSetup.ApplyCloudSprites(cloudFoes);

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
                    ? SeasonArtSetup.WinterPropDefs(pine)
                    : new[]
                    {
                        Prop("Pine", pine, 1.0f, 0.35f),
                        Prop("IceRock1", rock1, 0.9f, 0.50f, new Color(0.78f, 0.88f, 1f)), Prop("IceRock2", rock2, 1.8f, 0.30f, new Color(0.78f, 0.88f, 1f)),
                        Prop("IceRock3", rock3, 1.4f, 0.30f, new Color(0.78f, 0.88f, 1f))
                    },
                WinterLayout(winterProps, winterProps && SeasonArtSetup.HasPine && SeasonArtSetup.HasPine2),
                new[]
                {
                    Foe("Snowling", EnemyTier.Weak, snowMonster, 0.8f, new Color(1f, 1f, 1f), 22, 2.3f, 6, 10, 6, 0.35f),
                    Foe("Snowman", EnemyTier.Medium, snowMonster, 1.05f, new Color(0.82f, 0.92f, 1f), 50, 2.7f, 11, 25, 3, 0.45f),
                    Foe("Frost Golem", EnemyTier.Strong, snowMonster, 1.4f, new Color(0.62f, 0.78f, 1f), 115, 3.0f, 18, 65, 1, 0.60f)
                });
            SeasonArtSetup.ApplyToExistingThemes();
            CharacterAnimSetup.ApplyAll();

            GameObject enemyPrefab = MakeEnemyPrefab();
            GameObject playerPrefab = MakePlayerPrefab(warriorBody, warriorTunic);
            GameObject matchPrefab = MakeMatchManagerPrefab(enemyPrefab);

            var prefabList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            prefabList.Add(new NetworkPrefab { Prefab = enemyPrefab });
            prefabList.Add(new NetworkPrefab { Prefab = matchPrefab });
            string listPath = PrefabDir + "/SeasonRushNetworkPrefabs.asset";
            AssetDatabase.DeleteAsset(listPath);
            AssetDatabase.CreateAsset(prefabList, listPath);

            BuildScene(playerPrefab, matchPrefab, prefabList, new[] { spring, fall, winter });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Setup] Season Rush setup finished. Open Assets/Scenes/SeasonRush.unity and press Play.");
        }

        public static void BuildFromCommandLine()
        {
            try { BuildAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

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
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
        }

        static void ConfigureAndroidSettings()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.seasonrush.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.forceInternetPermission = true;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        internal static Texture2D Tex(string path, FilterMode filter = FilterMode.Bilinear, bool mipmaps = true)
        {
            ImportSprite(path, 100f, null, filter, mipmaps);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Sprite PropSprite(string path)
        {
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
            else if (!_resetMaps) return mat;
            mat.mainTexture = texture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

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
                return theme;
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
            theme.playerSpawns = new[] { new Vector2(1.3f, 10.8f), new Vector2(10.8f, 1.3f) };
            theme.enemySpawns = new[] { new Vector2(1.8f, 1.8f), new Vector2(9.6f, 11.0f), new Vector2(11.0f, 4.7f), new Vector2(4.7f, 11.0f) };
            theme.enemies = enemies;
            EditorUtility.SetDirty(theme);
            return theme;
        }

        static List<PropPlacement> SpringLayout()
        {
            var l = new List<PropPlacement>();
            l.Add(new PropPlacement { prop = 0, position = new Vector2(4.7f, 3.4f), flipX = false });
            l.Add(new PropPlacement { prop = 2, position = new Vector2(11.0f, 10.5f), flipX = false });
            l.Add(new PropPlacement { prop = 1, position = new Vector2(1.6f, 6.6f), flipX = false });
            l.Add(new PropPlacement { prop = 1, position = new Vector2(6.6f, 1.6f), flipX = true });
            l.Add(new PropPlacement { prop = 3, position = new Vector2(10.8f, 7.4f), flipX = false });
            l.Add(new PropPlacement { prop = 3, position = new Vector2(7.4f, 10.8f), flipX = true });
            l.Add(new PropPlacement { prop = 6, position = new Vector2(4.8f, 8.0f), flipX = false });
            l.Add(new PropPlacement { prop = 6, position = new Vector2(8.0f, 4.8f), flipX = true });
            l.Add(new PropPlacement { prop = 7, position = new Vector2(1.9f, 3.4f), flipX = false });
            l.Add(new PropPlacement { prop = 7, position = new Vector2(3.4f, 1.9f), flipX = true });
            l.Add(new PropPlacement { prop = 8, position = new Vector2(8.2f, 1.2f), flipX = false });
            l.Add(new PropPlacement { prop = 8, position = new Vector2(1.2f, 8.2f), flipX = true });
            l.Add(new PropPlacement { prop = 4, position = new Vector2(0.9f, 4.5f), flipX = false });
            l.Add(new PropPlacement { prop = 4, position = new Vector2(4.5f, 0.9f), flipX = true });
            l.Add(new PropPlacement { prop = 4, position = new Vector2(9.8f, 5.2f), flipX = false });
            l.Add(new PropPlacement { prop = 4, position = new Vector2(5.2f, 9.8f), flipX = true });
            l.Add(new PropPlacement { prop = 5, position = new Vector2(2.3f, 5.9f), flipX = false });
            l.Add(new PropPlacement { prop = 5, position = new Vector2(5.9f, 2.3f), flipX = true });
            l.Add(new PropPlacement { prop = 5, position = new Vector2(7.9f, 8.6f), flipX = false });
            l.Add(new PropPlacement { prop = 5, position = new Vector2(8.6f, 7.9f), flipX = true });
            l.Add(new PropPlacement { prop = 5, position = new Vector2(6.0f, 6.0f), flipX = false });
            return l;
        }

        static void Add(List<PropPlacement> list, int prop, float x, float y, bool flipX)
        {
            list.Add(new PropPlacement { prop = prop, position = new Vector2(x, y), flipX = flipX });
        }

        static List<PropPlacement> FallLayout()
        {
            var l = new List<PropPlacement>();
            Add(l, 0, 4.7f, 3.4f, false);
            Add(l, 2, 10.25f, 9.5f, false);
            Add(l, 1, 1.75f, 6.6f, false);
            Add(l, 1, 6.6f, 1.75f, true);
            Add(l, 2, 10.25f, 7.4f, false);
            Add(l, 2, 7.4f, 10.25f, true);
            Add(l, 3, 4.8f, 8.0f, false);
            Add(l, 3, 8.0f, 4.8f, true);
            Add(l, 4, 1.9f, 3.4f, false);
            Add(l, 4, 3.4f, 1.9f, true);
            Add(l, 5, 8.7f, 1.9f, false);
            Add(l, 5, 1.9f, 8.7f, true);
            return l;
        }

        static List<PropPlacement> WinterLayout(bool withBushes = false, bool withPine2 = false)
        {
            var l = new List<PropPlacement>();
            foreach (PropPlacement p in WinterFull())
            {
                int prop = p.prop;
                if ((prop == 4 || prop == 5) && !withBushes) continue;
                if (prop == 6 && !withPine2) prop = 0;
                Add(l, prop, p.position.x, p.position.y, p.flipX);
            }
            return l;
        }

        static List<PropPlacement> WinterFull()
        {
            var l = new List<PropPlacement>();
            Add(l, 0, 4.7f, 3.4f, false);
            Add(l, 0, 10.25f, 9.5f, false);
            Add(l, 6, 1.75f, 6.6f, false);
            Add(l, 6, 6.6f, 1.75f, true);
            Add(l, 6, 10.25f, 7.4f, false);
            Add(l, 6, 7.4f, 10.25f, true);
            Add(l, 1, 4.8f, 8.0f, false);
            Add(l, 1, 8.0f, 4.8f, true);
            Add(l, 2, 1.9f, 3.4f, false);
            Add(l, 2, 3.4f, 1.9f, true);
            Add(l, 3, 8.7f, 1.9f, false);
            Add(l, 3, 1.9f, 8.7f, true);
            Add(l, 4, 0.9f, 4.5f, false);
            Add(l, 4, 4.5f, 0.9f, true);
            Add(l, 4, 9.8f, 5.2f, false);
            Add(l, 4, 5.2f, 9.8f, true);
            Add(l, 5, 2.3f, 5.9f, false);
            Add(l, 5, 5.9f, 2.3f, true);
            Add(l, 5, 7.9f, 8.6f, false);
            Add(l, 5, 8.6f, 7.9f, true);
            Add(l, 5, 6.0f, 6.0f, false);
            return l;
        }

        static void ConfigureNetworkTransform(NetworkTransform nt, NetworkTransform.AuthorityModes mode)
        {
            nt.AuthorityMode = mode;
            nt.SyncPositionX = true;
            nt.SyncPositionY = true;
            nt.SyncPositionZ = false;
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
            var baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(NetworkAnimatorSetup.BaseControllerPath);
            if (baseController != null) NetworkAnimatorSetup.Configure(go, controller, baseController);

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

        static void BuildScene(GameObject playerPrefab, GameObject matchPrefab, NetworkPrefabsList prefabList, SeasonTheme[] themes)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", GameConfig.DefaultPort);
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            nm.NetworkConfig.TickRate = 30;
            nm.RunInBackground = true;

            var environment = new GameObject("Environment").transform;
            GameObject walls = MapManager.CreateWalls(environment);
            var mapsRoot = new GameObject("Maps").transform;
            mapsRoot.SetParent(environment, false);
            var mapInstances = new GameObject[themes.Length];
            for (int i = 0; i < themes.Length; i++)
            {
                GameObject mapPrefab = SceneBaker.BakeMapPrefab(themes[i]);
                mapInstances[i] = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, mapsRoot);
                mapInstances[i].SetActive(i == 0);
            }

            var managers = new GameObject("Managers").transform;
            GameObject Manager(string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(managers, false);
                return go;
            }
            var mapManager = Manager("MapManager").AddComponent<MapManager>();
            mapManager.themes = themes;
            mapManager.maps = mapInstances;
            mapManager.arenaWalls = walls;
            Manager("SpacetimeManager").AddComponent<SpacetimeManager>();
            Manager("OnlineMatchmaker").AddComponent<OnlineMatchmaker>();
            var menu = Manager("ConnectionMenu").AddComponent<ConnectionMenu>();
            menu.matchManagerPrefab = matchPrefab;
            var hud = Manager("GameHud").AddComponent<GameHud>();
            var controls = Manager("VirtualControls").AddComponent<VirtualControls>();
            Manager("AudioManager").AddComponent<AudioManager>();
            Manager("AutomatedTestDriver").AddComponent<AutomatedTestDriver>();

            UiBuilder.Build(menu, hud, controls);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}

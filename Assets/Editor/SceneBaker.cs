using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// Turns the Season Theme data into real objects: one prefab for every prop type (a tree, a rock, a bush) and one prefab for every
    /// season map (island meshes + all its props, each prop an instance of its prop prefab). The map prefabs are placed in the scene under
    /// Environment > Maps, so the Hierarchy shows the whole game.
    ///
    /// The Season Theme assets stay the place where maps are edited (Map Editor, Inspector). After editing a theme run
    /// Season Rush > Update Maps In Scene: it regenerates the prefabs. Until you do, the game notices the baked map is out of date and
    /// builds the map from the theme itself, so you never see old data in a match.
    /// </summary>
    public static class SceneBaker
    {
        public const string PropDir = "Assets/Prefabs/Props";
        public const string MapDir = "Assets/Prefabs/Maps";
        const string MeshDir = MapDir + "/Meshes";

        [MenuItem("Season Rush/Update Maps In Scene")]
        public static void UpdateMapsFromMenu()
        {
            string message = UpdateMapsInOpenScene();
            EditorUtility.DisplayDialog("Season Rush", message, "OK");
        }

        /// <summary>Regenerates the prop and map prefabs and makes sure the open scene has an instance of every map.</summary>
        public static string UpdateMapsInOpenScene()
        {
            MapManager manager = Object.FindFirstObjectByType<MapManager>();
            if (manager == null || manager.themes == null || manager.themes.Length == 0)
                return "Open Assets/Scenes/SeasonRush.unity first (the scene with the MapManager), then run this again.";

            Transform mapsRoot = FindOrCreate("Environment/Maps");
            var instances = new GameObject[manager.themes.Length];
            for (int i = 0; i < manager.themes.Length; i++)
            {
                GameObject prefab = BakeMapPrefab(manager.themes[i]);
                GameObject existing = manager.maps != null && i < manager.maps.Length ? manager.maps[i] : null;
                bool linked = existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing) == prefab;
                if (linked)
                {
                    instances[i] = existing;
                }
                else
                {
                    if (existing != null) Object.DestroyImmediate(existing);
                    instances[i] = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mapsRoot);
                    instances[i].SetActive(i == 0);
                }
            }
            Undo.RecordObject(manager, "Update maps");
            manager.maps = instances;
            if (manager.arenaWalls == null)
            {
                Transform environment = FindOrCreate("Environment");
                manager.arenaWalls = MapManager.CreateWalls(environment);
            }
            EditorUtility.SetDirty(manager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            AssetDatabase.SaveAssets();
            return $"The {manager.themes.Length} maps in the scene (Environment > Maps) and their prop prefabs are up to date.\n\nSave the scene (Ctrl+S).";
        }

        static Transform FindOrCreate(string path)
        {
            Transform parent = null;
            foreach (string part in path.Split('/'))
            {
                Transform found = null;
                if (parent == null) { GameObject go = GameObject.Find("/" + part); if (go != null) found = go.transform; }
                else found = parent.Find(part);
                if (found == null)
                {
                    var created = new GameObject(part).transform;
                    created.SetParent(parent, false);
                    found = created;
                }
                parent = found;
            }
            return parent;
        }

        // ------------------------------------------------------------------ prefabs

        /// <summary>Writes Prop_* prefabs and Map_* prefab for one theme (overwriting the old ones) and returns the map prefab.</summary>
        public static GameObject BakeMapPrefab(SeasonTheme theme)
        {
            EnsureFolder(PropDir);
            EnsureFolder(MapDir);
            EnsureFolder(MeshDir);
            string season = SafeName(theme.displayName);

            // 1) a prefab for every prop type
            var propPrefabs = new GameObject[theme.props != null ? theme.props.Length : 0];
            var used = new HashSet<string>();
            for (int i = 0; i < propPrefabs.Length; i++)
            {
                string name = $"Prop_{season}_{SafeName(theme.props[i].name)}";
                if (!used.Add(name)) name += "_" + i;
                propPrefabs[i] = SaveProp(theme.props[i], name);
            }

            // 2) the island (ground, ragged edge, patch, cliffs) without props
            GameObject root = IsoMapBuilder.Build(theme, null, false, false);
            root.name = "Map_" + season;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                string meshPath = $"{MeshDir}/{root.name}_{filter.gameObject.name}.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(filter.sharedMesh, meshPath);
            }

            // 3) the props: one instance of its prop prefab for every placement
            Transform props = new GameObject("Props").transform;
            props.SetParent(root.transform, false);
            if (theme.placements != null)
            {
                for (int i = 0; i < theme.placements.Length; i++)
                {
                    PropPlacement pl = theme.placements[i];
                    if (pl.prop < 0 || pl.prop >= propPrefabs.Length) continue;
                    PropDef def = theme.props[pl.prop];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(propPrefabs[pl.prop], props);
                    instance.name = $"{def.name}_{i}";
                    instance.transform.position = Iso.ToScreen3(pl.position);                     // where it is drawn
                    Transform visual = instance.transform.Find("Visual");
                    visual.localScale = new Vector3(pl.flipX ? -def.scale : def.scale, def.scale, 1f);
                    var renderer = visual.GetComponent<SpriteRenderer>();
                    if (renderer != null) renderer.sortingOrder = Iso.SortOrder(pl.position);
                    Transform collider = instance.transform.Find("Collider");
                    if (collider != null) collider.position = new Vector3(pl.position.x, pl.position.y, 0f);   // the blocking circle is on the flat plane
                }
            }

            var marker = root.AddComponent<BakedMap>();
            marker.season = (int)theme.season;
            marker.themeHash = theme.ContentHash();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{MapDir}/Map_{season}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>A prop prefab: an empty root, a "Visual" child with the picture and a "Collider" child (blocking props only).</summary>
        static GameObject SaveProp(PropDef def, string name)
        {
            var go = new GameObject(name);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(def.scale, def.scale, 1f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = def.sprite;
            renderer.color = def.tint;

            if (def.colliderRadius > 0f)
            {
                var collider = new GameObject("Collider");
                collider.transform.SetParent(go.transform, false);
                int layer = LayerMask.NameToLayer(IsoMapBuilder.ObstacleLayerName);
                if (layer >= 0) collider.layer = layer;
                collider.AddComponent<CircleCollider2D>().radius = def.colliderRadius;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PropDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static string SafeName(string text)
        {
            if (string.IsNullOrEmpty(text)) return "Unnamed";
            var chars = new List<char>();
            foreach (char c in text) if (char.IsLetterOrDigit(c)) chars.Add(c);
            return chars.Count == 0 ? "Unnamed" : new string(chars.ToArray());
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

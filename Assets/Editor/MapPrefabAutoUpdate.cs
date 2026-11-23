using System;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    [InitializeOnLoad]
    public static class MapPrefabAutoUpdate
    {
        const string DataDir = "Assets/Data";
        const string MapDir = "Assets/Prefabs/Maps";

        static MapPrefabAutoUpdate()
        {
            EditorApplication.update += WaitThenApply;
        }

        static void WaitThenApply()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitThenApply;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Apply(); }
            catch (Exception e) { Debug.LogWarning("[Setup] The map prefabs could not be updated: " + e.Message); }
        }

        public static void ApplyFromCommandLine()
        {
            try { Apply(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        public static int Apply()
        {
            int updated = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:SeasonTheme", new[] { DataDir }))
            {
                var theme = AssetDatabase.LoadAssetAtPath<SeasonTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (theme == null) continue;

                var baked = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapDir}/Map_{theme.displayName}.prefab");
                var marker = baked != null ? baked.GetComponent<BakedMap>() : null;
                if (baked == null || marker == null) continue;
                if (marker.themeHash == theme.ContentHash()) continue;

                SceneBaker.BakeMapPrefab(theme);
                updated++;
                Debug.Log($"[Setup] The {theme.displayName} map prefab was out of date and has been rebuilt from its theme.");
            }
            if (updated > 0) AssetDatabase.SaveAssets();
            return updated;
        }
    }
}

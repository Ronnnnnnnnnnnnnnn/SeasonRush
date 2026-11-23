using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    [InitializeOnLoad]
    public static class MenuStyleSetup
    {
        public const string AssetPath = "Assets/Resources/MenuStyle.asset";

        static MenuStyleSetup()
        {
            EditorApplication.update += WaitThenEnsure;
        }

        static void WaitThenEnsure()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitThenEnsure;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Ensure(); }
            catch (Exception e) { Debug.LogWarning("[Setup] Menu style asset could not be created: " + e.Message); }
        }

        public static bool Ensure()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MenuStyle>(AssetPath);
            if (existing != null)
            {
                if (existing.defaultsVersion < MenuStyle.CurrentDefaultsVersion)
                {
                    existing.MigrateDefaults();
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Setup] Menu style updated to the pastel colours: " + AssetPath);
                }
                return false;
            }
            Directory.CreateDirectory(Path.GetFullPath("Assets/Resources"));
            var created = ScriptableObject.CreateInstance<MenuStyle>();
            created.defaultsVersion = MenuStyle.CurrentDefaultsVersion;
            AssetDatabase.CreateAsset(created, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Menu style created: " + AssetPath + " (edit it to change the menu's font, texts and colours)");
            return true;
        }

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Select Menu Style (edit menu font, texts, colours)")]
#endif
        public static void SelectAsset()
        {
            Ensure();
            var asset = AssetDatabase.LoadAssetAtPath<MenuStyle>(AssetPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}

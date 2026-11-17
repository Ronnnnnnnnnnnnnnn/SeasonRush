using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// Netcode adds its own "DefaultNetworkPrefabs" list to every NetworkManager. It contains the same prefabs (Enemy, MatchManager) as
    /// Season Rush's own list (Assets/Prefabs/SeasonRushNetworkPrefabs.asset), so every Play printed the red errors
    /// "NetworkPrefab (Enemy) has a duplicate GlobalObjectIdHash ..." and "Removing invalid prefabs ...".
    /// This removes the default list from the NetworkManager when our own list is there. It runs when a scene is opened and just before
    /// Play, so the errors are gone without anything to click. Menu: Season Rush > Fix Network Prefab List In Scene.
    /// </summary>
    [InitializeOnLoad]
    public static class NetworkPrefabListFix
    {
        static NetworkPrefabListFix()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => Fix(scene);
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Fix(SceneManager.GetActiveScene());
            };
            EditorApplication.delayCall += () => Fix(SceneManager.GetActiveScene());
        }

        [MenuItem("Season Rush/Fix Network Prefab List In Scene")]
        public static void FixFromMenu()
        {
            bool changed = Fix(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Season Rush", changed
                ? "Done: the duplicate network prefab list is removed.\n\nSave the scene (Ctrl+S)."
                : "Nothing to fix: the NetworkManager only has Season Rush's prefab list.", "OK");
        }

        /// <summary>The list Netcode generated itself (Assets/DefaultNetworkPrefabs.asset): it has "IsDefault" ticked.</summary>
        public static bool IsDefaultList(NetworkPrefabsList list)
        {
            var property = new SerializedObject(list).FindProperty("IsDefault");
            return property != null ? property.boolValue : list.name == "DefaultNetworkPrefabs";
        }

        /// <summary>Returns true when the scene was changed (it is marked as changed, not saved).</summary>
        public static bool Fix(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlaying) return false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NetworkManager nm in root.GetComponentsInChildren<NetworkManager>(true))
                {
                    var lists = nm.NetworkConfig?.Prefabs?.NetworkPrefabsLists;
                    if (lists == null) continue;
                    bool hasOwn = lists.Any(l => l != null && !IsDefaultList(l));
                    if (!hasOwn) continue;                                              // never leave the NetworkManager without a list
                    int removed = lists.RemoveAll(l => l == null || IsDefaultList(l));
                    if (removed == 0) continue;
                    EditorUtility.SetDirty(nm);
                    EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log("[Setup] Removed the duplicate default network prefab list from the NetworkManager (save the scene to keep it).");
                    return true;
                }
            }
            return false;
        }
    }
}

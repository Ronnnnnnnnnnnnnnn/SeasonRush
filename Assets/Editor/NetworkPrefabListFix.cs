using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeasonRush.EditorTools
{
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

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Fix Network Prefab List In Scene")]
#endif
        public static void FixFromMenu()
        {
            bool changed = Fix(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Season Rush", changed
                ? "Done: the duplicate network prefab list is removed.\n\nSave the scene (Ctrl+S)."
                : "Nothing to fix: the NetworkManager only has Season Rush's prefab list.", "OK");
        }

        public static bool IsDefaultList(NetworkPrefabsList list)
        {
            var property = new SerializedObject(list).FindProperty("IsDefault");
            return property != null ? property.boolValue : list.name == "DefaultNetworkPrefabs";
        }

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
                    if (!hasOwn) continue;
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

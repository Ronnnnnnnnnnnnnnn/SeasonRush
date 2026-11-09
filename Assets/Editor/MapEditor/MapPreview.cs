using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// Shows one season map in the Scene view while you are NOT playing, so it can be edited.
    ///
    /// The preview objects are hidden and flagged "do not save": they never end up in the scene file or in a build.
    /// They are removed automatically when you press Play, switch or close the scene, recompile, or close the
    /// Map Editor window.
    /// </summary>
    [InitializeOnLoad]
    public static class MapPreview
    {
        public const string RootName = "~SeasonRush Map Preview (not saved)";
        const HideFlags Flags = HideFlags.HideAndDontSave;

        static GameObject _root;
        static GameObject _map;
        static SeasonTheme _theme;

        public static bool IsShown => _root != null;
        public static SeasonTheme Theme => _root != null ? _theme : null;

        static MapPreview()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorSceneManager.sceneClosing += (scene, removing) => Clear();
            EditorApplication.quitting += Clear;
            EditorApplication.delayCall += RemoveStrays;
        }

        public static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode) Clear();
        }

        /// <summary>Builds the whole map (ground, cliffs, patch and props) of <paramref name="theme"/> in the Scene view.</summary>
        public static void Show(SeasonTheme theme)
        {
            Clear();
            if (theme == null || Application.isPlaying) return;

            _theme = theme;
            _root = new GameObject(RootName);
            _root.hideFlags = Flags;
            _map = IsoMapBuilder.Build(theme, _root.transform, false);
            MarkAll();
        }

        /// <summary>Rebuilds only the props (cheap): call it after changing placements or prop types.</summary>
        public static void UpdateProps()
        {
            if (_root == null || _map == null || _theme == null) return;
            Transform old = _map.transform.Find("Props");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            IsoMapBuilder.BuildProps(_theme, _map.transform, false);
            MarkAll();
        }

        public static void Clear()
        {
            if (_root != null)
            {
                // the island is made of generated meshes: free them too
                foreach (MeshFilter filter in _root.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) Object.DestroyImmediate(filter.sharedMesh);
                Object.DestroyImmediate(_root);
            }
            _root = null;
            _map = null;
            _theme = null;
        }

        static void MarkAll()
        {
            foreach (Transform t in _root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = Flags;
            SceneVisibilityManager.instance.DisablePicking(_root, true);      // clicks go to the editor handles, not to the preview
        }

        /// <summary>After a crash or an odd reload a hidden preview may be left behind: remove it.</summary>
        static void RemoveStrays()
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go == _root || go.name != RootName || EditorUtility.IsPersistent(go)) continue;
                Object.DestroyImmediate(go);
            }
        }
    }
}

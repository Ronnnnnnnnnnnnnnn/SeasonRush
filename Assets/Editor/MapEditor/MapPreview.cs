using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SeasonRush.EditorTools
{
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
            SceneVisibilityManager.instance.DisablePicking(_root, true);
        }

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

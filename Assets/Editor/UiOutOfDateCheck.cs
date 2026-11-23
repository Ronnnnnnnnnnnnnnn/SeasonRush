using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeasonRush.EditorTools
{
    [InitializeOnLoad]
    public static class UiOutOfDateCheck
    {
        static UiOutOfDateCheck()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += () => CheckAndRebuild(scene, true);
            EditorApplication.delayCall += () => CheckAndRebuild(SceneManager.GetActiveScene(), true);
        }

        public static bool CheckAndRebuild(Scene scene, bool askUser)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode && askUser) return false;

            ConnectionMenu menu = null;
            GameHud hud = null;
            VirtualControls controls = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (menu == null) menu = root.GetComponentInChildren<ConnectionMenu>(true);
                if (hud == null) hud = root.GetComponentInChildren<GameHud>(true);
                if (controls == null) controls = root.GetComponentInChildren<VirtualControls>(true);
            }
            if (menu == null || hud == null || controls == null) return false;

            bool outOfDate = menu.canvas == null || hud.pausedByRival == null || hud.resultNote == null || hud.pausePanel == null
                || GameObject.Find("/UI/HUD/MatchUi/PausePanel/Box/SurrenderButton") == null && hud.pausePanel.transform.Find("Box/SurrenderButton") == null;
            if (!outOfDate) return false;

            if (askUser && !EditorUtility.DisplayDialog("Season Rush",
                    "The menu and in-game screen in this scene are from an older version: the pause menu still has the old buttons (no SURRENDER).\n\n" +
                    "Rebuild the canvas now? It replaces the \"UI\" object only; your maps and managers are not touched.\n\nSave the scene afterwards (Ctrl+S).",
                    "Rebuild", "Not now"))
                return true;

            UiBuilder.Build(menu, hud, controls);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Setup] The UI canvas was rebuilt (pause menu: RESUME / SURRENDER). Save the scene (Ctrl+S).");
            return true;
        }
    }
}

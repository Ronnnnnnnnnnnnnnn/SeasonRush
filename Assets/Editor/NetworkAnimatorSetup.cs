using System;
using System.IO;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    [InitializeOnLoad]
    public static class NetworkAnimatorSetup
    {
        public const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        public const string BaseControllerPath = "Assets/Animation/Players/Player 1(Blue)/Player 1.controller";

        static NetworkAnimatorSetup()
        {
            EditorApplication.update += WaitThenApply;
        }

        static void WaitThenApply()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitThenApply;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Apply(); }
            catch (Exception e) { Debug.LogWarning("[Setup] NetworkAnimator could not be added to the player: " + e.Message); }
        }

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Add NetworkAnimator To Player")]
#endif
        public static void ApplyFromMenu()
        {
            bool changed = Apply();
            EditorUtility.DisplayDialog("Season Rush", changed
                ? "Done: the Player prefab has an Animator and a NetworkAnimator now."
                : "Nothing to change: the Player prefab already has a NetworkAnimator (or the prefab / controller is missing).", "OK");
        }

        public static void ApplyFromCommandLine()
        {
            try { Apply(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        public static bool Apply()
        {
            if (!File.Exists(Path.GetFullPath(PlayerPrefabPath))) return false;
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseControllerPath);
            if (controller == null) return false;

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var player = root.GetComponent<PlayerController>();
                if (player == null || player.body == null) return false;
                bool changed = Configure(root, player, controller);
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    Debug.Log("[Setup] The Player prefab now has an Animator and a NetworkAnimator (owner authority).");
                }
                return changed;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static bool Configure(GameObject root, PlayerController player, RuntimeAnimatorController controller)
        {
            bool changed = false;

            var animator = player.body.GetComponent<Animator>();
            if (animator == null) { animator = player.body.gameObject.AddComponent<Animator>(); changed = true; }
            if (animator.runtimeAnimatorController != controller) { animator.runtimeAnimatorController = controller; changed = true; }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var networkAnimator = root.GetComponent<NetworkAnimator>();
            if (networkAnimator == null) { networkAnimator = root.AddComponent<NetworkAnimator>(); changed = true; }
            if (networkAnimator.Animator != animator) { networkAnimator.Animator = animator; changed = true; }
            if (networkAnimator.AuthorityMode != NetworkAnimator.AuthorityModes.Owner) { networkAnimator.AuthorityMode = NetworkAnimator.AuthorityModes.Owner; changed = true; }

            if (player.animator != animator) { player.animator = animator; changed = true; }
            if (player.netAnimator != networkAnimator) { player.netAnimator = networkAnimator; changed = true; }
            return changed;
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    /// <summary>Builds a Windows player so a second copy of the game can be launched next to the Editor.</summary>
    public static class BuildScript
    {
        const string OutputPath = "Builds/Windows/SeasonRush.exe";

        [MenuItem("Season Rush/Build Windows Player (for a second game window)")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(OutputPath)));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SeasonRushSetup.ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Build] {report.summary.result} - {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors -> {OutputPath}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed");
        }

        /// <summary>Batch mode entry point.</summary>
        public static void BuildFromCommandLine()
        {
            try { BuildWindows(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Build] FAILED: " + e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Builds an installable Android app (.apk) for a phone. Needs Unity's "Android Build Support" module
        /// (Unity Hub > Installs > your editor > Add modules). See docs/Mobile_Testing.md.
        /// </summary>
        [MenuItem("Season Rush/Build Android APK (for a phone)")]
        public static void BuildAndroidFromMenu()
        {
            try { BuildAndroid(); EditorUtility.DisplayDialog("Season Rush", "Android app built:\n\nBuilds/Android/SeasonRush.apk\n\nSee docs/Mobile_Testing.md to install it on your phone.", "OK"); }
            catch (Exception e) { EditorUtility.DisplayDialog("Season Rush - Android build", e.Message, "OK"); }
        }

        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new Exception("Android Build Support is not installed for this Unity version.\n\nIn Unity Hub: Installs > this editor > Add modules > tick 'Android Build Support' (with 'OpenJDK' and 'Android SDK & NDK Tools'), then try again.");

            if (!File.Exists(SeasonRushSetup.ScenePath)) SeasonRushSetup.BuildAll();
            string output = "Builds/Android/SeasonRush.apk";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

            EditorUserBuildSettings.buildAppBundle = false;                 // an .apk can be installed directly
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SeasonRushSetup.ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Build] Android {report.summary.result} - {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors -> {output}");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed - see the Console for the errors.");
        }

        /// <summary>Batch mode: build the Android app.</summary>
        public static void BuildAndroidFromCommandLine()
        {
            try { BuildAndroid(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Build] FAILED: " + e.Message); EditorApplication.Exit(1); }
        }

        /// <summary>Batch mode: refresh the whole project setup, then build the player.</summary>
        public static void SetupAndBuildFromCommandLine()
        {
            try { SeasonRushSetup.BuildAll(); BuildWindows(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Build] FAILED: " + e); EditorApplication.Exit(1); }
        }
    }
}

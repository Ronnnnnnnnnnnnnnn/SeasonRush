using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    public static class BuildScript
    {
        const string OutputPath = "Builds/Windows/SeasonRush.exe";

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Build Windows Player (for a second game window)")]
#endif
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

        public static void BuildFromCommandLine()
        {
            try { BuildWindows(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Build] FAILED: " + e); EditorApplication.Exit(1); }
        }

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Build Android APK (for a phone)")]
#endif
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

            EditorUserBuildSettings.buildAppBundle = false;
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

        public static void BuildAndroidFromCommandLine()
        {
            try { BuildAndroid(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Build] FAILED: " + e.Message); EditorApplication.Exit(1); }
        }

        public static void SetupAndBuildFromCommandLine()
        {
            try { SeasonRushSetup.BuildAll(); BuildWindows(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Build] FAILED: " + e); EditorApplication.Exit(1); }
        }
    }
}

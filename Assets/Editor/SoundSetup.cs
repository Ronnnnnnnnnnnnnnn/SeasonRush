using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    [InitializeOnLoad]
    public static class SoundSetup
    {
        public const string SoundDir = "Assets/Sound";
        public const string AssetPath = "Assets/Resources/Sounds.asset";

        static SoundSetup()
        {
            EditorApplication.update += WaitThenApply;
        }

        static void WaitThenApply()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitThenApply;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Apply(false); }
            catch (Exception e) { Debug.LogWarning("[Setup] The sounds could not be set up: " + e.Message); }
        }

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Apply Sounds")]
#endif
        public static void ApplyFromMenu()
        {
            bool found = Apply(true);
            EditorUtility.DisplayDialog("Season Rush", found
                ? "Done: the menu music, the sword slash and the damage sound are set up (Assets/Resources/Sounds.asset)."
                : "No sound files found in " + SoundDir, "OK");
        }

        public static void ApplyFromCommandLine()
        {
            try { Apply(true); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        public static bool Apply(bool force)
        {
            if (!AssetDatabase.IsValidFolder(SoundDir)) return false;
            string[] paths = AssetDatabase.FindAssets("t:AudioClip", new[] { SoundDir }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
            if (paths.Length == 0) return false;

            string music = paths.FirstOrDefault(p => Has(p, "MUSIC"));
            var slashes = paths.Where(p => Has(p, "SLASH")).ToArray();
            string hurt = paths.FirstOrDefault(p => Has(p, "DAMAGE") || Has(p, "HURT"));
            string endSeason = paths.FirstOrDefault(p => Has(p, "END SEASON") || Has(p, "ENDSEASON"));
            string season = paths.FirstOrDefault(p => Has(p, "TRANSITION") || (Has(p, "SEASON") && !Has(p, "END")));
            string countdown = paths.FirstOrDefault(p => Has(p, "3SECONDS") || Has(p, "TIMER START") || Has(p, "COUNTDOWN"));
            string death = paths.FirstOrDefault(p => Has(p, "DEATH") || Has(p, "DIE"));
            string win = Find(paths, "WIN"), lose = Find(paths, "LOSE"), draw = Find(paths, "DRAW");
            string[] battleMusic = { Find(paths, "SPRING"), Find(paths, "FALL"), Find(paths, "WINTER") };
            string click = paths.FirstOrDefault(p => Has(p, "CLICK") || Has(p, "BUTTON"));

            if (music != null) ImportMusic(music);
            foreach (string s in slashes) ImportEffect(s);
            if (hurt != null) ImportEffect(hurt);
            if (season != null) ImportEffect(season);
            if (endSeason != null) ImportEffect(endSeason);
            if (countdown != null) ImportEffect(countdown);
            if (death != null) ImportEffect(death);
            if (win != null) ImportEffect(win);
            if (lose != null) ImportEffect(lose);
            if (draw != null) ImportEffect(draw);
            foreach (string track in battleMusic) if (track != null) ImportMusic(track);
            if (click != null) ImportEffect(click);

            Directory.CreateDirectory(Path.GetFullPath("Assets/Resources"));
            var settings = AssetDatabase.LoadAssetAtPath<SoundSettings>(AssetPath);
            bool changed = false;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<SoundSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                changed = true;
            }

            if (music != null && (settings.menuMusic == null || force))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(music);
                if (settings.menuMusic != clip) { settings.menuMusic = clip; changed = true; }
            }
            if (slashes.Length > 0 && (settings.slashClips == null || settings.slashClips.Length == 0 || force))
            {
                settings.slashClips = slashes.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
                changed = true;
            }
            if (hurt != null && (settings.hurtClip == null || force))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(hurt);
                if (settings.hurtClip != clip) { settings.hurtClip = clip; changed = true; }
            }

            if (endSeason != null && (settings.endSeasonClip == null || force))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(endSeason);
                if (settings.endSeasonClip != clip) { settings.endSeasonClip = clip; changed = true; }
            }
            if (countdown != null && (settings.countdownClip == null || force))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(countdown);
                if (settings.countdownClip != clip) { settings.countdownClip = clip; changed = true; }
            }
            changed |= FillClip(ref settings.winClip, win, force);
            changed |= FillClip(ref settings.loseClip, lose, force);
            changed |= FillClip(ref settings.drawClip, draw, force);
            if (death != null && (settings.deathClip == null || force))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(death);
                if (settings.deathClip != clip) { settings.deathClip = clip; changed = true; }
            }
            if (settings.seasonMusic == null || settings.seasonMusic.Length < 3) { Array.Resize(ref settings.seasonMusic, 3); changed = true; }
            for (int i = 0; i < 3; i++)
            {
                if (battleMusic[i] == null || (settings.seasonMusic[i] != null && !force)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(battleMusic[i]);
                if (settings.seasonMusic[i] != clip) { settings.seasonMusic[i] = clip; changed = true; }
            }
            if (season != null && (settings.seasonChangeClip == null || force))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(season);
                if (settings.seasonChangeClip != clip) { settings.seasonChangeClip = clip; changed = true; }
            }
            if (click != null && (settings.buttonClickClip == null || force))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(click);
                if (settings.buttonClickClip != clip) { settings.buttonClickClip = clip; changed = true; }
            }

            if (changed)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Setup] Sounds ready: music {Name(settings.menuMusic)}, {settings.slashClips?.Length ?? 0} slash sound(s), damage {Name(settings.hurtClip)}, season change {Name(settings.seasonChangeClip)}, button click {Name(settings.buttonClickClip)} ({AssetPath})");
            }
            return true;
        }

        static bool FillClip(ref AudioClip slot, string path, bool force)
        {
            if (path == null || (slot != null && !force)) return false;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (slot == clip) return false;
            slot = clip;
            return true;
        }

        static string Find(string[] paths, string word) => paths.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).Equals(word, StringComparison.OrdinalIgnoreCase));

        static bool Has(string path, string word) => Path.GetFileNameWithoutExtension(path).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

        static string Name(UnityEngine.Object o) => o != null ? o.name : "(none)";

        static void ImportMusic(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.Streaming && !importer.forceToMono) return;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            importer.defaultSampleSettings = s;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }

        static void ImportEffect(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.DecompressOnLoad && importer.forceToMono) return;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.8f;
            importer.defaultSampleSettings = s;
            s.preloadAudioData = true;
            importer.defaultSampleSettings = s;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }
    }
}

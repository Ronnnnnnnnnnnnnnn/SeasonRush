using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// Makes Assets/2D Asset/Players/player1.png and player2.png the pictures of player 1 (blue, host) and
    /// player 2 (red).
    ///
    /// It happens by itself: when Unity loads or recompiles, and whenever one of the two pictures is imported, the
    /// pictures get the right import settings (Sprite, a size that makes the knight about 1.5 map units tall, pivot at
    /// the feet) and Assets/Resources/PlayerSkins.asset is created / filled in. Menu: Season Rush > Apply Player Sprites
    /// does it again on demand. A slot you assigned by hand in PlayerSkins.asset is not overwritten by the automatic run.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayerSkinsSetup
    {
        public const string PictureDir = "Assets/2D Asset/Players";
        public static readonly string[] Pictures = { PictureDir + "/player1.png", PictureDir + "/player2.png" };
        public const string AssetPath = "Assets/Resources/PlayerSkins.asset";

        /// <summary>Pixels per unit. A 2100 px picture with a 1300 px tall knight becomes about 1.5 map units tall.</summary>
        public const float Ppu = 880f;

        static PlayerSkinsSetup()
        {
            EditorApplication.update += WaitThenEnsure;
        }

        static void WaitThenEnsure()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;       // try again next tick
            EditorApplication.update -= WaitThenEnsure;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Ensure(false); }
            catch (Exception e) { Debug.LogWarning("[Setup] Player sprites could not be applied: " + e.Message); }
        }

        [MenuItem("Season Rush/Apply Player Sprites (player1.png + player2.png)")]
        public static void ApplyFromMenu()
        {
            bool found = Ensure(true);
            if (!found)
                EditorUtility.DisplayDialog("Season Rush", "No pictures found.\n\nPut player1.png and player2.png in\n" + PictureDir, "OK");
        }

        /// <summary>Batch mode entry point.</summary>
        public static void EnsureFromCommandLine()
        {
            try { Ensure(true); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        /// <param name="force">True = reapply the import settings and overwrite the slots in PlayerSkins.asset.</param>
        /// <returns>False when there is no player picture at all.</returns>
        public static bool Ensure(bool force)
        {
            bool anyPicture = false;
            foreach (string p in Pictures) if (File.Exists(Path.GetFullPath(p))) anyPicture = true;
            if (!anyPicture) return false;

            Directory.CreateDirectory(Path.GetFullPath("Assets/Resources"));
            var skins = AssetDatabase.LoadAssetAtPath<PlayerSkins>(AssetPath);
            bool changed = false;
            if (skins == null)
            {
                skins = ScriptableObject.CreateInstance<PlayerSkins>();
                AssetDatabase.CreateAsset(skins, AssetPath);
                changed = true;
            }
            if (skins.slotSprites == null || skins.slotSprites.Length < Pictures.Length)
            {
                Array.Resize(ref skins.slotSprites, Pictures.Length);
                changed = true;
            }

            for (int slot = 0; slot < Pictures.Length; slot++)
            {
                string path = Pictures[slot];
                if (!File.Exists(Path.GetFullPath(path))) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                if (force || !IsReady(importer)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);   // OnPreprocessTexture applies the settings

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null && (force || skins.slotSprites[slot] == null) && skins.slotSprites[slot] != sprite)
                {
                    skins.slotSprites[slot] = sprite;
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(skins);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Setup] Player sprites ready: player 1 = {Name(skins, 0)}, player 2 = {Name(skins, 1)} ({AssetPath})");
            }
            return true;
        }

        static string Name(PlayerSkins skins, int slot) =>
            skins.slotSprites != null && slot < skins.slotSprites.Length && skins.slotSprites[slot] != null ? skins.slotSprites[slot].name : "(placeholder)";

        static bool IsReady(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Sprite
                && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, Ppu);
        }

        static bool IsSkinPicture(string path)
        {
            foreach (string p in Pictures) if (string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Import settings of a player picture; called while Unity imports it.</summary>
        internal static void ApplyImportSettings(TextureImporter importer, string path)
        {
            Vector2 pivot = FeetPivot(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;                       // the knight is drawn small: mipmaps keep it smooth
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
        }

        /// <summary>The bottom-centre of the knight's feet (not of the sword), so the knight stands exactly on its map position.</summary>
        static Vector2 FeetPivot(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(path)));
                Color32[] px = tex.GetPixels32();
                int w = tex.width, h = tex.height;
                int minY = h, maxY = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 24) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
                if (maxY < 0) return new Vector2(0.5f, 0.05f);

                int band = Mathf.Max(2, (int)((maxY - minY + 1) * 0.08f));          // the lowest 8 % of the drawing = the feet
                int minX = w, maxX = -1;
                for (int y = minY; y < minY + band && y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 24) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
                if (maxX < 0) return new Vector2(0.5f, minY / (float)h);

                Debug.Log($"[Setup] {Path.GetFileName(path)}: drawn {(maxY - minY + 1) / Ppu:0.00} units tall, feet centred at x {(minX + maxX) * 0.5f / w:0.000}");
                return new Vector2((minX + maxX) * 0.5f / w, minY / (float)h);
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        class SkinPostprocessor : AssetPostprocessor
        {
            void OnPreprocessTexture()
            {
                if (!IsSkinPicture(assetPath)) return;
                ApplyImportSettings((TextureImporter)assetImporter, assetPath);
            }
        }
    }
}

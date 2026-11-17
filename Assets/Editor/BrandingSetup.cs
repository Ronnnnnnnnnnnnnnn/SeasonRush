using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// The look of the game as a product:
    ///  * Assets/2D Asset/Logo/Game title.png  -> the title picture at the top of the main menu (MenuStyle > Title Image),
    ///  * Assets/2D Asset/Logo/Game logo.png   -> the app icon (Windows .exe, the phone's home screen) = Project Settings > Player > Icon,
    ///  * the game's name is just "Season Rush" (Project Settings > Player > Product Name: the window title, the name under the phone icon).
    /// It runs by itself when Unity loads the project (and from Rebuild Project Setup); Season Rush > Apply Game Title And Icon runs it again.
    /// </summary>
    [InitializeOnLoad]
    public static class BrandingSetup
    {
        public const string ProductName = "Season Rush";
        const string LogoDir = "Assets/2D Asset/Logo";
        const string TitlePath = LogoDir + "/Game title.png";
        const string IconPath = LogoDir + "/Game logo.png";

        static BrandingSetup()
        {
            EditorApplication.update += WaitThenApply;
        }

        static void WaitThenApply()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitThenApply;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Apply(false); }
            catch (Exception e) { Debug.LogWarning("[Setup] The game title / icon could not be applied: " + e.Message); }
        }

        [MenuItem("Season Rush/Apply Game Title And Icon")]
        public static void ApplyFromMenu()
        {
            Apply(true);
            EditorUtility.DisplayDialog("Season Rush", "The game is called \"" + ProductName + "\" now, the logo is the app icon, and the title picture is on the main menu.\n\n" +
                "Run Season Rush > Create UI Canvas In Scene if the menu still shows the plain title text.", "OK");
        }

        public static void ApplyFromCommandLine()
        {
            try { Apply(true); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        static bool Exists(string path) => File.Exists(Path.GetFullPath(path));

        public static void Apply(bool force)
        {
            bool changed = false;

            // the game's name
            if (PlayerSettings.productName != ProductName)
            {
                PlayerSettings.productName = ProductName;
                changed = true;
            }

            // the title picture on the menu
            if (Exists(TitlePath))
            {
                ImportSprite(TitlePath);
                MenuStyleSetup.Ensure();
                var style = AssetDatabase.LoadAssetAtPath<MenuStyle>(MenuStyleSetup.AssetPath);
                var title = AssetDatabase.LoadAssetAtPath<Sprite>(TitlePath);
                if (style != null && title != null && (style.titleImage == null || force) && style.titleImage != title)
                {
                    style.titleImage = title;
                    EditorUtility.SetDirty(style);
                    changed = true;
                }
            }

            // the app icon
            if (Exists(IconPath))
            {
                ImportIcon(IconPath);
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
                Texture2D[] current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
                bool has = current != null && current.Length > 0 && current[0] == icon;
                if (icon != null && !has)
                {
                    PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);      // the default icon: used by every platform without its own
                    changed = true;
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[Setup] Game name \"{ProductName}\", app icon and menu title picture are set.");
            }
        }

        static void ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;                      // sharp pixel art
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static void ImportIcon(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Default && importer.filterMode == FilterMode.Point && importer.isReadable) return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }
}

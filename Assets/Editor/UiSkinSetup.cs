using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    [InitializeOnLoad]
    public static class UiSkinSetup
    {
        const string ButtonDir = "Assets/2D Asset/Buttonborder";
        const string PanelDir = "Assets/2D Asset/Panelborder";
        const string FontDir = "Assets/Font";
        public const float Ppu = 200f;

        static UiSkinSetup()
        {
            EditorApplication.update += WaitThenApply;
        }

        static void WaitThenApply()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitThenApply;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Apply(false); }
            catch (Exception e) { Debug.LogWarning("[Setup] The pixel UI skin could not be applied: " + e.Message); }
        }

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Apply Pixel UI Skin")]
#endif
        public static void ApplyFromMenu()
        {
            bool found = Apply(true);
            EditorUtility.DisplayDialog("Season Rush", found
                ? "Done: the menu uses your wooden buttons, panels and pixel fonts.\n\nRun Season Rush > Create UI Canvas In Scene to put them on the canvas, then save the scene."
                : "The pictures are not there yet. Put them in:\n" + ButtonDir + "\n" + PanelDir + "\n" + FontDir, "OK");
        }

        public static void ApplyFromCommandLine()
        {
            try { Apply(true); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        static bool Exists(string path) => File.Exists(Path.GetFullPath(path));

        public static bool HasArt => Exists(ButtonDir + "/Btn_Spring_Large.png") && Exists(PanelDir + "/Panel_Menu.png");

        public static bool Apply(bool force)
        {
            if (!HasArt) return false;

            foreach (string size in new[] { "Large", "Medium", "Small" })
            {
                int b = size == "Large" ? 36 : size == "Medium" ? 32 : 28;
                int top = b + 6;
                foreach (string season in new[] { "Spring", "Autumn", "Winter" })
                    ImportSliced($"{ButtonDir}/Btn_{season}_{size}.png", new Vector4(b, b, b, top));
            }
            ImportSliced(ButtonDir + "/Input_Field.png", new Vector4(20, 20, 20, 20));
            ImportSliced(ButtonDir + "/Checkbox_Off.png", Vector4.zero);
            ImportSliced(ButtonDir + "/Checkbox_On.png", Vector4.zero);
            ImportSliced(PanelDir + "/Divider_Wood.png", new Vector4(6, 0, 6, 0));
            foreach (string panel in new[] { "Menu", "Profile", "Leaderboard", "LocalNetwork" })
                ImportSliced($"{PanelDir}/Panel_{panel}.png", new Vector4(10, 10, 10, 10));

            Font jersey = ImportFont(FontDir + "/Jersey_10/Jersey10-Regular.ttf");
            Font vt323 = ImportFont(FontDir + "/VT323/VT323-Regular.ttf");

            MenuStyleSetup.Ensure();
            var style = AssetDatabase.LoadAssetAtPath<MenuStyle>(MenuStyleSetup.AssetPath);
            if (style == null) return true;

            bool changed = false;
            changed |= Fill(ref style.headingFont, jersey, force);
            changed |= Fill(ref style.bodyFont, vt323, force);
            changed |= Fill(ref style.springLarge, Load("Btn_Spring_Large"), force);
            changed |= Fill(ref style.springMedium, Load("Btn_Spring_Medium"), force);
            changed |= Fill(ref style.springSmall, Load("Btn_Spring_Small"), force);
            changed |= Fill(ref style.autumnLarge, Load("Btn_Autumn_Large"), force);
            changed |= Fill(ref style.autumnMedium, Load("Btn_Autumn_Medium"), force);
            changed |= Fill(ref style.autumnSmall, Load("Btn_Autumn_Small"), force);
            changed |= Fill(ref style.winterLarge, Load("Btn_Winter_Large"), force);
            changed |= Fill(ref style.winterMedium, Load("Btn_Winter_Medium"), force);
            changed |= Fill(ref style.winterSmall, Load("Btn_Winter_Small"), force);
            changed |= Fill(ref style.panelMenu, Load("Panel_Menu"), force);
            changed |= Fill(ref style.panelProfile, Load("Panel_Profile"), force);
            changed |= Fill(ref style.panelLeaderboard, Load("Panel_Leaderboard"), force);
            changed |= Fill(ref style.panelLocalNetwork, Load("Panel_LocalNetwork"), force);
            changed |= Fill(ref style.divider, Load("Divider_Wood"), force);
            changed |= Fill(ref style.inputField, Load("Input_Field"), force);
            changed |= Fill(ref style.checkboxOff, Load("Checkbox_Off"), force);
            changed |= Fill(ref style.checkboxOn, Load("Checkbox_On"), force);

            if (changed)
            {
                EditorUtility.SetDirty(style);
                AssetDatabase.SaveAssets();
                Debug.Log("[Setup] Pixel UI skin ready: wooden buttons, panels and pixel fonts are in " + MenuStyleSetup.AssetPath);
            }
            return true;
        }

        static bool Fill<T>(ref T slot, T value, bool force) where T : UnityEngine.Object
        {
            if (value == null || slot == value || (slot != null && !force)) return false;
            slot = value;
            return true;
        }

        static Sprite Load(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite", new[] { ButtonDir, PanelDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }

        static void ImportSliced(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool ready = importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, Ppu) && importer.spriteBorder == border && importer.filterMode == FilterMode.Point;
            if (ready) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Ppu;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static Font ImportFont(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (importer != null && importer.fontRenderingMode != FontRenderingMode.HintedRaster)
            {
                importer.fontRenderingMode = FontRenderingMode.HintedRaster;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Font>(path);
        }
    }
}

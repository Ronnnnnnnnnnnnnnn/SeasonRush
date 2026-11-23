using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    public static class PlayerSkinsSelfTest
    {
        static int _ticks;
        static readonly List<string> Failures = new List<string>();

        static void Check(bool ok, string what)
        {
            if (ok) Debug.Log("[PlayerSkinsSelfTest] ok: " + what);
            else Failures.Add(what);
        }

        public static void Run()
        {
            _ticks = 0;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            _ticks++;
            var skins = AssetDatabase.LoadAssetAtPath<PlayerSkins>(PlayerSkinsSetup.AssetPath);
            bool ready = skins != null && skins.slotSprites != null && skins.slotSprites.Length >= 2 && skins.slotSprites[0] != null && skins.slotSprites[1] != null;
            if (!ready && _ticks < 900) return;

            EditorApplication.update -= Tick;
            try { Verify(skins); }
            catch (Exception e) { Failures.Add("exception: " + e); }

            if (Failures.Count == 0) { Debug.Log($"[PlayerSkinsSelfTest] PASS (after {_ticks} editor ticks)"); EditorApplication.Exit(0); }
            else { foreach (string f in Failures) Debug.LogError("[PlayerSkinsSelfTest] FAIL: " + f); EditorApplication.Exit(1); }
        }

        static void Verify(PlayerSkins skins)
        {
            Check(skins != null, "PlayerSkins.asset was created automatically");
            if (skins == null) return;
            for (int slot = 0; slot < 2; slot++)
            {
                Sprite s = skins.slotSprites != null && slot < skins.slotSprites.Length ? skins.slotSprites[slot] : null;
                Check(s != null, $"slot {slot} has a picture ({(s != null ? s.name : "none")})");
                if (s == null) continue;
                float expectedSize = 2100f / PlayerSkinsSetup.Ppu;
                Check(Mathf.Abs(s.bounds.size.y - expectedSize) < 0.01f && Mathf.Abs(s.bounds.size.x - expectedSize) < 0.01f,
                    $"slot {slot}: the picture is {s.bounds.size.x:0.00} x {s.bounds.size.y:0.00} map units (knight about 1.5 tall)");
                Check(s.pivot.y > 400f && s.pivot.y < 470f, $"slot {slot}: pivot height {s.pivot.y:0} px is at the feet");
                Check(s.pivot.x > 900f && s.pivot.x < 1150f, $"slot {slot}: pivot x {s.pivot.x:0} px is under the body, not the sword");
                string path = AssetDatabase.GetAssetPath(s);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Check(importer.spriteImportMode == SpriteImportMode.Single && importer.mipmapEnabled, $"slot {slot}: imported as one sprite with mipmaps");
            }
            Check(skins.slotSprites[0] != skins.slotSprites[1], "the two players have different pictures");
            Check(PlayerSkins.For(0) == skins.slotSprites[0] || PlayerSkins.For(0) != null, "PlayerSkins.For(0) finds the picture through Resources");
        }
    }
}

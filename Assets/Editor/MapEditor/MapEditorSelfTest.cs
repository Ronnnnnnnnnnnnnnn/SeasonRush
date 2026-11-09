using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// Automated check of the map editor's logic and preview (it cannot click in the Scene view, but everything the
    /// mouse handles call is exercised). Run in batch mode:
    /// Unity -batchmode -executeMethod SeasonRush.EditorTools.MapEditorSelfTest.Run -outDir some/folder
    /// It never modifies the real theme assets: it works on copies.
    /// </summary>
    public static class MapEditorSelfTest
    {
        static readonly List<string> Failures = new List<string>();
        static int _checks;

        static void Check(bool ok, string what)
        {
            _checks++;
            if (ok) Debug.Log("[MapEditorSelfTest] ok: " + what);
            else Failures.Add(what);
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        public static void Run()
        {
            try { Test(); }
            catch (Exception e) { Failures.Add("exception: " + e); }

            if (Failures.Count == 0)
            {
                Debug.Log($"[MapEditorSelfTest] PASS ({_checks} checks)");
                EditorApplication.Exit(0);
            }
            else
            {
                foreach (string f in Failures) Debug.LogError("[MapEditorSelfTest] FAIL: " + f);
                EditorApplication.Exit(1);
            }
        }

        static SeasonTheme Load(string name) => AssetDatabase.LoadAssetAtPath<SeasonTheme>("Assets/Data/" + name + ".asset");
        static bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < 0.002f;

        static void Test()
        {
            SeasonTheme spring = Load("SpringTheme"), fall = Load("FallTheme"), winter = Load("WinterTheme");
            Check(spring != null && fall != null && winter != null, "the three theme assets load");

            // ---------- maths ----------
            bool roundTrip = true;
            for (float x = 0.3f; x < 12f; x += 2.9f)
                for (float y = 0.4f; y < 12f; y += 3.1f)
                    roundTrip &= Near(Iso.ToPlane(Iso.ToScreen(new Vector2(x, y))), new Vector2(x, y));
            Check(roundTrip, "screen position converts back to the same map position");
            Check(Near(MapEditModel.Constrain(new Vector2(-3f, 15f), 0f), new Vector2(0f, 12f)), "positions are kept inside the island");
            Check(Near(MapEditModel.Constrain(new Vector2(3.44f, 3.46f), 0.1f), new Vector2(3.4f, 3.5f)), "snapping to 0.1");

            // ---------- the generated maps are fair ----------
            foreach (SeasonTheme t in new[] { spring, fall, winter })
            {
                int noTwin = 0, offAxis = 0;
                for (int i = 0; i < t.placements.Length; i++)
                {
                    if (MapEditModel.OnMirrorAxis(t.placements[i].position)) continue;
                    offAxis++;
                    if (MapEditModel.FindTwin(t, i) < 0) noTwin++;
                }
                Check(noTwin == 0, $"{t.displayName}: all {offAxis} off-axis props have a mirror twin");
                List<string> w = MapEditModel.Check(t);
                Check(w.Count == 0, $"{t.displayName}: map check finds nothing ({string.Join(" | ", w)})");
            }

            // ---------- editing rules (on a copy) ----------
            SeasonTheme copy = Object.Instantiate(spring);
            copy.hideFlags = HideFlags.HideAndDontSave;        // otherwise opening the scene below unloads the unreferenced copy
            int small = -1;
            for (int i = 0; i < copy.placements.Length && small < 0; i++)
                if (copy.placements[i].prop == 1 && !MapEditModel.OnMirrorAxis(copy.placements[i].position)) small = i;
            int smallTwin = MapEditModel.FindTwin(copy, small);
            Check(small >= 0 && smallTwin >= 0 && Near(copy.placements[smallTwin].position, MapEditModel.Swap(copy.placements[small].position)), "twin of a small tree found at the mirrored position");

            // a whole drag (many small moves recorded the way the Scene handles do it) is undone by ONE Ctrl+Z
            SeasonTheme undoCopy = Object.Instantiate(spring);
            Vector2 original = undoCopy.placements[small].position;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            for (int step = 1; step <= 3; step++)
            {
                Undo.RecordObject(undoCopy, "Move prop");
                MapEditModel.MovePlacement(undoCopy, small, smallTwin, new Vector2(1f + step, 2f + step));
                Undo.FlushUndoRecordObjects();
            }
            Undo.CollapseUndoOperations(group);
            Check(Near(undoCopy.placements[small].position, new Vector2(4f, 5f)), "the drag moved the prop (test set-up)");
            Undo.PerformUndo();
            Check(Near(undoCopy.placements[small].position, original) && Near(undoCopy.placements[smallTwin].position, MapEditModel.Swap(original)), "one Ctrl+Z undoes a whole drag, twin included");
            Object.DestroyImmediate(undoCopy);

            MapEditModel.MovePlacement(copy, small, smallTwin, MapEditModel.Constrain(new Vector2(4.04f, 5.06f), 0.1f));
            Check(Near(copy.placements[small].position, new Vector2(4.0f, 5.1f)) && Near(copy.placements[smallTwin].position, new Vector2(5.1f, 4.0f)), "moving a prop moves its twin to the mirrored position");
            MapEditModel.MovePlacement(copy, small, -1, new Vector2(3f, 3.4f));
            Check(Near(copy.placements[smallTwin].position, new Vector2(5.1f, 4.0f)), "with the twin switched off only one prop moves");

            int before = copy.placements.Length;
            int idx = MapEditModel.AddPlacement(copy, 6, new Vector2(3f, 8f), true, out int twin);
            Check(copy.placements.Length == before + 2 && twin == idx + 1 && copy.placements[twin].flipX && Near(copy.placements[twin].position, new Vector2(8f, 3f)) && MapEditModel.FindTwin(copy, idx) == twin, "adding with mirror creates a flipped twin");
            int axis = MapEditModel.AddPlacement(copy, 6, new Vector2(5f, 5f), true, out int axisTwin);
            Check(axisTwin == -1 && copy.placements.Length == before + 3, "a prop on the mirror line gets no twin");
            MapEditModel.RemovePlacements(copy, idx, twin, axis);
            Check(copy.placements.Length == before, "deleting a prop and its twin restores the count");

            MapEditModel.AddPlacement(copy, 6, new Vector2(2f, 9f), false, out _);
            List<string> warnings = MapEditModel.Check(copy);
            bool tooClose = false, noTwinWarn = false;
            foreach (string s in warnings) { if (s.Contains("too close")) tooClose = true; if (s.Contains("no mirror twin")) noTwinWarn = true; }
            Check(tooClose && noTwinWarn, "a lone rock next to the blue start is reported (" + string.Join(" | ", warnings) + ")");

            MapEditModel.SetType(copy, small, -1, 2);
            MapEditModel.SetFlip(copy, small, true);
            Check(copy.placements[small].prop == 2 && copy.placements[small].flipX, "type and flip can be changed");

            Check(MapEditModel.FindSpawnTwin(copy.playerSpawns, 0) == 1 && MapEditModel.FindSpawnTwin(copy.playerSpawns, 1) == 0, "the two player starts are twins");
            MapEditModel.MoveSpawn(copy.playerSpawns, 0, 1, new Vector2(2.2f, 9.0f));
            Check(Near(copy.playerSpawns[1], new Vector2(9.0f, 2.2f)), "moving a start moves its twin");
            int diag = -1, side = -1;
            for (int i = 0; i < copy.enemySpawns.Length; i++)
            {
                if (MapEditModel.OnMirrorAxis(copy.enemySpawns[i])) diag = i;
                else if (MapEditModel.FindSpawnTwin(copy.enemySpawns, i) >= 0) side = i;
            }
            Check(diag >= 0 && side >= 0, "enemy spawns: corner points have no twin, side points do");

            // ---------- preview in the scene ----------
            EditorSceneManager.OpenScene(SeasonRushSetup.ScenePath, OpenSceneMode.Single);
            var scene = EditorSceneManager.GetActiveScene();
            bool dirtyBefore = scene.isDirty;
            MapPreview.Show(spring);
            Check(MapPreview.IsShown && MapPreview.Theme == spring, "preview is shown");
            GameObject root = FindPreviewRoot();
            Check(root != null && root.hideFlags == HideFlags.HideAndDontSave, "preview root is hidden and not saved");
            Transform map = root != null ? root.transform.Find("Map_" + spring.displayName) : null;
            Check(map != null && map.Find("Ground") != null && map.Find("Props") != null, "preview contains ground and props");
            int validProps = 0;
            foreach (PropPlacement p in spring.placements) if (p.prop >= 0 && p.prop < spring.props.Length) validProps++;
            Check(map != null && map.Find("Props").childCount == validProps, $"preview has all {validProps} props, no colliders");
            Check(root != null && root.GetComponentsInChildren<Collider2D>(true).Length == 0, "the preview has no physics colliders");
            Debug.Log($"[MapEditorSelfTest] scene dirty before={dirtyBefore} after showing preview={scene.isDirty}");

            MapPreview.Show(copy);
            map = FindPreviewRoot().transform.Find("Map_" + copy.displayName);
            Vector2 where = new Vector2(7.7f, 3.3f);
            MapEditModel.MovePlacement(copy, small, -1, where);
            MapPreview.UpdateProps();
            map = FindPreviewRoot().transform.Find("Map_" + copy.displayName);
            Transform moved = map.Find("Props/" + copy.props[copy.placements[small].prop].name + "_" + small);
            Check(moved != null && Vector2.Distance(moved.position, Iso.ToScreen3(where)) < 0.001f, "UpdateProps puts the moved prop at its new screen position");
            Check(map.Find("Props").childCount == validProps + 1, "UpdateProps keeps exactly one Props object");

            // ---------- picture of the preview (with the guide markers the Scene view draws) ----------
            string outDir = Arg("-outDir");
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
                MapPreview.Show(spring);
                RenderPicture(Path.Combine(outDir, "preview_spring_default.png"), spring);
                MapPreview.Show(copy);
                RenderPicture(Path.Combine(outDir, "preview_spring_edited.png"), copy);
                MapPreview.Show(fall);
                RenderPicture(Path.Combine(outDir, "preview_fall.png"), fall);
            }

            // ---------- clean up ----------
            MapPreview.Show(spring);
            MapPreview.OnPlayModeChanged(PlayModeStateChange.ExitingEditMode);
            Check(!MapPreview.IsShown && FindPreviewRoot() == null, "pressing Play removes the preview");
            int leftoverMeshes = 0;
            foreach (Mesh m in Resources.FindObjectsOfTypeAll<Mesh>()) if (m.name == "Ground" || m.name == "Cliff" || m.name == "GroundFringe" || m.name == "Patch") leftoverMeshes++;
            Check(leftoverMeshes == 0, "the generated island meshes are freed");

            // ---------- the window opens and closes without errors ----------
            SeasonRushMapEditor.Open();
            SeasonRushMapEditor window = EditorWindow.GetWindow<SeasonRushMapEditor>();
            Check(window != null, "the Map Editor window opens");
            Check(MapPreview.IsShown, "opening the window shows the first map in the Scene view");
            window.Close();
            Check(!MapPreview.IsShown && FindPreviewRoot() == null, "closing the window removes the preview");

            Object.DestroyImmediate(copy);
            bool unchanged = Load("SpringTheme").placements.Length == spring.placements.Length;
            Check(unchanged, "the real Spring theme asset was not changed by the test");
        }

        static GameObject FindPreviewRoot()
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == MapPreview.RootName && !EditorUtility.IsPersistent(go)) return go;
            return null;
        }

        /// <summary>Renders the preview like the game camera does, then paints the markers the editor draws on top.</summary>
        static void RenderPicture(string path, SeasonTheme theme)
        {
            const int W = 1600, H = 900;
            var camGo = new GameObject("TestCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = theme.skyColor;
            float n = GameConfig.ArenaSize;
            float halfWidth = n * Iso.HalfW + 1.0f, top = n * 2f * Iso.HalfH + 2.1f, bottom = -2.3f;
            cam.orthographicSize = Mathf.Max((top - bottom) * 0.5f, halfWidth / (W / (float)H));
            cam.transform.position = new Vector3(0f, (top + bottom) * 0.5f, -10f);
            cam.aspect = W / (float)H;

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);

            void Dot(Vector2 plane, Color c, int r)
            {
                Vector3 v = cam.WorldToViewportPoint(Iso.ToScreen3(plane));
                int px = Mathf.RoundToInt(v.x * W), py = Mathf.RoundToInt(v.y * H);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = px + dx, y = py + dy;
                        if (x >= 0 && x < W && y >= 0 && y < H) tex.SetPixel(x, y, c);
                    }
            }

            for (int i = 0; i < theme.placements.Length; i++)
            {
                PropPlacement pl = theme.placements[i];
                if (pl.prop < 0 || pl.prop >= theme.props.Length) continue;
                float radius = theme.props[pl.prop].colliderRadius;
                if (radius > 0f)
                    for (int s = 0; s < 48; s++)
                    {
                        float a = s / 48f * Mathf.PI * 2f;
                        Dot(pl.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, new Color(1f, 0.55f, 0.1f), 1);
                    }
                Dot(pl.position, new Color(1f, 1f, 0.1f), 5);
            }
            for (int i = 0; i < theme.playerSpawns.Length; i++) Dot(theme.playerSpawns[i], i == 0 ? new Color(0.2f, 0.4f, 1f) : new Color(1f, 0.2f, 0.2f), 8);
            foreach (Vector2 e in theme.enemySpawns) Dot(e, new Color(0.8f, 0.2f, 1f), 6);
            for (int i = 0; i <= 48; i++)       // island outline and the mirror line
            {
                float t = i / 48f * n;
                Dot(new Vector2(t, 0f), Color.white, 1); Dot(new Vector2(t, n), Color.white, 1);
                Dot(new Vector2(0f, t), Color.white, 1); Dot(new Vector2(n, t), Color.white, 1);
                if (i % 2 == 0) Dot(new Vector2(t, t), new Color(0.4f, 0.85f, 1f), 1);
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());

            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
            Debug.Log("[MapEditorSelfTest] picture written: " + path);
        }
    }
}

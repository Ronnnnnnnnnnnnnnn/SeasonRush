using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    [InitializeOnLoad]
    public static class CharacterAnimSetup
    {
        const string PlayersDir = "Assets/2D Asset/Players";
        const string AnimationDir = "Assets/Animation";
        const string PrefabDir = "Assets/Prefabs";
        const string ThemeDir = "Assets/Data";
        const float PlayerPpu = PlayerSkinsSetup.Ppu;
        const float DryleavesPpu = 900f;
        const float SnowmanPpu = 950f;
        const int FrameTextureSize = 1024;

        class Clip
        {
            public string path;
            public string[] frames;
            public float fps;
            public bool loop;
        }

        class Character
        {
            public string name;
            public string prefab;
            public string controller;
            public float ppu;
            public string pivotFrame;
            public Clip[] clips;
            public string defaultClip;
        }

        static string P(int n, string name) => $"{PlayersDir}/p{n}_{name}.png";

        static Character[] Characters()
        {
            string winter = SeasonArtSetup.WinterDir;
            return new[]
            {
                Player(1, "Player 1.prefab", "Player 1(Blue)", "Player 1.controller"),
                Player(2, "Player 2.prefab", "Player 2(Red)", "Player 2.controller"),
                new Character
                {
                    name = "Snowman", prefab = PrefabDir + "/Enemy/Snowman.prefab", controller = AnimationDir + "/Enemy/Winter/Snowman.controller",
                    ppu = SnowmanPpu, pivotFrame = winter + "/snowman_walk1.png", defaultClip = "Walk",
                    clips = new[]
                    {
                        new Clip { path = AnimationDir + "/Enemy/Winter/Walk.anim", frames = new[] { winter + "/snowman_walk1.png", winter + "/snowman_walk2.png" }, fps = 4f, loop = true },
                        new Clip { path = AnimationDir + "/Enemy/Winter/Attack.anim", frames = new[] { winter + "/snowman_attack1.png", winter + "/snowman_attack2.png" }, fps = 6f, loop = false },
                        new Clip { path = AnimationDir + "/Enemy/Winter/Death.anim", frames = new[] { winter + "/snowman_death1.png", winter + "/snowman_death2.png", winter + "/snowman_death3.png" }, fps = 6f, loop = false }
                    }
                },
                new Character
                {
                    name = "Dryleaves", prefab = PrefabDir + "/Enemy/Dryleaves.prefab", controller = AnimationDir + "/Enemy/Fall/Dryleaves.controller",
                    ppu = DryleavesPpu, pivotFrame = "Assets/2D Asset/Falll/fall_enemy.png", defaultClip = "walk",
                    clips = new[]
                    {
                        new Clip { path = AnimationDir + "/Enemy/Fall/walk.anim", frames = new[] { "Assets/2D Asset/Falll/fall_enemy_walk1.png", "Assets/2D Asset/Falll/fall_enemy_walk2.png" }, fps = 5f, loop = true }
                    }
                }
            };
        }

        static Character Player(int n, string prefabFile, string folder, string controllerFile)
        {
            return new Character
            {
                name = "Player " + n, prefab = $"{PrefabDir}/Players/{prefabFile}", controller = $"{AnimationDir}/Players/{folder}/{controllerFile}",
                ppu = PlayerPpu, pivotFrame = P(n, "idle1"), defaultClip = "Idle",
                clips = new[]
                {
                    new Clip { path = $"{AnimationDir}/Players/{folder}/Idle.anim", frames = new[] { P(n, "idle1"), P(n, "idle2") }, fps = 3f, loop = true },
                    new Clip { path = $"{AnimationDir}/Players/{folder}/Walk.anim", frames = new[] { P(n, "walk1"), P(n, "walk2") }, fps = 8f, loop = true },
                    new Clip { path = $"{AnimationDir}/Players/{folder}/Attack.anim", frames = new[] { P(n, "attack1"), P(n, "attack2"), P(n, "attack3") }, fps = 14f, loop = false },
                    new Clip { path = $"{AnimationDir}/Players/{folder}/Death.anim", frames = new[] { P(n, "death1"), P(n, "death2"), P(n, "death3") }, fps = 6f, loop = false }
                }
            };
        }

        static CharacterAnimSetup()
        {
            EditorApplication.update += WaitThenApply;
        }

        static void WaitThenApply()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= WaitThenApply;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { ApplyAll(); }
            catch (Exception e) { Debug.LogWarning("[Setup] Character animations could not be set up: " + e.Message); }
        }

#if SEASON_RUSH_MENU
        [MenuItem("Season Rush/Set Up Character Animations")]
#endif
        public static void ApplyFromMenu()
        {
            bool changed = ApplyAll();
            EditorUtility.DisplayDialog("Season Rush", changed
                ? "Done: the players and the enemies use your animated prefabs now.\n\nSave the scene if Unity asks."
                : "Nothing to change: everything that can use your animations already does.", "OK");
        }

        public static void ApplyFromCommandLine()
        {
            try { ApplyAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError("[Setup] FAILED: " + e); EditorApplication.Exit(1); }
        }

        public static bool ApplyAll()
        {
            bool changed = RepairSpriteReferences();
            var prepared = new Dictionary<string, Sprite>();

            foreach (Character c in Characters())
            {
                if (!AllPicturesExist(c)) continue;
                changed |= PrepareCharacter(c, out Sprite first);
                prepared[c.name] = first;
            }

            changed |= PrepareCloud(out Sprite cloudFirst);
            if (cloudFirst != null) prepared["Cloud"] = cloudFirst;

            AssetDatabase.SaveAssets();
            changed |= RepairSpriteReferences();
            changed |= LinkPlayerSkins();
            changed |= LinkEnemies();
            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Setup] Character animations are set up (players: Idle / Walk / Attack, enemies: Cloud, Dryleaves, Snowman).");
            }
            return changed;
        }

        static bool Exists(string path) => File.Exists(Path.GetFullPath(path));

        static bool AllPicturesExist(Character c)
        {
            if (!Exists(c.prefab) || !Exists(c.controller)) return false;
            foreach (Clip clip in c.clips)
            {
                if (!Exists(clip.path)) return false;
                foreach (string f in clip.frames) if (!Exists(f)) return false;
            }
            return true;
        }

        static bool PrepareCharacter(Character c, out Sprite firstFrame)
        {
            bool changed = false;

            Vector2 pivot = FeetPivot(c.pivotFrame, out _);
            var frames = new List<string>();
            foreach (Clip clip in c.clips) foreach (string f in clip.frames) if (!frames.Contains(f)) frames.Add(f);
            if (!frames.Contains(c.pivotFrame)) frames.Add(c.pivotFrame);
            foreach (string f in frames) changed |= ImportFrame(f, c.ppu, pivot);

            Sprite first = null;
            foreach (Clip clip in c.clips)
            {
                var sprites = new Sprite[clip.frames.Length];
                for (int i = 0; i < sprites.Length; i++) sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(clip.frames[i]);
                if (first == null && string.Equals(Path.GetFileNameWithoutExtension(clip.path), c.defaultClip, StringComparison.OrdinalIgnoreCase)) first = sprites[0];
                changed |= FillClip(clip, sprites);
                changed |= StopLooping(clip);
            }
            firstFrame = first != null ? first : AssetDatabase.LoadAssetAtPath<Sprite>(c.pivotFrame);

            changed |= BuildController(c);

            changed |= FixPrefab(c.prefab, firstFrame);
            return changed;
        }

        static bool PrepareCloud(out Sprite first)
        {
            first = null;
            string prefab = PrefabDir + "/Enemy/Cloud.prefab";
            string controller = AnimationDir + "/Enemy/Spring/Cloud.controller";
            string clipPath = AnimationDir + "/Enemy/Spring/walk.anim";
            if (!Exists(prefab) || !Exists(controller) || !Exists(clipPath)) return false;
            if (!SeasonArtSetup.PrepareCloudFrames(out Sprite[] frames, out _) || frames.Length < 2) return false;
            first = frames[0];

            bool changed = FillClip(new Clip { path = clipPath, fps = 2.5f, loop = true }, frames);
            var c = new Character
            {
                name = "Cloud", prefab = prefab, controller = controller, defaultClip = "walk",
                clips = new[] { new Clip { path = clipPath, fps = 2.5f, loop = true } }
            };
            changed |= BuildController(c);
            changed |= FixPrefab(prefab, first);
            return changed;
        }

        static bool ImportFrame(string path, float ppu, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;
            var current = new TextureImporterSettings();
            importer.ReadTextureSettings(current);
            bool ready = importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && Mathf.Approximately(importer.spritePixelsPerUnit, ppu) && current.spriteAlignment == (int)SpriteAlignment.Custom
                && Vector2.Distance(current.spritePivot, pivot) < 0.002f && importer.maxTextureSize == FrameTextureSize;
            if (ready) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = FrameTextureSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.ReadTextureSettings(current);
            current.spriteMeshType = SpriteMeshType.FullRect;
            current.spriteAlignment = (int)SpriteAlignment.Custom;
            current.spritePivot = pivot;
            importer.SetTextureSettings(current);
            importer.SaveAndReimport();
            return true;
        }

        static Vector2 FeetPivot(string path, out int drawnHeight)
        {
            drawnHeight = 0;
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
                drawnHeight = maxY - minY + 1;

                int band = Mathf.Max(2, (int)(drawnHeight * 0.08f));
                int minX = w, maxX = -1;
                for (int y = minY; y < minY + band && y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 24) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
                if (maxX < 0) return new Vector2(0.5f, minY / (float)h);
                return new Vector2((minX + maxX) * 0.5f / w, minY / (float)h);
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        static bool FillClip(Clip spec, Sprite[] sprites)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.path);
            if (clip == null) return false;
            foreach (Sprite s in sprites) if (s == null) return false;
            if (AnimationUtility.GetObjectReferenceCurveBindings(clip).Length > 0) return false;

            clip.frameRate = 60f;
            clip.wrapMode = spec.loop ? WrapMode.Loop : WrapMode.Once;
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / spec.fps, value = sprites[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = spec.loop;
            settings.startTime = 0f;
            settings.stopTime = sprites.Length / spec.fps;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return true;
        }

        static bool StopLooping(Clip spec)
        {
            if (spec.loop || !Path.GetFileNameWithoutExtension(spec.path).Equals("Death", StringComparison.OrdinalIgnoreCase)) return false;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.path);
            if (clip == null) return false;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (!settings.loopTime) return false;
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.wrapMode = WrapMode.Once;
            EditorUtility.SetDirty(clip);
            return true;
        }

        static readonly Regex SpriteReference = new Regex(@"\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 3\}");

        static bool RepairSpriteReferences()
        {
            var single = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/2D Asset" }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (importer != null && importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) single.Add(guid);
            }
            if (single.Count == 0) return false;

            var files = new List<string>();
            if (AssetDatabase.IsValidFolder(AnimationDir))
                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationDir })) files.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (string folder in new[] { PrefabDir + "/Players", PrefabDir + "/Enemy" })
                if (AssetDatabase.IsValidFolder(folder))
                    foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder })) files.Add(AssetDatabase.GUIDToAssetPath(guid));

            bool changed = false;
            foreach (string path in files)
            {
                string full = Path.GetFullPath(path);
                string text = File.ReadAllText(full);
                if (!text.StartsWith("%YAML", StringComparison.Ordinal)) continue;
                string fixedText = SpriteReference.Replace(text, m =>
                    single.Contains(m.Groups[2].Value) && m.Groups[1].Value != "21300000"
                        ? "{fileID: 21300000, guid: " + m.Groups[2].Value + ", type: 3}"
                        : m.Value);
                if (fixedText == text) continue;
                File.WriteAllText(full, fixedText);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log("[Setup] Repaired the picture references in " + path);
                changed = true;
            }
            return changed;
        }

        static AnimationClip Load(Character c, string clipName)
        {
            foreach (Clip clip in c.clips)
                if (string.Equals(Path.GetFileNameWithoutExtension(clip.path), clipName, StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<AnimationClip>(clip.path);
            return null;
        }

        static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (string.Equals(child.state.name, name, StringComparison.OrdinalIgnoreCase)) return child.state;
            return null;
        }

        static bool AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == name)) return false;
            controller.AddParameter(name, type);
            return true;
        }

        static bool BuildController(Character c)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(c.controller);
            if (controller == null) return false;
            if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            bool changed = false;
            bool player = c.name.StartsWith("Player", StringComparison.Ordinal);
            bool snowman = c.name == "Snowman";

            if (machine.states.Length == 0)
            {
                if (player)
                {
                    AddState(machine, "Idle", Load(c, "Idle"), 0);
                    AddState(machine, "Walk", Load(c, "Walk"), 1);
                    AddState(machine, "Attack", Load(c, "Attack"), 2);
                    if (Load(c, "Death") != null) AddState(machine, "Death", Load(c, "Death"), 3);
                }
                else if (snowman)
                {
                    AddState(machine, "Walk", Load(c, "Walk"), 0);
                    AddState(machine, "Attack", Load(c, "Attack"), 1);
                    AddState(machine, "Death", Load(c, "Death"), 2);
                }
                else
                {
                    AnimationClip clip = c.clips[0] != null ? AssetDatabase.LoadAssetAtPath<AnimationClip>(c.clips[0].path) : null;
                    AddState(machine, "Walk", clip, 0);
                }
                changed = true;
            }

            bool noTransitions = machine.anyStateTransitions.Length == 0 && machine.states.All(s => s.state.transitions.Length == 0);
            AnimatorState idle = FindState(machine, "Idle"), walk = FindState(machine, "Walk"), attack = FindState(machine, "Attack"), death = FindState(machine, "Death");

            if (player && idle != null && walk != null && attack != null)
            {
                changed |= AddParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
                changed |= AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
                if (noTransitions)
                {
                    Transition(idle.AddTransition(walk), false, 0f).AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
                    Transition(walk.AddTransition(idle), false, 0f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
                    AnimatorStateTransition swing = Transition(machine.AddAnyStateTransition(attack), false, 0f);
                    swing.canTransitionToSelf = true;
                    swing.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                    Transition(attack.AddTransition(idle), true, 0f);
                    changed = true;
                }
                if (machine.defaultState != idle) { machine.defaultState = idle; changed = true; }

                if (death != null)
                {
                    changed |= AddParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);
                    bool hasDeathTransition = false;
                    foreach (AnimatorStateTransition t in machine.anyStateTransitions) if (t.destinationState == death) hasDeathTransition = true;
                    if (!hasDeathTransition)
                    {
                        AnimatorStateTransition die = Transition(machine.AddAnyStateTransition(death), false, 0f);
                        die.canTransitionToSelf = false;
                        die.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
                        Transition(death.AddTransition(idle), false, 0f).AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
                        changed = true;
                    }
                }
            }
            else if (snowman && walk != null && attack != null && death != null)
            {
                changed |= AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
                changed |= AddParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
                if (noTransitions)
                {
                    AnimatorStateTransition swing = Transition(machine.AddAnyStateTransition(attack), false, 0f);
                    swing.canTransitionToSelf = true;
                    swing.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                    Transition(attack.AddTransition(walk), true, 0f);
                    Transition(machine.AddAnyStateTransition(death), false, 0f).AddCondition(AnimatorConditionMode.If, 0f, "Die");
                    changed = true;
                }
                if (machine.defaultState != walk) { machine.defaultState = walk; changed = true; }
            }

            if (changed) EditorUtility.SetDirty(controller);
            return changed;
        }

        static AnimatorState AddState(AnimatorStateMachine machine, string name, AnimationClip clip, int row)
        {
            AnimatorState state = machine.AddState(name, new Vector3(300f, 40f + row * 70f, 0f));
            state.motion = clip;
            return state;
        }

        static AnimatorStateTransition Transition(AnimatorStateTransition t, bool exitTime, float duration)
        {
            t.hasExitTime = exitTime;
            t.exitTime = 1f;
            t.duration = duration;
            t.hasFixedDuration = true;
            return t;
        }

        static bool FixPrefab(string path, Sprite first)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                if (root.transform.localPosition != Vector3.zero) { root.transform.localPosition = Vector3.zero; changed = true; }
                var renderer = root.GetComponentInChildren<SpriteRenderer>();
                if (renderer != null && first != null && renderer.sprite != first) { renderer.sprite = first; changed = true; }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static bool LinkPlayerSkins()
        {
            string[] paths = { PrefabDir + "/Players/Player 1.prefab", PrefabDir + "/Players/Player 2.prefab" };
            if (!Exists(paths[0]) && !Exists(paths[1])) return false;

            var skins = AssetDatabase.LoadAssetAtPath<PlayerSkins>(PlayerSkinsSetup.AssetPath);
            if (skins == null)
            {
                PlayerSkinsSetup.Ensure(false);
                skins = AssetDatabase.LoadAssetAtPath<PlayerSkins>(PlayerSkinsSetup.AssetPath);
                if (skins == null) return false;
            }
            if (skins.slotPrefabs == null || skins.slotPrefabs.Length < 2) Array.Resize(ref skins.slotPrefabs, 2);

            bool changed = false;
            for (int i = 0; i < 2; i++)
            {
                if (skins.slotPrefabs[i] != null || !Exists(paths[i])) continue;
                skins.slotPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(skins);
            return changed;
        }

        static bool LinkEnemies()
        {
            bool changed = false;
            changed |= LinkEnemies("SpringTheme", PrefabDir + "/Enemy/Cloud.prefab", null, 0f);
            changed |= LinkEnemies("FallTheme", PrefabDir + "/Enemy/Dryleaves.prefab", "Assets/2D Asset/Falll/fall_enemy.png", DryleavesPpu);
            changed |= LinkEnemies("WinterTheme", PrefabDir + "/Enemy/Snowman.prefab", SeasonArtSetup.WinterDir + "/snowman_walk1.png", SnowmanPpu);
            return changed;
        }

        static bool LinkEnemies(string themeName, string prefabPath, string pictureForHeight, float ppu)
        {
            var theme = AssetDatabase.LoadAssetAtPath<SeasonTheme>($"{ThemeDir}/{themeName}.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (theme == null || prefab == null || theme.enemies == null) return false;

            Sprite first = null;
            float head = 0f;
            var renderer = prefab.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null) first = renderer.sprite;
            if (pictureForHeight != null && Exists(pictureForHeight))
            {
                FeetPivot(pictureForHeight, out int drawn);
                head = drawn / ppu;
            }

            bool changed = false;
            foreach (EnemyDef def in theme.enemies)
            {
                if (def.artFacing == ArtFacing.Unset)
                {
                    def.artFacing = ArtFacing.Left;
                    changed = true;
                }
                if (def.visualPrefab != null) continue;
                def.visualPrefab = prefab;
                string current = def.sprite != null ? AssetDatabase.GetAssetPath(def.sprite) : "";
                if (first != null && (current.Length == 0 || current.Contains("/Placeholders/"))) def.sprite = first;
                if (head > 0f && def.headHeight <= 0f) def.headHeight = head;
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(theme);
            return changed;
        }
    }
}

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
    /// <summary>
    /// Sets up the animated characters from the pictures and the (empty) Animation Clips / Animator Controllers / prefabs you made:
    ///
    ///  * Player 1 (blue) and Player 2 (red):  Assets/Prefabs/Players/Player 1.prefab, Player 2.prefab
    ///      Idle (idle1, idle2), Walk (walk1, walk2), Attack (attack1-3);  Animator: Bool IsMoving, Trigger Attack
    ///  * Spring enemy "Cloud", Fall enemy "Dryleaves", Winter enemy "Snowman":  Assets/Prefabs/Enemy/
    ///      Cloud: walk (cloud1, cloud2); Dryleaves: walk (walk1, walk2); Snowman: Walk, Attack, Death;  Animator: Trigger Attack, Trigger Die
    ///
    /// It fills only what is still EMPTY (a clip without pictures, a controller without states, an enemy without a prefab), so whatever
    /// you changed by hand stays. It runs by itself when Unity loads the project; Season Rush > Set Up Character Animations runs it again.
    /// </summary>
    [InitializeOnLoad]
    public static class CharacterAnimSetup
    {
        const string PlayersDir = "Assets/2D Asset/Players";
        const string AnimationDir = "Assets/Animation";
        const string PrefabDir = "Assets/Prefabs";
        const string ThemeDir = "Assets/Data";
        const float PlayerPpu = PlayerSkinsSetup.Ppu;       // the knight is about 1.5 map units tall
        const float DryleavesPpu = 900f;                    // about 1.1 map units tall
        const float SnowmanPpu = 950f;                      // about 1.1 map units tall
        const int FrameTextureSize = 1024;                  // the characters are drawn small on screen: no need for 2100 px

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
            public string pivotFrame;            // the picture whose feet give the pivot of every frame
            public Clip[] clips;
            public string defaultClip;           // name of the state shown first
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
                // the Spring cloud is handled separately (its pictures keep the pivot in the middle of their shadow, see SeasonArtSetup)
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

        // ------------------------------------------------------------------ entry points

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

        [MenuItem("Season Rush/Set Up Character Animations")]
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

        /// <summary>Returns true when something was changed.</summary>
        public static bool ApplyAll()
        {
            bool changed = RepairSpriteReferences();
            var prepared = new Dictionary<string, Sprite>();       // character name -> first picture of its default animation

            foreach (Character c in Characters())
            {
                if (!AllPicturesExist(c)) continue;
                changed |= PrepareCharacter(c, out Sprite first);
                prepared[c.name] = first;
            }

            // the cloud keeps its own import (pivot in the shadow); its clip and controller are filled like the others
            changed |= PrepareCloud(out Sprite cloudFirst);
            if (cloudFirst != null) prepared["Cloud"] = cloudFirst;

            AssetDatabase.SaveAssets();                     // the repair below rewrites files: save what is changed in memory first
            changed |= RepairSpriteReferences();            // pictures imported just now (for example new frames in your clips) get their right ids too
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

        // ------------------------------------------------------------------ one character

        static bool PrepareCharacter(Character c, out Sprite firstFrame)
        {
            bool changed = false;

            // 1) pictures: one shared pivot (the feet) so the character does not jump when the frames change
            Vector2 pivot = FeetPivot(c.pivotFrame, out _);
            var frames = new List<string>();
            foreach (Clip clip in c.clips) foreach (string f in clip.frames) if (!frames.Contains(f)) frames.Add(f);
            if (!frames.Contains(c.pivotFrame)) frames.Add(c.pivotFrame);
            foreach (string f in frames) changed |= ImportFrame(f, c.ppu, pivot);

            // 2) clips
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

            // 3) controller
            changed |= BuildController(c);

            // 4) prefab
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

        // ---- pictures

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

        /// <summary>The bottom-centre of the drawing's lowest part (the feet), as a pivot, and how tall the drawing is in pixels.</summary>
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

                int band = Mathf.Max(2, (int)(drawnHeight * 0.08f));                   // the lowest 8 % of the drawing = the feet
                int minX = w, maxX = -1;
                for (int y = minY; y < minY + band && y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a > 24) { if (x < minX) minX = x; if (x > maxX) maxX = x; }
                if (maxX < 0) return new Vector2(0.5f, minY / (float)h);
                return new Vector2((minX + maxX) * 0.5f / w, minY / (float)h);
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        // ---- clips

        /// <summary>Puts the pictures into an Animation Clip, but only when the clip is still empty (so your own edits are never overwritten).</summary>
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
            settings.stopTime = sprites.Length / spec.fps;            // the last picture is shown for its full time
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return true;
        }

        /// <summary>A "Death" animation plays once (not in a loop).</summary>
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

        /// <summary>
        /// The pictures used to be "Multiple" sprites (each picture had a long random id). They are single sprites now, whose id is always
        /// 21300000, so a clip or prefab that still has the old id shows NOTHING (an empty sprite). This writes the right id into the character
        /// clips and prefabs and keeps everything else of them (your frames, timing, names).
        /// </summary>
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
                if (!text.StartsWith("%YAML", StringComparison.Ordinal)) continue;                 // binary asset: not touched
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

        // ---- animator controllers

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

        /// <summary>
        /// Makes the controller work with the game: the parameters the game sets (IsMoving / Attack / Die) and the transitions between the
        /// states. A controller with no states gets states too; one that already has your states (and no transitions yet) only gets the
        /// parameters and the transitions, so your states, clips and positions stay.
        /// </summary>
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
                    // Cloud, Dryleaves: one looping walk animation
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
                    swing.canTransitionToSelf = true;                                   // swinging again restarts the attack
                    swing.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                    Transition(attack.AddTransition(idle), true, 0f);                   // back to idle / walk when the swing is over
                    changed = true;
                }
                if (machine.defaultState != idle) { machine.defaultState = idle; changed = true; }

                // dying: the game sets the Bool IsDead; the death animation plays once and stays on its last picture until the respawn
                if (death != null)
                {
                    changed |= AddParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);
                    bool hasDeathTransition = false;
                    foreach (AnimatorStateTransition t in machine.anyStateTransitions) if (t.destinationState == death) hasDeathTransition = true;
                    if (!hasDeathTransition)
                    {
                        AnimatorStateTransition die = Transition(machine.AddAnyStateTransition(death), false, 0f);
                        die.canTransitionToSelf = false;                         // a Bool condition stays true: do not restart the animation every frame
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

        // ---- prefabs

        /// <summary>The prefab's root sits at 0,0 and shows the first picture of its default animation.</summary>
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

        // ------------------------------------------------------------------ hooking the prefabs into the game

        /// <summary>PlayerSkins.asset gets the two player prefabs (Player 1 = slot 0, Player 2 = slot 1).</summary>
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

        /// <summary>Spring = Cloud, Fall = Dryleaves, Winter = Snowman: the three enemies of every season get that prefab (when they have none).</summary>
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
                head = drawn / ppu;                                  // how high the picture reaches above the feet at scale 1
            }

            bool changed = false;
            foreach (EnemyDef def in theme.enemies)
            {
                if (def.visualPrefab != null) continue;              // already chosen (by you or an earlier run)
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

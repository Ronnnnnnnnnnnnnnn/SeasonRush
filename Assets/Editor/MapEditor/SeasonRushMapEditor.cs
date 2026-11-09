using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// Visual map editor (menu: Season Rush > Map Editor).
    ///
    /// Shows the season map in the Scene view while the game is not running. Drag a tree or rock to move it, click
    /// "Place with mouse" to drop new ones, Delete removes the selected one. With "mirror twin" on, every change is
    /// applied to the matching prop on the other side, so both players keep the same cover.
    /// Everything is stored in the Season Theme asset (Assets/Data), exactly like editing it in the Inspector.
    /// </summary>
    public class SeasonRushMapEditor : EditorWindow
    {
        enum Kind { None, Prop, PlayerSpawn, EnemySpawn }

        static readonly float[] SnapValues = { 0f, 0.05f, 0.1f, 0.25f, 0.5f };
        static readonly string[] SnapNames = { "Off", "0.05", "0.1", "0.25", "0.5" };
        static readonly int HandleHash = "SeasonRushMapHandle".GetHashCode();
        static readonly int EmptyHash = "SeasonRushMapEmpty".GetHashCode();
        static readonly int PlaceHash = "SeasonRushMapPlace".GetHashCode();

        static readonly Color Selected = new Color(1f, 0.9f, 0.2f);
        static readonly Color TwinColor = new Color(0.3f, 0.9f, 1f);
        static readonly Color Normal = new Color(1f, 1f, 1f, 0.9f);
        static readonly Color Decoration = new Color(0.7f, 1f, 0.7f, 0.9f);
        static readonly Color BlueStart = new Color(0.35f, 0.55f, 1f);
        static readonly Color RedStart = new Color(1f, 0.4f, 0.4f);
        static readonly Color EnemyStart = new Color(0.9f, 0.4f, 1f);

        [SerializeField] SeasonTheme _theme;
        [SerializeField] bool _show = true;
        [SerializeField] bool _mirror = true;
        [SerializeField] float _snap = 0.1f;
        [SerializeField] bool _showColliders = true;
        [SerializeField] bool _showLabels = true;
        [SerializeField] bool _showSpawns = true;
        [SerializeField] Kind _selKind = Kind.None;
        [SerializeField] int _selIndex = -1;
        [SerializeField] int _placeProp = -1;
        [SerializeField] Vector2 _scroll;
        [SerializeField] bool _listOpen;

        List<SeasonTheme> _themes = new List<SeasonTheme>();
        bool _framed;

        // state of the drag in progress (not saved)
        int _dragIndex = -1;
        int _dragTwin = -1;
        int _undoGroup;
        Vector2 _dragGrab;
        bool _dragMoved;

        GUIStyle _labelStyle;

        [MenuItem("Season Rush/Map Editor (move trees and rocks)")]
        public static void Open()
        {
            var window = GetWindow<SeasonRushMapEditor>("Map Editor");
            window.minSize = new Vector2(330f, 440f);
            window.Show();
        }

        // ================================================================== life cycle

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            RefreshThemes();
            if (_theme == null && _themes.Count > 0) _theme = _themes[0];
            SyncPreview();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            MapPreview.Clear();
            SceneView.RepaintAll();
        }

        void OnFocus() => RefreshThemes();

        void RefreshThemes()
        {
            _themes = new List<SeasonTheme>();
            foreach (string guid in AssetDatabase.FindAssets("t:SeasonTheme"))
            {
                var theme = AssetDatabase.LoadAssetAtPath<SeasonTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (theme != null) _themes.Add(theme);
            }
            _themes.Sort((a, b) => a.season.CompareTo(b.season));
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) { SyncPreview(); Repaint(); }
        }

        void OnUndoRedo()
        {
            if (_theme == null) return;
            if (_selKind == Kind.Prop && !MapEditModel.IsValid(_theme, _selIndex)) ClearSelection();
            MapPreview.UpdateProps();
            SceneView.RepaintAll();
            Repaint();
        }

        /// <summary>Makes the preview match the window: shown for the chosen season, or removed.</summary>
        void SyncPreview()
        {
            bool want = _show && _theme != null && !EditorApplication.isPlayingOrWillChangePlaymode;
            if (!want)
            {
                if (MapPreview.IsShown) { MapPreview.Clear(); SceneView.RepaintAll(); }
                return;
            }
            if (!MapPreview.IsShown || MapPreview.Theme != _theme)
            {
                MapPreview.Show(_theme);
                if (!_framed) { _framed = true; FrameMap(); }
                SceneView.RepaintAll();
            }
        }

        static void FrameMap()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null) view = GetWindow<SceneView>();
            if (view == null) return;
            view.in2DMode = true;
            view.LookAt(new Vector3(0f, 4.7f, 0f), Quaternion.identity, 7.5f, true, false);
        }

        void ClearSelection()
        {
            _selKind = Kind.None;
            _selIndex = -1;
        }

        /// <summary>Call after any change to the theme: refresh preview, save, redraw.</summary>
        void Commit(bool save)
        {
            EditorUtility.SetDirty(_theme);
            MapPreview.UpdateProps();
            if (save) AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            Repaint();
        }

        // ================================================================== window

        void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Stop Play mode to edit the maps.", MessageType.Info);
                return;
            }
            if (_themes.Count == 0)
            {
                EditorGUILayout.HelpBox("No Season Theme assets found. Run Season Rush > Rebuild Project Setup first.", MessageType.Warning);
                if (GUILayout.Button("Look again")) RefreshThemes();
                return;
            }

            SyncPreview();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawThemeToolbar();
            DrawViewOptions();
            if (_theme != null)
            {
                DrawAddSection();
                DrawSelection();
                DrawWarnings();
                DrawList();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawThemeToolbar()
        {
            EditorGUILayout.Space(4);
            var names = new string[_themes.Count];
            for (int i = 0; i < names.Length; i++) names[i] = _themes[i].displayName;
            int current = Mathf.Max(0, _themes.IndexOf(_theme));
            int chosen = GUILayout.Toolbar(current, names, GUILayout.Height(28));
            if (chosen != current || _theme == null)
            {
                _theme = _themes[chosen];
                ClearSelection();
                _placeProp = -1;
                SyncPreview();
            }
        }

        void DrawViewOptions()
        {
            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            _show = EditorGUILayout.ToggleLeft("Show this map in the Scene view", _show);
            if (EditorGUI.EndChangeCheck()) { SyncPreview(); if (_show) FrameMap(); }

            if (GUILayout.Button("Frame the map in the Scene view")) FrameMap();

            _mirror = EditorGUILayout.ToggleLeft("Keep the mirror twin in step (fair for both players)", _mirror);

            int snapIndex = Mathf.Max(0, System.Array.IndexOf(SnapValues, _snap));
            int newSnap = EditorGUILayout.Popup("Snap to grid", snapIndex, SnapNames);
            _snap = SnapValues[newSnap];

            EditorGUI.BeginChangeCheck();
            _showColliders = EditorGUILayout.ToggleLeft("Show blocking circles", _showColliders);
            _showLabels = EditorGUILayout.ToggleLeft("Show names", _showLabels);
            _showSpawns = EditorGUILayout.ToggleLeft("Show and move start points", _showSpawns);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        }

        void DrawAddSection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Add a prop", EditorStyles.boldLabel);
            if (_theme.props == null || _theme.props.Length == 0)
            {
                EditorGUILayout.HelpBox("This theme has no prop types yet. Add some to its Props list in the Inspector.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _theme.props.Length; i++)
            {
                PropDef def = _theme.props[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    Texture2D icon = null;
                    if (def.sprite != null)
                    {
                        icon = AssetPreview.GetAssetPreview(def.sprite);
                        if (icon == null && AssetPreview.IsLoadingAssetPreview(def.sprite.GetInstanceID())) Repaint();
                    }
                    GUILayout.Label(icon, GUILayout.Width(34f), GUILayout.Height(34f));
                    GUILayout.Label($"{i}  {def.name}", GUILayout.MinWidth(70f), GUILayout.Height(34f));
                    GUILayout.FlexibleSpace();

                    bool placing = _placeProp == i;
                    bool toggled = GUILayout.Toggle(placing, "Place with mouse", "Button", GUILayout.Width(118f), GUILayout.Height(34f));
                    if (toggled != placing)
                    {
                        _placeProp = toggled ? i : -1;
                        if (toggled) { SyncPreview(); SceneView view = SceneView.lastActiveSceneView; if (view != null) view.Focus(); }
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("Add", GUILayout.Width(44f), GUILayout.Height(34f))) AddNearCentre(i);
                }
            }
            if (_placeProp >= 0)
                EditorGUILayout.HelpBox("Click in the Scene view to drop it (it stays active so you can place several). Esc or right-click stops.", MessageType.None);
        }

        void DrawSelection()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Selected", EditorStyles.boldLabel);

            if (_selKind == Kind.Prop && MapEditModel.IsValid(_theme, _selIndex))
            {
                PropPlacement pl = _theme.placements[_selIndex];
                int twin = MapEditModel.FindTwin(_theme, _selIndex);
                int follower = _mirror ? twin : -1;
                var typeNames = new string[_theme.props != null ? _theme.props.Length : 0];
                for (int i = 0; i < typeNames.Length; i++) typeNames[i] = i + "  " + _theme.props[i].name;

                EditorGUILayout.LabelField($"{MapEditModel.PropName(_theme, pl.prop)}  #{_selIndex}" + (twin >= 0 ? $"   (twin #{twin})" : "   (no twin)"));
                EditorGUI.BeginChangeCheck();
                int newProp = typeNames.Length > 0 ? EditorGUILayout.Popup("Type", Mathf.Clamp(pl.prop, 0, typeNames.Length - 1), typeNames) : pl.prop;
                Vector2 newPos = EditorGUILayout.Vector2Field("Position (X, Y)", pl.position);
                bool newFlip = EditorGUILayout.Toggle("Flip X", pl.flipX);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_theme, "Edit prop");
                    if (newProp != pl.prop) MapEditModel.SetType(_theme, _selIndex, follower, newProp);
                    if (newPos != pl.position) MapEditModel.MovePlacement(_theme, _selIndex, follower, MapEditModel.Constrain(newPos, 0f));
                    if (newFlip != pl.flipX) MapEditModel.SetFlip(_theme, _selIndex, newFlip);
                    Commit(true);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Duplicate")) DuplicateSelected();
                    if (GUILayout.Button("Delete")) DeleteSelected();
                    using (new EditorGUI.DisabledScope(twin < 0))
                        if (GUILayout.Button("Select twin")) { _selIndex = twin; SceneView.RepaintAll(); }
                }
            }
            else if ((_selKind == Kind.PlayerSpawn || _selKind == Kind.EnemySpawn) && SpawnList(_selKind) != null && _selIndex >= 0 && _selIndex < SpawnList(_selKind).Length)
            {
                Vector2[] list = SpawnList(_selKind);
                EditorGUILayout.LabelField(SpawnLabel(_selKind, _selIndex));
                EditorGUI.BeginChangeCheck();
                Vector2 newPos = EditorGUILayout.Vector2Field("Position (X, Y)", list[_selIndex]);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_theme, "Move start point");
                    int twin = _mirror ? MapEditModel.FindSpawnTwin(list, _selIndex) : -1;
                    MapEditModel.MoveSpawn(list, _selIndex, twin, MapEditModel.Constrain(newPos, 0f));
                    Commit(true);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Click a prop (or a start point) in the Scene view, then drag it. Delete removes the selected prop.", MessageType.None);
            }
        }

        void DrawWarnings()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Map check", EditorStyles.boldLabel);
            List<string> warnings = MapEditModel.Check(_theme);
            if (warnings.Count == 0)
            {
                EditorGUILayout.LabelField("No problems found.");
                return;
            }
            foreach (string w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
        }

        void DrawList()
        {
            EditorGUILayout.Space(8);
            int count = MapEditModel.PlacementCount(_theme);
            _listOpen = EditorGUILayout.Foldout(_listOpen, $"All props on this map ({count})", true);
            if (!_listOpen) return;
            for (int i = 0; i < count; i++)
            {
                PropPlacement pl = _theme.placements[i];
                bool selected = _selKind == Kind.Prop && _selIndex == i;
                string text = $"#{i}  {MapEditModel.PropName(_theme, pl.prop)}   ({pl.position.x:0.0}, {pl.position.y:0.0})";
                bool pressed = GUILayout.Toggle(selected, text, "Button");
                if (pressed && !selected) { _selKind = Kind.Prop; _selIndex = i; SceneView.RepaintAll(); }
            }
        }

        // ================================================================== actions

        void AddNearCentre(int prop)
        {
            Vector2 at = MapEditModel.Constrain(new Vector2(4.8f, 7.2f), _snap);       // off the mirror line: the twin lands at (7.2, 4.8)
            AddProp(prop, at);
        }

        void AddProp(int prop, Vector2 position)
        {
            Undo.RecordObject(_theme, "Add prop");
            int index = MapEditModel.AddPlacement(_theme, prop, position, _mirror, out _);
            _selKind = Kind.Prop;
            _selIndex = index;
            Commit(true);
        }

        void DuplicateSelected()
        {
            if (_selKind != Kind.Prop || !MapEditModel.IsValid(_theme, _selIndex)) return;
            PropPlacement pl = _theme.placements[_selIndex];
            Vector2 at = MapEditModel.Constrain(pl.position + new Vector2(0.8f, -0.8f), _snap);
            AddProp(pl.prop, at);
        }

        void DeleteSelected()
        {
            if (_selKind != Kind.Prop || !MapEditModel.IsValid(_theme, _selIndex)) return;
            Undo.RecordObject(_theme, "Delete prop");
            int twin = _mirror ? MapEditModel.FindTwin(_theme, _selIndex) : -1;
            MapEditModel.RemovePlacements(_theme, _selIndex, twin);
            ClearSelection();
            Commit(true);
        }

        Vector2[] SpawnList(Kind kind)
        {
            if (_theme == null) return null;
            return kind == Kind.PlayerSpawn ? _theme.playerSpawns : kind == Kind.EnemySpawn ? _theme.enemySpawns : null;
        }

        static string SpawnLabel(Kind kind, int index)
        {
            if (kind == Kind.PlayerSpawn) return index == 0 ? "Blue player's start (the host)" : "Red player's start";
            return "Enemy spawn point " + (index + 1);
        }

        // ================================================================== Scene view

        bool Editable => _show && _theme != null && MapPreview.IsShown && MapPreview.Theme == _theme && !EditorApplication.isPlayingOrWillChangePlaymode;

        static bool MousePlane(Event e, out Vector2 plane)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var ground = new Plane(Vector3.back, Vector3.zero);                        // the z = 0 plane the map is drawn on
            if (ground.Raycast(ray, out float distance))
            {
                Vector3 p = ray.GetPoint(distance);
                plane = Iso.ToPlane(new Vector2(p.x, p.y));
                return true;
            }
            plane = Iso.ToPlane(new Vector2(ray.origin.x, ray.origin.y));              // orthographic fallback
            return true;
        }

        void OnSceneGUI(SceneView view)
        {
            if (_theme == null) return;
            SyncPreview();
            if (!Editable) return;

            Event e = Event.current;
            var oldZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            if (e.type == EventType.Repaint) DrawGuides();

            // clicking empty space clears the selection (and does not pick other scene objects)
            int emptyId = GUIUtility.GetControlID(EmptyHash, FocusType.Passive);
            if (_placeProp < 0)
            {
                switch (e.GetTypeForControl(emptyId))
                {
                    case EventType.Layout:
                        HandleUtility.AddDefaultControl(emptyId);
                        break;
                    case EventType.MouseDown:
                        if (e.button == 0 && !e.alt && HandleUtility.nearestControl == emptyId)
                        {
                            GUIUtility.hotControl = emptyId;
                            e.Use();
                        }
                        break;
                    case EventType.MouseUp:
                        if (GUIUtility.hotControl == emptyId)
                        {
                            GUIUtility.hotControl = 0;
                            ClearSelection();
                            e.Use();
                            Repaint();
                        }
                        break;
                }
            }

            int twinOfSelected = _selKind == Kind.Prop ? MapEditModel.FindTwin(_theme, _selIndex) : -1;

            if (_showSpawns)
            {
                DoSpawnHandles(Kind.PlayerSpawn, _theme.playerSpawns);
                DoSpawnHandles(Kind.EnemySpawn, _theme.enemySpawns);
            }
            for (int i = 0; i < MapEditModel.PlacementCount(_theme); i++) DoPropHandle(i, twinOfSelected);

            DoPlaceMode(e);
            DoKeys(e);
            DrawHelp();

            Handles.zTest = oldZTest;
            if (e.type == EventType.MouseMove) view.Repaint();
        }

        void DrawGuides()
        {
            float n = MapEditModel.Limit;
            Handles.color = new Color(1f, 1f, 1f, 0.4f);
            Handles.DrawAAPolyLine(2f,
                Iso.ToScreen3(new Vector2(0f, 0f)), Iso.ToScreen3(new Vector2(n, 0f)),
                Iso.ToScreen3(new Vector2(n, n)), Iso.ToScreen3(new Vector2(0f, n)), Iso.ToScreen3(new Vector2(0f, 0f)));
            Handles.color = new Color(0.4f, 0.85f, 1f, 0.55f);
            Handles.DrawDottedLine(Iso.ToScreen3(new Vector2(0f, 0f)), Iso.ToScreen3(new Vector2(n, n)), 6f);
            EnsureStyle();
            Handles.Label(Iso.ToScreen3(new Vector2(n, n)) + new Vector3(0.15f, 0.1f, 0f), "mirror line", _labelStyle);
        }

        void EnsureStyle()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            _labelStyle.normal.textColor = Color.white;
        }

        void DoPropHandle(int i, int twinOfSelected)
        {
            PropPlacement pl = _theme.placements[i];
            Vector3 pos = Iso.ToScreen3(pl.position);
            bool valid = _theme.props != null && pl.prop >= 0 && pl.prop < _theme.props.Length;
            PropDef def = valid ? _theme.props[pl.prop] : null;
            bool selected = _selKind == Kind.Prop && _selIndex == i;
            bool isTwin = i == twinOfSelected && _mirror;

            Color colour = selected ? Selected : isTwin ? TwinColor : (def != null && def.colliderRadius <= 0f ? Decoration : Normal);
            string label = def != null ? $"{def.name} #{i}" : $"? #{i}";
            DoHandle(Kind.Prop, i, pos, colour, selected || isTwin, label, def != null ? def.colliderRadius : 0f, pl.position);
        }

        void DoSpawnHandles(Kind kind, Vector2[] list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Length; i++)
            {
                Color colour = kind == Kind.EnemySpawn ? EnemyStart : (i == 0 ? BlueStart : RedStart);
                bool selected = _selKind == kind && _selIndex == i;
                DoHandle(kind, i, Iso.ToScreen3(list[i]), selected ? Selected : colour, selected, SpawnLabel(kind, i), 0f, list[i]);
            }
        }

        /// <summary>One draggable marker. Used for props and start points.</summary>
        void DoHandle(Kind kind, int index, Vector3 pos, Color colour, bool emphasise, string label, float colliderRadius, Vector2 plane)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(HandleHash, FocusType.Passive);
            float size = HandleUtility.GetHandleSize(pos);

            switch (e.GetTypeForControl(id))
            {
                case EventType.Layout:
                    if (_placeProp < 0) HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(pos, size * 0.16f));
                    break;

                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && _placeProp < 0 && HandleUtility.nearestControl == id)
                    {
                        GUIUtility.hotControl = id;
                        _selKind = kind;
                        _selIndex = index;
                        _dragIndex = index;
                        _dragMoved = false;
                        Vector2 mouse;
                        MousePlane(e, out mouse);
                        _dragGrab = plane - mouse;
                        _dragTwin = -1;
                        if (_mirror)
                            _dragTwin = kind == Kind.Prop ? MapEditModel.FindTwin(_theme, index) : MapEditModel.FindSpawnTwin(SpawnList(kind), index);
                        Undo.IncrementCurrentGroup();                    // the whole drag becomes one Ctrl+Z step
                        _undoGroup = Undo.GetCurrentGroup();
                        Undo.RecordObject(_theme, kind == Kind.Prop ? "Move prop" : "Move start point");
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id && e.button == 0)
                    {
                        Vector2 mouse;
                        MousePlane(e, out mouse);
                        Vector2 target = MapEditModel.Constrain(mouse + _dragGrab, _snap);
                        Undo.RecordObject(_theme, kind == Kind.Prop ? "Move prop" : "Move start point");
                        if (kind == Kind.Prop) MapEditModel.MovePlacement(_theme, _dragIndex, _dragTwin, target);
                        else MapEditModel.MoveSpawn(SpawnList(kind), _dragIndex, _dragTwin, target);
                        _dragMoved = true;
                        EditorUtility.SetDirty(_theme);
                        if (kind == Kind.Prop) MapPreview.UpdateProps();
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id && e.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        Undo.CollapseUndoOperations(_undoGroup);
                        if (_dragMoved) AssetDatabase.SaveAssets();
                        _dragIndex = -1;
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.Repaint:
                    bool hover = HandleUtility.nearestControl == id && GUIUtility.hotControl == 0;
                    DrawMarker(pos, colour, size, emphasise || hover, hover || emphasise || _showLabels, label, colliderRadius, plane, kind);
                    break;
            }
        }

        void DrawMarker(Vector3 pos, Color colour, float size, bool strong, bool withLabel, string label, float colliderRadius, Vector2 plane, Kind kind)
        {
            if (_showColliders && colliderRadius > 0f)
            {
                // the blocking circle lives on the flat plane; on screen it is an ellipse
                const int steps = 40;
                var ring = new Vector3[steps + 1];
                for (int s = 0; s <= steps; s++)
                {
                    float a = s / (float)steps * Mathf.PI * 2f;
                    ring[s] = Iso.ToScreen3(plane + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * colliderRadius);
                }
                Handles.color = new Color(1f, 0.6f, 0.2f, strong ? 0.95f : 0.55f);
                Handles.DrawAAPolyLine(strong ? 3f : 2f, ring);
            }

            float radius = size * (kind == Kind.Prop ? 0.07f : 0.1f) * (strong ? 1.35f : 1f);
            Handles.color = new Color(colour.r, colour.g, colour.b, strong ? 0.55f : 0.3f);
            Handles.DrawSolidDisc(pos, Vector3.back, radius);
            Handles.color = colour;
            Handles.DrawWireDisc(pos, Vector3.back, radius);

            if (withLabel)
            {
                EnsureStyle();
                Handles.Label(pos + new Vector3(radius * 1.2f, radius * 1.6f, 0f), label, _labelStyle);
            }
        }

        void DoPlaceMode(Event e)
        {
            if (_placeProp < 0) return;
            if (_theme.props == null || _placeProp >= _theme.props.Length) { _placeProp = -1; return; }

            int id = GUIUtility.GetControlID(PlaceHash, FocusType.Passive);
            Vector2 plane;
            MousePlane(e, out plane);
            Vector2 snapped = MapEditModel.Constrain(plane, _snap);

            switch (e.GetTypeForControl(id))
            {
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(id);
                    break;
                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt)
                    {
                        AddProp(_placeProp, snapped);
                        e.Use();
                    }
                    else if (e.button == 1)
                    {
                        _placeProp = -1;
                        e.Use();
                        Repaint();
                    }
                    break;
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape) { _placeProp = -1; e.Use(); Repaint(); }
                    break;
                case EventType.Repaint:
                    PropDef def = _theme.props[_placeProp];
                    Vector3 p = Iso.ToScreen3(snapped);
                    float size = HandleUtility.GetHandleSize(p);
                    DrawMarker(p, new Color(0.4f, 1f, 0.5f), size, true, true, "place: " + def.name, def.colliderRadius, snapped, Kind.Prop);
                    if (_mirror && !MapEditModel.OnMirrorAxis(snapped))
                    {
                        Vector2 twin = MapEditModel.Swap(snapped);
                        DrawMarker(Iso.ToScreen3(twin), new Color(0.4f, 1f, 0.5f), size, false, false, "", def.colliderRadius, twin, Kind.Prop);
                    }
                    break;
            }
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();
        }

        void DoKeys(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && _selKind == Kind.Prop && MapEditModel.IsValid(_theme, _selIndex))
            {
                DeleteSelected();
                e.Use();
            }
        }

        void DrawHelp()
        {
            Handles.BeginGUI();
            string mode = _placeProp >= 0
                ? "Click to place. Esc or right-click stops."
                : "Drag a prop to move it.  Delete removes the selected one.";
            string text = $"Map Editor - {_theme.displayName}     mirror twin {(_mirror ? "ON" : "OFF")}     snap {SnapNames[Mathf.Max(0, System.Array.IndexOf(SnapValues, _snap))]}\n{mode}";
            GUI.Label(new Rect(10f, 10f, 430f, 40f), text, EditorStyles.helpBox);
            Handles.EndGUI();
        }
    }
}

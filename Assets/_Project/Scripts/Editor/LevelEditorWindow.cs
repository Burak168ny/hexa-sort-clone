using System;
using System.Collections.Generic;
using HexaSort.Core.Levels;
using HexaSort.Model;
using HexaSort.Model.Simulation;
using HexaSort.View;
using UnityEditor;
using UnityEngine;

namespace HexaSort.Editor
{
    /// <summary>
    /// Hex Board Editor: pick a level, paint its board with the mouse, tune its generator and goal,
    /// validate it and estimate difficulty with the simulation bot. Edits are undoable and saved
    /// straight into the LevelData asset.
    /// </summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private enum Tool { Cells, Locks, Stacks }

        private const int GridRadius = 6;
        private const float ListWidth = 210f;
        private const float InspectorWidth = 320f;

        private static readonly Color Background = new Color(0.13f, 0.14f, 0.19f);
        private static readonly Color GhostOutline = new Color(1f, 1f, 1f, 0.08f);
        private static readonly Color CellFill = new Color(0.27f, 0.31f, 0.45f);
        private static readonly Color LockedFill = new Color(0.16f, 0.17f, 0.24f);
        private static readonly Color HoverOutline = new Color(1f, 0.85f, 0.3f);

        private LevelDatabase _database;
        private int _selected;
        private Tool _tool;
        private int _lockScore = 50;
        private readonly List<int> _brush = new List<int> { 0, 0, 1 };
        private Color[] _palette;
        private Vector2 _listScroll;
        private Vector2 _inspectorScroll;
        private string _validation;
        private bool _validationOk;
        private string _simulation;
        private int _simulationGames = 60;
        private HexCoord? _hover;
        private int _shapePreset;

        private static readonly string[] ShapeNames =
            { "Hexagon r2 (19)", "Hexagon r3 (37)", "Rectangle 3x4", "Rectangle 4x5", "Rectangle 5x6", "Rectangle 5x7", "Ring r3 hole 1", "Diamond 5x5" };

        private LevelData Current => _database != null ? _database.Get(_selected + 1) : null;

        [MenuItem("Tools/HexaSort/Level Editor", priority = 10)]
        public static void Open()
        {
            var window = GetWindow<LevelEditorWindow>("Hex Board Editor");
            window.minSize = new Vector2(900f, 560f);
        }

        private void OnEnable()
        {
            _database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(CampaignAssetBuilder.DatabasePath);
            var config = AssetDatabase.LoadAssetAtPath<VisualConfig>("Assets/_Project/Settings/VisualConfig.asset");
            _palette = config != null ? config.Palette : new[] { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta, new Color(1f, 0.5f, 0f), Color.cyan, Color.white };
            Undo.undoRedoPerformed += Repaint;
            wantsMouseMove = true;
        }

        private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

        private void OnGUI()
        {
            if (_database == null)
            {
                DrawMissingDatabase();
                return;
            }
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _database.Count - 1));

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLevelList();
                DrawCanvas();
                DrawInspector();
            }
        }

        // ---- Left: level list ----

        private void DrawMissingDatabase()
        {
            EditorGUILayout.HelpBox($"No level database at {CampaignAssetBuilder.DatabasePath}.", MessageType.Info);
            if (GUILayout.Button("Create from built-in campaign", GUILayout.Height(30)))
                _database = CampaignAssetBuilder.Rebuild();
        }

        private void DrawLevelList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
            {
                EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
                for (int i = 0; i < _database.Count; i++)
                {
                    var level = _database.Levels[i];
                    string label = level == null ? $"{i + 1:00}  <missing>" : $"{i + 1:00}  {level.DisplayName}";
                    var style = i == _selected ? EditorStyles.toolbarButton : EditorStyles.label;
                    if (GUILayout.Toggle(i == _selected, label, style) && i != _selected) Select(i);
                }
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add")) AddLevel(null);
                    GUI.enabled = Current != null;
                    if (GUILayout.Button("Duplicate")) AddLevel(Current);
                    if (GUILayout.Button("Delete")) DeleteSelected();
                    GUI.enabled = true;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = _selected > 0;
                    if (GUILayout.Button("▲ Up")) Move(-1);
                    GUI.enabled = _selected < _database.Count - 1;
                    if (GUILayout.Button("▼ Down")) Move(+1);
                    GUI.enabled = true;
                }
                if (GUILayout.Button("Rebuild campaign…")) CampaignAssetBuilder.RebuildWithConfirmation();
                if (GUILayout.Button("Ping asset") && Current != null) EditorGUIUtility.PingObject(Current);
            }
        }

        private void Select(int index)
        {
            _selected = index;
            _validation = null;
            _simulation = null;
            GUI.FocusControl(null);
        }

        private void AddLevel(LevelData template)
        {
            LevelData data;
            if (template != null)
            {
                data = Instantiate(template);
                data.DisplayName = template.DisplayName + " Copy";
            }
            else
            {
                data = CreateInstance<LevelData>();
                data.CopyFrom(ProceduralLevels.Create(4));
                data.DisplayName = "New Level";
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(CampaignAssetBuilder.LevelPath(_database.Count + 1));
            AssetDatabase.CreateAsset(data, path);

            Undo.RecordObject(_database, "Add level");
            int index = template != null ? _selected + 1 : _database.Count;
            _database.Levels.Insert(index, data);
            EditorUtility.SetDirty(_database);
            AssetDatabase.SaveAssets();
            Select(index);
        }

        private void DeleteSelected()
        {
            var level = Current;
            if (!EditorUtility.DisplayDialog("Delete level", $"Delete level {_selected + 1} ({level.DisplayName}) and its asset?", "Delete", "Cancel"))
                return;
            _database.Levels.RemoveAt(_selected);
            EditorUtility.SetDirty(_database);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(level));
            AssetDatabase.SaveAssets();
            Select(Mathf.Max(0, _selected - 1));
        }

        private void Move(int delta)
        {
            Undo.RecordObject(_database, "Reorder levels");
            var list = _database.Levels;
            (list[_selected], list[_selected + delta]) = (list[_selected + delta], list[_selected]);
            EditorUtility.SetDirty(_database);
            Select(_selected + delta);
        }

        // ---- Centre: board canvas ----

        private void DrawCanvas()
        {
            var rect = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var level = Current;
            EditorGUI.DrawRect(rect, Background);
            if (level == null) return;

            float size = Mathf.Min(rect.width / (3f * GridRadius + 2f), rect.height / (HexLayout.Sqrt3 * (2f * GridRadius + 1f)));
            Vector2 centre = rect.center;
            HandleInput(rect, centre, size, level);

            if (Event.current.type != EventType.Repaint) return;

            foreach (var coord in BoardShapes.Hexagon(GridRadius))
            {
                Vector2 p = ToScreen(coord, centre, size);
                int cellIndex = level.IndexOfCell(coord);
                if (cellIndex < 0)
                {
                    DrawHex(p, size * 0.96f, null, GhostOutline);
                    continue;
                }

                var cell = level.Cells[cellIndex];
                DrawHex(p, size * 0.94f, cell.Locked ? LockedFill : CellFill, new Color(0f, 0f, 0f, 0.5f));
                if (cell.Locked) DrawLabel(p, $"LOCK\n{cell.UnlockScore}", size, Color.white);

                int stackIndex = level.IndexOfStack(coord);
                if (stackIndex >= 0) DrawStack(p, size, level.Stacks[stackIndex].Colors);
            }

            if (_hover.HasValue) DrawHex(ToScreen(_hover.Value, centre, size), size * 0.98f, null, HoverOutline, 3f);

            var info = new Rect(rect.x + 8f, rect.yMax - 22f, rect.width - 16f, 18f);
            GUI.Label(info, $"{level.Cells.Count} cells · {level.Stacks.Count} preplaced · hover {(_hover.HasValue ? _hover.Value.ToString() : "-")}", EditorStyles.whiteMiniLabel);
        }

        private void HandleInput(Rect rect, Vector2 centre, float size, LevelData level)
        {
            var e = Event.current;
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                HexCoord? hover = null;
                if (rect.Contains(e.mousePosition))
                {
                    var c = FromScreen(e.mousePosition, centre, size);
                    if (HexCoord.Distance(c, HexCoord.Zero) <= GridRadius) hover = c;
                }
                if (hover != _hover)
                {
                    _hover = hover;
                    Repaint();
                }
            }

            bool press = e.type == EventType.MouseDown || (e.type == EventType.MouseDrag && _tool == Tool.Cells);
            if (!press || !rect.Contains(e.mousePosition) || !_hover.HasValue) return;
            if (e.button != 0 && e.button != 1) return;

            Apply(level, _hover.Value, primary: e.button == 0);
            e.Use();
        }

        private void Apply(LevelData level, HexCoord coord, bool primary)
        {
            Undo.RecordObject(level, "Edit level board");
            int cellIndex = level.IndexOfCell(coord);
            int stackIndex = level.IndexOfStack(coord);

            switch (_tool)
            {
                case Tool.Cells:
                    if (primary && cellIndex < 0) level.Cells.Add(new LevelData.Cell { Q = coord.Q, R = coord.R });
                    else if (!primary && cellIndex >= 0)
                    {
                        level.Cells.RemoveAt(cellIndex);
                        if (stackIndex >= 0) level.Stacks.RemoveAt(stackIndex);
                    }
                    break;

                case Tool.Locks:
                    if (cellIndex < 0) return;
                    var cell = level.Cells[cellIndex];
                    cell.Locked = primary;
                    cell.UnlockScore = primary ? _lockScore : 0;
                    level.Cells[cellIndex] = cell;
                    if (primary && stackIndex >= 0) level.Stacks.RemoveAt(stackIndex);
                    break;

                case Tool.Stacks:
                    if (cellIndex < 0 || level.Cells[cellIndex].Locked) return;
                    if (stackIndex >= 0) level.Stacks.RemoveAt(stackIndex);
                    if (primary && _brush.Count > 0)
                        level.Stacks.Add(new LevelData.Stack { Q = coord.Q, R = coord.R, Colors = _brush.ToArray() });
                    break;
            }

            MarkChanged(level);
        }

        private void MarkChanged(LevelData level)
        {
            EditorUtility.SetDirty(level);
            _validation = null;
            _simulation = null;
            Repaint();
        }

        // ---- Right: settings and tools ----

        private void DrawInspector()
        {
            var level = Current;
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(InspectorWidth)))
            {
                _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
                if (level == null)
                {
                    EditorGUILayout.HelpBox("Select or add a level.", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                EditorGUILayout.LabelField($"Level {_selected + 1}", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(level, "Edit level settings");
                level.DisplayName = EditorGUILayout.TextField("Name", level.DisplayName);
                level.TutorialText = EditorGUILayout.TextField("Tutorial hint", level.TutorialText);
                level.TargetTiles = Mathf.Max(1, EditorGUILayout.IntField("Target tiles", level.TargetTiles));
                level.ClearThreshold = Mathf.Max(2, EditorGUILayout.IntField("Clear threshold", level.ClearThreshold));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Tray generator", EditorStyles.boldLabel);
                level.ColorCount = EditorGUILayout.IntSlider("Colors", level.ColorCount, 1, ProceduralLevels.MaxColors);
                level.MinTiles = Mathf.Max(1, EditorGUILayout.IntField("Min tiles", level.MinTiles));
                level.MaxTiles = Mathf.Max(level.MinTiles, EditorGUILayout.IntField("Max tiles", level.MaxTiles));
                level.MaxGroups = EditorGUILayout.IntSlider("Max color groups", level.MaxGroups, 1, 5);
                level.MatchBias = EditorGUILayout.Slider(new GUIContent("Match bias", "Chance that a new stack's top can merge right away. Higher = easier."), level.MatchBias, 0f, 1f);
                level.Mercy = EditorGUILayout.Toggle(new GUIContent("Mercy", "Every tray refill offers at least one merge when the board allows it."), level.Mercy);
                level.Seed = EditorGUILayout.IntField("Seed", level.Seed);
                if (EditorGUI.EndChangeCheck()) MarkChanged(level);

                EditorGUILayout.Space(8);
                DrawTools(level);
                EditorGUILayout.Space(8);
                DrawShapePresets(level);
                EditorGUILayout.Space(8);
                DrawChecks(level);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTools(LevelData level)
        {
            EditorGUILayout.LabelField("Paint tool", EditorStyles.boldLabel);
            _tool = (Tool)GUILayout.Toolbar((int)_tool, new[] { "Cells", "Locks", "Stacks" });
            switch (_tool)
            {
                case Tool.Cells:
                    EditorGUILayout.HelpBox("Left click / drag: add cell. Right click / drag: remove cell.", MessageType.None);
                    break;
                case Tool.Locks:
                    _lockScore = Mathf.Max(1, EditorGUILayout.IntField("Unlock score", _lockScore));
                    EditorGUILayout.HelpBox("Left click: lock cell (opens at the score). Right click: unlock.", MessageType.None);
                    break;
                case Tool.Stacks:
                    DrawBrush(level);
                    EditorGUILayout.HelpBox("Left click: place the brush stack. Right click: remove stack.", MessageType.None);
                    break;
            }
        }

        private void DrawBrush(LevelData level)
        {
            EditorGUILayout.LabelField("Brush (bottom → top)");
            var rect = GUILayoutUtility.GetRect(InspectorWidth - 20f, 22f);
            float w = Mathf.Min(22f, rect.width / Mathf.Max(1, _brush.Count));
            for (int i = 0; i < _brush.Count; i++)
                EditorGUI.DrawRect(new Rect(rect.x + i * w, rect.y, w - 2f, rect.height), PaletteColor(_brush[i]));
            if (_brush.Count == 0) GUI.Label(rect, "(empty)", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.LabelField("Add tile on top:");
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int c = 0; c < level.ColorCount; c++)
                {
                    var r = GUILayoutUtility.GetRect(26f, 22f, GUILayout.Width(26f));
                    EditorGUI.DrawRect(r, PaletteColor(c));
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none) && _brush.Count < 12) _brush.Add(c);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Remove top") && _brush.Count > 0) _brush.RemoveAt(_brush.Count - 1);
                if (GUILayout.Button("Clear")) _brush.Clear();
            }
        }

        private void DrawShapePresets(LevelData level)
        {
            EditorGUILayout.LabelField("Board shape", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _shapePreset = EditorGUILayout.Popup(_shapePreset, ShapeNames);
                if (GUILayout.Button("Apply", GUILayout.Width(60f)) &&
                    EditorUtility.DisplayDialog("Replace board", "Replace all cells (locks and preplaced stacks are cleared)?", "Replace", "Cancel"))
                {
                    Undo.RecordObject(level, "Apply board shape");
                    level.Cells.Clear();
                    level.Stacks.Clear();
                    foreach (var c in ShapeFor(_shapePreset)) level.Cells.Add(new LevelData.Cell { Q = c.Q, R = c.R });
                    MarkChanged(level);
                }
            }
        }

        private static List<HexCoord> ShapeFor(int preset)
        {
            switch (preset)
            {
                case 0: return BoardShapes.Hexagon(2);
                case 1: return BoardShapes.Hexagon(3);
                case 2: return BoardShapes.Rectangle(3, 4);
                case 3: return BoardShapes.Rectangle(4, 5);
                case 4: return BoardShapes.Rectangle(5, 6);
                case 5: return BoardShapes.Rectangle(5, 7);
                case 6: return BoardShapes.Ring(3, 1);
                default: return BoardShapes.Parallelogram(5, 5);
            }
        }

        private void DrawChecks(LevelData level)
        {
            EditorGUILayout.LabelField("Checks", EditorStyles.boldLabel);
            if (GUILayout.Button("Validate"))
            {
                try
                {
                    level.ToDefinition(_selected + 1).Validate();
                    _validation = "Level is valid.";
                    _validationOk = true;
                }
                catch (Exception ex)
                {
                    _validation = ex.Message;
                    _validationOk = false;
                }
            }
            if (_validation != null) EditorGUILayout.HelpBox(_validation, _validationOk ? MessageType.Info : MessageType.Error);

            using (new EditorGUILayout.HorizontalScope())
            {
                _simulationGames = Mathf.Clamp(EditorGUILayout.IntField("Bot games", _simulationGames), 10, 500);
                if (GUILayout.Button("Simulate", GUILayout.Width(80f))) Simulate(level);
            }
            if (_simulation != null) EditorGUILayout.HelpBox(_simulation, MessageType.None);
        }

        private void Simulate(LevelData level)
        {
            try
            {
                var def = level.ToDefinition(_selected + 1);
                def.Validate();
                EditorUtility.DisplayProgressBar("Simulating", $"Greedy bot plays {_simulationGames} games…", 0.5f);
                var stats = LevelSimulator.Run(def, _simulationGames);
                _simulation = $"Bot: {stats}\nGuide: tutorial ~100%, normal 75-95%, hard 50-70%.";
            }
            catch (Exception ex)
            {
                _simulation = "Cannot simulate: " + ex.Message;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---- Drawing helpers ----

        private static Vector2 ToScreen(HexCoord coord, Vector2 centre, float size)
        {
            HexLayout.ToPlanar(coord, size, out float x, out float y);
            return new Vector2(centre.x + x, centre.y - y); // planar +y is up on screen (towards the top of the board)
        }

        private static HexCoord FromScreen(Vector2 point, Vector2 centre, float size) =>
            HexLayout.FromPlanar(point.x - centre.x, centre.y - point.y, size);

        private static readonly Vector3[] Corners = new Vector3[7];

        private static void DrawHex(Vector2 centre, float radius, Color? fill, Color outline, float width = 1.5f)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Deg2Rad * 60f * i;
                Corners[i] = new Vector3(centre.x + Mathf.Cos(a) * radius, centre.y + Mathf.Sin(a) * radius, 0f);
            }
            Corners[6] = Corners[0];

            if (fill.HasValue)
            {
                Handles.color = fill.Value;
                Handles.DrawAAConvexPolygon(Corners[0], Corners[1], Corners[2], Corners[3], Corners[4], Corners[5]);
            }
            Handles.color = outline;
            Handles.DrawAAPolyLine(width, Corners);
        }

        private void DrawStack(Vector2 centre, float size, int[] colors)
        {
            if (colors == null) return;
            // Tiles drawn as a small 3D-ish pile: each tile shifts up a few pixels.
            float step = Mathf.Clamp(size * 0.09f, 1.5f, 4f);
            float baseY = centre.y + step * (colors.Length - 1) * 0.5f;
            for (int i = 0; i < colors.Length; i++)
                DrawHex(new Vector2(centre.x, baseY - i * step), size * 0.6f, PaletteColor(colors[i]), new Color(0f, 0f, 0f, 0.6f), 1f);
            DrawLabel(new Vector2(centre.x, centre.y + size * 0.62f), colors.Length.ToString(), size * 0.8f, Color.white);
        }

        private static void DrawLabel(Vector2 centre, string text, float size, Color color)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(size * 0.4f), 8, 16),
                normal = { textColor = color },
            };
            GUI.Label(new Rect(centre.x - size, centre.y - size * 0.5f, size * 2f, size), text, style);
        }

        private Color PaletteColor(int id) => _palette[Mathf.Clamp(id, 0, _palette.Length - 1)];
    }
}

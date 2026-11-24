using System;
using System.Collections.Generic;
using HexaSort.Model;
using UnityEngine;

namespace HexaSort.Core.Levels
{
    /// <summary>
    /// Authored level asset (edited with Tools/HexaSort/Level Editor). A thin serializable shell
    /// around <see cref="LevelDefinition"/>: gameplay code only ever sees the converted definition.
    /// </summary>
    [CreateAssetMenu(menuName = "HexaSort/Level Data", fileName = "Level")]
    public sealed class LevelData : ScriptableObject
    {
        [Serializable]
        public struct Cell
        {
            public int Q;
            public int R;
            public bool Locked;
            [Tooltip("Cleared-tile score at which a locked cell opens.")]
            public int UnlockScore;

            public HexCoord Coord => new HexCoord(Q, R);
        }

        [Serializable]
        public struct Stack
        {
            public int Q;
            public int R;
            [Tooltip("Color ids from bottom to top.")]
            public int[] Colors;

            public HexCoord Coord => new HexCoord(Q, R);
        }

        [SerializeField] private string _displayName = "";
        [SerializeField, TextArea(1, 3)] private string _tutorialText = "";

        [Header("Board")]
        [SerializeField] private List<Cell> _cells = new List<Cell>();
        [SerializeField] private List<Stack> _stacks = new List<Stack>();

        [Header("Goal")]
        [SerializeField, Min(1)] private int _targetTiles = 100;
        [SerializeField, Min(2)] private int _clearThreshold = MergeResolver.DefaultClearThreshold;

        [Header("Tray generator")]
        [SerializeField, Range(1, ProceduralLevels.MaxColors)] private int _colorCount = 4;
        [SerializeField, Min(1)] private int _minTiles = 2;
        [SerializeField, Min(1)] private int _maxTiles = 6;
        [SerializeField, Range(1, 5)] private int _maxGroups = 3;
        [SerializeField, Range(0f, 1f)] private float _matchBias = 0.4f;
        [SerializeField] private bool _mercy = true;
        [SerializeField] private int _seed = 1;

        public string DisplayName { get => _displayName; set => _displayName = value; }
        public string TutorialText { get => _tutorialText; set => _tutorialText = value; }
        public List<Cell> Cells => _cells;
        public List<Stack> Stacks => _stacks;
        public int TargetTiles { get => _targetTiles; set => _targetTiles = value; }
        public int ClearThreshold { get => _clearThreshold; set => _clearThreshold = value; }
        public int ColorCount { get => _colorCount; set => _colorCount = value; }
        public int MinTiles { get => _minTiles; set => _minTiles = value; }
        public int MaxTiles { get => _maxTiles; set => _maxTiles = value; }
        public int MaxGroups { get => _maxGroups; set => _maxGroups = value; }
        public float MatchBias { get => _matchBias; set => _matchBias = value; }
        public bool Mercy { get => _mercy; set => _mercy = value; }
        public int Seed { get => _seed; set => _seed = value; }

        public int IndexOfCell(HexCoord coord) => _cells.FindIndex(c => c.Q == coord.Q && c.R == coord.R);
        public int IndexOfStack(HexCoord coord) => _stacks.FindIndex(s => s.Q == coord.Q && s.R == coord.R);

        public LevelDefinition ToDefinition(int number)
        {
            var def = new LevelDefinition
            {
                Number = number,
                Name = _displayName,
                TutorialText = _tutorialText,
                TargetTiles = _targetTiles,
                ClearThreshold = _clearThreshold,
                Seed = _seed,
                Generator =
                {
                    ColorCount = _colorCount,
                    MinTiles = _minTiles,
                    MaxTiles = Mathf.Max(_minTiles, _maxTiles),
                    MaxGroups = _maxGroups,
                    MatchBias = _matchBias,
                    Mercy = _mercy,
                },
            };
            foreach (var c in _cells) def.Cells.Add(new CellSpec(c.Coord, c.Locked, c.UnlockScore));
            foreach (var s in _stacks) def.Preplaced.Add(new PreplacedStack(s.Coord, (int[])(s.Colors ?? Array.Empty<int>()).Clone()));
            return def;
        }

        public void CopyFrom(LevelDefinition def)
        {
            _displayName = def.Name;
            _tutorialText = def.TutorialText;
            _targetTiles = def.TargetTiles;
            _clearThreshold = def.ClearThreshold;
            _seed = def.Seed;
            _colorCount = def.Generator.ColorCount;
            _minTiles = def.Generator.MinTiles;
            _maxTiles = def.Generator.MaxTiles;
            _maxGroups = def.Generator.MaxGroups;
            _matchBias = (float)def.Generator.MatchBias;
            _mercy = def.Generator.Mercy;

            _cells.Clear();
            foreach (var c in def.Cells)
                _cells.Add(new Cell { Q = c.Coord.Q, R = c.Coord.R, Locked = c.Locked, UnlockScore = c.UnlockScore });
            _stacks.Clear();
            foreach (var s in def.Preplaced)
                _stacks.Add(new Stack { Q = s.Coord.Q, R = s.Coord.R, Colors = (int[])s.ColorsBottomToTop.Clone() });
        }
    }
}

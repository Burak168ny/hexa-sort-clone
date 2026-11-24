using System;
using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>Tuning for <see cref="StackGenerator"/>.</summary>
    public sealed class GeneratorSettings
    {
        /// <summary>Colors are ids 0..ColorCount-1.</summary>
        public int ColorCount = 3;
        public int MinTiles = 2;
        public int MaxTiles = 6;
        public int MaxGroups = 3;

        /// <summary>
        /// Probability that a stack's top color is drawn from the colors that can currently merge on the
        /// board (tops next to an empty cell) instead of uniformly. Higher = easier.
        /// </summary>
        public double MatchBias = 0.45;

        /// <summary>
        /// When the board offers any merge opportunity, guarantee that at least one stack of every
        /// tray refill can take it ("mercy" rule).
        /// </summary>
        public bool Mercy = true;

        public void Validate()
        {
            if (ColorCount < 1) throw new ArgumentOutOfRangeException(nameof(ColorCount));
            if (MinTiles < 1 || MaxTiles < MinTiles) throw new ArgumentOutOfRangeException(nameof(MaxTiles));
            if (MaxGroups < 1) throw new ArgumentOutOfRangeException(nameof(MaxGroups));
            if (MatchBias < 0 || MatchBias > 1) throw new ArgumentOutOfRangeException(nameof(MatchBias));
        }

        public GeneratorSettings Clone() => (GeneratorSettings)MemberwiseClone();
    }

    /// <summary>
    /// Produces random stacks for the tray. Seeded, so a level replays identically.
    /// Adjacent groups always differ in color (otherwise they would just be one bigger group).
    /// The top color is chosen first because it is the only one that matters on placement;
    /// <see cref="GeneratorSettings.MatchBias"/> skews it towards colors the board can merge.
    /// </summary>
    public sealed class StackGenerator
    {
        private readonly GeneratorSettings _settings;
        private readonly Rng _rng;

        // Reused buffers.
        private readonly List<int> _mergeableTops = new List<int>(8);
        private readonly List<BoardCell> _neighbors = new List<BoardCell>(6);
        private readonly List<int> _groupColors = new List<int>(4);
        private readonly List<int> _groupSizes = new List<int>(4);

        public GeneratorSettings Settings => _settings;

        public StackGenerator(GeneratorSettings settings, int seed)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settings.Validate();
            _rng = new Rng(seed);
        }

        /// <summary>A random stack; when <paramref name="board"/> is given its top color is biased towards merges.</summary>
        public HexStack Generate(BoardModel board = null)
        {
            CollectMergeableTops(board, _mergeableTops);
            int top = _mergeableTops.Count > 0 && _rng.NextDouble() < _settings.MatchBias
                ? _mergeableTops[_rng.Next(_mergeableTops.Count)]
                : _rng.Next(_settings.ColorCount);
            return GenerateWithTop(top);
        }

        /// <summary>Fills every slot, then applies the mercy rule.</summary>
        public void FillTray(Tray tray, BoardModel board)
        {
            for (int i = 0; i < tray.SlotCount; i++) tray.Set(i, Generate(board));
            if (!_settings.Mercy || board == null) return;

            CollectMergeableTops(board, _mergeableTops);
            if (_mergeableTops.Count == 0) return;
            for (int i = 0; i < tray.SlotCount; i++)
                if (_mergeableTops.Contains(tray.Get(i).TopColor)) return;

            int slot = _rng.Next(tray.SlotCount);
            tray.Set(slot, GenerateWithTop(_mergeableTops[_rng.Next(_mergeableTops.Count)]));
        }

        /// <summary>
        /// Top colors of stacks adjacent to at least one placeable cell, in board order, without duplicates.
        /// Placing a stack with one of these tops next to it triggers a merge.
        /// </summary>
        public void CollectMergeableTops(BoardModel board, List<int> results)
        {
            results.Clear();
            if (board == null) return;

            var cells = board.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!cells[i].CanPlace) continue;
                board.GetNeighbors(cells[i].Coord, _neighbors);
                for (int n = 0; n < _neighbors.Count; n++)
                {
                    var stack = _neighbors[n].Stack;
                    if (stack == null) continue;
                    int top = stack.TopColor;
                    if (top < _settings.ColorCount && !results.Contains(top)) results.Add(top);
                }
            }
        }

        public HexStack GenerateWithTop(int topColor)
        {
            if (topColor < 0 || topColor >= _settings.ColorCount) throw new ArgumentOutOfRangeException(nameof(topColor));

            int tiles = _rng.Range(_settings.MinTiles, _settings.MaxTiles);
            int maxGroups = Math.Min(_settings.MaxGroups, tiles);
            if (_settings.ColorCount == 1) maxGroups = 1;
            int groups = _rng.Range(1, maxGroups);

            // Sizes bottom to top, leaving at least one tile for each remaining group.
            _groupSizes.Clear();
            int remaining = tiles;
            for (int g = 0; g < groups; g++)
            {
                int groupsLeft = groups - g;
                int size = groupsLeft == 1 ? remaining : _rng.Range(1, remaining - (groupsLeft - 1));
                _groupSizes.Add(size);
                remaining -= size;
            }

            // Colors top to bottom, each different from the group above it.
            _groupColors.Clear();
            int above = topColor;
            _groupColors.Add(topColor);
            for (int g = 1; g < groups; g++)
            {
                above = PickColorOtherThan(above);
                _groupColors.Add(above);
            }

            var stack = new HexStack();
            for (int g = 0; g < groups; g++) stack.Push(_groupColors[groups - 1 - g], _groupSizes[g]);
            return stack;
        }

        private int PickColorOtherThan(int excluded)
        {
            int c = _rng.Next(_settings.ColorCount - 1);
            return c >= excluded ? c + 1 : c;
        }
    }
}

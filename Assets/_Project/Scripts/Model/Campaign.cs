using System;
using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>
    /// Design source of the 100 campaign levels: 1-20 are handcrafted below, 21-100 are built from
    /// board templates (CampaignLate.cs). The editor turns these into LevelData assets The editor turns these into LevelData assets
    /// (Tools/HexaSort/Rebuild Campaign Levels); from then on the assets are what ships and what the
    /// Level Editor edits. Tests run the bot on this list so the default campaign stays winnable.
    ///
    /// Curve: 1-3 tutorials (small boards, 2-3 colors, generous generator), 5 introduces locks,
    /// 6 preplaced stacks, then boards grow and the palette climbs from 4 to 6 colors with
    /// breather levels (7, 11, 16) after each spike.
    /// Targets were calibrated with <see cref="Simulation.LevelSimulator"/>.
    /// </summary>
    public static partial class Campaign
    {
        public const int LevelCount = 100;
        public const int HandcraftedCount = 20;

        // Color ids, matching the default palette order.
        private const int R = 0, B = 1, G = 2, Y = 3, P = 4, O = 5;

        // Calibrated targets (see class remarks).
        private const int T4 = 60, T5 = 80, T6 = 80, T7 = 100, T8 = 180, T9 = 180, T10 = 250, T11 = 120,
            T12 = 140, T13 = 220, T14 = 250, T15 = 300, T16 = 150, T17 = 300, T18 = 270, T19 = 210, T20 = 250;

        public static List<LevelDefinition> CreateAll()
        {
            var levels = new List<LevelDefinition>(LevelCount);
            for (int n = 1; n <= LevelCount; n++) levels.Add(Create(n));
            return levels;
        }

        public static LevelDefinition Create(int number)
        {
            if (number > HandcraftedCount && number <= LevelCount) return CreateLate(number);
            switch (number)
            {
                case 1:
                    return New(1, "First Steps", colors: 2, target: 20, tiles: (2, 4), groups: 1, bias: 0.75)
                        .Tutorial("Drag a stack next to one of the same color.")
                        .Cells(BoardShapes.Rectangle(3, 4))
                        .Stack(Rect(0, 1, 3, 4), R, R)
                        .Stack(Rect(2, 2, 3, 4), B, B, B)
                        .Build();
                case 2:
                    return New(2, "Layers", colors: 3, target: 30, tiles: (2, 5), groups: 2, bias: 0.7)
                        .Tutorial("Only the top color of a stack can merge.")
                        .Cells(BoardShapes.Rectangle(4, 4))
                        .Stack(Rect(1, 1, 4, 4), B, R)
                        .Stack(Rect(2, 2, 4, 4), R, B, B)
                        .Build();
                case 3:
                    return New(3, "Ten Pops", colors: 3, target: 40, tiles: (2, 5), groups: 2, bias: 0.65)
                        .Tutorial("Gather 10 tiles of one color to clear them.")
                        .Cells(BoardShapes.Hexagon(2))
                        .Stack(Hex(0, 0), G, G, G, G)
                        .Build();
                case 4:
                    return New(4, "Mix It Up", colors: 4, target: T4, tiles: (2, 6), groups: 3, bias: 0.5)
                        .Cells(BoardShapes.Hexagon(2))
                        .Build();
                case 5:
                    return New(5, "Locked Out", colors: 5, target: T5, tiles: (2, 6), groups: 3, bias: 0.45)
                        .Tutorial("Clear tiles to open locked cells.")
                        .Cells(BoardShapes.Rectangle(5, 5))
                        .Lock(Rect(0, 0, 5, 5), 20).Lock(Rect(4, 0, 5, 5), 20)
                        .Lock(Rect(0, 4, 5, 5), 40).Lock(Rect(4, 4, 5, 5), 40)
                        .Build();
                case 6:
                    return New(6, "Head Start", colors: 5, target: T6, tiles: (2, 6), groups: 3, bias: 0.4)
                        .Cells(BoardShapes.Hexagon(2))
                        .Stack(Hex(-1, 0), R, B, B).Stack(Hex(1, 0), G, Y, Y)
                        .Stack(Hex(0, -2), Y, R).Stack(Hex(0, 2), B, G)
                        .Build();
                case 7:
                    return New(7, "Breathing Room", colors: 6, target: T7, tiles: (2, 6), groups: 3, bias: 0.4)
                        .Cells(BoardShapes.Rectangle(5, 6))
                        .Build();
                case 8:
                    return New(8, "Donut", colors: 6, target: T8, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Ring(3, 1))
                        .Build();
                case 9:
                    return New(9, "Four Corners", colors: 8, target: T9, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Rectangle(5, 6))
                        .Lock(Rect(0, 0, 5, 6), 40).Lock(Rect(4, 0, 5, 6), 40)
                        .Lock(Rect(0, 5, 5, 6), 80).Lock(Rect(4, 5, 5, 6), 80)
                        .Build();
                case 10:
                    return New(10, "Tight Squeeze", colors: 7, target: T10, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Hexagon(2))
                        .Build();
                case 11:
                    return New(11, "Diamond", colors: 6, target: T11, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Parallelogram(5, 5))
                        .Build();
                case 12:
                    return New(12, "Pillars", colors: 8, target: T12, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Rectangle(5, 7))
                        .Remove(Rect(1, 2, 5, 7), Rect(3, 2, 5, 7), Rect(1, 4, 5, 7), Rect(3, 4, 5, 7))
                        .Build();
                case 13:
                    return New(13, "Crown", colors: 8, target: T13, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Hexagon(3))
                        .Lock(Hex(3, 0), 60).Lock(Hex(-3, 0), 60).Lock(Hex(0, 3), 120)
                        .Lock(Hex(0, -3), 120).Lock(Hex(3, -3), 180).Lock(Hex(-3, 3), 180)
                        .Build();
                case 14:
                    return New(14, "Hourglass", colors: 8, target: T14, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Rectangle(5, 6))
                        .Remove(Rect(0, 2, 5, 6), Rect(4, 2, 5, 6), Rect(0, 3, 5, 6), Rect(4, 3, 5, 6))
                        .Build();
                case 15:
                    return New(15, "Rainbow", colors: 7, target: T15, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Rectangle(4, 5))
                        .Stack(Rect(0, 0, 4, 5), R, O, O).Stack(Rect(3, 0, 4, 5), B, P, P)
                        .Stack(Rect(0, 4, 4, 5), G, Y).Stack(Rect(3, 4, 4, 5), Y, G, G)
                        .Build();
                case 16:
                    return New(16, "Wide Open", colors: 8, target: T16, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Hexagon(3))
                        .Build();
                case 17:
                    return New(17, "Halo", colors: 7, target: T17, tiles: (2, 6), groups: 3, bias: 0.35)
                        .Cells(BoardShapes.Ring(3, 1))
                        .Build();
                case 18:
                    return New(18, "Vault", colors: 8, target: T18, tiles: (2, 6), groups: 3, bias: 0.3)
                        .Cells(BoardShapes.Rectangle(5, 6))
                        .Lock(Rect(0, 0, 5, 6), 80).Lock(Rect(2, 0, 5, 6), 160).Lock(Rect(4, 0, 5, 6), 80)
                        .Lock(Rect(0, 5, 5, 6), 240).Lock(Rect(2, 5, 5, 6), 160).Lock(Rect(4, 5, 5, 6), 240)
                        .Build();
                case 19:
                    return New(19, "Archipelago", colors: 8, target: T19, tiles: (2, 6), groups: 3, bias: 0.3)
                        .Cells(BoardShapes.Hexagon(3))
                        .Remove(Hex(0, 0), Hex(2, -1), Hex(-2, 1), Hex(1, 1), Hex(-1, -1))
                        .Build();
                case 20:
                    return New(20, "Grand Finale", colors: 8, target: T20, tiles: (2, 7), groups: 3, bias: 0.3)
                        .Cells(BoardShapes.Ring(3, 1))
                        .Lock(Hex(3, 0), 40).Lock(Hex(-3, 0), 80).Lock(Hex(0, 3), 120)
                        .Stack(Hex(3, -1), P, O, O).Stack(Hex(-3, 1), Y, R)
                        .Build();
                default:
                    throw new ArgumentOutOfRangeException(nameof(number), $"Campaign has levels 1..{LevelCount}.");
            }
        }

        private static HexCoord Hex(int q, int r) => new HexCoord(q, r);
        private static HexCoord Rect(int column, int row, int columns, int rows) => BoardShapes.Offset(column, row, columns, rows);

        private static Builder New(int number, string name, int colors, int target, (int min, int max) tiles, int groups, double bias) =>
            new Builder(new LevelDefinition
            {
                Number = number,
                Name = name,
                TargetTiles = target,
                Seed = number * 1013 + 7,
                Generator =
                {
                    ColorCount = colors, MinTiles = tiles.min, MaxTiles = tiles.max, MaxGroups = groups,
                    MatchBias = bias, Mercy = true,
                },
            });

        /// <summary>Fluent helper that keeps the level table above readable.</summary>
        private sealed class Builder
        {
            private readonly LevelDefinition _def;
            private readonly List<HexCoord> _cells = new List<HexCoord>();
            private readonly Dictionary<HexCoord, int> _locks = new Dictionary<HexCoord, int>();

            public Builder(LevelDefinition def) => _def = def;

            public Builder Tutorial(string text)
            {
                _def.TutorialText = text;
                return this;
            }

            public Builder Cells(IEnumerable<HexCoord> coords)
            {
                _cells.AddRange(coords);
                return this;
            }

            public Builder Remove(params HexCoord[] coords)
            {
                foreach (var c in coords)
                    if (!_cells.Remove(c)) throw new ArgumentException($"Level {_def.Number}: no cell {c} to remove.");
                return this;
            }

            public Builder Lock(HexCoord coord, int unlockScore)
            {
                if (!_cells.Contains(coord)) throw new ArgumentException($"Level {_def.Number}: no cell {coord} to lock.");
                _locks[coord] = unlockScore;
                return this;
            }

            public Builder Stack(HexCoord coord, params int[] colorsBottomToTop)
            {
                _def.Preplaced.Add(new PreplacedStack(coord, colorsBottomToTop));
                return this;
            }

            public LevelDefinition Build()
            {
                foreach (var c in _cells)
                    _def.Cells.Add(_locks.TryGetValue(c, out int score) ? new CellSpec(c, true, score) : new CellSpec(c));
                _def.Validate();
                return _def;
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>One board cell of a level layout.</summary>
    public readonly struct CellSpec
    {
        public readonly HexCoord Coord;
        public readonly bool Locked;
        public readonly int UnlockScore;

        public CellSpec(HexCoord coord, bool locked = false, int unlockScore = 0)
        {
            Coord = coord;
            Locked = locked;
            UnlockScore = unlockScore;
        }
    }

    /// <summary>A stack that is already on the board when the level starts.</summary>
    public readonly struct PreplacedStack
    {
        public readonly HexCoord Coord;
        public readonly int[] ColorsBottomToTop;

        public PreplacedStack(HexCoord coord, int[] colorsBottomToTop)
        {
            Coord = coord;
            ColorsBottomToTop = colorsBottomToTop ?? throw new ArgumentNullException(nameof(colorsBottomToTop));
        }
    }

    /// <summary>
    /// Engine-independent description of a level. Authored levels (ScriptableObjects) and the
    /// procedural fallback both produce this, and it is the only thing a <see cref="GameSession"/> needs.
    /// </summary>
    public sealed class LevelDefinition
    {
        public int Number = 1;
        public string Name = "";
        /// <summary>Short hint shown during the level (tutorial levels), or empty.</summary>
        public string TutorialText = "";
        public readonly List<CellSpec> Cells = new List<CellSpec>();
        public readonly List<PreplacedStack> Preplaced = new List<PreplacedStack>();
        public int TargetTiles = 50;
        public int ClearThreshold = MergeResolver.DefaultClearThreshold;
        public GeneratorSettings Generator = new GeneratorSettings();
        public int Seed = 1;

        public void AddCells(IEnumerable<HexCoord> coords)
        {
            foreach (var c in coords) Cells.Add(new CellSpec(c));
        }

        /// <summary>Throws with a readable message if the definition cannot be played.</summary>
        public void Validate()
        {
            if (Cells.Count == 0) throw new InvalidOperationException($"Level {Number}: no cells.");
            if (TargetTiles <= 0) throw new InvalidOperationException($"Level {Number}: target must be positive.");
            if (ClearThreshold < 2) throw new InvalidOperationException($"Level {Number}: clear threshold must be >= 2.");
            Generator.Validate();

            var board = CreateBoard();
            if (!board.HasPlaceableCell()) throw new InvalidOperationException($"Level {Number}: no free cell at start.");
            foreach (var p in Preplaced)
            {
                if (p.ColorsBottomToTop.Length == 0)
                    throw new InvalidOperationException($"Level {Number}: preplaced stack at {p.Coord} is empty.");
                foreach (int color in p.ColorsBottomToTop)
                    if (color < 0 || color >= Generator.ColorCount)
                        throw new InvalidOperationException($"Level {Number}: preplaced color {color} at {p.Coord} outside palette.");
            }

            // The starting board must already be stable, otherwise merges would happen before the first move.
            var neighbors = new List<BoardCell>(6);
            foreach (var cell in board.Cells)
            {
                if (cell.Stack == null) continue;
                if (cell.Stack.TopGroupCount >= ClearThreshold)
                    throw new InvalidOperationException($"Level {Number}: preplaced stack at {cell.Coord} would clear immediately.");
                board.GetNeighbors(cell.Coord, neighbors);
                foreach (var n in neighbors)
                    if (n.Stack != null && n.Stack.TopColor == cell.Stack.TopColor)
                        throw new InvalidOperationException($"Level {Number}: preplaced stacks at {cell.Coord} and {n.Coord} share a top color.");
            }
        }

        public BoardModel CreateBoard()
        {
            var board = new BoardModel();
            foreach (var cell in Cells) board.AddCell(cell.Coord, cell.Locked, cell.UnlockScore);
            foreach (var p in Preplaced)
            {
                if (!board.CanPlace(p.Coord))
                    throw new InvalidOperationException($"Level {Number}: preplaced stack at {p.Coord} needs a free, unlocked cell.");
                board.Place(p.Coord, HexStack.FromColors(p.ColorsBottomToTop));
            }
            return board;
        }

        /// <summary>
        /// Starts a run. <paramref name="attempt"/> varies the tray sequence between retries while
        /// keeping every attempt reproducible.
        /// </summary>
        public GameSession CreateSession(int attempt = 0)
        {
            int seed = unchecked(Seed * 486187739 + attempt * 7919);
            return new GameSession(CreateBoard(), new StackGenerator(Generator.Clone(), seed),
                new LevelGoal(TargetTiles), new MergeResolver(ClearThreshold));
        }
    }
}

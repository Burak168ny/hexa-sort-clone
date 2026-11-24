using System;
using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>Everything that happened as a consequence of one placement.</summary>
    public sealed class PlaceOutcome
    {
        /// <summary>Tray slot used, or -1 when the change came from a Move booster.</summary>
        public int Slot { get; internal set; }
        /// <summary>Source cell of a Move booster (null for placements).</summary>
        public HexCoord? MovedFrom { get; internal set; }
        public HexCoord Cell { get; internal set; }
        public MergeResult Merge { get; internal set; }

        /// <summary>Cells whose lock opened because the score crossed their threshold.</summary>
        public readonly List<HexCoord> UnlockedCells = new List<HexCoord>();

        /// <summary>True when the tray ran out and was refilled with new stacks.</summary>
        public bool TrayRefilled { get; internal set; }

        public bool IsWin { get; internal set; }
        public bool IsFail { get; internal set; }
    }

    /// <summary>
    /// Rules of one level run, independent of Unity: tray, placement, resolution, scoring, win/fail, revive.
    /// The game layer forwards input here and animates the returned outcomes.
    /// </summary>
    public sealed class GameSession
    {
        public BoardModel Board { get; }
        public Tray Tray { get; }
        public LevelGoal Goal { get; }
        public StackGenerator Generator { get; }

        private readonly MergeResolver _resolver;
        private readonly List<BoardCell> _reviveCandidates = new List<BoardCell>();

        public bool IsWon { get; private set; }
        public bool IsFailed { get; private set; }
        public bool IsOver => IsWon || IsFailed;
        public int RevivesUsed { get; private set; }

        public GameSession(BoardModel board, StackGenerator generator, LevelGoal goal, MergeResolver resolver,
            int traySlots = Tray.DefaultSlotCount)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
            Goal = goal ?? throw new ArgumentNullException(nameof(goal));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            Tray = new Tray(traySlots);
            Generator.FillTray(Tray, Board);
        }

        public bool CanPlace(int slot, HexCoord cell) =>
            !IsOver && slot >= 0 && slot < Tray.SlotCount && Tray.Get(slot) != null && Board.CanPlace(cell);

        public PlaceOutcome Place(int slot, HexCoord cell)
        {
            if (!CanPlace(slot, cell)) throw new InvalidOperationException($"Cannot place slot {slot} at {cell}.");

            var outcome = new PlaceOutcome { Slot = slot, Cell = cell };
            Board.Place(cell, Tray.Take(slot));
            ResolveAt(cell, outcome);
            return outcome;
        }

        // ---- Boosters ----

        public bool CanHammer(HexCoord cell) => !IsOver && Board.GetStack(cell) != null;

        /// <summary>Hammer booster: removes the stack without scoring it.</summary>
        public void Hammer(HexCoord cell)
        {
            if (!CanHammer(cell)) throw new InvalidOperationException($"Nothing to hammer at {cell}.");
            Board.RemoveStack(cell);
        }

        public bool CanMove(HexCoord from, HexCoord to) =>
            !IsOver && from != to && Board.GetStack(from) != null && Board.CanPlace(to);

        /// <summary>Move booster: relocates a stack to an empty cell, then resolves merges there like a placement.</summary>
        public PlaceOutcome Move(HexCoord from, HexCoord to)
        {
            if (!CanMove(from, to)) throw new InvalidOperationException($"Cannot move {from} to {to}.");
            var stack = Board.GetStack(from);
            Board.RemoveStack(from);
            Board.Place(to, stack);

            var outcome = new PlaceOutcome { Slot = -1, Cell = to, MovedFrom = from };
            ResolveAt(to, outcome);
            return outcome;
        }

        /// <summary>Shuffle booster: replaces every tray slot with a fresh stack.</summary>
        public void ShuffleTray()
        {
            if (IsOver) throw new InvalidOperationException("The level is over.");
            Generator.FillTray(Tray, Board);
        }

        private void ResolveAt(HexCoord cell, PlaceOutcome outcome)
        {
            outcome.Merge = _resolver.Resolve(Board, cell);

            Goal.AddCleared(outcome.Merge.TilesCleared);
            Board.UnlockCells(Goal.ClearedTiles, outcome.UnlockedCells);

            if (Tray.IsEmpty)
            {
                Generator.FillTray(Tray, Board);
                outcome.TrayRefilled = true;
            }

            if (Goal.IsComplete) IsWon = outcome.IsWin = true;
            else if (!Board.HasPlaceableCell()) IsFailed = outcome.IsFail = true;
        }

        /// <summary>
        /// Second chance after a fail: removes the <paramref name="cellsToFree"/> tallest stacks
        /// (ties in board order) without scoring them. Returns the freed cells.
        /// </summary>
        public List<HexCoord> Revive(int cellsToFree)
        {
            if (!IsFailed) throw new InvalidOperationException("Revive is only possible after a fail.");
            if (cellsToFree < 1) throw new ArgumentOutOfRangeException(nameof(cellsToFree));

            _reviveCandidates.Clear();
            foreach (var cell in Board.Cells)
                if (cell.Stack != null) _reviveCandidates.Add(cell);

            // Stable sort: List.Sort is not stable, so break ties on the original board order.
            var order = new Dictionary<HexCoord, int>(_reviveCandidates.Count);
            for (int i = 0; i < _reviveCandidates.Count; i++) order[_reviveCandidates[i].Coord] = i;
            _reviveCandidates.Sort((a, b) =>
            {
                int bySize = b.Stack.Count.CompareTo(a.Stack.Count);
                return bySize != 0 ? bySize : order[a.Coord].CompareTo(order[b.Coord]);
            });

            var freed = new List<HexCoord>(cellsToFree);
            for (int i = 0; i < _reviveCandidates.Count && freed.Count < cellsToFree; i++)
            {
                Board.RemoveStack(_reviveCandidates[i].Coord);
                freed.Add(_reviveCandidates[i].Coord);
            }

            IsFailed = false;
            RevivesUsed++;
            return freed;
        }
    }
}

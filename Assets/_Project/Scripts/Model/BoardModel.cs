using System;
using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>
    /// The hex board: an arbitrary set of cells keyed by axial coordinate.
    /// Cells keep their insertion order, which makes every iteration over the board deterministic.
    /// </summary>
    public sealed class BoardModel
    {
        private readonly Dictionary<HexCoord, BoardCell> _cells = new Dictionary<HexCoord, BoardCell>();
        private readonly List<BoardCell> _ordered = new List<BoardCell>();

        public IReadOnlyList<BoardCell> Cells => _ordered;
        public int CellCount => _ordered.Count;

        public BoardModel() { }

        public BoardModel(IEnumerable<HexCoord> coords)
        {
            foreach (var c in coords) AddCell(c);
        }

        public BoardCell AddCell(HexCoord coord, bool locked = false, int unlockScore = 0)
        {
            if (_cells.ContainsKey(coord)) throw new ArgumentException($"Cell {coord} already exists.", nameof(coord));
            var cell = new BoardCell(coord, locked, unlockScore);
            _cells.Add(coord, cell);
            _ordered.Add(cell);
            return cell;
        }

        public bool Contains(HexCoord coord) => _cells.ContainsKey(coord);

        public bool TryGetCell(HexCoord coord, out BoardCell cell) => _cells.TryGetValue(coord, out cell);

        /// <summary>The stack at <paramref name="coord"/>, or null for empty, locked or missing cells.</summary>
        public HexStack GetStack(HexCoord coord) => _cells.TryGetValue(coord, out var cell) ? cell.Stack : null;

        public bool CanPlace(HexCoord coord) => _cells.TryGetValue(coord, out var cell) && cell.CanPlace;

        public void Place(HexCoord coord, HexStack stack)
        {
            if (stack == null || stack.IsEmpty) throw new ArgumentException("Cannot place an empty stack.", nameof(stack));
            if (!_cells.TryGetValue(coord, out var cell)) throw new ArgumentException($"No cell at {coord}.", nameof(coord));
            if (!cell.CanPlace) throw new InvalidOperationException($"Cell {coord} is locked or occupied.");
            cell.Stack = stack;
        }

        /// <summary>Removes the stack at <paramref name="coord"/> (no-op when already empty).</summary>
        public void RemoveStack(HexCoord coord)
        {
            if (_cells.TryGetValue(coord, out var cell)) cell.Stack = null;
        }

        public bool HasPlaceableCell()
        {
            for (int i = 0; i < _ordered.Count; i++)
                if (_ordered[i].CanPlace) return true;
            return false;
        }

        public int PlaceableCellCount()
        {
            int count = 0;
            for (int i = 0; i < _ordered.Count; i++)
                if (_ordered[i].CanPlace) count++;
            return count;
        }

        public int TotalTileCount()
        {
            int count = 0;
            for (int i = 0; i < _ordered.Count; i++)
                if (_ordered[i].Stack != null) count += _ordered[i].Stack.Count;
            return count;
        }

        /// <summary>Fills <paramref name="results"/> with existing neighbour cells in direction order.</summary>
        public void GetNeighbors(HexCoord coord, List<BoardCell> results)
        {
            results.Clear();
            for (int d = 0; d < HexCoord.DirectionCount; d++)
                if (_cells.TryGetValue(coord.Neighbor(d), out var cell)) results.Add(cell);
        }

        /// <summary>
        /// Unlocks every locked cell whose threshold is at or below <paramref name="score"/>.
        /// Newly opened coordinates are appended to <paramref name="unlocked"/>; returns how many opened.
        /// </summary>
        public int UnlockCells(int score, List<HexCoord> unlocked)
        {
            int opened = 0;
            for (int i = 0; i < _ordered.Count; i++)
            {
                var cell = _ordered[i];
                if (!cell.IsLocked || cell.UnlockScore > score) continue;
                cell.IsLocked = false;
                unlocked?.Add(cell.Coord);
                opened++;
            }
            return opened;
        }

        /// <summary>Deep copy: cells and stacks are duplicated, so simulations never touch the original.</summary>
        public BoardModel Clone()
        {
            var copy = new BoardModel();
            foreach (var cell in _ordered)
            {
                var c = copy.AddCell(cell.Coord, cell.IsLocked, cell.UnlockScore);
                c.Stack = cell.Stack?.Clone();
            }
            return copy;
        }
    }
}

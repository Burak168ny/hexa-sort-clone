using System;
using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>
    /// Resolves the board after a stack is placed, mutating it until it is stable and recording
    /// every change as a <see cref="MergeStep"/>.
    ///
    /// Rules (deterministic: neighbours are visited in <see cref="HexCoord"/> direction order and
    /// cells in board order):
    /// <list type="number">
    /// <item>Work queue = [placed cell].</item>
    /// <item>Pop cell X with top color C. Collect neighbours M whose top color is also C. Nothing to do if M is empty.</item>
    /// <item>Pick target T from {X} ∪ M by priority: (a) uniform stack (all C), (b) larger C top group,
    ///       (c) more C-topped neighbours, (d) X itself, (e) earlier neighbour direction.</item>
    /// <item>Every m in M other than T pours its C group onto X. If T is not X, X then pours its whole
    ///       (now combined) C group onto T. Tiles only ever travel between adjacent cells.</item>
    /// <item>Enqueue T, the donors and X: their top colors changed, so they may match new neighbours.</item>
    /// <item>When the queue drains, every top group of at least <see cref="ClearThreshold"/> tiles
    ///       is cleared (cell order); cleared cells are enqueued and the loop restarts. Stable when nothing clears.</item>
    /// </list>
    /// Why this terminates: a donor always gives away its whole top group, so its top color changes;
    /// a receiver's group only grows, so it keeps winning rule 3 against the stacks it absorbed;
    /// clears strictly remove tiles. A hard iteration cap guards against future rule changes.
    /// </summary>
    public sealed class MergeResolver
    {
        public const int DefaultClearThreshold = 10;

        private readonly int _maxIterations;

        // Reused buffers: resolving allocates nothing besides the result and its steps.
        private readonly Queue<HexCoord> _queue = new Queue<HexCoord>();
        private readonly HashSet<HexCoord> _queued = new HashSet<HexCoord>();
        private readonly List<BoardCell> _neighbors = new List<BoardCell>(6);
        private readonly List<BoardCell> _matches = new List<BoardCell>(6);

        public int ClearThreshold { get; }

        public MergeResolver(int clearThreshold = DefaultClearThreshold, int maxIterations = 10000)
        {
            if (clearThreshold < 2) throw new ArgumentOutOfRangeException(nameof(clearThreshold));
            ClearThreshold = clearThreshold;
            _maxIterations = maxIterations;
        }

        public MergeResult Resolve(BoardModel board, HexCoord placed)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));

            var result = new MergeResult();
            _queue.Clear();
            _queued.Clear();
            Enqueue(placed);

            int iterations = 0;
            do
            {
                while (_queue.Count > 0)
                {
                    if (++iterations > _maxIterations)
                    {
                        result.HitIterationLimit = true;
                        return result;
                    }

                    var cell = _queue.Dequeue();
                    _queued.Remove(cell);
                    ProcessCell(board, cell, result);
                }
            } while (ClearFullGroups(board, result));

            return result;
        }

        private void ProcessCell(BoardModel board, HexCoord x, MergeResult result)
        {
            var stack = board.GetStack(x);
            if (stack == null) return;

            int color = stack.TopColor;
            _matches.Clear();
            board.GetNeighbors(x, _neighbors);
            for (int i = 0; i < _neighbors.Count; i++)
            {
                var s = _neighbors[i].Stack;
                if (s != null && s.TopColor == color) _matches.Add(_neighbors[i]);
            }
            if (_matches.Count == 0) return;

            var target = SelectTarget(board, x, stack, color);

            for (int i = 0; i < _matches.Count; i++)
            {
                var donor = _matches[i].Coord;
                if (donor != target) Transfer(board, donor, x, result);
            }
            if (target != x) Transfer(board, x, target, result);

            Enqueue(target);
            for (int i = 0; i < _matches.Count; i++) Enqueue(_matches[i].Coord);
            Enqueue(x);
        }

        private HexCoord SelectTarget(BoardModel board, HexCoord x, HexStack xStack, int color)
        {
            var best = x;
            var bestScore = Score(board, x, xStack, color, isCurrent: true);

            for (int i = 0; i < _matches.Count; i++)
            {
                var cell = _matches[i];
                var score = Score(board, cell.Coord, cell.Stack, color, isCurrent: false);
                // Strictly greater: on a full tie the earlier candidate (X, then direction order) wins.
                if (score.CompareTo(bestScore) > 0)
                {
                    best = cell.Coord;
                    bestScore = score;
                }
            }
            return best;
        }

        private TargetScore Score(BoardModel board, HexCoord coord, HexStack stack, int color, bool isCurrent)
        {
            int sameNeighbors = 0;
            for (int d = 0; d < HexCoord.DirectionCount; d++)
            {
                var n = board.GetStack(coord.Neighbor(d));
                if (n != null && n.TopColor == color) sameNeighbors++;
            }
            return new TargetScore(stack.IsUniform, stack.TopGroupCount, sameNeighbors, isCurrent);
        }

        private static void Transfer(BoardModel board, HexCoord from, HexCoord to, MergeResult result)
        {
            var source = board.GetStack(from);
            var dest = board.GetStack(to);
            int color = source.TopColor;
            int count = source.TopGroupCount;

            source.PopTop(count);
            dest.Push(color, count);
            if (source.IsEmpty) board.RemoveStack(from);

            result.Steps.Add(new TransferStep(from, to, color, count));
            result.TransferCount++;
        }

        private bool ClearFullGroups(BoardModel board, MergeResult result)
        {
            bool cleared = false;
            var cells = board.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                var stack = cells[i].Stack;
                if (stack == null) continue;

                int count = stack.TopGroupCount;
                if (count < ClearThreshold) continue;

                int color = stack.TopColor;
                stack.PopTop(count);
                if (stack.IsEmpty) board.RemoveStack(cells[i].Coord);

                result.ClearCount++;
                result.TilesCleared += count;
                result.Steps.Add(new ClearStep(cells[i].Coord, color, count, result.ClearCount));
                Enqueue(cells[i].Coord);
                cleared = true;
            }
            return cleared;
        }

        private void Enqueue(HexCoord coord)
        {
            if (_queued.Add(coord)) _queue.Enqueue(coord);
        }

        /// <summary>Lexicographic priority used to choose which stack collects a color.</summary>
        private readonly struct TargetScore : IComparable<TargetScore>
        {
            private readonly bool _uniform;
            private readonly int _groupSize;
            private readonly int _sameNeighbors;
            private readonly bool _isCurrent;

            public TargetScore(bool uniform, int groupSize, int sameNeighbors, bool isCurrent)
            {
                _uniform = uniform;
                _groupSize = groupSize;
                _sameNeighbors = sameNeighbors;
                _isCurrent = isCurrent;
            }

            public int CompareTo(TargetScore other)
            {
                if (_uniform != other._uniform) return _uniform ? 1 : -1;
                if (_groupSize != other._groupSize) return _groupSize.CompareTo(other._groupSize);
                if (_sameNeighbors != other._sameNeighbors) return _sameNeighbors.CompareTo(other._sameNeighbors);
                if (_isCurrent != other._isCurrent) return _isCurrent ? 1 : -1;
                return 0;
            }
        }
    }
}

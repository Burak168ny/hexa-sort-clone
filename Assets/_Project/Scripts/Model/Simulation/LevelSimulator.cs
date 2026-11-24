using System;
using System.Collections.Generic;

namespace HexaSort.Model.Simulation
{
    /// <summary>Aggregate result of simulated playthroughs.</summary>
    public readonly struct SimulationStats
    {
        public readonly int Games;
        public readonly int Wins;
        public readonly double AverageMoves;
        public readonly double AverageProgress;

        public SimulationStats(int games, int wins, double averageMoves, double averageProgress)
        {
            Games = games;
            Wins = wins;
            AverageMoves = averageMoves;
            AverageProgress = averageProgress;
        }

        public double WinRate => Games == 0 ? 0 : Wins / (double)Games;

        public override string ToString() =>
            $"win {WinRate:P0} ({Wins}/{Games}), avg moves {AverageMoves:0.#}, avg progress {AverageProgress:P0}";
    }

    /// <summary>
    /// Plays levels with a one-ply greedy bot to estimate difficulty. Used by tests (every campaign
    /// level must stay winnable) and by the Level Editor ("Simulate") while tuning targets.
    /// The bot is deliberately simple - a human who plans ahead should do at least as well.
    /// </summary>
    public static class LevelSimulator
    {
        /// <param name="clearedPerGame">Optional: receives tiles cleared in each game (for capacity analysis).</param>
        public static SimulationStats Run(LevelDefinition level, int games, int maxMoves = 600, List<int> clearedPerGame = null)
        {
            if (games < 1) throw new ArgumentOutOfRangeException(nameof(games));

            int wins = 0;
            long totalMoves = 0;
            double totalProgress = 0;
            var bot = new GreedyBot(level.ClearThreshold);
            for (int g = 0; g < games; g++)
            {
                var session = level.CreateSession(attempt: 1000 + g);
                var rng = new Rng(level.Seed ^ (g * 92821));
                int moves = 0;
                while (!session.IsOver && moves < maxMoves)
                {
                    if (!bot.TryChooseMove(session, rng, out int slot, out HexCoord cell)) break;
                    session.Place(slot, cell);
                    moves++;
                }
                if (session.IsWon) wins++;
                clearedPerGame?.Add(session.Goal.ClearedTiles);
                totalMoves += moves;
                totalProgress += session.Goal.Progress;
            }
            return new SimulationStats(games, wins, totalMoves / (double)games, totalProgress / games);
        }

        private sealed class GreedyBot
        {
            private readonly MergeResolver _resolver;
            private readonly List<BoardCell> _neighbors = new List<BoardCell>(6);

            public GreedyBot(int threshold) => _resolver = new MergeResolver(threshold);

            public bool TryChooseMove(GameSession session, Rng rng, out int bestSlot, out HexCoord bestCell)
            {
                bestSlot = -1;
                bestCell = default;
                double bestScore = double.MinValue;
                var board = session.Board;

                for (int slot = 0; slot < session.Tray.SlotCount; slot++)
                {
                    var stack = session.Tray.Get(slot);
                    if (stack == null) continue;

                    foreach (var cell in board.Cells)
                    {
                        if (!cell.CanPlace) continue;
                        double score = Evaluate(board, cell.Coord, stack) + rng.NextDouble() * 0.01;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestSlot = slot;
                            bestCell = cell.Coord;
                        }
                    }
                }
                return bestSlot >= 0;
            }

            private double Evaluate(BoardModel board, HexCoord coord, HexStack stack)
            {
                board.GetNeighbors(coord, _neighbors);
                int top = stack.TopColor;
                bool matches = false;
                int occupied = 0;
                int secondColorNeighbors = 0;
                int second = SecondColor(stack);
                foreach (var n in _neighbors)
                {
                    if (n.Stack == null) continue;
                    occupied++;
                    if (n.Stack.TopColor == top) matches = true;
                    if (n.Stack.TopColor == second) secondColorNeighbors++;
                }

                if (!matches)
                {
                    // No merge now: keep stacks apart, but next to colors they will expose later.
                    return -0.5 * occupied + 0.3 * secondColorNeighbors;
                }

                // Only simulate placements that actually merge (keeps the bot fast).
                var clone = board.Clone();
                clone.Place(coord, stack.Clone());
                var result = _resolver.Resolve(clone, coord);
                int moved = 0;
                foreach (var step in result.Steps)
                    if (step is TransferStep t) moved += t.Count;
                bool freedCell = clone.CanPlace(coord);
                return 10 + result.TilesCleared * 100 + moved * 2 + (freedCell ? 6 : 0);
            }

            private static int SecondColor(HexStack stack)
            {
                int topRun = stack.TopGroupCount;
                return stack.Count > topRun ? stack[stack.Count - topRun - 1] : HexStack.NoColor;
            }
        }
    }
}

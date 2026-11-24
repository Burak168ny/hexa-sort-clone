using System.Collections.Generic;
using HexaSort.Model;
using NUnit.Framework;
using static HexaSort.Tests.TestBoards;

namespace HexaSort.Tests
{
    public class MergeResolverTests
    {
        // Neighbours of (0,0) in direction order: (1,0) (1,-1) (0,-1) (-1,0) (-1,1) (0,1).

        private static MergeResult Place(BoardModel board, int q, int r, string stack, int threshold = 10)
        {
            board.Place(C(q, r), S(stack));
            return new MergeResolver(threshold).Resolve(board, C(q, r));
        }

        [Test]
        public void NoMatchingNeighbour_ProducesNoSteps()
        {
            var board = Board(2, (1, 0, "G"), (-1, 0, "RY"));
            var result = Place(board, 0, 0, "RB");

            Assert.That(result.IsEmpty);
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("RB"));
        }

        [Test]
        public void MixedPlacedStack_GivesTopGroupToUniformNeighbour()
        {
            var board = Board(2, (1, 0, "B"));
            var result = Place(board, 0, 0, "RB");

            Assert.That(Describe(result.Steps), Is.EqualTo("Transfer (0,0)->(1,0) 1x2"));
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("R"));
            Assert.That(Str(board.GetStack(C(1, 0))), Is.EqualTo("BB"));
        }

        [Test]
        public void UniformPlacedStack_CollectsFromMixedNeighbour()
        {
            var board = Board(2, (1, 0, "RB"));
            var result = Place(board, 0, 0, "BB");

            Assert.That(Describe(result.Steps), Is.EqualTo("Transfer (1,0)->(0,0) 1x2"));
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("BBB"));
            Assert.That(Str(board.GetStack(C(1, 0))), Is.EqualTo("R"));
        }

        [Test]
        public void WhenNeitherIsUniform_LargerTopGroupCollects()
        {
            var board = Board(2, (1, 0, "GB3"));
            Place(board, 0, 0, "RB");

            Assert.That(Str(board.GetStack(C(1, 0))), Is.EqualTo("GBBBB"));
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("R"));
        }

        [Test]
        public void OnFullTie_PlacedStackCollects()
        {
            var board = Board(2, (1, 0, "GB"));
            Place(board, 0, 0, "RB");

            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("RBB"));
            Assert.That(Str(board.GetStack(C(1, 0))), Is.EqualTo("G"));
        }

        [Test]
        public void MoreMatchingNeighbours_BreaksSizeTie()
        {
            // (1,0) touches the placed cell and (2,0); the placed cell only touches (1,0).
            var board = Board(2, (1, 0, "GB"), (2, 0, "YB"));
            var result = Place(board, 0, 0, "RB");

            // Target is (1,0) (2 matching neighbours vs 1). It then gathers from (2,0) in turn.
            Assert.That(Describe(result.Steps), Is.EqualTo(
                "Transfer (0,0)->(1,0) 1x2 | Transfer (2,0)->(1,0) 1x2"));
            Assert.That(Str(board.GetStack(C(1, 0))), Is.EqualTo("GBBB"));
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("R"));
            Assert.That(Str(board.GetStack(C(2, 0))), Is.EqualTo("Y"));
        }

        [Test]
        public void ThreeNeighbours_FunnelThroughPlacedCellIntoUniformTarget()
        {
            var board = Board(2, (1, 0, "GB"), (-1, 0, "YB"), (0, 1, "B"));
            var result = Place(board, 0, 0, "RB");

            Assert.That(Describe(result.Steps), Is.EqualTo(
                "Transfer (1,0)->(0,0) 1x2 | Transfer (-1,0)->(0,0) 1x2 | Transfer (0,0)->(0,1) 3x2"));
            Assert.That(Str(board.GetStack(C(0, 1))), Is.EqualTo("BBBB"));
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("R"));
            Assert.That(Str(board.GetStack(C(1, 0))), Is.EqualTo("G"));
            Assert.That(Str(board.GetStack(C(-1, 0))), Is.EqualTo("Y"));
        }

        [Test]
        public void RevealedColor_ChainsIntoNextMerge_AndEmptiedCellIsFreed()
        {
            var board = Board(2, (1, 0, "B"), (-1, 0, "G"));
            var result = Place(board, 0, 0, "GB");

            Assert.That(Describe(result.Steps), Is.EqualTo(
                "Transfer (0,0)->(1,0) 1x2 | Transfer (-1,0)->(0,0) 1x1"));
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("GG"));
            Assert.That(board.GetStack(C(-1, 0)), Is.Null);
            Assert.That(board.CanPlace(C(-1, 0)));
        }

        [Test]
        public void GroupReachingThreshold_IsCleared()
        {
            var board = Board(2, (1, 0, "B5"));
            var result = Place(board, 0, 0, "B5");

            Assert.That(Describe(result.Steps), Is.EqualTo(
                "Transfer (1,0)->(0,0) 5x2 | Clear (0,0) 10x2 (combo 1)"));
            Assert.That(result.TilesCleared, Is.EqualTo(10));
            Assert.That(board.TotalTileCount(), Is.Zero);
            Assert.That(board.CanPlace(C(0, 0)) && board.CanPlace(C(1, 0)));
        }

        [Test]
        public void GroupBelowThreshold_IsNotCleared()
        {
            var board = Board(2, (1, 0, "B4"));
            var result = Place(board, 0, 0, "B5");

            Assert.That(result.ClearCount, Is.Zero);
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("BBBBBBBBB"));
        }

        [Test]
        public void ClearThreshold_IsConfigurable()
        {
            var board = Board(2, (1, 0, "B2"));
            var result = Place(board, 0, 0, "B3", threshold: 5);

            Assert.That(result.TilesCleared, Is.EqualTo(5));
        }

        [Test]
        public void Clear_RevealsColorThatChainsAgain()
        {
            var board = Board(2, (1, 0, "RB5"), (-1, 0, "G"));
            var result = Place(board, 0, 0, "GGB5");

            Assert.That(Describe(result.Steps), Is.EqualTo(
                "Transfer (1,0)->(0,0) 5x2 | Clear (0,0) 10x2 (combo 1) | Transfer (-1,0)->(0,0) 1x1"));
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("GGG"));
            Assert.That(Str(board.GetStack(C(1, 0))), Is.EqualTo("R"));
        }

        [Test]
        public void TwoClearsInOneResolve_CountAsCombo()
        {
            var board = Board(2, (1, 0, "B5"), (-1, 0, "G5"));
            var result = Place(board, 0, 0, "G5B5");

            Assert.That(result.ClearCount, Is.EqualTo(2));
            Assert.That(result.TilesCleared, Is.EqualTo(20));
            var combos = new List<int>();
            foreach (var step in result.Steps)
                if (step is ClearStep clear) combos.Add(clear.Combo);
            Assert.That(combos, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(board.TotalTileCount(), Is.Zero);
        }

        [Test]
        public void MixedColors_OnlyMatchingTopsInteract()
        {
            // Only (0,-1) shares the placed top color; the others carry B lower down, which never counts.
            var board = Board(2, (1, 0, "BG"), (0, -1, "GY"), (-1, 1, "BR"));
            var result = Place(board, 0, 0, "BY");

            Assert.That(Describe(result.Steps), Is.EqualTo("Transfer (0,-1)->(0,0) 1x3"));
            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("BYY"));
            // The revealed G on (0,-1) does not touch the G-topped (1,0), so nothing else moves.
            Assert.That(Str(board.GetStack(C(0, -1))), Is.EqualTo("G"));
            Assert.That(Str(board.GetStack(C(1, 0))), Is.EqualTo("BG"));
        }

        [Test]
        public void SameInput_ProducesIdenticalSteps()
        {
            BoardModel Make() => Board(2, (1, 0, "GB"), (-1, 0, "YB"), (0, 1, "B"), (1, -1, "RG"), (-1, 1, "G2"));

            var a = Place(Make(), 0, 0, "GRB");
            var b = Place(Make(), 0, 0, "GRB");
            Assert.That(Describe(a.Steps), Is.EqualTo(Describe(b.Steps)));
            Assert.That(a.Steps.Count, Is.GreaterThan(1));
        }

        /// <summary>
        /// Property test over simulated games: thousands of random placements must always end stable,
        /// conserve tiles, move tiles only between neighbours, and be fully described by their steps.
        /// </summary>
        [Test]
        public void RandomGames_SatisfyInvariants([Values(1, 2, 3, 4, 5)] int seed)
        {
            var rng = new Rng(seed);
            var settings = new GeneratorSettings { ColorCount = 3 + seed % 3, MinTiles = 2, MaxTiles = 7, MaxGroups = 3 };
            var generator = new StackGenerator(settings, seed * 7919);
            var resolver = new MergeResolver();
            var empty = new List<HexCoord>();

            for (int game = 0; game < 20; game++)
            {
                var board = new BoardModel(BoardShapes.Hexagon(2 + game % 2));
                // Clears free cells, so a game is not guaranteed to end: cap the placements.
                for (int move = 0; move < 200; move++)
                {
                    empty.Clear();
                    foreach (var cell in board.Cells)
                        if (cell.CanPlace) empty.Add(cell.Coord);
                    if (empty.Count == 0) break;

                    var coord = empty[rng.Next(empty.Count)];
                    board.Place(coord, generator.Generate());
                    var before = board.Clone();
                    int tilesBefore = board.TotalTileCount();

                    var result = resolver.Resolve(board, coord);

                    Assert.That(result.HitIterationLimit, Is.False, "iteration limit");
                    Assert.That(board.TotalTileCount() + result.TilesCleared, Is.EqualTo(tilesBefore), "conservation");
                    AssertStable(board, resolver.ClearThreshold);
                    AssertReplayMatches(before, result, board);
                }
            }
        }

        private static void AssertStable(BoardModel board, int threshold)
        {
            var neighbors = new List<BoardCell>();
            foreach (var cell in board.Cells)
            {
                if (cell.Stack == null) continue;
                Assert.That(cell.Stack.TopGroupCount, Is.LessThan(threshold), $"uncleared group at {cell.Coord}");
                board.GetNeighbors(cell.Coord, neighbors);
                foreach (var n in neighbors)
                    if (n.Stack != null)
                        Assert.That(n.Stack.TopColor, Is.Not.EqualTo(cell.Stack.TopColor),
                            $"unresolved match {cell.Coord}-{n.Coord}");
            }
        }

        /// <summary>Applies the steps to the pre-resolve board exactly like the view does.</summary>
        private static void AssertReplayMatches(BoardModel before, MergeResult result, BoardModel after)
        {
            foreach (var step in result.Steps)
            {
                switch (step)
                {
                    case TransferStep t:
                        Assert.That(t.From.IsAdjacentTo(t.To), $"non-adjacent {t}");
                        var from = before.GetStack(t.From);
                        var to = before.GetStack(t.To);
                        Assert.That(from.TopColor, Is.EqualTo(t.Color));
                        Assert.That(from.TopGroupCount, Is.EqualTo(t.Count), "partial group transfer");
                        Assert.That(to.TopColor, Is.EqualTo(t.Color), "transfer onto other color");
                        from.PopTop(t.Count);
                        to.Push(t.Color, t.Count);
                        if (from.IsEmpty) before.RemoveStack(t.From);
                        break;
                    case ClearStep c:
                        var s = before.GetStack(c.Cell);
                        Assert.That(s.TopColor, Is.EqualTo(c.Color));
                        Assert.That(s.TopGroupCount, Is.EqualTo(c.Count));
                        s.PopTop(c.Count);
                        if (s.IsEmpty) before.RemoveStack(c.Cell);
                        break;
                }
            }

            foreach (var cell in after.Cells)
                Assert.That(Str(before.GetStack(cell.Coord)), Is.EqualTo(Str(cell.Stack)), $"replay differs at {cell.Coord}");
        }
    }
}

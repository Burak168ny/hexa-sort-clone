using System.Collections.Generic;
using HexaSort.Model;
using NUnit.Framework;
using static HexaSort.Tests.TestBoards;

namespace HexaSort.Tests
{
    public class HexCoordTests
    {
        [Test]
        public void Neighbors_AreSixDistinctCellsAtDistanceOne()
        {
            var origin = C(2, -1);
            var seen = new HashSet<HexCoord>();
            for (int d = 0; d < HexCoord.DirectionCount; d++)
            {
                var n = origin.Neighbor(d);
                Assert.That(HexCoord.Distance(origin, n), Is.EqualTo(1));
                Assert.That(origin.IsAdjacentTo(n));
                seen.Add(n);
            }
            Assert.That(seen.Count, Is.EqualTo(6));
        }

        [Test]
        public void Distance_UsesCubeMetric()
        {
            Assert.That(HexCoord.Distance(C(0, 0), C(2, -1)), Is.EqualTo(2));
            Assert.That(HexCoord.Distance(C(-2, 0), C(2, 0)), Is.EqualTo(4));
            Assert.That(HexCoord.Distance(C(1, 1), C(1, 1)), Is.Zero);
        }

        [Test]
        public void EqualityAndHash_AreValueBased()
        {
            Assert.That(C(3, -2), Is.EqualTo(C(3, -2)));
            Assert.That(C(3, -2) == C(3, -2));
            Assert.That(C(3, -2) != C(-2, 3));
            Assert.That(C(3, -2).GetHashCode(), Is.EqualTo(C(3, -2).GetHashCode()));
        }

        [Test]
        public void PlanarConversion_RoundTripsCellCentres()
        {
            foreach (var c in BoardShapes.Hexagon(4))
            {
                HexLayout.ToPlanar(c, 0.5f, out float x, out float y);
                Assert.That(HexLayout.FromPlanar(x, y, 0.5f), Is.EqualTo(c));
            }
        }

        [Test]
        public void PlanarConversion_PointsInsideHexMapToThatHex()
        {
            HexLayout.ToPlanar(C(1, -1), 1f, out float x, out float y);
            // Flat-top: inner radius is sqrt(3)/2 ~ 0.866; offsets well inside it stay in the cell.
            Assert.That(HexLayout.FromPlanar(x + 0.7f, y, 1f), Is.EqualTo(C(1, -1)));
            Assert.That(HexLayout.FromPlanar(x, y - 0.8f, 1f), Is.EqualTo(C(1, -1)));
            Assert.That(HexLayout.FromPlanar(x, y + 1.0f, 1f), Is.EqualTo(C(1, 0)));
        }

        [Test]
        public void HexagonShape_HasExpectedCellCount()
        {
            Assert.That(BoardShapes.Hexagon(0).Count, Is.EqualTo(1));
            Assert.That(BoardShapes.Hexagon(2).Count, Is.EqualTo(19));
            Assert.That(BoardShapes.Hexagon(3).Count, Is.EqualTo(37));
        }

        [Test]
        public void RectangleShape_IsDistinctAndSized()
        {
            var cells = BoardShapes.Rectangle(5, 6);
            Assert.That(new HashSet<HexCoord>(cells).Count, Is.EqualTo(30));
        }
    }

    public class HexStackTests
    {
        [Test]
        public void TopGroup_CountsTrailingRun()
        {
            var s = S("RRGBB");
            Assert.That(s.TopColor, Is.EqualTo(B));
            Assert.That(s.TopGroupCount, Is.EqualTo(2));
            Assert.That(s.GroupCount, Is.EqualTo(3));
            Assert.That(s.IsUniform, Is.False);
        }

        [Test]
        public void EmptyStack_HasNoColor()
        {
            var s = new HexStack();
            Assert.That(s.TopColor, Is.EqualTo(HexStack.NoColor));
            Assert.That(s.TopGroupCount, Is.Zero);
            Assert.That(s.IsUniform, Is.False);
        }

        [Test]
        public void PushAndPop_UpdateTop()
        {
            var s = S("RG");
            s.Push(G, 3);
            Assert.That(s.TopGroupCount, Is.EqualTo(4));
            s.PopTop(4);
            Assert.That(Str(s), Is.EqualTo("R"));
            Assert.That(s.IsUniform);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var a = S("RGB");
            var b = a.Clone();
            b.PopTop(1);
            Assert.That(Str(a), Is.EqualTo("RGB"));
            Assert.That(Str(b), Is.EqualTo("RG"));
        }

        [Test]
        public void PopTooMany_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => S("RG").PopTop(3));
        }
    }

    public class BoardModelTests
    {
        [Test]
        public void Place_OccupiesCell()
        {
            var board = Board(1);
            board.Place(C(0, 0), S("R"));
            Assert.That(board.CanPlace(C(0, 0)), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => board.Place(C(0, 0), S("G")));
        }

        [Test]
        public void CannotPlaceOutsideOrOnLockedCells()
        {
            var board = new BoardModel();
            board.AddCell(C(0, 0));
            board.AddCell(C(1, 0), locked: true, unlockScore: 20);

            Assert.That(board.CanPlace(C(5, 5)), Is.False);
            Assert.That(board.CanPlace(C(1, 0)), Is.False);
            Assert.That(board.PlaceableCellCount(), Is.EqualTo(1));
        }

        [Test]
        public void UnlockCells_OpensOnlyCellsAtOrBelowScore()
        {
            var board = new BoardModel();
            board.AddCell(C(0, 0), locked: true, unlockScore: 10);
            board.AddCell(C(1, 0), locked: true, unlockScore: 30);
            var opened = new List<HexCoord>();

            Assert.That(board.UnlockCells(9, opened), Is.Zero);
            Assert.That(board.UnlockCells(10, opened), Is.EqualTo(1));
            Assert.That(opened, Is.EqualTo(new[] { C(0, 0) }));
            Assert.That(board.CanPlace(C(0, 0)));
            Assert.That(board.CanPlace(C(1, 0)), Is.False);
        }

        [Test]
        public void GetNeighbors_ReturnsOnlyExistingCells()
        {
            var board = Board(1);
            var result = new List<BoardCell>();
            board.GetNeighbors(C(1, 0), result);
            // Edge cell of a radius-1 hexagon: centre plus two ring neighbours.
            Assert.That(result.Count, Is.EqualTo(3));
        }

        [Test]
        public void Clone_IsDeep()
        {
            var board = Board(1, (0, 0, "RG"));
            var copy = board.Clone();
            copy.GetStack(C(0, 0)).PopTop(1);
            copy.Place(C(1, 0), S("B"));

            Assert.That(Str(board.GetStack(C(0, 0))), Is.EqualTo("RG"));
            Assert.That(board.CanPlace(C(1, 0)));
        }
    }

    public class StackGeneratorTests
    {
        [Test]
        public void Generated_StacksRespectSettings()
        {
            var settings = new GeneratorSettings { ColorCount = 4, MinTiles = 2, MaxTiles = 6, MaxGroups = 3 };
            var generator = new StackGenerator(settings, 42);
            for (int i = 0; i < 2000; i++)
            {
                var s = generator.Generate();
                Assert.That(s.Count, Is.InRange(2, 6));
                Assert.That(s.GroupCount, Is.InRange(1, 3));
                for (int t = 0; t < s.Count; t++) Assert.That(s[t], Is.InRange(0, 3));
            }
        }

        [Test]
        public void SameSeed_SameStacks_DifferentSeed_DifferentStacks()
        {
            var settings = new GeneratorSettings { ColorCount = 5 };
            var a = new StackGenerator(settings, 7);
            var b = new StackGenerator(settings, 7);
            var c = new StackGenerator(settings, 8);
            bool anyDifferent = false;
            for (int i = 0; i < 50; i++)
            {
                string sa = a.Generate().ToString();
                Assert.That(b.Generate().ToString(), Is.EqualTo(sa));
                anyDifferent |= c.Generate().ToString() != sa;
            }
            Assert.That(anyDifferent);
        }

        [Test]
        public void SingleColor_ProducesUniformStacks()
        {
            var generator = new StackGenerator(new GeneratorSettings { ColorCount = 1 }, 1);
            for (int i = 0; i < 100; i++) Assert.That(generator.Generate().IsUniform);
        }

        [Test]
        public void Rng_RangeIsInclusiveAndCoversAllValues()
        {
            var rng = new Rng(123);
            var seen = new HashSet<int>();
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.Range(3, 7);
                Assert.That(v, Is.InRange(3, 7));
                seen.Add(v);
            }
            Assert.That(seen.Count, Is.EqualTo(5));
        }

        [Test]
        public void Rng_FullIntRangeDoesNotOverflow()
        {
            var rng = new Rng(1);
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(rng.Range(0, int.MaxValue), Is.GreaterThanOrEqualTo(0));
                rng.Range(int.MinValue, int.MaxValue);
            }
        }
    }

    public class GameSessionTests
    {
        private static GameSession Session(BoardModel board, int target = 1000, int seed = 1) =>
            new GameSession(board, new StackGenerator(new GeneratorSettings(), seed), new LevelGoal(target), new MergeResolver());

        [Test]
        public void Tray_RefillsOnlyAfterAllSlotsAreUsed()
        {
            var session = Session(Board(3));
            var outcomes = new List<PlaceOutcome>();
            for (int slot = 0; slot < 3; slot++)
            {
                HexCoord cell = default;
                foreach (var c in session.Board.Cells)
                    if (c.CanPlace) { cell = c.Coord; break; }
                outcomes.Add(session.Place(slot, cell));
            }

            Assert.That(outcomes[0].TrayRefilled || outcomes[1].TrayRefilled, Is.False);
            Assert.That(outcomes[2].TrayRefilled);
            for (int slot = 0; slot < 3; slot++) Assert.That(session.Tray.Get(slot), Is.Not.Null);
        }

        [Test]
        public void UsedSlot_CannotBePlacedAgain()
        {
            var session = Session(Board(2));
            session.Place(0, C(0, 0));
            Assert.That(session.CanPlace(0, C(1, 0)), Is.False);
            Assert.That(session.CanPlace(1, C(1, 0)));
        }

        [Test]
        public void ReachingGoal_Wins()
        {
            var session = Session(Board(2, (1, 0, "B9")), target: 10);
            session.Tray.Set(0, S("B"));

            var outcome = session.Place(0, C(0, 0));

            Assert.That(outcome.Merge.TilesCleared, Is.EqualTo(10));
            Assert.That(outcome.IsWin);
            Assert.That(session.IsOver);
            Assert.That(session.Goal.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void NoEmptyCellLeft_Fails()
        {
            var board = new BoardModel(new[] { C(0, 0) });
            var session = Session(board);

            var outcome = session.Place(1, C(0, 0));

            Assert.That(outcome.IsFail);
            Assert.That(outcome.IsWin, Is.False);
            Assert.That(session.CanPlace(0, C(0, 0)), Is.False);
        }

        [Test]
        public void ClearingTiles_UnlocksCellsAtThreshold()
        {
            var board = new BoardModel();
            board.AddCell(C(0, 0));
            board.AddCell(C(1, 0));
            board.AddCell(C(-1, 0), locked: true, unlockScore: 10);
            board.Place(C(1, 0), S("B5"));
            var session = Session(board);
            session.Tray.Set(0, S("B5"));

            var outcome = session.Place(0, C(0, 0));

            Assert.That(outcome.UnlockedCells, Is.EqualTo(new[] { C(-1, 0) }));
            Assert.That(session.Board.CanPlace(C(-1, 0)));
            Assert.That(outcome.IsFail, Is.False);
        }
    }
}

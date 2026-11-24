using System.Collections.Generic;
using HexaSort.Model;
using NUnit.Framework;
using static HexaSort.Tests.TestBoards;

namespace HexaSort.Tests
{
    public class GeneratorBiasTests
    {
        private static GeneratorSettings Settings(double bias, bool mercy) =>
            new GeneratorSettings { ColorCount = 6, MinTiles = 2, MaxTiles = 6, MaxGroups = 3, MatchBias = bias, Mercy = mercy };

        [Test]
        public void MergeableTops_AreTopsNextToFreeCells()
        {
            // (0,0) is free; (1,0) top Y and (-1,0) top G touch it. (2,-2) is far from any free cell.
            var board = new BoardModel(new[] { C(0, 0), C(1, 0), C(-1, 0), C(1, -1), C(2, -2) });
            board.Place(C(1, 0), S("RY"));
            board.Place(C(-1, 0), S("G"));
            board.Place(C(1, -1), S("BP"));
            board.Place(C(2, -2), S("O"));
            // (1,-1) also touches the free (0,0), but (2,-2) only touches occupied cells.
            var tops = new List<int>();
            new StackGenerator(Settings(0.5, true), 1).CollectMergeableTops(board, tops);

            Assert.That(tops, Is.EquivalentTo(new[] { Y, G, P }));
        }

        [Test]
        public void FullBias_AlwaysProducesMergeableTop()
        {
            var board = Board(2, (1, 0, "RB"), (-1, 0, "G"));
            var generator = new StackGenerator(Settings(1.0, false), 3);
            for (int i = 0; i < 500; i++)
                Assert.That(generator.Generate(board).TopColor, Is.EqualTo(B).Or.EqualTo(G));
        }

        [Test]
        public void ZeroBias_UsesWholePalette()
        {
            var board = Board(2, (1, 0, "B"));
            var generator = new StackGenerator(Settings(0.0, false), 3);
            var seen = new HashSet<int>();
            for (int i = 0; i < 500; i++) seen.Add(generator.Generate(board).TopColor);
            Assert.That(seen.Count, Is.EqualTo(6));
        }

        [Test]
        public void GenerateWithTop_KeepsRequestedTopAndDistinctNeighbouringGroups()
        {
            var generator = new StackGenerator(Settings(0.5, true), 9);
            for (int i = 0; i < 500; i++)
            {
                var s = generator.GenerateWithTop(P);
                Assert.That(s.TopColor, Is.EqualTo(P));
                Assert.That(s.GroupCount, Is.InRange(1, 3));
                Assert.That(s.Count, Is.InRange(2, 6));
            }
        }

        [Test]
        public void Mercy_GuaranteesAMergeableStackInEveryRefill([Values(1, 2, 3)] int seed)
        {
            var rng = new Rng(seed);
            var settings = Settings(0.0, true); // no bias: only mercy can create the match
            var generator = new StackGenerator(settings, seed);
            var tray = new Tray();
            var tops = new List<int>();

            for (int i = 0; i < 300; i++)
            {
                var board = Random(rng, 2, 0.5, settings);
                generator.FillTray(tray, board);
                generator.CollectMergeableTops(board, tops);
                if (tops.Count == 0) continue;

                bool any = false;
                for (int s = 0; s < tray.SlotCount; s++) any |= tops.Contains(tray.Get(s).TopColor);
                Assert.That(any, $"refill {i} has no mergeable stack");
            }
        }

        [Test]
        public void WithoutMercyOrBias_TraysSometimesCannotMerge()
        {
            // Sanity check that the mercy test above is meaningful.
            var rng = new Rng(5);
            var settings = Settings(0.0, false);
            var generator = new StackGenerator(settings, 5);
            var tray = new Tray();
            var tops = new List<int>();
            int unmergeable = 0;

            for (int i = 0; i < 300; i++)
            {
                var board = Random(rng, 2, 0.3, settings);
                generator.FillTray(tray, board);
                generator.CollectMergeableTops(board, tops);
                bool any = false;
                for (int s = 0; s < tray.SlotCount; s++) any |= tops.Contains(tray.Get(s).TopColor);
                if (tops.Count > 0 && !any) unmergeable++;
            }
            Assert.That(unmergeable, Is.GreaterThan(0));
        }
    }

    public class ReviveTests
    {
        private static GameSession FailedSession()
        {
            var board = new BoardModel(BoardShapes.Hexagon(1));
            var session = new GameSession(board, new StackGenerator(new GeneratorSettings { ColorCount = 6, Mercy = false, MatchBias = 0 }, 4),
                new LevelGoal(1000), new MergeResolver());
            for (int guard = 0; guard < 100 && !session.IsOver; guard++)
            {
                for (int slot = 0; slot < session.Tray.SlotCount && !session.IsOver; slot++)
                {
                    if (session.Tray.Get(slot) == null) continue;
                    foreach (var cell in session.Board.Cells)
                        if (cell.CanPlace) { session.Place(slot, cell.Coord); break; }
                }
            }
            Assume.That(session.IsFailed, "setup should end in a fail");
            return session;
        }

        [Test]
        public void Revive_FreesTallestStacksAndResumes()
        {
            var session = FailedSession();
            var heights = new Dictionary<HexCoord, int>();
            foreach (var cell in session.Board.Cells) heights[cell.Coord] = cell.Stack.Count;

            var freed = session.Revive(3);

            Assert.That(freed.Count, Is.EqualTo(3));
            Assert.That(session.IsOver, Is.False);
            Assert.That(session.RevivesUsed, Is.EqualTo(1));
            Assert.That(session.Board.PlaceableCellCount(), Is.EqualTo(3));

            int minFreed = int.MaxValue;
            foreach (var c in freed) minFreed = System.Math.Min(minFreed, heights[c]);
            foreach (var cell in session.Board.Cells)
                if (cell.Stack != null) Assert.That(cell.Stack.Count, Is.LessThanOrEqualTo(minFreed));
        }

        [Test]
        public void Revive_RequiresFail()
        {
            var session = new GameSession(Board(2), new StackGenerator(new GeneratorSettings(), 1), new LevelGoal(10), new MergeResolver());
            Assert.Throws<System.InvalidOperationException>(() => session.Revive(3));
        }
    }

    public class LevelDefinitionTests
    {
        [Test]
        public void CreateBoard_AppliesLocksAndPreplacedStacks()
        {
            var def = new LevelDefinition { Generator = { ColorCount = 3 } };
            def.Cells.Add(new CellSpec(C(0, 0)));
            def.Cells.Add(new CellSpec(C(1, 0), locked: true, unlockScore: 20));
            def.Cells.Add(new CellSpec(C(-1, 0)));
            def.Preplaced.Add(new PreplacedStack(C(-1, 0), new[] { R, R, G }));
            def.Validate();

            var board = def.CreateBoard();
            Assert.That(board.CanPlace(C(1, 0)), Is.False);
            Assert.That(Str(board.GetStack(C(-1, 0))), Is.EqualTo("RRG"));
            Assert.That(board.CanPlace(C(0, 0)));
        }

        [Test]
        public void Validate_RejectsPreplacedColorOutsidePalette()
        {
            var def = new LevelDefinition { Generator = { ColorCount = 2 } };
            def.AddCells(BoardShapes.Hexagon(1));
            def.Preplaced.Add(new PreplacedStack(C(0, 0), new[] { B }));
            Assert.Throws<System.InvalidOperationException>(() => def.Validate());
        }

        [Test]
        public void Sessions_AreReproduciblePerAttempt_AndDifferBetweenAttempts()
        {
            var def = ProceduralLevels.Create(5);
            string Tray(GameSession s) => $"{s.Tray.Get(0)}{s.Tray.Get(1)}{s.Tray.Get(2)}";

            Assert.That(Tray(def.CreateSession(0)), Is.EqualTo(Tray(def.CreateSession(0))));
            Assert.That(Tray(def.CreateSession(0)), Is.Not.EqualTo(Tray(def.CreateSession(1))));
        }

        [Test]
        public void ProceduralLevels_AreValidForAnyNumber()
        {
            for (int n = 1; n <= 100; n++)
            {
                var def = ProceduralLevels.Create(n);
                Assert.DoesNotThrow(def.Validate, $"level {n}");
                Assert.That(def.Number, Is.EqualTo(n));
                Assert.That(def.Generator.ColorCount, Is.InRange(2, ProceduralLevels.MaxColors));
            }
        }

        [Test]
        public void ProceduralLevels_UseCampaignFirst_ThenRemixWithGrowingTargets()
        {
            Assert.That(ProceduralLevels.Create(7).Name, Is.EqualTo(Campaign.Create(7).Name));

            int pool = Campaign.LevelCount - ProceduralLevels.FirstRemixed + 1;
            var lap1 = ProceduralLevels.Create(Campaign.LevelCount + 1);
            var lap2 = ProceduralLevels.Create(Campaign.LevelCount + 1 + pool);
            var source = Campaign.Create(ProceduralLevels.FirstRemixed);

            Assert.That(lap1.Cells.Count, Is.EqualTo(source.Cells.Count));
            Assert.That(lap1.TargetTiles, Is.GreaterThan(source.TargetTiles));
            Assert.That(lap2.TargetTiles, Is.GreaterThan(lap1.TargetTiles));
            Assert.That(lap1.Seed, Is.Not.EqualTo(lap2.Seed));
        }
    }

    public class ProgressServiceTests
    {
        [Test]
        public void DefaultsToLevelOneWithSettingsOn()
        {
            var progress = new ProgressService(new MemoryStore());
            Assert.That(progress.LevelNumber, Is.EqualTo(1));
            Assert.That(progress.SoundEnabled && progress.HapticsEnabled);
        }

        [Test]
        public void CompleteLevel_PersistsAcrossInstances()
        {
            var store = new MemoryStore();
            var progress = new ProgressService(store);
            progress.CompleteLevel();
            progress.CompleteLevel();

            Assert.That(new ProgressService(store).LevelNumber, Is.EqualTo(3));
            Assert.That(store.SaveCount, Is.EqualTo(2));
        }

        [Test]
        public void Settings_PersistAndNotify()
        {
            var store = new MemoryStore();
            var progress = new ProgressService(store);
            int notifications = 0;
            progress.SettingsChanged += () => notifications++;

            progress.SetHaptics(false);
            progress.SetHaptics(false); // no change, no event
            progress.SetSound(false);

            Assert.That(notifications, Is.EqualTo(2));
            var reloaded = new ProgressService(store);
            Assert.That(reloaded.HapticsEnabled, Is.False);
            Assert.That(reloaded.SoundEnabled, Is.False);
        }

        [Test]
        public void CorruptStoredLevel_IsClampedToOne()
        {
            var store = new MemoryStore();
            store.SetInt(ProgressService.LevelKey, -4);
            Assert.That(new ProgressService(store).LevelNumber, Is.EqualTo(1));
        }
    }
}

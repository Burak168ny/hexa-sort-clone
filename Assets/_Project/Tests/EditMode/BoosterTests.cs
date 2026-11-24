using HexaSort.Model;
using NUnit.Framework;
using static HexaSort.Tests.TestBoards;

namespace HexaSort.Tests
{
    public class BoosterTests
    {
        private static GameSession Session(BoardModel board, int target = 1000) =>
            new GameSession(board, new StackGenerator(new GeneratorSettings { ColorCount = 5 }, 3), new LevelGoal(target), new MergeResolver());

        [Test]
        public void Hammer_RemovesStackWithoutScoring()
        {
            var session = Session(Board(2, (1, 0, "RRGB")));
            Assert.That(session.CanHammer(C(1, 0)));
            Assert.That(session.CanHammer(C(0, 0)), Is.False);

            session.Hammer(C(1, 0));

            Assert.That(session.Board.CanPlace(C(1, 0)));
            Assert.That(session.Goal.ClearedTiles, Is.Zero);
        }

        [Test]
        public void Move_RelocatesStackAndResolvesMergesAtDestination()
        {
            // Moving B4 from (-2,0) next to B6 at (1,0) merges into ten and clears.
            var session = Session(Board(2, (-2, 0, "B4"), (1, 0, "B6")), target: 10);
            Assert.That(session.CanMove(C(-2, 0), C(0, 0)));
            Assert.That(session.CanMove(C(-2, 0), C(1, 0)), Is.False, "destination must be empty");

            var outcome = session.Move(C(-2, 0), C(0, 0));

            Assert.That(outcome.Slot, Is.EqualTo(-1));
            Assert.That(outcome.MovedFrom, Is.EqualTo(C(-2, 0)));
            Assert.That(outcome.Merge.TilesCleared, Is.EqualTo(10));
            Assert.That(outcome.IsWin);
            Assert.That(session.Board.TotalTileCount(), Is.Zero);
        }

        [Test]
        public void Move_DoesNotTouchTheTray()
        {
            var session = Session(Board(2, (-2, 0, "R")));
            string before = $"{session.Tray.Get(0)}{session.Tray.Get(1)}{session.Tray.Get(2)}";
            var outcome = session.Move(C(-2, 0), C(2, 0));
            Assert.That(outcome.TrayRefilled, Is.False);
            Assert.That($"{session.Tray.Get(0)}{session.Tray.Get(1)}{session.Tray.Get(2)}", Is.EqualTo(before));
        }

        [Test]
        public void Shuffle_RefillsEverySlot()
        {
            var session = Session(Board(2));
            session.Place(0, C(0, 0));
            Assert.That(session.Tray.Get(0), Is.Null);

            session.ShuffleTray();

            for (int i = 0; i < session.Tray.SlotCount; i++) Assert.That(session.Tray.Get(i), Is.Not.Null);
        }

        [Test]
        public void Inventory_StartsAtStartingCount_ConsumesAndPersists()
        {
            var store = new MemoryStore();
            var inventory = new BoosterInventory(store);
            BoosterType? changed = null;
            inventory.Changed += (t, n) => changed = t;

            foreach (var type in BoosterInventory.All) Assert.That(inventory.Count(type), Is.EqualTo(BoosterInventory.StartingCount));
            Assert.That(inventory.TryConsume(BoosterType.Hammer));
            Assert.That(changed, Is.EqualTo(BoosterType.Hammer));
            inventory.Add(BoosterType.Shuffle, 3);

            var reloaded = new BoosterInventory(store);
            Assert.That(reloaded.Count(BoosterType.Hammer), Is.EqualTo(BoosterInventory.StartingCount - 1));
            Assert.That(reloaded.Count(BoosterType.Shuffle), Is.EqualTo(BoosterInventory.StartingCount + 3));
            Assert.That(reloaded.Count(BoosterType.Move), Is.EqualTo(BoosterInventory.StartingCount));
        }

        [Test]
        public void Inventory_RunsOutAtZero()
        {
            var store = new MemoryStore();
            var inventory = new BoosterInventory(store);
            for (int i = 0; i < BoosterInventory.StartingCount; i++) Assert.That(inventory.TryConsume(BoosterType.Move));
            Assert.That(inventory.TryConsume(BoosterType.Move), Is.False);
            Assert.That(inventory.Count(BoosterType.Move), Is.Zero);
        }

        [Test]
        public void Inventory_TopsUpOldSavesOnce()
        {
            // A save from the old version: 1 hammer left, no grant marker.
            var store = new MemoryStore();
            store.SetInt(BoosterInventory.KeyFor(BoosterType.Hammer), 1);

            var inventory = new BoosterInventory(store);
            Assert.That(inventory.Count(BoosterType.Hammer), Is.EqualTo(BoosterInventory.StartingCount));

            inventory.TryConsume(BoosterType.Hammer);
            Assert.That(new BoosterInventory(store).Count(BoosterType.Hammer), Is.EqualTo(BoosterInventory.StartingCount - 1),
                "the top-up must not repeat on later launches");
        }
    }
}

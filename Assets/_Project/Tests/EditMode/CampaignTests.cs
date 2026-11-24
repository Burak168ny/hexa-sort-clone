using HexaSort.Model;
using HexaSort.Model.Simulation;
using NUnit.Framework;

namespace HexaSort.Tests
{
    /// <summary>Guards the authored campaign: structure, tutorial rules and bot-measured difficulty.</summary>
    public class CampaignTests
    {
        [Test]
        public void AllLevelsAreValidAndNumbered()
        {
            var levels = Campaign.CreateAll();
            Assert.That(levels.Count, Is.EqualTo(Campaign.LevelCount));
            for (int i = 0; i < levels.Count; i++)
            {
                Assert.That(levels[i].Number, Is.EqualTo(i + 1));
                Assert.That(levels[i].Name, Is.Not.Empty);
                Assert.DoesNotThrow(levels[i].Validate);
            }
        }

        [Test]
        public void TutorialLevels_AreSmallWithFewColorsAndExplainThemselves()
        {
            for (int n = 1; n <= 3; n++)
            {
                var level = Campaign.Create(n);
                Assert.That(level.Cells.Count, Is.LessThanOrEqualTo(19), $"level {n}");
                Assert.That(level.Generator.ColorCount, Is.InRange(2, 3), $"level {n}");
                Assert.That(level.TutorialText, Is.Not.Empty, $"level {n}");
            }
        }

        [Test]
        public void LockedCellsAppear_AndCanAlwaysOpenBeforeTheGoal()
        {
            bool anyLocks = false;
            foreach (var level in Campaign.CreateAll())
                foreach (var cell in level.Cells)
                {
                    if (!cell.Locked) continue;
                    anyLocks = true;
                    Assert.That(cell.UnlockScore, Is.LessThan(level.TargetTiles), $"level {level.Number} lock at {cell.Coord}");
                }
            Assert.That(anyLocks);
        }

        [Test]
        public void ColorsNeverExceedPalette()
        {
            foreach (var level in Campaign.CreateAll())
                Assert.That(level.Generator.ColorCount, Is.InRange(2, ProceduralLevels.MaxColors));
        }

        /// <summary>
        /// Difficulty floor measured with the greedy bot (simulations are seeded, so this is deterministic).
        /// Calibrated values: tutorials 100%, level 20 ~68%, level 100 ~40%. Floors leave headroom.
        /// </summary>
        [Test]
        public void EveryLevelIsWinnableByTheBot([Range(1, Campaign.LevelCount)] int number)
        {
            var level = Campaign.Create(number);
            var stats = LevelSimulator.Run(level, games: number <= Campaign.HandcraftedCount ? 30 : 15);
            double floor = number <= 3 ? 0.95 : number <= 9 ? 0.8 : number <= Campaign.HandcraftedCount ? 0.35 : 0.15;
            Assert.That(stats.WinRate, Is.GreaterThanOrEqualTo(floor), stats.ToString());
        }

        [Test]
        public void DifficultyRisesAcrossTheCampaign()
        {
            double Average(int from, int to)
            {
                double sum = 0;
                for (int n = from; n <= to; n++) sum += LevelSimulator.Run(Campaign.Create(n), games: 15).WinRate;
                return sum / (to - from + 1);
            }

            double early = Average(21, 40), middle = Average(51, 70), late = Average(81, 100);
            Assert.That(middle, Is.LessThan(early), $"early {early:P0} middle {middle:P0}");
            Assert.That(late, Is.LessThan(middle), $"middle {middle:P0} late {late:P0}");
        }

        [Test]
        public void LateLevelNamesAreUnique()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var level in Campaign.CreateAll()) Assert.That(names.Add(level.Name), $"duplicate name {level.Name}");
        }
    }
}

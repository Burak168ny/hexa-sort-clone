using HexaSort.Core.Levels;
using NUnit.Framework;
using UnityEditor;

namespace HexaSort.Tests
{
    /// <summary>Checks the shipped LevelData assets (Unity only: needs the AssetDatabase).</summary>
    public class LevelAssetTests
    {
        private const string DatabasePath = "Assets/_Project/Levels/LevelDatabase.asset";

        [Test]
        public void Database_ContainsTwentyValidLevels()
        {
            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            Assume.That(db, Is.Not.Null, "Run Tools/HexaSort/Setup Game Scene (or Rebuild Campaign Levels) first.");

            Assert.That(db.Count, Is.GreaterThanOrEqualTo(20));
            for (int n = 1; n <= db.Count; n++)
            {
                var data = db.Get(n);
                Assert.That(data, Is.Not.Null, $"level {n} missing");
                Assert.DoesNotThrow(() => data.ToDefinition(n).Validate(), $"level {n} ({data.name})");
            }
        }

        [Test]
        public void AssetRoundTrip_PreservesDefinition()
        {
            var def = Model.Campaign.Create(20);
            var data = UnityEngine.ScriptableObject.CreateInstance<LevelData>();
            data.CopyFrom(def);
            var back = data.ToDefinition(20);

            Assert.That(back.Cells.Count, Is.EqualTo(def.Cells.Count));
            Assert.That(back.Preplaced.Count, Is.EqualTo(def.Preplaced.Count));
            Assert.That(back.TargetTiles, Is.EqualTo(def.TargetTiles));
            Assert.That(back.Generator.ColorCount, Is.EqualTo(def.Generator.ColorCount));
            Assert.That(back.TutorialText, Is.EqualTo(def.TutorialText));
            UnityEngine.Object.DestroyImmediate(data);
        }
    }
}

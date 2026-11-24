using HexaSort.Core.Levels;
using HexaSort.Model;
using UnityEngine;

namespace HexaSort.Core
{
    /// <summary>
    /// Supplies the definition for a level number: authored <see cref="LevelData"/> from the database,
    /// then an endless remix of those levels (<see cref="ProceduralLevels"/>). Without a database it
    /// falls back to the built-in <see cref="Campaign"/>, so the game always has content.
    /// </summary>
    public sealed class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelDatabase _database;

        public LevelDatabase Database => _database;

        public LevelDefinition GetLevel(int number)
        {
            LevelDefinition definition;
            if (HasValidDatabase())
            {
                // Assets win where they exist; campaign levels the database does not have yet come from code.
                int authored = Mathf.Max(_database.Count, Campaign.LevelCount);
                definition = ProceduralLevels.Create(number,
                    n => n <= _database.Count ? _database.Get(n).ToDefinition(n) : Campaign.Create(n), authored);
            }
            else
            {
                definition = ProceduralLevels.Create(number);
            }

            definition.Validate();
            return definition;
        }

        private bool HasValidDatabase()
        {
            if (_database == null || _database.Count == 0) return false;
            for (int i = 1; i <= _database.Count; i++)
            {
                if (_database.Get(i) != null) continue;
                Debug.LogWarning($"[LevelManager] Level {i} is missing from the database; using the built-in campaign.", this);
                return false;
            }
            return true;
        }
    }
}

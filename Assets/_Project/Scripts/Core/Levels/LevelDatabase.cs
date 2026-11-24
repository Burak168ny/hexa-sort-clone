using System.Collections.Generic;
using UnityEngine;

namespace HexaSort.Core.Levels
{
    /// <summary>Ordered list of authored levels; index 0 is level 1.</summary>
    [CreateAssetMenu(menuName = "HexaSort/Level Database", fileName = "LevelDatabase")]
    public sealed class LevelDatabase : ScriptableObject
    {
        [SerializeField] private List<LevelData> _levels = new List<LevelData>();

        public List<LevelData> Levels => _levels;
        public int Count => _levels.Count;

        /// <summary>Level by 1-based number, or null if out of range or missing.</summary>
        public LevelData Get(int number) => number >= 1 && number <= _levels.Count ? _levels[number - 1] : null;
    }
}

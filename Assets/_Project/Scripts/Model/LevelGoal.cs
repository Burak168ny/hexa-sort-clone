using System;

namespace HexaSort.Model
{
    /// <summary>"Clear N tiles" objective. Cleared tiles also serve as the score for unlocking cells.</summary>
    public sealed class LevelGoal
    {
        public int TargetTiles { get; }
        public int ClearedTiles { get; private set; }

        public bool IsComplete => ClearedTiles >= TargetTiles;
        public float Progress => TargetTiles == 0 ? 1f : Math.Min(1f, ClearedTiles / (float)TargetTiles);

        public LevelGoal(int targetTiles)
        {
            if (targetTiles < 0) throw new ArgumentOutOfRangeException(nameof(targetTiles));
            TargetTiles = targetTiles;
        }

        public void AddCleared(int tiles)
        {
            if (tiles < 0) throw new ArgumentOutOfRangeException(nameof(tiles));
            ClearedTiles += tiles;
        }
    }
}

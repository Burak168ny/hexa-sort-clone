namespace HexaSort.Model
{
    /// <summary>One hex slot of the board. It is locked, empty, or holds exactly one stack.</summary>
    public sealed class BoardCell
    {
        public HexCoord Coord { get; }

        /// <summary>A locked cell opens once the level score reaches <see cref="UnlockScore"/>.</summary>
        public bool IsLocked { get; internal set; }
        public int UnlockScore { get; }

        /// <summary>The stack on this cell, or null when empty.</summary>
        public HexStack Stack { get; internal set; }

        public bool IsEmpty => Stack == null;
        public bool CanPlace => !IsLocked && Stack == null;

        public BoardCell(HexCoord coord, bool locked = false, int unlockScore = 0)
        {
            Coord = coord;
            IsLocked = locked;
            UnlockScore = unlockScore;
        }
    }
}

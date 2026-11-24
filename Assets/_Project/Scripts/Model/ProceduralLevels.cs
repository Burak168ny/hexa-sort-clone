using System;

namespace HexaSort.Model
{
    /// <summary>
    /// Level for any number: the authored campaign for 1..<see cref="Campaign.LevelCount"/>, then an
    /// endless remix of the calibrated second half of the campaign (level 51 onwards). Every lap uses new
    /// seeds and raises the targets a little, so difficulty keeps creeping up from known-good layouts.
    /// </summary>
    public static class ProceduralLevels
    {
        public const int MaxColors = 8;
        public const int FirstRemixed = 51;
        private const double TargetGrowthPerLap = 0.08;
        private const double MaxTargetScale = 1.6;

        public static LevelDefinition Create(int number) => Create(number, Campaign.Create, Campaign.LevelCount);

        /// <summary>
        /// As <see cref="Create(int)"/>, but authored levels 1..<paramref name="authoredCount"/> come from
        /// <paramref name="authored"/> (e.g. the edited LevelData assets), which must return a fresh definition.
        /// </summary>
        public static LevelDefinition Create(int number, Func<int, LevelDefinition> authored, int authoredCount)
        {
            if (authored == null) throw new ArgumentNullException(nameof(authored));
            if (authoredCount < 1) throw new ArgumentOutOfRangeException(nameof(authoredCount));
            int n = Math.Max(1, number);
            if (n <= authoredCount) return authored(n);

            int first = Math.Min(FirstRemixed, authoredCount);
            int pool = authoredCount - first + 1;
            int index = n - authoredCount - 1;
            int lap = index / pool + 1;

            var def = authored(first + index % pool);
            def.Number = n;
            def.Name = "";
            def.TutorialText = "";
            def.Seed = unchecked(n * 7919 + 13);
            double scale = Math.Min(MaxTargetScale, 1.0 + TargetGrowthPerLap * lap);
            def.TargetTiles = (int)Math.Round(def.TargetTiles * scale / 10.0) * 10;
            return def;
        }
    }
}

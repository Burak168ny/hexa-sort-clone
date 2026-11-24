using System;
using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>
    /// Levels 21-100: each one is a board template (shape, palette size, locks) plus a target.
    /// Template and target per level were chosen by the calibration tool: for every level a desired
    /// bot win rate is set (88% at level 21 sliding to 45% at level 100, breathers on x5, bosses on
    /// x0), then the template whose required target best matches the level's intended length is
    /// picked, and the target is read off the template's measured capacity distribution.
    /// </summary>
    public static partial class Campaign
    {
        private sealed class Template
        {
            public string Name;
            public int Colors;
            public int MaxTiles = 6;
            public double Bias = 0.35;
            /// <summary>Builds the board; lock scores are fractions of the level target so locks always open in time.</summary>
            public Action<Builder, int> Board;
        }

        private static readonly Template[] Templates =
        {
            new Template { Name = "Courtyard", Colors = 7, Board = (b, target) => b.Cells(BoardShapes.Rectangle(5, 6)) },
            new Template { Name = "Plaza", Colors = 7, Board = (b, target) => b.Cells(BoardShapes.Rectangle(5, 5)) },
            new Template { Name = "Honeycomb", Colors = 7, Board = (b, target) => b.Cells(BoardShapes.Hexagon(2)) },
            new Template { Name = "Crate", Colors = 7, Board = (b, target) => b.Cells(BoardShapes.Rectangle(4, 5)) },
            new Template { Name = "Orbit", Colors = 7, Board = (b, target) => b.Cells(BoardShapes.Ring(3, 1)) },
            new Template
            {
                Name = "Colonnade", Colors = 8,
                Board = (b, target) => b.Cells(BoardShapes.Rectangle(5, 7))
                    .Remove(Rect(1, 2, 5, 7), Rect(3, 2, 5, 7), Rect(1, 4, 5, 7), Rect(3, 4, 5, 7)),
            },
            new Template
            {
                Name = "Waist", Colors = 8,
                Board = (b, target) => b.Cells(BoardShapes.Rectangle(5, 6))
                    .Remove(Rect(0, 2, 5, 6), Rect(4, 2, 5, 6), Rect(0, 3, 5, 6), Rect(4, 3, 5, 6)),
            },
            new Template
            {
                Name = "Atoll", Colors = 8,
                Board = (b, target) => b.Cells(BoardShapes.Hexagon(3))
                    .Remove(Hex(0, 0), Hex(2, -1), Hex(-2, 1), Hex(1, 1), Hex(-1, -1)),
            },
            new Template { Name = "Square Dance", Colors = 8, Board = (b, target) => b.Cells(BoardShapes.Rectangle(5, 5)) },
            new Template { Name = "Eclipse", Colors = 8, Board = (b, target) => b.Cells(BoardShapes.Ring(3, 1)) },
            new Template { Name = "Hive", Colors = 8, Board = (b, target) => b.Cells(BoardShapes.Hexagon(2)) },
            new Template { Name = "Matchbox", Colors = 8, Board = (b, target) => b.Cells(BoardShapes.Rectangle(4, 5)) },
            new Template
            {
                Name = "Strongroom", Colors = 8,
                Board = (b, target) => b.Cells(BoardShapes.Rectangle(5, 6))
                    .Lock(Rect(0, 0, 5, 6), Frac(target, 0.3)).Lock(Rect(2, 0, 5, 6), Frac(target, 0.6)).Lock(Rect(4, 0, 5, 6), Frac(target, 0.3))
                    .Lock(Rect(0, 5, 5, 6), Frac(target, 0.85)).Lock(Rect(2, 5, 5, 6), Frac(target, 0.6)).Lock(Rect(4, 5, 5, 6), Frac(target, 0.85)),
            },
            new Template
            {
                Name = "Tiara", Colors = 8, MaxTiles = 7,
                Board = (b, target) => b.Cells(BoardShapes.Hexagon(3))
                    .Lock(Hex(3, 0), Frac(target, 0.25)).Lock(Hex(-3, 0), Frac(target, 0.25)).Lock(Hex(0, 3), Frac(target, 0.5))
                    .Lock(Hex(0, -3), Frac(target, 0.5)).Lock(Hex(3, -3), Frac(target, 0.75)).Lock(Hex(-3, 3), Frac(target, 0.75)),
            },
            new Template { Name = "Rhombus", Colors = 8, Board = (b, target) => b.Cells(BoardShapes.Parallelogram(5, 5)) },
            new Template
            {
                Name = "Sanctum", Colors = 8, MaxTiles = 7,
                Board = (b, target) => b.Cells(BoardShapes.Ring(3, 0))
                    .Lock(Hex(1, 0), Frac(target, 0.25)).Lock(Hex(-1, 0), Frac(target, 0.25)).Lock(Hex(0, 1), Frac(target, 0.5))
                    .Lock(Hex(0, -1), Frac(target, 0.5)).Lock(Hex(1, -1), Frac(target, 0.75)).Lock(Hex(-1, 1), Frac(target, 0.75)),
            },
            new Template { Name = "Corridor", Colors = 8, Board = (b, target) => b.Cells(BoardShapes.Rectangle(4, 6)) },
            new Template { Name = "Arena", Colors = 8, Board = (b, target) => b.Cells(BoardShapes.Rectangle(6, 6)) },
        };

        public static int TemplateCount => Templates.Length;

        // Calibrated by the simulation tool (one entry per level 21..100).
        private static readonly int[] LateTemplates =
        {
            7, 9, 5, 6, 0, 7, 2, 5, 9, 11,
            2, 7, 3, 9, 1, 4, 5, 9, 16, 10,
            5, 6, 7, 4, 0, 16, 5, 9, 7, 11,
            6, 14, 5, 7, 12, 10, 11, 9, 6, 10,
            16, 7, 5, 11, 13, 9, 7, 6, 10, 11,
            16, 9, 5, 8, 15, 16, 10, 14, 9, 11,
            7, 10, 8, 5, 17, 14, 6, 9, 11, 10,
            16, 5, 11, 7, 12, 9, 8, 16, 11, 10,
        };

        private static readonly int[] LateTargets =
        {
            120, 120, 130, 140, 120, 120, 200, 140, 140, 100,
            210, 140, 220, 140, 140, 230, 160, 140, 220, 100,
            160, 210, 170, 260, 160, 240, 170, 170, 180, 130,
            250, 290, 180, 190, 180, 120, 130, 190, 280, 140,
            320, 210, 190, 130, 210, 210, 210, 300, 130, 170,
            350, 220, 200, 360, 230, 360, 150, 380, 240, 210,
            220, 160, 390, 220, 250, 400, 410, 260, 200, 200,
            420, 250, 210, 250, 270, 280, 440, 450, 210, 220,
        };

        private static LevelDefinition CreateLate(int number)
        {
            int index = number - HandcraftedCount - 1;
            if (index >= LateTemplates.Length) throw new InvalidOperationException($"Level {number} is not calibrated yet.");
            int template = LateTemplates[index];

            int occurrence = 1;
            for (int i = 0; i < index; i++)
                if (LateTemplates[i] == template) occurrence++;
            return FromTemplate(number, template, LateTargets[index], $"{Templates[template].Name} {Roman(occurrence)}");
        }

        /// <summary>Builds level <paramref name="number"/> from a template (used by the level table and the calibration tool).</summary>
        public static LevelDefinition FromTemplate(int number, int template, int target, string name = null)
        {
            var t = Templates[template];
            var builder = New(number, name ?? t.Name, t.Colors, target, (2, t.MaxTiles), groups: 3, t.Bias);
            t.Board(builder, target);
            return builder.Build();
        }

        /// <summary>Unlock score as a share of the target, rounded to tens.</summary>
        private static int Frac(int target, double share) => Math.Max(10, (int)Math.Round(target * share / 10.0) * 10);

        private static string Roman(int n)
        {
            string[] ones = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
            string[] tens = { "", "X", "XX", "XXX" };
            return n >= 1 && n < 40 ? tens[n / 10] + ones[n % 10] : n.ToString();
        }
    }
}

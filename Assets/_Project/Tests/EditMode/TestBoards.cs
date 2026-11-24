using System;
using System.Collections.Generic;
using System.Text;
using HexaSort.Model;

namespace HexaSort.Tests
{
    /// <summary>
    /// Compact notation for tests. A stack is a string of color letters bottom to top,
    /// e.g. "RRB" = two red tiles under one blue. Digits after a letter repeat it: "B5" = five blue.
    /// </summary>
    internal static class TestBoards
    {
        public const int R = 0, G = 1, B = 2, Y = 3, P = 4, O = 5;
        private const string Letters = "RGBYPO";

        public static HexStack S(string notation)
        {
            var stack = new HexStack();
            for (int i = 0; i < notation.Length; i++)
            {
                int color = Letters.IndexOf(notation[i]);
                if (color < 0) throw new ArgumentException($"Unknown color '{notation[i]}' in \"{notation}\".");

                int j = i + 1;
                while (j < notation.Length && char.IsDigit(notation[j])) j++;
                int count = j > i + 1 ? int.Parse(notation.Substring(i + 1, j - i - 1)) : 1;
                stack.Push(color, count);
                i = j - 1;
            }
            return stack;
        }

        /// <summary>Expanded notation of a stack ("RRB"), or "" for null/empty.</summary>
        public static string Str(HexStack stack)
        {
            if (stack == null) return "";
            var sb = new StringBuilder(stack.Count);
            for (int i = 0; i < stack.Count; i++) sb.Append(Letters[stack[i]]);
            return sb.ToString();
        }

        public static HexCoord C(int q, int r) => new HexCoord(q, r);

        /// <summary>Hexagonal board of <paramref name="radius"/> with stacks pre-placed.</summary>
        public static BoardModel Board(int radius, params (int q, int r, string stack)[] stacks)
        {
            var board = new BoardModel(BoardShapes.Hexagon(radius));
            foreach (var (q, r, s) in stacks) board.Place(C(q, r), S(s));
            return board;
        }

        public static string Describe(IEnumerable<MergeStep> steps)
        {
            var parts = new List<string>();
            foreach (var step in steps) parts.Add(step.ToString());
            return string.Join(" | ", parts);
        }

        /// <summary>Random board for property tests: about <paramref name="fill"/> of the cells hold a stack.</summary>
        public static BoardModel Random(Rng rng, int radius, double fill, GeneratorSettings settings)
        {
            var board = new BoardModel(BoardShapes.Hexagon(radius));
            var generator = new StackGenerator(settings, rng.Range(0, int.MaxValue));
            foreach (var cell in board.Cells)
                if (rng.NextDouble() < fill) board.Place(cell.Coord, generator.Generate());
            return board;
        }
    }
}

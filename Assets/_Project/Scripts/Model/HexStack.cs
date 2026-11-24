using System;
using System.Collections.Generic;
using System.Text;

namespace HexaSort.Model
{
    /// <summary>
    /// A pile of hex tiles, stored bottom to top. A tile is fully described by its color id,
    /// so tiles are plain ints rather than objects. Equal colors that touch form a "group".
    /// </summary>
    public sealed class HexStack
    {
        public const int NoColor = -1;

        private readonly List<int> _tiles;

        public HexStack()
        {
            _tiles = new List<int>(8);
        }

        private HexStack(List<int> tiles)
        {
            _tiles = tiles;
        }

        public int Count => _tiles.Count;
        public bool IsEmpty => _tiles.Count == 0;

        /// <summary>Color of tile <paramref name="index"/>, where 0 is the bottom.</summary>
        public int this[int index] => _tiles[index];

        public int TopColor => _tiles.Count == 0 ? NoColor : _tiles[_tiles.Count - 1];

        /// <summary>Number of tiles in the top same-color run.</summary>
        public int TopGroupCount
        {
            get
            {
                int count = _tiles.Count;
                if (count == 0) return 0;

                int top = _tiles[count - 1];
                int run = 1;
                for (int i = count - 2; i >= 0 && _tiles[i] == top; i--) run++;
                return run;
            }
        }

        /// <summary>Number of same-color runs from bottom to top.</summary>
        public int GroupCount
        {
            get
            {
                int groups = 0;
                for (int i = 0; i < _tiles.Count; i++)
                    if (i == 0 || _tiles[i] != _tiles[i - 1]) groups++;
                return groups;
            }
        }

        /// <summary>True when the stack is non-empty and every tile has the same color.</summary>
        public bool IsUniform => _tiles.Count > 0 && TopGroupCount == _tiles.Count;

        public void Push(int color, int count = 1)
        {
            if (color < 0) throw new ArgumentOutOfRangeException(nameof(color));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++) _tiles.Add(color);
        }

        /// <summary>Removes <paramref name="count"/> tiles from the top.</summary>
        public void PopTop(int count)
        {
            if (count < 0 || count > _tiles.Count) throw new ArgumentOutOfRangeException(nameof(count));
            _tiles.RemoveRange(_tiles.Count - count, count);
        }

        public HexStack Clone() => new HexStack(new List<int>(_tiles));

        /// <summary>Builds a stack from (color, count) groups listed bottom to top.</summary>
        public static HexStack FromGroups(params (int color, int count)[] groups)
        {
            var stack = new HexStack();
            foreach (var (color, count) in groups) stack.Push(color, count);
            return stack;
        }

        public static HexStack FromColors(IEnumerable<int> colorsBottomToTop)
        {
            var stack = new HexStack();
            foreach (int c in colorsBottomToTop) stack.Push(c);
            return stack;
        }

        /// <summary>Copies colors bottom to top into <paramref name="results"/> (cleared first).</summary>
        public void CopyTo(List<int> results)
        {
            results.Clear();
            results.AddRange(_tiles);
        }

        public override string ToString()
        {
            var sb = new StringBuilder(_tiles.Count * 2 + 2);
            sb.Append('[');
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(_tiles[i]);
            }
            return sb.Append(']').ToString();
        }
    }
}

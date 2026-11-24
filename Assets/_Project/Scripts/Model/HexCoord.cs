using System;

namespace HexaSort.Model
{
    /// <summary>
    /// Axial hex coordinate (q, r) on a flat-top grid. The implicit third cube axis is s = -q - r.
    /// See https://www.redblobgames.com/grids/hexagons/ for the conventions used here.
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public const int DirectionCount = 6;

        // Order matters: neighbour iteration (and therefore merge tie-breaking) follows it.
        private static readonly HexCoord[] Directions =
        {
            new HexCoord(+1, 0), new HexCoord(+1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, +1), new HexCoord(0, +1),
        };

        public static readonly HexCoord Zero = new HexCoord(0, 0);

        public readonly int Q;
        public readonly int R;

        public int S => -Q - R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public static HexCoord Direction(int index) => Directions[index];

        /// <summary>Neighbour in direction <paramref name="index"/> (0..5).</summary>
        public HexCoord Neighbor(int index) => this + Directions[index];

        public bool IsAdjacentTo(HexCoord other) => Distance(this, other) == 1;

        public static int Distance(HexCoord a, HexCoord b)
        {
            int dq = Math.Abs(a.Q - b.Q);
            int dr = Math.Abs(a.R - b.R);
            int ds = Math.Abs(a.S - b.S);
            return Math.Max(dq, Math.Max(dr, ds));
        }

        public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.Q + b.Q, a.R + b.R);
        public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.Q - b.Q, a.R - b.R);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Q == b.Q && a.R == b.R;
        public static bool operator !=(HexCoord a, HexCoord b) => !(a == b);

        public bool Equals(HexCoord other) => this == other;
        public override bool Equals(object obj) => obj is HexCoord other && this == other;
        public override int GetHashCode() => unchecked((Q * 73856093) ^ (R * 19349663));
        public override string ToString() => $"({Q},{R})";
    }
}

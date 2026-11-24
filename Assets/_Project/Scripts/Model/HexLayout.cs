using System;

namespace HexaSort.Model
{
    /// <summary>
    /// Converts between axial coordinates and a 2D plane for a flat-top layout.
    /// <c>size</c> is the hex circumradius (centre to corner). The view maps planar (x, y) to world (x, z).
    /// </summary>
    public static class HexLayout
    {
        public const float Sqrt3 = 1.7320508f;

        public static void ToPlanar(HexCoord coord, float size, out float x, out float y)
        {
            x = size * 1.5f * coord.Q;
            y = size * Sqrt3 * (coord.R + coord.Q * 0.5f);
        }

        public static HexCoord FromPlanar(float x, float y, float size)
        {
            float q = (2f / 3f * x) / size;
            float r = (-1f / 3f * x + Sqrt3 / 3f * y) / size;
            return Round(q, r);
        }

        /// <summary>Rounds fractional axial coordinates to the containing hex (cube rounding).</summary>
        public static HexCoord Round(float q, float r)
        {
            float s = -q - r;
            int rq = (int)Math.Round(q);
            int rr = (int)Math.Round(r);
            int rs = (int)Math.Round(s);

            float dq = Math.Abs(rq - q);
            float dr = Math.Abs(rr - r);
            float ds = Math.Abs(rs - s);

            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;

            return new HexCoord(rq, rr);
        }
    }
}

using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>Coordinate sets for common board outlines.</summary>
    public static class BoardShapes
    {
        /// <summary>All cells within <paramref name="radius"/> of the origin (radius 2 = 19 cells).</summary>
        public static List<HexCoord> Hexagon(int radius)
        {
            var coords = new List<HexCoord>();
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = System.Math.Max(-radius, -q - radius);
                int rMax = System.Math.Min(radius, -q + radius);
                for (int r = rMin; r <= rMax; r++) coords.Add(new HexCoord(q, r));
            }
            return coords;
        }

        /// <summary>
        /// Rectangle of <paramref name="columns"/> x <paramref name="rows"/> in flat-top "odd-q" offset
        /// layout, centred on the origin. Columns stagger by half a cell, like the original game's boards.
        /// </summary>
        public static List<HexCoord> Rectangle(int columns, int rows)
        {
            var coords = new List<HexCoord>();
            for (int c = 0; c < columns; c++)
            for (int row = 0; row < rows; row++)
                coords.Add(Offset(c, row, columns, rows));
            return coords;
        }

        /// <summary>
        /// Axial coordinate of grid position (<paramref name="column"/>, <paramref name="row"/>), both 0-based,
        /// inside the centred <see cref="Rectangle"/> of the given size. Row 0 is at the low-r edge.
        /// </summary>
        public static HexCoord Offset(int column, int row, int columns, int rows)
        {
            int q = -(columns - 1) / 2 + column;
            int offsetRow = -(rows - 1) / 2 + row;
            int r = offsetRow - (q - (q & 1)) / 2;
            return new HexCoord(q, r);
        }

        /// <summary>Hexagon of <paramref name="radius"/> without the cells within <paramref name="holeRadius"/> of the centre.</summary>
        public static List<HexCoord> Ring(int radius, int holeRadius)
        {
            var coords = Hexagon(radius);
            coords.RemoveAll(c => HexCoord.Distance(c, HexCoord.Zero) <= holeRadius);
            return coords;
        }

        /// <summary>Rhombus of <paramref name="width"/> x <paramref name="height"/> cells along the q and r axes, centred.</summary>
        public static List<HexCoord> Parallelogram(int width, int height)
        {
            var coords = new List<HexCoord>();
            int q0 = -(width - 1) / 2, r0 = -(height - 1) / 2;
            for (int q = 0; q < width; q++)
            for (int r = 0; r < height; r++)
                coords.Add(new HexCoord(q0 + q, r0 + r));
            return coords;
        }
    }
}

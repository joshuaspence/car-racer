using System;

namespace CarRacer.Core.Art
{
    /// <summary>A rectangle of a texture in UV space: u across from the left, v up from the bottom.</summary>
    public struct UvRect
    {
        public float UMin;
        public float VMin;
        public float UMax;
        public float VMax;

        public UvRect(float uMin, float vMin, float uMax, float vMax)
        {
            UMin = uMin;
            VMin = vMin;
            UMax = uMax;
            VMax = vMax;
        }

        /// <summary>A block of whole cells of a texture divided into <paramref name="cells"/> by <paramref name="cells"/>, counted from the top-left as an image viewer shows it.</summary>
        public static UvRect Cells(int cells, int column, int topRow, int columns, int rows) =>
            new UvRect((float)column / cells, 1f - (float)(topRow + rows) / cells, (float)(column + columns) / cells, 1f - (float)topRow / cells);
    }

    /// <summary>
    /// Paints a car in a player's colour (design doc §13): Kenney's cars share one palette texture,
    /// so the strip of it a car's bodywork uses is recoloured, keeping the strip's light-to-dark
    /// shading.
    /// </summary>
    public static class PaintRecolour
    {
        /// <summary>
        /// Recolours <paramref name="area"/> of <paramref name="rgba"/> (4 bytes a pixel, rows from the
        /// bottom up as Unity stores them) to the colour (<paramref name="r"/>, <paramref name="g"/>,
        /// <paramref name="b"/>), each pixel as light or dark relative to the area's average as it was.
        /// </summary>
        public static void Recolour(byte[] rgba, int width, int height, UvRect area, byte r, byte g, byte b)
        {
            int x0 = Math.Max(0, (int)Math.Round(area.UMin * width));
            int x1 = Math.Min(width, (int)Math.Round(area.UMax * width));
            int y0 = Math.Max(0, (int)Math.Round(area.VMin * height));
            int y1 = Math.Min(height, (int)Math.Round(area.VMax * height));
            if (x1 <= x0 || y1 <= y0)
            {
                return;
            }

            double total = 0;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    total += Luma(rgba, (y * width + x) * 4);
                }
            }

            double mean = total / ((x1 - x0) * (y1 - y0));
            if (mean <= 0)
            {
                mean = 1;
            }

            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int i = (y * width + x) * 4;
                    double shade = Luma(rgba, i) / mean;
                    rgba[i] = Scale(r, shade);
                    rgba[i + 1] = Scale(g, shade);
                    rgba[i + 2] = Scale(b, shade);
                }
            }
        }

        private static double Luma(byte[] rgba, int i) => 0.299 * rgba[i] + 0.587 * rgba[i + 1] + 0.114 * rgba[i + 2];

        private static byte Scale(byte channel, double shade) => (byte)Math.Max(0, Math.Min(255, Math.Round(channel * shade)));
    }
}

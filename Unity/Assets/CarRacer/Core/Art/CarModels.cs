namespace CarRacer.Core.Art
{
    /// <summary>A Kenney Car Kit body (design doc §13): its file, and the strip of the shared palette its paint uses.</summary>
    public sealed class CarModel
    {
        public CarModel(string name, UvRect paint, float scale)
        {
            Name = name;
            Paint = paint;
            Scale = scale;
        }

        /// <summary>The model's file in <c>Resources/Models</c>, without <c>.obj.bytes</c>.</summary>
        public string Name { get; }

        public UvRect Paint { get; }

        /// <summary>The model is drawn this much smaller or larger, to match the physics car's size.</summary>
        public float Scale { get; }
    }

    public static class CarModels
    {
        /// <summary>The shared palette texture, in <c>Resources/Models</c> without <c>.png.bytes</c>.</summary>
        public const string Palette = "colormap";

        /// <summary>Kenney's palette is a 16 by 16 grid of colour cells.</summary>
        public const int PaletteCells = 16;

        /// <summary>The race car: 1.3 m wide and 2.56 m long, drawn at 0.95 to match the 2.3 m placeholder; orange-red paint.</summary>
        public static readonly CarModel Race = new CarModel("race", UvRect.Cells(PaletteCells, 13, 4, 1, 4), 0.95f);
    }
}

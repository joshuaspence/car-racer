using CarRacer.Core.Driving;

namespace CarRacer.Core.Art
{
    /// <summary>
    /// A Kenney Car Kit body (design doc §13): its file, the strip of the shared palette its paint
    /// uses, and how it handles (design doc §4).
    /// </summary>
    public sealed class CarModel
    {
        public CarModel(string name, UvRect paint, float scale, CarStats stats)
        {
            Name = name;
            Paint = paint;
            Scale = scale;
            Stats = stats;
        }

        /// <summary>The model's file in <c>Resources/Models</c>, without <c>.obj.bytes</c>.</summary>
        public string Name { get; }

        public UvRect Paint { get; }

        /// <summary>The model is drawn this much smaller or larger, to match the physics car's size.</summary>
        public float Scale { get; }

        /// <summary>Its top speed, steering and acceleration ratings.</summary>
        public CarStats Stats { get; }
    }

    public static class CarModels
    {
        /// <summary>The shared palette texture, in <c>Resources/Models</c> without <c>.png.bytes</c>.</summary>
        public const string Palette = "colormap";

        /// <summary>Kenney's palette is a 16 by 16 grid of colour cells.</summary>
        public const int PaletteCells = 16;

        /// <summary>Every body is drawn this long, whatever its own length, so they all fill the same physics car.</summary>
        public const float Length = 2.43f;

        /// <summary>The race car: 1.3 m wide and 2.56 m long; orange-red paint. Quick, but slow off the line.</summary>
        public static readonly CarModel Race = Body("race", 13, 2.56f, new CarStats(topSpeed: 4, steering: 3, acceleration: 2));

        /// <summary>
        /// The garage's bodies (design doc §11), in the order <see cref="Session.LobbyRoster.BodyCount"/>
        /// indexes them: each with the palette column its paint uses (rows 4–7 hold the paint
        /// gradients), its own length, which sets its scale, and its handling ratings (each set adds
        /// up to <see cref="CarStats.Total"/>).
        /// </summary>
        public static readonly CarModel[] Bodies =
        {
            Race,
            Body("race-future", 15, 2.66f, new CarStats(topSpeed: 5, steering: 2, acceleration: 2)),
            Body("sedan-sports", 13, 2.55f, CarStats.Balanced),
            Body("hatchback-sports", 7, 2.85f, new CarStats(topSpeed: 2, steering: 5, acceleration: 2)),
            Body("suv-luxury", 9, 2.85f, new CarStats(topSpeed: 3, steering: 2, acceleration: 4)),
            Body("van", 15, 2.75f, new CarStats(topSpeed: 2, steering: 3, acceleration: 4)),
        };

        /// <summary>A name for the garage, e.g. "Race future".</summary>
        public static string DisplayName(CarModel model)
        {
            string spaced = model.Name.Replace('-', ' ');
            return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        /// <summary>The body at <paramref name="index"/>, wrapping round so a stray index still gives a car.</summary>
        public static CarModel ForIndex(int index) => Bodies[((index % Bodies.Length) + Bodies.Length) % Bodies.Length];

        private static CarModel Body(string name, int paintColumn, float length, CarStats stats) =>
            new CarModel(name, UvRect.Cells(PaletteCells, paintColumn, 4, 1, 4), Length / length, stats);
    }
}

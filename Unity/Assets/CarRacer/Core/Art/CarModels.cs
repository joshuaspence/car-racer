using CarRacer.Core.Driving;

namespace CarRacer.Core.Art
{
    /// <summary>
    /// A Kenney Car Kit body (design doc §13): its file, the strip of the shared palette its paint
    /// uses, and how it handles (design doc §4).
    /// </summary>
    public sealed class CarModel
    {
        public CarModel(string name, UvRect paint, float scale, CarStats stats, string displayName = null)
        {
            Name = name;
            Paint = paint;
            Scale = scale;
            Stats = stats;
            Title = displayName;
        }

        /// <summary>The model's file in <c>Resources/Models</c>, without <c>.obj.bytes</c>.</summary>
        public string Name { get; }

        /// <summary>What the garage calls it, when that isn't just its file name spaced out.</summary>
        public string Title { get; }

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
        /// up to <see cref="CarStats.Total"/>). New bodies go on the end, so the indices phones
        /// remember and send stay the same.
        /// </summary>
        public static readonly CarModel[] Bodies =
        {
            Race,
            Body("race-future", 15, 2.66f, new CarStats(topSpeed: 5, steering: 2, acceleration: 2)),
            Body("sedan-sports", 13, 2.55f, CarStats.Balanced),
            Body("hatchback-sports", 7, 2.85f, new CarStats(topSpeed: 2, steering: 5, acceleration: 2)),
            Body("suv-luxury", 9, 2.85f, new CarStats(topSpeed: 3, steering: 2, acceleration: 4), title: "SUV luxury"),
            Body("van", 15, 2.75f, new CarStats(topSpeed: 2, steering: 3, acceleration: 4)),
            Body("sedan", 13, 2.55f, new CarStats(topSpeed: 3, steering: 4, acceleration: 2)),
            Body("suv", 7, 2.7f, new CarStats(topSpeed: 2, steering: 2, acceleration: 5), title: "SUV"),
            Body("taxi", 9, 2.75f, new CarStats(topSpeed: 4, steering: 2, acceleration: 3)),
            Body("truck", 7, 2.95f, new CarStats(topSpeed: 4, steering: 1, acceleration: 4), title: "Pickup"),
            Body("delivery", 7, 3.25f, new CarStats(topSpeed: 1, steering: 3, acceleration: 5), title: "Delivery truck"),

            // The kart's bodywork is the blue-grey strip below the paint rows; its driver comes with it.
            Body("kart-oopi", 11, 1.43f, new CarStats(topSpeed: 2, steering: 4, acceleration: 3), paintRow: 8, title: "Kart"),
        };

        /// <summary>A name for the garage, e.g. "Race future".</summary>
        public static string DisplayName(CarModel model)
        {
            if (model.Title != null)
            {
                return model.Title;
            }

            string spaced = model.Name.Replace('-', ' ');
            return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        /// <summary>The body at <paramref name="index"/>, wrapping round so a stray index still gives a car.</summary>
        public static CarModel ForIndex(int index) => Bodies[((index % Bodies.Length) + Bodies.Length) % Bodies.Length];

        /// <param name="paintRow">The top row of the four-row strip the bodywork uses; the cars' paint is rows 4–7.</param>
        private static CarModel Body(string name, int paintColumn, float length, CarStats stats, int paintRow = 4, string title = null) =>
            new CarModel(name, UvRect.Cells(PaletteCells, paintColumn, paintRow, 1, 4), Length / length, stats, title);
    }
}

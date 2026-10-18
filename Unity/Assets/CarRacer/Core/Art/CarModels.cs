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

        /// <summary>Every body is drawn this long, whatever its own length, so they all fill the same physics car.</summary>
        public const float Length = 2.43f;

        /// <summary>The race car: 1.3 m wide and 2.56 m long; orange-red paint.</summary>
        public static readonly CarModel Race = Body("race", 13, 2.56f);

        /// <summary>
        /// The garage's bodies (design doc §11), in the order <see cref="Session.LobbyRoster.BodyCount"/>
        /// indexes them: each with the palette column its paint uses (rows 4–7 hold the paint
        /// gradients) and its own length, which sets its scale.
        /// </summary>
        public static readonly CarModel[] Bodies =
        {
            Race,
            Body("race-future", 15, 2.66f),
            Body("sedan-sports", 13, 2.55f),
            Body("hatchback-sports", 7, 2.85f),
            Body("suv-luxury", 9, 2.85f),
            Body("van", 15, 2.75f),
        };

        /// <summary>A name for the garage, e.g. "Race future".</summary>
        public static string DisplayName(CarModel model)
        {
            string spaced = model.Name.Replace('-', ' ');
            return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        /// <summary>The body at <paramref name="index"/>, wrapping round so a stray index still gives a car.</summary>
        public static CarModel ForIndex(int index) => Bodies[((index % Bodies.Length) + Bodies.Length) % Bodies.Length];

        private static CarModel Body(string name, int paintColumn, float length) =>
            new CarModel(name, UvRect.Cells(PaletteCells, paintColumn, 4, 1, 4), Length / length);
    }
}

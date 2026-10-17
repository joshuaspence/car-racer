namespace CarRacer.Core.Track
{
    /// <summary>The built-in tracks (design doc §13): an easy oval, a technical track with a jump, and a figure-eight.</summary>
    public static class SampleTracks
    {
        /// <summary>Every built-in track, in the order the lobby offers them.</summary>
        public static TrackDefinition[] All() => new[] { Oval(), Canyon(), FigureEight() };

        /// <summary>A gentle rounded-rectangle circuit (about 386 m), driven clockwise when viewed from above.</summary>
        public static TrackDefinition Oval() => new TrackDefinition
        {
            Id = "oval",
            DisplayName = "Pebble Oval",
            CheckpointCount = 8,
            DefaultLaps = 3,
            WallHeight = 1.2f,
            ItemBoxRows = new[] { 0.3f, 0.75f },
            Points = new[]
            {
                new TrackPoint(0f, 0f, 0f),
                new TrackPoint(0f, 0f, 60f),
                new TrackPoint(15f, 0f, 90f, 16f, 8f),
                new TrackPoint(45f, 0f, 100f, 16f, 8f),
                new TrackPoint(75f, 0f, 90f, 16f, 8f),
                new TrackPoint(90f, 0f, 60f),
                new TrackPoint(90f, 0f, 0f),
                new TrackPoint(75f, 0f, -30f, 16f, 8f),
                new TrackPoint(45f, 0f, -40f, 16f, 8f),
                new TrackPoint(15f, 0f, -30f, 16f, 8f),
            },
        };

        /// <summary>
        /// A technical circuit (about 590 m): a jump over a crest on the opening straight, a tight
        /// right-hand hairpin, a chicane, a fast sweeper and a second hairpin back to the line.
        /// </summary>
        public static TrackDefinition Canyon() => new TrackDefinition
        {
            Id = "canyon",
            DisplayName = "Canyon Hairpins",
            CheckpointCount = 10,
            DefaultLaps = 3,
            WallHeight = 1.2f,
            ItemBoxRows = new[] { 0.35f, 0.7f },
            Points = new[]
            {
                new TrackPoint(0f, 0f, 0f),
                new TrackPoint(0f, 0f, 40f),
                new TrackPoint(0f, 2.6f, 62f, 12f),
                new TrackPoint(0f, 0f, 82f, 12f),
                new TrackPoint(0f, 0f, 106f),
                new TrackPoint(15f, 0f, 130f, 14f, 6f),
                new TrackPoint(45f, 0f, 138f, 14f, 8f),
                new TrackPoint(68f, 0f, 118f, 14f, 8f),
                new TrackPoint(70f, 0f, 90f),
                new TrackPoint(88f, 0f, 66f, 12f),
                new TrackPoint(90f, 0f, 40f, 12f),
                new TrackPoint(110f, 0f, 20f),
                new TrackPoint(140f, 0f, 0f, 16f, 6f),
                new TrackPoint(150f, 0f, -35f, 16f, 6f),
                new TrackPoint(130f, 0f, -65f, 14f, 8f),
                new TrackPoint(95f, 0f, -65f, 14f, 8f),
                new TrackPoint(60f, 0f, -45f),
                new TrackPoint(30f, 0f, -48f, 14f, 6f),
                new TrackPoint(6f, 0f, -32f, 14f, 6f),
            },
        };

        /// <summary>
        /// A figure-eight (about 700 m) with a crossover: the line crosses the middle on the ground,
        /// then the road climbs round the right-hand loop and crosses back over on a bridge 8 m up.
        /// </summary>
        public static TrackDefinition FigureEight() => new TrackDefinition
        {
            Id = "figure-eight",
            DisplayName = "Crossover Eight",
            CheckpointCount = 10,
            DefaultLaps = 3,
            WallHeight = 1.2f,
            ItemBoxRows = new[] { 0.2f, 0.7f },
            Points = new[]
            {
                new TrackPoint(-35f, 0f, -35f),
                new TrackPoint(35f, 0f, 35f),
                new TrackPoint(75f, 0f, 55f, 16f, 6f),
                new TrackPoint(120f, 2f, 20f, 16f, 6f),
                new TrackPoint(115f, 5f, -30f, 16f, 6f),
                new TrackPoint(75f, 7.5f, -55f, 16f, 6f),
                new TrackPoint(35f, 8f, -35f),
                new TrackPoint(-35f, 8f, 35f),
                new TrackPoint(-75f, 7.5f, 55f, 16f, -6f),
                new TrackPoint(-115f, 5f, 30f, 16f, -6f),
                new TrackPoint(-120f, 2f, -20f, 16f, -6f),
                new TrackPoint(-75f, 0f, -55f, 16f, -6f),
            },
        };
    }
}

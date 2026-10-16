namespace CarRacer.Core.Track
{
    /// <summary>Built-in test track used by tests and as the first M1 track until track files exist.</summary>
    public static class SampleTracks
    {
        /// <summary>A gentle rounded-rectangle circuit (about 470 m), driven clockwise when viewed from above.</summary>
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
    }
}

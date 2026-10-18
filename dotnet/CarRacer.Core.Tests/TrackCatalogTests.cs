using System;
using System.Collections.Generic;
using System.Linq;
using CarRacer.Core.Bots;
using CarRacer.Core.Driving;
using CarRacer.Core.Numerics;
using CarRacer.Core.Race;
using CarRacer.Core.Track;
using Xunit;

namespace CarRacer.Core.Tests
{
    /// <summary>Checks every built-in track is drivable: clear of itself, gently graded, not too tight, and lappable by bots.</summary>
    public class TrackCatalogTests
    {
        private const float Dt = 0.02f;

        /// <summary>Roads that pass over each other need at least this much vertical clearance.</summary>
        private const float Clearance = 6f;

        public static IEnumerable<object[]> Tracks => SampleTracks.All().Select(t => new object[] { t.Id });

        private static TrackLayout Layout(string id) => new TrackLayout(SampleTracks.All().Single(t => t.Id == id));

        [Theory]
        [InlineData("oval", 386f)]
        [InlineData("canyon", 590f)]
        [InlineData("figure-eight", 700f)]
        [InlineData("sweepers", 500f)]
        [InlineData("switchbacks", 480f)]
        public void EachTrackIsAboutAsLongAsItsDescriptionSays(string id, float metres)
        {
            // The lengths quoted in SampleTracks and the design doc, to within 10 m.
            Assert.InRange(Layout(id).Spline.Length, metres - 10f, metres + 10f);
        }

        [Fact]
        public void EveryTrackHasADistinctIdAndName()
        {
            var all = SampleTracks.All();
            Assert.Equal(all.Length, all.Select(t => t.Id).Distinct().Count());
            Assert.Equal(all.Length, all.Select(t => t.DisplayName).Distinct().Count());
            Assert.All(all, t => Assert.Empty(t.Validate()));
        }

        [Theory]
        [MemberData(nameof(Tracks))]
        public void TheRoadNeverRunsIntoItself(string id)
        {
            var spline = Layout(id).Spline;
            var samples = spline.SampleEvenly(2f);
            for (int i = 0; i < samples.Count; i++)
            {
                for (int j = i + 1; j < samples.Count; j++)
                {
                    float along = Math.Abs(samples[i].Distance - samples[j].Distance);
                    along = Math.Min(along, spline.Length - along);
                    if (along < 40f)
                    {
                        continue;
                    }

                    float apart = (samples[i].Position - samples[j].Position).Flattened.Length;
                    float needed = (samples[i].Width + samples[j].Width) / 2f + 4f;
                    float above = Math.Abs(samples[i].Position.Y - samples[j].Position.Y);
                    Assert.True(apart >= needed || above >= Clearance,
                        $"{id}: {samples[i].Distance:0} m and {samples[j].Distance:0} m are {apart:0.0} m apart and {above:0.0} m above each other");
                }
            }
        }

        [Theory]
        [MemberData(nameof(Tracks))]
        public void SlopesAreDrivable(string id)
        {
            var samples = Layout(id).Spline.SampleEvenly(2f);
            for (int i = 0; i < samples.Count; i++)
            {
                var a = samples[i];
                var b = samples[(i + 1) % samples.Count];
                float grade = Math.Abs(b.Position.Y - a.Position.Y) / (b.Position - a.Position).Flattened.Length;
                Assert.True(grade <= 0.2f, $"{id}: {grade:P0} slope at {a.Distance:0} m");
            }
        }

        [Theory]
        [MemberData(nameof(Tracks))]
        public void NoBendIsTooTight(string id)
        {
            var spline = Layout(id).Spline;
            foreach (var sample in spline.SampleEvenly(2f))
            {
                var before = spline.SampleAtDistance(sample.Distance - 3f).Position.Flattened;
                var at = sample.Position.Flattened;
                var after = spline.SampleAtDistance(sample.Distance + 3f).Position.Flattened;
                float radius = CircumRadius(before, at, after);
                Assert.True(radius >= 11f, $"{id}: {radius:0.0} m radius at {sample.Distance:0} m");
            }
        }

        [Theory]
        [MemberData(nameof(Tracks))]
        public void BotsLapEveryTrackWithoutLeavingTheRoad(string id)
        {
            foreach (var difficulty in new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard })
            {
                var (seconds, margin) = DriveALap(Layout(id), difficulty);
                Assert.True(seconds < 90f, $"{id} {difficulty}: lap took {seconds} s");
                Assert.True(margin > 0f, $"{id} {difficulty}: went {-margin:0.0} m off the road");
            }
        }

        [Fact]
        public void TheCanyonHasACrestACarJumpsOff()
        {
            // At 20 m/s a car leaves the road where the crest curves down faster than gravity can pull it.
            var spline = Layout("canyon").Spline;
            const float speed = 20f;
            bool airborne = false;
            for (float d = 0f; d < spline.Length; d += 1f)
            {
                float y0 = spline.SampleAtDistance(d - 4f).Position.Y;
                float y1 = spline.SampleAtDistance(d).Position.Y;
                float y2 = spline.SampleAtDistance(d + 4f).Position.Y;
                float curvature = -(y0 - 2f * y1 + y2) / 16f;
                airborne |= speed * speed * curvature > 9.81f;
            }

            Assert.True(airborne);
        }

        [Fact]
        public void TheFigureEightCrossesOverOnABridge()
        {
            var samples = Layout("figure-eight").Spline.SampleEvenly(1f);
            bool crossover = samples.Any(a => samples.Any(b =>
                (a.Position - b.Position).Flattened.Length < 2f && b.Position.Y - a.Position.Y >= 7f));
            Assert.True(crossover);
        }

        [Fact]
        public void AtTheCrossoverTheNearestRoadIsTheOneTheCarIsOn()
        {
            var spline = Layout("figure-eight").Spline;
            float ground = spline.FindNearestDistance(new Vec3(0f, 0.5f, 0f));
            float bridge = spline.FindNearestDistance(new Vec3(0f, 8.5f, 0f));
            Assert.InRange(spline.SampleAtDistance(ground).Position.Y, -0.5f, 1f);
            Assert.InRange(spline.SampleAtDistance(bridge).Position.Y, 7f, 9f);
        }

        [Fact]
        public void ACarOnTheBridgeDoesNotCrossAGateOnTheRoadBelow()
        {
            // A gate on the ground road right at the crossover, crossed sideways from 8 m up.
            var spline = Layout("figure-eight").Spline;
            float d = spline.FindNearestDistance(new Vec3(0f, 0f, 0f));
            var gate = new CheckpointGate { Index = 0, Sample = spline.SampleAtDistance(d) };
            var detector = new CheckpointDetector(new[] { gate });
            var from = gate.Sample.Position - gate.Sample.Forward * 1f + Vec3.Up * 8.5f;
            var to = gate.Sample.Position + gate.Sample.Forward * 1f + Vec3.Up * 8.5f;
            Assert.Equal(-1, detector.Update(from, to));
            Assert.Equal(0, detector.Update(from - Vec3.Up * 8f, to - Vec3.Up * 8f));
        }

        /// <summary>Drives a bot once round with the real car model (no walls), following the road's height, and reports the lap time and closest approach to the edge.</summary>
        private static (float Seconds, float Margin) DriveALap(TrackLayout track, BotDifficulty difficulty)
        {
            var spline = track.Spline;
            var tuning = new CarTuning();
            var driver = new BotDriver(spline, BotSkill.For(difficulty), tuning, 1);
            var start = spline.SampleAtDistance(0f);
            var state = new CarState { Heading = MathF.Atan2(start.Forward.X, start.Forward.Z) };
            var position = start.Position;
            float travelled = 0f;
            float last = 0f;
            float margin = float.PositiveInfinity;
            for (float t = 0f; t < 120f; t += Dt)
            {
                state = CarModel.Step(state, driver.Drive(state, position, Dt, null), grounded: true, Dt, tuning);
                position += state.Velocity * Dt;

                float distance = driver.TrackDistance;
                var sample = spline.SampleAtDistance(distance);
                position = new Vec3(position.X, sample.Position.Y, position.Z);
                float step = distance - last;
                if (step < -spline.Length / 2f)
                {
                    step += spline.Length;
                }

                travelled += step;
                last = distance;
                float lateral = Math.Abs(Vec3.Dot((position - sample.Position).Flattened, sample.Right.Flattened.Normalized));
                margin = Math.Min(margin, sample.Width / 2f - lateral);
                if (travelled >= spline.Length)
                {
                    return (t, margin);
                }
            }

            return (float.PositiveInfinity, margin);
        }

        private static float CircumRadius(Vec3 a, Vec3 b, Vec3 c)
        {
            float ab = (b - a).Length;
            float bc = (c - b).Length;
            float ca = (a - c).Length;
            float cross = Vec3.Cross(b - a, c - a).Length;
            return cross < 1e-6f ? float.PositiveInfinity : ab * bc * ca / (2f * cross);
        }
    }
}

using System;
using System.Linq;
using CarRacer.Core.Driving;
using CarRacer.Core.Numerics;
using CarRacer.Core.Race;
using CarRacer.Core.Track;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class DriftChargeTests
    {
        private static readonly CarTuning Tuning = new CarTuning();

        [Fact]
        public void NoDriftNoCharge()
        {
            Assert.Equal(0f, new CarState { DriftSeconds = 5f }.DriftCharge(Tuning));
        }

        [Fact]
        public void ChargeRisesToFullAtTheTopTier()
        {
            float top = Tuning.DriftTierSeconds.Last();
            Assert.Equal(0.5f, new CarState { Drifting = true, DriftSeconds = top / 2f }.DriftCharge(Tuning), 4);
            Assert.Equal(1f, new CarState { Drifting = true, DriftSeconds = top }.DriftCharge(Tuning));
            Assert.Equal(1f, new CarState { Drifting = true, DriftSeconds = top * 3f }.DriftCharge(Tuning));
        }
    }

    public class TrackProgressTests
    {
        private static readonly TrackLayout Oval = new TrackLayout(SampleTracks.Oval());

        private static (Vec3 Position, float Heading) Along(float distance, bool backwards = false)
        {
            var s = Oval.Spline.SampleAtDistance(distance);
            float heading = MathF.Atan2(s.Forward.X, s.Forward.Z) + (backwards ? MathF.PI : 0f);
            return (s.Position, heading);
        }

        [Fact]
        public void DrivingTheRightWayIsAligned()
        {
            var progress = new TrackProgress(Oval.Spline);
            var (p, h) = Along(30f);
            progress.Update(p, h, 20f, 0.02f);
            Assert.Equal(30f, progress.Distance, 0);
            Assert.InRange(progress.Alignment, 0.99f, 1.01f);
            Assert.False(progress.IsWrongWay);
        }

        [Fact]
        public void FollowsTheCarAroundTheLap()
        {
            var progress = new TrackProgress(Oval.Spline);
            for (float d = 0f; d < Oval.Spline.Length; d += 2f)
            {
                var (p, h) = Along(d);
                progress.Update(p, h, 20f, 0.02f);
                float gap = Oval.Spline.WrapDistance(progress.Distance - d);
                Assert.True(Math.Min(gap, Oval.Spline.Length - gap) < 1f, $"at {d}: {progress.Distance}");
            }
        }

        [Fact]
        public void DrivingBackwardsForASecondShowsWrongWay()
        {
            var progress = new TrackProgress(Oval.Spline);
            float d = 40f;
            for (int i = 0; i < 40; i++) // 0.8 s
            {
                var (p, h) = Along(d, backwards: true);
                progress.Update(p, h, 10f, 0.02f);
                d -= 0.2f;
            }

            Assert.True(progress.Alignment < -0.9f);
            Assert.False(progress.IsWrongWay);

            for (int i = 0; i < 15; i++) // past 1 s
            {
                var (p, h) = Along(d, backwards: true);
                progress.Update(p, h, 10f, 0.02f);
                d -= 0.2f;
            }

            Assert.True(progress.IsWrongWay);
        }

        [Fact]
        public void ARespawnFarAwayIsFoundAgain()
        {
            var progress = new TrackProgress(Oval.Spline);
            var (p, h) = Along(30f);
            progress.Update(p, h, 20f, 0.02f);

            (p, h) = Along(200f); // far beyond the search window
            progress.Update(p, h, 20f, 0.02f);
            Assert.Equal(200f, progress.Distance, 0);
            Assert.InRange(progress.Alignment, 0.99f, 1.01f);
        }

        [Fact]
        public void ResetClearsTheWarning()
        {
            var progress = new TrackProgress(Oval.Spline);
            var (p, h) = Along(40f, backwards: true);
            for (int i = 0; i < 100; i++)
            {
                progress.Update(p, h, 10f, 0.02f);
            }

            Assert.True(progress.IsWrongWay);
            progress.Reset();
            Assert.False(progress.IsWrongWay);
            Assert.Equal(1f, progress.Alignment);
        }
    }

    public class MiniMapTests
    {
        private static readonly TrackLayout Oval = new TrackLayout(SampleTracks.Oval());

        [Fact]
        public void TheOutlineFitsInsideTheMarginsAndTouchesTheLongerSide()
        {
            var map = new MiniMap(Oval.Spline);
            float minX = map.Outline.Min(p => p.X), maxX = map.Outline.Max(p => p.X);
            float minY = map.Outline.Min(p => p.Y), maxY = map.Outline.Max(p => p.Y);
            Assert.True(minX >= MiniMap.Margin - 1e-4f && maxX <= 1f - MiniMap.Margin + 1e-4f);
            Assert.True(minY >= MiniMap.Margin - 1e-4f && maxY <= 1f - MiniMap.Margin + 1e-4f);

            // The oval is taller (Z) than wide (X): it fills the height and is centred across.
            Assert.Equal(MiniMap.Margin, minY, 3);
            Assert.Equal(1f - MiniMap.Margin, maxY, 3);
            Assert.Equal(0.5f, (minX + maxX) / 2f, 3);
        }

        [Fact]
        public void TheScaleIsTheSameOnBothAxes()
        {
            var map = new MiniMap(Oval.Spline);
            Vec2 a = map.ToMap(new Vec3(0f, 0f, 0f));
            Vec2 right = map.ToMap(new Vec3(10f, 0f, 0f));
            Vec2 up = map.ToMap(new Vec3(0f, 0f, 10f));
            Assert.Equal(right.X - a.X, up.Y - a.Y, 5);
            Assert.Equal(a.Y, right.Y, 5);
            Assert.True(right.X > a.X && up.Y > a.Y, "east is right, north is up");
        }

        [Fact]
        public void HeightIsIgnored()
        {
            var map = new MiniMap(Oval.Spline);
            Assert.Equal(map.ToMap(new Vec3(20f, 0f, 20f)), map.ToMap(new Vec3(20f, 5f, 20f)));
        }
    }
}

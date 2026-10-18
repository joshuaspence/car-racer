using System.Collections.Generic;
using CarRacer.Core.Driving;
using CarRacer.Core.Numerics;
using Xunit;

namespace CarRacer.Core.Tests
{
    /// <summary>The accessibility options (design doc §5, §6): left-handed and large controls, and reduced motion.</summary>
    public class AccessibilityTests
    {
        [Fact]
        public void TheLeftHandedLayoutIsTheMirrorImage()
        {
            var right = new TouchControlLayout(2400f, 1080f, 100f, 0f, 2300f, 1080f, 400f);
            var left = new TouchControlLayout(2400f, 1080f, 100f, 0f, 2300f, 1080f, 400f, leftHanded: true);

            // Mirrored about the safe area's middle (x = 1200), so each button keeps its margin.
            foreach (var (r, l) in new[] { (right.DriftCentre, left.DriftCentre), (right.BrakeCentre, left.BrakeCentre), (right.GasCentre, left.GasCentre), (right.ItemCentre, left.ItemCentre), (right.LookBackCentre, left.LookBackCentre) })
            {
                Assert.Equal(2400f - r.X, l.X, 2);
                Assert.Equal(r.Y, l.Y, 2);
            }

            // Look back is on the steering side in both, as the right-handed layout has it.
            Assert.True(right.InSteerZone(right.LookBackCentre.X));
            Assert.True(left.InSteerZone(left.LookBackCentre.X));
            Assert.True(left.DriftCentre.X - left.ButtonRadius >= 100f);
            Assert.True(left.InSteerZone(1800f));
            Assert.False(left.InSteerZone(600f));
            Assert.True(right.InSteerZone(600f));
        }

        [Fact]
        public void LeftHandedTheRightThumbSteersAndTheLeftPresses()
        {
            var layout = new TouchControlLayout(1920f, 1080f, 0f, 0f, 1920f, 1080f, 400f, leftHanded: true);
            var controls = new TouchControls();
            controls.Update(new List<TouchPoint> { new TouchPoint(1, 1500f, 300f, TouchStage.Began) }, layout, showGas: false);
            var steering = controls.Update(new List<TouchPoint> { new TouchPoint(1, 1500f + layout.SteerRange, 300f, TouchStage.Held) }, layout, showGas: false);
            Assert.Equal(1f, steering.Steer, 3);

            var drift = layout.DriftCentre;
            var both = controls.Update(new List<TouchPoint>
            {
                new TouchPoint(1, 1500f + layout.SteerRange, 300f, TouchStage.Held),
                new TouchPoint(2, drift.X, drift.Y, TouchStage.Began),
            }, layout, showGas: false);
            Assert.True(both.Drift);
            Assert.Equal(1f, both.Steer, 3);

            // A touch on the empty left half doesn't steer.
            var fresh = new TouchControls();
            Assert.False(fresh.Update(new List<TouchPoint> { new TouchPoint(3, 900f, 700f, TouchStage.Began) }, layout, showGas: false).Drift);
            Assert.False(fresh.Steering);
        }

        [Theory]
        [InlineData(false, 1f)]
        [InlineData(true, 1f)]
        [InlineData(false, TouchControlLayout.LargeButtonScale)]
        [InlineData(true, TouchControlLayout.LargeButtonScale)]
        public void NoTwoButtonsOverlap(bool leftHanded, float scale)
        {
            foreach (var (width, height, dpi) in new[] { (1920f, 1080f, 400f), (2880f, 1440f, 538f), (2400f, 1080f, 420f) })
            {
                var layout = new TouchControlLayout(width, height, 0f, 0f, width, height, dpi, leftHanded, scale);
                var all = new[] { layout.DriftCentre, layout.BrakeCentre, layout.GasCentre, layout.ItemCentre, layout.LookBackCentre };
                for (int i = 0; i < all.Length; i++)
                {
                    for (int j = i + 1; j < all.Length; j++)
                    {
                        float dx = all[i].X - all[j].X;
                        float dy = all[i].Y - all[j].Y;
                        Assert.True(dx * dx + dy * dy >= 4f * layout.ButtonRadius * layout.ButtonRadius, $"buttons {i} and {j} overlap at {width}x{height}, left-handed {leftHanded}, scale {scale}");
                    }
                }
            }
        }

        [Fact]
        public void LargeControlsAreBiggerAsFarAsTheyFit()
        {
            // A tall screen has room for the full 25%.
            var tallStandard = new TouchControlLayout(1920f, 1440f, 0f, 0f, 1920f, 1440f, 400f);
            var tallLarge = new TouchControlLayout(1920f, 1440f, 0f, 0f, 1920f, 1440f, 400f, buttonScale: TouchControlLayout.LargeButtonScale);
            Assert.Equal(tallStandard.ButtonRadius * 1.25f, tallLarge.ButtonRadius, 2);

            // A phone held sideways (Pixel 2 XL: 1440 px at 538 dpi) has less, but still gets bigger buttons.
            var standard = new TouchControlLayout(2880f, 1440f, 0f, 0f, 2880f, 1440f, 538f);
            var large = new TouchControlLayout(2880f, 1440f, 0f, 0f, 2880f, 1440f, 538f, buttonScale: TouchControlLayout.LargeButtonScale);
            Assert.InRange(large.ButtonRadius, standard.ButtonRadius * 1.1f, standard.ButtonRadius * 1.25f);
            Assert.True(large.DriftCentre.X + large.ButtonRadius <= 2880f);
            Assert.True(large.ItemCentre.Y + large.ButtonRadius <= 1440f);
            Assert.True(large.BrakeCentre.X > large.SteerZoneRight);
            Assert.True(large.HitsButton(new Vec2(large.DriftCentre.X + standard.ButtonRadius * 1.3f, large.DriftCentre.Y), large.DriftCentre));
        }

        [Fact]
        public void ReducedMotionKeepsTheCameraStillAtSpeed()
        {
            var settings = new ChaseCameraSettings();
            var calm = settings.WithReducedMotion();
            Assert.NotSame(settings, calm);
            Assert.Equal(72f, settings.FieldOfViewAtTopSpeed);

            var state = ChaseCameraRig.Step(default, Vec3.Zero, 0f, 0f, 0.02f, calm);
            var fast = state;
            for (int i = 0; i < 200; i++)
            {
                fast = ChaseCameraRig.Step(fast, Vec3.Zero, 0f, calm.TopSpeed, 0.02f, calm);
            }

            Assert.Equal(state.FieldOfView, fast.FieldOfView, 3);
            Assert.True(fast.Position.ApproximatelyEquals(state.Position));
        }
    }
}

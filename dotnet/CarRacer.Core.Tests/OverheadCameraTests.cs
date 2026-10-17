using CarRacer.Core.Driving;
using CarRacer.Core.Numerics;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class OverheadCameraTests
    {
        private static readonly OverheadCameraSettings Settings = new OverheadCameraSettings();

        [Fact]
        public void TheFirstStepJumpsAboveAndBehindTheFocus()
        {
            var state = OverheadCameraRig.Step(default, new Vec3(10f, 2f, 0f), Vec3.Zero, 0.016f, Settings);
            var focus = new Vec3(10f * (1f - Settings.CentrePull), 2f, 0f);
            Assert.Equal(focus.X, state.LookAt.X, 3);
            Assert.Equal(focus.Y, state.LookAt.Y, 3);
            Assert.Equal(focus.X, state.Position.X, 3);
            Assert.Equal(2f + Settings.Height, state.Position.Y, 3);
            Assert.Equal(-Settings.Back, state.Position.Z, 3);
        }

        [Fact]
        public void ItCatchesUpSmoothly()
        {
            var state = OverheadCameraRig.Step(default, Vec3.Zero, Vec3.Zero, 0.016f, Settings);
            var moved = new Vec3(0f, 0f, 10f);
            state = OverheadCameraRig.Step(state, moved, Vec3.Zero, 0.016f, Settings);
            float wanted = 10f * (1f - Settings.CentrePull);
            Assert.InRange(state.LookAt.Z, 0.01f, wanted - 0.01f);
            for (int i = 0; i < 300; i++)
            {
                state = OverheadCameraRig.Step(state, moved, Vec3.Zero, 0.016f, Settings);
            }

            Assert.Equal(wanted, state.LookAt.Z, 2);
        }
    }
}

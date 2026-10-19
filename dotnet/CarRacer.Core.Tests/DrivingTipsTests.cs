using System.Linq;
using CarRacer.Core.Driving;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class DrivingTipsTests
    {
        [Fact]
        public void TheDefaultSetupSteersOnTheLeftAndAcceleratesByItself()
        {
            var tips = DrivingTips.For(leftHanded: false, autoAccelerate: true, tilt: false, lookBack: true);

            Assert.Contains("left half", tips[0]);
            Assert.Contains("accelerates by itself", tips[1]);
            Assert.Contains(tips, t => t.Contains("DRIFT"));
            Assert.Contains(tips, t => t.Contains("LOOK BACK"));
            Assert.DoesNotContain(tips, t => t.Contains("GAS"));
        }

        [Fact]
        public void LeftHandedControlsSwapTheSides()
        {
            var tips = DrivingTips.For(leftHanded: true, autoAccelerate: true, tilt: false, lookBack: true);

            Assert.Contains("right half", tips[0]);
            Assert.Contains("on the left", tips[1]);
        }

        [Fact]
        public void TiltManualGasAndNoLookBackChangeTheirTips()
        {
            var tips = DrivingTips.For(leftHanded: false, autoAccelerate: false, tilt: true, lookBack: false);

            Assert.Contains("steering wheel", tips[0]);
            Assert.Contains("GAS", tips[1]);
            Assert.DoesNotContain(tips, t => t.Contains("LOOK BACK"));
            Assert.Equal(tips.Count, tips.Distinct().Count());
        }
    }
}

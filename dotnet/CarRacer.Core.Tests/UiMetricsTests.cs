using CarRacer.Core.Platform;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class UiMetricsTests
    {
        [Fact]
        public void LayoutIsIn720PixelTallUnits()
        {
            Assert.Equal(2f, UiMetrics.Scale(1440f));
            Assert.Equal(1440f, UiMetrics.Width(2880f, 1440f));
            Assert.Equal(1f, UiMetrics.Scale(0f));
        }

        [Fact]
        public void ANotchOnTheLeftIsKeptClear()
        {
            // A 2880x1440 phone held with its notch on the left: the safe area starts 120 px in.
            var insets = UiMetrics.SafeInsets(2880f, 1440f, 120f, 0f, 2760f, 1440f);
            Assert.Equal(60f, insets.Left);
            Assert.Equal(0f, insets.Right);
            Assert.Equal(0f, insets.Top);
            Assert.Equal(0f, insets.Bottom);
        }

        [Fact]
        public void TopAndBottomAreMeasuredFromTheTop()
        {
            // Unity's safe area runs from the bottom-left; 40 px off the bottom, 80 px off the top.
            var insets = UiMetrics.SafeInsets(1920f, 1080f, 0f, 40f, 1920f, 960f);
            Assert.Equal(80f / 1.5f, insets.Top, 3);
            Assert.Equal(40f / 1.5f, insets.Bottom, 3);
        }

        [Fact]
        public void ASafeAreaBeyondTheScreenGivesNoNegativeInsets()
        {
            var insets = UiMetrics.SafeInsets(1920f, 1080f, -10f, -10f, 1940f, 1100f);
            Assert.Equal(0f, insets.Left);
            Assert.Equal(0f, insets.Top);
            Assert.Equal(0f, insets.Right);
            Assert.Equal(0f, insets.Bottom);
        }
    }
}

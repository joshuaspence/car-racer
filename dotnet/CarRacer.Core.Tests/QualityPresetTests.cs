using CarRacer.Core.Platform;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class QualityPresetTests
    {
        [Theory]
        [InlineData("Adreno (TM) 540", 4096, QualityLevel.Low)] // Pixel 2 XL, the baseline
        [InlineData("Adreno (TM) 618", 6144, QualityLevel.Medium)]
        [InlineData("Adreno (TM) 640", 8192, QualityLevel.High)]
        [InlineData("Adreno (TM) 740", 12288, QualityLevel.High)]
        [InlineData("Mali-G71 MP20", 4096, QualityLevel.Low)]
        [InlineData("Mali-G76 MP10", 8192, QualityLevel.Medium)]
        [InlineData("Mali-G78 MP20", 8192, QualityLevel.High)]
        [InlineData("Mali-G710 MC10", 12288, QualityLevel.High)]
        [InlineData("Mali-G310", 4096, QualityLevel.Low)] // held down by memory
        public void TheLevelFollowsTheGpuAndMemory(string gpu, int memoryMb, QualityLevel expected)
        {
            Assert.Equal(expected, QualityPresets.Pick(gpu, memoryMb));
        }

        [Fact]
        public void LittleMemoryHoldsAFastGpuDown()
        {
            Assert.Equal(QualityLevel.Low, QualityPresets.Pick("Adreno (TM) 740", 3072));
            Assert.Equal(QualityLevel.Medium, QualityPresets.Pick("Adreno (TM) 740", 6144));
        }

        [Fact]
        public void AnUnknownGpuGoesByMemory()
        {
            Assert.Equal(QualityLevel.High, QualityPresets.Pick("PowerVR Rogue GE8320", 8192));
            Assert.Equal(QualityLevel.Low, QualityPresets.Pick(null, 2048));
        }

        [Fact]
        public void LowerLevelsRenderFewerPixels()
        {
            var low = QualityPresets.For(QualityLevel.Low);
            var medium = QualityPresets.For(QualityLevel.Medium);
            var high = QualityPresets.For(QualityLevel.High);
            Assert.InRange(low.RenderScale, 0.65f, 0.75f);
            Assert.True(low.RenderScale < medium.RenderScale && medium.RenderScale < high.RenderScale);
            Assert.True(low.Msaa <= medium.Msaa && medium.Msaa <= high.Msaa);
        }
    }
}

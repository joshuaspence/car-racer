using CarRacer.Core.Race;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class RaceFormatTests
    {
        [Theory]
        [InlineData(1, "1st")]
        [InlineData(2, "2nd")]
        [InlineData(3, "3rd")]
        [InlineData(4, "4th")]
        [InlineData(11, "11th")]
        [InlineData(12, "12th")]
        [InlineData(13, "13th")]
        [InlineData(21, "21st")]
        [InlineData(22, "22nd")]
        [InlineData(111, "111th")]
        public void Ordinals(int n, string expected) => Assert.Equal(expected, RaceFormat.Ordinal(n));

        [Theory]
        [InlineData(0, "0:00.00")]
        [InlineData(83456, "1:23.45")]
        [InlineData(5009, "0:05.00")]
        [InlineData(600000, "10:00.00")]
        [InlineData(-5, "0:00.00")]
        public void Times(long ms, string expected) => Assert.Equal(expected, RaceFormat.Time(ms));
    }
}

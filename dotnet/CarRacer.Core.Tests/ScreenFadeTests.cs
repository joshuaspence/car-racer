using CarRacer.Core.Platform;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class ScreenFadeTests
    {
        [Fact]
        public void TheCoverHoldsThenLiftsSmoothlyToClear()
        {
            Assert.Equal(1f, ScreenFade.Alpha(0f));
            Assert.Equal(1f, ScreenFade.Alpha(ScreenFade.HoldSeconds));
            Assert.Equal(0.5f, ScreenFade.Alpha(ScreenFade.HoldSeconds + ScreenFade.FadeSeconds / 2f), 4);
            Assert.Equal(0f, ScreenFade.Alpha(ScreenFade.Total));
            Assert.Equal(0f, ScreenFade.Alpha(10f));

            float previous = 1f;
            for (float t = 0f; t <= ScreenFade.Total; t += 0.01f)
            {
                float alpha = ScreenFade.Alpha(t);
                Assert.True(alpha <= previous, $"the cover should only ever lift (at {t} s)");
                previous = alpha;
            }
        }

        [Fact]
        public void ASlowFrameMovesTheFadeOnByNoMoreThanAFastOne()
        {
            Assert.Equal(1f / 60f, ScreenFade.Advance(0f, 1f / 60f), 5);
            Assert.Equal(ScreenFade.MaxStep, ScreenFade.Advance(0f, 2.5f), 5);
            Assert.Equal(0f, ScreenFade.Advance(0f, -1f));
        }

        [Fact]
        public void AtSixtyFramesASecondTheFadeTakesItsFullLength()
        {
            float elapsed = 0f;
            int frames = 0;
            while (!ScreenFade.Done(elapsed))
            {
                elapsed = ScreenFade.Advance(elapsed, 1f / 60f);
                frames++;
            }

            Assert.InRange(frames, 20, 22);
        }
    }
}

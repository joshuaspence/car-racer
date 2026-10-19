using System;
using System.Linq;
using CarRacer.Core.Audio;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class UiToneTests
    {
        [Theory]
        [InlineData(UiSound.Tap)]
        [InlineData(UiSound.Back)]
        [InlineData(UiSound.Confirm)]
        public void EachSoundIsShortPeaksAtTheSetLevelAndStartsAndEndsSilent(UiSound sound)
        {
            float[] samples = UiTone.Make(sound);

            Assert.InRange(samples.Length, UiTone.SampleRate / 50, UiTone.SampleRate / 5);
            Assert.Equal(UiTone.Peak, samples.Max(Math.Abs), 4);
            Assert.Equal(0f, samples[0], 4);
            Assert.Equal(0f, samples[samples.Length - 1], 4);
        }

        [Fact]
        public void BackIsLowerThanTapAndConfirmIsTheLongest()
        {
            float[] tap = UiTone.Make(UiSound.Tap);
            float[] back = UiTone.Make(UiSound.Back);
            float[] confirm = UiTone.Make(UiSound.Confirm);

            Assert.True(Pitch(back) < Pitch(tap), "going back should sound lower than a tap");
            Assert.True(confirm.Length > tap.Length && confirm.Length > back.Length);
        }

        [Fact]
        public void ConfirmRisesFromItsFirstNoteToItsSecond()
        {
            float[] confirm = UiTone.Make(UiSound.Confirm);
            int half = confirm.Length * 2 / 5;

            Assert.True(Pitch(confirm.Take(half).ToArray()) < Pitch(confirm.Skip(half).ToArray()));
        }

        /// <summary>Upward zero crossings per second: the fundamental, as the harmonic is too weak to add any.</summary>
        private static float Pitch(float[] samples)
        {
            int crossings = 0;
            for (int i = 1; i < samples.Length; i++)
            {
                if (samples[i - 1] < 0f && samples[i] >= 0f)
                {
                    crossings++;
                }
            }

            return crossings * (float)UiTone.SampleRate / samples.Length;
        }
    }
}

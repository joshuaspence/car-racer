using System.Collections;
using CarRacer.Game.Ui;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace CarRacer.Tests
{
    /// <summary>A new screen fades in from black and the cover then gets out of the way.</summary>
    public class ScreenFaderTests
    {
        [UnityTest]
        public IEnumerator TheCoverLiftsWithinAFewFrames()
        {
            ScreenFader.Install();
            ScreenFader.Cover();
            Assert.IsTrue(ScreenFader.Fading);

            // At most a 30 fps step a frame, so 0.35 s of fade takes at least 11 frames; allow plenty.
            for (int frame = 0; frame < 300 && ScreenFader.Fading; frame++)
            {
                yield return null;
            }

            Assert.IsFalse(ScreenFader.Fading, "the fade should finish");
        }
    }
}

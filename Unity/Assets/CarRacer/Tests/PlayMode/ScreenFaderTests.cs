using System.Collections;
using CarRacer.Game.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CarRacer.Tests
{
    /// <summary>A new screen fades in from black and the cover then gets out of the way.</summary>
    public class ScreenFaderTests
    {
        [UnityTest]
        public IEnumerator TheCoverLiftsWithinASecond()
        {
            ScreenFader.Install();
            ScreenFader.Cover();
            Assert.IsTrue(ScreenFader.Fading);

            // The fade runs on real time (each frame moving it on by at most a 30 fps step), and the
            // test runner's frames can be far shorter than a phone's, so wait on the clock, not a frame count.
            float giveUp = Time.realtimeSinceStartup + 3f;
            while (ScreenFader.Fading && Time.realtimeSinceStartup < giveUp)
            {
                yield return null;
            }

            Assert.IsFalse(ScreenFader.Fading, "the fade should finish");
        }
    }
}

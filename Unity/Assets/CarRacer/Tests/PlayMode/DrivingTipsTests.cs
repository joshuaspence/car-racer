using System.Collections;
using CarRacer.Game.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CarRacer.Tests
{
    /// <summary>The "how to drive" tips show on the first drive and not once they have been seen.</summary>
    public class DrivingTipsTests
    {
        private bool _seen;

        [SetUp]
        public void SetUp() => _seen = DrivingTipsPanel.Seen;

        [TearDown]
        public void TearDown() => DrivingTipsPanel.Seen = _seen;

        [UnityTest]
        public IEnumerator TheTipsShowUntilSeen()
        {
            DrivingTipsPanel.Seen = false;
            var first = TrackScreen.Create();
            yield return null;
            Assert.IsNotNull(first.GetComponent<DrivingTipsPanel>(), "a first drive should show the tips");
            Object.Destroy(first.gameObject);
            yield return null;

            DrivingTipsPanel.Seen = true;
            var later = TrackScreen.Create();
            yield return null;
            Assert.IsNull(later.GetComponent<DrivingTipsPanel>(), "tips already seen shouldn't show again");
            Object.Destroy(later.gameObject);
        }
    }
}

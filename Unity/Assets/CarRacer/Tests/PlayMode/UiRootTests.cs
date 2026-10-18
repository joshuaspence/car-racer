using System.Collections;
using CarRacer.Game.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CarRacer.Tests
{
    /// <summary>A UI Toolkit layer built from code lays its elements out in 720-pixel-tall reference units.</summary>
    public class UiRootTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp() => _host = new GameObject("UI test");

        [TearDown]
        public void TearDown() => Object.Destroy(_host);

        [UnityTest]
        public IEnumerator ElementsLayOutAtTheHouseSizes()
        {
            var layer = UiRoot.Create(_host.transform, "Test layer");
            var panel = UiKit.Panel(400f);
            int clicks = 0;
            var button = UiKit.Button("Go", () => clicks++, primary: true);
            panel.Add(UiKit.Heading("Hello"));
            panel.Add(button);
            layer.Safe.Add(panel);

            // Layout happens on the panel's next update.
            yield return null;
            yield return null;

            Assert.That(button.resolvedStyle.height, Is.EqualTo(UiKit.ButtonHeight).Within(0.5f));
            Assert.That(panel.resolvedStyle.width, Is.EqualTo(400f).Within(0.5f));
            Assert.That(button.worldBound.height, Is.GreaterThan(0f), "the button should be laid out on screen");
            Assert.AreEqual(PickingMode.Ignore, layer.Safe.pickingMode, "containers mustn't block touches to the game");
            Assert.AreEqual(0, clicks);
        }

        [UnityTest]
        public IEnumerator TheSafeAreaIsInsideTheScreen()
        {
            var layer = UiRoot.Create(_host.transform, "Safe layer");
            yield return null;
            yield return null;

            var full = layer.Full.worldBound;
            var safe = layer.Safe.worldBound;
            Assert.That(full.width, Is.GreaterThan(0f));
            Assert.That(safe.xMin, Is.GreaterThanOrEqualTo(full.xMin - 0.5f));
            Assert.That(safe.xMax, Is.LessThanOrEqualTo(full.xMax + 0.5f));
            if (Screen.height > 0)
            {
                Assert.That(full.height, Is.EqualTo(720f).Within(1f), "the panel is scaled to 720 reference pixels tall");
            }
        }
    }
}

using System.IO;
using System.Linq;
using CarRacer.Game.Audio;
using NUnit.Framework;
using UnityEngine;

namespace CarRacer.Tests
{
    /// <summary>The sound effects import as audio, and every clip the game asks for is there.</summary>
    public class AudioTests
    {
        [Test]
        public void EveryClipLoads()
        {
            foreach (string name in GameAudio.ClipNames)
            {
                var clip = GameAudio.Clip(name);
                Assert.IsNotNull(clip, name);
                Assert.Greater(clip.length, 0f, name);
            }
        }

        [Test]
        public void EveryFileInTheFolderIsAKnownClip()
        {
            var files = Directory.GetFiles(Path.Combine(Application.dataPath, "CarRacer/Resources/Audio"), "*.ogg")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n);
            CollectionAssert.AreEqual(GameAudio.ClipNames.OrderBy(n => n), files);
        }

        [Test]
        public void TheEngineNoteIsMadeInCode()
        {
            var clip = GameAudio.EngineClip();
            Assert.AreEqual(1, clip.channels);
            Assert.Greater(clip.samples, 0);
        }
    }
}

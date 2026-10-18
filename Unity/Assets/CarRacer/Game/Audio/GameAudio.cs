using System.Collections.Generic;
using CarRacer.Core.Audio;
using UnityEngine;

namespace CarRacer.Game.Audio
{
    /// <summary>
    /// Sound effects (design doc §13): CC0 clips from Kenney in <c>Resources/Audio</c>, played
    /// where they happen or, for this phone's own car, straight to the speaker; the engine's note
    /// made in code (<see cref="EngineTone"/>); and the sound-effects volume, remembered.
    /// </summary>
    public static class GameAudio
    {
        public const string VolumeKey = "effectsVolume";

        /// <summary>Every clip in <c>Resources/Audio</c>, so a test can check they all load.</summary>
        public static readonly string[] ClipNames =
        {
            "crunch_light", "crunch_medium", "crunch_heavy", "boost", "item_box",
            "missile", "bomb_throw", "shockwave", "shield", "oil", "explosion",
        };

        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static AudioClip _engine;
        private static AudioSource _speaker;

        public static int VolumeStep
        {
            get => SoundRules.ClampVolumeStep(PlayerPrefs.GetInt(VolumeKey, SoundRules.DefaultVolumeStep));
            private set => PlayerPrefs.SetInt(VolumeKey, value);
        }

        /// <summary>The sound-effects volume, 0 (off) to 1.</summary>
        public static float Volume => SoundRules.VolumeSteps[VolumeStep];

        /// <summary>For the menu button, e.g. "Sound: High".</summary>
        public static string Label => $"Sound: {SoundRules.VolumeName(VolumeStep)}";

        public static void CycleVolume() => VolumeStep = SoundRules.NextVolumeStep(VolumeStep);

        public static AudioClip Clip(string name)
        {
            if (!Clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                Clips[name] = clip;
            }

            return clip;
        }

        /// <summary>The engine's looping note, made once.</summary>
        public static AudioClip EngineClip()
        {
            if (_engine == null)
            {
                float[] samples = EngineTone.Loop();
                _engine = AudioClip.Create("Engine", samples.Length, 1, EngineTone.SampleRate, false);
                _engine.SetData(samples, 0);
            }

            return _engine;
        }

        /// <summary>Plays a clip in the world at <paramref name="position"/>, quieter the further it is from the camera.</summary>
        public static void PlayAt(string name, Vector3 position, float volume = 1f)
        {
            var clip = Clip(name);
            if (clip != null && Volume > 0f)
            {
                AudioSource.PlayClipAtPoint(clip, position, volume * Volume);
            }
        }

        /// <summary>Plays a clip straight to the speaker, for what happens to this phone's own car.</summary>
        public static void Play(string name, float volume = 1f)
        {
            var clip = Clip(name);
            if (clip == null || Volume <= 0f)
            {
                return;
            }

            if (_speaker == null)
            {
                var host = new GameObject("Speaker");
                Object.DontDestroyOnLoad(host);
                _speaker = host.AddComponent<AudioSource>();
                _speaker.playOnAwake = false;
                _speaker.spatialBlend = 0f;
            }

            _speaker.PlayOneShot(clip, volume * Volume);
        }
    }
}

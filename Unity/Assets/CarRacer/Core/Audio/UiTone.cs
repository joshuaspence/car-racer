using System;

namespace CarRacer.Core.Audio
{
    /// <summary>The sounds menu controls make (design doc §13).</summary>
    public enum UiSound
    {
        /// <summary>Any ordinary button: a short high tick.</summary>
        Tap,

        /// <summary>Going back a page or leaving a screen: a lower tick.</summary>
        Back,

        /// <summary>The main action on a page (Practice, Host, Start, Ready): two rising notes.</summary>
        Confirm,
    }

    /// <summary>
    /// Menu sounds made in code, like the engine (<see cref="EngineTone"/>), so there are no clips
    /// to ship for them: each is one or two short sine notes with a touch of second harmonic, a
    /// quick attack and an exponential decay, faded to silence at the end so it never clicks.
    /// </summary>
    public static class UiTone
    {
        public const int SampleRate = 22050;
        public const float Peak = 0.5f;

        /// <summary>The attack and the final fade, in seconds.</summary>
        public const float Edge = 0.003f;

        public static float[] Make(UiSound sound)
        {
            switch (sound)
            {
                case UiSound.Back:
                    return Notes((660f, 0.06f));
                case UiSound.Confirm:
                    return Notes((880f, 0.06f), (1320f, 0.09f));
                default:
                    return Notes((1320f, 0.045f));
            }
        }

        private static float[] Notes(params (float Frequency, float Seconds)[] notes)
        {
            int length = 0;
            foreach (var note in notes)
            {
                length += (int)Math.Round(note.Seconds * SampleRate);
            }

            var samples = new float[length];
            int start = 0;
            foreach (var (frequency, seconds) in notes)
            {
                int count = (int)Math.Round(seconds * SampleRate);
                int edge = (int)Math.Round(Edge * SampleRate);
                for (int i = 0; i < count; i++)
                {
                    double t = (double)i / SampleRate;
                    double phase = 2.0 * Math.PI * frequency * t;
                    double tone = Math.Sin(phase) + 0.25 * Math.Sin(2.0 * phase);

                    // A few milliseconds up, then a decay to about 5% by the end, then a fade to exactly zero.
                    double envelope = Math.Exp(-3.0 * i / count);
                    envelope *= Math.Min(1.0, (double)i / edge);
                    envelope *= Math.Min(1.0, (double)(count - 1 - i) / edge);
                    samples[start + i] = (float)(tone * envelope);
                }

                start += count;
            }

            float peak = 0f;
            foreach (float sample in samples)
            {
                peak = Math.Max(peak, Math.Abs(sample));
            }

            for (int i = 0; i < length; i++)
            {
                samples[i] *= Peak / peak;
            }

            return samples;
        }
    }
}

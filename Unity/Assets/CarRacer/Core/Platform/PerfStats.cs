using System;
using System.Text;

namespace CarRacer.Core.Platform
{
    /// <summary>
    /// Frame times over the last <see cref="WindowSeconds"/> (design doc §14): the average frame
    /// rate, the slowest frame and how many frames missed 30 fps. A spike shows up for the whole
    /// window, so it can be read on a phone mid-race.
    /// </summary>
    public sealed class FrameStats
    {
        public const float WindowSeconds = 2f;

        /// <summary>A frame slower than this missed 30 fps.</summary>
        public const float SlowFrameSeconds = 1f / 30f;

        // Enough for 2 s at 240 fps; at 60 fps the window, not the buffer, is the limit.
        private readonly float[] _frames = new float[512];
        private int _start;
        private int _count;
        private float _total;

        public int Frames => _count;

        /// <summary>Frames per second over the window, or 0 before the first frame.</summary>
        public float Fps => _total > 0f ? _count / _total : 0f;

        public float WorstMs
        {
            get
            {
                float worst = 0f;
                for (int i = 0; i < _count; i++)
                {
                    worst = Math.Max(worst, _frames[(_start + i) % _frames.Length]);
                }

                return worst * 1000f;
            }
        }

        public int SlowFrames
        {
            get
            {
                int slow = 0;
                for (int i = 0; i < _count; i++)
                {
                    if (_frames[(_start + i) % _frames.Length] > SlowFrameSeconds)
                    {
                        slow++;
                    }
                }

                return slow;
            }
        }

        /// <summary>Adds a frame that took <paramref name="seconds"/> and drops frames older than the window.</summary>
        public void Add(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            if (_count == _frames.Length)
            {
                DropOldest();
            }

            _frames[(_start + _count) % _frames.Length] = seconds;
            _count++;
            _total += seconds;

            // Keep the newest frame even if it alone is longer than the window.
            while (_count > 1 && _total - _frames[_start] >= WindowSeconds)
            {
                DropOldest();
            }
        }

        private void DropOldest()
        {
            _total -= _frames[_start];
            _start = (_start + 1) % _frames.Length;
            _count--;
        }
    }

    /// <summary>Bytes sent and received per second, counted in one-second buckets and read as the last full second.</summary>
    public sealed class TrafficCounter
    {
        private long _bucket = -1;
        private int _sending;
        private int _receiving;

        public int SentPerSecond { get; private set; }
        public int ReceivedPerSecond { get; private set; }

        public void Sent(int bytes, double nowSeconds)
        {
            Roll(nowSeconds);
            _sending += bytes;
        }

        public void Received(int bytes, double nowSeconds)
        {
            Roll(nowSeconds);
            _receiving += bytes;
        }

        /// <summary>Moves on to the bucket for <paramref name="nowSeconds"/>; call it each frame so quiet seconds read as zero.</summary>
        public void Roll(double nowSeconds)
        {
            long bucket = (long)Math.Floor(nowSeconds);
            if (bucket == _bucket)
            {
                return;
            }

            // A gap of more than a second means the last full second was silent.
            bool previous = bucket == _bucket + 1;
            SentPerSecond = previous ? _sending : 0;
            ReceivedPerSecond = previous ? _receiving : 0;
            _sending = 0;
            _receiving = 0;
            _bucket = bucket;
        }
    }

    /// <summary>The performance readout's text (design doc §14), kept short enough for a phone held sideways.</summary>
    public static class PerfReadout
    {
        /// <param name="drawCalls">Draw calls last frame, or a negative number where the player can't count them.</param>
        /// <param name="memoryMb">Memory in use, or a negative number if unknown.</param>
        /// <param name="traffic">This phone's network traffic, or null outside a session.</param>
        public static string Format(FrameStats frames, int drawCalls, int memoryMb, TrafficCounter traffic)
        {
            var text = new StringBuilder();
            text.Append($"{frames.Fps:0} fps  worst {frames.WorstMs:0} ms  {frames.SlowFrames} slow");
            text.Append(drawCalls >= 0 ? $"\n{drawCalls} draws" : "\n- draws");
            if (memoryMb >= 0)
            {
                text.Append($"  {memoryMb} MB");
            }

            if (traffic != null)
            {
                text.Append($"\nup {Kilobytes(traffic.SentPerSecond)}  down {Kilobytes(traffic.ReceivedPerSecond)} KB/s");
            }

            return text.ToString();
        }

        private static string Kilobytes(int bytes) => (bytes / 1024f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }
}

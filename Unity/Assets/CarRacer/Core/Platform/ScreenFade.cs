using System;

namespace CarRacer.Core.Platform
{
    /// <summary>
    /// How a new screen fades in from black (design doc §3): fully covered for a moment, so the
    /// frames where the screen is still being built never show, then an eased fade to clear. Time
    /// only advances by at most <see cref="MaxStep"/> a frame, so a slow first frame (a track being
    /// built) can't use up the fade before anything is seen.
    /// </summary>
    public static class ScreenFade
    {
        public const float HoldSeconds = 0.05f;
        public const float FadeSeconds = 0.3f;

        /// <summary>The most one frame moves the fade on: a 30 fps frame.</summary>
        public const float MaxStep = 1f / 30f;

        public static float Total => HoldSeconds + FadeSeconds;

        /// <summary>The fade's clock after a frame of <paramref name="deltaTime"/> seconds.</summary>
        public static float Advance(float elapsed, float deltaTime) => elapsed + Math.Max(0f, Math.Min(deltaTime, MaxStep));

        /// <summary>How opaque the black cover is, 1 to 0, <paramref name="elapsed"/> seconds in.</summary>
        public static float Alpha(float elapsed)
        {
            if (elapsed <= HoldSeconds)
            {
                return 1f;
            }

            float t = Math.Min(1f, (elapsed - HoldSeconds) / FadeSeconds);

            // Smoothstep, so the cover lifts gently at both ends.
            return 1f - t * t * (3f - 2f * t);
        }

        public static bool Done(float elapsed) => elapsed >= Total;
    }
}

using System;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Driving
{
    /// <summary>
    /// Optional tilt steering (design doc §5): turn the phone like a steering wheel. Works from
    /// the accelerometer's reading of gravity, turned into screen space (x right, y up), so it is
    /// the same whichever way round the landscape phone is held.
    /// </summary>
    public static class TiltSteering
    {
        public const string PreferenceKey = "tiltSteering";

        /// <summary>Turning the phone this far is full lock.</summary>
        public const float FullLockDegrees = 30f;

        /// <summary>Tilts smaller than this drive straight, so a slightly crooked grip doesn't wander.</summary>
        public const float DeadZoneDegrees = 3f;

        /// <summary>Below this much gravity in the screen's plane (the phone lying flat) it can't tell, so it steers straight.</summary>
        public const float MinGravityInPlane = 0.3f;

        /// <summary>
        /// A reading in the device's own axes (x right and y up when held upright) turned into
        /// screen axes for a phone held in landscape: <paramref name="landscapeLeft"/> when it is
        /// turned anticlockwise from upright, otherwise clockwise.
        /// </summary>
        public static Vec2 ToScreen(float deviceX, float deviceY, bool landscapeLeft) =>
            landscapeLeft ? new Vec2(-deviceY, deviceX) : new Vec2(deviceY, -deviceX);

        /// <summary>-1 (full left) to 1 (full right) from gravity in screen axes; turning the phone clockwise steers right.</summary>
        public static float Steer(Vec2 screenGravity)
        {
            float inPlane = MathF.Sqrt(screenGravity.X * screenGravity.X + screenGravity.Y * screenGravity.Y);
            if (inPlane < MinGravityInPlane)
            {
                return 0f;
            }

            float degrees = MathF.Atan2(screenGravity.X, -screenGravity.Y) * 180f / MathF.PI;
            float magnitude = MathF.Abs(degrees);
            if (magnitude <= DeadZoneDegrees)
            {
                return 0f;
            }

            return Math.Sign(degrees) * Math.Min(1f, (magnitude - DeadZoneDegrees) / (FullLockDegrees - DeadZoneDegrees));
        }
    }
}

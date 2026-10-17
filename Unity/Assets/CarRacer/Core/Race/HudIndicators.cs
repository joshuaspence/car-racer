using System;
using System.Collections.Generic;
using CarRacer.Core.Items;
using CarRacer.Core.Net;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Race
{
    /// <summary>An arrow at the edge of the screen for a car the chase camera can't show.</summary>
    public struct EdgeIndicator
    {
        /// <summary>Which of the rivals passed in it is for.</summary>
        public int Index;

        /// <summary>Where the car is, relative to the way this car faces: 0 ahead, +π/2 to the right, ±π behind.</summary>
        public float Bearing;

        /// <summary>0 at the edge of <see cref="HudIndicators.Range"/>, rising to 1 at <see cref="HudIndicators.NearDistance"/>.</summary>
        public float Closeness;
    }

    /// <summary>
    /// The race HUD's edge-of-screen indicators and incoming-missile warning (design doc §6): which
    /// nearby cars are behind or beside you, how close they are, and where on the screen's edge to
    /// point at them.
    /// </summary>
    public static class HudIndicators
    {
        /// <summary>Cars further away than this get no arrow.</summary>
        public const float Range = 25f;

        /// <summary>Arrows are full size this close.</summary>
        public const float NearDistance = 4f;

        /// <summary>Cars within this angle of straight ahead are on screen, so they get no arrow.</summary>
        public const float ViewHalfAngleRadians = 50f * MathF.PI / 180f;

        /// <summary>A Missile chasing you is announced once it is this close.</summary>
        public const float MissileWarningRange = 80f;

        /// <summary>Where <paramref name="to"/> is from a car at <paramref name="from"/> facing <paramref name="headingRadians"/>: 0 ahead, positive to the right.</summary>
        public static float Bearing(Vec3 from, float headingRadians, Vec3 to)
        {
            var d = (to - from).Flattened;
            float forward = d.X * MathF.Sin(headingRadians) + d.Z * MathF.Cos(headingRadians);
            float right = d.X * MathF.Cos(headingRadians) - d.Z * MathF.Sin(headingRadians);
            return MathF.Atan2(right, forward);
        }

        /// <summary>Fills <paramref name="into"/> with an arrow for each rival in range and out of the camera's view.</summary>
        public static void Rivals(Vec3 me, float headingRadians, IReadOnlyList<Vec3> rivals, List<EdgeIndicator> into)
        {
            into.Clear();
            for (int i = 0; i < rivals.Count; i++)
            {
                float distance = (rivals[i] - me).Length;
                if (distance > Range)
                {
                    continue;
                }

                float bearing = Bearing(me, headingRadians, rivals[i]);
                if (MathF.Abs(bearing) <= ViewHalfAngleRadians)
                {
                    continue;
                }

                into.Add(new EdgeIndicator { Index = i, Bearing = bearing, Closeness = Closeness(distance) });
            }
        }

        public static float Closeness(float distance) =>
            Math.Max(0f, Math.Min(1f, (Range - distance) / (Range - NearDistance)));

        /// <summary>The nearest Missile chasing <paramref name="playerId"/> within <see cref="MissileWarningRange"/>, if any.</summary>
        public static bool TryIncomingMissile(IReadOnlyList<ProjectileSnapshot> projectiles, byte playerId, Vec3 me, out ProjectileSnapshot missile, out float distance)
        {
            missile = default;
            distance = float.MaxValue;
            bool found = false;
            foreach (var p in projectiles)
            {
                if (p.Kind != ItemKind.Missile || p.TargetId != playerId)
                {
                    continue;
                }

                float d = (p.Position - me).Length;
                if (d <= MissileWarningRange && d < distance)
                {
                    missile = p;
                    distance = d;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Where an arrow for <paramref name="bearing"/> sits on a <paramref name="width"/> by
        /// <paramref name="height"/> screen, <paramref name="inset"/> in from its edge, with ahead up
        /// the screen and y running down (IMGUI's way).
        /// </summary>
        public static Vec2 EdgePoint(float bearing, float width, float height, float inset)
        {
            float dx = MathF.Sin(bearing);
            float dy = -MathF.Cos(bearing);
            float halfWidth = width / 2f - inset;
            float halfHeight = height / 2f - inset;
            float t = Math.Min(
                MathF.Abs(dx) > 1e-6f ? halfWidth / MathF.Abs(dx) : float.MaxValue,
                MathF.Abs(dy) > 1e-6f ? halfHeight / MathF.Abs(dy) : float.MaxValue);
            return new Vec2(width / 2f + dx * t, height / 2f + dy * t);
        }
    }
}

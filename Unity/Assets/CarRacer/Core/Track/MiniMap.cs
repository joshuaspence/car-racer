using System;
using System.Collections.Generic;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Track
{
    /// <summary>
    /// Fits a track's outline into a square on the HUD (design doc §6, mini-map). World X runs left
    /// to right and world Z bottom to top, so the map is a top-down view with north up. The scale is
    /// the same on both axes, so the track keeps its shape, and the outline is centred with a margin.
    /// Map coordinates are 0..1 from the bottom-left corner.
    /// </summary>
    public sealed class MiniMap
    {
        /// <summary>Space left around the outline, as a fraction of the map's size.</summary>
        public const float Margin = 0.08f;

        private readonly float _minX;
        private readonly float _minZ;
        private readonly float _scale;
        private readonly float _offsetX;
        private readonly float _offsetZ;

        public MiniMap(TrackSpline spline, float outlineSpacing = 4f)
        {
            var samples = spline.SampleEvenly(outlineSpacing);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var s in samples)
            {
                minX = Math.Min(minX, s.Position.X);
                maxX = Math.Max(maxX, s.Position.X);
                minZ = Math.Min(minZ, s.Position.Z);
                maxZ = Math.Max(maxZ, s.Position.Z);
            }

            float span = Math.Max(Math.Max(maxX - minX, maxZ - minZ), 1e-3f);
            _minX = minX;
            _minZ = minZ;
            _scale = (1f - 2f * Margin) / span;
            _offsetX = Margin + (span - (maxX - minX)) * _scale / 2f;
            _offsetZ = Margin + (span - (maxZ - minZ)) * _scale / 2f;

            var outline = new List<Vec2>(samples.Count);
            foreach (var s in samples)
            {
                outline.Add(ToMap(s.Position));
            }

            Outline = outline;
        }

        /// <summary>The track's centre line as a closed loop of map points.</summary>
        public IReadOnlyList<Vec2> Outline { get; }

        public Vec2 ToMap(Vec3 world) => new Vec2(
            _offsetX + (world.X - _minX) * _scale,
            _offsetZ + (world.Z - _minZ) * _scale);
    }
}

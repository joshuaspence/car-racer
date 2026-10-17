using System.Collections.Generic;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Track
{
    /// <summary>Engine-independent mesh buffers; the Unity layer copies these into a UnityEngine.Mesh.</summary>
    public sealed class MeshData
    {
        public readonly List<Vec3> Vertices = new List<Vec3>();
        public readonly List<Vec3> Normals = new List<Vec3>();
        public readonly List<Vec2> Uvs = new List<Vec2>();

        /// <summary>Clockwise winding when viewed from the visible side (Unity's front-face convention).</summary>
        public readonly List<int> Triangles = new List<int>();

        public int TriangleCount => Triangles.Count / 3;
    }

    public sealed class TrackMesh
    {
        public MeshData Road;
        public MeshData LeftWall;
        public MeshData RightWall;
    }

    /// <summary>Generates the road surface and inward-facing barrier walls along a track spline.</summary>
    public static class RoadMeshBuilder
    {
        public static TrackMesh Build(TrackSpline spline, float wallHeight, float spacing = 2f)
        {
            var samples = spline.SampleEvenly(spacing);
            // Repeat the first ring (at full lap distance) so UVs run continuously round the loop.
            var closing = samples[0];
            closing.Distance = spline.Length;
            samples.Add(closing);

            return new TrackMesh
            {
                Road = BuildRoad(samples),
                LeftWall = BuildWall(samples, wallHeight, leftSide: true),
                RightWall = BuildWall(samples, wallHeight, leftSide: false),
            };
        }

        private static MeshData BuildRoad(List<TrackSample> samples)
        {
            var mesh = new MeshData();
            // V advances by each step's length over the local width (one texture tile is roughly
            // square), accumulated so a change in width can't make it run backwards.
            float v = 0f;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                if (i > 0)
                {
                    var previous = samples[i - 1];
                    v += (s.Distance - previous.Distance) / ((s.Width + previous.Width) / 2f);
                }

                Vec3 half = s.Right * (s.Width / 2f);
                mesh.Vertices.Add(s.Position - half);
                mesh.Vertices.Add(s.Position + half);
                mesh.Normals.Add(s.Up);
                mesh.Normals.Add(s.Up);
                mesh.Uvs.Add(new Vec2(0f, v));
                mesh.Uvs.Add(new Vec2(1f, v));
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                int left = i * 2;
                int right = left + 1;
                int nextLeft = left + 2;
                int nextRight = left + 3;
                mesh.Triangles.AddRange(new[] { left, nextLeft, nextRight, left, nextRight, right });
            }

            return mesh;
        }

        private static MeshData BuildWall(List<TrackSample> samples, float height, bool leftSide)
        {
            var mesh = new MeshData();
            foreach (var s in samples)
            {
                Vec3 edge = s.Position + s.Right * (leftSide ? -s.Width / 2f : s.Width / 2f);
                Vec3 inward = leftSide ? s.Right : -s.Right;
                float u = s.Distance / height;
                mesh.Vertices.Add(edge);
                mesh.Vertices.Add(edge + s.Up * height);
                mesh.Normals.Add(inward);
                mesh.Normals.Add(inward);
                mesh.Uvs.Add(new Vec2(u, 0f));
                mesh.Uvs.Add(new Vec2(u, 1f));
            }

            for (int i = 0; i < samples.Count - 1; i++)
            {
                int bottom = i * 2;
                int top = bottom + 1;
                int nextBottom = bottom + 2;
                int nextTop = bottom + 3;
                if (leftSide)
                {
                    mesh.Triangles.AddRange(new[] { bottom, top, nextTop, bottom, nextTop, nextBottom });
                }
                else
                {
                    mesh.Triangles.AddRange(new[] { bottom, nextTop, top, bottom, nextBottom, nextTop });
                }
            }

            return mesh;
        }
    }
}

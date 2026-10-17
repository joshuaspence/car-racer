using CarRacer.Core.Numerics;
using CarRacer.Core.Track;
using UnityEngine;

namespace CarRacer.Game
{
    /// <summary>Bridges the engine-independent core types to Unity types.</summary>
    public static class UnityConversions
    {
        public static Vector3 ToUnity(this Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        public static Vec3 ToCore(this Vector3 v) => new Vec3(v.x, v.y, v.z);

        public static Vector2 ToUnity(this Vec2 v) => new Vector2(v.X, v.Y);

        /// <summary>Copies generated mesh buffers into a new Unity mesh.</summary>
        public static Mesh ToUnityMesh(this MeshData data, string name)
        {
            var vertices = new Vector3[data.Vertices.Count];
            var normals = new Vector3[data.Normals.Count];
            var uvs = new Vector2[data.Uvs.Count];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = data.Vertices[i].ToUnity();
                normals[i] = data.Normals[i].ToUnity();
                uvs[i] = data.Uvs[i].ToUnity();
            }

            var mesh = new Mesh { name = name };
            if (vertices.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = data.Triangles.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

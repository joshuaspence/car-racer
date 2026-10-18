using System.Collections.Generic;
using CarRacer.Core.Art;
using CarRacer.Core.Numerics;
using CarRacer.Core.Track;
using UnityEngine;

namespace CarRacer.Game.Arenas
{
    /// <summary>
    /// Puts Kenney Racing Kit scenery round a track (design doc §13): <see cref="TrackScenery"/>
    /// places it and <see cref="SceneryMesh"/> bakes it into one mesh per colour. The models are read
    /// from <c>Resources/Models</c> once. Scenery has no colliders; it sits behind the walls.
    /// </summary>
    public static class TrackDressing
    {
        private static Dictionary<string, ObjModel> _models;
        private static Dictionary<string, Vec3> _colours;

        public static void Build(Transform parent, TrackLayout layout, string name)
        {
            if (!Load())
            {
                return;
            }

            var placements = TrackScenery.Place(layout.Spline, model => _models.TryGetValue(model, out var m) ? m.Max.X - m.Min.X : 0f);
            foreach (var (colour, mesh) in SceneryMesh.Build(placements, _models, _colours))
            {
                var piece = new GameObject($"{name} scenery {ColorUtility.ToHtmlStringRGB(ToColour(colour))}");
                piece.transform.SetParent(parent, false);
                piece.AddComponent<MeshFilter>().sharedMesh = mesh.ToUnityMesh(piece.name);
                piece.AddComponent<MeshRenderer>().sharedMaterial = SimpleMaterials.Get(ToColour(colour));
            }
        }

        private static bool Load()
        {
            if (_models != null)
            {
                return _models.Count > 0;
            }

            _models = new Dictionary<string, ObjModel>();
            _colours = new Dictionary<string, Vec3>();
            foreach (string model in TrackScenery.Models)
            {
                var obj = Resources.Load<TextAsset>("Models/" + model + ".obj");
                var mtl = Resources.Load<TextAsset>("Models/" + model + ".mtl");
                if (obj == null || mtl == null)
                {
                    Debug.LogError($"Scenery model '{model}' is missing; leaving it out.");
                    continue;
                }

                _models[model] = ObjModel.Parse(obj.text);
                ObjMaterials.Parse(mtl.text, _colours);
            }

            return _models.Count > 0;
        }

        private static Color ToColour(Vec3 c) => new Color(c.X, c.Y, c.Z);
    }
}

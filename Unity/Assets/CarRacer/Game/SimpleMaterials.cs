using System.Collections.Generic;
using UnityEngine;

namespace CarRacer.Game
{
    /// <summary>
    /// Flat-coloured materials and collider-free primitive parts for placeholder geometry. Colours
    /// are copies of the default primitive material, so they work with whichever render pipeline
    /// is active and need no shader lookups that a build could strip.
    /// </summary>
    public static class SimpleMaterials
    {
        private static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();
        private static Material _template;

        public static Material Get(Color colour)
        {
            if (Cache.TryGetValue(colour, out var material) && material != null)
            {
                return material;
            }

            if (_template == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _template = probe.GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(probe);
            }

            material = new Material(_template) { name = "Colour " + ColorUtility.ToHtmlStringRGB(colour), color = colour };
            Cache[colour] = material;
            return material;
        }

        /// <summary>A primitive shape for show only (no collider), parented under <paramref name="parent"/>.</summary>
        public static Transform Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CarRacer.Game
{
    /// <summary>
    /// Flat-coloured and textured materials, and collider-free primitive parts for placeholder geometry.
    /// </summary>
    public static class SimpleMaterials
    {
        /// <summary>
        /// A cheap lit shader that is always in the build: <c>ProjectConfigurator</c> adds it to the
        /// always-included shaders. A build only keeps shaders that some asset references, so the
        /// default primitive material's Standard shader was stripped and everything rendered pink.
        /// </summary>
        public const string ShaderName = "Legacy Shaders/Diffuse";

        private static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();
        private static Material _template;

        public static Material Get(Color colour)
        {
            if (Cache.TryGetValue(colour, out var material) && material != null)
            {
                return material;
            }

            material = new Material(Template()) { name = "Colour " + ColorUtility.ToHtmlStringRGB(colour), color = colour };
            Cache[colour] = material;
            return material;
        }

        /// <summary>A new material showing <paramref name="texture"/> untinted, e.g. a car's painted palette.</summary>
        public static Material Textured(Texture2D texture) =>
            new Material(Template()) { name = texture.name, color = Color.white, mainTexture = texture };

        private static Material Template()
        {
            if (_template == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader != null)
                {
                    _template = new Material(shader);
                }
                else
                {
                    Debug.LogError($"Shader '{ShaderName}' is missing from the build; falling back to the default material.");
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _template = probe.GetComponent<Renderer>().sharedMaterial;
                    Object.DestroyImmediate(probe);
                }
            }

            return _template;
        }

        /// <summary>A primitive shape for show only (no collider), parented under <paramref name="parent"/>.</summary>
        public static Transform Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);

            // The collider can be missing if a build strips its class (see link.xml); there's nothing to remove then.
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }
    }
}

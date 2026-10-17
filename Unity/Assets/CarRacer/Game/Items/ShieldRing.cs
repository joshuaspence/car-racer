using UnityEngine;

namespace CarRacer.Game.Items
{
    /// <summary>
    /// The Shield item's look: a flat cyan ring around a car, at the height of its wheels. The
    /// placeholder shader is opaque, so a bubble round the car would hide it.
    /// </summary>
    public static class ShieldRing
    {
        private static readonly Color Colour = new Color(0.3f, 0.9f, 1f);

        /// <summary>Adds a hidden ring under <paramref name="car"/> (the physics sphere's centre).</summary>
        public static Transform Build(Transform car)
        {
            var ring = SimpleMaterials.Part(PrimitiveType.Cylinder, car, new Vector3(0f, -0.45f, 0f), new Vector3(3.2f, 0.02f, 3.2f), SimpleMaterials.Get(Colour));
            ring.name = "Shield";
            ring.gameObject.SetActive(false);
            return ring;
        }

        public static void Show(Transform ring, bool shown)
        {
            if (ring != null && ring.gameObject.activeSelf != shown)
            {
                ring.gameObject.SetActive(shown);
            }
        }
    }
}

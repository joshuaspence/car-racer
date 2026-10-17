using CarRacer.Game.Net;
using UnityEngine;

namespace CarRacer.Game.Driving
{
    /// <summary>
    /// Builds a car from code: the physics sphere plus a placeholder body made of primitives,
    /// until the CC0 car models arrive (design doc §13).
    /// </summary>
    public static class CarFactory
    {
        private static PhysicsMaterial _frictionless;

        public static CarController Build(string name, Color colour, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // The car model owns grip, so the sphere itself must slide freely.
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = CarController.SphereRadius;
            sphere.sharedMaterial = Frictionless();

            var visual = new GameObject("Body").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, -CarController.SphereRadius, 0f);
            BuildBody(visual, colour);

            var car = root.AddComponent<CarController>();
            car.Initialise(visual);
            root.AddComponent<CarHaptics>();
            return car;
        }

        /// <summary>
        /// Another phone's car: just the body, placed by its <see cref="RemoteCar"/>. It has no
        /// collider, so it is never a solid wall for this phone's car; contact with it is
        /// resolved by <c>BumpMonitor</c> instead (design doc §2.2).
        /// </summary>
        public static RemoteCar BuildRemote(string name, byte playerId, Color colour, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var visual = new GameObject("Body").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, -CarController.SphereRadius, 0f);
            BuildBody(visual, colour);

            var car = root.AddComponent<RemoteCar>();
            car.Initialise(playerId, colour, visual);
            return car;
        }

        private static void BuildBody(Transform visual, Color colour)
        {
            var paint = SimpleMaterials.Get(colour);
            var dark = SimpleMaterials.Get(new Color(0.12f, 0.12f, 0.14f));
            var glass = SimpleMaterials.Get(new Color(0.55f, 0.75f, 0.9f));

            SimpleMaterials.Part(PrimitiveType.Cube, visual, new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.45f, 2.3f), paint);
            SimpleMaterials.Part(PrimitiveType.Cube, visual, new Vector3(0f, 0.85f, -0.2f), new Vector3(1f, 0.4f, 1.1f), glass);
            SimpleMaterials.Part(PrimitiveType.Cube, visual, new Vector3(0f, 0.6f, -1.15f), new Vector3(1.2f, 0.12f, 0.25f), dark);

            foreach (float x in new[] { -0.7f, 0.7f })
            {
                foreach (float z in new[] { -0.75f, 0.8f })
                {
                    var wheel = SimpleMaterials.Part(PrimitiveType.Cylinder, visual, new Vector3(x, 0.32f, z), new Vector3(0.62f, 0.12f, 0.62f), dark);
                    wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
        }

        private static PhysicsMaterial Frictionless()
        {
            if (_frictionless == null)
            {
                _frictionless = new PhysicsMaterial("Car")
                {
                    staticFriction = 0f,
                    dynamicFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                };
            }

            return _frictionless;
        }
    }
}

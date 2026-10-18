using CarRacer.Core.Art;
using CarRacer.Game.Audio;
using CarRacer.Game.Net;
using UnityEngine;

namespace CarRacer.Game.Driving
{
    /// <summary>
    /// Builds a car from code: the physics sphere plus the player's Kenney body in their colour
    /// (design doc §11, §13), or a placeholder made of primitives if the model can't be loaded.
    /// Every body drives the same; only the look differs.
    /// </summary>
    public static class CarFactory
    {
        private static PhysicsMaterial _frictionless;

        /// <param name="haptics">Whether bumps and boosts vibrate the phone and sound up close: yes for this phone's own car, no for bots the host drives.</param>
        /// <param name="body">The garage body (design doc §11); the race car if not given.</param>
        public static CarController Build(string name, Color colour, Transform parent, bool haptics = true, CarModel body = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var sphereBody = root.AddComponent<Rigidbody>();
            sphereBody.mass = 1f;
            sphereBody.linearDamping = 0f;
            sphereBody.angularDamping = 0f;
            sphereBody.constraints = RigidbodyConstraints.FreezeRotation;
            sphereBody.interpolation = RigidbodyInterpolation.Interpolate;
            sphereBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // The car model owns grip, so the sphere itself must slide freely.
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = CarController.SphereRadius;
            sphere.sharedMaterial = Frictionless();

            var visual = new GameObject("Body").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, -CarController.SphereRadius, 0f);
            BuildBody(visual, colour, body);

            var car = root.AddComponent<CarController>();
            car.Initialise(visual);
            if (haptics)
            {
                root.AddComponent<CarHaptics>();
            }

            CarAudio.ForCar(car, own: haptics);
            return car;
        }

        /// <summary>
        /// Another phone's car: just the body, placed by its <see cref="RemoteCar"/>. It has no
        /// collider, so it is never a solid wall for this phone's car; contact with it is
        /// resolved by <c>BumpMonitor</c> instead (design doc §2.2).
        /// </summary>
        public static RemoteCar BuildRemote(string name, byte playerId, Color colour, Transform parent, CarModel body = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var visual = new GameObject("Body").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, -CarController.SphereRadius, 0f);
            BuildBody(visual, colour, body);

            var car = root.AddComponent<RemoteCar>();
            car.Initialise(playerId, colour, visual);
            CarAudio.ForRemote(car);
            return car;
        }

        private static void BuildBody(Transform visual, Color colour, CarModel body)
        {
            if (CarBodies.TryBuild(visual, colour, body ?? CarModels.Race))
            {
                return;
            }

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

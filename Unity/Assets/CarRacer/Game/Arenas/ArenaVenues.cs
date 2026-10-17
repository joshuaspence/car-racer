using CarRacer.Core.Arena;
using CarRacer.Core.Race;
using CarRacer.Game.Net;
using UnityEngine;

namespace CarRacer.Game.Arenas
{
    /// <summary>
    /// The two arenas (design doc §7.0), built from code next to the track scene: the floating Sumo
    /// platform, which shrinks with <see cref="SumoRules.PlatformRadius"/> once a Sumo event is under
    /// way, with a catch floor far below for the cars that fall off, and the walled Battle bowl. Every
    /// phone works out the platform's size from the shared start time, so it shrinks in step everywhere.
    /// </summary>
    public sealed class ArenaVenues : MonoBehaviour
    {
        private const float PlatformThickness = 1f;
        private const float CatchFloorDepth = 30f;
        private const int BowlWallSegments = 40;
        private const float BowlWallHeight = 1.6f;

        private static readonly Color PlatformColour = new Color(0.8f, 0.78f, 0.7f);
        private static readonly Color PlatformEdgeColour = new Color(0.9f, 0.3f, 0.25f);
        private static readonly Color CatchFloorColour = new Color(0.2f, 0.35f, 0.6f);
        private static readonly Color BowlFloorColour = new Color(0.45f, 0.42f, 0.5f);
        private static readonly Color BowlWallColour = new Color(0.95f, 0.65f, 0.2f);

        private NetworkSession _session;
        private Transform _platform;
        private float _platformRadius = -1f;

        public static ArenaVenues Build(Transform parent, NetworkSession session)
        {
            var venues = new GameObject("Arenas").AddComponent<ArenaVenues>();
            venues.transform.SetParent(parent, false);
            venues._session = session;
            venues.BuildSumo();
            venues.BuildBattle();
            return venues;
        }

        private void BuildSumo()
        {
            var centre = ArenaLayout.SumoCentre.ToUnity();
            _platform = Solid(PrimitiveType.Cylinder, "Sumo platform", centre, Vector3.one, PlatformColour);

            // A thin red rim just under the top, so the edge reads from the overhead camera.
            var rim = SimpleMaterials.Part(PrimitiveType.Cylinder, _platform, new Vector3(0f, -0.05f, 0f), new Vector3(1.04f, 0.92f, 1.04f), SimpleMaterials.Get(PlatformEdgeColour));
            rim.name = "Rim";
            SetPlatformRadius(SumoRules.StartRadius);

            var floor = Solid(PrimitiveType.Plane, "Sumo catch floor", centre + Vector3.down * CatchFloorDepth, new Vector3(20f, 1f, 20f), CatchFloorColour);
            floor.gameObject.isStatic = true;
        }

        private void BuildBattle()
        {
            var centre = ArenaLayout.BattleCentre.ToUnity();
            float radius = BattleRules.BowlRadius;
            Solid(PrimitiveType.Cylinder, "Battle floor", centre + Vector3.down * (PlatformThickness / 2f), new Vector3(2f * radius + 4f, PlatformThickness / 2f, 2f * radius + 4f), BowlFloorColour);

            float segment = 2f * Mathf.PI * radius / BowlWallSegments * 1.08f;
            for (int i = 0; i < BowlWallSegments; i++)
            {
                float angle = 360f * i / BowlWallSegments;
                var rotation = Quaternion.Euler(0f, angle, 0f);
                var position = centre + rotation * new Vector3(0f, BowlWallHeight / 2f, radius + 0.5f);
                var wall = Solid(PrimitiveType.Cube, $"Battle wall {i}", position, new Vector3(segment, BowlWallHeight, 1f), BowlWallColour);
                wall.rotation = rotation;
            }
        }

        /// <summary>A static primitive with its collider swapped for a mesh collider (cylinders come with a capsule).</summary>
        private Transform Solid(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color colour)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            var collider = part.GetComponent<Collider>();
            if (type == PrimitiveType.Cylinder)
            {
                if (collider != null)
                {
                    DestroyImmediate(collider);
                }

                part.AddComponent<MeshCollider>().sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;
            }

            part.transform.SetParent(transform, false);
            part.transform.position = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = SimpleMaterials.Get(colour);
            return part.transform;
        }

        private void SetPlatformRadius(float radius)
        {
            _platformRadius = radius;

            // Unity's cylinder is 1 m across and 2 m tall, centred on its middle; keep the top at the centre's height.
            _platform.localScale = new Vector3(2f * radius, PlatformThickness / 2f, 2f * radius);
            _platform.position = ArenaLayout.SumoCentre.ToUnity() + Vector3.down * (PlatformThickness / 2f);
        }

        private void Update()
        {
            var race = _session != null ? _session.Race : null;
            float radius = SumoRules.StartRadius;
            if (race != null && race.Mode == GameMode.Sumo && (race.Phase == RacePhase.Racing || race.Phase == RacePhase.Results))
            {
                radius = SumoRules.PlatformRadius(Mathf.Max(0f, NetworkSession.NowMs - race.StartAtLocalMs) / 1000f);
            }

            if (Mathf.Abs(radius - _platformRadius) > 0.05f)
            {
                SetPlatformRadius(radius);
            }
        }
    }
}

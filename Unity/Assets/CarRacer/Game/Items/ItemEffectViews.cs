using System.Collections.Generic;
using CarRacer.Core.Items;
using CarRacer.Core.Net;
using CarRacer.Game.Net;
using UnityEngine;

namespace CarRacer.Game.Items
{
    /// <summary>
    /// Shows what items leave on the track (design doc §8): a dark disc for each oil slick, kept in
    /// step with the session's hazards; Missiles and Bombs in flight, eased towards the positions the
    /// host sends; and a quickly expanding ring where a Shockwave or a Bomb goes off.
    /// </summary>
    public sealed class ItemEffectViews : MonoBehaviour
    {
        private const float RingSeconds = 0.4f;

        /// <summary>How quickly a drawn projectile closes on its latest known position (per second, exponential).</summary>
        private const float ProjectileSharpness = 20f;

        private static readonly Color OilColour = new Color(0.08f, 0.07f, 0.1f);
        private static readonly Color ShockwaveColour = new Color(0.55f, 0.8f, 1f);
        private static readonly Color BlastColour = new Color(1f, 0.55f, 0.15f);
        private static readonly Color MissileColour = new Color(0.9f, 0.15f, 0.12f);
        private static readonly Color BombColour = new Color(0.12f, 0.12f, 0.14f);

        private readonly Dictionary<ushort, Transform> _oil = new Dictionary<ushort, Transform>();
        private readonly Dictionary<ushort, Transform> _projectiles = new Dictionary<ushort, Transform>();
        private readonly HashSet<ushort> _seen = new HashSet<ushort>();
        private readonly List<ushort> _gone = new List<ushort>();
        private readonly List<(Transform Ring, float Age, float Radius)> _rings = new List<(Transform, float, float)>();
        private IItemView _items;

        public void Initialise(IItemView items)
        {
            _items = items;
            _items.ItemUsed += OnItemUsed;
            _items.BombExploded += OnBombExploded;
        }

        private void OnDestroy()
        {
            if (_items != null)
            {
                _items.ItemUsed -= OnItemUsed;
                _items.BombExploded -= OnBombExploded;
            }
        }

        private void OnItemUsed(ItemUsedMessage used)
        {
            if (used.Item == ItemKind.Shockwave)
            {
                AddRing(used.Position.ToUnity(), ItemEffects.ShockwaveRadius, ShockwaveColour);
            }
        }

        private void OnBombExploded(CarRacer.Core.Numerics.Vec3 position)
        {
            AddRing(position.ToUnity(), ProjectileRules.BombBlastRadius, BlastColour);
        }

        private void AddRing(Vector3 centre, float radius, Color colour)
        {
            var ring = SimpleMaterials.Part(PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.zero, SimpleMaterials.Get(colour));
            ring.position = centre + Vector3.down * 0.5f;
            _rings.Add((ring, 0f, radius));
        }

        private void Update()
        {
            if (_items == null)
            {
                return;
            }

            SyncOil();
            SyncProjectiles();

            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var (ring, age, radius) = _rings[i];
                age += Time.deltaTime;
                if (age >= RingSeconds)
                {
                    Destroy(ring.gameObject);
                    _rings.RemoveAt(i);
                    continue;
                }

                float diameter = 2f * radius * (age / RingSeconds);
                ring.localScale = new Vector3(diameter, 0.03f, diameter);
                _rings[i] = (ring, age, radius);
            }
        }

        private void SyncProjectiles()
        {
            _seen.Clear();
            float blend = 1f - Mathf.Exp(-ProjectileSharpness * Time.deltaTime);
            foreach (var snapshot in _items.Projectiles)
            {
                _seen.Add(snapshot.EffectId);
                var target = snapshot.Position.ToUnity();
                if (!_projectiles.TryGetValue(snapshot.EffectId, out var view))
                {
                    view = snapshot.Kind == ItemKind.Missile
                        ? SimpleMaterials.Part(PrimitiveType.Cube, transform, Vector3.zero, new Vector3(0.35f, 0.35f, 1.1f), SimpleMaterials.Get(MissileColour))
                        : SimpleMaterials.Part(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.8f, SimpleMaterials.Get(BombColour));
                    view.position = target;
                    _projectiles[snapshot.EffectId] = view;
                    continue;
                }

                var next = Vector3.Lerp(view.position, target, blend);
                var travel = next - view.position;
                travel.y = 0f;
                if (snapshot.Kind == ItemKind.Missile && travel.sqrMagnitude > 1e-6f)
                {
                    view.rotation = Quaternion.LookRotation(travel);
                }

                view.position = next;
            }

            RemoveUnseen(_projectiles);
        }

        private void SyncOil()
        {
            _seen.Clear();
            foreach (var hazard in _items.Hazards.Active)
            {
                _seen.Add(hazard.EffectId);
                if (!_oil.ContainsKey(hazard.EffectId))
                {
                    float diameter = 2f * ItemEffects.OilRadius;
                    var disc = SimpleMaterials.Part(PrimitiveType.Cylinder, transform, Vector3.zero, new Vector3(diameter, 0.02f, diameter), SimpleMaterials.Get(OilColour));
                    disc.position = hazard.Position.ToUnity() + Vector3.down * 0.55f;
                    _oil[hazard.EffectId] = disc;
                }
            }

            RemoveUnseen(_oil);
        }

        /// <summary>Destroys the views in <paramref name="views"/> whose ids weren't seen this frame.</summary>
        private void RemoveUnseen(Dictionary<ushort, Transform> views)
        {
            _gone.Clear();
            foreach (var id in views.Keys)
            {
                if (!_seen.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (var id in _gone)
            {
                Destroy(views[id].gameObject);
                views.Remove(id);
            }
        }
    }
}

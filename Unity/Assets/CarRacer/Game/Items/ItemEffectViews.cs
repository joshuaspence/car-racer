using System.Collections.Generic;
using CarRacer.Core.Items;
using CarRacer.Core.Net;
using CarRacer.Game.Net;
using UnityEngine;

namespace CarRacer.Game.Items
{
    /// <summary>
    /// Shows what items leave on the track (design doc §8): a dark disc for each oil slick, kept in
    /// step with the session's hazards, and a quickly expanding ring where a Shockwave goes off.
    /// </summary>
    public sealed class ItemEffectViews : MonoBehaviour
    {
        private const float ShockwaveSeconds = 0.4f;
        private static readonly Color OilColour = new Color(0.08f, 0.07f, 0.1f);
        private static readonly Color ShockwaveColour = new Color(0.55f, 0.8f, 1f);

        private readonly Dictionary<ushort, Transform> _oil = new Dictionary<ushort, Transform>();
        private readonly HashSet<ushort> _seen = new HashSet<ushort>();
        private readonly List<ushort> _gone = new List<ushort>();
        private readonly List<(Transform Ring, float Age)> _rings = new List<(Transform, float)>();
        private IItemView _items;

        public void Initialise(IItemView items)
        {
            _items = items;
            _items.ItemUsed += OnItemUsed;
        }

        private void OnDestroy()
        {
            if (_items != null)
            {
                _items.ItemUsed -= OnItemUsed;
            }
        }

        private void OnItemUsed(ItemUsedMessage used)
        {
            if (used.Item != ItemKind.Shockwave)
            {
                return;
            }

            var ring = SimpleMaterials.Part(PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.zero, SimpleMaterials.Get(ShockwaveColour));
            ring.position = used.Position.ToUnity() + Vector3.down * 0.5f;
            _rings.Add((ring, 0f));
        }

        private void Update()
        {
            if (_items == null)
            {
                return;
            }

            SyncOil();

            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var (ring, age) = _rings[i];
                age += Time.deltaTime;
                if (age >= ShockwaveSeconds)
                {
                    Destroy(ring.gameObject);
                    _rings.RemoveAt(i);
                    continue;
                }

                float diameter = 2f * ItemEffects.ShockwaveRadius * (age / ShockwaveSeconds);
                ring.localScale = new Vector3(diameter, 0.03f, diameter);
                _rings[i] = (ring, age);
            }
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

            _gone.Clear();
            foreach (var id in _oil.Keys)
            {
                if (!_seen.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (var id in _gone)
            {
                Destroy(_oil[id].gameObject);
                _oil.Remove(id);
            }
        }
    }
}

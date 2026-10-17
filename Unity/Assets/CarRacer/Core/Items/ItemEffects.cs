using System;
using System.Collections.Generic;
using CarRacer.Core.Net;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Items
{
    /// <summary>Where item effects land and how hard they hit (design doc §8). The host decides every hit with these.</summary>
    public static class ItemEffects
    {
        /// <summary>A Shockwave hits every other car whose centre is within this distance of the user.</summary>
        public const float ShockwaveRadius = 9f;

        /// <summary>The change of speed (m/s) a Shockwave gives a car, pushing it straight away from the user.</summary>
        public const float ShockwavePush = 14f;

        /// <summary>The small upward pop every item hit gives, so a hit reads as a hit.</summary>
        public const float HitPop = 3f;

        /// <summary>An Oil slick lands this far behind the car that dropped it.</summary>
        public const float OilDropBehind = 3f;

        /// <summary>A car whose centre comes this close to an oil slick's centre slides on it.</summary>
        public const float OilRadius = 2f;

        /// <summary>An oil slick is harmless for this long after landing, so the dropper is clear of it.</summary>
        public const long OilArmMs = 500;

        public const long OilLifetimeMs = 20000;

        /// <summary>Where an oil slick dropped by a car at <paramref name="carPosition"/> facing <paramref name="headingRadians"/> lands.</summary>
        public static Vec3 OilPosition(Vec3 carPosition, float headingRadians)
        {
            var forward = new Vec3(MathF.Sin(headingRadians), 0f, MathF.Cos(headingRadians));
            return carPosition - forward * OilDropBehind;
        }

        /// <summary>The push a Shockwave from <paramref name="centre"/> gives a car at <paramref name="target"/>: straight away from the centre, plus the pop.</summary>
        public static Vec3 ShockwaveImpulse(Vec3 centre, Vec3 target)
        {
            var away = (target - centre).Flattened;
            var direction = away.LengthSquared > 1e-4f ? away.Normalized : Vec3.Forward;
            return direction * ShockwavePush + Vec3.Up * HitPop;
        }

        /// <summary>The push an oil slick gives: just the pop; the spin-out does the rest.</summary>
        public static Vec3 OilImpulse => Vec3.Up * HitPop;
    }

    /// <summary>A hazard left on the track by an item (an oil slick), in this phone's clock.</summary>
    public struct Hazard
    {
        public ushort EffectId;
        public byte OwnerId;
        public ItemKind Item;
        public Vec3 Position;
        public long ArmedAtMs;
        public long ExpiresAtMs;
    }

    /// <summary>The hazards on the track. Every phone keeps one to draw them; the host's also decides which car each catches.</summary>
    public sealed class HazardField
    {
        private readonly List<Hazard> _hazards = new List<Hazard>();

        public IReadOnlyList<Hazard> Active => _hazards;

        /// <summary>Adds an oil slick for item use <paramref name="used"/>, which happened at <paramref name="usedAtMs"/> in this phone's clock.</summary>
        public void AddOil(ItemUsedMessage used, long usedAtMs)
        {
            Remove(used.EffectId);
            _hazards.Add(new Hazard
            {
                EffectId = used.EffectId,
                OwnerId = used.PlayerId,
                Item = ItemKind.OilSlick,
                Position = ItemEffects.OilPosition(used.Position, used.HeadingRadians),
                ArmedAtMs = usedAtMs + ItemEffects.OilArmMs,
                ExpiresAtMs = usedAtMs + ItemEffects.OilLifetimeMs,
            });
        }

        public bool Remove(ushort effectId)
        {
            for (int i = 0; i < _hazards.Count; i++)
            {
                if (_hazards[i].EffectId == effectId)
                {
                    _hazards.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Drops hazards that have run their time.</summary>
        public void Expire(long nowMs) => _hazards.RemoveAll(h => nowMs >= h.ExpiresAtMs);

        /// <summary>An armed hazard within reach of a car at <paramref name="carPosition"/>, if any.</summary>
        public bool TryFindTouched(Vec3 carPosition, long nowMs, out Hazard hazard)
        {
            foreach (var h in _hazards)
            {
                if (nowMs >= h.ArmedAtMs && nowMs < h.ExpiresAtMs
                    && (h.Position - carPosition).Flattened.LengthSquared <= ItemEffects.OilRadius * ItemEffects.OilRadius)
                {
                    hazard = h;
                    return true;
                }
            }

            hazard = default;
            return false;
        }

        public void Clear() => _hazards.Clear();
    }

    public enum ItemHitOutcome
    {
        /// <summary>Not an item hit: a car-to-car bump, handled as before.</summary>
        NotAnItemHit,

        /// <summary>The shield soaked it up: nothing happens to the car.</summary>
        Blocked,

        /// <summary>The car takes the push and spins out.</summary>
        SpinOut,
    }

    /// <summary>What an item hit from the host does to this phone's car (design doc §8).</summary>
    public static class ItemHitResponse
    {
        public static ItemHitOutcome Resolve(HitMessage hit, Shield shield)
        {
            if (!hit.IsItemHit)
            {
                return ItemHitOutcome.NotAnItemHit;
            }

            return shield.Absorb() ? ItemHitOutcome.Blocked : ItemHitOutcome.SpinOut;
        }
    }

    /// <summary>Where the host sees a player's car: its own, or another phone's predicted position.</summary>
    public delegate bool CarPositionLookup(byte playerId, out Vec3 position);
}

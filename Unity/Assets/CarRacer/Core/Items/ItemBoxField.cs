using System.Collections.Generic;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Items
{
    /// <summary>
    /// The item boxes on a track and which are currently out (design doc §8). Every phone keeps one:
    /// a car's owner uses <see cref="FindTouched"/> to notice it has driven through a box and asks the
    /// host for it; the host grants the first claim on a box that is still up (<see cref="TryTake"/>)
    /// and tells everyone, who mirror it with <see cref="MarkTaken"/>. A taken box comes back after
    /// <see cref="RespawnMs"/>.
    /// </summary>
    public sealed class ItemBoxField
    {
        /// <summary>A car touches a box when its centre comes this close to the box's centre.</summary>
        public const float PickupRadius = 1.6f;

        public const long RespawnMs = 3000;

        private readonly Vec3[] _positions;
        private readonly long[] _respawnAtMs;

        public ItemBoxField(IReadOnlyList<Vec3> positions)
        {
            _positions = new Vec3[positions.Count];
            for (int i = 0; i < _positions.Length; i++)
            {
                _positions[i] = positions[i];
            }

            _respawnAtMs = new long[_positions.Length];
        }

        public int Count => _positions.Length;

        public Vec3 Position(int box) => _positions[box];

        /// <summary>True while the box is up and can be picked up.</summary>
        public bool IsUp(int box, long nowMs) => nowMs >= _respawnAtMs[box];

        /// <summary>The box nearest <paramref name="carPosition"/> that is up and within reach, or -1.</summary>
        public int FindTouched(Vec3 carPosition, long nowMs)
        {
            int best = -1;
            float bestDistance = PickupRadius * PickupRadius;
            for (int i = 0; i < _positions.Length; i++)
            {
                float distance = (_positions[i] - carPosition).LengthSquared;
                if (distance <= bestDistance && IsUp(i, nowMs))
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Host side: grants a claim on <paramref name="box"/> if it is up, taking it down until
        /// <paramref name="respawnAtMs"/>. A second claim on the same box (a car that arrived later)
        /// is refused.
        /// </summary>
        public bool TryTake(int box, long nowMs, out long respawnAtMs)
        {
            respawnAtMs = 0;
            if (box < 0 || box >= _positions.Length || !IsUp(box, nowMs))
            {
                return false;
            }

            respawnAtMs = nowMs + RespawnMs;
            _respawnAtMs[box] = respawnAtMs;
            return true;
        }

        /// <summary>Takes a box down until <paramref name="respawnAtMs"/>, mirroring the host or hiding it locally at once.</summary>
        public void MarkTaken(int box, long respawnAtMs)
        {
            if (box >= 0 && box < _positions.Length && respawnAtMs > _respawnAtMs[box])
            {
                _respawnAtMs[box] = respawnAtMs;
            }
        }

        /// <summary>Puts every box back up, for a new race.</summary>
        public void Reset()
        {
            for (int i = 0; i < _respawnAtMs.Length; i++)
            {
                _respawnAtMs[i] = 0;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using CarRacer.Core.Numerics;
using CarRacer.Core.Track;

namespace CarRacer.Core.Race
{
    /// <summary>
    /// Runs on each phone for its own car (design doc §7.1): watches for the car crossing the next
    /// expected checkpoint gate, forwards, between the road edges and near the road's height, and
    /// reports only that one. The host's <see cref="RaceTracker"/> applies the same ordering rule,
    /// so a report it rejects can't advance anything.
    /// </summary>
    public sealed class CheckpointDetector
    {
        /// <summary>Gates are this much wider than the road on each side, so a car riding the wall still counts.</summary>
        public const float EdgeMargin = 2f;

        /// <summary>
        /// Gates reach this far below and above the road, so a car in the air off a jump still
        /// counts but one crossing on a bridge overhead (a figure-eight) doesn't.
        /// </summary>
        public const float GateBelow = 2f;
        public const float GateAbove = 5f;

        /// <summary>A jump longer than this in one step is a respawn or teleport, not a crossing.</summary>
        public const float MaxStepDistance = 20f;

        private readonly IReadOnlyList<CheckpointGate> _gates;

        public CheckpointDetector(IReadOnlyList<CheckpointGate> gates)
        {
            if (gates == null || gates.Count == 0)
            {
                throw new ArgumentException("A track needs at least one checkpoint.", nameof(gates));
            }

            _gates = gates;
        }

        /// <summary>The gate the car must cross next. Starts at 0, the start line ahead of the grid.</summary>
        public int NextCheckpoint { get; private set; }

        /// <summary>Accepted crossings so far, including the first crossing of the start line.</summary>
        public int CrossingsCount { get; private set; }

        public int LapsCompleted => CrossingsCount == 0 ? 0 : (CrossingsCount - 1) / _gates.Count;

        public void Reset()
        {
            NextCheckpoint = 0;
            CrossingsCount = 0;
        }

        /// <summary>Picks up partway through a race, after <paramref name="crossings"/> accepted crossings (a bot taking over a player's car).</summary>
        public void Resume(int crossings)
        {
            CrossingsCount = Math.Max(0, crossings);
            NextCheckpoint = CrossingsCount % _gates.Count;
        }

        /// <summary>
        /// Call every physics step with the car's previous and current positions. Returns the index
        /// of the checkpoint crossed (to report to the host), or -1.
        /// </summary>
        public int Update(Vec3 from, Vec3 to)
        {
            Vec3 step = (to - from).Flattened;
            if (step.LengthSquared > MaxStepDistance * MaxStepDistance)
            {
                return -1;
            }

            var gate = _gates[NextCheckpoint].Sample;
            Vec3 forward = gate.Forward.Flattened.Normalized;
            Vec3 right = gate.Right.Flattened.Normalized;
            float before = Vec3.Dot((from - gate.Position).Flattened, forward);
            float after = Vec3.Dot((to - gate.Position).Flattened, forward);
            if (!(before < 0f && after >= 0f))
            {
                return -1;
            }

            // Where along the gate line the car crossed it.
            float t = before / (before - after);
            Vec3 crossing = from + (to - from) * t;
            float lateral = Math.Abs(Vec3.Dot((crossing - gate.Position).Flattened, right));
            float height = crossing.Y - gate.Position.Y;
            if (lateral > gate.Width / 2f + EdgeMargin || height < -GateBelow || height > GateAbove)
            {
                return -1;
            }

            int crossed = NextCheckpoint;
            CrossingsCount++;
            NextCheckpoint = (NextCheckpoint + 1) % _gates.Count;
            return crossed;
        }
    }
}

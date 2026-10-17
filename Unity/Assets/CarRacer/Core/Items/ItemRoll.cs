using System;

namespace CarRacer.Core.Items
{
    /// <summary>
    /// Which item a box gives (design doc §8). The odds shift with race position so players further
    /// back get better items: the leader mostly gets defensive ones (Shield, Oil slick), the last
    /// car mostly gets ones that catch up (Boost, Missile, Shockwave). The odds are interpolated
    /// between <see cref="FrontWeights"/> and <see cref="BackWeights"/> by how far back the player is.
    /// </summary>
    public static class ItemRoll
    {
        /// <summary>The items a box can give, in the order of the weight tables.</summary>
        public static readonly ItemKind[] Kinds =
        {
            ItemKind.Boost, ItemKind.Shield, ItemKind.Shockwave, ItemKind.Missile, ItemKind.OilSlick, ItemKind.Bomb,
        };

        /// <summary>Relative odds for the leader.</summary>
        public static readonly float[] FrontWeights = { 15f, 30f, 5f, 0f, 35f, 15f };

        /// <summary>Relative odds for the last car.</summary>
        public static readonly float[] BackWeights = { 35f, 5f, 20f, 30f, 0f, 10f };

        /// <summary>
        /// How far back a player is: 0 for the leader, 1 for last. A lone racer (practice) counts as
        /// mid-pack so every item turns up.
        /// </summary>
        public static float Backness(int position, int racers)
        {
            if (racers <= 1)
            {
                return 0.5f;
            }

            int clamped = Math.Max(1, Math.Min(racers, position));
            return (clamped - 1) / (float)(racers - 1);
        }

        /// <summary>The odds of each of <see cref="Kinds"/>, summing to 1, for a player at <paramref name="backness"/>.</summary>
        public static float[] Odds(float backness)
        {
            float t = Math.Max(0f, Math.Min(1f, backness));
            var odds = new float[Kinds.Length];
            float total = 0f;
            for (int i = 0; i < odds.Length; i++)
            {
                odds[i] = FrontWeights[i] + (BackWeights[i] - FrontWeights[i]) * t;
                total += odds[i];
            }

            for (int i = 0; i < odds.Length; i++)
            {
                odds[i] /= total;
            }

            return odds;
        }

        /// <summary>
        /// Rolls an item for the player in <paramref name="position"/> (1-based) of
        /// <paramref name="racers"/>. <paramref name="random01"/> is a uniform random number in [0, 1).
        /// </summary>
        public static ItemKind Roll(int position, int racers, double random01)
        {
            var odds = Odds(Backness(position, racers));
            double cumulative = 0.0;
            for (int i = 0; i < odds.Length; i++)
            {
                cumulative += odds[i];
                if (random01 < cumulative && odds[i] > 0f)
                {
                    return Kinds[i];
                }
            }

            // Rounding can leave random01 just past the last bucket: give the last item with any odds.
            for (int i = odds.Length - 1; i >= 0; i--)
            {
                if (odds[i] > 0f)
                {
                    return Kinds[i];
                }
            }

            return ItemKind.None;
        }
    }
}

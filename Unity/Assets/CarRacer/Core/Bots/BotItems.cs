using CarRacer.Core.Items;

namespace CarRacer.Core.Bots
{
    /// <summary>What a bot can see when deciding whether to use its item.</summary>
    public struct BotItemSituation
    {
        /// <summary>Distance to the nearest rival in front, or <see cref="float.PositiveInfinity"/>.</summary>
        public float NearestAhead;

        /// <summary>Distance to the nearest rival behind, or <see cref="float.PositiveInfinity"/>.</summary>
        public float NearestBehind;

        /// <summary>The track just ahead is a straight (see <see cref="BotDriver.CurvatureAhead"/>).</summary>
        public bool OnStraight;

        /// <summary>A car is ahead in the standings, so a Missile has something to chase.</summary>
        public bool HasCarAhead;
    }

    /// <summary>
    /// When a bot uses its item (design doc §9: "use items opportunistically"). Each item waits for
    /// its moment: Boost on a straight, Shockwave with a rival close by, Missile when there is a car
    /// ahead, Oil with a rival behind, Bomb with a rival in front. Nothing is used before the bot's
    /// skill delay, and anything held too long is used anyway.
    /// </summary>
    public static class BotItems
    {
        public const float ShockwaveRange = 7f;
        public const float OilRange = 20f;
        public const float BombRange = 25f;
        public const float MissileRange = 60f;

        /// <summary>A bot that has held an item this long uses it whatever the situation.</summary>
        public const float GiveUpSeconds = 10f;

        public static bool ShouldUse(ItemKind held, float heldSeconds, BotSkill skill, BotItemSituation situation)
        {
            if (held == ItemKind.None || heldSeconds < skill.ItemDelaySeconds)
            {
                return false;
            }

            if (heldSeconds >= GiveUpSeconds)
            {
                return true;
            }

            switch (held)
            {
                case ItemKind.Boost:
                    return situation.OnStraight;
                case ItemKind.Shield:
                    return true;
                case ItemKind.Shockwave:
                    return situation.NearestAhead <= ShockwaveRange || situation.NearestBehind <= ShockwaveRange;
                case ItemKind.Missile:
                    return situation.HasCarAhead && situation.NearestAhead <= MissileRange;
                case ItemKind.OilSlick:
                    return situation.NearestBehind <= OilRange;
                case ItemKind.Bomb:
                    return situation.NearestAhead <= BombRange;
                default:
                    return false;
            }
        }
    }
}

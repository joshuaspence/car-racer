namespace CarRacer.Core.Items
{
    /// <summary>The nine items (design doc §8). The values travel in network messages, so don't reorder them.</summary>
    public enum ItemKind : byte
    {
        None = 0,

        /// <summary>A burst of speed, applied at once by the owner.</summary>
        Boost = 1,

        /// <summary>Blocks the next item hit for a while, applied at once by the owner.</summary>
        Shield = 2,

        /// <summary>A radial bump around the user.</summary>
        Shockwave = 3,

        /// <summary>A projectile that chases the car ahead.</summary>
        Missile = 4,

        /// <summary>A hazard dropped behind the car.</summary>
        OilSlick = 5,

        /// <summary>A lobbed projectile that bounces and explodes.</summary>
        Bomb = 6,

        /// <summary>Strikes every car ahead in the standings at once.</summary>
        Lightning = 7,

        /// <summary>A hazard dropped behind the car that looks like an item box.</summary>
        FakeBox = 8,

        /// <summary>A fast projectile that hunts down the race leader.</summary>
        Seeker = 9,
    }

    public static class ItemNames
    {
        /// <summary>The short label shown on the Item button and in the HUD slot; "ITEM" when the slot is empty.</summary>
        public static string Label(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Boost:
                    return "BOOST";
                case ItemKind.Shield:
                    return "SHIELD";
                case ItemKind.Shockwave:
                    return "SHOCK";
                case ItemKind.Missile:
                    return "MISSILE";
                case ItemKind.OilSlick:
                    return "OIL";
                case ItemKind.Bomb:
                    return "BOMB";
                case ItemKind.Lightning:
                    return "BOLT";
                case ItemKind.FakeBox:
                    return "FAKE";
                case ItemKind.Seeker:
                    return "SEEKER";
                default:
                    return "ITEM";
            }
        }
    }
}

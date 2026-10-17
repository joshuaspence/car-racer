namespace CarRacer.Core.Items
{
    /// <summary>The six items (design doc §8). The values travel in network messages, so don't reorder them.</summary>
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
    }
}

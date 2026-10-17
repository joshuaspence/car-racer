namespace CarRacer.Core.Items
{
    /// <summary>
    /// The Shield item on this phone's car (design doc §8): up for <see cref="Seconds"/>, and it
    /// soaks up the first item hit in that time, which also drops it.
    /// </summary>
    public sealed class Shield
    {
        public const float Seconds = 8f;

        public float SecondsLeft { get; private set; }

        public bool IsUp => SecondsLeft > 0f;

        public void Raise() => SecondsLeft = Seconds;

        public void Tick(float deltaSeconds)
        {
            SecondsLeft = SecondsLeft > deltaSeconds ? SecondsLeft - deltaSeconds : 0f;
        }

        /// <summary>An item hit arrived: true if the shield took it (and is now down), false if the car takes it.</summary>
        public bool Absorb()
        {
            if (!IsUp)
            {
                return false;
            }

            SecondsLeft = 0f;
            return true;
        }

        /// <summary>Drops the shield, for a new race.</summary>
        public void Clear() => SecondsLeft = 0f;
    }
}

namespace CarRacer.Core.Race
{
    /// <summary>
    /// Shows "Wrong way!" when a moving car has faced against the track direction for a
    /// while, with hysteresis so a spin-out or a bump doesn't flash the warning.
    /// </summary>
    public sealed class WrongWayDetector
    {
        public float EnterAlignment = -0.3f;
        public float ExitAlignment = 0.2f;
        public float MinSpeed = 2f;
        public float EnterSeconds = 1.0f;
        public float ExitSeconds = 0.3f;

        private float _timer;

        public bool IsWrongWay { get; private set; }

        /// <summary>
        /// <paramref name="alignment"/> is dot(car forward, track tangent), both normalised
        /// on the ground plane: 1 = racing direction, -1 = backwards.
        /// </summary>
        public bool Update(float alignment, float speed, float deltaSeconds)
        {
            bool condition = IsWrongWay
                ? alignment > ExitAlignment
                : alignment < EnterAlignment && speed >= MinSpeed;

            if (!condition)
            {
                _timer = 0f;
                return IsWrongWay;
            }

            _timer += deltaSeconds;
            if (_timer >= (IsWrongWay ? ExitSeconds : EnterSeconds))
            {
                IsWrongWay = !IsWrongWay;
                _timer = 0f;
            }

            return IsWrongWay;
        }

        public void Reset()
        {
            IsWrongWay = false;
            _timer = 0f;
        }
    }
}

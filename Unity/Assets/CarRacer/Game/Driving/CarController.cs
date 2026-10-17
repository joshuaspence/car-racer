using CarRacer.Core.Driving;
using UnityEngine;

namespace CarRacer.Game.Driving
{
    /// <summary>Supplies the driver's input each physics step (the local player, a bot, or a test).</summary>
    public interface ICarInputSource
    {
        CarInput Read();
    }

    /// <summary>
    /// The sphere-based car (design doc §4). A frictionless, non-rotating sphere rigidbody carries
    /// the car; every fixed step its horizontal velocity goes through <see cref="CarModel"/> and
    /// back, while gravity and collisions stay with the physics engine. The visible body is a child
    /// that turns to the car's heading and leans for show.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarController : MonoBehaviour
    {
        public const float SphereRadius = 0.6f;

        /// <summary>Extra downward pull on top of normal gravity, so the car feels planted and lands quickly.</summary>
        public const float ExtraGravity = 12f;

        /// <summary>How far below the sphere the ground can be and still count as touching it.</summary>
        public const float GroundProbe = 0.25f;

        /// <summary>A car that falls below this height is put back where it started.</summary>
        public const float FallLimit = -20f;

        private const float DriftVisualYawDegrees = 22f;
        private const float MaxLeanDegrees = 5f;
        private const float VisualSharpness = 10f;

        public CarTuning Tuning = new CarTuning();
        public ICarInputSource InputSource;

        /// <summary>While true the driver's input is ignored, e.g. on the grid before the start.</summary>
        public bool InputLocked;

        private Rigidbody _body;
        private Transform _visual;
        private Vector3 _pendingImpulse;
        private CarState _state;
        private Vector3 _spawnPosition;
        private float _spawnHeading;
        private float _visualDriftYaw;
        private float _visualLean;

        public CarState State => _state;

        /// <summary>The physics sphere's centre and velocity, as sent to other phones.</summary>
        public Vector3 BodyPosition => _body.position;
        public Vector3 BodyVelocity => _body.linearVelocity;
        public bool Grounded { get; private set; }
        public CarInput LastInput { get; private set; }

        /// <summary>The car was pushed by <see cref="AddImpulse"/> (a bump or a hit).</summary>
        public event System.Action<Vector3> Pushed;

        /// <summary>A drift released a boost of this tier (1..3).</summary>
        public event System.Action<int> BoostReleased;

        /// <summary>Speed along the car's heading in m/s; negative when reversing.</summary>
        public float ForwardSpeed => _body == null ? 0f : Vector3.Dot(_body.linearVelocity, _state.Forward.ToUnity());

        public void Initialise(Transform visual)
        {
            _body = GetComponent<Rigidbody>();
            _visual = visual;
        }

        /// <summary>Puts the car at rest at <paramref name="position"/> facing <paramref name="heading"/> (radians, 0 = +Z).</summary>
        public void Place(Vector3 position, float heading)
        {
            _spawnPosition = position;
            _spawnHeading = heading;
            Respawn();
        }

        /// <summary>
        /// Pushes the car (mass × Δv), e.g. a car-to-car bump. Applied at the start of the next
        /// physics step, before the handling model, so grip then straightens the car up.
        /// </summary>
        public void AddImpulse(Vector3 impulse)
        {
            _pendingImpulse += impulse;
            Pushed?.Invoke(impulse);
        }

        /// <summary>Starts a boost (the Boost item) of <paramref name="speed"/> extra top speed for <paramref name="seconds"/>.</summary>
        public void Boost(float speed, float seconds)
        {
            _state = CarModel.Boost(_state, speed, seconds);
        }

        private void Respawn()
        {
            _pendingImpulse = Vector3.zero;
            _body.position = _spawnPosition;
            transform.position = _spawnPosition;
            _body.linearVelocity = Vector3.zero;
            _state = new CarState { Heading = _spawnHeading };
            _visualDriftYaw = 0f;
            _visualLean = 0f;
            UpdateVisual(0f);
        }

        private void FixedUpdate()
        {
            Grounded = Physics.Raycast(_body.position, Vector3.down, SphereRadius + GroundProbe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            LastInput = InputLocked ? default : InputSource?.Read() ?? default;

            Vector3 velocity = _body.linearVelocity + _pendingImpulse / _body.mass;
            _pendingImpulse = Vector3.zero;
            _state.Velocity = velocity.ToCore();
            var before = _state;
            _state = CarModel.Step(_state, LastInput, Grounded, Time.fixedDeltaTime, Tuning);
            int boostTier = HapticPatterns.ReleasedBoostTier(before, _state, Time.fixedDeltaTime, Tuning);
            if (boostTier > 0)
            {
                BoostReleased?.Invoke(boostTier);
            }

            _body.linearVelocity = new Vector3(_state.Velocity.X, velocity.y, _state.Velocity.Z);
            _body.AddForce(Vector3.down * ExtraGravity, ForceMode.Acceleration);

            if (_body.position.y < FallLimit)
            {
                Respawn();
            }
        }

        private void Update()
        {
            UpdateVisual(Time.deltaTime);
        }

        private void UpdateVisual(float dt)
        {
            if (_visual == null)
            {
                return;
            }

            // Kart-style drift: the body swings out into the slide. It also leans out of turns.
            float blend = dt > 0f ? 1f - Mathf.Exp(-VisualSharpness * dt) : 1f;
            float targetDriftYaw = _state.Drifting ? _state.DriftDirection * DriftVisualYawDegrees : 0f;
            float speedFraction = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / Tuning.MaxSpeed);
            float targetLean = -LastInput.Steer * speedFraction * MaxLeanDegrees;
            _visualDriftYaw = Mathf.Lerp(_visualDriftYaw, targetDriftYaw, blend);
            _visualLean = Mathf.Lerp(_visualLean, targetLean, blend);

            _visual.rotation = Quaternion.Euler(0f, _state.Heading * Mathf.Rad2Deg + _visualDriftYaw, _visualLean);
        }
    }
}

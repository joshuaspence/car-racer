using System;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Driving
{
    /// <summary>One physics step's worth of driver input.</summary>
    public struct CarInput
    {
        /// <summary>-1 (full left) to 1 (full right).</summary>
        public float Steer;

        /// <summary>-1 (brake, then reverse) to 1 (full throttle).</summary>
        public float Throttle;

        public bool Drift;

        public CarInput(float steer, float throttle, bool drift = false)
        {
            Steer = steer;
            Throttle = throttle;
            Drift = drift;
        }
    }

    /// <summary>Handling numbers. Defaults are a starting point for tuning on real phones.</summary>
    [Serializable]
    public sealed class CarTuning
    {
        public float MaxSpeed = 26f;
        public float MaxReverseSpeed = 8f;
        public float Acceleration = 16f;
        public float Braking = 32f;

        /// <summary>Deceleration with no throttle, and when above top speed (e.g. after a boost).</summary>
        public float Coasting = 5f;

        /// <summary>Peak turn rate, reached at <see cref="FullSteerSpeed"/>.</summary>
        public float SteerDegreesPerSecond = 150f;

        /// <summary>Below this speed turning scales down, so the car can't spin on the spot.</summary>
        public float FullSteerSpeed = 6f;

        /// <summary>Fraction of the peak turn rate left at top speed (high-speed stability).</summary>
        public float TopSpeedSteerFactor = 0.6f;

        /// <summary>How quickly sideways sliding is removed (per second). Higher is grippier.</summary>
        public float Grip = 10f;

        public float DriftGrip = 2.5f;

        /// <summary>While drifting the car always turns into the drift; steering varies how tightly, between these fractions of full lock.</summary>
        public float DriftMinTurn = 0.35f;
        public float DriftMaxTurn = 1.25f;

        public float DriftMinSpeed = 9f;
        public float DriftMinSteer = 0.3f;

        /// <summary>Seconds of drifting needed for each boost tier (ascending).</summary>
        public float[] DriftTierSeconds = { 0.7f, 1.4f, 2.2f };

        /// <summary>Extra top speed for each boost tier, in m/s.</summary>
        public float[] BoostSpeeds = { 5f, 8f, 11f };

        /// <summary>Duration of each boost tier, in seconds.</summary>
        public float[] BoostDurations = { 0.6f, 1.0f, 1.5f };

        public float BoostAcceleration = 30f;
    }

    /// <summary>The handling state that persists between steps (and is sent over the network as flags).</summary>
    public struct CarState
    {
        /// <summary>Facing direction in radians: 0 faces +Z, π/2 faces +X (clockwise from above, like Unity's yaw).</summary>
        public float Heading;

        /// <summary>Horizontal velocity in m/s (Y is ignored; gravity is the physics engine's job).</summary>
        public Vec3 Velocity;

        public bool Drifting;

        /// <summary>-1 for a left drift, 1 for a right drift, 0 when not drifting.</summary>
        public int DriftDirection;

        public float DriftSeconds;
        public float BoostSecondsLeft;
        public float BoostSpeed;

        public Vec3 Forward => new Vec3(MathF.Sin(Heading), 0f, MathF.Cos(Heading));

        /// <summary>Boost tier the current drift would release: 0 for none, then 1..3.</summary>
        public int DriftTier(CarTuning tuning)
        {
            if (!Drifting)
            {
                return 0;
            }

            int tier = 0;
            for (int i = 0; i < tuning.DriftTierSeconds.Length; i++)
            {
                if (DriftSeconds >= tuning.DriftTierSeconds[i])
                {
                    tier = i + 1;
                }
            }

            return tier;
        }
    }

    /// <summary>
    /// Arcade handling for the sphere-based car (design doc §4). Each fixed physics step the Unity
    /// layer feeds in the rigidbody's current horizontal velocity, applies the returned velocity
    /// and heading, and leaves vertical motion to gravity. Velocity is read back every step, so
    /// bumps and collisions change it naturally and grip then pulls the car straight again.
    /// </summary>
    public static class CarModel
    {
        public static CarState Step(CarState state, CarInput input, bool grounded, float deltaSeconds, CarTuning tuning)
        {
            float steer = Clamp(input.Steer, -1f, 1f);
            float throttle = Clamp(input.Throttle, -1f, 1f);
            Vec3 velocity = state.Velocity.Flattened;

            state.BoostSecondsLeft = Math.Max(0f, state.BoostSecondsLeft - deltaSeconds);
            if (state.BoostSecondsLeft <= 0f)
            {
                state.BoostSpeed = 0f;
            }

            if (!grounded)
            {
                // No traction in the air: keep momentum and heading; a drift in progress is lost.
                EndDrift(ref state, tuning, releaseBoost: false);
                state.Velocity = velocity;
                return state;
            }

            Vec3 forward = state.Forward;
            Vec3 right = new Vec3(forward.Z, 0f, -forward.X);
            float forwardSpeed = Vec3.Dot(velocity, forward);
            float sideSpeed = Vec3.Dot(velocity, right);

            UpdateDrift(ref state, input.Drift, steer, forwardSpeed, deltaSeconds, tuning);

            forwardSpeed = ApplyThrottle(forwardSpeed, throttle, state, deltaSeconds, tuning);

            float turn = state.Drifting
                ? state.DriftDirection * Lerp(tuning.DriftMinTurn, tuning.DriftMaxTurn, (steer * state.DriftDirection + 1f) / 2f)
                : steer;
            float yawRate = turn * tuning.SteerDegreesPerSecond * (MathF.PI / 180f) * SteerFactor(forwardSpeed, tuning);
            state.Heading = WrapAngle(state.Heading + yawRate * Math.Sign(forwardSpeed) * deltaSeconds);

            float grip = state.Drifting ? tuning.DriftGrip : tuning.Grip;
            sideSpeed *= MathF.Exp(-grip * deltaSeconds);

            // Re-express the kept momentum along the new heading so the car carves rather than skids.
            Vec3 newForward = state.Forward;
            Vec3 newRight = new Vec3(newForward.Z, 0f, -newForward.X);
            state.Velocity = newForward * forwardSpeed + newRight * sideSpeed;
            return state;
        }

        private static float ApplyThrottle(float speed, float throttle, CarState state, float dt, CarTuning t)
        {
            float topSpeed = t.MaxSpeed + state.BoostSpeed;
            if (state.BoostSecondsLeft > 0f)
            {
                return Math.Min(topSpeed, speed + t.BoostAcceleration * dt);
            }

            if (speed > topSpeed)
            {
                return Math.Max(topSpeed, speed - t.Coasting * 2f * dt);
            }

            if (throttle > 0f)
            {
                if (speed < 0f)
                {
                    return Math.Min(0f, speed + t.Braking * throttle * dt);
                }

                return Math.Min(topSpeed, speed + t.Acceleration * throttle * dt);
            }

            if (throttle < 0f)
            {
                if (speed > 0f)
                {
                    return Math.Max(0f, speed + t.Braking * throttle * dt);
                }

                return Math.Max(-t.MaxReverseSpeed, speed + t.Acceleration * 0.6f * throttle * dt);
            }

            return speed > 0f
                ? Math.Max(0f, speed - t.Coasting * dt)
                : Math.Min(0f, speed + t.Coasting * dt);
        }

        private static void UpdateDrift(ref CarState state, bool driftHeld, float steer, float forwardSpeed, float dt, CarTuning t)
        {
            if (state.Drifting)
            {
                if (!driftHeld)
                {
                    EndDrift(ref state, t, releaseBoost: true);
                }
                else if (forwardSpeed < t.DriftMinSpeed * 0.6f)
                {
                    EndDrift(ref state, t, releaseBoost: false);
                }
                else
                {
                    state.DriftSeconds += dt;
                }

                return;
            }

            if (driftHeld && forwardSpeed >= t.DriftMinSpeed && Math.Abs(steer) >= t.DriftMinSteer)
            {
                state.Drifting = true;
                state.DriftDirection = Math.Sign(steer);
                state.DriftSeconds = 0f;
            }
        }

        private static void EndDrift(ref CarState state, CarTuning t, bool releaseBoost)
        {
            if (state.Drifting && releaseBoost)
            {
                int tier = state.DriftTier(t);
                if (tier > 0)
                {
                    state.BoostSpeed = t.BoostSpeeds[tier - 1];
                    state.BoostSecondsLeft = t.BoostDurations[tier - 1];
                }
            }

            state.Drifting = false;
            state.DriftDirection = 0;
            state.DriftSeconds = 0f;
        }

        /// <summary>0 at a standstill, rising to 1 at <see cref="CarTuning.FullSteerSpeed"/>, easing to the top-speed factor.</summary>
        private static float SteerFactor(float forwardSpeed, CarTuning t)
        {
            float speed = Math.Abs(forwardSpeed);
            if (speed < t.FullSteerSpeed)
            {
                return speed / t.FullSteerSpeed;
            }

            float fast = Clamp((speed - t.FullSteerSpeed) / Math.Max(1f, t.MaxSpeed - t.FullSteerSpeed), 0f, 1f);
            return Lerp(1f, t.TopSpeedSteerFactor, fast);
        }

        private static float WrapAngle(float radians)
        {
            const float twoPi = MathF.PI * 2f;
            radians %= twoPi;
            return radians < 0f ? radians + twoPi : radians;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}

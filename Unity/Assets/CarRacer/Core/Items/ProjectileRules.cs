using System;
using CarRacer.Core.Net;
using CarRacer.Core.Numerics;

namespace CarRacer.Core.Items
{
    /// <summary>A Missile or Bomb in flight. Only the host simulates them; other phones draw the positions it sends.</summary>
    public struct Projectile
    {
        public ushort EffectId;
        public byte OwnerId;
        public ItemKind Kind;
        public Vec3 Position;
        public Vec3 Velocity;

        /// <summary>The player a Missile chases, or <see cref="ProjectileRules.NoTarget"/>.</summary>
        public byte TargetId;

        /// <summary>The height a Bomb bounces on (the ground under its launch) and a Missile flies at.</summary>
        public float GroundY;

        public long LaunchedAtMs;
        public long ExpiresAtMs;
    }

    /// <summary>How Missiles and Bombs fly and hit (design doc §8).</summary>
    public static class ProjectileRules
    {
        public const byte NoTarget = byte.MaxValue;

        public const float MissileSpeed = 40f;
        public const float MissileTurnDegreesPerSecond = 220f;
        public const long MissileLifetimeMs = 5000;

        /// <summary>A Missile starts this far ahead of the car that fired it.</summary>
        public const float MissileLaunchAhead = 2.5f;

        /// <summary>A Missile's push along its flight, plus the usual pop.</summary>
        public const float MissilePush = 10f;

        /// <summary>A Bomb is thrown this fast forwards (on top of the car's own speed) and upwards.</summary>
        public const float BombThrowSpeed = 14f;
        public const float BombThrowUp = 9f;
        public const float Gravity = 25f;

        /// <summary>The share of a Bomb's falling speed kept when it bounces, and of its rolling speed.</summary>
        public const float BombBounce = 0.5f;
        public const float BombRoll = 0.8f;

        public const long BombFuseMs = 2500;

        /// <summary>A Bomb ignores the car that threw it for this long, so it doesn't go off in their face.</summary>
        public const long BombSafeMs = 500;

        public const float BombBlastRadius = 6f;
        public const float BombPush = 12f;

        /// <summary>A projectile meets a car when their centres come this close.</summary>
        public const float HitRadius = 1.6f;

        public static Projectile LaunchMissile(ItemUsedMessage used, byte targetId, long nowMs)
        {
            var forward = Forward(used.HeadingRadians);
            return new Projectile
            {
                EffectId = used.EffectId,
                OwnerId = used.PlayerId,
                Kind = ItemKind.Missile,
                Position = used.Position + forward * MissileLaunchAhead,
                Velocity = forward * MissileSpeed,
                TargetId = targetId,
                GroundY = used.Position.Y,
                LaunchedAtMs = nowMs,
                ExpiresAtMs = nowMs + MissileLifetimeMs,
            };
        }

        public static Projectile LaunchBomb(ItemUsedMessage used, long nowMs)
        {
            var forward = Forward(used.HeadingRadians);
            return new Projectile
            {
                EffectId = used.EffectId,
                OwnerId = used.PlayerId,
                Kind = ItemKind.Bomb,
                Position = used.Position,
                Velocity = used.Velocity.Flattened + forward * BombThrowSpeed + Vec3.Up * BombThrowUp,
                TargetId = NoTarget,
                GroundY = used.Position.Y,
                LaunchedAtMs = nowMs,
                ExpiresAtMs = nowMs + BombFuseMs,
            };
        }

        /// <summary>
        /// Moves a projectile on by <paramref name="deltaSeconds"/>. A Missile with a target turns
        /// towards it (no faster than its turn rate) and skims at its launch height; a Bomb falls
        /// and bounces on the ground it was thrown from.
        /// </summary>
        public static Projectile Step(Projectile p, float deltaSeconds, bool hasTarget, Vec3 targetPosition)
        {
            if (p.Kind == ItemKind.Missile)
            {
                var direction = p.Velocity.Flattened.Normalized;
                if (hasTarget)
                {
                    var toTarget = (targetPosition - p.Position).Flattened;
                    if (toTarget.LengthSquared > 1e-4f)
                    {
                        float turn = WrapAngle(Heading(toTarget) - Heading(direction));
                        float maxTurn = MissileTurnDegreesPerSecond * (MathF.PI / 180f) * deltaSeconds;
                        direction = Vec3.RotateAroundUp(direction, Math.Max(-maxTurn, Math.Min(maxTurn, turn)));
                    }
                }

                p.Velocity = direction * MissileSpeed;
                p.Position = new Vec3(p.Position.X, p.GroundY, p.Position.Z) + p.Velocity * deltaSeconds;
                return p;
            }

            var velocity = p.Velocity - Vec3.Up * (Gravity * deltaSeconds);
            var position = p.Position + velocity * deltaSeconds;
            if (position.Y < p.GroundY)
            {
                position.Y = p.GroundY;
                velocity = new Vec3(velocity.X * BombRoll, -velocity.Y * BombBounce, velocity.Z * BombRoll);
            }

            p.Position = position;
            p.Velocity = velocity;
            return p;
        }

        /// <summary>A Missile's push on the car it hits: along its flight, plus the pop.</summary>
        public static Vec3 MissileImpulse(Projectile missile) =>
            missile.Velocity.Flattened.Normalized * MissilePush + Vec3.Up * ItemEffects.HitPop;

        /// <summary>A Bomb blast's push on a car at <paramref name="target"/>: straight away from the blast, plus the pop.</summary>
        public static Vec3 BlastImpulse(Vec3 blast, Vec3 target)
        {
            var away = (target - blast).Flattened;
            var direction = away.LengthSquared > 1e-4f ? away.Normalized : Vec3.Forward;
            return direction * BombPush + Vec3.Up * ItemEffects.HitPop;
        }

        private static Vec3 Forward(float headingRadians) => new Vec3(MathF.Sin(headingRadians), 0f, MathF.Cos(headingRadians));

        private static float Heading(Vec3 v) => MathF.Atan2(v.X, v.Z);

        private static float WrapAngle(float radians)
        {
            while (radians > MathF.PI)
            {
                radians -= 2f * MathF.PI;
            }

            while (radians < -MathF.PI)
            {
                radians += 2f * MathF.PI;
            }

            return radians;
        }
    }
}

using System;
using System.Collections.Generic;
using CarRacer.Core.Items;
using CarRacer.Core.Net;
using CarRacer.Core.Numerics;
using CarRacer.Core.Race;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class HudIndicatorTests
    {
        private static readonly Vec3 Me = new Vec3(10f, 0f, 10f);

        [Theory]
        [InlineData(0f, 0f, 10f, 0f)]          // facing +Z, car ahead
        [InlineData(0f, 10f, 0f, 90f)]         // facing +Z, car to the right (+X)
        [InlineData(0f, -10f, 0f, -90f)]       // to the left
        [InlineData(0f, 0f, -10f, 180f)]       // behind
        [InlineData(90f, 10f, 0f, 0f)]         // facing +X, car ahead
        [InlineData(90f, 0f, 10f, -90f)]       // facing +X, +Z is on the left
        public void BearingIsRelativeToTheWayTheCarFaces(float headingDegrees, float dx, float dz, float expectedDegrees)
        {
            float bearing = HudIndicators.Bearing(Me, Radians(headingDegrees), Me + new Vec3(dx, 3f, dz));
            Assert.Equal(expectedDegrees, MathF.Abs(Degrees(bearing)) > 179f ? 180f : Degrees(bearing), 2);
        }

        [Fact]
        public void OnlyCarsInRangeAndOutOfViewGetArrows()
        {
            var rivals = new List<Vec3>
            {
                Me + new Vec3(0f, 0f, 10f),   // ahead: on screen
                Me + new Vec3(0f, 0f, -10f),  // behind
                Me + new Vec3(8f, 0f, 1f),    // beside, on the right
                Me + new Vec3(0f, 0f, -40f),  // behind, but too far
            };
            var arrows = new List<EdgeIndicator>();
            HudIndicators.Rivals(Me, 0f, rivals, arrows);

            Assert.Equal(new[] { 1, 2 }, arrows.ConvertAll(a => a.Index));
            Assert.True(arrows[1].Bearing > 0f);
        }

        [Fact]
        public void ArrowsGrowAsCarsCloseIn()
        {
            Assert.Equal(0f, HudIndicators.Closeness(HudIndicators.Range));
            Assert.Equal(1f, HudIndicators.Closeness(HudIndicators.NearDistance));
            Assert.Equal(1f, HudIndicators.Closeness(0f));
            Assert.True(HudIndicators.Closeness(8f) > HudIndicators.Closeness(15f));
        }

        [Theory]
        [InlineData(180f, 640f, 700f)]   // behind: bottom centre
        [InlineData(90f, 1260f, 360f)]   // right: right centre
        [InlineData(-90f, 20f, 360f)]    // left: left centre
        [InlineData(0f, 640f, 20f)]      // ahead: top centre
        public void ArrowsSitOnTheScreenEdge(float bearingDegrees, float x, float y)
        {
            var point = HudIndicators.EdgePoint(Radians(bearingDegrees), 1280f, 720f, 20f);
            Assert.Equal(x, point.X, 1);
            Assert.Equal(y, point.Y, 1);
        }

        [Fact]
        public void ADiagonalArrowMeetsTheNearerEdge()
        {
            // Behind and to the right of a wide screen: the bottom edge comes first.
            var point = HudIndicators.EdgePoint(Radians(135f), 1280f, 720f, 20f);
            Assert.Equal(700f, point.Y, 1);
            Assert.Equal(640f + 340f, point.X, 1);
        }

        [Fact]
        public void WarnsOfTheNearestMissileChasingYou()
        {
            var projectiles = new List<ProjectileSnapshot>
            {
                new ProjectileSnapshot { EffectId = 1, Kind = ItemKind.Missile, Position = Me + new Vec3(0f, 0f, -50f), TargetId = 2 },
                new ProjectileSnapshot { EffectId = 2, Kind = ItemKind.Missile, Position = Me + new Vec3(0f, 0f, -20f), TargetId = 2 },
                new ProjectileSnapshot { EffectId = 3, Kind = ItemKind.Missile, Position = Me + new Vec3(0f, 0f, -5f), TargetId = 4 },
                new ProjectileSnapshot { EffectId = 4, Kind = ItemKind.Bomb, Position = Me, TargetId = ProjectileRules.NoTarget },
            };

            Assert.True(HudIndicators.TryIncomingMissile(projectiles, 2, Me, out var missile, out float distance));
            Assert.Equal(2, missile.EffectId);
            Assert.Equal(20f, distance, 3);
        }

        [Fact]
        public void ASeekerChasingThisCarWarnsLikeAMissile()
        {
            var projectiles = new List<ProjectileSnapshot>
            {
                new ProjectileSnapshot { EffectId = 8, Kind = ItemKind.Seeker, Position = Me + new Vec3(0f, 0f, -30f), TargetId = 2 },
            };

            Assert.True(HudIndicators.TryIncomingMissile(projectiles, 2, Me, out var seeker, out _));
            Assert.Equal(8, seeker.EffectId);
        }

        [Fact]
        public void NoWarningForMissilesFarAwayOrChasingOthers()
        {
            var projectiles = new List<ProjectileSnapshot>
            {
                new ProjectileSnapshot { Kind = ItemKind.Missile, Position = Me + new Vec3(0f, 0f, -200f), TargetId = 2 },
                new ProjectileSnapshot { Kind = ItemKind.Missile, Position = Me, TargetId = 4 },
            };

            Assert.False(HudIndicators.TryIncomingMissile(projectiles, 2, Me, out _, out _));
        }

        private static float Radians(float degrees) => degrees * MathF.PI / 180f;

        private static float Degrees(float radians) => radians * 180f / MathF.PI;
    }
}

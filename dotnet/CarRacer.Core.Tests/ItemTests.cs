using System.Collections.Generic;
using CarRacer.Core.Items;
using CarRacer.Core.Numerics;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class ItemRollTests
    {
        private static float OddsOf(ItemKind kind, float backness) =>
            ItemRoll.Odds(backness)[System.Array.IndexOf(ItemRoll.Kinds, kind)];

        [Theory]
        [InlineData(1, 6, 0f)]
        [InlineData(6, 6, 1f)]
        [InlineData(2, 3, 0.5f)]
        [InlineData(1, 1, 0.5f)]
        [InlineData(9, 4, 1f)]
        [InlineData(0, 4, 0f)]
        public void BacknessRunsFromLeaderToLast(int position, int racers, float expected)
        {
            Assert.Equal(expected, ItemRoll.Backness(position, racers), 4);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.3f)]
        [InlineData(1f)]
        public void OddsSumToOne(float backness)
        {
            float total = 0f;
            foreach (float p in ItemRoll.Odds(backness))
            {
                Assert.True(p >= 0f);
                total += p;
            }

            Assert.Equal(1f, total, 4);
        }

        [Fact]
        public void TheLeaderNeverGetsAMissileAndLastNeverGetsOil()
        {
            Assert.Equal(0f, OddsOf(ItemKind.Missile, 0f));
            Assert.Equal(0f, OddsOf(ItemKind.OilSlick, 1f));
        }

        [Fact]
        public void CatchUpItemsGetLikelierFurtherBack()
        {
            foreach (var kind in new[] { ItemKind.Boost, ItemKind.Missile, ItemKind.Shockwave })
            {
                Assert.True(OddsOf(kind, 1f) > OddsOf(kind, 0.5f), kind.ToString());
                Assert.True(OddsOf(kind, 0.5f) > OddsOf(kind, 0f), kind.ToString());
            }

            foreach (var kind in new[] { ItemKind.Shield, ItemKind.OilSlick })
            {
                Assert.True(OddsOf(kind, 0f) > OddsOf(kind, 1f), kind.ToString());
            }
        }

        [Fact]
        public void RollsFollowTheOdds()
        {
            // Sweeping random01 evenly across [0, 1) hands out each item in proportion to its odds.
            const int samples = 10000;
            var counts = new Dictionary<ItemKind, int>();
            for (int i = 0; i < samples; i++)
            {
                var kind = ItemRoll.Roll(3, 5, (i + 0.5) / samples);
                counts[kind] = counts.TryGetValue(kind, out int n) ? n + 1 : 1;
            }

            var odds = ItemRoll.Odds(0.5f);
            for (int i = 0; i < ItemRoll.Kinds.Length; i++)
            {
                counts.TryGetValue(ItemRoll.Kinds[i], out int n);
                Assert.Equal(odds[i], n / (float)samples, 2);
            }
        }

        [Fact]
        public void TheEdgesOfTheRangeStillGiveAnItemWithOdds()
        {
            Assert.Equal(ItemKind.Boost, ItemRoll.Roll(1, 6, 0.0));
            Assert.Equal(ItemKind.Bomb, ItemRoll.Roll(1, 6, 0.9999999));
            Assert.NotEqual(ItemKind.OilSlick, ItemRoll.Roll(6, 6, 0.9999999));
            Assert.Equal(ItemKind.Bomb, ItemRoll.Roll(6, 6, 1.0));
        }

        [Fact]
        public void TheLeaderNeverRollsAMissile()
        {
            for (int i = 0; i < 1000; i++)
            {
                Assert.NotEqual(ItemKind.Missile, ItemRoll.Roll(1, 4, i / 1000.0));
            }
        }
    }

    public class ItemBoxFieldTests
    {
        private static ItemBoxField Field() => new ItemBoxField(new List<Vec3>
        {
            new Vec3(0f, 0.75f, 10f),
            new Vec3(3f, 0.75f, 10f),
        });

        [Fact]
        public void ACarTouchesTheNearestBoxInReach()
        {
            var field = Field();
            Assert.Equal(0, field.FindTouched(new Vec3(0.5f, 0.5f, 10f), 0));
            Assert.Equal(1, field.FindTouched(new Vec3(2.0f, 0.5f, 10f), 0));
            Assert.Equal(-1, field.FindTouched(new Vec3(0f, 0.5f, 5f), 0));
        }

        [Fact]
        public void TheFirstClaimWinsAndTheBoxComesBack()
        {
            var field = Field();
            Assert.True(field.TryTake(0, 1000, out long respawnAt));
            Assert.Equal(1000 + ItemBoxField.RespawnMs, respawnAt);
            Assert.False(field.TryTake(0, 1050, out _));
            Assert.False(field.IsUp(0, respawnAt - 1));
            Assert.Equal(-1, field.FindTouched(field.Position(0), respawnAt - 1));
            Assert.True(field.IsUp(0, respawnAt));
            Assert.True(field.TryTake(0, respawnAt, out _));
        }

        [Fact]
        public void ADownBoxDoesNotHideOneNextToIt()
        {
            var field = Field();
            field.MarkTaken(0, 5000);
            Assert.Equal(1, field.FindTouched(new Vec3(1.5f, 0.5f, 10f), 0));
        }

        [Fact]
        public void ClaimsOnBoxesThatDoNotExistAreRefused()
        {
            var field = Field();
            Assert.False(field.TryTake(-1, 0, out _));
            Assert.False(field.TryTake(2, 0, out _));
        }

        [Fact]
        public void MirroringNeverBringsABoxBackEarly()
        {
            var field = Field();
            field.MarkTaken(0, 5000); // hidden locally on pickup
            field.MarkTaken(0, 4000); // an older report from the host
            Assert.False(field.IsUp(0, 4500));
        }

        [Fact]
        public void BoxesAreUpWhateverTheClockReads()
        {
            var field = Field();
            Assert.True(field.IsUp(0, -1_000_000));
            Assert.Equal(0, field.FindTouched(field.Position(0), long.MinValue + 1));
            Assert.True(field.TryTake(0, -5000, out long respawnAt));
            Assert.Equal(-5000 + ItemBoxField.RespawnMs, respawnAt);
        }

        [Fact]
        public void ResetPutsEveryBoxBack()
        {
            var field = Field();
            field.TryTake(0, 0, out _);
            field.TryTake(1, 0, out _);
            field.Reset();
            Assert.True(field.IsUp(0, 0));
            Assert.True(field.IsUp(1, 0));
        }
    }

    public class ItemSlotsTests
    {
        [Fact]
        public void APlayerHoldsOneItemAndCanOnlyUseThatOne()
        {
            var slots = new ItemSlots();
            Assert.True(slots.CanPickUp(1));
            slots.Give(1, ItemKind.Missile);
            Assert.False(slots.CanPickUp(1));
            Assert.Equal(ItemKind.Missile, slots.Held(1));
            Assert.False(slots.TryUse(1, ItemKind.Boost));
            Assert.True(slots.TryUse(1, ItemKind.Missile));
            Assert.False(slots.TryUse(1, ItemKind.Missile));
            Assert.True(slots.CanPickUp(1));
        }

        [Fact]
        public void SlotsArePerPlayerAndClearForANewRace()
        {
            var slots = new ItemSlots();
            slots.Give(1, ItemKind.Shield);
            slots.Give(2, ItemKind.Bomb);
            slots.Remove(1);
            Assert.Equal(ItemKind.None, slots.Held(1));
            Assert.Equal(ItemKind.Bomb, slots.Held(2));
            slots.Clear();
            Assert.Equal(ItemKind.None, slots.Held(2));
            Assert.False(slots.TryUse(2, ItemKind.None));
        }
    }
}

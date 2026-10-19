using CarRacer.Core.Arena;
using CarRacer.Core.Race;
using Xunit;

namespace CarRacer.Core.Tests
{
    public class EventRulesTests
    {
        [Theory]
        [InlineData(true, true, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)]
        public void RulesSurviveTheWire(bool damage, bool items, bool carStats)
        {
            var rules = EventRules.FromByte(new EventRules(damage, items, carStats).ToByte());
            Assert.Equal(damage, rules.Damage);
            Assert.Equal(items, rules.Items);
            Assert.Equal(carStats, rules.CarStats);
        }

        [Fact]
        public void AZeroByteIsTheDefaultEverythingOn()
        {
            Assert.Equal(0, EventRules.Default.ToByte());
            var rules = EventRules.FromByte(0);
            Assert.True(rules.Damage && rules.Items && rules.CarStats);
        }

        [Fact]
        public void BattleAlwaysHasItems()
        {
            var noItems = EventRules.Default.WithItems(false);
            Assert.True(noItems.For(GameMode.Battle).Items);
            Assert.False(noItems.For(GameMode.Race).Items);
            Assert.False(noItems.For(GameMode.Sumo).Items);
            Assert.False(noItems.WithDamage(false).For(GameMode.Battle).Damage, "only items are forced");
        }
    }
}

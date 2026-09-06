using GameForWork.Core.Builds;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Tests;

public sealed class AilmentTransferTests
{
    [Fact]
    public void PropagationKeepsRemainingSnapshotAndCannotRepeatOrRecurse()
    {
        var source = new AilmentState();
        var target = new AilmentState();
        var third = new AilmentState();
        source.Apply(Ailment.Poison, DamageType.Void, 100, 2_000, 0, "hero", debuffedDamagePerSecond: 150, selfCast: true);
        source.Advance(500, (_, damage) => damage);
        source.SpreadTo(target, Ailment.Poison);
        source.Advance(100, (_, damage) => damage);
        source.SpreadTo(target, Ailment.Poison);
        var copy = Assert.Single(target.Instances);
        Assert.Equal((100m, 150m, 1_500m), (copy.DamagePerSecond, copy.DebuffedDamagePerSecond, copy.RemainingMilliseconds));
        Assert.True(copy.SelfCast);
        target.SpreadTo(third, Ailment.Poison);
        Assert.Empty(third.Instances);
        target.Advance(2_000, (_, damage) => damage);
        source.SpreadTo(target, Ailment.Poison);
        Assert.Empty(target.Instances);
        source.Apply(Ailment.Poison, DamageType.Void, 200, 2_000, 0, "hero");
        source.SpreadTo(target, Ailment.Poison);
        Assert.Equal(200, Assert.Single(target.Instances).DamagePerSecond);
    }

    [Fact]
    public void SettlementConsumesWaitingLayersWithoutAddingTheirDamage()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Ignite, DamageType.Fire, 100, 2_000, 0, "hero");
        state.Apply(Ailment.Ignite, DamageType.Fire, 50, 4_000, 0, "hero");
        state.Apply(Ailment.Poison, DamageType.Void, 10, 2_000, 0, "hero");
        Assert.Equal(140, state.Consume(Ailment.Ignite, 7_000));
        Assert.Equal(Ailment.Poison, Assert.Single(state.Instances).Kind);
        Assert.Equal(0, state.Consume(Ailment.Ignite, 7_000));
    }
    [Fact]
    public void SettlementDefendsCombinedDpsBeforeMultiplyingRemainingDuration()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Poison, DamageType.Void, 100, 2_000, 0, "hero");
        state.Apply(Ailment.Poison, DamageType.Void, 100, 4_000, 0, "hero");
        int calls = 0;
        decimal amount = state.Consume(Ailment.Poison, 5_000, (_, dps) =>
        {
            calls++;
            Assert.Equal(200, dps);
            return dps / 2;
        });
        Assert.Equal(150, amount);
        Assert.Equal(1, calls);
        Assert.Empty(state.Instances);
    }

    [Fact]
    public void AvoidedPropagationCannotRetryTheSameOriginalInstance()
    {
        var source = new AilmentState();
        var target = new AilmentState();
        source.Apply(Ailment.Bleed, DamageType.Physical, 100, 2_000, 0, "hero");
        int attempts = 0;
        source.SpreadTo(target, Ailment.Bleed, () => { attempts++; return false; });
        source.SpreadTo(target, Ailment.Bleed, () => { attempts++; return true; });
        Assert.Equal(1, attempts);
        Assert.Empty(target.Instances);
        source.Apply(Ailment.Bleed, DamageType.Physical, 200, 2_000, 0, "hero");
        source.SpreadTo(target, Ailment.Bleed, () => true);
        Assert.Equal(200, Assert.Single(target.Instances).DamagePerSecond);
    }

    [Fact]
    public void SameActionCannotConsumeItsNewAilmentAndTargetsCountIndependently()
    {
        var first = new AilmentState();
        var second = new AilmentState();
        Assert.Equal(0, first.ConsumeForAction(Ailment.Bleed, "action", 6_500));
        foreach (var target in new[] { first, second }) target.Apply(Ailment.Bleed, DamageType.Physical, 100, 2_000, 0, "hero");
        Assert.Equal(0, first.ConsumeForAction(Ailment.Bleed, "action", 6_500));
        Assert.Single(first.Instances);
        Assert.Equal(130, second.ConsumeForAction(Ailment.Bleed, "action", 6_500));
        Assert.Equal(150, first.ConsumeForAction(Ailment.Bleed, "next", 7_500));
        Assert.Empty(first.Instances);
    }

}

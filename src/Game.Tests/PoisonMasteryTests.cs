using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class PoisonMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.中毒.{option}")) };

    [Fact]
    public void FirstPoisonAndCopyShareSnapshotWithoutMultiplyingFirstBonusAgain()
    {
        var state = new AilmentState();
        state.ApplyPoison(Rules(0, 1, 2, 6), 100, 2_000, 0, "skill", "one", true, 150);
        Assert.Equal(new decimal[] { 256, 179.2m }, state.Instances.Select(dot => dot.DamagePerSecond));
        Assert.All(state.Instances, dot => Assert.Equal(4_500, dot.RemainingMilliseconds));
        Assert.Equal(384, state.Instances[0].DebuffedDamagePerSecond);
        state.ApplyPoison(Rules(0, 1, 2, 6), 100, 2_000, 0, "skill", "one", true);
        Assert.Equal(3, state.Instances.Count);
        Assert.Equal((128m, 3_000m), (state.Instances[2].DamagePerSecond, state.Instances[2].RemainingMilliseconds));
        state.ApplyPoison(Rules(2), 100, 2_000, 0, "skill", "two", true);
        Assert.Equal(5, state.Instances.Count);
    }

    [Fact]
    public void TriggeredPoisonDoesNotDuplicateAndExpiredFirstBonusCanReturn()
    {
        var state = new AilmentState();
        state.ApplyPoison(Rules(2, 6), 100, 1_000, 0, "skill", "one", false);
        Assert.Equal(200, Assert.Single(state.Instances).DamagePerSecond);
        state.Advance(2_000, (_, dps) => dps);
        state.ApplyPoison(Rules(6), 100, 1_000, 0, "skill", "two", true);
        Assert.Equal(200, Assert.Single(state.Instances).DamagePerSecond);
    }

    [Fact]
    public void PoisonSpreadSelectsFiveStrongestOriginalSnapshots()
    {
        var source = new AilmentState();
        for (int i = 1; i <= 7; i++) source.Apply(Ailment.Poison, DamageType.Void, i * 10, 2_000, 0, "hero");
        var target = new AilmentState();
        source.SpreadTo(target, Ailment.Poison, maximum: 5);
        Assert.Equal(new decimal[] { 70, 60, 50, 40, 30 }, target.Instances.Select(dot => dot.DamagePerSecond));
    }

    [Fact]
    public void CriticalPoisonIsGuaranteedButStillRespectsAvoidance()
    {
        Assert.DoesNotContain(Run(Rules()).Events, e => e.Detail.Contains("ailment:poison", StringComparison.Ordinal));
        Assert.Contains(Run(Rules(3)).Events, e => e.Detail.Contains("ailment:poison", StringComparison.Ordinal));
        Assert.DoesNotContain(Run(Rules(3), 10_000).Events, e => e.Detail.Contains("ailment:poison", StringComparison.Ordinal));
    }

    [Fact]
    public void TenPoisonLayersReduceIncomingHitsInProductionCombat()
    {
        Assert.True(Run(Rules(3, 5)).HeroLife > Run(Rules(3)).HeroLife);
    }

    private static NodeCombatResult Run(PassiveModifiers rules, int avoidance = 0)
    {
        var config = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        var build = new TeamBuild(new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000, FlatMaximumMana: 100_000),
            new("test", 1, 1, 60_000, 10_000), config, UseWarCry: false, AlwaysHit: true,
            ActiveSkills: [config], PassiveProfile: rules);
        return new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, MaximumTicks: 160,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, AilmentAvoidanceBasisPoints = avoidance,
                MinimumPhysicalDamage = 100, MaximumPhysicalDamage = 100 }]), 731);
    }
}

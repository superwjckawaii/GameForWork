using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class DamageOverTimeMasteryTests
{
    private static PassiveModifiers Mastery(params int[] options) => PassiveModifiers.Empty with
    {
        MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.持续伤害通用.{option}")),
    };

    [Fact]
    public void DamageTradeoffDurationAndSpeedAreExplicitRules()
    {
        Assert.Equal(7_000, DamageOverTimeMasteryRules.HitMultiplier(Mastery(0)));
        Assert.Equal(14_000, DamageOverTimeMasteryRules.OutputMultiplier(Mastery(0), true));
        Assert.Equal(18_000, DamageOverTimeMasteryRules.DurationMultiplier(Mastery(1)));
        Assert.Equal(5_000, DamageOverTimeMasteryRules.FasterAilments(Mastery(2)));
        Assert.Equal(7_500, DamageOverTimeMasteryRules.OutputMultiplier(Mastery(3), true));
        Assert.Equal(14_000, DamageOverTimeMasteryRules.OutputMultiplier(Mastery(3), false));
    }

    [Fact]
    public void CompositeDamageSnapshotsOnlyWhenAnotherDamageTypeIsAlreadyActive()
    {
        var target = new AilmentState();
        PassiveModifiers mastery = Mastery(4);

        Assert.Equal(10_000, DamageOverTimeMasteryRules.NewEffectMultiplier(mastery, target, DamageType.Fire));
        target.Apply(Ailment.Bleed, DamageType.Physical, 10, 1_000, 0, "bleed");
        Assert.Equal(13_000, DamageOverTimeMasteryRules.NewEffectMultiplier(mastery, target, DamageType.Fire));
        Assert.Equal(10_000, DamageOverTimeMasteryRules.NewEffectMultiplier(mastery, target, DamageType.Physical));
    }

    [Fact]
    public void DamageOverTimeProtectionRequiresAnActiveEffect()
    {
        var target = new AilmentState();
        Assert.Equal(10_000, DamageOverTimeMasteryRules.IncomingHitMultiplier(Mastery(5), target));
        target.Apply(Ailment.Poison, DamageType.Void, 10, 1_000, 0, "poison");
        Assert.Equal(9_000, DamageOverTimeMasteryRules.IncomingHitMultiplier(Mastery(5), target));
    }

    [Fact]
    public void RecoveryIsLimitedToFiveDamageOverTimeKillsPerSecond()
    {
        var state = new DamageOverTimeRecoveryState();
        Assert.Equal(5, Enumerable.Range(0, 8).Count(_ => state.TryRecover(10)));
        Assert.True(state.TryRecover(20));
    }

    [Fact]
    public void NonAilmentGroundDamageUsesTheProductionMasteryPath()
    {
        int Damage(PassiveModifiers passives)
        {
            var skill = new SkillConfiguration(SkillIds.VoidDecayField, SkillSupport.None);
            TeamBuild build = Build(passives, skill);
            NodeCombatResult result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0,
                MaximumTicks: 160, EnemyPool: [Enemies.CorruptedWorker with
                { Life = 100_000_000, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0, MovementSpeedRawPerSecond = 0 }]), 731);
            return result.Events.Where(item => item.Detail == "dot:ground").Sum(item => item.Value);
        }

        int baseline = Damage(PassiveModifiers.Empty);
        Assert.True(baseline > 0);
        Assert.True(Damage(Mastery(3)) > baseline);
    }

    [Fact]
    public void DamageOverTimeMasteriesNoLongerInjectFallbackStats()
    {
        PassiveNodeDefinition node = PassiveTree.Nodes.First(candidate =>
            candidate.MasteryGroup == "builds.mastery.持续伤害通用");
        Assert.All(PassiveTree.MasteryOptions(node), effect => Assert.Equal(0, effect.Value));
    }

    private static TeamBuild Build(PassiveModifiers passives, SkillConfiguration configuration) => new(
        new(20, new(100, 100, 100, 100), new(100, 0, 100), FlatMaximumLife: 1_000, FlatMaximumMana: 1_000),
        new("test", 10, 20, 1_000, 500), new(SkillIds.HeavyStrike, SkillSupport.None), UseWarCry: false, AlwaysHit: true,
        ActiveSkills: [configuration], PassiveProfile: passives);
}

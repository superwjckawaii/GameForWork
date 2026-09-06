using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Tests;

public sealed class AilmentMasteryTests
{
    private static PassiveModifiers Rules(string group, params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.{group}.{option}")) };

    [Theory]
    [InlineData(Ailment.Bleed, "流血", 8, 50)]
    [InlineData(Ailment.Ignite, "点燃", 2, 70)]
    public void MasteryMultipleLayerPenaltyAppliesToOneLayerAndCapsActiveLayers(Ailment kind, string group, int maximum, int expected)
    {
        var state = new AilmentState();
        AilmentMasteryRules.Configure(state, Rules(group, 1), true, true);
        state.Apply(kind, DamageType.Fire, 100, 2_000, 0, "hero");
        Assert.Equal(expected, state.Advance(1_000, (_, dps) => dps).Sum(pulse => pulse.Damage));
        for (int i = 0; i < 10; i++) state.Apply(kind, DamageType.Fire, 100, 2_000, 0, "hero");
        Assert.Equal(maximum, state.Count(kind));
    }

    [Theory]
    [InlineData(Ailment.Bleed, "流血")]
    [InlineData(Ailment.Ignite, "点燃")]
    public void SingleModeOverridesAscendancyAndCannotMultiplyWithMultiMode(Ailment kind, string group)
    {
        var state = new AilmentState();
        AilmentMasteryRules.Configure(state, Rules(group, 0, 1), true, true);
        state.Apply(kind, DamageType.Fire, 100, 2_000, 0, "hero");
        state.Apply(kind, DamageType.Fire, 200, 2_000, 0, "hero");
        Assert.Equal(1, state.Count(kind));
        Assert.Equal(200, Assert.Single(state.Advance(1_000, (_, dps) => dps)).Damage);
    }

    [Theory]
    [InlineData(Ailment.Bleed, 5)]
    [InlineData(Ailment.Ignite, 4)]
    public void SettlementCountsOnlyDistinctSuccessfulSelfActionsAgainstActiveAilment(Ailment kind, int every)
    {
        var state = new AilmentState();
        Assert.False(state.CountSettlementHit(kind, "missing", every, true));
        state.Apply(kind, DamageType.Fire, 100, 2_000, 0, "hero");
        for (int i = 1; i <= every; i++)
        {
            Assert.False(state.CountSettlementHit(kind, "trigger", every, false));
            Assert.Equal(i == every, state.CountSettlementHit(kind, i.ToString(), every, true));
            Assert.False(state.CountSettlementHit(kind, i.ToString(), every, true));
        }
    }

    [Fact]
    public void ConditionsEndAsTheTargetsAilmentsExpire()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Bleed, DamageType.Physical, 100, 1_000, 0, "hero");
        state.Apply(Ailment.Ignite, DamageType.Fire, 100, 1_000, 0, "hero");
        Assert.Equal(13_500, AilmentMasteryRules.BleedingTargetHitMultiplier(Rules("流血", 2), state));
        Assert.Equal(4_000, AilmentMasteryRules.LifeRecoveryMultiplier(Rules("点燃", 6), state));
        state.Advance(1_000, (_, dps) => dps);
        Assert.Equal(10_000, AilmentMasteryRules.BleedingTargetHitMultiplier(Rules("流血", 2), state));
        Assert.Equal(10_000, AilmentMasteryRules.LifeRecoveryMultiplier(Rules("点燃", 6), state));
    }
    [Fact]
    public void ConvertedLightningCriticalCanIgniteOnlyWithElementalIgnitionAndRespectsFocus()
    {
        NodeCombatResult Run(bool mastery, bool focus)
        {
            var configuration = new SkillConfiguration(SkillIds.HeavyStrike,
                SkillSupport.PhysicalToLightning | (focus ? SkillSupport.ElementalFocus : SkillSupport.None));
            var build = new TeamBuild(new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000, FlatMaximumMana: 100_000),
                new("test", 100, 100, 1_000, 10_000), configuration, UseWarCry: false, AlwaysHit: true,
                ActiveSkills: [configuration], PassiveProfile: mastery ? Rules("点燃", 3) : PassiveModifiers.Empty);
            return new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, MaximumTicks: 160,
                EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000 }]), 731);
        }
        Assert.DoesNotContain(Run(false, false).Events, e => e.Detail.Contains("ailment:ignite", StringComparison.Ordinal));
        Assert.Contains(Run(true, false).Events, e => e.Detail.Contains("ailment:ignite", StringComparison.Ordinal));
        Assert.DoesNotContain(Run(true, true).Events, e => e.Detail.Contains("ailment:ignite", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("流血")]
    [InlineData("点燃")]
    public void SelectingExclusiveModeReplacesPreviousChoiceAndOldSelectionsRestore(string group)
    {
        string[] nodes = PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Mastery && node.MasteryGroup == $"builds.mastery.{group}")
            .Take(2).Select(node => node.StableId).ToArray();
        Assert.Equal(2, nodes.Length);
        var allocation = new PassiveTreeAllocation();
        foreach (string node in nodes) Assert.True(allocation.TryAllocatePath(node, 149));
        Assert.True(allocation.TrySelectMastery(nodes[0], 0));
        Assert.True(allocation.TrySelectMastery(nodes[1], 1));
        Assert.Equal(nodes[1], Assert.Single(allocation.MasterySelections).Key);
        var restored = PassiveTreeAllocation.Restore(allocation.Allocated, 0,
            new Dictionary<string, int> { [nodes[0]] = 0, [nodes[1]] = 1 });
        Assert.Equal(nodes[1], Assert.Single(restored.MasterySelections).Key);
        Assert.Contains(nodes[0], restored.Allocated);
    }

}

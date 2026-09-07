using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Spatial;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;

public sealed class StunMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.眩晕.{option}")) };

    [Fact]
    public void ThresholdReductionCannotRoundToZero()
    {
        Assert.Equal(10_000, CombatRules.StunChance(1, 1, 7_500));
        Assert.Equal(0, CombatRules.StunChance(0, 1, 7_500));
        Assert.Equal(4_000, StunMasteryRules.Chance(Rules(0), 10, 100));
        Assert.Equal(3_333, StunMasteryRules.Chance(Rules(1), 10, 100));
    }

    [Theory]
    [InlineData(1_500, 875)]
    [InlineData(1_000, 875)]
    [InlineData(500, 500)]
    public void DurationIncreaseRespectsTargetCap(int cap, int expected)
    {
        Assert.Equal(expected, StunMasteryRules.Duration(Rules(2), cap));
        Assert.Equal(0, StunMasteryRules.Duration(Rules(2), cap, 10_000));
    }

    [Fact]
    public void PursuitAndSpeedExpireIndependentlyOfControl()
    {
        var state = new CombatConditionState();
        state.Stunned(10);
        Assert.Equal(13_500, StunMasteryRules.HitMultiplier(Rules(3), 89, state.StunRecentUntil));
        Assert.Equal(12_000, StunMasteryRules.SpeedMultiplier(Rules(5), 89, state.StunRecentUntil));
        Assert.Equal(10_000, StunMasteryRules.SpeedMultiplier(Rules(5), 90, state.StunRecentUntil));
        var build = Build(Rules()) with { IncreasedAttackSpeedBasisPoints = 10_000 };
        Assert.Equal(5_000, CombatSkillRules.ActionFrequencyMilliPerSecond(build with { AttackCastSpeedMultiplierBasisPoints = 12_000 },
            8, 0, SkillTag.Attack) * 10_000 / 12_000);
    }

    [Fact]
    public void AllHitsCanStunAndAvoidanceStillApplies()
    {
        Assert.Contains(Run(Rules()).Events, e => e.Detail == "ailment:stun");
        Assert.DoesNotContain(Run(Rules(), 10_000).Events, e => e.Detail == "ailment:stun");
    }

    [Fact]
    public void PursuitDoesNotEnhanceTheApplyingHit()
    {
        var baseline = Run(Rules()).Events.Where(e => e.Kind == SpatialEventKind.HeavyStrike).ToArray();
        var enhanced = Run(Rules(3)).Events.Where(e => e.Kind == SpatialEventKind.HeavyStrike).ToArray();
        Assert.True(baseline.Length > 1 && enhanced.Length > 1);
        Assert.Equal(baseline[0].Value, enhanced[0].Value);
        Assert.True(enhanced[1].Value > baseline[1].Value);
    }

    [Fact]
    public void MasteriesDoNotInjectUnrelatedStats()
    {
        var node = PassiveTree.Nodes.First(n => n.MasteryGroup == "builds.mastery.眩晕");
        Assert.All(PassiveTree.MasteryOptions(node), effect => Assert.Equal(0, effect.Value));
    }

    [Fact]
    public void SuccessfulStunActuallyAcceleratesAttacksAndAppliesBoundedArmorBreak()
    {
        var baseline = Run(Rules(0));
        var faster = Run(Rules(0, 5, 6));
        Assert.True(faster.Events.Count(e => e.Kind == SpatialEventKind.HeavyStrike) > baseline.Events.Count(e => e.Kind == SpatialEventKind.HeavyStrike));
        Assert.Contains(faster.Frames.SelectMany(frame => frame.Enemies), enemy => enemy.ArmorBreakStacks > 0);
        Assert.All(faster.Frames.SelectMany(frame => frame.Enemies), enemy => Assert.InRange(enemy.ArmorBreakStacks, 0, 5));
    }

    [Fact]
    public void TypedStunNodesCarryDurationAndChanceRatherThanDamageApproximations()
    {
        var node = PassiveTree.Nodes.Single(n => n.DisplayName == "镇军震慑");
        Assert.Contains(node.Effects, effect => effect.Kind == PassiveEffectKind.IncreasedStunChanceBasisPoints && effect.Value == 6_000);
        Assert.Contains(node.Effects, effect => effect.Kind == PassiveEffectKind.IncreasedStunDurationBasisPoints && effect.Value == 8_000);
        var profile = Rules(2) with { Specialized = node.Effects.ToDictionary(e => e.Kind, e => e.Value) };
        Assert.Equal(1_155, StunMasteryRules.Duration(profile, 1_500));
    }

    private static TeamBuild Build(PassiveModifiers rules)
    {
        var config = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        return new(new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000, FlatMaximumMana: 100_000),
            new("test", 100, 100, 2_000, 0), config, UseWarCry: false, AlwaysHit: true,
            ActiveSkills: [config], PassiveProfile: rules);
    }
    private static NodeCombatResult Run(PassiveModifiers rules, int avoidance = 0) => new SpatialCombatRunner().Run(
        new(Build(rules), 1, 1, 1, false, false, false, 0, MaximumTicks: 240,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 10_000, Armor = 0, AilmentAvoidanceBasisPoints = avoidance,
                MinimumPhysicalDamage = 1, MaximumPhysicalDamage = 1 }]), 731);
}

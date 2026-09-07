using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class AttackMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.攻击.{option}")) };
    [Fact]
    public void GuaranteedAttacksDoNotDisableSpellCriticals()
    {
        Assert.True(MasteryRuntime.CannotCrit(Rules(0), SkillTag.Attack));
        Assert.False(MasteryRuntime.CannotCrit(Rules(0), SkillTag.Spell));
        Assert.True(MasteryRuntime.AlwaysHits(Rules(0), SkillTag.Attack));
        Assert.False(MasteryRuntime.AlwaysHits(Rules(0), SkillTag.Spell));
    }
    [Fact]
    public void GlobalAddedDamageDoesNotMultiplyWeaponBase()
    {
        Assert.Equal(140, AttackMasteryRules.AddedDamage(Rules(1), 100));
        var config = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        int damage = CombatSkillRules.BaseDamage(CombatSkillRules.Resolve(config, 100), SkillTag.Attack,
            new("test", 100, 100, 1_000, 500), AttackMasteryRules.AddedDamage(Rules(1), 100), 100);
        Assert.Equal(240, damage);
    }
    [Fact]
    public void AttackSpeedAndCostAreIndependentOfCooldownRecovery()
    {
        var config = new SkillConfiguration(SkillIds.SeismicCharge, SkillSupport.None);
        var baseline = CombatSkillRules.Resolve(config, 1_000);
        var faster = CombatSkillRules.Resolve(config, 1_000, Rules(2));
        Assert.Equal(baseline.CooldownTicks, faster.CooldownTicks);
        Assert.Equal(CombatRules.ApplyMore(baseline.ManaCost, [12_500]), faster.ManaCost);
        Assert.Equal(12_500, MasteryRuntime.ActionSpeedMultiplier(Rules(2), SkillTag.Attack, Weapons.Unequipped));
        var recovery = CombatSkillRules.Resolve(config, 1_000, Rules(3));
        Assert.True(recovery.CooldownTicks < baseline.CooldownTicks);
        Assert.Equal(baseline.CastTimeTicks, recovery.CastTimeTicks);
    }
    [Fact]
    public void ActivationAndTargetTradeoffsAreDistinctMultipliers()
    {
        Assert.Equal(13_500, AttackMasteryRules.TargetMultiplier(Rules(4), true));
        Assert.Equal(8_000, AttackMasteryRules.TargetMultiplier(Rules(4), false));
        Assert.Equal(13_000, AttackMasteryRules.ActivationMultiplier(Rules(5), true));
        Assert.Equal(7_000, AttackMasteryRules.ActivationMultiplier(Rules(5), false));
    }
    [Fact]
    public void SuppressionCountsActionsOnMainTargetAndResetsOnSwitchOrExpiry()
    {
        var state = new CombatConditionState(); state.SelectAttackTarget("main");
        state.AttackHit("main", "one", 1, true); state.AttackHit("main", "one", 2, true);
        state.AttackHit("other", "two", 2, true); state.AttackHit("main", "trigger", 2, false);
        Assert.Equal(10_400, state.AttackMultiplier("main", 3));
        for (int i = 0; i < 10; i++) state.AttackHit("main", $"extra:{i}", 4, true);
        Assert.Equal(13_200, state.AttackMultiplier("main", 83));
        Assert.Equal(10_000, state.AttackMultiplier("main", 84));
        state.AttackHit("main", "fresh", 85, true);
        Assert.Equal(10_400, state.AttackMultiplier("main", 85));
        state.SelectAttackTarget("other"); state.SelectAttackTarget("main");
        Assert.Equal(10_000, state.AttackMultiplier("main", 86));
    }
    [Fact]
    public void SuppressionIncreasesSubsequentProductionHits()
    {
        var baseline = Run(Rules()).Events.Where(e => e.Kind == SpatialEventKind.HeavyStrike && e.Value > 0).ToArray();
        var enhanced = Run(Rules(6)).Events.Where(e => e.Kind == SpatialEventKind.HeavyStrike && e.Value > 0).ToArray();
        Assert.Equal(baseline[0].Value, enhanced[0].Value);
        Assert.True(enhanced[1].Value > baseline[1].Value);
    }
    private static NodeCombatResult Run(PassiveModifiers rules)
    {
        var config = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        var build = new TeamBuild(new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000, FlatMaximumMana: 100_000),
            new("test", 100, 100, 2_000, 0), config, UseWarCry: false, AlwaysHit: true, ActiveSkills: [config], PassiveProfile: rules);
        return new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, MaximumTicks: 140,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 100_000, Armor = 0, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
    }
}

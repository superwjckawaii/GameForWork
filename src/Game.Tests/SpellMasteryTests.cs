using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;
namespace GameForWork.Tests;
public sealed class SpellMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with { MasteryMechanics = string.Join('|', options.Select(o => $"builds.mastery.rule.法术.{o}")) };
    [Fact]
    public void PreviewIncludesPermanentSpellMultipliersButNotConditionalCastRewards()
    {
        var config = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        TeamBuild build = new(new(20, new(100, 100, 100, 100), new(0, 0, 0)), Weapons.Unequipped, new(SkillIds.HeavyStrike, SkillSupport.None));
        int Hit(PassiveModifiers p) => BuildSummaryRules.CalculateOffense(build with { PassiveProfile = p }, config).AverageHitDamage;
        int baseline = Hit(Rules()); Assert.True(baseline > 0);
        Assert.True(Hit(Rules(0)) > baseline); Assert.True(Hit(Rules(4)) >= baseline * 2 - 2);
        Assert.Equal(baseline, Hit(Rules(2, 6)));
    }
    [Fact]
    public void SelfAttackInterruptsChannelButTriggeredActionDoesNot()
    {
        var p = Rules(2, 6); var tags = SkillTag.Spell | SkillTag.Channelling;
        var state = new SpellCastState();
        state.Started("one", "channel", tags, p, 0, true);
        state.Started("trigger", "trigger", SkillTag.Spell | SkillTag.Trigger, p, 1, false);
        state.Started("pulse", "channel", tags, p, 2, false);
        Assert.Equal(14000, state.Multiplier("pulse"));
        state.Started("attack", SkillIds.HeavyStrike, SkillTag.Attack, p, 3, false);
        state.Started("two", "channel", tags, p, 4, false);
        Assert.Equal(10000, state.Multiplier("two"));
        state.Started("counter", "counter", SkillTag.Spell | SkillTag.Counter, p, 5, false);
        state.Started("three", SkillIds.EmberNova, SkillTag.Spell, p, 6, false);
        Assert.Equal(14000, state.Multiplier("three"));
    }
    [Fact]
    public void FreeManaDoesNotWaiveConvertedShieldAndFailureIsAtomic()
    {
        var profile = PassiveModifiers.Empty with { MasteryMechanics = "builds.mastery.rule.能量护盾.6" };
        CharacterSheet sheet = new(1, new(0, 0, 0, 0), new(0, 0, 100));
        var skill = CombatSkillRules.Resolve(new(SkillIds.EmberNova, SkillSupport.None), 1000) with { ManaCost = 100, WaiveManaCost = true };
        var hero = new ResourceState(sheet, initialMana: 0, initialShield: 100, passives: profile);
        Assert.True(CombatSkillRules.TryPay(hero, skill)); Assert.Equal(50, hero.Shield); Assert.Equal(0, hero.Mana);
        var poor = new ResourceState(sheet, initialMana: 0, initialShield: 49, passives: profile);
        Assert.False(CombatSkillRules.TryPay(poor, skill)); Assert.Equal(49, poor.Shield); Assert.Equal(0, poor.Mana);
        var channel = new ChannelCostState();
        var payer = new ResourceState(sheet, initialMana: 0, initialShield: 100, passives: profile);
        Assert.True(channel.TryPay(payer, skill, out _)); Assert.Equal(88, payer.Shield); Assert.Equal(0, payer.Mana);
    }
    [Fact]
    public void ThirdCastQuoteDoesNotAdvanceOnFailureAndOnlyWaivesMana()
    {
        var state = new SpellCastState(); var p = Rules(6);
        var skill = CombatSkillRules.Resolve(new(SkillIds.EmberNova, SkillSupport.None), 1000) with { ManaCost = 100, LifeCost = 30 };
        state.Started("one", skill.SkillId, SkillTag.Spell, p, 0, false);
        Assert.Equal(100, state.Quote(skill, p, 1).ManaCost);
        state.Started("two", skill.SkillId, SkillTag.Spell, p, 1, false);
        Assert.True(state.Quote(skill, p, 2).WaiveManaCost); Assert.Equal(30, state.Quote(skill, p, 2).LifeCost);
        Assert.True(state.Quote(skill, p, 3).WaiveManaCost);
        state.Started("three", skill.SkillId, SkillTag.Spell, p, 3, false);
        Assert.Equal(14000, state.Multiplier("three")); Assert.Equal(100, state.Quote(skill, p, 4).ManaCost);
    }
    [Fact]
    public void ContinuousChannelDoesNotAdvanceAndKeepsItsInitialManaCondition()
    {
        var state = new SpellCastState(); var p = Rules(2, 6); var tags = SkillTag.Spell | SkillTag.Channelling;
        state.Started("one", "channel", tags, p, 0, true);
        state.Started("pulse", "channel", tags, p, 5, false);
        Assert.Equal(14000, state.Multiplier("pulse"));
        state.Started("two", "other", SkillTag.Spell, p, 6, false);
        state.Started("three", "channel", tags, p, 7, true);
        Assert.Equal(19600, state.Multiplier("three"));
    }
    [Fact]
    public void ConditionalRecoverySharesCooldownAcrossTargetsAndExcludesTriggers()
    {
        var state = new SpellCastState(); var p = Rules(5);
        Assert.Equal(0, state.HitRecovery(p, false, true, 0, 1000));
        Assert.Equal(20, state.HitRecovery(p, true, true, 0, 1000));
        Assert.Equal(0, state.HitRecovery(p, true, true, 9, 1000));
        Assert.Equal(20, state.HitRecovery(p, true, true, 10, 1000));
        Assert.False(SpellMasteryRules.Self("archetypes.skill.corrosive_trap", SkillTag.Spell));
    }
    [Fact]
    public void HitAndAilmentTradeoffsAreSeparateFromMultiplicativeSpeedAndCost()
    {
        Assert.Equal(195, MasteryRuntime.ManaCost(Rules(2, 3, 4), SkillTag.Spell, 100));
        Assert.Equal(100, MasteryRuntime.ManaCost(Rules(2, 3, 4), SkillTag.Attack, 100));
        Assert.Equal(8450, SpellMasteryRules.SpeedMultiplier(Rules(3, 4)));
        Assert.Equal(20000, SpellMasteryRules.HitMultiplier(Rules(4)));
        Assert.Equal(6500, SpellMasteryRules.AilmentMultiplier(Rules(4)));
    }
    [Fact]
    public void SelfSpellMasteryIncreasesProductionGroundDamage()
    {
        int Damage(PassiveModifiers p)
        {
            var config = new SkillConfiguration(SkillIds.VoidDecayField, SkillSupport.None);
            TeamBuild build = new(new(20, new(100, 100, 100, 100), new(100, 0, 100), FlatMaximumLife: 1000, FlatMaximumMana: 1000),
                new("test", 10, 20, 1000, 500), new(SkillIds.HeavyStrike, SkillSupport.None), UseWarCry: false, AlwaysHit: true, ActiveSkills: [config], PassiveProfile: p);
            var result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, MaximumTicks: 160, EnemyPool: [Enemies.CorruptedWorker with { Life = 100000000, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0, MovementSpeedRawPerSecond = 0 }]), 731);
            return result.Events.Where(e => e.Detail == "dot:ground").Sum(e => e.Value);
        }
        int baseline = Damage(Rules()); Assert.True(baseline > 0); Assert.True(Damage(Rules(0)) > baseline);
    }
}

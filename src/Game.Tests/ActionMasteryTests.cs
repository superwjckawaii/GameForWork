using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;

public sealed class ActionMasteryTests
{
    private static PassiveModifiers Mastery(string group, params int[] options) => PassiveModifiers.Empty with
    {
        MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.{group}.{option}")),
    };

    [Fact]
    public void TriggerAndChannelTradeoffsAreExplicit()
    {
        PassiveModifiers trigger = Mastery("触发_冷却", 0);
        PassiveModifiers channel = Mastery("重复_引导", 3, 4);

        Assert.Equal(5_000, ActionMasteryRules.SkillCostMultiplier(trigger, true));
        Assert.Equal(8_000, ActionMasteryRules.DamageMultiplier(trigger, SkillTag.Spell, true));
        Assert.Equal(8_000, ActionMasteryRules.DamageMultiplier(channel, SkillTag.Spell | SkillTag.Channelling, false));
        Assert.Equal(8_500, ActionMasteryRules.IncomingHitMultiplier(channel, true));
    }

    [Fact]
    public void RepeatSpeedAndFinalRepeatDamageCompose()
    {
        PassiveModifiers mastery = Mastery("重复_引导", 0, 1);

        Assert.Equal(7_500, ActionMasteryRules.RepeatDelayMultiplier(mastery));
        Assert.Equal(6_400, ActionMasteryRules.RepeatDamageMultiplier(mastery, 1, 2));
        Assert.Equal(16_000, ActionMasteryRules.RepeatDamageMultiplier(mastery, 2, 2));
    }

    [Fact]
    public void TriggeredRepeatIsCapturedAsOneTerminalCopy()
    {
        PassiveModifiers mastery = Mastery("触发_冷却", 2);
        var configuration = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        var build = new TeamBuild(
            new(20, new(100, 100, 100, 100), new(100, 0, 100), FlatMaximumLife: 1_000, FlatMaximumMana: 1_000),
            new("test", 10, 20, 1_000, 500), new(SkillIds.HeavyStrike, SkillSupport.None),
            UseWarCry: false, AlwaysHit: true, ActiveSkills: [configuration], PassiveProfile: mastery);
        ResolvedSkill skill = CombatSkillRules.Resolve(configuration, 1_000, mastery);
        var hit = new CombatHitSnapshot("target", new(0, 0), skill, configuration, build,
            new(0, 100, 0, 0, 0, [new(100, DamageType.Fire, [DamageType.Fire], [])], []), [], false);
        var queue = new CombatActionQueue();

        queue.Record("triggered", hit, 0, true);
        queue.CompleteReady(50_000, new HashSet<string>(), false, false);
        DeferredCombatCopy copy = Assert.Single(queue.TakeDue(100_000));

        Assert.Equal("mastery:trigger-repeat", copy.Source);
        Assert.Equal(5_000, copy.Multiplier);
        Assert.Equal((1, 1), (copy.RepeatIndex, copy.RepeatCount));
    }

    [Fact]
    public void TriggerStorageKeepsOnlyTheLatestConditionUntilCooldownEnds()
    {
        var state = new ReactionState(Mastery("触发_冷却", 1));
        var configuration = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);

        Assert.True(state.Schedule(configuration, "first", 10));
        Assert.Single(state.Drain());
        state.Tick = 1;
        Assert.True(state.Schedule(configuration, "old", 10));
        Assert.True(state.Schedule(configuration, "latest", 10));
        Assert.Empty(state.Drain());
        state.Tick = 10;

        Assert.Equal("latest", Assert.Single(state.Drain()).TargetId);
        state.Tick = 11;
        Assert.True(state.Schedule(configuration, "next", 10));
        Assert.Empty(state.Drain());
        state.Tick = 20;
        Assert.Equal("next", Assert.Single(state.Drain()).TargetId);
    }

    [Fact]
    public void ThirdAttackTriggerCountsActionsInsteadOfAreaTargets()
    {
        var state = new ReactionState(Mastery("触发_冷却", 3));

        Assert.False(state.ThirdAttack("attack-1"));
        Assert.False(state.ThirdAttack("attack-1"));
        Assert.False(state.ThirdAttack("attack-2"));
        Assert.True(state.ThirdAttack("attack-3"));
    }

    [Fact]
    public void CooldownChargesOverdraftAndRouletteShareOneState()
    {
        PassiveModifiers mastery = Mastery("触发_冷却", 4, 5, 6);
        var hero = new ResourceState(new(1, new(0, 0, 0, 0), new(0, 0, 0),
            FlatMaximumLife: 1_000, FlatMaximumMana: 1_000));
        ResolvedSkill template = CombatSkillRules.Resolve(
            new(SkillIds.EmberNova, SkillSupport.None), hero.MaximumLife, mastery) with { CooldownTicks = 10 };
        var state = new CooldownMasteryState(mastery);

        Assert.True(state.Start(template, 0, hero));
        Assert.True(state.Start(template, 0, hero));
        Assert.Equal(0, state.Charges(template, 0));
        int manaBeforeOverdraft = hero.Mana;
        Assert.True(state.Start(template, 0, hero));
        Assert.Equal(manaBeforeOverdraft - hero.MaximumMana / 5, hero.Mana);
        Assert.False(state.CanUse(template, 1, hero));

        state = new CooldownMasteryState(mastery);
        ResolvedSkill first = template with { SkillId = "first" };
        ResolvedSkill second = template with { SkillId = "second" };
        ResolvedSkill third = template with { SkillId = "third" };
        Assert.True(state.Start(first, 0, hero));
        Assert.True(state.Start(second, 1, hero));
        Assert.True(state.Start(third, 2, hero));
        Assert.Equal(5, state.Remaining(first.SkillId, 2));
        Assert.Equal(6, state.Remaining(second.SkillId, 2));
        Assert.Equal(7, state.Remaining(third.SkillId, 2));
    }

    [Fact]
    public void BackupChargeAppliesItsFinalCooldownPenalty()
    {
        var configuration = new SkillConfiguration(SkillIds.SeismicCharge, SkillSupport.None);
        int baseline = CombatSkillRules.Resolve(configuration, 1_000).CooldownTicks;
        int modified = CombatSkillRules.Resolve(configuration, 1_000, Mastery("触发_冷却", 4)).CooldownTicks;

        Assert.Equal(Math.Max(2, baseline * 13_000 / 10_000), modified);
    }

    [Fact]
    public void DeepChannelAddsTwoTimedLayers()
    {
        PassiveModifiers mastery = Mastery("重复_引导", 6);
        var configuration = new SkillConfiguration(SkillIds.BloodTideSpin, SkillSupport.None);
        var build = new TeamBuild(
            new(20, new(100, 100, 100, 100), new(100, 0, 100), FlatMaximumLife: 1_000, FlatMaximumMana: 1_000),
            new("test", 10, 20, 1_000, 500), new(SkillIds.HeavyStrike, SkillSupport.None),
            UseWarCry: false, AlwaysHit: true, ActiveSkills: [configuration], PassiveProfile: mastery);
        ResolvedSkill skill = CombatSkillRules.Resolve(configuration, 1_000, mastery);
        var queue = new CombatActionQueue();

        queue.Begin("channel", skill, build, 0, false);

        Assert.Equal(16_000, queue.ChannelDepthMultiplier("channel", 80, mastery));
        Assert.Equal(19_000, queue.ChannelDepthMultiplier("channel", 120, mastery));
    }

    [Fact]
    public void ActionMasteriesDoNotInjectFallbackStats()
    {
        foreach (string group in new[] { "重复_引导", "触发_冷却" })
        {
            PassiveNodeDefinition node = PassiveTree.Nodes.First(candidate => candidate.MasteryGroup == $"builds.mastery.{group}");
            Assert.All(PassiveTree.MasteryOptions(node), effect => Assert.Equal(0, effect.Value));
        }
    }
}

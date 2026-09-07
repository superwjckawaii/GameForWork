using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;

namespace GameForWork.Tests;
public sealed class RegenerationMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.再生_持续恢复.{option}")) };
    private static CharacterSheet Sheet() => new(1, new(0, 0, 0, 0), new(0, 0, 100), FlatLifeRegeneration: 100);
    [Fact]
    public void TypedDesperationNotableChecksEachResourceIndependently()
    {
        var node = PassiveTree.Nodes.Single(n => n.DisplayName == "绝境复苏");
        var p = PassiveModifiers.Empty with { Specialized = node.Effects.ToDictionary(e => e.Kind, e => e.Value) };
        var sheet = Sheet() with { MaximumShieldRegenerationBasisPoints = 10000 };
        var life = RegenerationMasteryRules.Recent(sheet, p, 0, 0, true, false);
        var shield = RegenerationMasteryRules.Recent(sheet, p, 0, 0, false, true);
        Assert.Equal(150, life.LifeRegenerationPerSecond().Value);
        Assert.Equal(100m, life.ShieldRegenerationPerSecond);
        Assert.Equal(100, shield.LifeRegenerationPerSecond().Value);
        Assert.Equal(150m, shield.ShieldRegenerationPerSecond);
    }
    [Fact]
    public void ConversionSplitsBaseBeforeIndependentResourceModifiers()
    {
        var s = RegenerationMasteryRules.Apply(Sheet() with { IncreasedLifeRegenerationBasisPoints = 10000, IncreasedShieldRegenerationBasisPoints = 20000 }, Rules(5));
        Assert.Equal(100, s.LifeRegenerationPerSecond().Value);
        Assert.Equal(150m, s.ShieldRegenerationPerSecond);
        Assert.Equal(Sheet().MaximumLife().Value, s.MaximumLife().Value);
    }
    [Fact]
    public void MoreAndLessRegenerationMultiplyWithoutChangingRechargeOrMana()
    {
        var baseline = Sheet() with { MaximumShieldRegenerationBasisPoints = 10000 };
        var s = RegenerationMasteryRules.Apply(baseline, Rules(0, 1, 2));
        Assert.Equal(13650, s.LifeRegenerationMultiplierBasisPoints);
        Assert.Equal(14560, s.ShieldRegenerationMultiplierBasisPoints);
        Assert.Equal(baseline.ShieldRecoveryPerSecond().Value, s.ShieldRecoveryPerSecond().Value);
        Assert.Equal(baseline.ManaRegenerationPerSecond().Value, s.ManaRegenerationPerSecond().Value);
        Assert.Equal(9000, MasteryRuntime.ActionSpeedMultiplier(Rules(2), SkillTag.Spell, Weapons.Unequipped));
    }
    [Fact]
    public void HitWindowSelectsOneConditionalMultiplierAndDamageOverTimeDoesNotRefreshIt()
    {
        var state = new CombatConditionState(); state.Damaged(true, 5); state.Damaged(false, 20);
        Assert.Equal(85, state.HitRecentUntil);
        Assert.Equal(140, RegenerationMasteryRules.Recent(Sheet(), Rules(3, 4), 84, state.HitRecentUntil).LifeRegenerationPerSecond().Value);
        Assert.Equal(160, RegenerationMasteryRules.Recent(Sheet(), Rules(3, 4), 85, state.HitRecentUntil).LifeRegenerationPerSecond().Value);
    }
    [Fact]
    public void IncreasedRatesApplyAfterConversionAndDoNotAffectResourceMaximums()
    {
        var s = RegenerationMasteryRules.Apply(Sheet(), Rules(5, 6));
        Assert.Equal(100, s.LifeRegenerationPerSecond().Value);
        Assert.Equal(100m, s.ShieldRegenerationPerSecond);
        Assert.Equal(Sheet().MaximumShield().Value, s.MaximumShield().Value);
    }
}

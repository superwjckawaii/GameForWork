using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;

namespace GameForWork.Tests;

public sealed class ResistanceMasteryTests
{
    private static PassiveModifiers Rules(string group, params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.{group}.{option}")) };
    private static CharacterSheet Sheet() => new(1, new(0, 0, 0, 0), new(100, 100, 100));
    [Fact]
    public void TypedMaximumResistanceNotableChangesCapWithoutGrantingResistance()
    {
        var node = PassiveTree.Nodes.Single(n => n.DisplayName == "三相抗御");
        var p = PassiveModifiers.Empty with { Specialized = node.Effects.ToDictionary(e => e.Kind, e => e.Value) };
        var sheet = MasteryRuntime.ApplySheet(Sheet(), p, Weapons.Unequipped, false);
        Assert.Equal(7800, sheet.ResistanceMaximum(EnemyDamageType.Fire));
        Assert.Equal(0, sheet.FireResistanceBasisPoints);
    }

    [Fact]
    public void ChestMaximumResistanceCapIsUsedByDamageOverTime()
    {
        var sheet = Sheet() with { FireResistanceBasisPoints = 8_100, MaximumFireResistanceBonusBasisPoints = 300 };
        Assert.Equal(8_400, sheet.CappedResistance(sheet.FireResistanceBasisPoints, EnemyDamageType.Fire));
        Assert.Equal(8_400, CombatRules.EffectiveResistance(sheet.FireResistanceBasisPoints, sheet.ResistanceMaximum(EnemyDamageType.Fire)));
    }
    [Fact]
    public void MaximumInheritanceUsesOriginalHighestAndAbsoluteCap()
    {
        var sheet = ResistanceMasteryRules.Apply(Sheet() with { MaximumFireResistanceBonusBasisPoints = 300, MaximumColdResistanceBonusBasisPoints = 700 }, Rules("元素抗性", 0, 1));
        Assert.Equal(8500, sheet.ResistanceMaximum(EnemyDamageType.Fire));
        Assert.Equal(8500, sheet.ResistanceMaximum(EnemyDamageType.Lightning));
        Assert.Equal(-2500, sheet.FireResistanceBasisPoints);
        Assert.Equal(9000, (sheet with { MaximumLightningResistanceBonusBasisPoints = 5000 }).ResistanceMaximum(EnemyDamageType.Cold));
    }
    [Fact]
    public void OverflowAddsPositiveExcessBeforeRoundingAndTracksResistanceChanges()
    {
        var sheet = ResistanceMasteryRules.Apply(Sheet() with { FireResistanceBasisPoints = 7800, ColdResistanceBasisPoints = 7800, LightningResistanceBasisPoints = 0 }, Rules("元素抗性", 2));
        Assert.Equal(400, sheet.ElementalOverflowDefenseIncrease);
        Assert.Equal(0, (sheet with { FireResistanceBasisPoints = 7500 }).ElementalOverflowDefenseIncrease);
        Assert.True(sheet.Armor().Value > (sheet with { ElementalOverflowDefenseRate = 0 }).Armor().Value);
        Assert.True(sheet.Evasion().Value > (sheet with { ElementalOverflowDefenseRate = 0 }).Evasion().Value);
    }
    [Fact]
    public void VoidOverflowDoesNotUseElementalMaximumOrGrantCurrentResistance()
    {
        var sheet = ResistanceMasteryRules.Apply(Sheet() with { VoidResistanceBasisPoints = 8700 }, Rules("虚空抗性", 1, 2));
        Assert.Equal(8000, sheet.ResistanceMaximum(EnemyDamageType.Void));
        Assert.Equal(7200, sheet.ResistanceMaximum(EnemyDamageType.Fire));
        Assert.Equal(600, sheet.VoidOverflowBarrierIncrease);
        Assert.Equal(0, (sheet with { VoidResistanceBasisPoints = 7999 }).VoidOverflowBarrierIncrease);
    }
    [Theory]
    [InlineData(EnemyDamageType.Fire, true, 8500)]
    [InlineData(EnemyDamageType.Fire, false, 8000)]
    [InlineData(EnemyDamageType.Physical, true, 11000)]
    [InlineData(EnemyDamageType.Physical, false, 10000)]
    public void ElementalMitigationSeparatesHitsAndDamageOverTime(EnemyDamageType type, bool hit, int expected) =>
        Assert.Equal(expected, ResistanceMasteryRules.IncomingMultiplier(Sheet(), Rules("元素抗性", 3, 5), type, hit));
    [Fact]
    public void CappedConditionsRequireEveryCurrentResistanceAndStackMultiplicatively()
    {
        var sheet = Sheet() with { FireResistanceBasisPoints = 7500, ColdResistanceBasisPoints = 7500, LightningResistanceBasisPoints = 7500, VoidResistanceBasisPoints = 7500 };
        Assert.Equal(7650, ResistanceMasteryRules.IncomingMultiplier(sheet, Rules("元素抗性", 3, 6), EnemyDamageType.Fire, true));
        Assert.Equal(10000, ResistanceMasteryRules.IncomingMultiplier(sheet with { ColdResistanceBasisPoints = 7499 }, Rules("元素抗性", 6), EnemyDamageType.Fire, true));
        Assert.Equal(7500, ResistanceMasteryRules.IncomingMultiplier(sheet, Rules("虚空抗性", 3, 6), EnemyDamageType.Void, false));
        Assert.Equal(8500, ResistanceMasteryRules.IncomingMultiplier(sheet, Rules("虚空抗性", 3, 6), EnemyDamageType.Void, true));
    }
    [Fact]
    public void RecentWindowsAreIndependentAndVoidBonusIncludesDamageOverTime()
    {
        var state = new CombatConditionState(); state.EnemyHit(EnemyDamageType.Fire, 5); state.EnemyHit(EnemyDamageType.Void, 10);
        Assert.Equal(85, state.ElementalHitRecentUntil); Assert.Equal(90, state.VoidHitRecentUntil);
        Assert.Equal(14000, ResistanceMasteryRules.OutgoingMultiplier(Rules("虚空抗性", 5), DamageType.Void, false, 89, 85, 90));
        Assert.Equal(10000, ResistanceMasteryRules.OutgoingMultiplier(Rules("虚空抗性", 5), DamageType.Void, true, 90, 85, 90));
        Assert.Equal(10000, ResistanceMasteryRules.OutgoingMultiplier(Rules("元素抗性", 4), DamageType.Fire, false, 80, 85, 90));
        Assert.Equal(200, ResistanceMasteryRules.Recovery(Sheet(), Rules("虚空抗性", 4), 89, 90).MaximumLifeRegenerationBasisPoints);
        Assert.Equal(Sheet(), ResistanceMasteryRules.Recovery(Sheet(), Rules("虚空抗性", 4), 90, 90));
    }
}

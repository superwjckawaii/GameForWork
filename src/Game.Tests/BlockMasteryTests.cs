using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;
using GameForWork.Core.Ascendancies;
namespace GameForWork.Tests;
public sealed class BlockMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with { MasteryMechanics = string.Join('|', options.Select(o => $"builds.mastery.rule.格挡.{o}")) };
    [Fact]
    public void TypedSpellBlockAndRecentGuardUseDistinctConsumers()
    {
        var node = PassiveTree.Nodes.Single(n => n.DisplayName == "咒击偏转");
        var p = PassiveModifiers.Empty with { Specialized = node.Effects.ToDictionary(e => e.Kind, e => e.Value) };
        CharacterSheet sheet = new(1, new(0, 0, 0, 0), new(0, 0, 0));
        Assert.Equal(800, MasteryRuntime.ApplySheet(sheet, p, Weapons.Unequipped, false).SpellBlockChanceBasisPoints);
        Assert.Equal(0, MasteryRuntime.ApplySheet(sheet, p, Weapons.Unequipped, false).BlockChanceBasisPoints);
        var guard = PassiveTree.Nodes.Single(n => n.DisplayName == "格后固守");
        p = PassiveModifiers.Empty with { Specialized = guard.Effects.ToDictionary(e => e.Kind, e => e.Value) };
        Assert.Equal(9000, BlockMasteryRules.UnblockedMultiplier(p, 79, 80));
        Assert.Equal(10000, BlockMasteryRules.UnblockedMultiplier(p, 80, 80));
    }
    [Fact]
    public void ChanceAndCapAreSeparateAndPartialBlockKeepsTheLargestRemainingFraction()
    {
        var s = BlockMasteryRules.Apply(new(1, new(0, 0, 0, 0), new(0, 0, 0)), Rules(0, 1));
        Assert.Equal(1000, s.BlockChanceBasisPoints); Assert.Equal(8000, s.MaximumBlockChanceBasisPoints);
        Assert.Equal(1000, s.SpellBlockChanceBasisPoints); Assert.Equal(8000, s.MaximumSpellBlockChanceBasisPoints);
        Assert.Equal(12000, BlockMasteryRules.Chance(Rules(3), 6000));
        Assert.Equal(3500, BlockMasteryRules.RemainingMultiplier(Rules(3), 2000));
        Assert.Equal(5000, BlockMasteryRules.RemainingMultiplier(Rules(3), 5000));
    }
    [Fact]
    public void InheritanceUsesHighestRatioInsteadOfAddingBothSources()
    {
        var p = new CombatProfile(Ascendancy.IronGuardian, [WarriorNodeIds.BastionSpellBlockCore]);
        Assert.Equal(6400, WarriorAscendancyRules.SpellBlockChanceBasisPoints(1000, 9000, p, false, 5000));
        Assert.Equal(5500, WarriorAscendancyRules.SpellBlockChanceBasisPoints(1000, 9000, CombatProfile.Empty, false, 5000));
    }
    [Fact]
    public void AnyBlockRestartsGuaranteeAndChargeIsSingleUseWithIndependentCooldown()
    {
        var state = new CombatConditionState(); var p = Rules(5, 6);
        Assert.False(state.GuaranteedBlock(p, 59)); Assert.True(state.GuaranteedBlock(p, 60));
        state.Blocked(p, 60); Assert.False(state.GuaranteedBlock(p, 119));
        Assert.Equal(10000, state.ConsumeBlockCharge(60, false));
        Assert.Equal(15000, state.ConsumeBlockCharge(61, true));
        state.Blocked(p, 62); Assert.Equal(10000, state.ConsumeBlockCharge(63, true));
        Assert.False(state.GuaranteedBlock(p, 121)); Assert.True(state.GuaranteedBlock(p, 122));
        state.Blocked(p, 82); Assert.Equal(10000, state.ConsumeBlockCharge(162, true));
    }
    [Fact]
    public void RecoveryRefreshesWithoutChangingResourceMaximums()
    {
        CharacterSheet s = new(1, new(0, 0, 0, 0), new(0, 0, 0));
        var actual = BlockMasteryRules.Recovery(s, Rules(4), 79, 80);
        Assert.Equal(200, actual.MaximumLifeRegenerationBasisPoints);
        Assert.Equal(200, actual.MaximumShieldRegenerationBasisPoints);
        Assert.Equal(s.MaximumLife().Value, actual.MaximumLife().Value);
        Assert.Equal(s, BlockMasteryRules.Recovery(s, Rules(4), 80, 80));
    }
}

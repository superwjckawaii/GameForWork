using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Tests;

public sealed class ElementalControlMasteryTests
{
    private static PassiveModifiers Mastery(string group, params int[] options) => PassiveModifiers.Empty with
    {
        MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.{group}.{option}")),
    };

    [Fact]
    public void ChillAndFreezeRulesUseTheirDocumentedControlValues()
    {
        Assert.Equal(5_000, ElementalControlMasteryRules.ChillMaximum(Mastery("冰缓_冻结", 0)));
        Assert.Equal(300, ElementalControlMasteryRules.FreezeThreshold(Mastery("冰缓_冻结", 2), 900));
        Assert.Equal(13_000, ElementalControlMasteryRules.ColdHitMultiplier(Mastery("冰缓_冻结", 3), 10, 20));
        Assert.Equal(10_000, ElementalControlMasteryRules.ColdHitMultiplier(Mastery("冰缓_冻结", 3), 20, 20));
        Assert.Equal(1_500, ElementalControlMasteryRules.ColdGroundChill(Mastery("冰缓_冻结", 4)));
        Assert.True(ElementalControlMasteryRules.CanSpreadChill(Mastery("冰缓_冻结", 5), 1_500, 10, 20, false));
        Assert.False(ElementalControlMasteryRules.CanSpreadChill(Mastery("冰缓_冻结", 5), 1_500, 10, 20, true));
        Assert.Equal(800, ElementalControlMasteryRules.FrozenExplosionDamage(Mastery("冰缓_冻结", 6), 10_000, true));
    }

    [Fact]
    public void ShockAndParalysisAlternativesAreMutuallyExclusive()
    {
        PassiveModifiers paralysis = Mastery("感电_麻痹", 2, 3, 6);
        PassiveModifiers shock = Mastery("感电_麻痹", 0, 1, 4, 5);

        Assert.False(ElementalControlMasteryRules.CanShock(paralysis));
        Assert.Equal(10_000, ElementalControlMasteryRules.ParalysisChance(paralysis, 0));
        Assert.Equal(20_000, ElementalControlMasteryRules.ParalysisAccumulationIncrease(paralysis));
        Assert.Equal(500, ElementalControlMasteryRules.LightningThreshold(paralysis, 1_000, false));
        Assert.Equal(100, ElementalControlMasteryRules.ParalysisDecayDelayTicks(paralysis));
        Assert.False(ElementalControlMasteryRules.CanParalyze(shock));
        Assert.Equal(10_000, ElementalControlMasteryRules.ShockMaximum(shock, 5_000));
        Assert.Equal(1_500, ElementalControlMasteryRules.ShockEffect(shock, 600));
        Assert.Equal(1_500, ElementalControlMasteryRules.ShockEffect(shock, 0));
        Assert.Equal(14_000, ElementalControlMasteryRules.HitMultiplier(shock, 10, 20));
    }

    [Fact]
    public void ElementalControlMasteriesDoNotInjectFallbackStats()
    {
        foreach (string group in new[] { "冰缓_冻结", "感电_麻痹" })
        {
            PassiveNodeDefinition node = PassiveTree.Nodes.First(candidate => candidate.MasteryGroup == $"builds.mastery.{group}");
            Assert.All(PassiveTree.MasteryOptions(node), effect => Assert.Equal(0, effect.Value));
        }
    }
}

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
    public void ShockCapRemainsBoundedWhenCombinedWithAscendancy()
    {
        Assert.Equal(10_000, ElementalControlMasteryRules.ShockMaximum(Mastery("感电_麻痹", 0), 7_500));
        int raw = CombatRules.Shock(100, 100, 10_000, 12_500).EffectBasisPoints;
        Assert.Equal(10_000, ElementalControlMasteryRules.ShockEffect(Mastery("感电_麻痹", 4), raw));
    }

    [Fact]
    public void LightningModesReplacePreviousChoiceAndRestoreTheLastSelection()
    {
        string[] nodes = PassiveTree.Nodes.Where(n => n.Kind == PassiveNodeKind.Mastery && n.MasteryGroup == "builds.mastery.感电_麻痹")
            .Take(2).Select(n => n.StableId).ToArray();
        var allocation = new PassiveTreeAllocation();
        foreach (string node in nodes) Assert.True(allocation.TryAllocatePath(node, 149));
        Assert.True(allocation.TrySelectMastery(nodes[0], 2));
        Assert.True(allocation.TrySelectMastery(nodes[1], 4));
        Assert.Equal(4, Assert.Single(allocation.MasterySelections).Value);
        var restored = PassiveTreeAllocation.Restore(allocation.Allocated, 0,
            new Dictionary<string, int> { [nodes[0]] = 2, [nodes[1]] = 4 });
        Assert.Equal(4, Assert.Single(restored.MasterySelections).Value);
    }

    [Fact]
    public void ChillAndFreezeRulesUseTheirDocumentedControlValues()
    {
        Assert.Equal(5_000, ElementalControlMasteryRules.ChillMaximum(Mastery("冰缓_冻结", 0)));
        Assert.Equal(2_700, ElementalControlMasteryRules.FreezeDamage(Mastery("冰缓_冻结", 2), 900));
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
        Assert.Equal(2_000, ElementalControlMasteryRules.LightningDamage(paralysis, 1_000, false));
        Assert.Equal(1_000, ElementalControlMasteryRules.LightningDamage(paralysis, 1_000, true));
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

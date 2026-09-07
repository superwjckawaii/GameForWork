using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Tests;

public sealed class VoidDebuffMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.侵蚀_凋零.{option}")) };

    [Fact]
    public void CorrosionNotableAndMasteryReachHardCapTogether()
    {
        var node = PassiveTree.Nodes.Single(n => n.DisplayName == "蚀界之痕");
        var profile = Rules(0) with { Specialized = node.Effects.ToDictionary(e => e.Kind, e => e.Value) };
        Assert.Equal(10, VoidDebuffMasteryRules.Maximum(profile, Ailment.Erosion));
        Assert.Equal(10_000, profile.SpecializedValue(PassiveEffectKind.VoidHitErosionChanceBasisPoints));
        Assert.True(CombatRules.WitherMultiplier(15, 15) > CombatRules.WitherMultiplier(10, 15));
    }

    [Fact]
    public void ExtraLayerIsPerActionTargetAndKindAndDoesNotGrantOtherDebuff()
    {
        var state = new AilmentState();
        state.ApplyVoidDebuff(Rules(2), Ailment.Erosion, 0, "one", true);
        Assert.Equal(2, state.Stack(Ailment.Erosion, 0));
        Assert.Equal(0, state.Stack(Ailment.Wither, 0));
        state.ApplyVoidDebuff(Rules(2), Ailment.Erosion, 1, "one", true);
        Assert.Equal(3, state.Stack(Ailment.Erosion, 1));
        state.ApplyVoidDebuff(Rules(2), Ailment.Wither, 1, "one", true);
        Assert.Equal(2, state.Stack(Ailment.Wither, 1));
        state.ApplyVoidDebuff(Rules(2), Ailment.Wither, 1, "trigger", false);
        Assert.Equal(3, state.Stack(Ailment.Wither, 1));
    }

    [Theory]
    [InlineData(Ailment.Erosion, 8, 360)]
    [InlineData(Ailment.Wither, 15, 240)]
    public void CapsAndDurationApplyWithoutCreatingPermanentStacks(Ailment kind, int maximum, int expires)
    {
        var state = new AilmentState();
        for (int i = 0; i < 20; i++) state.ApplyVoidDebuff(Rules(0, 1, 3), kind, 0, i.ToString(), true);
        Assert.Equal(maximum, state.Stack(kind, expires - 1));
        Assert.Equal(0, state.Stack(kind, expires));
    }

    [Fact]
    public void PropagationPreservesExpiryDoesNotRecurseAndCannotRetryAvoidance()
    {
        var source = new AilmentState();
        source.AddStack(Ailment.Wither, 10, 15, 80, 0);
        var target = new AilmentState();
        source.SpreadDebuffsTo(target, Rules(), 20);
        Assert.Equal(5, target.Stack(Ailment.Wither, 79));
        source.SpreadDebuffsTo(target, Rules(), 40);
        Assert.Equal(5, target.Stack(Ailment.Wither, 79));
        var third = new AilmentState();
        target.SpreadDebuffsTo(third, Rules(), 40);
        Assert.Equal(0, third.Stack(Ailment.Wither, 40));
        Assert.Equal(0, target.Stack(Ailment.Wither, 80));
        var avoided = new AilmentState();
        source.SpreadDebuffsTo(avoided, Rules(), 20, () => false);
        source.SpreadDebuffsTo(avoided, Rules(), 20);
        Assert.Equal(0, avoided.Stack(Ailment.Wither, 20));
    }

    [Fact]
    public void TargetBonusesUseCurrentStacksAndDisappearAfterConsumption()
    {
        var target = new AilmentState();
        target.AddStack(Ailment.Erosion, 5, 5, 120, 0);
        target.AddStack(Ailment.Wither, 10, 10, 80, 0);
        Assert.Equal(14_000, VoidDebuffMasteryRules.HitMultiplier(Rules(5), target, 0));
        Assert.Equal(8_500, VoidDebuffMasteryRules.IncomingHitMultiplier(Rules(6), target, 0));
        Assert.Equal(1, target.ConsumeStacks(Ailment.Wither, 1, 0));
        Assert.Equal(10_000, VoidDebuffMasteryRules.HitMultiplier(Rules(5), target, 0));
        Assert.Equal(10_000, VoidDebuffMasteryRules.IncomingHitMultiplier(Rules(6), target, 0));
    }
}

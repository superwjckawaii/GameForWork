using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;
public sealed class SpiritBarrierMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.灵障.{option}")) };
    private static CharacterSheet Sheet() => new(1, new(0, 0, 0, 0), new(100, 0, 100), FlatSpiritBarrier: 4998);
    [Fact]
    public void TypedNotableMoreMultipliesExistingIncreasedBarrier()
    {
        var node = PassiveTree.Nodes.Single(n => n.DisplayName == "贯通灵壁");
        var p = PassiveModifiers.Empty with { Specialized = node.Effects.ToDictionary(e => e.Kind, e => e.Value) };
        var sheet = MasteryRuntime.ApplySheet(Sheet() with { IncreasedSpiritBarrierBasisPoints = 10000 }, p, Weapons.Unequipped, false);
        Assert.Equal(14000, sheet.SpiritBarrier().Value);
    }
    [Fact]
    public void FinalBarrierIncludesMoreModifiersAndOverflowBeforeUncappedDamageIncrease()
    {
        var s = MasteryRuntime.ApplySheet(Sheet(), Rules(0, 1), Weapons.Unequipped, false);
        Assert.Equal(9750, s.SpiritBarrier().Value);
        Assert.Equal(77600, SpiritBarrierMasteryRules.DamageIncrease(s, Rules(6)));
        Assert.Equal(80, s.Armor().Value);
        Assert.Equal(11500, MasteryRuntime.ShieldMultiplier(Rules(1)));
    }
    [Fact]
    public void LowLifeAndQuietWindowAreIndependentAndDoNotAccumulateAcrossRefreshes()
    {
        var s = SpiritBarrierMasteryRules.Dynamic(Sheet(), Rules(3, 5), true, 80, 80);
        Assert.Equal(18000, s.SpiritBarrier().Value);
        Assert.Equal(s.SpiritBarrier().Value, SpiritBarrierMasteryRules.Dynamic(s, Rules(3, 5), true, 81, 80).SpiritBarrier().Value);
        Assert.Equal(5000, SpiritBarrierMasteryRules.Dynamic(s, Rules(3, 5), false, 79, 80).SpiritBarrier().Value);
    }
    [Fact]
    public void DamageOverTimeRecoveryHasItsOwnOneSecondWindow()
    {
        var state = new CombatConditionState(); state.Damaged(false, 7); state.Damaged(true, 10);
        Assert.Equal(27, state.DamageOverTimeRecentUntil);
        Assert.Equal(150, SpiritBarrierMasteryRules.Recovery(Sheet(), Rules(4), 26, state.DamageOverTimeRecentUntil).MaximumShieldRegenerationBasisPoints);
        Assert.Equal(Sheet(), SpiritBarrierMasteryRules.Recovery(Sheet(), Rules(4), 27, state.DamageOverTimeRecentUntil));
    }
    [Fact]
    public void OffenseIsAdditiveAndOnlyAppliesToDamageOverTime()
    {
        var build = new TeamBuild(Sheet(), Weapons.Unequipped, new(SkillIds.HeavyStrike, SkillSupport.None), PassiveProfile: Rules(6));
        var baseline = build with { PassiveProfile = PassiveModifiers.Empty };
        Assert.Equal(CombatSkillRules.OffensiveIncreases(baseline, SkillTag.Spell).InitialIncreasedBasisPoints, CombatSkillRules.OffensiveIncreases(build, SkillTag.Spell).InitialIncreasedBasisPoints);
        Assert.Equal(40000, CombatSkillRules.OffensiveIncreases(build, SkillTag.Spell, true).InitialIncreasedBasisPoints - CombatSkillRules.OffensiveIncreases(baseline, SkillTag.Spell, true).InitialIncreasedBasisPoints);
        Assert.Equal(120, MasteryRuntime.ManaCost(Rules(2), SkillTag.Spell, 100));
        var hero = new ResourceState(Sheet());
        Assert.Equal(8500, MasteryRuntime.IncomingResourceMultiplier(Rules(2), hero, false));
        Assert.Equal(10000, MasteryRuntime.IncomingResourceMultiplier(Rules(2), hero, true));
    }
}

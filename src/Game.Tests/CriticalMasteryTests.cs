using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Skills;
using GameForWork.Core.Simulation;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class CriticalMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.暴击.{option}")) };
    [Fact]
    public void AxeCriticalRestrictionDoesNotDisableSpellsOrOtherWeapons()
    {
        var p = PassiveModifiers.Empty with { MasteryMechanics = "builds.mastery.rule.斧类.0" };
        WeaponProfile axe = new("core.base.heavy_battleaxe", 10, 20, 1000, 500);
        Assert.True(MasteryRuntime.CannotCrit(p, SkillTag.Attack, axe));
        Assert.False(MasteryRuntime.CannotCrit(p, SkillTag.Spell, axe));
        Assert.False(MasteryRuntime.CannotCrit(p, SkillTag.Attack, Weapons.Unequipped));
        Assert.False(MasteryRuntime.CannotCrit(p));
    }
    [Fact]
    public void LuckyChanceIsTwoTrialsAndNeverDuplicatesDamage()
    {
        Assert.Equal(7_500, CriticalMasteryRules.ExpectedChance(Rules(1), 5_000));
        var expected = new Pcg32(731);
        bool first = expected.NextBasisPoints() < 5_000, second = expected.NextBasisPoints() < 5_000;
        var actual = new Pcg32(731);
        Assert.Equal(first || second, CriticalMasteryRules.Roll(Rules(1), 5_000, actual));
        Assert.Equal(expected.NextUInt(), actual.NextUInt());
    }
    [Fact]
    public void BaseChanceAndMultiplierUseSeparatePools()
    {
        var config = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        var build = Build(Rules(0, 2, 4)) with { IncreasedCriticalChanceBasisPoints = 10_000 };
        Assert.Equal(2_000, CriticalHitRules.Chance(build, CombatSkillRules.Resolve(config, 100), config, 0));
        Assert.Equal(20_000, CriticalHitRules.Multiplier(build, config));
        Assert.Equal(10_000, CriticalMasteryRules.AilmentMultiplierBonus(Rules(4)));
        Assert.Equal(7_500, CriticalMasteryRules.AilmentDamageMultiplier(Rules(2)));
    }
    [Fact]
    public void CriticalChanceConversionIsIncreasedHitDamageAndCannotCrit()
    {
        var build = Build(Rules(6)) with { IncreasedCriticalChanceBasisPoints = 40_000 };
        Assert.Equal(30_000, CriticalMasteryRules.ConvertedHitIncrease(Rules(6), 40_000));
        Assert.Equal(0, CriticalHitRules.Chance(build, CombatSkillRules.Resolve(build.HeavyStrike, 100), build.HeavyStrike, 0));
        var before = CombatSkillRules.OffensiveIncreases(Build(Rules()), SkillTag.Attack);
        var after = CombatSkillRules.OffensiveIncreases(build, SkillTag.Attack);
        Assert.Equal(before.InitialIncreasedBasisPoints + 30_000, after.InitialIncreasedBasisPoints);
        Assert.Equal(0, CriticalMasteryRules.ConvertedHitIncrease(Rules(), 40_000));
    }
    [Fact]
    public void PursuitRequiresBothCriticalHitAndCurrentControl()
    {
        Assert.Equal(13_000, CriticalMasteryRules.TargetMultiplier(Rules(3), true, true));
        Assert.Equal(10_000, CriticalMasteryRules.TargetMultiplier(Rules(3), false, true));
        Assert.Equal(10_000, CriticalMasteryRules.TargetMultiplier(Rules(3), true, false));
    }
    [Fact]
    public void KillBonusRefreshesWithoutStacking()
    {
        var state = new CombatConditionState(); state.Killed(5); state.Killed(10);
        Assert.Equal(15_000, CriticalMasteryRules.RecentChanceIncrease(Rules(5), 89, state.KillRecentUntil));
        Assert.Equal(5_000, CriticalMasteryRules.RecentMultiplierBonus(Rules(5), 89, state.KillRecentUntil));
        Assert.Equal(0, CriticalMasteryRules.RecentChanceIncrease(Rules(5), 90, state.KillRecentUntil));
    }
    [Fact]
    public void MultiplierMasteryIncreasesActualCriticalHits()
    {
        var baseline = Run(Rules()).Events.First(e => e.Kind == SpatialEventKind.HeavyStrike && e.Value > 0);
        var enhanced = Run(Rules(2)).Events.First(e => e.Kind == SpatialEventKind.HeavyStrike && e.Value > 0);
        Assert.Contains("critical", baseline.Detail);
        Assert.True(enhanced.Value > baseline.Value);
    }
    private static TeamBuild Build(PassiveModifiers rules)
    {
        var config = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        return new(new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000, FlatMaximumMana: 100_000),
            new("test", 100, 100, 2_000, 800), config, UseWarCry: false, AlwaysHit: true, ActiveSkills: [config], PassiveProfile: rules);
    }
    private static NodeCombatResult Run(PassiveModifiers rules) => new SpatialCombatRunner().Run(
        new(Build(rules) with { IncreasedCriticalChanceBasisPoints = 120_000 }, 1, 1, 1, false, false, false, 0, MaximumTicks: 140,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 100_000, Armor = 0, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
}

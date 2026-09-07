using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class SuppressionMasteryTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.法术压制.{option}")) };
    [Fact]
    public void ChanceAndEffectChangesAreAdditiveAndSeparate()
    {
        var sheet = MasteryRuntime.ApplySheet(Sheet() with { SpellSuppressionBasisPoints = 7_000 }, Rules(0, 1), Weapons.Unequipped, false);
        Assert.Equal(7_000, sheet.SpellSuppressionBasisPoints);
        Assert.Equal(8_500, sheet.SpellSuppressionEffectBasisPoints);
        Assert.Equal(9_100, SuppressionMasteryRules.ExpectedChance(Rules(2), sheet.SpellSuppressionBasisPoints));
    }
    [Fact]
    public void OverflowUsesRawChanceAndWholePercentagePoints()
    {
        Assert.Equal(0, SuppressionMasteryRules.OverflowHitIncrease(Rules(2, 6), 10_000));
        Assert.Equal(600, SuppressionMasteryRules.OverflowHitIncrease(Rules(6), 10_299));
        Assert.Equal(15_000, SuppressionMasteryRules.OverflowHitIncrease(Rules(6), 15_000));
    }
    [Fact]
    public void RecoveryAndPursuitRefreshWithoutStacking()
    {
        var state = new CombatConditionState(); state.Suppressed(5); state.Suppressed(10);
        var sheet = SuppressionMasteryRules.Recovery(Sheet(), Rules(4), 89, state.SuppressionRecentUntil);
        Assert.Equal(200, sheet.MaximumLifeRegenerationBasisPoints);
        Assert.Equal(200, sheet.MaximumShieldRegenerationBasisPoints);
        Assert.Equal(13_500, SuppressionMasteryRules.HitMultiplier(Rules(5), 89, state.SuppressionRecentUntil));
        Assert.Equal(Sheet(), SuppressionMasteryRules.Recovery(Sheet(), Rules(4), 90, state.SuppressionRecentUntil));
    }
    [Fact]
    public void FullyBlockedSpellsDoNotGrantSuppressionRewards()
    {
        var result = Run(Rules(4, 5), 10_000, true);
        Assert.Contains(result.Events, e => e.Kind == SpatialEventKind.Block && e.Detail == "spell");
        Assert.DoesNotContain(result.Events, e => e.Detail == "spell_suppression");
        Assert.Equal(50_000, result.HeroLife);
    }
    [Fact]
    public void UnblockedSpellsActuallySuppressAndGrantRecovery()
    {
        var baseline = Run(Rules(), 10_000);
        var recovery = Run(Rules(4), 10_000);
        Assert.Contains(recovery.Events, e => e.Detail == "spell_suppression");
        Assert.True(recovery.HeroLife > baseline.HeroLife);
        Assert.True(Run(Rules(3), 0).HeroLife > Run(Rules(), 0).HeroLife);
    }
    private static CharacterSheet Sheet() => new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000, FlatMaximumMana: 100_000);
    private static NodeCombatResult Run(PassiveModifiers rules, int chance, bool block = false)
    {
        var config = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        var sheet = Sheet() with { SpellSuppressionBasisPoints = chance, SpellBlockChanceBasisPoints = block ? 10_000 : 0, MaximumSpellBlockChanceBasisPoints = 10_000 };
        var build = new TeamBuild(sheet, new("test", 1, 1, 1_000, 0), config, UseWarCry: false, AlwaysHit: true, ActiveSkills: [config], PassiveProfile: rules);
        var enemy = Enemies.CorruptedWorker with
        {
            Life = 100_000,
            MinimumPhysicalDamage = 100,
            MaximumPhysicalDamage = 100,
            Skills = [new(EnemySkillKind.BasicStrike, "spell", EnemyDamageType.Lightning, 10_000, RangeRaw: 8_000, IsSpell: true, Avoidable: false)]
        };
        return new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, InitialHeroLife: 50_000, MaximumTicks: 140, EnemyPool: [enemy]), 731);
    }
}

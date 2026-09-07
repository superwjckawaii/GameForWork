using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Skills;
namespace GameForWork.Tests;

public sealed class SkillSpeedTests
{
    private static TeamBuild Build() => new(new(1, new(0, 0, 0, 0), new(0, 0, 0)),
        Weapons.RustedGreatsword, new(SkillIds.HeavyStrike, SkillSupport.None),
        IncreasedAttackSpeedBasisPoints: 5_000, IncreasedCastSpeedBasisPoints: 5_000);

    [Theory]
    [InlineData(1, 0, 2_000)]
    [InlineData(21, 20, 5_000)]
    public void AttackSupportUsesOwnLevelAndQualityWithoutChangingCooldown(int level, int quality, int increase)
    {
        var plain = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None, Level: 7);
        var linked = plain with { Supports = SkillSupport.AttackSpeed, SupportLinks = [new(ActiveSkillCatalog.SupportFor(SkillSupport.AttackSpeed).StoneId, level, quality)] };
        var normal = CombatSkillRules.Resolve(plain, 1_000);
        var fast = CombatSkillRules.Resolve(linked, 1_000);
        Assert.Equal(increase, fast.AdditionalAttackSpeedBasisPoints);
        Assert.Equal(normal.CooldownTicks, fast.CooldownTicks);
        Assert.Equal(normal.CastTimeTicks, fast.CastTimeTicks);
        Assert.Equal(20 * 10_000 / (15_000 + increase), CombatSkillRules.ActionDelay(Build(), fast with { CastTimeTicks = 20 }, SkillTag.Attack));
    }

    [Theory]
    [InlineData(1, 0, 2_000)]
    [InlineData(21, 20, 5_000)]
    public void CastSupportAddsToExistingSpeedBeforeRounding(int level, int quality, int increase)
    {
        var plain = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None, Level: 7);
        var linked = plain with { Supports = SkillSupport.FasterCasting, SupportLinks = [new(ActiveSkillCatalog.SupportFor(SkillSupport.FasterCasting).StoneId, level, quality)] };
        var normal = CombatSkillRules.Resolve(plain, 1_000);
        var fast = CombatSkillRules.Resolve(linked, 1_000);
        Assert.Equal(increase, fast.AdditionalCastSpeedBasisPoints);
        Assert.Equal(normal.CastTimeTicks, fast.CastTimeTicks);
        Assert.Equal(normal.CooldownTicks, fast.CooldownTicks);
        Assert.Equal(20 * 10_000 / (15_000 + increase), CombatSkillRules.ActionDelay(Build(), fast with { CastTimeTicks = 20 }, SkillTag.Spell));
    }

    [Fact]
    public void HeavyStrikeConfigurationSharesResolvedCostRangeAndLinkedSpeed()
    {
        var config = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.AttackSpeed | SkillSupport.LifeCost, Level: 21,
            SupportLinks: [new(ActiveSkillCatalog.SupportFor(SkillSupport.AttackSpeed).StoneId, 1, 0),
                new(ActiveSkillCatalog.SupportFor(SkillSupport.LifeCost).StoneId, 21, 20)]);
        var resolved = CombatSkillRules.Resolve(config, 1_000);
        var profile = SkillRules.BuildHeavyStrike(config, Weapons.RustedGreatsword, 1_000, 5_000);
        Assert.Equal(resolved.LifeCost, profile.LifeCost);
        Assert.Equal(resolved.RangeRaw, profile.RangeRaw);
        Assert.Equal(7_000, profile.IncreasedAttackSpeedBasisPoints);
        Assert.Equal(SkillRules.BuildHeavyStrike(config, Weapons.RustedGreatsword, 100_000, 5_000).LifeCost, profile.LifeCost);
    }
}



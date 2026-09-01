using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P4;
using GameForWork.Core.P6;
using GameForWork.Core.P17;

namespace GameForWork.Tests;

public sealed class P17FeatureTests
{
    [Fact]
    public void CatalogHasThirtyActivesAndFortyEightSupportsWithUniqueIds()
    {
        Assert.Equal(30, P17SkillCatalog.Active.Count);
        Assert.Equal(48, P17SkillCatalog.Supports.Count);
        Assert.Equal(30, P17SkillCatalog.Active.Select(item => item.SkillId).Distinct().Count());
        Assert.Equal(48, P17SkillCatalog.Supports.Select(item => item.StoneId).Distinct().Count());
    }

    [Fact]
    public void CapabilityAndConflictRulesRejectIllegalSupportGroups()
    {
        SkillStoneDefinition heavy = P2SkillStones.Get("core.skill_stone.heavy_strike");
        SkillStoneDefinition brutality = P2SkillStones.Get("core.skill_stone.brutality");
        SkillStoneDefinition addedFire = P2SkillStones.Get("core.skill_stone.added_fire");
        SkillStoneDefinition projectiles = P2SkillStones.Get("core.skill_stone.multiple_projectiles");

        Assert.True(P6SkillCompatibility.Check(heavy, brutality).Compatible);
        Assert.False(P6SkillCompatibility.Check(heavy, projectiles).Compatible);
        Assert.False(P6SkillCompatibility.CheckGroup(heavy, addedFire, [brutality]).Compatible);
    }

    [Fact]
    public void DamageConversionUsesFixedOrderAndVoidResistance()
    {
        SkillSupport conversions = SkillSupport.PhysicalToLightning | SkillSupport.LightningToCold |
                                   SkillSupport.ColdToFire | SkillSupport.FireToVoid;
        P17DamageBreakdown result = P17DamageRules.Resolve(1_600, P17DamageType.Physical, conversions,
            targetArmor: 0, fireResistance: 0, coldResistance: 0, lightningResistance: 0, voidResistance: 5_000);

        Assert.Equal(0, result.Physical);
        Assert.Equal(800, result.Lightning);
        Assert.Equal(400, result.Cold);
        Assert.Equal(200, result.Fire);
        Assert.Equal(100, result.Void);
        Assert.Equal(1_500, result.Total);
        Assert.Contains(result.Trace, line => line.Contains("convert:support.physical_to_lightning", StringComparison.Ordinal));
        Assert.Contains(result.Trace, line => line.Contains("convert:support.physical_to_cold", StringComparison.Ordinal));
    }

    [Fact]
    public void NewGenericSkillExecutesAndEmitsTypedCombatDetail()
    {
        var build = new P1TeamBuild(
            new CharacterSheet(30, new CharacterAttributes(200, 150, 150, 150),
                new DefensiveEquipment(600, 150, 200), FlatMaximumLife: 1_200),
            new WeaponProfile("p17.test", 180, 220, 1_500, 800),
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None),
            FlatAccuracy: 1_500,
            IncreasedDamageBasisPoints: 4_000,
            ActiveSkills:
            [
                new SkillConfiguration(P1SkillIds.ChainLightning, SkillSupport.FasterCasting, Priority: 1),
            ]);

        P4NodeCombatResult result = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            build, 1, 4, 4, false, false, false, 0, MaximumTicks: 300), 1717);

        P4SpatialEvent effect = Assert.Single(result.Events.Where(item =>
            item.Kind == P4SpatialEventKind.SkillEffect && item.Detail.Contains(P1SkillIds.ChainLightning)).Take(1));
        Assert.Contains("damage:", effect.Detail);
        Assert.Contains("lightning:", effect.Detail);
    }

    [Fact]
    public void ShieldRequiredSkillIsNotUsedWithoutShield()
    {
        var build = new P1TeamBuild(
            new CharacterSheet(20, new CharacterAttributes(150, 100, 100, 100),
                new DefensiveEquipment(400, 100, 100), FlatMaximumLife: 900),
            new WeaponProfile("p17.no-shield", 100, 120, 1_300, 500),
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None),
            ActiveSkills: [new SkillConfiguration(P1SkillIds.IronGuard, SkillSupport.None)],
            HasShield: false);

        P4NodeCombatResult result = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            build, 1, 2, 2, false, false, false, 0, MaximumTicks: 80), 88);

        Assert.DoesNotContain(result.Events, item => item.Kind == P4SpatialEventKind.Guard);
    }

    [Fact]
    public void PartialRewardsScaleByDefeatedEnemiesAndExcludeCompletionRewards()
    {
        P1MapRewards rewards = P1MapRewardGenerator.GeneratePartial(
            new P1MapItem("p17-partial", 5), MapRoute.Safe, 99, defeatedEnemies: 3, totalEnemies: 12);

        Assert.Equal(47, rewards.Experience);
        Assert.True(rewards.Trace!.DefeatedEnemies > 0);
        Assert.Empty(rewards.Maps);
        Assert.True(rewards.Stackables.Gold > 0);
        Assert.Equal(0, rewards.Stackables.MemoryAshes);
        Assert.Equal(0, rewards.Stackables.WardenMarks);
    }
}

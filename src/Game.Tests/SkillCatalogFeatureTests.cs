using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Spatial;
using GameForWork.Core.Skills;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Tests;

public sealed class SkillCatalogFeatureTests
{
    [Fact]
    public void CatalogHasThirtyActivesAndFortyEightSupportsWithUniqueIds()
    {
        Assert.Equal(30, CoreSkillDefinitions.Active.Count);
        Assert.Equal(48, CoreSkillDefinitions.Supports.Count);
        Assert.Equal(30, CoreSkillDefinitions.Active.Select(item => item.SkillId).Distinct().Count());
        Assert.Equal(48, CoreSkillDefinitions.Supports.Select(item => item.StoneId).Distinct().Count());
    }

    [Fact]
    public void CapabilityAndConflictRulesRejectIllegalSupportGroups()
    {
        SkillStoneDefinition heavy = SkillStoneCatalog.Get("core.skill_stone.heavy_strike");
        SkillStoneDefinition brutality = SkillStoneCatalog.Get("core.skill_stone.brutality");
        SkillStoneDefinition addedFire = SkillStoneCatalog.Get("core.skill_stone.added_fire");
        SkillStoneDefinition projectiles = SkillStoneCatalog.Get("core.skill_stone.multiple_projectiles");

        Assert.True(SkillCompatibility.Check(heavy, brutality).Compatible);
        Assert.False(SkillCompatibility.Check(heavy, projectiles).Compatible);
        Assert.False(SkillCompatibility.CheckGroup(heavy, addedFire, [brutality]).Compatible);
    }

    [Fact]
    public void DamageConversionUsesFixedOrderAndVoidResistance()
    {
        SkillSupport conversions = SkillSupport.PhysicalToLightning | SkillSupport.LightningToCold |
                                   SkillSupport.ColdToFire | SkillSupport.FireToVoid;
        DamageBreakdown result = DamagePacketRules.Resolve(1_600, SkillDamageType.Physical, conversions,
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
        var build = new TeamBuild(
            new CharacterSheet(30, new CharacterAttributes(200, 150, 150, 150),
                new DefensiveEquipment(600, 150, 200), FlatMaximumLife: 1_200),
            new WeaponProfile("skillCatalog.test", 180, 220, 1_500, 800),
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None),
            FlatAccuracy: 1_500,
            IncreasedDamageBasisPoints: 4_000,
            ActiveSkills:
            [
                new SkillConfiguration(SkillIds.ChainLightning, SkillSupport.FasterCasting, Priority: 1),
            ]);

        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            build, 1, 4, 4, false, false, false, 0, MaximumTicks: 300), 1717);

        SpatialEvent effect = Assert.Single(result.Events.Where(item =>
            item.Kind == SpatialEventKind.SkillEffect && item.Detail.Contains(SkillIds.ChainLightning)).Take(1));
        Assert.Contains("damage:", effect.Detail);
        Assert.Contains("lightning:", effect.Detail);
    }

    [Fact]
    public void LocalElementalWeaponDamageEntersLiveAttackPacket()
    {
        var weapon = new WeaponProfile("skillCatalog.local-elemental", 80, 80, 1_500, 0);
        var local = new LocalWeaponStats(weapon, new LocalDamageRange(120, 120), default, default, default);
        var skill = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        var build = new TeamBuild(
            new CharacterSheet(30, new CharacterAttributes(200, 150, 150, 150),
                new DefensiveEquipment(600, 150, 200), FlatMaximumLife: 1_200),
            weapon, skill, FlatAccuracy: 5_000, ActiveSkills: [skill], LocalWeaponStats: local);

        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            build, 1, 4, 1, false, false, false, 0, MaximumTicks: 300), 1_718);

        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.HeavyStrike &&
            item.Detail.Contains("fire:", StringComparison.Ordinal) &&
            !item.Detail.Contains("fire:0", StringComparison.Ordinal));
    }

    [Fact]
    public void ShieldRequiredSkillIsNotUsedWithoutShield()
    {
        var build = new TeamBuild(
            new CharacterSheet(20, new CharacterAttributes(150, 100, 100, 100),
                new DefensiveEquipment(400, 100, 100), FlatMaximumLife: 900),
            new WeaponProfile("skillCatalog.no-shield", 100, 120, 1_300, 500),
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None),
            ActiveSkills: [new SkillConfiguration(SkillIds.IronGuard, SkillSupport.None)],
            HasShield: false);

        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            build, 1, 2, 2, false, false, false, 0, MaximumTicks: 80), 88);

        Assert.DoesNotContain(result.Events, item => item.Kind == SpatialEventKind.Guard);
    }

    [Fact]
    public void PartialRewardsScaleByDefeatedEnemiesAndExcludeCompletionRewards()
    {
        MapRewards rewards = MapRewardGenerator.GeneratePartial(
            new MapItem("skillCatalog-partial", 5), MapRoute.Safe, 99, defeatedEnemies: 3, totalEnemies: 12);

        Assert.Equal(47, rewards.Experience);
        Assert.True(rewards.Trace!.DefeatedEnemies > 0);
        Assert.Empty(rewards.Maps);
        Assert.True(rewards.Stackables.Gold > 0);
        Assert.Equal(0, rewards.Stackables.MemoryAshes);
        Assert.Equal(0, rewards.Stackables.WardenMarks);
    }
}

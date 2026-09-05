using GameForWork.Core.Campaign.World;
using GameForWork.Core.Campaign;
using GameForWork.Core.Endgame;
using GameForWork.Core.Maps;
using GameForWork.Core.Atlas;
using GameForWork.Core.Characters;

namespace GameForWork.Tests;

public sealed class AtlasFeatureTests
{
    [Fact]
    public void CatalogContainsTwelvePrefixesTwelveSuffixesAndFourRankValues()
    {
        Assert.Equal(24, MapAffixCatalog.All.Count);
        Assert.Equal(12, MapAffixCatalog.Prefixes.Count);
        Assert.Equal(12, MapAffixCatalog.Suffixes.Count);
        Assert.All(MapAffixCatalog.All, definition => Assert.Equal(4, definition.Values.Count));
        Assert.DoesNotContain(MapAffixCatalog.All, definition => definition.DisplayName.Contains("反射", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(MapRarity.Basic, 0, 0)]
    [InlineData(MapRarity.Magic, 1, 1)]
    [InlineData(MapRarity.Rare, 2, 2)]
    public void MapRarityAlwaysRollsTheFixedPrefixSuffixStructure(MapRarity rarity, int prefixes, int suffixes)
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            IReadOnlyList<MapAffix> affixes = MapGenerationRules.RollAffixes(rarity, 20, seed);
            Assert.Equal(prefixes, affixes.Count(affix => affix.Family == MapAffixFamily.DangerousPrefix));
            Assert.Equal(suffixes, affixes.Count(affix => affix.Family == MapAffixFamily.RewardSuffix));
            Assert.All(affixes, affix => Assert.Equal(4, affix.Rank));
            Assert.DoesNotContain(affixes.GroupBy(affix => MapAffixCatalog.Get(affix.Kind).Group),
                group => group.Key != MapAffixGroup.None && group.Count() > 1);
        }
    }

    [Fact]
    public void FilterUsesAndAcrossFieldsOrAcrossAreasAndStrictAffixRules()
    {
        MapItem map = new MapItem("filter-map", 12, Rarity: MapRarity.Rare,
            Quality: 20, Affixes: MapGenerationRules.RollAffixes(MapRarity.Rare, 12, 77),
            AreaId: MapCatalog.Areas[0].StableId, RouteCandidates: [MapRoute.Safe]).Validate();
        MapAffixKind required = map.EffectiveAffixes[0].Kind;
        MapAffixKind excluded = MapAffixCatalog.All.Select(item => item.Kind)
            .First(kind => map.EffectiveAffixes.All(affix => affix.Kind != kind));
        var filter = new MapFilter(10, 16, 5_000, 11_000, 3_000, 6_000,
            [MapCatalog.Areas[0].StableId, MapCatalog.Areas[1].StableId], [MapRarity.Rare],
            MinimumQuality: 20, RequiredAffixes: [required], ExcludedAffixes: [excluded]);

        Assert.True(filter.Matches(map));
        Assert.False((filter with { AreaIds = [MapCatalog.Areas[1].StableId] }).Matches(map));
        Assert.False((filter with { ExcludedAffixes = [required] }).Matches(map));
    }

    [Fact]
    public void RecommendedOrderIsTierThenItemThenMonsterThenOldest()
    {
        MapItem low = Formal("low", 10, 1);
        MapItem old = Formal("old", 12, 2) with { AcquiredSequence = 1 };
        MapItem young = old with { InstanceId = "young", AcquiredSequence = 2 };
        Assert.Equal(["old", "young", "low"], MapFilter.All.Select([young, low, old]).Select(map => map.InstanceId));
    }

    [Fact]
    public void InventoryCapAutoSellsUnprotectedMapsAndKeepsLockedMaps()
    {
        var world = new WorldState(TestBuild(), TestBuild(), new TownEconomyState());
        world.AddMap(Formal("locked", 1, 1) with { IsLocked = true });
        for (int index = 0; index < MapGenerationRules.MaximumInventory; index++)
            world.AddMap(Formal($"map-{index:0000}", 1 + index % 20, (ulong)index + 2));

        Assert.Equal(MapGenerationRules.MaximumInventory, world.MapInventory.Count);
        Assert.Contains(world.MapInventory, map => map.InstanceId == "locked");
        Assert.True(world.Economy.Gold > 0);
    }

    [Fact]
    public void CorruptionUsesTenPercentDestroyAndFourEqualSurvivalRules()
    {
        MapItem map = Formal("corrupt", 20, 88) with { Rarity = MapRarity.Rare, Quality = 20,
            Affixes = MapGenerationRules.RollAffixes(MapRarity.Rare, 20, 88) };
        var counts = Enum.GetValues<CorruptionRule>().ToDictionary(rule => rule, _ => 0);
        int destroyed = 0;
        for (ulong seed = 1; seed <= 10_000; seed++)
        {
            MapItem? result = MapGenerationRules.Corrupt(map, seed, out bool wasDestroyed);
            if (wasDestroyed) destroyed++;
            else counts[result!.CorruptionRule]++;
        }
        Assert.InRange(destroyed, 850, 1_150);
        foreach (CorruptionRule rule in Enum.GetValues<CorruptionRule>().Where(rule => rule != CorruptionRule.None))
            Assert.InRange(counts[rule], 2_050, 2_450);
    }

    [Theory]
    [InlineData(CorruptionRule.BloodTide, 12_000, 17_000)]
    [InlineData(CorruptionRule.Greed, 9_000, 25_000)]
    [InlineData(CorruptionRule.Disorder, 12_000, 25_000)]
    [InlineData(CorruptionRule.KingDisaster, 7_500, 23_000)]
    public void RankFourCorruptionQuantityResultsMatchTheConfirmedCaps(CorruptionRule rule, int monster, int item)
    {
        MapItem map = Formal("rank4", 20, 99) with
        {
            Rarity = MapRarity.Rare,
            Quality = 20,
            Affixes = MapGenerationRules.RollAffixes(MapRarity.Rare, 20, 99),
            IsCorrupted = true,
            CorruptionRule = rule,
        };
        Assert.Equal(monster, map.MonsterQuantityBasisPoints);
        Assert.Equal(item, map.ItemQuantityBonusBasisPoints);
    }

    [Fact]
    public void AtlasHasTenSequentialCategoriesAndConfirmedTotalCost()
    {
        Assert.Equal(120, AtlasCatalog.All.Count);
        Assert.Equal(10, AtlasCatalog.All.Select(node => node.Category).Distinct().Count());
        Assert.All(Enum.GetValues<AtlasCategory>(), category =>
            Assert.Equal(AtlasCatalog.Costs, AtlasCatalog.All.Where(node => node.Category == category)
                .OrderBy(node => node.Position).Select(node => node.GoldCost)));
        Assert.Equal(AtlasCatalog.TotalGoldCost, AtlasCatalog.All.Sum(node => node.GoldCost));
        Assert.All(AtlasCatalog.All.Where(node => node.Category == AtlasCategory.Warfront),
            node => Assert.True(node.HiddenUntilWarfront));
    }

    [Fact]
    public void VersionTwentyMigrationCompensatesOldAtlasPointsAndWritesVersionTwentyOne()
    {
        GameSession session = GameSession.CreateNew(new PlayerIdentity("迁移测试", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, BaseClass.Fighter), 26);
        var oldEndgame = new EndgameState();
        oldEndgame.RecordMapCompletion(new MapItem("old-t1", 1), MapRoute.Safe, 1);
        oldEndgame.RecordMapCompletion(new MapItem("old-t2", 2), MapRoute.Safe, 2);
        oldEndgame.RecordMapCompletion(new MapItem("old-t3", 3), MapRoute.Safe, 3);
        GameSessionSnapshot old = session.Capture() with { FormatVersion = 20, Endgame = oldEndgame.Capture() };

        GameSession restored = GameSession.Restore(old);

        Assert.Equal(12_000, restored.World.Economy.Gold);
        Assert.Equal(GameSession.CurrentFormatVersion, restored.Capture().FormatVersion);
    }

    private static MapItem Formal(string id, int tier, ulong seed) => new MapItem(id, tier).EnsureFormal(seed);

    private static TeamBuild TestBuild() => new(
        new GameForWork.Core.Campaign.Combat.CharacterSheet(100,
            GameForWork.Core.Campaign.Combat.CharacterAttributes.IronOathStarting,
            new GameForWork.Core.Campaign.Combat.DefensiveEquipment(500, 50, 50)),
        GameForWork.Core.Campaign.Combat.Weapons.RustedGreatsword,
        new GameForWork.Core.Campaign.Combat.SkillConfiguration(GameForWork.Core.Campaign.Combat.SkillIds.HeavyStrike,
            GameForWork.Core.Campaign.Combat.SkillSupport.Bleed));
}

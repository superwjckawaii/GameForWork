using GameForWork.Core.P1.World;
using GameForWork.Core.P1;
using GameForWork.Core.P10;
using GameForWork.Core.P12;
using GameForWork.Core.P26;
using GameForWork.Core.P23;

namespace GameForWork.Tests;

public sealed class P26FeatureTests
{
    [Fact]
    public void CatalogContainsTwelvePrefixesTwelveSuffixesAndFourRankValues()
    {
        Assert.Equal(24, P26MapAffixCatalog.All.Count);
        Assert.Equal(12, P26MapAffixCatalog.Prefixes.Count);
        Assert.Equal(12, P26MapAffixCatalog.Suffixes.Count);
        Assert.All(P26MapAffixCatalog.All, definition => Assert.Equal(4, definition.Values.Count));
        Assert.DoesNotContain(P26MapAffixCatalog.All, definition => definition.DisplayName.Contains("反射", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(P12MapRarity.Basic, 0, 0)]
    [InlineData(P12MapRarity.Magic, 1, 1)]
    [InlineData(P12MapRarity.Rare, 2, 2)]
    public void MapRarityAlwaysRollsTheFixedPrefixSuffixStructure(P12MapRarity rarity, int prefixes, int suffixes)
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            IReadOnlyList<P12MapAffix> affixes = P26MapRules.RollAffixes(rarity, 20, seed);
            Assert.Equal(prefixes, affixes.Count(affix => affix.Family == P26MapAffixFamily.DangerousPrefix));
            Assert.Equal(suffixes, affixes.Count(affix => affix.Family == P26MapAffixFamily.RewardSuffix));
            Assert.All(affixes, affix => Assert.Equal(4, affix.Rank));
            Assert.DoesNotContain(affixes.GroupBy(affix => P26MapAffixCatalog.Get(affix.Kind).Group),
                group => group.Key != P26MapAffixGroup.None && group.Count() > 1);
        }
    }

    [Fact]
    public void FilterUsesAndAcrossFieldsOrAcrossAreasAndStrictAffixRules()
    {
        P1MapItem map = new P1MapItem("filter-map", 12, Rarity: P12MapRarity.Rare,
            Quality: 20, Affixes: P26MapRules.RollAffixes(P12MapRarity.Rare, 12, 77),
            AreaId: P12MapCatalog.Areas[0].StableId, RouteCandidates: [MapRoute.Safe]).Validate();
        P12MapAffixKind required = map.EffectiveAffixes[0].Kind;
        P12MapAffixKind excluded = P26MapAffixCatalog.All.Select(item => item.Kind)
            .First(kind => map.EffectiveAffixes.All(affix => affix.Kind != kind));
        var filter = new P26MapFilter(10, 16, 5_000, 11_000, 3_000, 6_000,
            [P12MapCatalog.Areas[0].StableId, P12MapCatalog.Areas[1].StableId], [P12MapRarity.Rare],
            MinimumQuality: 20, RequiredAffixes: [required], ExcludedAffixes: [excluded]);

        Assert.True(filter.Matches(map));
        Assert.False((filter with { AreaIds = [P12MapCatalog.Areas[1].StableId] }).Matches(map));
        Assert.False((filter with { ExcludedAffixes = [required] }).Matches(map));
    }

    [Fact]
    public void RecommendedOrderIsTierThenItemThenMonsterThenOldest()
    {
        P1MapItem low = Formal("low", 10, 1);
        P1MapItem old = Formal("old", 12, 2) with { AcquiredSequence = 1 };
        P1MapItem young = old with { InstanceId = "young", AcquiredSequence = 2 };
        Assert.Equal(["old", "young", "low"], P26MapFilter.All.Select([young, low, old]).Select(map => map.InstanceId));
    }

    [Fact]
    public void InventoryCapAutoSellsUnprotectedMapsAndKeepsLockedMaps()
    {
        var world = new P1WorldState(TestBuild(), TestBuild(), new TownEconomyState());
        world.AddMap(Formal("locked", 1, 1) with { IsLocked = true });
        for (int index = 0; index < P26MapRules.MaximumInventory; index++)
            world.AddMap(Formal($"map-{index:0000}", 1 + index % 20, (ulong)index + 2));

        Assert.Equal(P26MapRules.MaximumInventory, world.MapInventory.Count);
        Assert.Contains(world.MapInventory, map => map.InstanceId == "locked");
        Assert.True(world.Economy.Gold > 0);
    }

    [Fact]
    public void CorruptionUsesTenPercentDestroyAndFourEqualSurvivalRules()
    {
        P1MapItem map = Formal("corrupt", 20, 88) with { Rarity = P12MapRarity.Rare, Quality = 20,
            Affixes = P26MapRules.RollAffixes(P12MapRarity.Rare, 20, 88) };
        var counts = Enum.GetValues<P26CorruptionRule>().ToDictionary(rule => rule, _ => 0);
        int destroyed = 0;
        for (ulong seed = 1; seed <= 10_000; seed++)
        {
            P1MapItem? result = P26MapRules.Corrupt(map, seed, out bool wasDestroyed);
            if (wasDestroyed) destroyed++;
            else counts[result!.CorruptionRule]++;
        }
        Assert.InRange(destroyed, 850, 1_150);
        foreach (P26CorruptionRule rule in Enum.GetValues<P26CorruptionRule>().Where(rule => rule != P26CorruptionRule.None))
            Assert.InRange(counts[rule], 2_050, 2_450);
    }

    [Theory]
    [InlineData(P26CorruptionRule.BloodTide, 12_000, 17_000)]
    [InlineData(P26CorruptionRule.Greed, 9_000, 25_000)]
    [InlineData(P26CorruptionRule.Disorder, 12_000, 25_000)]
    [InlineData(P26CorruptionRule.KingDisaster, 7_500, 23_000)]
    public void RankFourCorruptionQuantityResultsMatchTheConfirmedCaps(P26CorruptionRule rule, int monster, int item)
    {
        P1MapItem map = Formal("rank4", 20, 99) with
        {
            Rarity = P12MapRarity.Rare,
            Quality = 20,
            Affixes = P26MapRules.RollAffixes(P12MapRarity.Rare, 20, 99),
            IsCorrupted = true,
            CorruptionRule = rule,
        };
        Assert.Equal(monster, map.MonsterQuantityBasisPoints);
        Assert.Equal(item, map.ItemQuantityBonusBasisPoints);
    }

    [Fact]
    public void AtlasHasTenSequentialCategoriesAndConfirmedTotalCost()
    {
        Assert.Equal(120, P26AtlasCatalog.All.Count);
        Assert.Equal(10, P26AtlasCatalog.All.Select(node => node.Category).Distinct().Count());
        Assert.All(Enum.GetValues<P26AtlasCategory>(), category =>
            Assert.Equal(P26AtlasCatalog.Costs, P26AtlasCatalog.All.Where(node => node.Category == category)
                .OrderBy(node => node.Position).Select(node => node.GoldCost)));
        Assert.Equal(P26AtlasCatalog.TotalGoldCost, P26AtlasCatalog.All.Sum(node => node.GoldCost));
        Assert.All(P26AtlasCatalog.All.Where(node => node.Category == P26AtlasCategory.Warfront),
            node => Assert.True(node.HiddenUntilWarfront));
    }

    [Fact]
    public void VersionTwentyMigrationCompensatesOldAtlasPointsAndWritesVersionTwentyOne()
    {
        P1GameSession session = P1GameSession.CreateNew(new PlayerIdentity("迁移测试", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, P23BaseClass.Fighter), 26);
        var oldEndgame = new P10EndgameState();
        oldEndgame.RecordMapCompletion(new P1MapItem("old-t1", 1), MapRoute.Safe, 1);
        oldEndgame.RecordMapCompletion(new P1MapItem("old-t2", 2), MapRoute.Safe, 2);
        oldEndgame.RecordMapCompletion(new P1MapItem("old-t3", 3), MapRoute.Safe, 3);
        P1GameSessionSnapshot old = session.Capture() with { FormatVersion = 20, Endgame = oldEndgame.Capture() };

        P1GameSession restored = P1GameSession.Restore(old);

        Assert.Equal(12_000, restored.World.Economy.Gold);
        Assert.Equal(24, restored.Capture().FormatVersion);
    }

    private static P1MapItem Formal(string id, int tier, ulong seed) => new P1MapItem(id, tier).EnsureFormal(seed);

    private static P1TeamBuild TestBuild() => new(
        new GameForWork.Core.P1.Combat.CharacterSheet(100,
            GameForWork.Core.P1.Combat.CharacterAttributes.IronOathStarting,
            new GameForWork.Core.P1.Combat.DefensiveEquipment(500, 50, 50)),
        GameForWork.Core.P1.Combat.P1Weapons.RustedGreatsword,
        new GameForWork.Core.P1.Combat.SkillConfiguration(GameForWork.Core.P1.Combat.P1SkillIds.HeavyStrike,
            GameForWork.Core.P1.Combat.SkillSupport.Bleed));
}

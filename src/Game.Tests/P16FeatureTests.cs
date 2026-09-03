using System.Text.Json;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P4;
using GameForWork.Core.P14;
using GameForWork.Core.P16;
using GameForWork.Core.Simulation;

namespace GameForWork.Tests;

public sealed class P16FeatureTests
{
    [Fact]
    public void MapTiersHaveConfirmedMonsterLevelsAndMigrateAreaLevelJson()
    {
        Assert.Equal(70, P16MapTierLevels.MonsterLevel(1));
        Assert.Equal(100, P16MapTierLevels.MonsterLevel(20));
        Assert.Equal([70, 72, 73, 75, 76, 78, 79, 81, 83, 84, 86, 87, 89, 91, 92, 94, 95, 97, 98, 100],
            P16MapTierLevels.All);
        P1MapItem restored = JsonSerializer.Deserialize<P1MapItem>("{\"InstanceId\":\"legacy\",\"AreaLevel\":20}")!;
        Assert.Equal(20, restored.Tier);
        Assert.Equal(100, restored.MonsterLevel);
        Assert.Contains("\"AreaLevel\":20", JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void CampaignLevelsReachSixtyNineBeforeTierOne()
    {
        Assert.Equal(2, P16CampaignLevels.MonsterLevel(P2CampaignCatalog.Nodes[0]));
        Assert.Equal(69, P16CampaignLevels.MonsterLevel(P2CampaignCatalog.Nodes[^1]));
        Assert.All(P2CampaignCatalog.Nodes.Zip(P2CampaignCatalog.Nodes.Skip(1)), pair =>
            Assert.True(P16CampaignLevels.MonsterLevel(pair.First) <= P16CampaignLevels.MonsterLevel(pair.Second)));
    }

    [Fact]
    public void MonsterCatalogHasTenFamiliesEightMembersAndEighteenAffixes()
    {
        Assert.Equal(80, P1Enemies.NormalEnemies.Count);
        Assert.Equal(10, P1Enemies.NormalEnemies.Select(enemy => enemy.Family).Distinct().Count());
        Assert.All(P1Enemies.NormalEnemies.GroupBy(enemy => enemy.Family), family => Assert.Equal(8, family.Count()));
        Assert.Equal(18, Enum.GetValues<EliteAffix>().Length);
        IReadOnlyList<EliteAffix> rare = EnemyRules.RollAffixes(new Pcg32(16), EnemyRarity.Rare);
        Assert.InRange(rare.Count, 3, 4);
        ScaledEnemy scaled = EnemyRules.Scale(P1Enemies.NormalEnemies[0], 100, rare, rarity: EnemyRarity.Rare);
        Assert.Equal(EnemyRarity.Rare, scaled.Rarity);
        Assert.True(scaled.Life > P1Enemies.NormalEnemies[0].Life * 10);
    }

    [Fact]
    public void EncounterBudgetCreatesMagicPackAndRareLeaderTogether()
    {
        P4NodeCombatResult result = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            StrongBuild(), 1, 70, 12, HasElite: true, HasBoss: false, AbyssRoute: false, Formation: 0), 16);
        P4SpatialFrame first = result.Frames[0];
        Assert.Equal(12, first.Enemies.Count);
        Assert.Single(first.Enemies, enemy => enemy.Rarity == EnemyRarity.Rare);
        Assert.InRange(first.Enemies.Count(enemy => enemy.Rarity == EnemyRarity.Magic), 2, 6);
    }

    [Fact]
    public void SortingUsesConfirmedDescendingTieBreakers()
    {
        var storage = new EquipmentStorage();
        storage.TryStore(Item("basic-six", ItemRarity.Basic, 6, 20));
        storage.TryStore(Item("legend-one", ItemRarity.Legendary, 1, 10));
        storage.TryStore(Item("rare-five", ItemRarity.Rare, 5, 30));
        storage.Sort(P16ItemSortMode.LinkedSockets);
        Assert.Equal(["basic-six", "rare-five", "legend-one"], storage.Items.Select(item => item.InstanceId));
        storage.Sort(P16ItemSortMode.Rarity);
        Assert.Equal(["legend-one", "rare-five", "basic-six"], storage.Items.Select(item => item.InstanceId));
    }

    [Fact]
    public void BatchPreviewProcessesAllOrdinaryLegendariesAndProtectsSafetyItems()
    {
        P1GameSession session = Session();
        session.World.Storage.TryStore(Item("basic", ItemRarity.Basic, 0, 1));
        session.World.Storage.TryStore(Item("locked", ItemRarity.Rare, 0, 1) with { IsLocked = true });
        session.World.Storage.TryStore(Item("craft", ItemRarity.Rare, 0, 1) with { IsCraftingBase = true });
        ItemInstance first = P14UniqueItems.Create("core.unique.red_vow", 70, "unique-one");
        ItemInstance second = P14UniqueItems.Create("core.unique.red_vow", 70, "unique-two");
        session.World.Storage.TryStore(first);
        session.World.Storage.TryStore(second);
        session.World.Storage.TryStore(P14UniqueItems.Create("core.mythic.heart_of_ash", 100, "mythic"));

        P16BatchPreview preview = P16BatchItems.Preview(session, P16BatchAction.Sell,
            P16BatchScope.Storage, ItemRarity.Legendary);

        Assert.Contains(preview.Targets, target => target.Item.InstanceId == "basic");
        Assert.Equal(2, preview.Targets.Count(target => target.Item.LegendaryRule?.StableId == "core.unique.red_vow"));
        Assert.DoesNotContain(preview.Targets, target => target.Item.InstanceId is "locked" or "craft" or "mythic");
        Assert.Equal(3, preview.Excluded);
        Assert.Equal(1, preview.ExcludedReasons["已锁定"]);
        Assert.Equal(1, preview.ExcludedReasons["制作底材"]);
        Assert.Equal(1, preview.ExcludedReasons["神话装备"]);
    }

    [Fact]
    public void BatchSellHandlesStorageFullOfDistinctOrdinaryLegendaries()
    {
        P1GameSession session = Session();
        string[] ids = P14UniqueItems.All.Where(item => !item.Mythic).Take(10)
            .Select(item => item.StableId).ToArray();
        foreach ((string id, int index) in ids.Select((id, index) => (id, index)))
            Assert.True(session.World.Storage.TryStore(P14UniqueItems.Create(id, 100, $"save-one-{index}")));

        P16BatchPreview preview = P16BatchItems.Preview(session, P16BatchAction.Sell,
            P16BatchScope.Storage, ItemRarity.Legendary);
        P16BatchExecution execution = P16BatchItems.Execute(session, preview);

        Assert.Equal(10, preview.Total);
        Assert.Equal(new P16BatchExecution(10, 0), execution);
        Assert.Empty(session.World.Storage.Items);
    }

    [Fact]
    public void BatchPreviewCanExplicitlyIncludeCraftingBasesWithoutIncludingOtherProtectedItems()
    {
        P1GameSession session = Session();
        session.World.Storage.TryStore(Item("craft", ItemRarity.Rare, 0, 80) with { IsCraftingBase = true });
        session.World.Storage.TryStore(Item("locked-craft", ItemRarity.Rare, 0, 80) with
        {
            IsCraftingBase = true,
            IsLocked = true,
        });
        session.World.Storage.TryStore(P14UniqueItems.Create("core.mythic.heart_of_ash", 100, "mythic"));

        P16BatchPreview preview = P16BatchItems.Preview(session, P16BatchAction.Sell,
            P16BatchScope.Storage, ItemRarity.Legendary, includeCraftingBases: true);

        Assert.Single(preview.Targets, target => target.Item.InstanceId == "craft");
        Assert.Equal(2, preview.Excluded);
        Assert.Equal(1, preview.ExcludedReasons["已锁定"]);
        Assert.Equal(1, preview.ExcludedReasons["神话装备"]);
        Assert.DoesNotContain("制作底材", preview.ExcludedReasons.Keys);
    }

    [Fact]
    public void FilterSupportsAndConditionsRangesAndSystemProtection()
    {
        var filter = new LootFilter([
            new LootFilterRule("test", LootDisposition.Dismantle, MinimumRarity: ItemRarity.Magic,
                MaximumRarity: ItemRarity.Rare, Category: ItemCategory.Ring, MinimumItemLevel: 70,
                MaximumItemLevel: 90, MinimumLinkedSockets: 1, MaximumLinkedSockets: 4),
        ]);
        ItemInstance match = Item("match", ItemRarity.Rare, 2, 80);
        ItemInstance protectedFiveLink = match with { InstanceId = "five", LinkedSocketCount = 5 };
        Assert.Equal(LootDisposition.Dismantle, filter.Evaluate(match));
        Assert.Equal(LootDisposition.Keep, filter.Evaluate(protectedFiveLink));
        Assert.Equal(LootDisposition.Keep, filter.Evaluate(match with { InstanceId = "low", ItemLevel = 60 }));
    }

    private static ItemInstance Item(string id, ItemRarity rarity, int links, int level) => new(
        id, P1ItemBases.Get("core.base.life_ring"), level, rarity, [], LinkedSocketCount: links);

    private static P1GameSession Session() => P1GameSession.CreateNew(new PlayerIdentity(
        "P16测试者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
        CharacterHairStyle.Braided, P23BaseClass.Fighter), 16);

    private static P1TeamBuild StrongBuild() => new(
        new CharacterSheet(100, new CharacterAttributes(600, 300, 300, 200),
            new DefensiveEquipment(4_000, 1_000, 1_000), FlatMaximumLife: 5_000),
        new WeaponProfile("p16.weapon", 2_000, 3_000, 1_500, 1_000),
        new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed),
        FlatAccuracy: 4_000,
        IncreasedDamageBasisPoints: 10_000,
        ActiveSkills: [new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed)]);
}

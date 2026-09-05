using System.Text.Json;
using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Spatial;
using GameForWork.Core.Content;
using GameForWork.Core.Inventory;
using GameForWork.Core.Simulation;

namespace GameForWork.Tests;

public sealed class InventoryFeatureTests
{
    [Fact]
    public void MapTiersHaveConfirmedMonsterLevelsAndMigrateAreaLevelJson()
    {
        Assert.Equal(70, MapTierLevels.MonsterLevel(1));
        Assert.Equal(100, MapTierLevels.MonsterLevel(20));
        Assert.Equal([70, 72, 73, 75, 76, 78, 79, 81, 83, 84, 86, 87, 89, 91, 92, 94, 95, 97, 98, 100],
            MapTierLevels.All);
        MapItem restored = JsonSerializer.Deserialize<MapItem>("{\"InstanceId\":\"legacy\",\"AreaLevel\":20}")!;
        Assert.Equal(20, restored.Tier);
        Assert.Equal(100, restored.MonsterLevel);
        Assert.Contains("\"AreaLevel\":20", JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void CampaignLevelsReachSixtyNineBeforeTierOne()
    {
        Assert.Equal(2, CampaignLevels.MonsterLevel(CampaignCatalog.Nodes[0]));
        Assert.Equal(69, CampaignLevels.MonsterLevel(CampaignCatalog.Nodes[^1]));
        Assert.All(CampaignCatalog.Nodes.Zip(CampaignCatalog.Nodes.Skip(1)), pair =>
            Assert.True(CampaignLevels.MonsterLevel(pair.First) <= CampaignLevels.MonsterLevel(pair.Second)));
    }

    [Fact]
    public void MonsterCatalogHasTenFamiliesEightMembersAndEighteenAffixes()
    {
        Assert.Equal(80, Enemies.NormalEnemies.Count);
        Assert.Equal(10, Enemies.NormalEnemies.Select(enemy => enemy.Family).Distinct().Count());
        Assert.All(Enemies.NormalEnemies.GroupBy(enemy => enemy.Family), family => Assert.Equal(8, family.Count()));
        Assert.Equal(18, Enum.GetValues<EliteAffix>().Length);
        IReadOnlyList<EliteAffix> rare = EnemyRules.RollAffixes(new Pcg32(16), EnemyRarity.Rare);
        Assert.InRange(rare.Count, 3, 4);
        ScaledEnemy scaled = EnemyRules.Scale(Enemies.NormalEnemies[0], 100, rare, rarity: EnemyRarity.Rare);
        Assert.Equal(EnemyRarity.Rare, scaled.Rarity);
        Assert.True(scaled.Life > Enemies.NormalEnemies[0].Life * 10);
    }

    [Fact]
    public void EncounterBudgetCreatesMagicPackAndRareLeaderTogether()
    {
        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            StrongBuild(), 1, 70, 12, HasElite: true, HasBoss: false, AbyssRoute: false, Formation: 0), 16);
        SpatialFrame first = result.Frames[0];
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
        storage.Sort(ItemSortMode.LinkedSockets);
        Assert.Equal(["basic-six", "rare-five", "legend-one"], storage.Items.Select(item => item.InstanceId));
        storage.Sort(ItemSortMode.Rarity);
        Assert.Equal(["legend-one", "rare-five", "basic-six"], storage.Items.Select(item => item.InstanceId));
    }

    [Fact]
    public void BatchPreviewProcessesAllOrdinaryLegendariesAndProtectsSafetyItems()
    {
        GameSession session = Session();
        session.World.Storage.TryStore(Item("basic", ItemRarity.Basic, 0, 1));
        session.World.Storage.TryStore(Item("locked", ItemRarity.Rare, 0, 1) with { IsLocked = true });
        session.World.Storage.TryStore(Item("craft", ItemRarity.Rare, 0, 1) with { IsCraftingBase = true });
        ItemInstance first = UniqueItems.Create("core.unique.red_vow", 70, "unique-one");
        ItemInstance second = UniqueItems.Create("core.unique.red_vow", 70, "unique-two");
        session.World.Storage.TryStore(first);
        session.World.Storage.TryStore(second);
        session.World.Storage.TryStore(UniqueItems.Create("core.mythic.heart_of_ash", 100, "mythic"));

        BatchPreview preview = BatchItems.Preview(session, BatchAction.Sell,
            BatchScope.Storage, ItemRarity.Legendary);

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
        GameSession session = Session();
        string[] ids = UniqueItems.All.Where(item => !item.Mythic).Take(10)
            .Select(item => item.StableId).ToArray();
        foreach ((string id, int index) in ids.Select((id, index) => (id, index)))
            Assert.True(session.World.Storage.TryStore(UniqueItems.Create(id, 100, $"save-one-{index}")));

        BatchPreview preview = BatchItems.Preview(session, BatchAction.Sell,
            BatchScope.Storage, ItemRarity.Legendary);
        BatchExecution execution = BatchItems.Execute(session, preview);

        Assert.Equal(10, preview.Total);
        Assert.Equal(new BatchExecution(10, 0), execution);
        Assert.Empty(session.World.Storage.Items);
    }

    [Fact]
    public void BatchPreviewCanExplicitlyIncludeCraftingBasesWithoutIncludingOtherProtectedItems()
    {
        GameSession session = Session();
        session.World.Storage.TryStore(Item("craft", ItemRarity.Rare, 0, 80) with { IsCraftingBase = true });
        session.World.Storage.TryStore(Item("locked-craft", ItemRarity.Rare, 0, 80) with
        {
            IsCraftingBase = true,
            IsLocked = true,
        });
        session.World.Storage.TryStore(UniqueItems.Create("core.mythic.heart_of_ash", 100, "mythic"));

        BatchPreview preview = BatchItems.Preview(session, BatchAction.Sell,
            BatchScope.Storage, ItemRarity.Legendary, includeCraftingBases: true);

        Assert.Single(preview.Targets, target => target.Item.InstanceId == "craft");
        Assert.Equal(2, preview.Excluded);
        Assert.Equal(1, preview.ExcludedReasons["已锁定"]);
        Assert.Equal(1, preview.ExcludedReasons["神话装备"]);
        Assert.DoesNotContain("制作底材", preview.ExcludedReasons.Keys);
    }

    [Fact]
    public void BatchCleanupIncludesMythicOnlyWhenExplicitlySelected()
    {
        GameSession session = Session();
        ItemInstance mythic = UniqueItems.Create("core.mythic.heart_of_ash", 100, "mythic-cleanup");
        Assert.True(session.World.Storage.TryStore(mythic));

        BatchPreview protectedPreview = BatchItems.Preview(session, BatchAction.Dismantle,
            BatchScope.Storage, ItemRarity.Legendary);
        Assert.Empty(protectedPreview.Targets);
        Assert.Equal(1, protectedPreview.ExcludedReasons["神话装备"]);

        BatchPreview selectedPreview = BatchItems.Preview(session, BatchAction.Dismantle,
            BatchScope.Storage, ItemRarity.Legendary, includeMythic: true);
        Assert.Single(selectedPreview.Targets, target => target.Item.InstanceId == mythic.InstanceId);
        Assert.True(selectedPreview.IncludesMythic);
        Assert.Equal(1, selectedPreview.MythicCount);
        Assert.Equal(new BatchExecution(1, 0), BatchItems.Execute(session, selectedPreview));
        Assert.Empty(session.World.Storage.Items);
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
        id, ItemBases.Get("core.base.life_ring"), level, rarity, [], LinkedSocketCount: links);

    private static GameSession Session() => GameSession.CreateNew(new PlayerIdentity(
        "Inventory测试者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
        CharacterHairStyle.Braided, BaseClass.Fighter), 16);

    private static TeamBuild StrongBuild() => new(
        new CharacterSheet(100, new CharacterAttributes(600, 300, 300, 200),
            new DefensiveEquipment(4_000, 1_000, 1_000), FlatMaximumLife: 5_000),
        new WeaponProfile("inventory.weapon", 2_000, 3_000, 1_500, 1_000),
        new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.Bleed),
        FlatAccuracy: 4_000,
        IncreasedDamageBasisPoints: 10_000,
        ActiveSkills: [new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.Bleed)]);
}

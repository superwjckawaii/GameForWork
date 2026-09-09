using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Characters;
using GameForWork.Core.Management;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Spatial;
using GameForWork.Core.Builds;
using GameForWork.Core.Content;
using GameForWork.Core.Endgame;

namespace GameForWork.Tests;

public sealed class LootChestTests
{
    [Fact]
    public void CompletedMapStoresEquipmentAndGoldUntilChestClaim()
    {
        GameSession session = Session();
        LootChest chest = new("loot-test", "map-test", 5, [], 123, 0);
        session = GameSession.Restore(session.Capture() with { LootChests = new LootChestSnapshot([chest]) });
        int goldBefore = session.World.Economy.Gold;
        Assert.True(session.ClaimLootChest(chest.Id));
        Assert.Empty(session.LootChests);
        Assert.True(session.World.Economy.Gold >= goldBefore + chest.Gold);
        Assert.False(session.ClaimLootChest(chest.Id));
    }

    [Fact]
    public void OrdinaryChestViewAndContentsSurviveSaveRestore()
    {
        GameSession session = Session();
        LootChest chest = new("loot-save", "map-save", 5, [], 45, 2);
        session = GameSession.Restore(session.Capture() with { LootChests = new LootChestSnapshot([chest]) });
        Assert.True(session.MarkLootChestViewed(chest.Id));
        GameSession restored = GameSession.Restore(System.Text.Json.JsonSerializer.Deserialize<GameSessionSnapshot>(
            System.Text.Json.JsonSerializer.Serialize(session.Capture()))!);
        LootChest restoredChest = Assert.Single(restored.LootChests);
        Assert.False(restoredChest.IsNew);
        Assert.Equal(chest.Equipment.Select(item => item.InstanceId), restoredChest.Equipment.Select(item => item.InstanceId));
        Assert.Equal(chest.Gold, restoredChest.Gold);
    }

    [Fact]
    public void OrdinaryChestClaimsFrozenSkillStoneAndJewelExactlyOnce()
    {
        GameSession session = Session();
        SkillStoneDefinition definition = SkillStoneCatalog.DropPool.First(item => !item.StarterGranted);
        SkillStoneInstance stone = new("chest-skill", definition.StableId, Quality: 20, Mutated: false);
        JewelInstance jewel = JewelCatalog.RollPrismatic(90, 77, "chest-jewel");
        LootChest chest = new("loot-frozen", "map-frozen", 10, [], 0, 0, [], [stone], [jewel]);
        session = GameSession.Restore(session.Capture() with { LootChests = new LootChestSnapshot([chest]) });

        Assert.Empty(session.World.Storage.Items);
        Assert.DoesNotContain(session.Management.SkillStones, item => item.InstanceId == stone.InstanceId);
        Assert.DoesNotContain(session.Jewels.Items, item => item.InstanceId == jewel.InstanceId);
        Assert.True(session.ClaimLootChest(chest.Id));
        Assert.Contains(session.Management.SkillStones, item => item.InstanceId == stone.InstanceId && item.Quality == 20);
        Assert.Contains(session.Jewels.Items, item => item.InstanceId == jewel.InstanceId);
        Assert.Empty(session.LootChests);
        Assert.False(session.ClaimLootChest(chest.Id));
    }

    [Fact]
    public void OrdinaryChestRemainsUnclaimedWhenJewelCapacityCannotAcceptIt()
    {
        GameSession session = Session();
        JewelInstance[] full = Enumerable.Range(0, JewelState.Capacity)
            .Select(index => JewelCatalog.RollPrismatic(90, (ulong)index + 1, $"full-jewel-{index}"))
            .ToArray();
        session = GameSession.Restore(session.Capture() with
        {
            Jewels = new JewelStateSnapshot(full, new Dictionary<string, string>()),
            LootChests = new LootChestSnapshot([new LootChest(
                "loot-full-jewel", "map-full-jewel", 10, [], 0, 0, [], [],
                [JewelCatalog.RollPrismatic(90, 1001, "overflow-jewel")])]),
        });

        Assert.False(session.ClaimLootChest("loot-full-jewel"));
        Assert.Single(session.LootChests);
        Assert.Equal(JewelState.Capacity, session.Jewels.Items.Count);
    }

    [Fact]
    public void OrdinaryChestKeepsPendingFirstClearRewardProtectedAndStable()
    {
        GameSession session = Session();
        ItemInstance mythic = UniqueItems.Create(MythicRewardRules.WorldEater, 100, "pending-first-clear") with
        {
            IsLocked = true,
        };
        LootChest chest = new("loot-first-clear", "map-first-clear", 20, [mythic], 0, 0);
        session = GameSession.Restore(session.Capture() with { LootChests = new LootChestSnapshot([chest]) });
        GameSession restored = GameSession.Restore(System.Text.Json.JsonSerializer.Deserialize<GameSessionSnapshot>(
            System.Text.Json.JsonSerializer.Serialize(session.Capture()))!);

        LootChest persisted = Assert.Single(restored.LootChests);
        Assert.Equal(mythic.InstanceId, Assert.Single(persisted.Equipment).InstanceId);
        Assert.True(restored.ClaimLootChest(persisted.Id));
        Assert.Contains(restored.World.Storage.Items, item => item.InstanceId == mythic.InstanceId && item.IsLocked);
        Assert.Empty(restored.LootChests);
    }

    [Fact]
    public void OrdinaryChestRejectsBatchThatWouldCrossSkillStoneHoldLimit()
    {
        GameSession session = Session();
        SkillStoneDefinition definition = SkillStoneCatalog.DropPool.First(item => !item.StarterGranted);
        SkillStoneInstance[] held = Enumerable.Range(0, 4)
            .Select(index => new SkillStoneInstance($"held-{index}", definition.StableId))
            .ToArray();
        ManagementSnapshot management = session.Management.Capture() with
        {
            SkillStones = session.Management.SkillStones.Concat(held).ToArray(),
        };
        LootChest chest = new("loot-skill-limit", "map-skill-limit", 10, [], 0, 0, [],
            [new SkillStoneInstance("pending-a", definition.StableId), new SkillStoneInstance("pending-b", definition.StableId)]);
        session = GameSession.Restore(session.Capture() with
        {
            Management = management,
            LootChests = new LootChestSnapshot([chest]),
        });

        Assert.False(session.ClaimLootChest(chest.Id));
        Assert.Single(session.LootChests);
        Assert.Equal(4, session.Management.HeldSkillStoneCount(definition.StableId, false));
    }

    private static GameSession Session()
    {
        GameSession session = GameSession.CreateNew(new("箱子测试", CharacterGender.Androgynous,
            CharacterSkinTone.Fair, CharacterHairStyle.Cropped, BaseClass.Fighter), 100, tutorialEnabled: false);
        return GameSession.Restore(session.Capture() with { Campaign = CampaignState.CreateLegacyCompleted().Capture() });
    }
}

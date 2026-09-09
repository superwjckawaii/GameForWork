using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Characters;
using GameForWork.Core.Management;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Spatial;

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

    private static GameSession Session()
    {
        GameSession session = GameSession.CreateNew(new("箱子测试", CharacterGender.Androgynous,
            CharacterSkinTone.Fair, CharacterHairStyle.Cropped, BaseClass.Fighter), 100, tutorialEnabled: false);
        return GameSession.Restore(session.Capture() with { Campaign = CampaignState.CreateLegacyCompleted().Capture() });
    }
}

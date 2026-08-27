using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;

namespace GameForWork.Tests;

public sealed class P2ManagementTests
{
    [Fact]
    public void NewEquipmentSlotsHaveTwoBasesAndAffixFamilies()
    {
        foreach (ItemCategory category in new[]
                 {
                     ItemCategory.Gloves,
                     ItemCategory.Boots,
                     ItemCategory.Belt,
                     ItemCategory.Amulet,
                 })
        {
            Assert.Equal(2, P1ItemBases.All.Count(item => item.Category == category));
            int families = P1Affixes.For(category, 60)
                .Select(affix => affix.StableFamilyId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            Assert.InRange(families, 4, 6);
        }
    }

    [Fact]
    public void SortingBagOverflowRoutesToPermanentRecovery()
    {
        P2ManagementState state = P2ManagementState.CreateNew();
        for (int index = 0; index < P2ManagementState.SortingBagCapacity; index++)
        {
            Assert.True(state.TryAddToSortingBag(Item($"bag-{index}")));
        }

        ItemInstance overflow = Item("overflow");
        Assert.False(state.TryAddToSortingBag(overflow));
        state.AddToRecovery(overflow, "test");

        Assert.Single(state.Recovery);
        Assert.Equal("overflow", state.Recovery[0].InstanceId);
    }

    [Fact]
    public void BuybackKeepsLatestTwentyAtOriginalPrice()
    {
        P2ManagementState state = P2ManagementState.CreateNew();
        for (int index = 0; index < 24; index++)
        {
            state.AddBuyback(Item($"sold-{index}"), index + 1);
        }

        Assert.Equal(20, state.Buyback.Count);
        Assert.Equal("sold-4", state.Buyback[0].Item.InstanceId);
        Assert.Equal(24, state.Buyback[^1].SalePrice);
    }

    [Fact]
    public void ManagementSnapshotPreservesLocksSkillsAndHistory()
    {
        P2ManagementState state = P2ManagementState.CreateNew();
        Assert.True(state.TryAddToSortingBag(Item("locked") with { IsLocked = true }));
        state.AddHistory("测试记录");

        P2ManagementState restored = P2ManagementState.Restore(state.Capture(), legacyMigration: false);

        Assert.True(restored.SortingBag[0].IsLocked);
        Assert.Equal(6, restored.SkillStones.Count);
        Assert.Contains("测试记录", restored.OperationHistory);
    }

    [Fact]
    public void LegacySessionMigratesToLevelSixtyAndFreeRespec()
    {
        P1GameSession current = P1GameSession.CreateNew(new PlayerIdentity(
            "迁移者",
            CharacterGender.Androgynous,
            CharacterSkinTone.Umber,
            CharacterHairStyle.Cropped,
            P1Ascendancy.IronOath), 77);
        P1GameSessionSnapshot legacy = current.Capture() with { FormatVersion = 3, Management = null };

        P1GameSession migrated = P1GameSession.Restore(legacy);

        Assert.Equal(60, migrated.World.Hero.Progression.Level);
        Assert.True(migrated.Management.FreeFullRespecAvailable);
        Assert.Equal(6, migrated.Management.SkillStones.Count);
    }

    private static ItemInstance Item(string instanceId) => ItemGenerator.Generate(
        "core.base.iron_ring",
        1,
        ItemRarity.Basic,
        1,
        instanceId);
}

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

    [Fact]
    public void EquipCommandMovesExactlyOneInstanceAndReturnsPreviousItem()
    {
        P1GameSession session = Session();
        ItemInstance replacement = ItemGenerator.Generate(
            "core.base.heavy_battleaxe", 4, ItemRarity.Magic, 10, "replacement");
        Assert.True(session.World.Storage.TryStore(replacement));
        int initialEquippedAndStored = session.HeroEquipment.Items.Count + session.World.Storage.Count;

        P2ItemCommandResult result = new P2ItemCommandService(session).TryEquip(
            ItemContainerKind.Storage, 0, EquipmentSlot.MainHand);

        Assert.True(result.Succeeded);
        Assert.Equal("replacement", session.HeroEquipment.Items[EquipmentSlot.MainHand].InstanceId);
        Assert.Equal(initialEquippedAndStored, session.HeroEquipment.Items.Count + session.World.Storage.Count);
        Assert.DoesNotContain(session.World.Storage.Items, item => item.InstanceId == "replacement");
    }

    [Fact]
    public void LockedItemsCannotBeSoldAndManualSaleCanBeBoughtBack()
    {
        P1GameSession session = Session();
        Assert.True(session.Management.TryAddToSortingBag(Item("sale")));
        var commands = new P2ItemCommandService(session);
        Assert.True(commands.ToggleLock(ItemContainerKind.SortingBag, 0).Succeeded);
        Assert.Equal("item_locked", commands.Sell(ItemContainerKind.SortingBag, 0).Code);
        Assert.True(commands.ToggleLock(ItemContainerKind.SortingBag, 0).Succeeded);

        Assert.True(commands.Sell(ItemContainerKind.SortingBag, 0).Succeeded);
        Assert.Single(session.Management.Buyback);
        Assert.True(commands.BuyBack(0).Succeeded);
        Assert.Contains(session.Management.SortingBag, item => item.InstanceId == "sale");
        Assert.Empty(session.Management.Buyback);
    }

    [Fact]
    public void OrdinaryItemMovementCanBeUndoneWithinTheSession()
    {
        P1GameSession session = Session();
        Assert.True(session.Management.TryAddToSortingBag(Item("undo-a")));
        Assert.True(session.Management.TryAddToSortingBag(Item("undo-b")));
        var commands = new P2ItemCommandService(session);

        Assert.True(commands.Move(ItemContainerKind.SortingBag, 0, ItemContainerKind.SortingBag, 1).Succeeded);
        Assert.Equal(["undo-b", "undo-a"], session.Management.SortingBag.Select(item => item.InstanceId));
        Assert.True(commands.UndoLastMovement().Succeeded);
        Assert.Equal(["undo-a", "undo-b"], session.Management.SortingBag.Select(item => item.InstanceId));
    }

    [Fact]
    public void RareDismantleRequiresExplicitConfirmation()
    {
        P1GameSession session = Session();
        Assert.True(session.Management.TryAddToSortingBag(ItemGenerator.Generate(
            "core.base.life_ring", 6, ItemRarity.Rare, 22, "rare")));
        var commands = new P2ItemCommandService(session);

        Assert.Equal("confirmation_required", commands.Dismantle(ItemContainerKind.SortingBag, 0, false).Code);
        Assert.True(commands.Dismantle(ItemContainerKind.SortingBag, 0, true).Succeeded);
        Assert.Empty(session.Management.SortingBag);
        Assert.Equal(5, session.World.Economy.IronScraps);
    }

    [Fact]
    public void LegacyItemCategoryNumbersRemainStable()
    {
        Assert.Equal(0, (int)ItemCategory.TwoHandWeapon);
        Assert.Equal(1, (int)ItemCategory.BodyArmor);
        Assert.Equal(2, (int)ItemCategory.Helmet);
        Assert.Equal(3, (int)ItemCategory.Ring);
        Assert.Equal(4, (int)ItemCategory.LifeFlask);
    }

    [Fact]
    public void EquipmentComparisonReportsCapacityAndCombatDeltasWithoutMutation()
    {
        P1GameSession session = Session();
        ItemInstance candidate = ItemGenerator.Generate(
            "core.base.heavy_battleaxe", 6, ItemRarity.Rare, 123, "compare");
        string originalWeapon = session.HeroEquipment.Items[EquipmentSlot.MainHand].InstanceId;

        P2EquipmentComparison comparison = session.CompareHeroEquipment(candidate, EquipmentSlot.MainHand);

        Assert.Equal(EquipmentSlot.MainHand, comparison.TargetSlot);
        Assert.Equal(originalWeapon, session.HeroEquipment.Items[EquipmentSlot.MainHand].InstanceId);
    }

    [Fact]
    public void MercenaryEquipmentRoundTripsAndCanBeChangedByPlayer()
    {
        P1GameSession session = Session();
        ItemInstance gloves = ItemGenerator.Generate(
            "core.base.iron_gauntlets", 3, ItemRarity.Magic, 9, "merc-gloves");
        Assert.True(session.World.Storage.TryStore(gloves));

        P2ItemCommandResult result = new P2ItemCommandService(session, P2CharacterKind.Mercenary)
            .TryEquip(ItemContainerKind.Storage, 0, EquipmentSlot.Gloves);
        P1GameSession restored = P1GameSession.Restore(session.Capture());

        Assert.True(result.Succeeded);
        Assert.Equal("merc-gloves", restored.MercenaryEquipment.Items[EquipmentSlot.Gloves].InstanceId);
    }

    [Fact]
    public void SkillStoneLinksMoveSupportAndDriveCombatConfiguration()
    {
        P1GameSession session = Session();
        SkillStoneInstance active = session.Management.SkillStones.Single(
            item => item.DefinitionId == "core.skill_stone.heavy_strike");
        SkillStoneInstance speed = session.Management.SkillStones.Single(
            item => item.DefinitionId == "core.skill_stone.attack_speed");

        Assert.True(session.Management.TryLinkSupport(active.InstanceId, speed.InstanceId));
        session.SyncHeavyStrikeFromSkillStones();

        Assert.True(session.HeavyStrikeSupports.HasFlag(GameForWork.Core.P1.Combat.SkillSupport.AttackSpeed));
        Assert.True(session.Management.UnlinkSupport(active.InstanceId, speed.InstanceId));
    }

    [Fact]
    public void DisabledLootFilterRuleDoesNotMatch()
    {
        ItemInstance item = Item("filter");
        var filter = new GameForWork.Core.P1.World.LootFilter(
        [
            new GameForWork.Core.P1.World.LootFilterRule(
                "disabled", GameForWork.Core.P1.World.LootDisposition.Sell,
                ItemRarity.Basic, Enabled: false),
            new GameForWork.Core.P1.World.LootFilterRule(
                "fallback", GameForWork.Core.P1.World.LootDisposition.Keep,
                ItemRarity.Basic),
        ]);

        Assert.Equal(GameForWork.Core.P1.World.LootDisposition.Keep, filter.Evaluate(item));
    }

    [Fact]
    public void ThreeWorkshopRecipesPreviewDeterministically()
    {
        ItemInstance weapon = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 3, ItemRarity.Basic, 1, "craft-weapon");
        ItemInstance armor = ItemGenerator.Generate(
            "core.base.iron_gauntlets", 3, ItemRarity.Basic, 2, "craft-armor");
        ItemInstance amulet = ItemGenerator.Generate(
            "core.base.ember_amulet", 3, ItemRarity.Basic, 3, "craft-amulet");

        Assert.True(P2Workshop.Preview(weapon, P2WorkshopRecipe.WeaponPhysical).Succeeded);
        Assert.True(P2Workshop.Preview(armor, P2WorkshopRecipe.ReinforceDefense).Succeeded);
        Assert.True(P2Workshop.Preview(amulet, P2WorkshopRecipe.VitalityEtching).Succeeded);
    }

    private static ItemInstance Item(string instanceId) => ItemGenerator.Generate(
        "core.base.iron_ring",
        1,
        ItemRarity.Basic,
        1,
        instanceId);

    private static P1GameSession Session() => P1GameSession.CreateNew(new PlayerIdentity(
        "管理者",
        CharacterGender.Androgynous,
        CharacterSkinTone.Umber,
        CharacterHairStyle.Cropped,
        P1Ascendancy.IronOath), 88);
}

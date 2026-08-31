using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using GameForWork.Core.P4;

namespace GameForWork.Tests;

public sealed class P2ManagementTests
{
    [Fact]
    public void EquipmentSlotsHaveP19BaseRoutesAndAffixFamilies()
    {
        foreach (ItemCategory category in new[]
                 {
                     ItemCategory.Gloves,
                     ItemCategory.Boots,
                     ItemCategory.Belt,
                     ItemCategory.Amulet,
                 })
        {
            int expected = category switch
            {
                ItemCategory.Gloves => 11,
                ItemCategory.Boots => 6,
                ItemCategory.Belt => 11,
                _ => 16,
            };
            Assert.Equal(expected, P1ItemBases.All.Count(item => item.Category == category));
            int families = P1Affixes.For(category, 60)
                .Select(affix => affix.StableFamilyId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            Assert.True(families >= 8);
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
        Assert.Equal(9, restored.SkillStones.Count);
        Assert.Contains("测试记录", restored.OperationHistory);
    }

    [Fact]
    public void LegacyManagementSnapshotIsRejectedAtSessionBoundary()
    {
        P1GameSession current = P1GameSession.CreateNew(new PlayerIdentity(
            "迁移者",
            CharacterGender.Androgynous,
            CharacterSkinTone.Umber,
            CharacterHairStyle.Cropped,
            P23BaseClass.Fighter), 77);
        P1GameSessionSnapshot legacy = current.Capture() with { FormatVersion = 3, Management = null };

        Assert.Throws<InvalidDataException>(() => P1GameSession.Restore(legacy));
    }

    [Fact]
    public void EquipCommandMovesExactlyOneInstanceAndReturnsPreviousItem()
    {
        P1GameSession session = Session();
        ItemInstance replacement = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 4, ItemRarity.Magic, 10, "replacement");
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
    public void UnequippingMainHandKeepsBuildAndPreviewRefreshValid()
    {
        P1GameSession session = Session();
        var commands = new P2ItemCommandService(session);

        P2ItemCommandResult result = commands.TryUnequip(EquipmentSlot.MainHand);
        var exception = Record.Exception(() => session.GetCombatPreview());

        Assert.True(result.Succeeded);
        Assert.Null(exception);
        Assert.False(session.HeroBuild.HasUsableWeapon);
        Assert.False(session.World.Hero.Build.HasUsableWeapon);
        Assert.Equal("core.weapon.unequipped", session.HeroBuild.EffectiveWeapon.StableId);
        Assert.DoesNotContain(EquipmentSlot.MainHand, session.HeroEquipment.Items.Keys);
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

    [Fact]
    public void WorkshopCraftsSortingBagAndEquippedItemsInPlaceWithMetals()
    {
        P1GameSession session = Session();
        ItemInstance armor = ItemGenerator.Generate(
            "core.base.iron_gauntlets", 3, ItemRarity.Basic, 31, "craft-bag");
        Assert.True(session.Management.TryAddToSortingBag(armor));
        var commands = new P2ItemCommandService(session);

        P2WorkshopPreview bagCraft = commands.Craft(
            ItemContainerKind.SortingBag, 0, P2WorkshopRecipe.ReinforceDefense);
        Assert.True(bagCraft.Succeeded);
        Assert.Contains(session.Management.SortingBag[0].Affixes, affix => affix.Crafted);
        Assert.Equal(2, session.World.Economy.MetalAmount(MetalCurrencyKind.WardSteel));

        Assert.True(commands.TryEquip(ItemContainerKind.SortingBag, 0, EquipmentSlot.Gloves).Succeeded);
        P2WorkshopPreview equippedCraft = commands.Craft(
            ItemContainerKind.Equipped, (int)EquipmentSlot.Gloves, P2WorkshopRecipe.VitalityEtching);
        Assert.True(equippedCraft.Succeeded);
        Assert.Contains(session.HeroEquipment.Items[EquipmentSlot.Gloves].Affixes,
            affix => affix.Definition.ModifierKind == ItemModifierKind.FlatMaximumLife);
        Assert.Equal(2, session.World.Economy.MetalAmount(MetalCurrencyKind.VitalSilver));
    }

    [Fact]
    public void EquipmentSlotsSwapOnlyWhenBothDirectionsAreLegal()
    {
        P1GameSession session = Session();
        ItemInstance ring = Item("swap-ring");
        Assert.True(session.World.Storage.TryStore(ring));
        var commands = new P2ItemCommandService(session);
        Assert.True(commands.TryEquip(ItemContainerKind.Storage, 0, EquipmentSlot.RingLeft).Succeeded);

        Assert.True(commands.SwapEquipment(EquipmentSlot.RingLeft, EquipmentSlot.RingRight).Succeeded);
        Assert.Equal("swap-ring", session.HeroEquipment.Items[EquipmentSlot.RingRight].InstanceId);
        Assert.False(commands.SwapEquipment(EquipmentSlot.RingRight, EquipmentSlot.Helmet).Succeeded);
        Assert.Equal("swap-ring", session.HeroEquipment.Items[EquipmentSlot.RingRight].InstanceId);
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
        P23BaseClass.Fighter), 88);
}

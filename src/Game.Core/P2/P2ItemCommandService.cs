using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using System.Runtime.CompilerServices;
using GameForWork.Core.P6;
using GameForWork.Core.P4;
using GameForWork.Core.P9;
using GameForWork.Core.P14;
using GameForWork.Core.P29;

namespace GameForWork.Core.P2;

public sealed record P2ItemCommandResult(bool Succeeded, string Code, string Message)
{
    public static P2ItemCommandResult Ok(string message) => new(true, string.Empty, message);
    public static P2ItemCommandResult Fail(string code, string message) => new(false, code, message);
}

public sealed class P2ItemCommandService(
    P1GameSession session,
    P2CharacterKind character = P2CharacterKind.Hero,
    string mercenaryId = "")
{
    private static readonly ConditionalWeakTable<P1GameSession, UndoState> UndoStates = new();
    private EquipmentLoadout Loadout => character == P2CharacterKind.Hero
        ? session.HeroEquipment
        : string.IsNullOrEmpty(mercenaryId)
            ? session.MercenaryEquipment
            : session.Town.Roster.First(member => member.Identity.StableId == mercenaryId).Equipment;

    public P2ItemCommandResult TryEquip(ItemContainerKind source, int index, EquipmentSlot slot)
    {
        if (source == ItemContainerKind.Equipped)
        {
            return SwapEquipment((EquipmentSlot)index, slot);
        }

        ItemInstance? candidate = Peek(source, index);
        if (candidate is null)
        {
            return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
        }

        if (!EquipmentLoadout.CanEquip(slot, candidate.Base.Category))
        {
            return P2ItemCommandResult.Fail("slot_mismatch", "该物品不能放入目标装备槽。");
        }
        if (!MeetsRequirements(candidate))
        {
            return P2ItemCommandResult.Fail("requirements_not_met",
                $"需求不足：等级 {candidate.Base.RequiredLevel}，体魄 {candidate.Base.RequiredPhysique}，" +
                $"灵巧 {candidate.Base.RequiredDexterity}，精神 {candidate.Base.RequiredSpirit}，能量 {candidate.Base.RequiredEnergy}。");
        }
        if (slot is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5 &&
            (int)slot - (int)EquipmentSlot.Flask1 >= session.UnlockedFlaskSlots)
            return P2ItemCommandResult.Fail("flask_slot_locked", "升级传送装置可开放更多药剂槽。");

        ItemInstance? removed = Take(source, index);
        if (removed is null)
        {
            return P2ItemCommandResult.Fail("item_changed", "物品位置已经变化。");
        }

        ItemInstance? previous = Loadout.Unequip(slot);
        ItemInstance? displacedOffhand = candidate.Base.Category == ItemCategory.TwoHandWeapon
            ? Loadout.Unequip(EquipmentSlot.OffHand)
            : null;
        if (!Loadout.TryEquip(slot, removed))
        {
            if (previous is not null)
            {
                Loadout.TryEquip(slot, previous);
            }

            ReturnToSource(source, index, removed);
            if (displacedOffhand is not null)
            {
                Loadout.TryEquip(EquipmentSlot.OffHand, displacedOffhand);
            }
            return P2ItemCommandResult.Fail("equip_failed", "换装失败，操作已回滚。");
        }

        if (previous is not null)
        {
            ReturnDisplaced(source, index, previous);
        }

        if (displacedOffhand is not null)
        {
            ReturnDisplaced(source, index, displacedOffhand);
        }

        session.NotifyEquipmentChanged(character);
        if (character == P2CharacterKind.Hero) session.RecordJourneyEvent(P8JourneyEvent.EquippedItem);
        session.Management.AddHistory($"已装备 {removed.Base.DisplayName}。 ");
        return P2ItemCommandResult.Ok($"已装备 {removed.Base.DisplayName}。");
    }

    public P2ItemCommandResult SwapEquipment(EquipmentSlot source, EquipmentSlot target)
    {
        if (source == target)
        {
            return P2ItemCommandResult.Ok("装备已在目标位置。");
        }

        ItemInstance? sourceItem = Loadout.Items.GetValueOrDefault(source);
        if (sourceItem is null)
        {
            return P2ItemCommandResult.Fail("item_missing", "来源装备槽为空。");
        }

        ItemInstance? targetItem = Loadout.Items.GetValueOrDefault(target);
        if (!EquipmentLoadout.CanEquip(target, sourceItem.Base.Category) ||
            targetItem is not null && !EquipmentLoadout.CanEquip(source, targetItem.Base.Category))
        {
            return P2ItemCommandResult.Fail("slot_mismatch", "这两个装备槽不能交换物品。");
        }

        Loadout.Unequip(source);
        Loadout.Unequip(target);
        bool placedSource = Loadout.TryEquip(target, sourceItem);
        bool placedTarget = targetItem is null || Loadout.TryEquip(source, targetItem);
        if (!placedSource || !placedTarget)
        {
            Loadout.Unequip(source);
            Loadout.Unequip(target);
            Loadout.TryEquip(source, sourceItem);
            if (targetItem is not null)
            {
                Loadout.TryEquip(target, targetItem);
            }
            return P2ItemCommandResult.Fail("equip_failed", "交换失败，装备已回滚。");
        }

        session.NotifyEquipmentChanged(character);
        if (character == P2CharacterKind.Hero) session.RecordJourneyEvent(P8JourneyEvent.EquippedItem);
        session.Management.AddHistory($"已将 {sourceItem.Base.DisplayName} 移至{target}。");
        return P2ItemCommandResult.Ok("装备槽交换完成。");
    }

    private bool MeetsRequirements(ItemInstance item)
    {
        if (character == P2CharacterKind.Hero)
        {
            return item.Base.MeetsRequirements(session.World.Hero.Progression.Level, session.HeroBuild.Sheet.Attributes);
        }

        P9MercenaryMember member = string.IsNullOrEmpty(mercenaryId)
            ? session.Town.Roster.First()
            : session.Town.Roster.First(candidate => candidate.Identity.StableId == mercenaryId);
        return item.Base.MeetsRequirements(member.Level, member.Identity.FinalAttributes);
    }

    public P2WorkshopPreview Craft(ItemContainerKind source, int index, P2WorkshopRecipe recipe)
    {
        ItemInstance? item = PeekIncludingEquipped(source, index);
        if (item is null)
        {
            return new P2WorkshopPreview(false, "item_missing", null, 0, 0, "物品不存在。");
        }

        P2WorkshopPreview result = P2Workshop.Craft(session.World.Economy, item, recipe);
        if (!result.Succeeded)
        {
            return result;
        }

        bool replaced = Replace(source, index, result.Result!);
        if (!replaced)
        {
            if (result.MetalCostKind is GameForWork.Core.P4.MetalCurrencyKind metal)
            {
                session.World.Economy.AddMetal(metal, result.MetalCost);
            }
            return result with { Succeeded = false, FailureReason = "replace_failed", Summary = "物品位置已变化，制作已回滚。" };
        }

        if (source == ItemContainerKind.Equipped)
        {
            session.NotifyEquipmentChanged(character);
        }
        session.Management.AddHistory($"已对 {item.Base.DisplayName} 完成制作：{result.Summary}。");
        session.RecordJourneyEvent(P8JourneyEvent.CraftedItem);
        return result;
    }

    public P6CraftPreview CraftP6(
        ItemContainerKind source,
        int index,
        P6CraftOperation operation,
        string fractureFamilyId = "")
    {
        ItemInstance? item = PeekIncludingEquipped(source, index);
        if (item is null)
        {
            return new P6CraftPreview(false, "item_missing", "物品不存在。", null,
                MetalCurrencyKind.ChainSteel, 0, 0, 0);
        }
        P6CraftPreview result = P6CraftingRules.Craft(session.World.Economy, item, operation, fractureFamilyId);
        if (!result.Succeeded) return result;
        if (!Replace(source, index, result.Result!))
        {
            session.World.Economy.AddMetal(result.Currency, result.Cost);
            return result with { Succeeded = false, FailureReason = "replace_failed", Summary = "物品位置变化，制作与材料已回滚。" };
        }
        if (source == ItemContainerKind.Equipped)
        {
            session.NotifyEquipmentChanged(character);
        }
        session.Management.AddHistory($"制作完成：{result.Summary}。 ");
        session.RecordJourneyEvent(P8JourneyEvent.CraftedItem);
        return result;
    }

    public P9CraftResult CraftP9(ItemContainerKind source, int index, P9CraftOperation operation)
    {
        ItemInstance? item = PeekIncludingEquipped(source, index);
        if (item is null)
            return new(false, "item_missing", "物品不存在。", null, MetalCurrencyKind.AwakeningCopper, 0);
        P9CraftResult result = P9CraftingRules.Craft(session.World.Economy, item, operation);
        if (!result.Succeeded) return result;
        bool applied = result.Destroyed ? RemoveIncludingEquipped(source, index) is not null : Replace(source, index, result.Result!);
        if (!applied)
        {
            session.World.Economy.AddMetal(result.Currency, result.Cost);
            return result with { Succeeded = false, FailureReason = "replace_failed", Summary = "物品位置变化，制作与材料已回滚。" };
        }
        if (source == ItemContainerKind.Equipped) session.NotifyEquipmentChanged(character);
        session.Management.AddHistory($"金属加工：{result.Summary}。 ");
        session.RecordJourneyEvent(P8JourneyEvent.CraftedItem);
        return result;
    }

    public P9CraftResult EnchantP9(ItemContainerKind source, int index, string enchantmentId)
    {
        ItemInstance? item = PeekIncludingEquipped(source, index);
        if (item is null)
            return new(false, "item_missing", "物品不存在。", null, MetalCurrencyKind.TemperingIron, 0);
        P9CraftResult result = P9EnchantmentCatalog.Craft(session.World.Economy, item, enchantmentId,
            session.Town.Level(P9BuildingKind.Workshop));
        if (!result.Succeeded) return result;
        if (!Replace(source, index, result.Result!))
        {
            ItemEnchantment enchantment = P9EnchantmentCatalog.Get(enchantmentId);
            session.World.Economy.AddDispositionProceeds(enchantment.GoldCost, 0);
            return result with { Succeeded = false, FailureReason = "replace_failed", Summary = "物品位置变化，金币已返还。" };
        }
        if (source == ItemContainerKind.Equipped) session.NotifyEquipmentChanged(character);
        session.Management.AddHistory(result.Summary);
        return result;
    }

    public P14GardenCraftResult CraftP14(ItemContainerKind source, int index, P14GardenCraft craft)
    {
        ItemInstance? item = PeekIncludingEquipped(source, index);
        int cost = P14GardenCrafting.Cost(craft);
        if (item is null) return new(false, "物品不存在。", null, cost);
        if (!item.CanModify || item.Rarity != ItemRarity.Rare)
            return new(false, "命能加工要求未锁定、未腐化的稀有装备。", null, cost);
        if (session.Endgame.LifeForce < cost) return new(false, $"命能不足，需要 {cost}。", null, cost);
        if (!P14GardenCrafting.CanApply(item, craft)) return new(false, "当前底材没有合法的目标词缀，未消耗命能。", null, cost);
        ItemInstance result = P14GardenCrafting.Apply(item, craft,
            session.Seed ^ (ulong)session.Endgame.GameplayOperationSequence * 0x9e3779b97f4a7c15UL ^ (ulong)index);
        if (!Replace(source, index, result)) return new(false, "物品位置变化，命能未消耗。", null, cost);
        if (!session.Endgame.TrySpendLifeForce(cost)) throw new InvalidOperationException("Life force changed during crafting.");
        session.Endgame.CompleteGameplayOperation();
        if (source == ItemContainerKind.Equipped) session.NotifyEquipmentChanged(character);
        session.Management.AddHistory($"命能加工：{craft}，消耗 {cost} 命能。");
        session.RecordJourneyEvent(P8JourneyEvent.CraftedItem);
        return new(true, $"{craft} 完成，消耗 {cost} 命能。", result, cost);
    }

    public P29ResourceCraftResult CraftP29Red(ItemContainerKind source, int index, string affixFamilyId)
    {
        ItemInstance? item = PeekIncludingEquipped(source, index);
        if (item is null) return new(false, "物品不存在。", null, P29ResourceCrafting.RedFavorCost);
        if (session.Endgame.RedFavor < P29ResourceCrafting.RedFavorCost) return new(false, "赤誓收益不足。", null, P29ResourceCrafting.RedFavorCost);
        P29ResourceCraftResult result = P29ResourceCrafting.ShiftAffixTier(item, affixFamilyId, CraftSeed(index));
        if (!result.Succeeded || !Replace(source, index, result.Result!)) return result with { Succeeded = false };
        if (!session.Endgame.TrySpendRedFavor(result.Cost)) throw new InvalidOperationException("Red favor changed during crafting.");
        FinishResourceCraft(source, result.Summary); return result;
    }

    public P29ResourceCraftResult CraftP29Blue(ItemContainerKind source, int index)
    {
        ItemInstance? item = PeekIncludingEquipped(source, index);
        if (item is null) return new(false, "物品不存在。", null, P29ResourceCrafting.BlueFavorCost);
        if (session.Endgame.BlueFavor < P29ResourceCrafting.BlueFavorCost) return new(false, "苍誓收益不足。", null, P29ResourceCrafting.BlueFavorCost);
        P29ResourceCraftResult result = P29ResourceCrafting.RerollQuality(item, CraftSeed(index));
        if (!result.Succeeded || !Replace(source, index, result.Result!)) return result with { Succeeded = false };
        if (!session.Endgame.TrySpendBlueFavor(result.Cost)) throw new InvalidOperationException("Blue favor changed during crafting.");
        FinishResourceCraft(source, result.Summary); return result;
    }

    private ulong CraftSeed(int index) => session.Seed ^ (ulong)session.Endgame.GameplayOperationSequence * 0x9e3779b97f4a7c15UL ^ (ulong)index;
    private void FinishResourceCraft(ItemContainerKind source, string summary)
    {
        session.Endgame.CompleteGameplayOperation();
        if (source == ItemContainerKind.Equipped) session.NotifyEquipmentChanged(character);
        session.Management.AddHistory(summary); session.RecordJourneyEvent(P8JourneyEvent.CraftedItem);
    }

    public P2ItemCommandResult TryUnequip(EquipmentSlot slot)
    {
        ItemInstance? item = Loadout.Unequip(slot);
        if (item is null)
        {
            return P2ItemCommandResult.Fail("slot_empty", "装备槽为空。");
        }

        if (!session.Management.TryAddToSortingBag(item) && !session.World.Storage.TryStore(item))
        {
            session.Management.AddToRecovery(item, "卸装时整理背包与仓库已满");
        }

        session.NotifyEquipmentChanged(character);
        return P2ItemCommandResult.Ok($"已卸下 {item.Base.DisplayName}。");
    }

    public P2ItemCommandResult QuickTransfer(ItemContainerKind source, int index)
    {
        if (source == ItemContainerKind.Storage)
        {
            ItemInstance? item = session.World.Storage.TakeAt(index);
            if (item is null)
            {
                return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
            }

            if (!session.Management.TryAddToSortingBag(item))
            {
                session.World.Storage.TryStore(item);
                return P2ItemCommandResult.Fail("sorting_full", "整理背包已满。");
            }

            return P2ItemCommandResult.Ok($"{item.Base.DisplayName} 已移入整理背包。");
        }

        if (source == ItemContainerKind.SortingBag)
        {
            ItemInstance? item = session.Management.TakeSortingBagAt(index);
            if (item is null)
            {
                return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
            }

            if (!session.World.Storage.TryStore(item))
            {
                session.Management.ReturnToSortingBag(item, index);
                return P2ItemCommandResult.Fail("storage_full", "仓库已满。");
            }

            session.Management.AddHistory($"{item.Base.DisplayName} 已移入仓库。");
            return P2ItemCommandResult.Ok($"{item.Base.DisplayName} 已移入仓库。");
        }

        if (source == ItemContainerKind.Recovery)
        {
            ItemInstance? item = session.Management.TakeRecoveryAt(index);
            if (item is null)
            {
                return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
            }

            if (!session.Management.TryAddToSortingBag(item) && !session.World.Storage.TryStore(item))
            {
                session.Management.AddToRecovery(item, "整理背包与仓库均已满");
                return P2ItemCommandResult.Fail("containers_full", "整理背包和仓库都已满。");
            }

            return P2ItemCommandResult.Ok($"已从恢复箱取出 {item.Base.DisplayName}。");
        }

        return P2ItemCommandResult.Fail("unsupported_source", "该容器不支持快速转移。");
    }

    public P2ItemCommandResult Move(
        ItemContainerKind source,
        int sourceIndex,
        ItemContainerKind target,
        int targetIndex)
    {
        if (source == target)
        {
            bool reordered = source switch
            {
                ItemContainerKind.Storage => session.World.Storage.TryMove(sourceIndex, targetIndex),
                ItemContainerKind.SortingBag => session.Management.TryMoveSortingBag(sourceIndex, targetIndex),
                _ => false,
            };
            if (!reordered)
            {
                return P2ItemCommandResult.Fail("reorder_failed", "该容器不能调整顺序。");
            }

            RegisterUndo(() => new P2ItemCommandService(session, character, mercenaryId)
                .Move(source, targetIndex, source, sourceIndex));
            return P2ItemCommandResult.Ok("物品顺序已调整，可撤销。");
        }

        if (target is not (ItemContainerKind.Storage or ItemContainerKind.SortingBag))
        {
            return P2ItemCommandResult.Fail("target_invalid", "目标容器不接受手动放入。");
        }

        ItemInstance? item = Take(source, sourceIndex);
        if (item is null)
        {
            return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
        }

        bool inserted = target == ItemContainerKind.Storage
            ? session.World.Storage.TryInsert(item, targetIndex)
            : session.Management.TryInsertSortingBag(item, targetIndex);
        if (!inserted)
        {
            ReturnToSource(source, sourceIndex, item);
            return P2ItemCommandResult.Fail("target_full", "目标容器已满，操作已回滚。");
        }

        RegisterUndo(() => new P2ItemCommandService(session, character, mercenaryId)
            .Move(target, targetIndex, source, sourceIndex));
        return P2ItemCommandResult.Ok($"已移动 {item.Base.DisplayName}，可撤销。");
    }

    public P2ItemCommandResult UndoLastMovement()
    {
        UndoState state = UndoStates.GetOrCreateValue(session);
        if (state.Actions.Count == 0)
        {
            return P2ItemCommandResult.Fail("nothing_to_undo", "没有可撤销的物品移动。");
        }

        Func<P2ItemCommandResult> action = state.Actions.Pop();
        state.Applying = true;
        try
        {
            P2ItemCommandResult result = action();
            return result.Succeeded
                ? P2ItemCommandResult.Ok("已撤销上一次物品移动。")
                : P2ItemCommandResult.Fail("undo_invalid", $"无法撤销：{result.Message}");
        }
        finally
        {
            state.Applying = false;
        }
    }

    public P2ItemCommandResult ToggleLock(ItemContainerKind source, int index, EquipmentSlot? slot = null)
    {
        ItemInstance? item = source == ItemContainerKind.Equipped && slot is not null
            ? Loadout.Items.GetValueOrDefault(slot.Value)
            : Peek(source, index);
        if (item is null)
        {
            return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
        }

        ItemInstance updated = item.WithLocked(!item.IsLocked);
        bool replaced = source switch
        {
            ItemContainerKind.Storage => session.World.Storage.TryReplaceAt(index, updated),
            ItemContainerKind.SortingBag => ReplaceSorting(index, updated),
            ItemContainerKind.Recovery => ReplaceRecovery(index, updated),
            ItemContainerKind.Equipped when slot is not null => Loadout.TryEquip(slot.Value, updated),
            _ => false,
        };
        if (!replaced)
        {
            return P2ItemCommandResult.Fail("replace_failed", "物品状态没有更新。");
        }

        if (source == ItemContainerKind.Equipped)
        {
            session.NotifyEquipmentChanged(character);
        }

        session.Management.AddHistory($"{updated.Base.DisplayName} 已{(updated.IsLocked ? "锁定" : "解锁")}。");
        return P2ItemCommandResult.Ok(updated.IsLocked ? "物品已锁定。" : "物品已解除锁定。");
    }

    public P2ItemCommandResult ToggleCraftingBase(ItemContainerKind source, int index)
    {
        ItemInstance? item = Peek(source, index);
        if (item is null)
        {
            return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
        }

        ItemInstance updated = item.WithCraftingBase(!item.IsCraftingBase);
        bool replaced = source switch
        {
            ItemContainerKind.Storage => session.World.Storage.TryReplaceAt(index, updated),
            ItemContainerKind.SortingBag => ReplaceSorting(index, updated),
            _ => false,
        };
        if (!replaced)
        {
            return P2ItemCommandResult.Fail("replace_failed", "物品标记没有更新。");
        }

        session.Management.AddHistory($"{updated.Base.DisplayName} 已{(updated.IsCraftingBase ? "标记为" : "取消")}制作底材。");
        return P2ItemCommandResult.Ok(updated.IsCraftingBase ? "已标记制作底材。" : "已取消制作底材标记。");
    }

    public P2ItemCommandResult Sell(ItemContainerKind source, int index)
    {
        ItemInstance? item = Peek(source, index);
        if (item is null)
        {
            return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
        }

        if (item.IsLocked)
        {
            return P2ItemCommandResult.Fail("item_locked", "锁定物品不能出售。");
        }

        item = Take(source, index);
        if (item is null)
        {
            return P2ItemCommandResult.Fail("item_changed", "物品位置已经变化。");
        }

        int price = P2ManagementState.SalePrice(item);
        session.World.Economy.AddDispositionProceeds(price, 0);
        session.Management.AddBuyback(item, price);
        return P2ItemCommandResult.Ok($"已出售 {item.Base.DisplayName}，获得 {price} 金币。");
    }

    public P2ItemCommandResult Dismantle(ItemContainerKind source, int index, bool confirmed)
    {
        ItemInstance? item = Peek(source, index);
        if (item is null)
        {
            return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
        }

        if (item.IsLocked)
        {
            return P2ItemCommandResult.Fail("item_locked", "锁定物品不能分解。");
        }

        if (item.Rarity is ItemRarity.Rare or ItemRarity.Legendary && !confirmed)
        {
            return P2ItemCommandResult.Fail("confirmation_required", "稀有或传奇物品需要确认分解。");
        }

        item = Take(source, index);
        if (item is null)
        {
            return P2ItemCommandResult.Fail("item_changed", "物品位置已经变化。");
        }

        int scraps = item.Rarity switch
        {
            ItemRarity.Basic => 1,
            ItemRarity.Magic => 2,
            ItemRarity.Rare => 5,
            ItemRarity.Legendary => 12,
            _ => 0,
        };
        session.World.Economy.AddDispositionProceeds(0, scraps);
        session.Management.AddHistory($"已分解 {item.Base.DisplayName}，获得 {scraps} 铁屑。");
        return P2ItemCommandResult.Ok($"已分解 {item.Base.DisplayName}。");
    }

    public P2ItemCommandResult BuyBack(int index)
    {
        if (index < 0 || index >= session.Management.Buyback.Count)
        {
            return P2ItemCommandResult.Fail("item_missing", "回购物品不存在。");
        }

        BuybackEntry entry = session.Management.Buyback[index];
        bool hasBagSpace = session.Management.SortingBag.Count < P2ManagementState.SortingBagCapacity;
        bool hasStorageSpace = !session.World.Storage.IsFull;
        if (!hasBagSpace && !hasStorageSpace)
        {
            return P2ItemCommandResult.Fail("containers_full", "整理背包和仓库都已满。");
        }

        if (!session.World.Economy.TrySpendGold(entry.SalePrice))
        {
            return P2ItemCommandResult.Fail("insufficient_gold", "金币不足。");
        }

        BuybackEntry? removed = session.Management.TakeBuybackAt(index);
        if (removed is null)
        {
            session.World.Economy.AddDispositionProceeds(entry.SalePrice, 0);
            return P2ItemCommandResult.Fail("item_changed", "回购列表已经变化。");
        }

        if (!session.Management.TryAddToSortingBag(removed.Item) && !session.World.Storage.TryStore(removed.Item))
        {
            session.Management.AddToRecovery(removed.Item, "回购落位失败");
        }

        session.Management.AddHistory($"已回购 {removed.Item.Base.DisplayName}。");
        return P2ItemCommandResult.Ok($"已回购 {removed.Item.Base.DisplayName}。");
    }

    private ItemInstance? Peek(ItemContainerKind source, int index) => source switch
    {
        ItemContainerKind.Storage when index >= 0 && index < session.World.Storage.Items.Count =>
            session.World.Storage.Items[index],
        ItemContainerKind.SortingBag when index >= 0 && index < session.Management.SortingBag.Count =>
            session.Management.SortingBag[index],
        ItemContainerKind.Recovery when index >= 0 && index < session.Management.Recovery.Count =>
            session.Management.Recovery[index],
        _ => null,
    };

    private ItemInstance? PeekIncludingEquipped(ItemContainerKind source, int index) =>
        source == ItemContainerKind.Equipped && Enum.IsDefined(typeof(EquipmentSlot), index)
            ? Loadout.Items.GetValueOrDefault((EquipmentSlot)index)
            : Peek(source, index);

    private bool Replace(ItemContainerKind source, int index, ItemInstance item) => source switch
    {
        ItemContainerKind.Storage => session.World.Storage.TryReplaceAt(index, item),
        ItemContainerKind.SortingBag => ReplaceSorting(index, item),
        ItemContainerKind.Equipped when Enum.IsDefined(typeof(EquipmentSlot), index) =>
            Loadout.TryEquip((EquipmentSlot)index, item),
        _ => false,
    };

    private void RegisterUndo(Func<P2ItemCommandResult> action)
    {
        UndoState state = UndoStates.GetOrCreateValue(session);
        if (state.Applying)
        {
            return;
        }

        state.Actions.Push(action);
        if (state.Actions.Count > 12)
        {
            state.Actions = new Stack<Func<P2ItemCommandResult>>(state.Actions.Take(12).Reverse());
        }
    }

    private ItemInstance? Take(ItemContainerKind source, int index) => source switch
    {
        ItemContainerKind.Storage => session.World.Storage.TakeAt(index),
        ItemContainerKind.SortingBag => session.Management.TakeSortingBagAt(index),
        ItemContainerKind.Recovery => session.Management.TakeRecoveryAt(index),
        _ => null,
    };

    private ItemInstance? RemoveIncludingEquipped(ItemContainerKind source, int index) =>
        source == ItemContainerKind.Equipped && Enum.IsDefined(typeof(EquipmentSlot), index)
            ? Loadout.Unequip((EquipmentSlot)index)
            : Take(source, index);

    private void ReturnToSource(ItemContainerKind source, int index, ItemInstance item)
    {
        if (source == ItemContainerKind.SortingBag)
        {
            session.Management.ReturnToSortingBag(item, index);
        }
        else if (source == ItemContainerKind.Storage && !session.World.Storage.TryStore(item))
        {
            session.Management.AddToRecovery(item, "回滚时仓库已满");
        }
        else if (source == ItemContainerKind.Recovery)
        {
            session.Management.AddToRecovery(item, "操作回滚");
        }
    }

    private void ReturnDisplaced(ItemContainerKind source, int index, ItemInstance item)
    {
        if (source == ItemContainerKind.SortingBag)
        {
            session.Management.ReturnToSortingBag(item, index);
            return;
        }

        if (source == ItemContainerKind.Storage && session.World.Storage.TryStore(item) ||
            session.Management.TryAddToSortingBag(item) ||
            session.World.Storage.TryStore(item))
        {
            return;
        }

        session.Management.AddToRecovery(item, "换装替换物品无可用容器");
    }

    private bool ReplaceSorting(int index, ItemInstance item)
    {
        ItemInstance? removed = session.Management.TakeSortingBagAt(index);
        if (removed is null)
        {
            return false;
        }

        session.Management.ReturnToSortingBag(item, index);
        return true;
    }

    private bool ReplaceRecovery(int index, ItemInstance item)
    {
        ItemInstance? removed = session.Management.TakeRecoveryAt(index);
        if (removed is null)
        {
            return false;
        }

        session.Management.AddToRecovery(item, "状态更新");
        return true;
    }

    private sealed class UndoState
    {
        public Stack<Func<P2ItemCommandResult>> Actions { get; set; } = new();
        public bool Applying { get; set; }
    }
}

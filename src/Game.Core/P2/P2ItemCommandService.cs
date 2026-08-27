using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;

namespace GameForWork.Core.P2;

public sealed record P2ItemCommandResult(bool Succeeded, string Code, string Message)
{
    public static P2ItemCommandResult Ok(string message) => new(true, string.Empty, message);
    public static P2ItemCommandResult Fail(string code, string message) => new(false, code, message);
}

public sealed class P2ItemCommandService(P1GameSession session, P2CharacterKind character = P2CharacterKind.Hero)
{
    private EquipmentLoadout Loadout => character == P2CharacterKind.Hero
        ? session.HeroEquipment
        : session.MercenaryEquipment;

    public P2ItemCommandResult TryEquip(ItemContainerKind source, int index, EquipmentSlot slot)
    {
        ItemInstance? candidate = Peek(source, index);
        if (candidate is null)
        {
            return P2ItemCommandResult.Fail("item_missing", "物品不存在。");
        }

        if (!EquipmentLoadout.CanEquip(slot, candidate.Base.Category))
        {
            return P2ItemCommandResult.Fail("slot_mismatch", "该物品不能放入目标装备槽。");
        }

        ItemInstance? removed = Take(source, index);
        if (removed is null)
        {
            return P2ItemCommandResult.Fail("item_changed", "物品位置已经变化。");
        }

        ItemInstance? previous = Loadout.Unequip(slot);
        if (!Loadout.TryEquip(slot, removed))
        {
            if (previous is not null)
            {
                Loadout.TryEquip(slot, previous);
            }

            ReturnToSource(source, index, removed);
            return P2ItemCommandResult.Fail("equip_failed", "换装失败，操作已回滚。");
        }

        if (previous is not null)
        {
            ReturnDisplaced(source, index, previous);
        }

        session.NotifyEquipmentChanged(character);
        session.Management.AddHistory($"已装备 {removed.Base.DisplayName}。 ");
        return P2ItemCommandResult.Ok($"已装备 {removed.Base.DisplayName}。");
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
            return reordered
                ? P2ItemCommandResult.Ok("物品顺序已调整。")
                : P2ItemCommandResult.Fail("reorder_failed", "该容器不能调整顺序。");
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

        return P2ItemCommandResult.Ok($"已移动 {item.Base.DisplayName}。");
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

    private ItemInstance? Take(ItemContainerKind source, int index) => source switch
    {
        ItemContainerKind.Storage => session.World.Storage.TakeAt(index),
        ItemContainerKind.SortingBag => session.Management.TakeSortingBagAt(index),
        ItemContainerKind.Recovery => session.Management.TakeRecoveryAt(index),
        _ => null,
    };

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
}

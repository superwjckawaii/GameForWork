using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using System.Runtime.CompilerServices;
using GameForWork.Core.P6;
using GameForWork.Core.P4;
using GameForWork.Core.P9;
using GameForWork.Core.P14;
using GameForWork.Core.P29;
using GameForWork.Core.Equipment;

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

    public EquipmentCraftingResult CraftEquipment(
        ItemContainerKind source,
        int index,
        string operationName,
        string selectedDefinitionId = "",
        string selectedAffixFamilyId = "")
    {
        ItemInstance? item = PeekIncludingEquipped(source, index);
        if (item is null) return new(false, "item_missing", "物品不存在。", null, string.Empty, 0);
        EquipmentCraftingOperationEntry? operation = EquipmentCatalog.CraftingOperations
            .FirstOrDefault(value => value.DisplayName == operationName);
        if (operation is null) return new(false, "unknown_operation", $"未知做装操作：{operationName}。", null, string.Empty, 0);

        var request = new EquipmentCraftingRequest(operation.Id, selectedDefinitionId, selectedAffixFamilyId,
            session.Town.Level(P9BuildingKind.Workshop));
        EquipmentCraftingPreview preview = EquipmentCraftingService.Preview(item, request);
        if (!preview.Available) return new(false, preview.FailureReason, preview.Summary, null, preview.Resource, preview.Cost);
        var wallet = new EquipmentCraftingWallet();
        wallet.Credit(preview.Resource, ResourceBalance(preview.Resource));
        EquipmentCraftingResult result = EquipmentCraftingService.Execute(wallet, item, request);
        if (!result.Succeeded) return result;

        bool applied = result.Destroyed
            ? RemoveIncludingEquipped(source, index) is not null
            : result.Item is not null && Replace(source, index, result.Item);
        if (!applied) return result with { Succeeded = false, FailureReason = "replace_failed", Summary = "物品位置变化，制作与材料均未改变。", Item = null };
        if (!TrySpendResource(result.Resource, result.Cost)) throw new InvalidOperationException($"Crafting resource changed: {result.Resource}.");
        session.Endgame.CompleteGameplayOperation();
        if (source == ItemContainerKind.Equipped) session.NotifyEquipmentChanged(character);
        session.Management.AddHistory($"装备打造：{result.Summary}。 ");
        session.RecordJourneyEvent(P8JourneyEvent.CraftedItem);
        return result;
    }

    public P2WorkshopPreview Craft(ItemContainerKind source, int index, P2WorkshopRecipe recipe)
    {
        string name = recipe switch
        {
            P2WorkshopRecipe.WeaponPhysical => "淬刃打造",
            P2WorkshopRecipe.ReinforceDefense => "守壁打造",
            _ => "活血打造",
        };
        EquipmentCraftingResult result = CraftEquipment(source, index, name);
        MetalCurrencyKind? metal = MetalFor(result.Resource);
        return new(result.Succeeded, result.FailureReason, result.Item, 0, 0, result.Summary, metal, result.Cost);
    }

    public P6CraftPreview CraftP6(
        ItemContainerKind source,
        int index,
        P6CraftOperation operation,
        string fractureFamilyId = "")
    {
        ItemInstance? before = PeekIncludingEquipped(source, index);
        string name = operation switch
        {
            P6CraftOperation.RerollLinks => "连接重铸", P6CraftOperation.UpgradeLinks => "稳固增连",
            P6CraftOperation.ChaosReroll => "混沌重铸", P6CraftOperation.DivineReroll => "神铸重掷",
            _ => "破裂",
        };
        EquipmentCraftingResult result = CraftEquipment(source, index, name, selectedAffixFamilyId: fractureFamilyId);
        return new(result.Succeeded, result.FailureReason, result.Summary, result.Item,
            MetalFor(result.Resource) ?? MetalCurrencyKind.ChainSteel, result.Cost,
            before?.LinkedSocketCount ?? 0, result.Item?.LinkedSocketCount ?? before?.LinkedSocketCount ?? 0);
    }

    public P9CraftResult CraftP9(ItemContainerKind source, int index, P9CraftOperation operation)
    {
        EquipmentCraftingResult result = CraftEquipment(source, index, MetalOperationName(operation));
        return new(result.Succeeded, result.FailureReason, result.Summary, result.Item,
            MetalFor(result.Resource) ?? MetalCurrencyKind.AwakeningCopper, result.Cost, result.Destroyed);
    }

    public P9CraftResult EnchantP9(ItemContainerKind source, int index, string enchantmentId)
    {
        ItemEnchantment enchantment = P9EnchantmentCatalog.Get(enchantmentId);
        EquipmentCraftingResult result = CraftEquipment(source, index, $"附魔：{enchantment.DisplayName}", enchantment.StableId);
        return new(result.Succeeded, result.FailureReason, result.Summary, result.Item,
            MetalCurrencyKind.TemperingIron, result.Cost, result.Destroyed);
    }

    public P14GardenCraftResult CraftP14(ItemContainerKind source, int index, P14GardenCraft craft)
    {
        int cost = P14GardenCrafting.Cost(craft);
        EquipmentCraftingResult result = CraftEquipment(source, index, GardenOperationName(craft));
        return new(result.Succeeded, result.Summary, result.Item, result.Cost == 0 ? cost : result.Cost);
    }

    public P29ResourceCraftResult CraftP29Red(ItemContainerKind source, int index, string affixFamilyId)
    {
        EquipmentCraftingResult result = CraftEquipment(source, index, "赤誓升降", selectedAffixFamilyId: affixFamilyId);
        return new(result.Succeeded, result.Summary, result.Item, result.Cost == 0 ? P29ResourceCrafting.RedFavorCost : result.Cost);
    }

    public P29ResourceCraftResult CraftP29Blue(ItemContainerKind source, int index)
    {
        EquipmentCraftingResult result = CraftEquipment(source, index, "苍誓品质重置");
        return new(result.Succeeded, result.Summary, result.Item, result.Cost == 0 ? P29ResourceCrafting.BlueFavorCost : result.Cost);
    }

    private int ResourceBalance(string resource)
    {
        MetalCurrencyKind? metal = MetalFor(resource);
        if (metal is not null) return session.World.Economy.MetalAmount(metal.Value);
        return resource switch
        {
            "金币" => session.World.Economy.Gold,
            "命能" => session.Endgame.LifeForce,
            "赤誓收益" => session.Endgame.RedFavor,
            "苍誓收益" => session.Endgame.BlueFavor,
            "监守印记" => session.World.Economy.WardenMarks,
            _ => 0,
        };
    }

    private bool TrySpendResource(string resource, int cost)
    {
        MetalCurrencyKind? metal = MetalFor(resource);
        if (metal is not null) return session.World.Economy.TrySpendMetal(metal.Value, cost);
        return resource switch
        {
            "金币" => session.World.Economy.TrySpendGold(cost),
            "命能" => session.Endgame.TrySpendLifeForce(cost),
            "赤誓收益" => session.Endgame.TrySpendRedFavor(cost),
            "苍誓收益" => session.Endgame.TrySpendBlueFavor(cost),
            "监守印记" => session.World.Economy.TrySpendWardenMarks(cost),
            _ => cost == 0,
        };
    }

    private static MetalCurrencyKind? MetalFor(string resource) => P4MetalCurrencies.All
        .FirstOrDefault(value => value.DisplayName == resource)?.Kind;

    private static string MetalOperationName(P9CraftOperation operation) => operation switch
    {
        P9CraftOperation.AwakenMagic => "启灵", P9CraftOperation.AugmentMagic => "添铸",
        P9CraftOperation.RerollMagic => "易变重铸", P9CraftOperation.FatefulUpgrade => "命铸",
        P9CraftOperation.AlchemicalRare => "炼真", P9CraftOperation.RegalUpgrade => "王铸",
        P9CraftOperation.ChaosReroll => "混沌重铸", P9CraftOperation.ExaltedAdd => "崇高增附",
        P9CraftOperation.DissolveAffix => "消解", P9CraftOperation.Scour => "洗炼",
        P9CraftOperation.DivineReroll => "神铸重掷", P9CraftOperation.BlessedReroll => "祝铸重掷",
        P9CraftOperation.Fracture => "破裂", P9CraftOperation.PolishQuality => "精磨品质",
        _ => "赤蚀腐化",
    };

    private static string GardenOperationName(P14GardenCraft craft) => craft switch
    {
        P14GardenCraft.KeepPrefixes => "保留前缀重铸", P14GardenCraft.KeepSuffixes => "保留后缀重铸",
        P14GardenCraft.BiasLife => "生命偏向重铸", P14GardenCraft.BiasDefense => "防御偏向重铸",
        P14GardenCraft.BiasAttack => "攻击偏向重铸", P14GardenCraft.BiasSpell => "法术偏向重铸",
        P14GardenCraft.BiasSpeed => "速度偏向重铸", P14GardenCraft.BiasCritical => "暴击偏向重铸",
        P14GardenCraft.ReplaceLife => "生命偏向打造", P14GardenCraft.ReplaceDefense => "防御偏向打造",
        P14GardenCraft.ReplaceAttack => "攻击偏向打造", P14GardenCraft.ReplaceSpell => "法术偏向打造",
        P14GardenCraft.ReplaceSpeed => "速度偏向打造", _ => "暴击偏向打造",
    };

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

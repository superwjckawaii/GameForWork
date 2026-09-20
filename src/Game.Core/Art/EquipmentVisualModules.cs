using GameForWork.Core.Campaign.Items;

namespace GameForWork.Core.Art;

public sealed record EquipmentVisualProfile(
    bool HasChest,
    bool HasHelmet,
    bool HasGloves,
    bool HasBoots,
    bool HasMainHand,
    bool HasOffHand,
    bool HasShield,
    WeaponFamily MainHandFamily,
    ItemRarity HighestRarity,
    bool HasLegendaryEffect,
    bool HasEnchantment,
    IReadOnlyDictionary<EquipmentSlot, string>? ModuleIds = null)
{
    public static EquipmentVisualProfile Empty { get; } = new(
        false, false, false, false, false, false, false,
        WeaponFamily.None, ItemRarity.Basic, false, false, new Dictionary<EquipmentSlot, string>());

    public string ModuleId(EquipmentSlot slot) => ModuleIds?.GetValueOrDefault(slot) ?? string.Empty;
}

public static class EquipmentVisualModules
{
    public static EquipmentVisualProfile Resolve(IReadOnlyDictionary<EquipmentSlot, ItemInstance>? items)
    {
        if (items is null || items.Count == 0) return EquipmentVisualProfile.Empty;

        items.TryGetValue(EquipmentSlot.Chest, out ItemInstance? chest);
        items.TryGetValue(EquipmentSlot.Helmet, out ItemInstance? helmet);
        items.TryGetValue(EquipmentSlot.Gloves, out ItemInstance? gloves);
        items.TryGetValue(EquipmentSlot.Boots, out ItemInstance? boots);
        items.TryGetValue(EquipmentSlot.MainHand, out ItemInstance? mainHand);
        items.TryGetValue(EquipmentSlot.OffHand, out ItemInstance? offHand);
        ItemInstance?[] equipped = [chest, helmet, gloves, boots, mainHand, offHand];
        ItemRarity highestRarity = equipped.Where(item => item is not null)
            .Select(item => item!.Rarity).DefaultIfEmpty(ItemRarity.Basic).Max();

        Dictionary<EquipmentSlot, string> moduleIds = new();
        AddModuleId(moduleIds, EquipmentSlot.Chest, chest);
        AddModuleId(moduleIds, EquipmentSlot.Helmet, helmet);
        AddModuleId(moduleIds, EquipmentSlot.Gloves, gloves);
        AddModuleId(moduleIds, EquipmentSlot.Boots, boots);
        AddModuleId(moduleIds, EquipmentSlot.MainHand, mainHand);
        AddModuleId(moduleIds, EquipmentSlot.OffHand, offHand);

        return new EquipmentVisualProfile(
            chest is not null,
            helmet is not null,
            gloves is not null,
            boots is not null,
            mainHand is not null,
            offHand is not null,
            offHand?.Base.Category == ItemCategory.Shield,
            mainHand?.Base.WeaponFamily ?? WeaponFamily.None,
            highestRarity,
            equipped.Any(item => item?.Rarity == ItemRarity.Legendary || item?.LegendaryRule is not null || !string.IsNullOrEmpty(item?.LegendaryCatalogId)),
            equipped.Any(item => item?.AllEnchantments.Count > 0),
            moduleIds);
    }

    private static void AddModuleId(Dictionary<EquipmentSlot, string> ids, EquipmentSlot slot, ItemInstance? item)
    {
        if (item is null) return;
        ids[slot] = item.Base.StableId;
    }
}

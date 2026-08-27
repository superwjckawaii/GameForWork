using System.Numerics;
using GameForWork.Core.P1.Combat;

namespace GameForWork.Core.P1.Items;

public sealed record EquipmentModifiers(
    int AddedPhysicalDamage,
    int IncreasedPhysicalDamageBasisPoints,
    int FlatAccuracy,
    int IncreasedAttackSpeedBasisPoints,
    int IncreasedCriticalChanceBasisPoints,
    int IncreasedBleedChanceBasisPoints,
    int Physique,
    int Spirit,
    int FlatMaximumLife,
    int FlatMaximumMana,
    int IncreasedArmorBasisPoints,
    int IncreasedEvasionBasisPoints,
    int IncreasedShieldBasisPoints,
    int IncreasedLifeFlaskEffectBasisPoints,
    int IncreasedManaRegenerationBasisPoints);

public sealed record EquipmentSummary(
    DefensiveEquipment Defense,
    EquipmentModifiers Modifiers,
    int CoreSkillCapacity,
    int SupportLinkCapacity,
    WeaponProfile? Weapon,
    LegendaryRule? WeaponLegendaryRule);

public sealed class EquipmentLoadout
{
    private readonly Dictionary<EquipmentSlot, ItemInstance> _items = [];

    public IReadOnlyDictionary<EquipmentSlot, ItemInstance> Items => _items;

    public bool TryEquip(EquipmentSlot slot, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!CanEquip(slot, item.Base.Category))
        {
            return false;
        }

        if (item.Base.Category == ItemCategory.TwoHandWeapon)
        {
            _items.Remove(EquipmentSlot.OffHand);
        }

        _items[slot] = item;
        return true;
    }

    public ItemInstance? Unequip(EquipmentSlot slot)
    {
        if (!_items.Remove(slot, out ItemInstance? item))
        {
            return null;
        }

        return item;
    }

    public static EquipmentLoadout Restore(IEnumerable<KeyValuePair<EquipmentSlot, ItemInstance>> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var result = new EquipmentLoadout();
        foreach ((EquipmentSlot slot, ItemInstance item) in items)
        {
            if (!result.TryEquip(slot, item))
            {
                throw new InvalidDataException($"Item {item.InstanceId} cannot be restored to {slot}.");
            }
        }

        return result;
    }

    public EquipmentSummary CalculateSummary()
    {
        ItemInstance[] equipped = _items.Values.ToArray();
        int armor = equipped.Sum(item => item.Base.Armor);
        int evasion = equipped.Sum(item => item.Base.Evasion);
        int shield = equipped.Sum(item => item.Base.Shield);
        int[] sums = new int[Enum.GetValues<ItemModifierKind>().Length];
        foreach (ItemInstance item in equipped)
        {
            sums[(int)item.Base.ImplicitModifier] = checked(sums[(int)item.Base.ImplicitModifier] + item.ImplicitValue);
            foreach (AffixRoll affix in item.Affixes)
            {
                sums[(int)affix.Definition.ModifierKind] = checked(
                    sums[(int)affix.Definition.ModifierKind] + affix.Value);
            }
        }

        var modifiers = new EquipmentModifiers(
            sums[(int)ItemModifierKind.AddedPhysicalDamage],
            sums[(int)ItemModifierKind.IncreasedPhysicalDamageBasisPoints],
            sums[(int)ItemModifierKind.FlatAccuracy],
            sums[(int)ItemModifierKind.IncreasedAttackSpeedBasisPoints],
            sums[(int)ItemModifierKind.IncreasedCriticalChanceBasisPoints],
            sums[(int)ItemModifierKind.IncreasedBleedChanceBasisPoints],
            sums[(int)ItemModifierKind.Physique],
            sums[(int)ItemModifierKind.Spirit],
            sums[(int)ItemModifierKind.FlatMaximumLife],
            sums[(int)ItemModifierKind.FlatMaximumMana],
            sums[(int)ItemModifierKind.IncreasedArmorBasisPoints],
            sums[(int)ItemModifierKind.IncreasedEvasionBasisPoints],
            sums[(int)ItemModifierKind.IncreasedShieldBasisPoints],
            sums[(int)ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints],
            sums[(int)ItemModifierKind.IncreasedManaRegenerationBasisPoints]);
        ItemInstance? weaponItem = _items.GetValueOrDefault(EquipmentSlot.MainHand);
        return new EquipmentSummary(
            new DefensiveEquipment(armor, evasion, shield),
            modifiers,
            equipped.Sum(item => item.Base.CoreSkillCapacity),
            equipped.Sum(item => item.Base.SupportLinkCapacity + item.ExtraSupportLinkCapacity),
            weaponItem?.Base.Category == ItemCategory.TwoHandWeapon ? weaponItem.Base.ToWeaponProfile() : null,
            weaponItem?.LegendaryRule);
    }

    private static bool CanEquip(EquipmentSlot slot, ItemCategory category) => category switch
    {
        ItemCategory.TwoHandWeapon => slot == EquipmentSlot.MainHand,
        ItemCategory.BodyArmor => slot == EquipmentSlot.Chest,
        ItemCategory.Helmet => slot == EquipmentSlot.Helmet,
        ItemCategory.Ring => slot is EquipmentSlot.RingLeft or EquipmentSlot.RingRight,
        ItemCategory.LifeFlask => slot is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5,
        _ => false,
    };
}

public sealed record SkillCapacityResult(
    bool IsValid,
    int RequiredCoreSkills,
    int AvailableCoreSkills,
    int RequiredSupportLinks,
    int AvailableSupportLinks,
    string FailureReason);

public static class SkillCapacityRules
{
    public static SkillCapacityResult Validate(
        IReadOnlyList<SkillConfiguration> skills,
        EquipmentSummary equipment)
    {
        int requiredCore = skills.Count;
        int requiredLinks = skills.Sum(skill => BitOperations.PopCount((uint)skill.Supports));
        bool enoughCore = requiredCore <= equipment.CoreSkillCapacity;
        bool enoughLinks = requiredLinks <= equipment.SupportLinkCapacity;
        string reason = !enoughCore ? "core_capacity" : !enoughLinks ? "support_capacity" : string.Empty;
        return new SkillCapacityResult(
            enoughCore && enoughLinks,
            requiredCore,
            equipment.CoreSkillCapacity,
            requiredLinks,
            equipment.SupportLinkCapacity,
            reason);
    }
}

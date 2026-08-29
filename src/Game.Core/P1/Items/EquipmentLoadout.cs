using System.Numerics;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P6;

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
    int IncreasedManaRegenerationBasisPoints,
    int Dexterity = 0,
    int Energy = 0,
    int FireResistanceBasisPoints = 0,
    int ColdResistanceBasisPoints = 0,
    int LightningResistanceBasisPoints = 0,
    int VoidResistanceBasisPoints = 0,
    int IncreasedMovementSpeedBasisPoints = 0,
    int BlockChanceBasisPoints = 0,
    int SpellSuppressionBasisPoints = 0,
    int FlatLifeRegeneration = 0);

public sealed record EquipmentSummary(
    DefensiveEquipment Defense,
    EquipmentModifiers Modifiers,
    int CoreSkillCapacity,
    int SupportLinkCapacity,
    WeaponProfile? Weapon,
    LegendaryRule? WeaponLegendaryRule,
    bool HasShield = false,
    int BaseBlockChanceBasisPoints = 0);

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

        if (slot == EquipmentSlot.OffHand &&
            _items.GetValueOrDefault(EquipmentSlot.MainHand) is { } mainHand &&
            mainHand.Base.Category == ItemCategory.TwoHandWeapon &&
            !(mainHand.Base.ItemTags.Contains("bow", StringComparer.Ordinal) && item.Base.ItemTags.Contains("quiver", StringComparer.Ordinal)))
        {
            return false;
        }

        if (item.Base.Category == ItemCategory.TwoHandWeapon)
        {
            ItemInstance? offHand = _items.GetValueOrDefault(EquipmentSlot.OffHand);
            if (!item.Base.ItemTags.Contains("bow", StringComparer.Ordinal) ||
                offHand?.Base.ItemTags.Contains("quiver", StringComparer.Ordinal) != true)
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
            if (!result.TryEquip(slot, P6SocketRules.Ensure(item)))
            {
                throw new InvalidDataException($"Item {item.InstanceId} cannot be restored to {slot}.");
            }
        }

        return result;
    }

    public EquipmentSummary CalculateSummary()
    {
        ItemInstance[] equipped = _items.Values.ToArray();
        int armor = equipped.Sum(item => QualityScale(item.Base.Armor, item.Quality));
        int evasion = equipped.Sum(item => QualityScale(item.Base.Evasion, item.Quality));
        int shield = equipped.Sum(item => QualityScale(item.Base.Shield, item.Quality));
        int[] sums = new int[Enum.GetValues<ItemModifierKind>().Length];
        foreach (ItemInstance item in equipped)
        {
            sums[(int)item.Base.ImplicitModifier] = checked(sums[(int)item.Base.ImplicitModifier] + item.ImplicitValue);
            foreach (AffixRoll affix in item.Affixes)
            {
                sums[(int)affix.Definition.ModifierKind] = checked(
                    sums[(int)affix.Definition.ModifierKind] + affix.EffectiveValue);
            }
            if (item.Enchantment is not null)
            {
                sums[(int)item.Enchantment.ModifierKind] = checked(
                    sums[(int)item.Enchantment.ModifierKind] + item.Enchantment.Value);
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
            sums[(int)ItemModifierKind.IncreasedManaRegenerationBasisPoints],
            sums[(int)ItemModifierKind.Dexterity],
            sums[(int)ItemModifierKind.Energy],
            sums[(int)ItemModifierKind.FireResistanceBasisPoints],
            sums[(int)ItemModifierKind.ColdResistanceBasisPoints],
            sums[(int)ItemModifierKind.LightningResistanceBasisPoints],
            sums[(int)ItemModifierKind.VoidResistanceBasisPoints],
            sums[(int)ItemModifierKind.IncreasedMovementSpeedBasisPoints],
            sums[(int)ItemModifierKind.BlockChanceBasisPoints],
            sums[(int)ItemModifierKind.SpellSuppressionBasisPoints],
            sums[(int)ItemModifierKind.FlatLifeRegeneration]);
        ItemInstance? weaponItem = _items.GetValueOrDefault(EquipmentSlot.MainHand);
        return new EquipmentSummary(
            new DefensiveEquipment(armor, evasion, shield),
            modifiers,
            equipped.Sum(item => item.Base.CoreSkillCapacity),
            equipped.Sum(item => item.Base.SupportLinkCapacity + item.ExtraSupportLinkCapacity),
            weaponItem?.Base.Category is ItemCategory.TwoHandWeapon or ItemCategory.OneHandWeapon ? QualityWeapon(weaponItem) : null,
            weaponItem?.LegendaryRule,
            _items.GetValueOrDefault(EquipmentSlot.OffHand)?.Base.ItemTags.Contains("shield", StringComparer.Ordinal) == true,
            equipped.Sum(item => item.Base.BlockChanceBasisPoints));
    }

    private static int QualityScale(int value, int quality) => checked(value * (100 + Math.Clamp(quality, 0, 20)) / 100);

    private static WeaponProfile QualityWeapon(ItemInstance item)
    {
        WeaponProfile weapon = item.Base.ToWeaponProfile();
        return weapon with
        {
            MinimumPhysicalDamage = QualityScale(weapon.MinimumPhysicalDamage, item.Quality),
            MaximumPhysicalDamage = QualityScale(weapon.MaximumPhysicalDamage, item.Quality),
        };
    }

    public static bool CanEquip(EquipmentSlot slot, ItemCategory category) => category switch
    {
        ItemCategory.TwoHandWeapon => slot == EquipmentSlot.MainHand,
        ItemCategory.OneHandWeapon => slot == EquipmentSlot.MainHand,
        ItemCategory.Shield => slot == EquipmentSlot.OffHand,
        ItemCategory.BodyArmor => slot == EquipmentSlot.Chest,
        ItemCategory.Helmet => slot == EquipmentSlot.Helmet,
        ItemCategory.Gloves => slot == EquipmentSlot.Gloves,
        ItemCategory.Boots => slot == EquipmentSlot.Boots,
        ItemCategory.Belt => slot == EquipmentSlot.Belt,
        ItemCategory.Amulet => slot == EquipmentSlot.Amulet,
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
        int requiredLinks = skills.Sum(skill => BitOperations.PopCount((ulong)skill.Supports) + skill.ExtendedSupports.Count);
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

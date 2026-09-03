using System.Numerics;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P6;
using GameForWork.Core.P30;
using GameForWork.Core.Equipment;

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
    int FlatLifeRegeneration = 0,
    int IncreasedCooldownRecoveryBasisPoints = 0,
    int IncreasedFlaskChargeGainBasisPoints = 0,
    int IncreasedFlaskDurationBasisPoints = 0,
    int IncreasedMaximumLifeBasisPoints = 0,
    int IncreasedMaximumManaBasisPoints = 0,
    int IncreasedMaximumShieldBasisPoints = 0,
    int MaximumAllResistanceBasisPoints = 0,
    int MoreRareBossDamageBasisPoints = 0,
    int ActiveSkillGemLevels = 0,
    int SupportSkillGemLevels = 0,
    IReadOnlyDictionary<ItemModifierKind, int>? Extended = null)
{
    public int Value(ItemModifierKind kind) => Extended?.GetValueOrDefault(kind) ?? 0;
}

public sealed record EquipmentSummary(
    DefensiveEquipment Defense,
    EquipmentModifiers Modifiers,
    int CoreSkillCapacity,
    int SupportLinkCapacity,
    WeaponProfile? Weapon,
    LegendaryRule? WeaponLegendaryRule,
    bool HasShield = false,
    int BaseBlockChanceBasisPoints = 0,
    int SpiritBarrier = 0,
    LocalWeaponStats? LocalWeapon = null,
    EquipmentEffectSnapshot? Effects = null);

public readonly record struct LocalDamageRange(int Minimum, int Maximum)
{
    public double Average => (Minimum + Maximum) / 2.0;
    public bool HasDamage => Maximum > 0;
}

public sealed record LocalWeaponStats(
    WeaponProfile Physical,
    LocalDamageRange Fire,
    LocalDamageRange Cold,
    LocalDamageRange Lightning,
    LocalDamageRange Void)
{
    public double PhysicalDamagePerSecond =>
        (Physical.MinimumPhysicalDamage + Physical.MaximumPhysicalDamage) / 2.0 * Physical.AttacksPerSecondMilli / 1_000.0;
    public double ElementalDamagePerSecond =>
        (Fire.Average + Cold.Average + Lightning.Average) * Physical.AttacksPerSecondMilli / 1_000.0;
    public double VoidDamagePerSecond => Void.Average * Physical.AttacksPerSecondMilli / 1_000.0;
    public double TotalDamagePerSecond => PhysicalDamagePerSecond + ElementalDamagePerSecond + VoidDamagePerSecond;
}

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

        bool incomingQuiver = item.Base.ItemTags.Contains("quiver", StringComparer.Ordinal);
        if (slot == EquipmentSlot.OffHand && incomingQuiver &&
            _items.GetValueOrDefault(EquipmentSlot.MainHand)?.Base.ItemTags.Contains("bow", StringComparer.Ordinal) != true)
        {
            return false;
        }
        if (slot == EquipmentSlot.OffHand && item.Base.Category == ItemCategory.OneHandWeapon &&
            _items.GetValueOrDefault(EquipmentSlot.MainHand)?.Base.Category != ItemCategory.OneHandWeapon)
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

        if (slot == EquipmentSlot.MainHand)
        {
            ItemInstance? offHand = _items.GetValueOrDefault(EquipmentSlot.OffHand);
            bool bow = item.Base.ItemTags.Contains("bow", StringComparer.Ordinal);
            bool quiver = offHand?.Base.ItemTags.Contains("quiver", StringComparer.Ordinal) == true;
            bool offHandWeapon = offHand?.Base.Category == ItemCategory.OneHandWeapon;
            if (item.Base.Category == ItemCategory.TwoHandWeapon && (!bow || !quiver) || quiver && !bow ||
                offHandWeapon && item.Base.Category != ItemCategory.OneHandWeapon)
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
        foreach ((EquipmentSlot slot, ItemInstance item) in items.OrderBy(pair => pair.Key == EquipmentSlot.MainHand ? 0 : 1))
        {
            if (!result.TryEquip(slot, P6SocketRules.Ensure(P30EquipmentAffixes.RemoveForbiddenGlobalWeaponAffixes(item))))
            {
                throw new InvalidDataException($"Item {item.InstanceId} cannot be restored to {slot}.");
            }
        }

        return result;
    }

    public EquipmentSummary CalculateSummary()
    {
        ItemInstance[] equipped = _items.Values.ToArray();
        int armor = equipped.Sum(item => LocalDefense(item, item.EffectiveBaseArmor, ItemModifierKind.FlatArmor, ItemModifierKind.IncreasedArmorBasisPoints));
        int evasion = equipped.Sum(item => LocalDefense(item, item.EffectiveBaseEvasion, ItemModifierKind.FlatEvasion, ItemModifierKind.IncreasedEvasionBasisPoints));
        int shield = equipped.Sum(item => LocalDefense(item, item.EffectiveBaseShield, ItemModifierKind.FlatShield, ItemModifierKind.IncreasedShieldBasisPoints));
        int spiritBarrier = equipped.Sum(item => LocalDefense(item, item.EffectiveBaseSpiritBarrier, ItemModifierKind.FlatSpiritBarrier, ItemModifierKind.IncreasedSpiritBarrierBasisPoints));
        int[] sums = new int[Enum.GetValues<ItemModifierKind>().Length];
        foreach (ItemInstance item in equipped)
        {
            foreach (RolledAffixComponent implicitModifier in item.EffectiveImplicitComponents)
                AddGlobal(sums, implicitModifier.Kind, implicitModifier.Value, implicitModifier.Scope);
            foreach (AffixRoll affix in item.Affixes)
            foreach (RolledAffixComponent effect in affix.Effects)
                AddGlobal(sums, effect.Kind, effect.Value, effect.Scope);
            if (item.Enchantment is not null)
            {
            foreach (AffixModifierComponent effect in item.Enchantment.EffectComponents)
                AddGlobal(sums, effect.Kind, effect.MinimumValue, effect.Scope);
                if (item.Enchantment.DisplayName == "不灭王印")
                    sums[(int)ItemModifierKind.FlatMaximumLife] = checked(sums[(int)ItemModifierKind.FlatMaximumLife] +
                        EquipmentRuleEngine.ImmortalMaximumLife(LocalDefense(item, item.EffectiveBaseArmor,
                            ItemModifierKind.FlatArmor, ItemModifierKind.IncreasedArmorBasisPoints)));
            }
        }

        IReadOnlyDictionary<ItemModifierKind, int> extended = Enum.GetValues<ItemModifierKind>().ToDictionary(kind => kind, kind => sums[(int)kind]);
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
            checked(sums[(int)ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints] + sums[(int)ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints]),
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
            sums[(int)ItemModifierKind.FlatLifeRegeneration],
            sums[(int)ItemModifierKind.IncreasedCooldownRecoveryBasisPoints],
            sums[(int)ItemModifierKind.IncreasedFlaskChargeGainBasisPoints],
            sums[(int)ItemModifierKind.IncreasedFlaskDurationBasisPoints],
            sums[(int)ItemModifierKind.IncreasedMaximumLifeBasisPoints],
            sums[(int)ItemModifierKind.IncreasedMaximumManaBasisPoints],
            sums[(int)ItemModifierKind.IncreasedMaximumShieldBasisPoints],
            sums[(int)ItemModifierKind.MaximumAllResistanceBasisPoints],
            sums[(int)ItemModifierKind.MoreRareBossDamageBasisPoints],
            sums[(int)ItemModifierKind.ActiveSkillGemLevels],
            sums[(int)ItemModifierKind.SupportSkillGemLevels],
            extended);
        ItemInstance? weaponItem = _items.GetValueOrDefault(EquipmentSlot.MainHand);
        LocalWeaponStats? localWeapon = weaponItem?.Base.Category is ItemCategory.TwoHandWeapon or ItemCategory.OneHandWeapon
            ? CalculateLocalWeapon(weaponItem)
            : null;
        return new EquipmentSummary(
            new DefensiveEquipment(armor, evasion, shield),
            modifiers,
            equipped.Sum(item => item.Base.CoreSkillCapacity) + sums[(int)ItemModifierKind.AdditionalCoreSkillCapacity],
            equipped.Sum(item => item.Base.SupportLinkCapacity) + sums[(int)ItemModifierKind.ExtraSupportLinkCapacity],
            localWeapon?.Physical,
            weaponItem?.LegendaryRule,
            _items.GetValueOrDefault(EquipmentSlot.OffHand)?.Base.ItemTags.Contains("shield", StringComparer.Ordinal) == true,
            equipped.Sum(LocalBlock),
            spiritBarrier,
            localWeapon,
            EquipmentEffectCompiler.Compile(extended));
    }

    public static LocalWeaponStats CalculateLocalWeapon(ItemInstance item)
    {
        WeaponProfile weapon = item.Base.ToWeaponProfile();
        int addedMinimum = LocalValue(item, ItemModifierKind.AddedMinimumPhysicalDamage) + LocalValue(item, ItemModifierKind.AddedPhysicalDamage);
        int addedMaximum = LocalValue(item, ItemModifierKind.AddedMaximumPhysicalDamage) + LocalValue(item, ItemModifierKind.AddedPhysicalDamage);
        int physicalIncrease = LocalValue(item, ItemModifierKind.IncreasedPhysicalDamageBasisPoints);
        int attackSpeedIncrease = LocalValue(item, ItemModifierKind.IncreasedAttackSpeedBasisPoints);
        int criticalIncrease = LocalValue(item, ItemModifierKind.IncreasedCriticalChanceBasisPoints);
        weapon = weapon with
        {
            MinimumPhysicalDamage = LocalWeaponDamage(weapon.MinimumPhysicalDamage, addedMinimum, physicalIncrease, item.Quality),
            MaximumPhysicalDamage = LocalWeaponDamage(weapon.MaximumPhysicalDamage, addedMaximum, physicalIncrease, item.Quality),
            AttacksPerSecondMilli = checked(weapon.AttacksPerSecondMilli * (10_000 + attackSpeedIncrease) / 10_000),
            CriticalChanceBasisPoints = checked(weapon.CriticalChanceBasisPoints * (10_000 + criticalIncrease) / 10_000),
        };
        LocalDamageRange fire = LocalElementalRange(item, ItemModifierKind.AddedMinimumFireDamage, ItemModifierKind.AddedMaximumFireDamage);
        LocalDamageRange cold = LocalElementalRange(item, ItemModifierKind.AddedMinimumColdDamage, ItemModifierKind.AddedMaximumColdDamage);
        LocalDamageRange lightning = LocalElementalRange(item, ItemModifierKind.AddedMinimumLightningDamage, ItemModifierKind.AddedMaximumLightningDamage);
        if (item.Enchantment?.DisplayName == "奥术王印")
        {
            var copied = EquipmentRuleEngine.CopyHighestElementalRange((fire.Minimum, fire.Maximum), (cold.Minimum, cold.Maximum), (lightning.Minimum, lightning.Maximum));
            fire = new LocalDamageRange(copied.fireMinimum, copied.fireMaximum);
            cold = new LocalDamageRange(copied.coldMinimum, copied.coldMaximum);
            lightning = new LocalDamageRange(copied.lightningMinimum, copied.lightningMaximum);
        }
        return new LocalWeaponStats(
            weapon,
            fire,
            cold,
            lightning,
            LocalElementalRange(item, ItemModifierKind.AddedMinimumVoidDamage, ItemModifierKind.AddedMaximumVoidDamage));
    }

    public static WeaponProfile CalculateWeapon(ItemInstance item) => CalculateLocalWeapon(item).Physical;

    public static (int Armor, int Evasion, int Shield, int SpiritBarrier, int BlockChanceBasisPoints) CalculateLocalDefense(ItemInstance item) =>
        (LocalDefense(item, item.EffectiveBaseArmor, ItemModifierKind.FlatArmor, ItemModifierKind.IncreasedArmorBasisPoints),
         LocalDefense(item, item.EffectiveBaseEvasion, ItemModifierKind.FlatEvasion, ItemModifierKind.IncreasedEvasionBasisPoints),
         LocalDefense(item, item.EffectiveBaseShield, ItemModifierKind.FlatShield, ItemModifierKind.IncreasedShieldBasisPoints),
         LocalDefense(item, item.EffectiveBaseSpiritBarrier, ItemModifierKind.FlatSpiritBarrier, ItemModifierKind.IncreasedSpiritBarrierBasisPoints),
         LocalBlock(item));

    private static int LocalWeaponDamage(int baseValue, int flat, int increasedBasisPoints, int quality) => checked(
        (baseValue + flat) * (10_000 + increasedBasisPoints) / 10_000 * (100 + Math.Clamp(quality, 0, 40)) / 100);

    private static LocalDamageRange LocalElementalRange(ItemInstance item, ItemModifierKind minimumKind,
        ItemModifierKind maximumKind) => new(
        LocalWeaponDamage(0, LocalValue(item, minimumKind), 0, item.Quality),
        LocalWeaponDamage(0, LocalValue(item, maximumKind), 0, item.Quality));

    private static int LocalDefense(ItemInstance item, int baseValue, ItemModifierKind flatKind, ItemModifierKind increasedKind)
    {
        int flat = LocalValue(item, flatKind);
        int increased = LocalValue(item, increasedKind);
        int result = checked((baseValue + flat) * (10_000 + increased) / 10_000 * (100 + Math.Clamp(item.Quality, 0, 40)) / 100);
        if (flatKind == ItemModifierKind.FlatShield && item.Enchantment?.DisplayName == "晶心铭文")
            result = checked(result * 13_500 / 10_000);
        return result;
    }

    private static int LocalBlock(ItemInstance item)
    {
        int increased = LocalValue(item, ItemModifierKind.IncreasedLocalBlockBasisPoints);
        return checked(item.Base.BlockChanceBasisPoints * (10_000 + increased) / 10_000);
    }

    private static int LocalValue(ItemInstance item, ItemModifierKind kind)
    {
        int value = item.EffectiveImplicitComponents.Where(effect => effect.Kind == kind && effect.Scope is ItemModifierScope.LocalWeapon or ItemModifierScope.LocalDefense or ItemModifierScope.LocalBlock)
            .Sum(effect => effect.Value);
        value += item.Affixes.SelectMany(affix => affix.Effects)
            .Where(effect => effect.Kind == kind && effect.Scope is ItemModifierScope.LocalWeapon or ItemModifierScope.LocalDefense or ItemModifierScope.LocalBlock)
            .Sum(effect => effect.Value);
        if (item.Enchantment is not null)
            value += item.Enchantment.EffectComponents
                .Where(effect => effect.Kind == kind && effect.Scope is ItemModifierScope.LocalWeapon or ItemModifierScope.LocalDefense or ItemModifierScope.LocalBlock)
                .Sum(effect => effect.MinimumValue);
        return value;
    }

    private static void AddGlobal(int[] sums, ItemModifierKind kind, int value, ItemModifierScope scope)
    {
        if (kind == ItemModifierKind.None || scope is ItemModifierScope.LocalWeapon or ItemModifierScope.LocalDefense or ItemModifierScope.LocalBlock)
            return;
        sums[(int)kind] = checked(sums[(int)kind] + value);
    }

    public static bool CanEquip(EquipmentSlot slot, ItemCategory category) => category switch
    {
        ItemCategory.TwoHandWeapon => slot == EquipmentSlot.MainHand,
        ItemCategory.OneHandWeapon => slot is EquipmentSlot.MainHand or EquipmentSlot.OffHand,
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

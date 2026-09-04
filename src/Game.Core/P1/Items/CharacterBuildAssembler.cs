using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P30;

namespace GameForWork.Core.P1.Items;

public sealed record AssembledCharacterBuild(
    CharacterSheet Sheet,
    EquipmentSummary Equipment,
    PassiveBuildModifiers Passives,
    SkillUseProfile HeavyStrike,
    int FlatAccuracy,
    int IncreasedAttackDamageBasisPoints,
    int AddedPhysicalDamage,
    int IncreasedCriticalChanceBasisPoints,
    int IncreasedCriticalMultiplierBasisPoints,
    int MoreAttackDamageBasisPoints,
    int MoreSpellDamageBasisPoints,
    int MoreDamageOverTimeBasisPoints,
    int IncreasedActionSpeedBasisPoints,
    int InstantLifeLeechBasisPoints,
    int IncreasedBleedChanceBasisPoints,
    WarCryState WarCry,
    ChargedHeavyStrikeState? ChargedHeavyStrike,
    IReadOnlyList<P1FlaskKind> Flasks,
    WeaponProfile EffectiveWeapon,
    P30VirtueViceLoadout? VirtueViceLoadout = null,
    int IncreasedSpellDamageBasisPoints = 0,
    int IncreasedAttackSpeedBasisPoints = 0)
{
    public bool HasUsableWeapon => Equipment.Weapon is not null;

    public HeavyStrikeRequest CreateHeavyStrikeRequest(
        ResourceState resources,
        int targetEvasion,
        int targetArmor) => new(
        resources,
        HeavyStrike,
        EffectiveWeapon,
        Sheet.Accuracy(FlatAccuracy).Value,
        targetEvasion,
        targetArmor,
        IncreasedAttackDamageBasisPoints,
        AddedPhysicalDamage,
        AddedPhysicalDamage,
        IncreasedCriticalChanceBasisPoints,
        IncreasedBleedChanceBasisPoints,
        WarCry,
        ChargedHeavyStrike);
}

public static class CharacterBuildAssembler
{
    public static AssembledCharacterBuild Assemble(
        int level,
        CharacterAttributes baseAttributes,
        EquipmentLoadout loadout,
        PassiveTreeAllocation passiveTree,
        SkillConfiguration heavyStrikeConfiguration,
        P30JewelState? jewelState = null)
    {
        ArgumentNullException.ThrowIfNull(baseAttributes);
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(passiveTree);
        ArgumentNullException.ThrowIfNull(heavyStrikeConfiguration);

        EquipmentSummary equipment = loadout.CalculateSummary();
        WeaponProfile weapon = equipment.Weapon ?? P1Weapons.Unequipped;
        EquipmentModifiers item = equipment.Modifiers;
        PassiveBuildModifiers passive = passiveTree.CalculateModifiers();
        P205PassiveModifiers advanced = passive.Advanced ?? P205PassiveModifiers.Empty;
        P30JewelModifiers jewel = jewelState is null ? new() : P30Jewels.CalculateModifiers(jewelState, passiveTree);
        int specializedWeaponDamage = WeaponPassiveIncrease(loadout, passive, advanced);
        int physique = ScaleAttribute(baseAttributes.Physique + item.Physique + advanced.Physique + jewel.Physique,
            item.Value(ItemModifierKind.IncreasedPhysiqueBasisPoints) + item.Value(ItemModifierKind.IncreasedAllAttributesBasisPoints) +
            jewel.IncreasedPhysiqueBasisPoints);
        int dexterity = ScaleAttribute(baseAttributes.Dexterity + item.Dexterity + advanced.Dexterity + jewel.Dexterity,
            item.Value(ItemModifierKind.IncreasedDexterityBasisPoints) + item.Value(ItemModifierKind.IncreasedAllAttributesBasisPoints));
        int spirit = ScaleAttribute(baseAttributes.Spirit + item.Spirit + advanced.Spirit + jewel.Spirit,
            item.Value(ItemModifierKind.IncreasedSpiritBasisPoints) + item.Value(ItemModifierKind.IncreasedAllAttributesBasisPoints));
        int energy = ScaleAttribute(baseAttributes.Energy + item.Energy + advanced.Energy + jewel.Energy,
            item.Value(ItemModifierKind.IncreasedEnergyBasisPoints) + item.Value(ItemModifierKind.IncreasedAllAttributesBasisPoints));
        if (P30MasteryRuntime.Has(advanced, "属性", 3))
        {
            int maximum = Math.Max(Math.Max(physique, dexterity), Math.Max(spirit, energy));
            if (physique == maximum) physique = ScaleAttribute(physique, 2_000);
            else if (dexterity == maximum) dexterity = ScaleAttribute(dexterity, 2_000);
            else if (spirit == maximum) spirit = ScaleAttribute(spirit, 2_000);
            else energy = ScaleAttribute(energy, 2_000);
        }
        var attributes = new CharacterAttributes(physique, dexterity, spirit, energy);
        P30JewelModifiers attributeMemory = jewelState is null
            ? new()
            : P30Jewels.CalculateAttributeMemoryModifiers(jewelState, attributes);
        DefensiveEquipment defense = equipment.Defense;
        int masteryDefense = P30MasteryRuntime.DefenseMultiplier(advanced, weapon);
        defense = defense with
        {
            Armor = checked(defense.Armor * masteryDefense / 10_000),
            Evasion = checked(defense.Evasion * masteryDefense / 10_000),
        };
        defense = defense with { Shield = checked(defense.Shield * P30MasteryRuntime.ShieldMultiplier(advanced) / 10_000) };
        int evasionIncrease = checked(item.IncreasedEvasionBasisPoints + advanced.IncreasedEvasionBasisPoints);
        if (advanced.IronReflexes)
        {
            int converted = checked((defense.Evasion + attributes.Dexterity) * (10_000 + evasionIncrease) / 10_000);
            defense = new DefensiveEquipment(checked(defense.Armor + converted), 0, defense.Shield);
            evasionIncrease = -10_000;
        }
        var sheet = new CharacterSheet(
            level,
            attributes,
            defense,
            checked(item.FlatMaximumLife + passive.FlatMaximumLife),
            checked(passive.IncreasedMaximumLifeBasisPoints + item.IncreasedMaximumLifeBasisPoints +
                jewel.IncreasedMaximumLifeBasisPoints + attributeMemory.IncreasedMaximumLifeBasisPoints),
            checked(item.FlatMaximumMana + passive.FlatMaximumMana),
            checked(item.IncreasedArmorBasisPoints + passive.IncreasedArmorBasisPoints +
                jewel.IncreasedArmorBasisPoints + attributeMemory.IncreasedArmorBasisPoints),
            checked(evasionIncrease + jewel.IncreasedEvasionBasisPoints + attributeMemory.IncreasedEvasionBasisPoints),
            checked(item.IncreasedShieldBasisPoints + item.IncreasedMaximumShieldBasisPoints +
                advanced.IncreasedShieldBasisPoints + jewel.IncreasedMaximumShieldBasisPoints +
                attributeMemory.IncreasedMaximumShieldBasisPoints),
            checked(item.IncreasedManaRegenerationBasisPoints + passive.IncreasedManaRegenerationBasisPoints),
            checked(item.FireResistanceBasisPoints + advanced.FireResistanceBasisPoints),
            checked(item.ColdResistanceBasisPoints + advanced.ColdResistanceBasisPoints),
            checked(item.LightningResistanceBasisPoints + advanced.LightningResistanceBasisPoints),
            checked(item.VoidResistanceBasisPoints + advanced.VoidResistanceBasisPoints),
            checked(item.BlockChanceBasisPoints + advanced.BlockChanceBasisPoints),
            checked(item.SpellSuppressionBasisPoints + advanced.SpellSuppressionBasisPoints),
            checked(item.FlatLifeRegeneration + advanced.FlatLifeRegeneration),
            item.IncreasedMovementSpeedBasisPoints,
            checked(item.IncreasedMaximumManaBasisPoints + jewel.IncreasedMaximumManaBasisPoints +
                attributeMemory.IncreasedMaximumManaBasisPoints),
            item.Value(ItemModifierKind.FlatShield),
            equipment.SpiritBarrier,
            item.Value(ItemModifierKind.FlatSpiritBarrier),
            checked(item.Value(ItemModifierKind.IncreasedSpiritBarrierBasisPoints) + jewel.IncreasedSpiritBarrierBasisPoints),
            MaximumElementalResistanceBasisPoints: checked(7_500 + item.MaximumAllResistanceBasisPoints +
                Math.Max(item.Value(ItemModifierKind.MaximumFireResistanceBasisPoints),
                    Math.Max(item.Value(ItemModifierKind.MaximumColdResistanceBasisPoints), item.Value(ItemModifierKind.MaximumLightningResistanceBasisPoints))) +
                jewel.MaximumElementalResistanceBasisPoints),
            MaximumVoidResistanceBasisPoints: checked(7_500 + item.MaximumAllResistanceBasisPoints +
                item.Value(ItemModifierKind.MaximumVoidResistanceBasisPoints) + jewel.MaximumVoidResistanceBasisPoints),
            MaximumBlockChanceBasisPoints: checked(7_500 + item.Value(ItemModifierKind.MaximumAttackBlockChanceBasisPoints)),
            SpellBlockChanceBasisPoints: item.Value(ItemModifierKind.SpellBlockChanceBasisPoints),
            MaximumSpellBlockChanceBasisPoints: checked(7_500 + item.Value(ItemModifierKind.MaximumSpellBlockChanceBasisPoints)),
            IncreasedRecoveryRateBasisPoints: attributeMemory.IncreasedRecoveryRateBasisPoints);
        int increasedAttackSpeed = checked(item.IncreasedAttackSpeedBasisPoints + passive.IncreasedAttackSpeedBasisPoints +
            jewel.IncreasedAttackSpeedBasisPoints + attributeMemory.IncreasedAttackSpeedBasisPoints);
        SkillUseProfile heavyStrike = SkillRules.BuildHeavyStrike(
            heavyStrikeConfiguration,
            weapon,
            sheet.MaximumLife().Value,
            increasedAttackSpeed);
        heavyStrike = P1LegendaryRules.ApplyToHeavyStrike(heavyStrike, equipment.WeaponLegendaryRule);
        var warCry = new WarCryState { EchoNotableAllocated = passive.Echo };
        return new AssembledCharacterBuild(
            sheet,
            equipment,
            passive,
            heavyStrike,
            checked(item.FlatAccuracy + passive.FlatAccuracy + jewel.FlatAccuracy),
            checked(
                item.IncreasedPhysicalDamageBasisPoints +
                item.Value(ItemModifierKind.IncreasedAttackDamageBasisPoints) +
                passive.IncreasedAttackDamageBasisPoints +
                specializedWeaponDamage + jewel.IncreasedAttackDamageBasisPoints +
                attributeMemory.IncreasedAttackDamageBasisPoints +
                sheet.AttackDamageIncreaseFromPhysique().Value),
            checked(item.AddedPhysicalDamage +
                (item.Value(ItemModifierKind.AddedMinimumPhysicalDamage) + item.Value(ItemModifierKind.AddedMaximumPhysicalDamage)) / 2),
            checked(item.IncreasedCriticalChanceBasisPoints + advanced.IncreasedCriticalChanceBasisPoints + jewel.IncreasedCriticalChanceBasisPoints),
            checked(item.Value(ItemModifierKind.IncreasedCriticalMultiplierBasisPoints) +
                advanced.IncreasedCriticalMultiplierBasisPoints + jewel.IncreasedCriticalMultiplierBasisPoints),
            jewel.MoreAttackDamageBasisPoints,
            checked(jewel.MoreSpellDamageBasisPoints + (equipment.Effects?.Value(ItemModifierKind.MoreSpellDamageBasisPoints) ?? 0)),
            jewel.MoreDamageOverTimeBasisPoints,
            jewel.IncreasedActionSpeedBasisPoints,
            jewel.InstantLifeLeechBasisPoints,
            checked(item.IncreasedBleedChanceBasisPoints + passive.IncreasedBleedChanceBasisPoints),
            warCry,
            passive.ChargedHeavyStrike ? new ChargedHeavyStrikeState() : null,
            (advanced.Flaskless ? [] : loadout.Items.Where(pair => pair.Key is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5)
                .Select(pair => P1FlaskRules.KindForBase(pair.Value.Base.StableId)).Where(kind => kind.HasValue)
                .Select(kind => kind!.Value).Distinct().ToArray()),
            weapon,
            VirtueVice(item, jewel),
            checked(item.Value(ItemModifierKind.IncreasedSpellDamageBasisPoints) +
                jewel.IncreasedSpellDamageBasisPoints + attributeMemory.IncreasedSpellDamageBasisPoints),
            increasedAttackSpeed);
    }

    private static P30VirtueViceLoadout VirtueVice(EquipmentModifiers item, P30JewelModifiers jewel)
    {
        var maximum = new Dictionary<P30VirtueViceKind, int>(jewel.AdditionalVirtueViceMaximum ??
            new Dictionary<P30VirtueViceKind, int>());
        var held = new HashSet<P30VirtueViceKind>();
        Add(P30VirtueViceKind.Mercy, ItemModifierKind.MercyMaximum, ItemModifierKind.HoldMercyAtMaximum);
        Add(P30VirtueViceKind.Temperance, ItemModifierKind.TemperanceMaximum, ItemModifierKind.HoldTemperanceAtMaximum);
        Add(P30VirtueViceKind.Humility, ItemModifierKind.HumilityMaximum, ItemModifierKind.HoldHumilityAtMaximum);
        Add(P30VirtueViceKind.Rage, ItemModifierKind.RageMaximum, ItemModifierKind.HoldRageAtMaximum);
        Add(P30VirtueViceKind.Sloth, ItemModifierKind.SlothMaximum, ItemModifierKind.HoldSlothAtMaximum);
        Add(P30VirtueViceKind.Arrogance, ItemModifierKind.ArroganceMaximum, ItemModifierKind.HoldArroganceAtMaximum);
        return new(maximum, held.Order().ToArray(), jewel.Oaths ?? []);
        void Add(P30VirtueViceKind kind, ItemModifierKind maximumKind, ItemModifierKind heldKind)
        {
            maximum[kind] = maximum.GetValueOrDefault(kind) + item.Value(maximumKind);
            if (item.Value(heldKind) > 0) held.Add(kind);
        }
    }

    private static int WeaponPassiveIncrease(EquipmentLoadout loadout, PassiveBuildModifiers legacy, P205PassiveModifiers passive)
    {
        ItemInstance? main = loadout.Items.GetValueOrDefault(EquipmentSlot.MainHand);
        ItemInstance? off = loadout.Items.GetValueOrDefault(EquipmentSlot.OffHand);
        int result = main?.Base.Category switch
        {
            ItemCategory.TwoHandWeapon => legacy.IncreasedTwoHandDamageBasisPoints,
            ItemCategory.OneHandWeapon => passive.SpecializedValue(PassiveEffectKind.IncreasedOneHandDamageBasisPoints),
            _ => passive.SpecializedValue(PassiveEffectKind.IncreasedUnarmedDamageBasisPoints),
        };
        IReadOnlyList<string> tags = main?.Base.ItemTags ?? Array.Empty<string>();
        if (tags.Contains("sword", StringComparer.Ordinal) || tags.Contains("runeblade", StringComparer.Ordinal))
            result += passive.SpecializedValue(PassiveEffectKind.IncreasedSwordDamageBasisPoints);
        if (tags.Contains("axe", StringComparer.Ordinal)) result += passive.SpecializedValue(PassiveEffectKind.IncreasedAxeDamageBasisPoints);
        if (tags.Contains("mace", StringComparer.Ordinal)) result += passive.SpecializedValue(PassiveEffectKind.IncreasedMaceDamageBasisPoints);
        if (tags.Contains("dagger", StringComparer.Ordinal)) result += passive.SpecializedValue(PassiveEffectKind.IncreasedDaggerDamageBasisPoints);
        if (tags.Contains("bow", StringComparer.Ordinal)) result += passive.SpecializedValue(PassiveEffectKind.IncreasedBowDamageBasisPoints);
        if (tags.Contains("wand", StringComparer.Ordinal)) result += passive.SpecializedValue(PassiveEffectKind.IncreasedWandDamageBasisPoints);
        if (off?.Base.Category == ItemCategory.OneHandWeapon)
            result += passive.SpecializedValue(PassiveEffectKind.IncreasedDualWieldDamageBasisPoints);
        if (off?.Base.ItemTags.Contains("shield", StringComparer.Ordinal) == true)
            result += passive.SpecializedValue(PassiveEffectKind.IncreasedShieldAttackDamageBasisPoints);
        return result;
    }

    private static int ScaleAttribute(int value, int increasedBasisPoints) =>
        checked(value * (10_000 + increasedBasisPoints) / 10_000);
}

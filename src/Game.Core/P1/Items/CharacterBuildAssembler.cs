using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Progression;

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
    int IncreasedBleedChanceBasisPoints,
    WarCryState WarCry,
    ChargedHeavyStrikeState? ChargedHeavyStrike,
    IReadOnlyList<P1FlaskKind> Flasks,
    WeaponProfile EffectiveWeapon)
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
        SkillConfiguration heavyStrikeConfiguration)
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
        var attributes = new CharacterAttributes(
            checked(baseAttributes.Physique + item.Physique + advanced.Physique),
            checked(baseAttributes.Dexterity + item.Dexterity + advanced.Dexterity),
            checked(baseAttributes.Spirit + item.Spirit + advanced.Spirit),
            checked(baseAttributes.Energy + item.Energy + advanced.Energy));
        DefensiveEquipment defense = equipment.Defense;
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
            passive.IncreasedMaximumLifeBasisPoints,
            checked(item.FlatMaximumMana + passive.FlatMaximumMana),
            checked(item.IncreasedArmorBasisPoints + passive.IncreasedArmorBasisPoints),
            evasionIncrease,
            checked(item.IncreasedShieldBasisPoints + advanced.IncreasedShieldBasisPoints),
            checked(item.IncreasedManaRegenerationBasisPoints + passive.IncreasedManaRegenerationBasisPoints),
            checked(item.FireResistanceBasisPoints + advanced.FireResistanceBasisPoints),
            checked(item.ColdResistanceBasisPoints + advanced.ColdResistanceBasisPoints),
            checked(item.LightningResistanceBasisPoints + advanced.LightningResistanceBasisPoints),
            checked(item.VoidResistanceBasisPoints + advanced.VoidResistanceBasisPoints),
            checked(item.BlockChanceBasisPoints + advanced.BlockChanceBasisPoints),
            checked(item.SpellSuppressionBasisPoints + advanced.SpellSuppressionBasisPoints),
            checked(item.FlatLifeRegeneration + advanced.FlatLifeRegeneration),
            item.IncreasedMovementSpeedBasisPoints);
        SkillUseProfile heavyStrike = SkillRules.BuildHeavyStrike(
            heavyStrikeConfiguration,
            weapon,
            sheet.MaximumLife().Value,
            checked(item.IncreasedAttackSpeedBasisPoints + passive.IncreasedAttackSpeedBasisPoints));
        heavyStrike = P1LegendaryRules.ApplyToHeavyStrike(heavyStrike, equipment.WeaponLegendaryRule);
        var warCry = new WarCryState { EchoNotableAllocated = passive.Echo };
        return new AssembledCharacterBuild(
            sheet,
            equipment,
            passive,
            heavyStrike,
            checked(item.FlatAccuracy + passive.FlatAccuracy),
            checked(
                item.IncreasedPhysicalDamageBasisPoints +
                passive.IncreasedAttackDamageBasisPoints +
                passive.IncreasedTwoHandDamageBasisPoints),
            item.AddedPhysicalDamage,
            checked(item.IncreasedCriticalChanceBasisPoints + advanced.IncreasedCriticalChanceBasisPoints),
            checked(item.IncreasedBleedChanceBasisPoints + passive.IncreasedBleedChanceBasisPoints),
            warCry,
            passive.ChargedHeavyStrike ? new ChargedHeavyStrikeState() : null,
            (advanced.Flaskless ? [] : loadout.Items.Where(pair => pair.Key is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5)
                .Select(pair => P1FlaskRules.KindForBase(pair.Value.Base.StableId)).Where(kind => kind.HasValue)
                .Select(kind => kind!.Value).Distinct().ToArray()),
            weapon);
    }
}

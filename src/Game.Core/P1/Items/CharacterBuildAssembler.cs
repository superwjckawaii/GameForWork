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
    IReadOnlyList<P1FlaskKind> Flasks)
{
    public HeavyStrikeRequest CreateHeavyStrikeRequest(
        ResourceState resources,
        int targetEvasion,
        int targetArmor) => new(
        resources,
        HeavyStrike,
        Equipment.Weapon ?? throw new InvalidOperationException("A weapon must be equipped."),
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
        WeaponProfile weapon = equipment.Weapon ?? throw new InvalidOperationException("A weapon must be equipped.");
        EquipmentModifiers item = equipment.Modifiers;
        PassiveBuildModifiers passive = passiveTree.CalculateModifiers();
        var attributes = new CharacterAttributes(
            checked(baseAttributes.Physique + item.Physique),
            checked(baseAttributes.Dexterity + item.Dexterity),
            checked(baseAttributes.Spirit + item.Spirit),
            checked(baseAttributes.Energy + item.Energy));
        var sheet = new CharacterSheet(
            level,
            attributes,
            equipment.Defense,
            checked(item.FlatMaximumLife + passive.FlatMaximumLife),
            passive.IncreasedMaximumLifeBasisPoints,
            checked(item.FlatMaximumMana + passive.FlatMaximumMana),
            checked(item.IncreasedArmorBasisPoints + passive.IncreasedArmorBasisPoints),
            item.IncreasedEvasionBasisPoints,
            item.IncreasedShieldBasisPoints,
            checked(item.IncreasedManaRegenerationBasisPoints + passive.IncreasedManaRegenerationBasisPoints),
            item.FireResistanceBasisPoints,
            item.ColdResistanceBasisPoints,
            item.LightningResistanceBasisPoints,
            item.VoidResistanceBasisPoints,
            item.BlockChanceBasisPoints,
            item.SpellSuppressionBasisPoints,
            item.FlatLifeRegeneration,
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
            item.IncreasedCriticalChanceBasisPoints,
            checked(item.IncreasedBleedChanceBasisPoints + passive.IncreasedBleedChanceBasisPoints),
            warCry,
            passive.ChargedHeavyStrike ? new ChargedHeavyStrikeState() : null,
            loadout.Items.Where(pair => pair.Key is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5)
                .Select(pair => P1FlaskRules.KindForBase(pair.Value.Base.StableId)).Where(kind => kind.HasValue)
                .Select(kind => kind!.Value).Distinct().ToArray());
    }
}

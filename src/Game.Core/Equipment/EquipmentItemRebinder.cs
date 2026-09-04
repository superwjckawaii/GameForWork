using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.Items;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Equipment;

public static class EquipmentItemRebinder
{
    private static readonly HashSet<ItemModifierKind> ForbiddenGlobalWeaponDamageIncreases =
    [
        ItemModifierKind.IncreasedAttackDamageBasisPoints, ItemModifierKind.IncreasedSpellDamageBasisPoints,
        ItemModifierKind.IncreasedElementalDamageBasisPoints, ItemModifierKind.IncreasedPhysicalDamageBasisPoints,
        ItemModifierKind.IncreasedFireDamageBasisPoints, ItemModifierKind.IncreasedColdDamageBasisPoints,
        ItemModifierKind.IncreasedLightningDamageBasisPoints, ItemModifierKind.IncreasedVoidDamageBasisPoints,
        ItemModifierKind.IncreasedMeleeDamageBasisPoints, ItemModifierKind.IncreasedProjectileDamageBasisPoints,
        ItemModifierKind.IncreasedAreaDamageBasisPoints, ItemModifierKind.IncreasedDamageOverTimeBasisPoints,
        ItemModifierKind.IncreasedBleedDamageBasisPoints, ItemModifierKind.IncreasedPoisonDamageBasisPoints,
        ItemModifierKind.IncreasedIgniteDamageBasisPoints,
    ];
    private static readonly HashSet<string> ConditionalWeaponDamageFamilies =
    [
        "equipment.affix.occult.wither",
        "equipment.affix.projectile.far_damage",
        "equipment.affix.spell.elemental",
        "equipment.affix.spell.void",
    ];

    public static ItemInstance Rebind(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ItemBaseDefinition itemBase = EquipmentCatalog.GetBase(item.Base.StableId);
        AffixRoll[] affixes = item.Affixes.Where(affix => !IsForbiddenLegacyWeaponAffix(itemBase, affix))
            .Select(affix => RebindAffix(item, itemBase, affix)).ToArray();
        string fractured = affixes.Any(affix => affix.Definition.StableFamilyId == EquipmentCatalog.ResolveAffixId(item.FracturedAffixFamilyId))
            ? EquipmentCatalog.ResolveAffixId(item.FracturedAffixFamilyId) : string.Empty;
        ItemEnchantment? enchantment = item.Enchantment;
        if (enchantment is not null)
        {
            try { enchantment = EquipmentEnchantmentCatalog.Get(enchantment.StableId); }
            catch (InvalidOperationException)
            {
                enchantment = EquipmentEnchantmentCatalog.All.FirstOrDefault(value => value.DisplayName == enchantment.DisplayName);
            }
        }
        IReadOnlyList<RolledAffixComponent> corruptionComponents = RestoreCorruptionComponents(item);
        return item with
        {
            Base = itemBase,
            Affixes = affixes,
            FracturedAffixFamilyId = fractured,
            Enchantment = enchantment,
            RolledBaseArmor = item.RolledBaseArmor > 0 ? item.RolledBaseArmor : itemBase.Armor,
            RolledBaseEvasion = item.RolledBaseEvasion > 0 ? item.RolledBaseEvasion : itemBase.Evasion,
            RolledBaseShield = item.RolledBaseShield > 0 ? item.RolledBaseShield : itemBase.Shield,
            RolledBaseSpiritBarrier = item.RolledBaseSpiritBarrier > 0 ? item.RolledBaseSpiritBarrier : itemBase.SpiritBarrier,
            RolledCorruptionComponents = corruptionComponents,
        };
    }

    private static IReadOnlyList<RolledAffixComponent> RestoreCorruptionComponents(ItemInstance item)
    {
        if (item.CorruptionComponents.Count > 0 || string.IsNullOrWhiteSpace(item.CorruptionImplicitId))
            return item.CorruptionComponents;
        EquipmentCorruptionImplicitEntry? entry = EquipmentCatalog.CorruptionImplicits.FirstOrDefault(value => value.Id == item.CorruptionImplicitId);
        if (entry is null) return [];
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{item.InstanceId}|{entry.Id}|corruption"));
        return EquipmentCorruptionCatalog.Roll(entry, new Pcg32(BitConverter.ToUInt64(digest, 0)));
    }

    private static AffixRoll RebindAffix(ItemInstance item, ItemBaseDefinition itemBase, AffixRoll roll)
    {
        if (roll.Crafted || roll.Definition.Source is "LegendaryFixed" or "传奇固定") return roll;
        string canonical = EquipmentCatalog.ResolveAffixId(roll.Definition.StableFamilyId);
        AffixDefinition? definition = EquipmentCatalog.Affixes.FirstOrDefault(value =>
            value.StableFamilyId == canonical && value.Tier == roll.Definition.Tier && value.Supports(itemBase));
        if (definition is not null) return RebindToDefinition(roll, definition);

        AffixDefinition[] replacements = EquipmentCatalog.Affixes.Where(value => value.Position == roll.Definition.Position &&
            value.Tier == roll.Definition.Tier && value.MinimumItemLevel <= item.ItemLevel && value.Supports(itemBase) &&
            SharesTag(value, roll.Definition)).ToArray();
        if (replacements.Length == 0)
            replacements = EquipmentCatalog.Affixes.Where(value => value.Position == roll.Definition.Position &&
                value.MinimumItemLevel <= item.ItemLevel && value.Supports(itemBase)).ToArray();
        if (replacements.Length == 0) throw new InvalidDataException($"No legal replacement for {roll.Definition.StableFamilyId} on {itemBase.StableId}.");
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{item.InstanceId}|{roll.Definition.StableFamilyId}|{roll.Definition.Position}|{roll.Definition.Tier}"));
        definition = replacements[BitConverter.ToUInt32(digest, 0) % replacements.Length];
        RolledAffixComponent[] effects = definition.EffectComponents.Select(component => new RolledAffixComponent(
            component.Kind, component.MinimumValue + (int)(BitConverter.ToUInt32(digest, 4) % (uint)Math.Max(1, component.MaximumValue - component.MinimumValue + 1)),
            component.Scope, component.DisplayText)).ToArray();
        return new AffixRoll(definition, effects[0].Value, roll.Crafted, effects);
    }

    private static AffixRoll RebindToDefinition(AffixRoll roll, AffixDefinition definition)
    {
        IReadOnlyList<AffixModifierComponent> oldDefinitions = roll.Definition.EffectComponents;
        IReadOnlyList<AffixModifierComponent> newDefinitions = definition.EffectComponents;
        IReadOnlyList<RolledAffixComponent> oldEffects = roll.Effects;
        RolledAffixComponent[] effects = newDefinitions.Select((component, index) =>
        {
            RolledAffixComponent? oldEffect = oldEffects.FirstOrDefault(value => value.Kind == component.Kind)
                ?? oldEffects.ElementAtOrDefault(index);
            AffixModifierComponent? oldDefinition = oldDefinitions.FirstOrDefault(value => value.Kind == component.Kind)
                ?? oldDefinitions.ElementAtOrDefault(index);
            int value = oldEffect is null || oldDefinition is null
                ? component.MinimumValue
                : RescaleRoll(oldEffect.Value, oldDefinition.MinimumValue, oldDefinition.MaximumValue,
                    component.MinimumValue, component.MaximumValue);
            return new RolledAffixComponent(component.Kind, value, component.Scope, component.DisplayText);
        }).ToArray();
        return roll with { Definition = definition, Value = effects[0].Value, Components = effects };
    }

    private static int RescaleRoll(int value, int oldMinimum, int oldMaximum, int newMinimum, int newMaximum)
    {
        if (oldMinimum == newMinimum && oldMaximum == newMaximum) return Math.Clamp(value, newMinimum, newMaximum);
        int newSpan = newMaximum - newMinimum;
        int oldSpan = oldMaximum - oldMinimum;
        if (oldSpan <= 0) return newMinimum + newSpan / 2;
        long offset = Math.Clamp(value, oldMinimum, oldMaximum) - oldMinimum;
        return newMinimum + (int)((offset * newSpan + oldSpan / 2L) / oldSpan);
    }

    private static bool SharesTag(AffixDefinition left, AffixDefinition right)
    {
        string[] leftTags = left.ModTags?.ToArray() ?? [];
        string[] rightTags = right.ModTags?.ToArray() ?? [];
        return leftTags.Length == 0 || rightTags.Length == 0 || leftTags.Intersect(rightTags, StringComparer.Ordinal).Any();
    }

    private static bool IsForbiddenLegacyWeaponAffix(ItemBaseDefinition itemBase, AffixRoll affix) =>
        itemBase.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon &&
        !ConditionalWeaponDamageFamilies.Contains(affix.Definition.StableFamilyId) &&
        affix.Effects.Any(effect => effect.Scope == ItemModifierScope.Global && ForbiddenGlobalWeaponDamageIncreases.Contains(effect.Kind));
}

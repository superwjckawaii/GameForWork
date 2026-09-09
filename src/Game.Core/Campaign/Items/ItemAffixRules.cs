namespace GameForWork.Core.Campaign.Items;

/// <summary>Shared explicit-affix capacity and effect rules; stored rolls stay unscaled.</summary>
public static class ItemAffixRules
{
    private static AffixPosition? ExclusivePosition(ItemBaseDefinition itemBase) => itemBase.StableId switch
    {
        "harbor.base.wavechaser_ring" => AffixPosition.Suffix,
        "harbor.base.harbor_guard_ring" => AffixPosition.Prefix,
        _ => null,
    };

    public static int Capacity(ItemBaseDefinition itemBase, ItemRarity rarity, AffixPosition position)
    {
        int normal = rarity switch { ItemRarity.Magic => 1, ItemRarity.Rare => 3, _ => 0 };
        return ExclusivePosition(itemBase) is { } exclusive
            ? normal == 0 || position != exclusive ? 0 : normal + 2
            : normal;
    }

    public static bool IsSpecial(ItemBaseDefinition itemBase) => ExclusivePosition(itemBase).HasValue;

    public static int CraftedCapacity(ItemInstance item, AffixPosition position) =>
        !IsSpecial(item.Base) && item.Rarity == ItemRarity.Basic ? 3 : Capacity(item.Base, item.Rarity, position);

    public static bool Fits(ItemInstance item) => item.Rarity == ItemRarity.Legendary ||
        item.PrefixCount <= Capacity(item.Base, item.Rarity, AffixPosition.Prefix) &&
        item.SuffixCount <= Capacity(item.Base, item.Rarity, AffixPosition.Suffix);

    public static int EffectMultiplier(ItemBaseDefinition itemBase, AffixPosition position, ItemModifierKind kind,
        ItemModifierScope scope) => ExclusivePosition(itemBase) == position && scope != ItemModifierScope.Rule &&
        IsScalable(kind) ? 2 : 1;

    // Basis-point modifiers are numeric; capacities, counts and Boolean rules are deliberately excluded.
    private static bool IsScalable(ItemModifierKind kind) => kind.ToString().EndsWith("BasisPoints", StringComparison.Ordinal) ||
        kind is ItemModifierKind.Physique or ItemModifierKind.Dexterity or ItemModifierKind.Spirit or ItemModifierKind.Energy or
            ItemModifierKind.AddedPhysicalDamage or ItemModifierKind.FlatAccuracy or ItemModifierKind.FlatMaximumLife or
            ItemModifierKind.FlatMaximumMana or ItemModifierKind.FlatLifeRegeneration or ItemModifierKind.FlatArmor or
            ItemModifierKind.FlatEvasion or ItemModifierKind.FlatShield or ItemModifierKind.FlatSpiritBarrier or
            ItemModifierKind.LifeOnHit or ItemModifierKind.ManaOnHit or ItemModifierKind.ShieldOnHit or
            ItemModifierKind.AddedMinimumPhysicalDamage or ItemModifierKind.AddedMaximumPhysicalDamage or
            ItemModifierKind.AddedMinimumFireDamage or ItemModifierKind.AddedMaximumFireDamage or
            ItemModifierKind.AddedMinimumColdDamage or ItemModifierKind.AddedMaximumColdDamage or
            ItemModifierKind.AddedMinimumLightningDamage or ItemModifierKind.AddedMaximumLightningDamage or
            ItemModifierKind.AddedMinimumVoidDamage or ItemModifierKind.AddedMaximumVoidDamage;

    public static IReadOnlyList<RolledAffixComponent> Effects(ItemInstance item, AffixRoll affix) =>
        affix.Effects.Select(effect => effect with
        {
            Value = checked(effect.Value * EffectMultiplier(item.Base, affix.Definition.Position, effect.Kind, effect.Scope)),
        }).ToArray();
}

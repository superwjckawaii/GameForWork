using GameForWork.Core.Equipment;

namespace GameForWork.Core.P1.Items;

public static class P1Affixes
{
    private static readonly IReadOnlyList<AffixDefinition> Catalog = EquipmentCatalog.Affixes;
    private static readonly IReadOnlyDictionary<string, AffixDefinition[]> ByBase = EquipmentCatalog.Bases
        .ToDictionary(itemBase => itemBase.StableId,
            itemBase => Catalog.Where(affix => affix.Supports(itemBase)).ToArray(), StringComparer.Ordinal);

    public static IReadOnlyList<AffixDefinition> All => Catalog;

    public static IReadOnlyList<AffixDefinition> For(ItemCategory category, int itemLevel) =>
        Catalog.Where(affix =>
            (affix.ApplicableCategories?.Contains(category) ?? affix.Category == category) &&
            affix.MinimumItemLevel <= itemLevel).ToArray();

    public static IReadOnlyList<AffixDefinition> For(ItemBaseDefinition itemBase, int itemLevel) =>
        ByBase[EquipmentCatalog.ResolveBaseId(itemBase.StableId)]
            .Where(affix => affix.MinimumItemLevel <= itemLevel).ToArray();

    public static int TierFor(ItemBaseDefinition itemBase, AffixDefinition affix)
        => affix.Tier;
}

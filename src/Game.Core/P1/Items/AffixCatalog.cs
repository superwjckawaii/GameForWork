using GameForWork.Core.Equipment;

namespace GameForWork.Core.P1.Items;

public static class P1Affixes
{
    private static readonly IReadOnlyList<AffixDefinition> Catalog = EquipmentCatalog.Affixes;

    public static IReadOnlyList<AffixDefinition> All => Catalog;

    public static IReadOnlyList<AffixDefinition> For(ItemCategory category, int itemLevel) =>
        Catalog.Where(affix =>
            (affix.ApplicableCategories?.Contains(category) ?? affix.Category == category) &&
            affix.MinimumItemLevel <= itemLevel).ToArray();

    public static IReadOnlyList<AffixDefinition> For(ItemBaseDefinition itemBase, int itemLevel) =>
        Catalog.Where(affix => affix.MinimumItemLevel <= itemLevel && affix.Supports(itemBase)).ToArray();

    public static int TierFor(ItemBaseDefinition itemBase, AffixDefinition affix)
        => affix.Tier;
}

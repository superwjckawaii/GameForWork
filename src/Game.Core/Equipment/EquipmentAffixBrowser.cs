using GameForWork.Core.P1.Items;

namespace GameForWork.Core.Equipment;

public sealed record EquipmentAffixQuery(
    int ItemLevel = 120,
    ItemCategory? Category = null,
    string? BaseStableId = null,
    AffixPosition? Position = null,
    string Search = "");

public sealed record EquipmentAffixView(AffixDefinition Definition, int Tier, int Weight);

public static class EquipmentAffixBrowser
{
    public static IReadOnlyList<EquipmentAffixView> Query(EquipmentAffixQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        int itemLevel = Math.Clamp(query.ItemLevel, 1, 120);
        ItemBaseDefinition? itemBase = string.IsNullOrWhiteSpace(query.BaseStableId)
            ? null
            : EquipmentCatalog.GetBase(query.BaseStableId);
        string search = query.Search.Trim();

        return EquipmentCatalog.Affixes
            .Where(affix => affix.MinimumItemLevel <= itemLevel)
            .Where(affix => itemBase is not null
                ? affix.Supports(itemBase)
                : query.Category is null || SupportsCategory(affix, query.Category.Value))
            .Where(affix => query.Position is null || affix.Position == query.Position)
            .Where(affix => search.Length == 0 || SearchText(affix).Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(affix => new EquipmentAffixView(affix, affix.Tier,
                itemBase is null ? affix.Weight : affix.WeightFor(itemBase)))
            .OrderBy(view => view.Definition.DisplayName, StringComparer.Ordinal)
            .ThenBy(view => view.Tier)
            .ThenBy(view => view.Definition.StableFamilyId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool SupportsCategory(AffixDefinition affix, ItemCategory category) =>
        affix.ApplicableCategories?.Contains(category) ?? affix.Category == category;

    private static string SearchText(AffixDefinition affix) =>
        $"{affix.DisplayName} {affix.StableFamilyId} {affix.SourceId} {affix.Source} " +
        $"{string.Join(' ', affix.ModTags ?? [])} {string.Join(' ', affix.RequiredBaseTags ?? [])}";
}

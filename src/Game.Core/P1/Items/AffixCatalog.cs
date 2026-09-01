using GameForWork.Core.P19;
using GameForWork.Core.P24;
using GameForWork.Core.P30;

namespace GameForWork.Core.P1.Items;

public static class P1Affixes
{
    private static readonly IReadOnlyList<AffixDefinition> Catalog = Build();
    private static readonly IReadOnlyDictionary<string, int> ContextTierMap = BuildContextTierMap();

    public static IReadOnlyList<AffixDefinition> All => Catalog;

    public static IReadOnlyList<AffixDefinition> For(ItemCategory category, int itemLevel) =>
        Catalog.Where(affix =>
            (affix.ApplicableCategories?.Contains(category) ?? affix.Category == category) &&
            affix.MinimumItemLevel <= itemLevel).ToArray();

    public static IReadOnlyList<AffixDefinition> For(ItemBaseDefinition itemBase, int itemLevel) =>
        Catalog.Where(affix => affix.MinimumItemLevel <= itemLevel && affix.Supports(itemBase)).ToArray();

    public static int TierFor(ItemBaseDefinition itemBase, AffixDefinition affix)
    {
        if (!affix.StableFamilyId.StartsWith("p19.affix.", StringComparison.Ordinal)) return affix.Tier;
        return ContextTierMap.GetValueOrDefault(ContextTierKey(itemBase.StableId, affix.StableFamilyId, affix.SourceId), affix.Tier);
    }

    private static IReadOnlyDictionary<string, int> BuildContextTierMap()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ItemBaseDefinition itemBase in P19Catalog.Bases)
        foreach (IGrouping<string, AffixDefinition> family in Catalog
                     .Where(affix => affix.StableFamilyId.StartsWith("p19.affix.", StringComparison.Ordinal) && affix.Supports(itemBase))
                     .GroupBy(affix => affix.StableFamilyId, StringComparer.Ordinal))
        {
            AffixDefinition[] applicable = family.OrderByDescending(affix => affix.MinimumItemLevel)
                .ThenByDescending(affix => affix.MaximumValue).ToArray();
            for (int index = 0; index < applicable.Length; index++)
                result[ContextTierKey(itemBase.StableId, applicable[index].StableFamilyId, applicable[index].SourceId)] = index + 1;
        }
        return result;
    }

    private static string ContextTierKey(string baseId, string familyId, string sourceId) =>
        baseId + '|' + familyId + '|' + sourceId;

    private static IReadOnlyList<AffixDefinition> Build() => P19Catalog.Affixes
        .Concat(P30EquipmentAffixes.Ordinary)
        .Concat(P24ItemCatalog.Affixes)
        .OrderBy(affix => affix.StableFamilyId, StringComparer.Ordinal)
        .ThenBy(affix => affix.Tier)
        .ThenBy(affix => affix.SourceId, StringComparer.Ordinal)
        .ToArray();
}

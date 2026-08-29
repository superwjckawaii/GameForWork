using GameForWork.Core.P19;

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

    private static string ContextTierKey(string baseId, string familyId, string sourceId) => baseId + '|' + familyId + '|' + sourceId;

    private static IReadOnlyList<AffixDefinition> Build()
    {
        var result = new List<AffixDefinition>();
        AddTwoTiers(result, ItemCategory.TwoHandWeapon, "weapon.added_physical", "附加物理伤害", AffixPosition.Prefix,
            ItemModifierKind.AddedPhysicalDamage, 1, 2, 3, 5, 1_000);
        AddTwoTiers(result, ItemCategory.TwoHandWeapon, "weapon.increased_physical", "物理伤害增加", AffixPosition.Prefix,
            ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 1_000, 2_000, 2_100, 3_500, 1_000);
        AddTwoTiers(result, ItemCategory.TwoHandWeapon, "weapon.accuracy", "命中", AffixPosition.Suffix,
            ItemModifierKind.FlatAccuracy, 10, 20, 21, 35, 1_000);
        AddTwoTiers(result, ItemCategory.TwoHandWeapon, "weapon.attack_speed", "攻击速度增加", AffixPosition.Suffix,
            ItemModifierKind.IncreasedAttackSpeedBasisPoints, 400, 700, 800, 1_200, 800);
        AddTwoTiers(result, ItemCategory.TwoHandWeapon, "weapon.critical", "暴击率增加", AffixPosition.Suffix,
            ItemModifierKind.IncreasedCriticalChanceBasisPoints, 500, 1_000, 1_100, 1_800, 700);
        AddTwoTiers(result, ItemCategory.TwoHandWeapon, "weapon.bleed", "流血概率", AffixPosition.Suffix,
            ItemModifierKind.IncreasedBleedChanceBasisPoints, 500, 800, 900, 1_500, 700);
        AddExtraLink(result, ItemCategory.TwoHandWeapon, "weapon.extra_link");

        foreach (ItemCategory category in new[] { ItemCategory.BodyArmor, ItemCategory.Helmet })
        {
            string prefix = category == ItemCategory.BodyArmor ? "body" : "helmet";
            AddTwoTiers(result, category, $"{prefix}.life", "最大生命", AffixPosition.Prefix,
                ItemModifierKind.FlatMaximumLife, 5, 10, 11, 18, 1_000);
            AddTwoTiers(result, category, $"{prefix}.mana", "最大法力", AffixPosition.Prefix,
                ItemModifierKind.FlatMaximumMana, 5, 10, 11, 18, 800);
            AddTwoTiers(result, category, $"{prefix}.armor", "护甲增加", AffixPosition.Prefix,
                ItemModifierKind.IncreasedArmorBasisPoints, 1_000, 2_000, 2_100, 3_500, 900);
            AddTwoTiers(result, category, $"{prefix}.evasion", "闪避增加", AffixPosition.Prefix,
                ItemModifierKind.IncreasedEvasionBasisPoints, 1_000, 2_000, 2_100, 3_500, 900);
            AddTwoTiers(result, category, $"{prefix}.shield", "护盾增加", AffixPosition.Prefix,
                ItemModifierKind.IncreasedShieldBasisPoints, 1_000, 2_000, 2_100, 3_500, 900);
            AddTwoTiers(result, category, $"{prefix}.flask", "生命药剂效果", AffixPosition.Suffix,
                ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints, 500, 1_000, 1_100, 1_800, 500);
            AddExtraLink(result, category, $"{prefix}.extra_link");
        }

        foreach (ItemCategory category in new[] { ItemCategory.Gloves, ItemCategory.Boots })
        {
            string prefix = category == ItemCategory.Gloves ? "gloves" : "boots";
            AddTwoTiers(result, category, $"{prefix}.life", "最大生命", AffixPosition.Prefix,
                ItemModifierKind.FlatMaximumLife, 4, 8, 9, 14, 1_000);
            AddTwoTiers(result, category, $"{prefix}.armor", "护甲增加", AffixPosition.Prefix,
                ItemModifierKind.IncreasedArmorBasisPoints, 800, 1_600, 1_700, 2_800, 900);
            AddTwoTiers(result, category, $"{prefix}.evasion", "闪避增加", AffixPosition.Prefix,
                ItemModifierKind.IncreasedEvasionBasisPoints, 800, 1_600, 1_700, 2_800, 900);
            AddTwoTiers(result, category, $"{prefix}.shield", "护盾增加", AffixPosition.Prefix,
                ItemModifierKind.IncreasedShieldBasisPoints, 800, 1_600, 1_700, 2_800, 900);
            AddTwoTiers(result, category, $"{prefix}.accuracy", "命中", AffixPosition.Suffix,
                ItemModifierKind.FlatAccuracy, 6, 12, 13, 22, 800);
        }

        foreach (ItemCategory category in new[] { ItemCategory.Belt, ItemCategory.Amulet })
        {
            string prefix = category == ItemCategory.Belt ? "belt" : "amulet";
            AddTwoTiers(result, category, $"{prefix}.life", "最大生命", AffixPosition.Prefix,
                ItemModifierKind.FlatMaximumLife, 5, 10, 11, 18, 1_000);
            AddTwoTiers(result, category, $"{prefix}.mana", "最大法力", AffixPosition.Prefix,
                ItemModifierKind.FlatMaximumMana, 5, 10, 11, 18, 800);
            AddTwoTiers(result, category, $"{prefix}.physical", "附加物理伤害", AffixPosition.Prefix,
                ItemModifierKind.AddedPhysicalDamage, 1, 2, 3, 4, 700);
            AddTwoTiers(result, category, $"{prefix}.flask", "生命药剂效果", AffixPosition.Suffix,
                ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints, 400, 800, 900, 1_500, 600);
        }

        AddTwoTiers(result, ItemCategory.Ring, "ring.added_physical", "附加物理伤害", AffixPosition.Prefix,
            ItemModifierKind.AddedPhysicalDamage, 1, 2, 3, 4, 900);
        AddTwoTiers(result, ItemCategory.Ring, "ring.life", "最大生命", AffixPosition.Prefix,
            ItemModifierKind.FlatMaximumLife, 4, 8, 9, 15, 1_000);
        AddTwoTiers(result, ItemCategory.Ring, "ring.mana", "最大法力", AffixPosition.Prefix,
            ItemModifierKind.FlatMaximumMana, 4, 8, 9, 15, 800);
        AddTwoTiers(result, ItemCategory.Ring, "ring.accuracy", "命中", AffixPosition.Suffix,
            ItemModifierKind.FlatAccuracy, 8, 16, 17, 30, 1_000);
        AddTwoTiers(result, ItemCategory.Ring, "ring.mana_regeneration", "法力恢复", AffixPosition.Suffix,
            ItemModifierKind.IncreasedManaRegenerationBasisPoints, 500, 1_000, 1_100, 2_000, 700);
        AddTwoTiers(result, ItemCategory.Ring, "ring.critical", "暴击率增加", AffixPosition.Suffix,
            ItemModifierKind.IncreasedCriticalChanceBasisPoints, 500, 1_000, 1_100, 1_800, 700);
        result.AddRange(P19Catalog.Affixes);
        return result
            .OrderBy(affix => affix.StableFamilyId, StringComparer.Ordinal)
            .ThenBy(affix => affix.Tier)
            .ThenBy(affix => affix.SourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddTwoTiers(
        ICollection<AffixDefinition> target,
        ItemCategory category,
        string family,
        string name,
        AffixPosition position,
        ItemModifierKind kind,
        int tier2Minimum,
        int tier2Maximum,
        int tier1Minimum,
        int tier1Maximum,
        int weight)
    {
        target.Add(new AffixDefinition(
            $"core.affix.{family}", name, category, position, Tier: 2, MinimumItemLevel: 1,
            tier2Minimum, tier2Maximum, weight, kind));
        target.Add(new AffixDefinition(
            $"core.affix.{family}", name, category, position, Tier: 1, MinimumItemLevel: 6,
            tier1Minimum, tier1Maximum, Math.Max(1, weight / 2), kind));
    }

    private static void AddExtraLink(ICollection<AffixDefinition> target, ItemCategory category, string family)
    {
        target.Add(new AffixDefinition(
            $"core.affix.{family}", "额外连接容量", category, AffixPosition.Prefix,
            Tier: 1, MinimumItemLevel: 6, MinimumValue: 1, MaximumValue: 1, Weight: 100,
            ItemModifierKind.ExtraSupportLinkCapacity));
    }
}

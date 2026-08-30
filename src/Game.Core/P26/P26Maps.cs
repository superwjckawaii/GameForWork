using GameForWork.Core.P1.World;
using GameForWork.Core.P12;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P26;

public enum P26MapAffixFamily
{
    DangerousPrefix,
    RewardSuffix,
}

public enum P26MapAffixGroup
{
    None,
    BossShape,
    MechanicImprint,
}

public enum P26CorruptionRule
{
    None,
    BloodTide,
    Greed,
    Disorder,
    KingDisaster,
}

public enum P26MapOrder
{
    Recommended,
    TierAscending,
    OldestFirst,
}

public enum P26NoMatchBehavior
{
    Wait,
    Stop,
}

public sealed record P26MapAffixDefinition(
    P12MapAffixKind Kind,
    string StableId,
    string DisplayName,
    P26MapAffixFamily Family,
    P26MapAffixGroup Group,
    IReadOnlyList<int> Values,
    string EffectTemplate);

public static class P26MapAffixCatalog
{
    private static readonly IReadOnlyList<P26MapAffixDefinition> Definitions =
    [
        new(P12MapAffixKind.MonsterLife, "p26.map.prefix.stubborn", "顽固", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [20, 30, 40, 50], "怪物拥有 {0}% 更多最大生命"),
        new(P12MapAffixKind.MonsterDamage, "p26.map.prefix.ferocious", "凶暴", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [22, 32, 43, 54], "怪物造成 {0}% 更多伤害"),
        new(P12MapAffixKind.MonsterSpeed, "p26.map.prefix.swift_hunt", "迅猎", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [18, 27, 36, 45], "怪物行动与移动速度提高 {0}%"),
        new(P12MapAffixKind.PhysicalResistance, "p26.map.prefix.iron_wall", "铁壁", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [10, 15, 20, 25], "怪物物理伤害减免提高 {0}%"),
        new(P12MapAffixKind.ElementalShell, "p26.map.prefix.elemental_shell", "元素甲壳", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [15, 25, 32, 40], "怪物火焰、冰霜、闪电抗性提高 {0}%"),
        new(P12MapAffixKind.VoidShroud, "p26.map.prefix.void_shroud", "虚界遮蔽", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [15, 25, 32, 40], "怪物虚空抗性提高 {0}%"),
        new(P12MapAffixKind.ReducedRecovery, "p26.map.prefix.exhaustion", "枯竭", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [15, 25, 32, 40], "主角、佣兵和召唤物总回复降低 {0}%（最低保留 20%）"),
        new(P12MapAffixKind.Penetration, "p26.map.prefix.piercing", "穿透", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [7, 11, 14, 18], "怪物元素与虚空穿透、物理压制提高 {0}%"),
        new(P12MapAffixKind.MultipleProjectiles, "p26.map.prefix.multiple_hunt", "多重猎杀", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [18, 36, 54, 72], "怪物额外发射 2 个投射物，投射物速度提高 {0}%，投射物造成 30% 更少伤害，可重复命中"),
        new(P12MapAffixKind.AreaDisaster, "p26.map.prefix.area_disaster", "广域灾变", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [36, 54, 72, 90], "怪物范围提高 {0}%，范围伤害造成 9/18/27/36% 更多伤害"),
        new(P12MapAffixKind.ElementalPossession, "p26.map.prefix.elemental_possession", "元素附体", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [18, 27, 36, 45], "怪物随机选择火焰、冰霜、闪电或虚空，额外获得物理伤害的 {0}% 为该伤害"),
        new(P12MapAffixKind.Stronghold, "p26.map.prefix.stronghold", "强敌盘踞", P26MapAffixFamily.DangerousPrefix, P26MapAffixGroup.None, [30, 45, 60, 75], "稀有怪与 Boss 拥有 {0}% 更多生命，并造成 14/22/29/36% 更多伤害"),

        new(P12MapAffixKind.MightyPacks, "p26.map.suffix.mighty_packs", "群雄", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.None, [20, 30, 40, 50], "{0}% 普通怪群升级为魔法怪群，不增加怪物数量"),
        new(P12MapAffixKind.EliteLeaders, "p26.map.suffix.elite_leaders", "精锐", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.None, [1, 1, 2, 3], "地图中有 {0} 场遭遇的首领被替换为稀有怪"),
        new(P12MapAffixKind.RoyalGuard, "p26.map.suffix.royal_guard", "王庭护卫", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.BossShape, [2, 2, 3, 4], "Boss 由 {0} 名魔法或稀有护卫保护，护卫计入怪物数量"),
        new(P12MapAffixKind.TwinThrone, "p26.map.suffix.twin_throne", "双生王座", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.BossShape, [70, 75, 80, 85], "生成 2 个 Boss；单体生命为 {0}%，各自拥有 60% 击杀预算，完成与专属传奇仅结算一次"),
        new(P12MapAffixKind.SealedVault, "p26.map.suffix.sealed_vault", "封印宝库", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.None, [1, 1, 2, 3], "生成 {0} 个受保护宝箱，预算为地图基础预算的 8/10/12/15%"),
        new(P12MapAffixKind.BountifulMark, "p26.map.suffix.bountiful_mark", "富饶路印", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.None, [20, 30, 45, 60], "非传奇装备稀有度提高 {0}%"),
        new(P12MapAffixKind.RoadEcho, "p26.map.suffix.road_echo", "道路回响", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.None, [10, 15, 20, 30], "Boss 额外掉落同阶地图的概率为 {0}%"),
        new(P12MapAffixKind.AbyssMark, "p26.map.suffix.abyss_mark", "裂渊印记", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.MechanicImprint, [20, 30, 45, 60], "必定出现深渊，深渊奖励造成 {0}% 更多收益"),
        new(P12MapAffixKind.GardenMark, "p26.map.suffix.garden_mark", "命能印记", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.MechanicImprint, [20, 30, 45, 60], "必定出现命能花园，命能获取提高 {0}%"),
        new(P12MapAffixKind.RedOathMark, "p26.map.suffix.red_oath_mark", "赤誓印记", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.MechanicImprint, [20, 30, 45, 60], "必定出现赤誓祭坛，祭坛奖励造成 {0}% 更多收益"),
        new(P12MapAffixKind.BlueOathMark, "p26.map.suffix.blue_oath_mark", "苍誓印记", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.MechanicImprint, [20, 30, 45, 60], "必定出现苍誓祭坛，延迟奖励造成 {0}% 更多收益"),
        new(P12MapAffixKind.HeadHunterMark, "p26.map.suffix.head_hunter_mark", "猎首印记", P26MapAffixFamily.RewardSuffix, P26MapAffixGroup.None, [1, 1, 2, 2], "生成 {0} 名游荡稀有怪，并保证掉落更高物品等级的底材"),
    ];

    private static readonly IReadOnlyDictionary<P12MapAffixKind, P26MapAffixDefinition> ByKind =
        Definitions.ToDictionary(definition => definition.Kind);

    public static IReadOnlyList<P26MapAffixDefinition> All => Definitions;
    public static IReadOnlyList<P26MapAffixDefinition> Prefixes => Definitions.Where(item => item.Family == P26MapAffixFamily.DangerousPrefix).ToArray();
    public static IReadOnlyList<P26MapAffixDefinition> Suffixes => Definitions.Where(item => item.Family == P26MapAffixFamily.RewardSuffix).ToArray();
    public static P26MapAffixDefinition Get(P12MapAffixKind kind) => ByKind.TryGetValue(kind, out P26MapAffixDefinition? value)
        ? value : throw new KeyNotFoundException($"Unknown P26 map affix: {kind}");

    public static int RankForTier(int tier) => tier switch
    {
        <= 5 => 1,
        <= 10 => 2,
        <= 16 => 3,
        _ => 4,
    };

    public static (int Monster, int Item) QuantityFor(P26MapAffixFamily family, int rank) => family switch
    {
        P26MapAffixFamily.DangerousPrefix => rank switch { 1 => (600, 800), 2 => (900, 1_200), 3 => (1_200, 1_600), _ => (1_500, 2_000) },
        _ => rank switch { 1 => (600, 1_000), 2 => (900, 1_500), 3 => (1_200, 2_000), _ => (1_500, 2_500) },
    };

    public static P12MapAffix Create(P26MapAffixDefinition definition, int rank)
    {
        rank = Math.Clamp(rank, 1, 4);
        (int monster, int item) = QuantityFor(definition.Family, rank);
        return new P12MapAffix(definition.Kind, definition.DisplayName, definition.Values[rank - 1],
            definition.Family, rank, monster, item);
    }
}

public sealed record P26MapFilter(
    int MinimumTier = P1MapItem.MinimumTier,
    int MaximumTier = P1MapItem.MaximumTier,
    int MinimumItemQuantityBasisPoints = 0,
    int MaximumItemQuantityBasisPoints = 25_000,
    int MinimumMonsterQuantityBasisPoints = 0,
    int MaximumMonsterQuantityBasisPoints = 12_000,
    IReadOnlyList<string>? AreaIds = null,
    IReadOnlyList<P12MapRarity>? Rarities = null,
    bool IncludeUncorrupted = true,
    bool IncludeCorrupted = true,
    int MinimumQuality = 0,
    IReadOnlyList<P12MapAffixKind>? RequiredAffixes = null,
    IReadOnlyList<P12MapAffixKind>? ExcludedAffixes = null)
{
    public static P26MapFilter All { get; } = new();

    public P26MapFilter Validate()
    {
        if (MinimumTier is < 1 or > 20 || MaximumTier is < 1 or > 20 || MinimumTier > MaximumTier ||
            MinimumQuality is < 0 or > 20 || MinimumItemQuantityBasisPoints < 0 ||
            MinimumMonsterQuantityBasisPoints < 0 || MaximumItemQuantityBasisPoints < MinimumItemQuantityBasisPoints ||
            MaximumMonsterQuantityBasisPoints < MinimumMonsterQuantityBasisPoints || (!IncludeCorrupted && !IncludeUncorrupted) ||
            (AreaIds?.Any(id => !P12MapCatalog.TryGet(id, out _)) ?? false) ||
            (Rarities?.Any(rarity => !Enum.IsDefined(rarity)) ?? false))
            throw new ArgumentOutOfRangeException(nameof(MinimumTier), "P26 map filter is invalid.");
        return this;
    }

    public bool Matches(P1MapItem map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Tier < MinimumTier || map.Tier > MaximumTier || map.Quality < MinimumQuality ||
            map.ItemQuantityBonusBasisPoints < MinimumItemQuantityBasisPoints || map.ItemQuantityBonusBasisPoints > MaximumItemQuantityBasisPoints ||
            map.MonsterQuantityBasisPoints < MinimumMonsterQuantityBasisPoints || map.MonsterQuantityBasisPoints > MaximumMonsterQuantityBasisPoints ||
            map.IsCorrupted && !IncludeCorrupted || !map.IsCorrupted && !IncludeUncorrupted ||
            AreaIds is { Count: > 0 } && !AreaIds.Contains(map.AreaId, StringComparer.Ordinal) ||
            Rarities is { Count: > 0 } && !Rarities.Contains(map.Rarity)) return false;
        IReadOnlySet<P12MapAffixKind> kinds = map.EffectiveAffixes.Select(affix => affix.Kind).ToHashSet();
        if (RequiredAffixes is { Count: > 0 } && RequiredAffixes.Any(required => !kinds.Contains(required))) return false;
        return ExcludedAffixes is not { Count: > 0 } || ExcludedAffixes.All(excluded => !kinds.Contains(excluded));
    }

    public IReadOnlyList<P1MapItem> Select(IEnumerable<P1MapItem> maps, P26MapOrder order = P26MapOrder.Recommended)
    {
        IEnumerable<P1MapItem> selected = maps.Where(Matches);
        return order switch
        {
            P26MapOrder.TierAscending => selected.OrderBy(map => map.Tier).ThenBy(map => map.AcquiredSequence).ToArray(),
            P26MapOrder.OldestFirst => selected.OrderBy(map => map.AcquiredSequence).ThenByDescending(map => map.Tier).ToArray(),
            _ => selected.OrderByDescending(map => map.Tier)
                .ThenByDescending(map => map.ItemQuantityBonusBasisPoints)
                .ThenByDescending(map => map.MonsterQuantityBasisPoints)
                .ThenBy(map => map.AcquiredSequence)
                .ThenBy(map => map.InstanceId, StringComparer.Ordinal).ToArray(),
        };
    }
}

public static class P26MapRules
{
    public const int MaximumInventory = 2_000;
    public const int RareMonsterQuantityCap = 6_000;
    public const int RareItemQuantityCap = 11_000;
    public const int CorruptedMonsterQuantityCap = 12_000;
    public const int CorruptedItemQuantityCap = 25_000;
    public const int CorruptionDestroyBasisPoints = 1_000;

    public static IReadOnlyList<P12MapAffix> RollAffixes(P12MapRarity rarity, int tier, ulong seed)
    {
        if (rarity == P12MapRarity.Basic) return [];
        var random = new Pcg32(seed);
        int rank = P26MapAffixCatalog.RankForTier(tier);
        int perFamily = rarity == P12MapRarity.Magic ? 1 : 2;
        P26MapAffixDefinition[] prefixes = P26MapAffixCatalog.Prefixes.OrderBy(_ => random.NextUInt()).Take(perFamily).ToArray();
        var selected = new List<P26MapAffixDefinition>(perFamily * 2);
        selected.AddRange(prefixes);
        foreach (P26MapAffixDefinition suffix in P26MapAffixCatalog.Suffixes.OrderBy(_ => random.NextUInt()))
        {
            if (selected.Count(item => item.Family == P26MapAffixFamily.RewardSuffix) >= perFamily) break;
            if (suffix.Group != P26MapAffixGroup.None && selected.Any(item => item.Group == suffix.Group)) continue;
            selected.Add(suffix);
        }
        return selected.Select(definition => P26MapAffixCatalog.Create(definition, rank)).ToArray();
    }

    public static P1MapItem NormalizeLegacy(P1MapItem map, ulong seed)
    {
        P12MapRarity rarity = map.IsCorrupted ? P12MapRarity.Rare : map.Rarity;
        P26CorruptionRule corruption = map.IsCorrupted && map.CorruptionRule == P26CorruptionRule.None
            ? (P26CorruptionRule)(1 + seed % 4) : map.CorruptionRule;
        if (rarity == P12MapRarity.Basic) return map with { Affixes = null, CorruptionRule = P26CorruptionRule.None };
        int expected = rarity == P12MapRarity.Magic ? 2 : 4;
        bool formal = map.EffectiveAffixes.Count == expected &&
            map.EffectiveAffixes.Count(affix => affix.Family == P26MapAffixFamily.DangerousPrefix) == expected / 2 &&
            map.EffectiveAffixes.Count(affix => affix.Family == P26MapAffixFamily.RewardSuffix) == expected / 2;
        return map with { Rarity = rarity, CorruptionRule = corruption,
            Affixes = formal ? map.EffectiveAffixes : RollAffixes(rarity, map.Tier, seed) };
    }

    public static int SaleGold(P1MapItem map)
    {
        int rarity = map.Rarity switch { P12MapRarity.Magic => 2, P12MapRarity.Rare => 5, _ => 0 };
        return Math.Max(1, 2 + map.Tier + rarity + map.Quality / 10 + map.MonsterQuantityBasisPoints / 3_000 +
            map.ItemQuantityBonusBasisPoints / 5_000 + (map.IsCorrupted ? 3 : 0));
    }

    public static P1MapItem? Corrupt(P1MapItem map, ulong seed, out bool destroyed)
    {
        if (map.Rarity != P12MapRarity.Rare || map.IsCorrupted)
            throw new InvalidOperationException("Only an uncorrupted rare map can be corrupted.");
        int roll = new Pcg32(seed).NextBasisPoints();
        if (roll < CorruptionDestroyBasisPoints)
        {
            destroyed = true;
            return null;
        }
        destroyed = false;
        int outcome = (roll - CorruptionDestroyBasisPoints) / 2_250;
        P26CorruptionRule rule = outcome switch
        {
            0 => P26CorruptionRule.BloodTide,
            1 => P26CorruptionRule.Greed,
            2 => P26CorruptionRule.Disorder,
            _ => P26CorruptionRule.KingDisaster,
        };
        return (map with { IsCorrupted = true, CorruptionRule = rule }).Validate();
    }

    public static (int Monster, int Item) CorruptionBonus(P1MapItem map)
    {
        int affixMonster = map.EffectiveAffixes.Sum(affix => affix.MonsterQuantityBasisPoints);
        int affixItem = map.EffectiveAffixes.Sum(affix => affix.ItemQuantityBasisPoints);
        return map.CorruptionRule switch
        {
            P26CorruptionRule.BloodTide => (affixMonster + 6_000, affixItem + 6_000),
            P26CorruptionRule.Greed => (affixMonster + 3_000, affixItem + 14_000),
            P26CorruptionRule.Disorder => (affixMonster * 2, affixItem * 2 + 5_000),
            P26CorruptionRule.KingDisaster => (affixMonster + 1_500, affixItem + 12_000),
            _ => (affixMonster, affixItem),
        };
    }

    public static IReadOnlyList<P1MapItem> EnforceInventoryLimit(IEnumerable<P1MapItem> maps, P26MapFilter? autoSellFilter,
        out int goldGained, out IReadOnlyList<string> soldIds)
    {
        var retained = maps.ToList();
        var sold = new List<string>();
        goldGained = 0;
        while (retained.Count > MaximumInventory)
        {
            P1MapItem? victim = retained.Where(map => !map.IsProtected)
                .Where(map => autoSellFilter is null || autoSellFilter.Matches(map))
                .OrderBy(SaleGold).ThenBy(map => map.AcquiredSequence).FirstOrDefault();
            victim ??= retained.Where(map => !map.IsProtected).OrderBy(SaleGold).ThenBy(map => map.AcquiredSequence).FirstOrDefault();
            victim ??= retained[^1];
            retained.Remove(victim);
            sold.Add(victim.InstanceId);
            goldGained = checked(goldGained + SaleGold(victim));
        }
        soldIds = sold;
        return retained;
    }
}

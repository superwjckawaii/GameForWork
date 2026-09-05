using GameForWork.Core.Campaign.World;
using GameForWork.Core.Maps;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Atlas;

public enum MapAffixFamily
{
    DangerousPrefix,
    RewardSuffix,
}

public enum MapAffixGroup
{
    None,
    BossShape,
    MechanicImprint,
}

public enum CorruptionRule
{
    None,
    BloodTide,
    Greed,
    Disorder,
    KingDisaster,
}

public enum MapOrder
{
    Recommended,
    TierAscending,
    OldestFirst,
}

public enum NoMatchBehavior
{
    Wait,
    Stop,
}

public sealed record MapAffixDefinition(
    MapAffixKind Kind,
    string StableId,
    string DisplayName,
    MapAffixFamily Family,
    MapAffixGroup Group,
    IReadOnlyList<int> Values,
    string EffectTemplate);

public static class MapAffixCatalog
{
    private static readonly IReadOnlyList<MapAffixDefinition> Definitions =
    [
        new(MapAffixKind.MonsterLife, "atlas.map.prefix.stubborn", "顽固", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [20, 30, 40, 50], "怪物拥有 {0}% 更多最大生命"),
        new(MapAffixKind.MonsterDamage, "atlas.map.prefix.ferocious", "凶暴", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [22, 32, 43, 54], "怪物造成 {0}% 更多伤害"),
        new(MapAffixKind.MonsterSpeed, "atlas.map.prefix.swift_hunt", "迅猎", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [18, 27, 36, 45], "怪物行动与移动速度提高 {0}%"),
        new(MapAffixKind.PhysicalResistance, "atlas.map.prefix.iron_wall", "铁壁", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [10, 15, 20, 25], "怪物物理伤害减免提高 {0}%"),
        new(MapAffixKind.ElementalShell, "atlas.map.prefix.elemental_shell", "元素甲壳", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [15, 25, 32, 40], "怪物火焰、冰霜、闪电抗性提高 {0}%"),
        new(MapAffixKind.VoidShroud, "atlas.map.prefix.void_shroud", "虚界遮蔽", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [15, 25, 32, 40], "怪物虚空抗性提高 {0}%"),
        new(MapAffixKind.ReducedRecovery, "atlas.map.prefix.exhaustion", "枯竭", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [15, 25, 32, 40], "主角、佣兵和召唤物总回复降低 {0}%（最低保留 20%）"),
        new(MapAffixKind.Penetration, "atlas.map.prefix.piercing", "穿透", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [7, 11, 14, 18], "怪物元素与虚空穿透、物理压制提高 {0}%"),
        new(MapAffixKind.MultipleProjectiles, "atlas.map.prefix.multiple_hunt", "多重猎杀", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [18, 36, 54, 72], "怪物额外发射 2 个投射物，投射物速度提高 {0}%，投射物造成 30% 更少伤害，可重复命中"),
        new(MapAffixKind.AreaDisaster, "atlas.map.prefix.area_disaster", "广域灾变", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [36, 54, 72, 90], "怪物范围提高 {0}%，范围伤害造成 9/18/27/36% 更多伤害"),
        new(MapAffixKind.ElementalPossession, "atlas.map.prefix.elemental_possession", "元素附体", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [18, 27, 36, 45], "怪物随机选择火焰、冰霜、闪电或虚空，额外获得物理伤害的 {0}% 为该伤害"),
        new(MapAffixKind.Stronghold, "atlas.map.prefix.stronghold", "强敌盘踞", MapAffixFamily.DangerousPrefix, MapAffixGroup.None, [30, 45, 60, 75], "稀有怪与 Boss 拥有 {0}% 更多生命，并造成 14/22/29/36% 更多伤害"),

        new(MapAffixKind.MightyPacks, "atlas.map.suffix.mighty_packs", "群雄", MapAffixFamily.RewardSuffix, MapAffixGroup.None, [20, 30, 40, 50], "{0}% 普通怪群升级为魔法怪群，不增加怪物数量"),
        new(MapAffixKind.EliteLeaders, "atlas.map.suffix.elite_leaders", "精锐", MapAffixFamily.RewardSuffix, MapAffixGroup.None, [1, 1, 2, 3], "地图中有 {0} 场遭遇的首领被替换为稀有怪"),
        new(MapAffixKind.RoyalGuard, "atlas.map.suffix.royal_guard", "王庭护卫", MapAffixFamily.RewardSuffix, MapAffixGroup.BossShape, [2, 2, 3, 4], "Boss 由 {0} 名魔法或稀有护卫保护，护卫计入怪物数量"),
        new(MapAffixKind.TwinThrone, "atlas.map.suffix.twin_throne", "双生王座", MapAffixFamily.RewardSuffix, MapAffixGroup.BossShape, [70, 75, 80, 85], "生成 2 个 Boss；单体生命为 {0}%，各自拥有 60% 击杀预算，完成与专属传奇仅结算一次"),
        new(MapAffixKind.SealedVault, "atlas.map.suffix.sealed_vault", "封印宝库", MapAffixFamily.RewardSuffix, MapAffixGroup.None, [1, 1, 2, 3], "生成 {0} 个受保护宝箱，预算为地图基础预算的 8/10/12/15%"),
        new(MapAffixKind.BountifulMark, "atlas.map.suffix.bountiful_mark", "富饶路印", MapAffixFamily.RewardSuffix, MapAffixGroup.None, [20, 30, 45, 60], "非传奇装备稀有度提高 {0}%"),
        new(MapAffixKind.RoadEcho, "atlas.map.suffix.road_echo", "道路回响", MapAffixFamily.RewardSuffix, MapAffixGroup.None, [10, 15, 20, 30], "Boss 额外掉落同阶地图的概率为 {0}%"),
        new(MapAffixKind.AbyssMark, "atlas.map.suffix.abyss_mark", "裂渊印记", MapAffixFamily.RewardSuffix, MapAffixGroup.MechanicImprint, [20, 30, 45, 60], "必定出现深渊，深渊奖励造成 {0}% 更多收益"),
        new(MapAffixKind.GardenMark, "atlas.map.suffix.garden_mark", "命能印记", MapAffixFamily.RewardSuffix, MapAffixGroup.MechanicImprint, [20, 30, 45, 60], "必定出现命能花园，命能获取提高 {0}%"),
        new(MapAffixKind.RedOathMark, "atlas.map.suffix.red_oath_mark", "赤誓印记", MapAffixFamily.RewardSuffix, MapAffixGroup.MechanicImprint, [20, 30, 45, 60], "必定出现赤誓祭坛，祭坛奖励造成 {0}% 更多收益"),
        new(MapAffixKind.BlueOathMark, "atlas.map.suffix.blue_oath_mark", "苍誓印记", MapAffixFamily.RewardSuffix, MapAffixGroup.MechanicImprint, [20, 30, 45, 60], "必定出现苍誓祭坛，延迟奖励造成 {0}% 更多收益"),
        new(MapAffixKind.HeadHunterMark, "atlas.map.suffix.head_hunter_mark", "猎首印记", MapAffixFamily.RewardSuffix, MapAffixGroup.None, [1, 1, 2, 2], "生成 {0} 名游荡稀有怪，并保证掉落更高物品等级的底材"),
    ];

    private static readonly IReadOnlyDictionary<MapAffixKind, MapAffixDefinition> ByKind =
        Definitions.ToDictionary(definition => definition.Kind);

    public static IReadOnlyList<MapAffixDefinition> All => Definitions;
    public static IReadOnlyList<MapAffixDefinition> Prefixes => Definitions.Where(item => item.Family == MapAffixFamily.DangerousPrefix).ToArray();
    public static IReadOnlyList<MapAffixDefinition> Suffixes => Definitions.Where(item => item.Family == MapAffixFamily.RewardSuffix).ToArray();
    public static MapAffixDefinition Get(MapAffixKind kind) => ByKind.TryGetValue(kind, out MapAffixDefinition? value)
        ? value : throw new KeyNotFoundException($"Unknown Atlas map affix: {kind}");

    public static int RankForTier(int tier) => tier switch
    {
        <= 5 => 1,
        <= 10 => 2,
        <= 16 => 3,
        _ => 4,
    };

    public static (int Monster, int Item) QuantityFor(MapAffixFamily family, int rank) => family switch
    {
        MapAffixFamily.DangerousPrefix => rank switch { 1 => (600, 800), 2 => (900, 1_200), 3 => (1_200, 1_600), _ => (1_500, 2_000) },
        _ => rank switch { 1 => (600, 1_000), 2 => (900, 1_500), 3 => (1_200, 2_000), _ => (1_500, 2_500) },
    };

    public static MapAffix Create(MapAffixDefinition definition, int rank)
    {
        rank = Math.Clamp(rank, 1, 4);
        (int monster, int item) = QuantityFor(definition.Family, rank);
        return new MapAffix(definition.Kind, definition.DisplayName, definition.Values[rank - 1],
            definition.Family, rank, monster, item);
    }
}

public sealed record MapFilter(
    int MinimumTier = MapItem.MinimumTier,
    int MaximumTier = MapItem.MaximumTier,
    int MinimumItemQuantityBasisPoints = 0,
    int MaximumItemQuantityBasisPoints = 25_000,
    int MinimumMonsterQuantityBasisPoints = 0,
    int MaximumMonsterQuantityBasisPoints = 12_000,
    IReadOnlyList<string>? AreaIds = null,
    IReadOnlyList<MapRarity>? Rarities = null,
    bool IncludeUncorrupted = true,
    bool IncludeCorrupted = true,
    int MinimumQuality = 0,
    IReadOnlyList<MapAffixKind>? RequiredAffixes = null,
    IReadOnlyList<MapAffixKind>? ExcludedAffixes = null)
{
    public static MapFilter All { get; } = new();

    public MapFilter Validate()
    {
        if (MinimumTier is < 1 or > 20 || MaximumTier is < 1 or > 20 || MinimumTier > MaximumTier ||
            MinimumQuality is < 0 or > 20 || MinimumItemQuantityBasisPoints < 0 ||
            MinimumMonsterQuantityBasisPoints < 0 || MaximumItemQuantityBasisPoints < MinimumItemQuantityBasisPoints ||
            MaximumMonsterQuantityBasisPoints < MinimumMonsterQuantityBasisPoints || (!IncludeCorrupted && !IncludeUncorrupted) ||
            (AreaIds?.Any(id => !MapCatalog.TryGet(id, out _)) ?? false) ||
            (Rarities?.Any(rarity => !Enum.IsDefined(rarity)) ?? false))
            throw new ArgumentOutOfRangeException(nameof(MinimumTier), "Atlas map filter is invalid.");
        return this;
    }

    public bool Matches(MapItem map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Tier < MinimumTier || map.Tier > MaximumTier || map.Quality < MinimumQuality ||
            map.ItemQuantityBonusBasisPoints < MinimumItemQuantityBasisPoints || map.ItemQuantityBonusBasisPoints > MaximumItemQuantityBasisPoints ||
            map.MonsterQuantityBasisPoints < MinimumMonsterQuantityBasisPoints || map.MonsterQuantityBasisPoints > MaximumMonsterQuantityBasisPoints ||
            map.IsCorrupted && !IncludeCorrupted || !map.IsCorrupted && !IncludeUncorrupted ||
            AreaIds is { Count: > 0 } && !AreaIds.Contains(map.AreaId, StringComparer.Ordinal) ||
            Rarities is { Count: > 0 } && !Rarities.Contains(map.Rarity)) return false;
        IReadOnlySet<MapAffixKind> kinds = map.EffectiveAffixes.Select(affix => affix.Kind).ToHashSet();
        if (RequiredAffixes is { Count: > 0 } && RequiredAffixes.Any(required => !kinds.Contains(required))) return false;
        return ExcludedAffixes is not { Count: > 0 } || ExcludedAffixes.All(excluded => !kinds.Contains(excluded));
    }

    public IReadOnlyList<MapItem> Select(IEnumerable<MapItem> maps, MapOrder order = MapOrder.Recommended)
    {
        IEnumerable<MapItem> selected = maps.Where(Matches);
        return order switch
        {
            MapOrder.TierAscending => selected.OrderBy(map => map.Tier).ThenBy(map => map.AcquiredSequence).ToArray(),
            MapOrder.OldestFirst => selected.OrderBy(map => map.AcquiredSequence).ThenByDescending(map => map.Tier).ToArray(),
            _ => selected.OrderByDescending(map => map.Tier)
                .ThenByDescending(map => map.ItemQuantityBonusBasisPoints)
                .ThenByDescending(map => map.MonsterQuantityBasisPoints)
                .ThenBy(map => map.AcquiredSequence)
                .ThenBy(map => map.InstanceId, StringComparer.Ordinal).ToArray(),
        };
    }
}

public static class MapGenerationRules
{
    public const int MaximumInventory = 2_000;
    public const int RareMonsterQuantityCap = 6_000;
    public const int RareItemQuantityCap = 11_000;
    public const int CorruptedMonsterQuantityCap = 12_000;
    public const int CorruptedItemQuantityCap = 25_000;
    public const int CorruptionDestroyBasisPoints = 1_000;

    public static IReadOnlyList<MapAffix> RollAffixes(MapRarity rarity, int tier, ulong seed)
    {
        if (rarity == MapRarity.Basic) return [];
        var random = new Pcg32(seed);
        int rank = MapAffixCatalog.RankForTier(tier);
        int perFamily = rarity == MapRarity.Magic ? 1 : 2;
        MapAffixDefinition[] prefixes = MapAffixCatalog.Prefixes.OrderBy(_ => random.NextUInt()).Take(perFamily).ToArray();
        var selected = new List<MapAffixDefinition>(perFamily * 2);
        selected.AddRange(prefixes);
        foreach (MapAffixDefinition suffix in MapAffixCatalog.Suffixes.OrderBy(_ => random.NextUInt()))
        {
            if (selected.Count(item => item.Family == MapAffixFamily.RewardSuffix) >= perFamily) break;
            if (suffix.Group != MapAffixGroup.None && selected.Any(item => item.Group == suffix.Group)) continue;
            selected.Add(suffix);
        }
        return selected.Select(definition => MapAffixCatalog.Create(definition, rank)).ToArray();
    }

    public static IReadOnlyList<MapAffix> AddExaltedAffix(MapItem map, ulong seed)
    {
        if (map.Rarity != MapRarity.Rare || map.EffectiveAffixes.Count >= 6)
            throw new InvalidOperationException("A rare map with an open affix slot is required.");
        var random = new Pcg32(seed);
        int prefixes = map.EffectiveAffixes.Count(affix => affix.Family == MapAffixFamily.DangerousPrefix);
        int suffixes = map.EffectiveAffixes.Count(affix => affix.Family == MapAffixFamily.RewardSuffix);
        MapAffixDefinition[] candidates = MapAffixCatalog.All
            .Where(definition => !map.EffectiveAffixes.Any(affix => affix.Kind == definition.Kind))
            .Where(definition => definition.Family == MapAffixFamily.DangerousPrefix ? prefixes < 3 : suffixes < 3)
            .Where(definition => definition.Group == MapAffixGroup.None ||
                !map.EffectiveAffixes.Any(affix => MapAffixCatalog.Get(affix.Kind).Group == definition.Group))
            .OrderBy(_ => random.NextUInt()).ToArray();
        if (candidates.Length == 0) throw new InvalidOperationException("No compatible map affix remains.");
        return map.EffectiveAffixes.Append(MapAffixCatalog.Create(candidates[0], MapAffixCatalog.RankForTier(map.Tier))).ToArray();
    }

    public static MapItem NormalizeLegacy(MapItem map, ulong seed)
    {
        MapRarity rarity = map.IsCorrupted ? MapRarity.Rare : map.Rarity;
        CorruptionRule corruption = map.IsCorrupted && map.CorruptionRule == CorruptionRule.None
            ? (CorruptionRule)(1 + seed % 4) : map.CorruptionRule;
        if (rarity == MapRarity.Basic) return map with { Affixes = null, CorruptionRule = CorruptionRule.None };
        int prefixes = map.EffectiveAffixes.Count(affix => affix.Family == MapAffixFamily.DangerousPrefix);
        int suffixes = map.EffectiveAffixes.Count(affix => affix.Family == MapAffixFamily.RewardSuffix);
        bool formal = rarity == MapRarity.Magic
            ? map.EffectiveAffixes.Count == 2 && prefixes == 1 && suffixes == 1
            : map.EffectiveAffixes.Count is >= 4 and <= 6 && prefixes is >= 2 and <= 3 && suffixes is >= 2 and <= 3;
        return map with { Rarity = rarity, CorruptionRule = corruption,
            Affixes = formal ? map.EffectiveAffixes : RollAffixes(rarity, map.Tier, seed) };
    }

    public static int SaleGold(MapItem map)
    {
        int rarity = map.Rarity switch { MapRarity.Magic => 2, MapRarity.Rare => 5, _ => 0 };
        return Math.Max(1, 2 + map.Tier + rarity + map.Quality / 10 + map.MonsterQuantityBasisPoints / 3_000 +
            map.ItemQuantityBonusBasisPoints / 5_000 + (map.IsCorrupted ? 3 : 0));
    }

    public static MapItem? Corrupt(MapItem map, ulong seed, out bool destroyed)
    {
        if (map.Rarity != MapRarity.Rare || map.IsCorrupted)
            throw new InvalidOperationException("Only an uncorrupted rare map can be corrupted.");
        int roll = new Pcg32(seed).NextBasisPoints();
        if (roll < CorruptionDestroyBasisPoints)
        {
            destroyed = true;
            return null;
        }
        destroyed = false;
        int outcome = (roll - CorruptionDestroyBasisPoints) / 2_250;
        CorruptionRule rule = outcome switch
        {
            0 => CorruptionRule.BloodTide,
            1 => CorruptionRule.Greed,
            2 => CorruptionRule.Disorder,
            _ => CorruptionRule.KingDisaster,
        };
        return (map with { IsCorrupted = true, CorruptionRule = rule }).Validate();
    }

    public static (int Monster, int Item) CorruptionBonus(MapItem map)
    {
        int affixMonster = map.EffectiveAffixes.Sum(affix => affix.MonsterQuantityBasisPoints);
        int affixItem = map.EffectiveAffixes.Sum(affix => affix.ItemQuantityBasisPoints);
        return map.CorruptionRule switch
        {
            CorruptionRule.BloodTide => (affixMonster + 6_000, affixItem + 6_000),
            CorruptionRule.Greed => (affixMonster + 3_000, affixItem + 14_000),
            CorruptionRule.Disorder => (affixMonster * 2, affixItem * 2 + 5_000),
            CorruptionRule.KingDisaster => (affixMonster + 1_500, affixItem + 12_000),
            _ => (affixMonster, affixItem),
        };
    }

    public static IReadOnlyList<MapItem> EnforceInventoryLimit(IEnumerable<MapItem> maps, MapFilter? autoSellFilter,
        out int goldGained, out IReadOnlyList<string> soldIds)
    {
        var retained = maps.ToList();
        var sold = new List<string>();
        goldGained = 0;
        while (retained.Count > MaximumInventory)
        {
            MapItem? victim = retained.Where(map => !map.IsProtected)
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

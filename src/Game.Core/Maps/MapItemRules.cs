using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;
using GameForWork.Core.Atlas;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Maps;

public enum MapRarity { Basic, Magic, Rare }
public enum MapAltar { None, RedOath, BlueOath }
public enum MapAffixKind
{
    MonsterLife,
    MonsterDamage,
    MonsterSpeed,
    ReducedRecovery,
    PhysicalResistance,
    ElementalShell,
    VoidShroud,
    Penetration,
    MultipleProjectiles,
    AreaDisaster,
    ElementalPossession,
    Stronghold,
    MightyPacks,
    EliteLeaders,
    RoyalGuard,
    TwinThrone,
    SealedVault,
    BountifulMark,
    RoadEcho,
    AbyssMark,
    GardenMark,
    RedOathMark,
    BlueOathMark,
    HeadHunterMark,
}

public sealed record MapAffix(
    MapAffixKind Kind,
    string DisplayName,
    int Value,
    MapAffixFamily Family = MapAffixFamily.DangerousPrefix,
    int Rank = 1,
    int MonsterQuantityBasisPoints = 0,
    int ItemQuantityBasisPoints = 0);
public sealed record MapArea(string StableId, string DisplayName, string Environment, string MonsterPool, string BossName);
public sealed record MapCombatModifiers(
    int EnemyLifeBasisPoints = 10_000,
    int EnemyDamageBasisPoints = 10_000,
    int EnemySpeedBasisPoints = 10_000,
    int PlayerRecoveryBasisPoints = 10_000,
    int MonsterQuantityBasisPoints = 10_000,
    bool ExtraElites = false,
    int BossLifeBasisPoints = 10_000,
    int BossDamageBasisPoints = 10_000,
    int EnemyPhysicalReductionBasisPoints = 0,
    int EnemyElementalResistanceBasisPoints = 0,
    int EnemyVoidResistanceBasisPoints = 0,
    int EnemyPenetrationBasisPoints = 0,
    int ExtraProjectiles = 0,
    int ProjectileDamageBasisPoints = 10_000,
    int EnemyAreaBasisPoints = 10_000,
    int EnemyAreaDamageBasisPoints = 10_000,
    int BossCount = 1,
    int BossAdditionalGuards = 0,
    int AdditionalRareEnemies = 0)
{
    public static MapCombatModifiers From(MapItem map)
    {
        int Value(MapAffixKind kind)
        {
            int value = map.EffectiveAffixes.Where(affix => affix.Kind == kind).Sum(affix => affix.Value);
            return map.CorruptionRule == CorruptionRule.Disorder ? value * 3 / 2 : value;
        }
        int tierLife = map.Tier == 20 ? 2_500 : 0;
        int tierDamage = map.Tier is 17 or 20 ? 1_500 : 0;
        int tierSpeed = map.Tier == 20 ? 1_000 : 0;
        int tierRecovery = map.Tier == 18 ? 3_000 : 0;
        int life = 10_000 + tierLife;
        int damage = 10_000 + tierDamage;
        life = life * (10_000 + Value(MapAffixKind.MonsterLife) * 100) / 10_000;
        damage = damage * (10_000 + Value(MapAffixKind.MonsterDamage) * 100) / 10_000;
        damage = damage * (10_000 + Value(MapAffixKind.ElementalPossession) * 100) / 10_000;
        int bossLife = 10_000 + Value(MapAffixKind.Stronghold) * 100;
        int strongholdRank = map.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == MapAffixKind.Stronghold)?.Rank ?? 0;
        int bossDamage = 10_000 + (strongholdRank switch { 1 => 1_400, 2 => 2_200, 3 => 2_900, 4 => 3_600, _ => 0 });
        switch (map.CorruptionRule)
        {
            case CorruptionRule.BloodTide: damage = damage * 13_600 / 10_000; break;
            case CorruptionRule.Greed: life = life * 17_500 / 10_000; damage = damage * 13_600 / 10_000; break;
            case CorruptionRule.KingDisaster: bossLife = bossLife * 25_000 / 10_000; bossDamage = bossDamage * 16_500 / 10_000; break;
        }
        int areaRank = map.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == MapAffixKind.AreaDisaster)?.Rank ?? 0;
        int areaDamage = areaRank switch { 1 => 10_900, 2 => 11_800, 3 => 12_700, 4 => 13_600, _ => 10_000 };
        int area = 10_000 + Value(MapAffixKind.AreaDisaster) * 100;
        if (map.CorruptionRule == CorruptionRule.KingDisaster) area = area * 18_000 / 10_000;
        MapAffix? twin = map.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == MapAffixKind.TwinThrone);
        if (twin is not null)
        {
            bossLife = bossLife * twin.Value * 100 / 10_000;
            bossDamage = bossDamage * (twin.Rank switch { 1 => 9_000, 2 => 9_500, 3 => 10_000, _ => 10_500 }) / 10_000;
        }
        int guards = map.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == MapAffixKind.RoyalGuard)?.Value ?? 0;
        int rareEnemies = map.EffectiveAffixes.Where(affix => affix.Kind is MapAffixKind.EliteLeaders or MapAffixKind.HeadHunterMark)
            .Sum(affix => affix.Value);
        return new(
            life,
            damage,
            10_000 + tierSpeed + Value(MapAffixKind.MonsterSpeed) * 100,
            Math.Max(2_000, 10_000 - tierRecovery - Value(MapAffixKind.ReducedRecovery) * 100),
            10_000 + map.MonsterQuantityBasisPoints,
            map.Tier == 19 || map.EffectiveAffixes.Any(affix => affix.Kind is MapAffixKind.MightyPacks or MapAffixKind.EliteLeaders),
            bossLife,
            bossDamage,
            Value(MapAffixKind.PhysicalResistance) * 100,
            Value(MapAffixKind.ElementalShell) * 100,
            Value(MapAffixKind.VoidShroud) * 100,
            Value(MapAffixKind.Penetration) * 100,
            map.EffectiveAffixes.Any(affix => affix.Kind == MapAffixKind.MultipleProjectiles) ? 2 : 0,
            map.EffectiveAffixes.Any(affix => affix.Kind == MapAffixKind.MultipleProjectiles) ? 7_000 : 10_000,
            area,
            areaDamage,
            twin is null ? 1 : 2,
            guards,
            rareEnemies);
    }
}

public static class MapCatalog
{
    public static IReadOnlyList<MapArea> Areas { get; } =
    [
        new("core.map.cinder_road", "烬灰古道", "焦土荒原", "灰狼、烬骨弓手", "灼痕督军"),
        new("core.map.sunken_crypt", "沉水墓窟", "潮湿墓室", "溺尸、墓穴甲虫", "沉棺祭司"),
        new("core.map.iron_orchard", "铁枝果园", "锈蚀林地", "棘兽、铁皮树妖", "绞枝母体"),
        new("core.map.broken_bastion", "断垣堡垒", "战争废墟", "失誓士兵、弩手", "无旗将军"),
        new("core.map.blood_marsh", "血苔沼泽", "毒沼", "泥沼兽、血蛭", "苔冠巨兽"),
        new("core.map.glass_mine", "琉璃矿坑", "晶矿深层", "晶壳虫、矿奴", "碎光监工"),
        new("core.map.hollow_cloister", "空响回廊", "废弃修院", "赎罪者、钟灵", "默祷院长"),
        new("core.map.black_tide_port", "黑潮港", "风暴码头", "潮盗、盐尸", "黑帆船长"),
        new("core.map.ashen_garden", "灰烬庭园", "焚毁园林", "烟羽鸦、根须魔", "枯荣园丁"),
        new("core.map.withered_observatory", "凋星观测台", "破败高塔", "星骸、秘仪守卫", "盲眼占星师"),
        new("core.map.furnace_depths", "熔炉深渊", "地下熔炉", "炉渣魔、链锤工", "赤炉之心"),
        new("core.map.oathbreaker_throne", "背誓王座", "黑石宫殿", "誓卫、黯影骑士", "末代誓王"),
    ];

    public static MapArea Get(string id) => Areas.First(area => area.StableId == id);

    public static bool TryGet(string? id, out MapArea area)
    {
        MapArea? found = Areas.FirstOrDefault(candidate => candidate.StableId == id);
        area = found!;
        return found is not null;
    }
}

public static class MapItemRules
{
    private static readonly MapRoute[] InitialRoutes = [MapRoute.Safe, MapRoute.Abyss, MapRoute.LifeGarden];
    private static readonly IReadOnlyDictionary<int, MapRoute[]> CanonicalRouteSets = Enumerable.Range(1, 7)
        .ToDictionary(mask => mask, mask => InitialRoutes.Where(route => (mask & (1 << (int)route)) != 0).ToArray());
    public static MapItem EnsureFormal(MapItem map, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (MapCatalog.TryGet(map.AreaId, out _) && map.EffectiveRouteCandidates.Count > 0)
            return ApplyImprints(MapGenerationRules.NormalizeLegacy(map, seed)).Validate();
        ulong stableSeed = StableSeed(seed, map.InstanceId, map.Tier);
        var random = new Pcg32(stableSeed);
        MapArea area = MapCatalog.Areas[(int)(random.NextUInt() % (uint)MapCatalog.Areas.Count)];
        MapRoute[] routes = InitialRoutes;
        int candidateCount = 1 + (int)(random.NextUInt() % 3);
        MapRoute[] rolled = routes.OrderBy(_ => random.NextUInt()).Take(candidateCount).ToArray();
        int mask = rolled.Aggregate(0, (value, route) => value | 1 << (int)route);
        MapRoute[] candidates = CanonicalRouteSets[mask];
        MapAltar altar = random.NextBasisPoints() switch
        {
            < 1_500 => MapAltar.RedOath,
            < 3_000 => MapAltar.BlueOath,
            _ => MapAltar.None,
        };
        return ApplyImprints(MapGenerationRules.NormalizeLegacy(map with { AreaId = area.StableId, RouteCandidates = candidates, Altar = altar }, stableSeed)).Validate();
    }

    private static MapItem ApplyImprints(MapItem map)
    {
        bool abyss = map.EffectiveAffixes.Any(affix => affix.Kind == MapAffixKind.AbyssMark);
        bool garden = map.EffectiveAffixes.Any(affix => affix.Kind == MapAffixKind.GardenMark);
        bool red = map.EffectiveAffixes.Any(affix => affix.Kind == MapAffixKind.RedOathMark);
        bool blue = map.EffectiveAffixes.Any(affix => affix.Kind == MapAffixKind.BlueOathMark);
        MapAltar desiredAltar = red ? MapAltar.RedOath : blue ? MapAltar.BlueOath : map.Altar;
        bool routesChanged = abyss && !map.EffectiveRouteCandidates.Contains(MapRoute.Abyss) ||
            garden && !map.EffectiveRouteCandidates.Contains(MapRoute.LifeGarden);
        if (!routesChanged && desiredAltar == map.Altar) return map;
        var routes = map.EffectiveRouteCandidates.ToList();
        if (abyss && !routes.Contains(MapRoute.Abyss))
            routes = routes.Append(MapRoute.Abyss).Distinct().TakeLast(3).ToList();
        if (garden && !routes.Contains(MapRoute.LifeGarden))
            routes = routes.Append(MapRoute.LifeGarden).Distinct().TakeLast(3).ToList();
        return map with { RouteCandidates = routesChanged ? routes : map.RouteCandidates, Altar = desiredAltar };
    }

    public static IReadOnlyList<MapAffix> RollAffixes(MapRarity rarity, ulong seed)
        => MapGenerationRules.RollAffixes(rarity, 1, seed);

    public static IReadOnlyList<MapAffix> RollAffixes(MapRarity rarity, int tier, ulong seed)
        => MapGenerationRules.RollAffixes(rarity, tier, seed);

    private static ulong StableSeed(ulong seed, string id, int tier)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}|{id}|{tier}|maps"));
        return BitConverter.ToUInt64(hash, 0);
    }
}

public enum MapCraftOperation { PolishQuality, AwakenMagic, AlchemicalRare, ChaosReroll, ExaltedAdd, Corrupt }
public enum BatchFailureBehavior { Keep, Sell }
public sealed record MapCraftResult(bool Succeeded, MapItem? Map, MetalCurrencyKind Currency, int Cost, string Summary,
    bool Destroyed = false);

public static class MapCrafting
{
    public static (MetalCurrencyKind Currency, int Cost) Cost(MapCraftOperation operation) => operation switch
    {
        MapCraftOperation.PolishQuality => (MetalCurrencyKind.PolishingCobalt, 1),
        MapCraftOperation.AwakenMagic => (MetalCurrencyKind.AwakeningCopper, 1),
        MapCraftOperation.AlchemicalRare => (MetalCurrencyKind.AlchemicalGold, 1),
        MapCraftOperation.ChaosReroll => (MetalCurrencyKind.ChaosGold, 1),
        MapCraftOperation.ExaltedAdd => (MetalCurrencyKind.ExaltedGold, 1),
        MapCraftOperation.Corrupt => (MetalCurrencyKind.CorruptionIron, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    public static MapCraftResult Apply(TownEconomyState economy, MapItem source, MapCraftOperation operation,
        ulong seed, int maximumUnlockedTier = MapItem.MaximumTier, int polishQualityGain = 5)
    {
        ArgumentNullException.ThrowIfNull(economy);
        MapItem map = source.EnsureFormal(seed);
        (MetalCurrencyKind currency, int cost) = Cost(operation);
        string? invalid = operation switch
        {
            MapCraftOperation.PolishQuality when map.Quality >= 20 => "quality_maximum",
            MapCraftOperation.AwakenMagic when map.Rarity != MapRarity.Basic => "basic_required",
            MapCraftOperation.AlchemicalRare when map.Rarity == MapRarity.Rare => "not_rare_required",
            MapCraftOperation.ChaosReroll when map.Rarity != MapRarity.Rare => "rare_required",
            MapCraftOperation.ExaltedAdd when map.Rarity != MapRarity.Rare => "rare_required",
            MapCraftOperation.ExaltedAdd when map.EffectiveAffixes.Count >= 6 => "affixes_full",
            MapCraftOperation.Corrupt when map.Rarity != MapRarity.Rare => "rare_required",
            MapCraftOperation.Corrupt when map.IsCorrupted => "already_corrupted",
            _ when map.IsCorrupted => "map_corrupted",
            _ => null,
        };
        if (invalid is not null) return new(false, map, currency, 0, invalid);
        if (!economy.TrySpendMetal(currency, cost)) return new(false, map, currency, cost, "metal_insufficient");

        map = operation switch
        {
            MapCraftOperation.PolishQuality => map with { Quality = Math.Min(20, map.Quality + Math.Clamp(polishQualityGain, 1, 20)) },
            MapCraftOperation.AwakenMagic => map with { Rarity = MapRarity.Magic, Affixes = MapItemRules.RollAffixes(MapRarity.Magic, map.Tier, seed) },
            MapCraftOperation.AlchemicalRare => map with { Rarity = MapRarity.Rare, Affixes = MapItemRules.RollAffixes(MapRarity.Rare, map.Tier, seed) },
            MapCraftOperation.ChaosReroll => map with { Affixes = MapItemRules.RollAffixes(MapRarity.Rare, map.Tier, seed) },
            MapCraftOperation.ExaltedAdd => map with { Affixes = MapGenerationRules.AddExaltedAffix(map, seed) },
            _ => map,
        };
        if (operation == MapCraftOperation.Corrupt)
        {
            MapItem? corrupted = MapGenerationRules.Corrupt(map, seed, out bool destroyed);
            return new(true, corrupted, currency, cost, destroyed ? "corruption_destroyed" : $"corruption_{corrupted!.CorruptionRule}", destroyed);
        }
        return new(true, map.Validate(), currency, cost, operation.ToString());
    }
}

public sealed record MapBatchRule(
    MapRarity TargetRarity = MapRarity.Rare,
    int MinimumQuality = 20,
    IReadOnlyList<MapAffixKind>? ExcludedAffixes = null,
    bool FillAffixes = false,
    bool Corrupt = false,
    BatchFailureBehavior ExcludedAffixBehavior = BatchFailureBehavior.Keep)
{
    public MapBatchRule Validate()
    {
        if (MinimumQuality is < 0 or > 20 || !Enum.IsDefined(TargetRarity) || !Enum.IsDefined(ExcludedAffixBehavior))
            throw new ArgumentOutOfRangeException(nameof(MinimumQuality));
        return this;
    }
}

public sealed record MapBatchResult(int Processed, int Completed, int Skipped, int MetalsSpent, bool Stopped, string Summary);

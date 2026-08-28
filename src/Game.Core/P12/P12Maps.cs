using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.World;
using GameForWork.Core.P4;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P12;

public enum P12MapRarity { Basic, Magic, Rare }
public enum P12MapAltar { None, RedOath, BlueOath }
public enum P12MapAffixKind
{
    MonsterLife,
    MonsterDamage,
    MonsterSpeed,
    ReducedRecovery,
    ExtraElites,
    PhysicalResistance,
    ElementalPressure,
    IncreasedPackSize,
}

public sealed record P12MapAffix(P12MapAffixKind Kind, string DisplayName, int Value, int Danger, int QuantityBasisPoints);
public sealed record P12MapArea(string StableId, string DisplayName, string Environment, string MonsterPool, string BossName);
public sealed record P12MapCombatModifiers(
    int EnemyLifeBasisPoints = 10_000,
    int EnemyDamageBasisPoints = 10_000,
    int EnemySpeedBasisPoints = 10_000,
    int PlayerRecoveryBasisPoints = 10_000,
    int PackSizeBasisPoints = 10_000,
    bool ExtraElites = false)
{
    public static P12MapCombatModifiers From(P1MapItem map)
    {
        int Value(P12MapAffixKind kind) => map.EffectiveAffixes.Where(affix => affix.Kind == kind).Sum(affix => affix.Value);
        int tierLife = map.AreaLevel == 20 ? 2_500 : 0;
        int tierDamage = map.AreaLevel is 17 or 20 ? 1_500 : 0;
        int tierSpeed = map.AreaLevel == 20 ? 1_000 : 0;
        int tierRecovery = map.AreaLevel == 18 ? 3_000 : 0;
        return new(
            10_000 + tierLife + (Value(P12MapAffixKind.MonsterLife) + Value(P12MapAffixKind.PhysicalResistance)) * 100,
            10_000 + tierDamage + (Value(P12MapAffixKind.MonsterDamage) + Value(P12MapAffixKind.ElementalPressure) / 2) * 100,
            10_000 + tierSpeed + Value(P12MapAffixKind.MonsterSpeed) * 100,
            Math.Max(2_000, 10_000 - tierRecovery - Value(P12MapAffixKind.ReducedRecovery) * 100),
            10_000 + Value(P12MapAffixKind.IncreasedPackSize) * 100,
            map.AreaLevel == 19 || map.EffectiveAffixes.Any(affix => affix.Kind == P12MapAffixKind.ExtraElites));
    }
}

public static class P12MapCatalog
{
    public static IReadOnlyList<P12MapArea> Areas { get; } =
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

    public static P12MapArea Get(string id) => Areas.First(area => area.StableId == id);

    public static bool TryGet(string? id, out P12MapArea area)
    {
        P12MapArea? found = Areas.FirstOrDefault(candidate => candidate.StableId == id);
        area = found!;
        return found is not null;
    }
}

public static class P12MapRules
{
    private static readonly IReadOnlyDictionary<int, MapRoute[]> CanonicalRouteSets = Enumerable.Range(1, 7)
        .ToDictionary(mask => mask, mask => Enum.GetValues<MapRoute>().Where(route => (mask & (1 << (int)route)) != 0).ToArray());
    private static readonly (P12MapAffixKind Kind, string Name, int Min, int Max, int Danger, int Quantity)[] AffixPool =
    [
        (P12MapAffixKind.MonsterLife, "顽固", 20, 55, 8, 700),
        (P12MapAffixKind.MonsterDamage, "凶暴", 15, 45, 12, 950),
        (P12MapAffixKind.MonsterSpeed, "迅猎", 10, 35, 10, 800),
        (P12MapAffixKind.ReducedRecovery, "枯竭", 15, 50, 15, 1_100),
        (P12MapAffixKind.ExtraElites, "群雄", 1, 3, 9, 850),
        (P12MapAffixKind.PhysicalResistance, "铁壁", 10, 35, 9, 750),
        (P12MapAffixKind.ElementalPressure, "灾焰", 10, 40, 11, 900),
        (P12MapAffixKind.IncreasedPackSize, "密集", 8, 25, 6, 1_200),
    ];

    public static int RouteDanger(MapRoute route) => route switch
    {
        MapRoute.Safe => 0,
        MapRoute.LifeGarden => 8,
        MapRoute.Abyss => 14,
        _ => 0,
    };

    public static P1MapItem EnsureFormal(P1MapItem map, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (P12MapCatalog.TryGet(map.AreaId, out _) && map.EffectiveRouteCandidates.Count > 0)
            return map.Validate();
        ulong stableSeed = StableSeed(seed, map.InstanceId, map.AreaLevel);
        var random = new Pcg32(stableSeed);
        P12MapArea area = P12MapCatalog.Areas[(int)(random.NextUInt() % (uint)P12MapCatalog.Areas.Count)];
        MapRoute[] routes = Enum.GetValues<MapRoute>();
        int candidateCount = 1 + (int)(random.NextUInt() % 3);
        MapRoute[] rolled = routes.OrderBy(_ => random.NextUInt()).Take(candidateCount).ToArray();
        int mask = rolled.Aggregate(0, (value, route) => value | 1 << (int)route);
        MapRoute[] candidates = CanonicalRouteSets[mask];
        P12MapAltar altar = random.NextBasisPoints() switch
        {
            < 1_500 => P12MapAltar.RedOath,
            < 3_000 => P12MapAltar.BlueOath,
            _ => P12MapAltar.None,
        };
        return (map with { AreaId = area.StableId, RouteCandidates = candidates, Altar = altar }).Validate();
    }

    public static IReadOnlyList<P12MapAffix> RollAffixes(P12MapRarity rarity, ulong seed)
    {
        int count = rarity switch { P12MapRarity.Basic => 0, P12MapRarity.Magic => 2, _ => 4 + (int)(seed % 3) };
        var random = new Pcg32(seed);
        return AffixPool.OrderBy(_ => random.NextUInt()).Take(count).Select(template =>
        {
            int value = template.Min + (int)(random.NextUInt() % (uint)(template.Max - template.Min + 1));
            return new P12MapAffix(template.Kind, template.Name, value, template.Danger, template.Quantity);
        }).ToArray();
    }

    private static ulong StableSeed(ulong seed, string id, int tier)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}|{id}|{tier}|p12"));
        return BitConverter.ToUInt64(hash, 0);
    }
}

public enum P12MapCraftOperation { PolishQuality, AwakenMagic, AlchemicalRare, ChaosReroll, Corrupt }
public enum P12BatchFailureBehavior { Keep, Skip, Stop }
public sealed record P12MapCraftResult(bool Succeeded, P1MapItem Map, MetalCurrencyKind Currency, int Cost, string Summary);

public static class P12MapCrafting
{
    public static (MetalCurrencyKind Currency, int Cost) Cost(P12MapCraftOperation operation) => operation switch
    {
        P12MapCraftOperation.PolishQuality => (MetalCurrencyKind.PolishingCobalt, 1),
        P12MapCraftOperation.AwakenMagic => (MetalCurrencyKind.AwakeningCopper, 1),
        P12MapCraftOperation.AlchemicalRare => (MetalCurrencyKind.AlchemicalGold, 1),
        P12MapCraftOperation.ChaosReroll => (MetalCurrencyKind.ChaosGold, 1),
        P12MapCraftOperation.Corrupt => (MetalCurrencyKind.CorruptionIron, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    public static P12MapCraftResult Apply(TownEconomyState economy, P1MapItem source, P12MapCraftOperation operation,
        ulong seed, int maximumUnlockedTier = P1MapItem.MaximumAreaLevel)
    {
        ArgumentNullException.ThrowIfNull(economy);
        P1MapItem map = source.EnsureFormal(seed);
        (MetalCurrencyKind currency, int cost) = Cost(operation);
        string? invalid = operation switch
        {
            P12MapCraftOperation.PolishQuality when map.Quality >= 20 => "quality_maximum",
            P12MapCraftOperation.AwakenMagic when map.Rarity != P12MapRarity.Basic => "basic_required",
            P12MapCraftOperation.AlchemicalRare when map.Rarity != P12MapRarity.Basic => "basic_required",
            P12MapCraftOperation.ChaosReroll when map.Rarity != P12MapRarity.Rare => "rare_required",
            P12MapCraftOperation.Corrupt when map.IsCorrupted => "already_corrupted",
            _ when map.IsCorrupted => "map_corrupted",
            _ => null,
        };
        if (invalid is not null) return new(false, map, currency, 0, invalid);
        if (!economy.TrySpendMetal(currency, cost)) return new(false, map, currency, cost, "metal_insufficient");

        map = operation switch
        {
            P12MapCraftOperation.PolishQuality => map with { Quality = Math.Min(20, map.Quality + 5) },
            P12MapCraftOperation.AwakenMagic => map with { Rarity = P12MapRarity.Magic, Affixes = P12MapRules.RollAffixes(P12MapRarity.Magic, seed) },
            P12MapCraftOperation.AlchemicalRare => map with { Rarity = P12MapRarity.Rare, Affixes = P12MapRules.RollAffixes(P12MapRarity.Rare, seed) },
            P12MapCraftOperation.ChaosReroll => map with { Affixes = P12MapRules.RollAffixes(P12MapRarity.Rare, seed) },
            P12MapCraftOperation.Corrupt => Corrupt(map, seed, maximumUnlockedTier),
            _ => map,
        };
        return new(true, map.Validate(), currency, cost, operation.ToString());
    }

    private static P1MapItem Corrupt(P1MapItem map, ulong seed, int maximumUnlockedTier)
    {
        int outcome = (int)(seed % 4);
        return outcome switch
        {
            0 => map with { IsCorrupted = true, Quality = 20 },
            1 => map with { IsCorrupted = true, Rarity = P12MapRarity.Rare, Affixes = P12MapRules.RollAffixes(P12MapRarity.Rare, seed ^ 0xc0ffeeUL) },
            2 => map with { IsCorrupted = true, AreaLevel = Math.Min(maximumUnlockedTier, map.AreaLevel + 1) },
            _ => map with { IsCorrupted = true },
        };
    }
}

public sealed record P12MapBatchRule(
    P12MapRarity TargetRarity = P12MapRarity.Rare,
    int MinimumQuality = 20,
    IReadOnlyList<P12MapAffixKind>? ExcludedAffixes = null,
    int MaximumMetalSpendPerMap = 8,
    bool Corrupt = false,
    P12BatchFailureBehavior FailureBehavior = P12BatchFailureBehavior.Keep)
{
    public P12MapBatchRule Validate()
    {
        if (MinimumQuality is < 0 or > 20 || MaximumMetalSpendPerMap is < 0 or > 100 || !Enum.IsDefined(TargetRarity))
            throw new ArgumentOutOfRangeException(nameof(MinimumQuality));
        return this;
    }
}

public sealed record P12MapBatchResult(int Processed, int Completed, int Skipped, int MetalsSpent, bool Stopped, string Summary);

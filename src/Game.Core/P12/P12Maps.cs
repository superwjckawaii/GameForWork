using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.World;
using GameForWork.Core.P4;
using GameForWork.Core.P26;
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

public sealed record P12MapAffix(
    P12MapAffixKind Kind,
    string DisplayName,
    int Value,
    P26MapAffixFamily Family = P26MapAffixFamily.DangerousPrefix,
    int Rank = 1,
    int MonsterQuantityBasisPoints = 0,
    int ItemQuantityBasisPoints = 0);
public sealed record P12MapArea(string StableId, string DisplayName, string Environment, string MonsterPool, string BossName);
public sealed record P12MapCombatModifiers(
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
    public static P12MapCombatModifiers From(P1MapItem map)
    {
        int Value(P12MapAffixKind kind)
        {
            int value = map.EffectiveAffixes.Where(affix => affix.Kind == kind).Sum(affix => affix.Value);
            return map.CorruptionRule == P26CorruptionRule.Disorder ? value * 3 / 2 : value;
        }
        int tierLife = map.Tier == 20 ? 2_500 : 0;
        int tierDamage = map.Tier is 17 or 20 ? 1_500 : 0;
        int tierSpeed = map.Tier == 20 ? 1_000 : 0;
        int tierRecovery = map.Tier == 18 ? 3_000 : 0;
        int life = 10_000 + tierLife;
        int damage = 10_000 + tierDamage;
        life = life * (10_000 + Value(P12MapAffixKind.MonsterLife) * 100) / 10_000;
        damage = damage * (10_000 + Value(P12MapAffixKind.MonsterDamage) * 100) / 10_000;
        damage = damage * (10_000 + Value(P12MapAffixKind.ElementalPossession) * 100) / 10_000;
        int bossLife = 10_000 + Value(P12MapAffixKind.Stronghold) * 100;
        int strongholdRank = map.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == P12MapAffixKind.Stronghold)?.Rank ?? 0;
        int bossDamage = 10_000 + (strongholdRank switch { 1 => 1_400, 2 => 2_200, 3 => 2_900, 4 => 3_600, _ => 0 });
        switch (map.CorruptionRule)
        {
            case P26CorruptionRule.BloodTide: damage = damage * 13_600 / 10_000; break;
            case P26CorruptionRule.Greed: life = life * 17_500 / 10_000; damage = damage * 13_600 / 10_000; break;
            case P26CorruptionRule.KingDisaster: bossLife = bossLife * 25_000 / 10_000; bossDamage = bossDamage * 16_500 / 10_000; break;
        }
        int areaRank = map.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == P12MapAffixKind.AreaDisaster)?.Rank ?? 0;
        int areaDamage = areaRank switch { 1 => 10_900, 2 => 11_800, 3 => 12_700, 4 => 13_600, _ => 10_000 };
        int area = 10_000 + Value(P12MapAffixKind.AreaDisaster) * 100;
        if (map.CorruptionRule == P26CorruptionRule.KingDisaster) area = area * 18_000 / 10_000;
        P12MapAffix? twin = map.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == P12MapAffixKind.TwinThrone);
        if (twin is not null)
        {
            bossLife = bossLife * twin.Value * 100 / 10_000;
            bossDamage = bossDamage * (twin.Rank switch { 1 => 9_000, 2 => 9_500, 3 => 10_000, _ => 10_500 }) / 10_000;
        }
        int guards = map.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == P12MapAffixKind.RoyalGuard)?.Value ?? 0;
        int rareEnemies = map.EffectiveAffixes.Where(affix => affix.Kind is P12MapAffixKind.EliteLeaders or P12MapAffixKind.HeadHunterMark)
            .Sum(affix => affix.Value);
        return new(
            life,
            damage,
            10_000 + tierSpeed + Value(P12MapAffixKind.MonsterSpeed) * 100,
            Math.Max(2_000, 10_000 - tierRecovery - Value(P12MapAffixKind.ReducedRecovery) * 100),
            10_000 + map.MonsterQuantityBasisPoints,
            map.Tier == 19 || map.EffectiveAffixes.Any(affix => affix.Kind is P12MapAffixKind.MightyPacks or P12MapAffixKind.EliteLeaders),
            bossLife,
            bossDamage,
            Value(P12MapAffixKind.PhysicalResistance) * 100,
            Value(P12MapAffixKind.ElementalShell) * 100,
            Value(P12MapAffixKind.VoidShroud) * 100,
            Value(P12MapAffixKind.Penetration) * 100,
            map.EffectiveAffixes.Any(affix => affix.Kind == P12MapAffixKind.MultipleProjectiles) ? 2 : 0,
            map.EffectiveAffixes.Any(affix => affix.Kind == P12MapAffixKind.MultipleProjectiles) ? 7_000 : 10_000,
            area,
            areaDamage,
            twin is null ? 1 : 2,
            guards,
            rareEnemies);
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
    private static readonly MapRoute[] InitialRoutes = [MapRoute.Safe, MapRoute.Abyss, MapRoute.LifeGarden];
    private static readonly IReadOnlyDictionary<int, MapRoute[]> CanonicalRouteSets = Enumerable.Range(1, 7)
        .ToDictionary(mask => mask, mask => InitialRoutes.Where(route => (mask & (1 << (int)route)) != 0).ToArray());
    public static P1MapItem EnsureFormal(P1MapItem map, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (P12MapCatalog.TryGet(map.AreaId, out _) && map.EffectiveRouteCandidates.Count > 0)
            return ApplyImprints(P26MapRules.NormalizeLegacy(map, seed)).Validate();
        ulong stableSeed = StableSeed(seed, map.InstanceId, map.Tier);
        var random = new Pcg32(stableSeed);
        P12MapArea area = P12MapCatalog.Areas[(int)(random.NextUInt() % (uint)P12MapCatalog.Areas.Count)];
        MapRoute[] routes = InitialRoutes;
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
        return ApplyImprints(P26MapRules.NormalizeLegacy(map with { AreaId = area.StableId, RouteCandidates = candidates, Altar = altar }, stableSeed)).Validate();
    }

    private static P1MapItem ApplyImprints(P1MapItem map)
    {
        bool abyss = map.EffectiveAffixes.Any(affix => affix.Kind == P12MapAffixKind.AbyssMark);
        bool garden = map.EffectiveAffixes.Any(affix => affix.Kind == P12MapAffixKind.GardenMark);
        bool red = map.EffectiveAffixes.Any(affix => affix.Kind == P12MapAffixKind.RedOathMark);
        bool blue = map.EffectiveAffixes.Any(affix => affix.Kind == P12MapAffixKind.BlueOathMark);
        P12MapAltar desiredAltar = red ? P12MapAltar.RedOath : blue ? P12MapAltar.BlueOath : map.Altar;
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

    public static IReadOnlyList<P12MapAffix> RollAffixes(P12MapRarity rarity, ulong seed)
        => P26MapRules.RollAffixes(rarity, 1, seed);

    public static IReadOnlyList<P12MapAffix> RollAffixes(P12MapRarity rarity, int tier, ulong seed)
        => P26MapRules.RollAffixes(rarity, tier, seed);

    private static ulong StableSeed(ulong seed, string id, int tier)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}|{id}|{tier}|p12"));
        return BitConverter.ToUInt64(hash, 0);
    }
}

public enum P12MapCraftOperation { PolishQuality, AwakenMagic, AlchemicalRare, ChaosReroll, ExaltedAdd, Corrupt }
public enum P12BatchFailureBehavior { Keep, Sell }
public sealed record P12MapCraftResult(bool Succeeded, P1MapItem? Map, MetalCurrencyKind Currency, int Cost, string Summary,
    bool Destroyed = false);

public static class P12MapCrafting
{
    public static (MetalCurrencyKind Currency, int Cost) Cost(P12MapCraftOperation operation) => operation switch
    {
        P12MapCraftOperation.PolishQuality => (MetalCurrencyKind.PolishingCobalt, 1),
        P12MapCraftOperation.AwakenMagic => (MetalCurrencyKind.AwakeningCopper, 1),
        P12MapCraftOperation.AlchemicalRare => (MetalCurrencyKind.AlchemicalGold, 1),
        P12MapCraftOperation.ChaosReroll => (MetalCurrencyKind.ChaosGold, 1),
        P12MapCraftOperation.ExaltedAdd => (MetalCurrencyKind.ExaltedGold, 1),
        P12MapCraftOperation.Corrupt => (MetalCurrencyKind.CorruptionIron, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    public static P12MapCraftResult Apply(TownEconomyState economy, P1MapItem source, P12MapCraftOperation operation,
        ulong seed, int maximumUnlockedTier = P1MapItem.MaximumTier, int polishQualityGain = 5)
    {
        ArgumentNullException.ThrowIfNull(economy);
        P1MapItem map = source.EnsureFormal(seed);
        (MetalCurrencyKind currency, int cost) = Cost(operation);
        string? invalid = operation switch
        {
            P12MapCraftOperation.PolishQuality when map.Quality >= 20 => "quality_maximum",
            P12MapCraftOperation.AwakenMagic when map.Rarity != P12MapRarity.Basic => "basic_required",
            P12MapCraftOperation.AlchemicalRare when map.Rarity == P12MapRarity.Rare => "not_rare_required",
            P12MapCraftOperation.ChaosReroll when map.Rarity != P12MapRarity.Rare => "rare_required",
            P12MapCraftOperation.ExaltedAdd when map.Rarity != P12MapRarity.Rare => "rare_required",
            P12MapCraftOperation.ExaltedAdd when map.EffectiveAffixes.Count >= 6 => "affixes_full",
            P12MapCraftOperation.Corrupt when map.Rarity != P12MapRarity.Rare => "rare_required",
            P12MapCraftOperation.Corrupt when map.IsCorrupted => "already_corrupted",
            _ when map.IsCorrupted => "map_corrupted",
            _ => null,
        };
        if (invalid is not null) return new(false, map, currency, 0, invalid);
        if (!economy.TrySpendMetal(currency, cost)) return new(false, map, currency, cost, "metal_insufficient");

        map = operation switch
        {
            P12MapCraftOperation.PolishQuality => map with { Quality = Math.Min(20, map.Quality + Math.Clamp(polishQualityGain, 1, 20)) },
            P12MapCraftOperation.AwakenMagic => map with { Rarity = P12MapRarity.Magic, Affixes = P12MapRules.RollAffixes(P12MapRarity.Magic, map.Tier, seed) },
            P12MapCraftOperation.AlchemicalRare => map with { Rarity = P12MapRarity.Rare, Affixes = P12MapRules.RollAffixes(P12MapRarity.Rare, map.Tier, seed) },
            P12MapCraftOperation.ChaosReroll => map with { Affixes = P12MapRules.RollAffixes(P12MapRarity.Rare, map.Tier, seed) },
            P12MapCraftOperation.ExaltedAdd => map with { Affixes = P26MapRules.AddExaltedAffix(map, seed) },
            _ => map,
        };
        if (operation == P12MapCraftOperation.Corrupt)
        {
            P1MapItem? corrupted = P26MapRules.Corrupt(map, seed, out bool destroyed);
            return new(true, corrupted, currency, cost, destroyed ? "corruption_destroyed" : $"corruption_{corrupted!.CorruptionRule}", destroyed);
        }
        return new(true, map.Validate(), currency, cost, operation.ToString());
    }
}

public sealed record P12MapBatchRule(
    P12MapRarity TargetRarity = P12MapRarity.Rare,
    int MinimumQuality = 20,
    IReadOnlyList<P12MapAffixKind>? ExcludedAffixes = null,
    bool FillAffixes = false,
    bool Corrupt = false,
    P12BatchFailureBehavior ExcludedAffixBehavior = P12BatchFailureBehavior.Keep)
{
    public P12MapBatchRule Validate()
    {
        if (MinimumQuality is < 0 or > 20 || !Enum.IsDefined(TargetRarity) || !Enum.IsDefined(ExcludedAffixBehavior))
            throw new ArgumentOutOfRangeException(nameof(MinimumQuality));
        return this;
    }
}

public sealed record P12MapBatchResult(int Processed, int Completed, int Skipped, int MetalsSpent, bool Stopped, string Summary);

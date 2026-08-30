using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P3;
using GameForWork.Core.P4;
using GameForWork.Core.P5;
using GameForWork.Core.P10;
using GameForWork.Core.P12;
using GameForWork.Core.P14;
using GameForWork.Core.P26;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P20;

public sealed record P20DefeatedEnemy(string StableId, EnemyRarity Rarity, int MonsterLevel, int ThreatPoints);

public sealed record P20LootContext(
    string SourceId,
    int MonsterLevel,
    int QuantityBasisPoints,
    int MonsterQuantityBonusBasisPoints,
    MapRoute Route,
    int SourceTier = 0,
    int MaximumUnlockedTier = P1MapItem.MaximumTier,
    bool AllowMaps = false,
    bool AllowLegendary = true,
    bool Completed = true,
    bool Practice = false,
    string BossPool = "",
    P1MapItem? Map = null);

public sealed record P20DropTrace(
    int DefeatedEnemies,
    long BaseBudget,
    long EffectiveBudget,
    int EquipmentCount,
    int Gold,
    int MetalCount,
    int MapCount,
    int SkillStoneCount,
    bool LegendaryDropped,
    int QuantityBasisPoints,
    int MonsterQuantityBonusBasisPoints);

public sealed record P20RewardBatch(
    IReadOnlyList<ItemInstance> Equipment,
    IReadOnlyList<P1MapItem> Maps,
    int Gold,
    IReadOnlyList<MetalCurrencyStack> Metals,
    int SkillStones,
    bool LegendaryDropped,
    P20DropTrace Trace);

public static class P20DropFormula
{
    public const int NormalBudget = 100;
    public const int MagicBudget = 250;
    public const int RareBudget = 800;
    public const int BossBudget = 3_000;
    public const int BaseMapReturnBasisPoints = 11_500;
    public const int RegularLegendaryChanceBasisPoints = 333;
    public const int BossLegendaryChanceBasisPoints = 800;
    public const int PinnacleMapReturnBasisPoints = 9_200;
    private const int EquipmentCost = 2_400;
    private const int MetalCost = 6_000;
    private const int SkillStoneCost = 80_000;
    private static readonly IReadOnlyDictionary<string, EnemyProfile> EnemyProfiles = P1Enemies.NormalEnemies
        .Append(P1Enemies.AbyssWarden)
        .GroupBy(enemy => enemy.StableId, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public static P20RewardBatch Roll(P20LootContext context, IReadOnlyList<P20DefeatedEnemy> defeated, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(defeated);
        var random = new Pcg32(seed);
        long baseBudget = defeated.Sum(enemy => EnemyBudget(enemy, context.Map?.AtlasSnapshot));
        int levelMultiplier = Math.Clamp(8_000 + context.MonsterLevel * 30, 8_000, 11_600);
        int monsterMultiplier = 10_000 + Math.Clamp(context.MonsterQuantityBonusBasisPoints, 0, 12_000);
        long effectiveBudget = Scale(Scale(Scale(baseBudget, levelMultiplier), monsterMultiplier),
            Math.Max(0, context.QuantityBasisPoints));
        P12MapAffix? vault = context.Map?.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == P12MapAffixKind.SealedVault);
        if (vault is not null)
        {
            int chestBudget = vault.Rank switch { 1 => 800, 2 => 1_000, 3 => 1_200, _ => 1_500 };
            effectiveBudget += Scale(Scale(baseBudget, chestBudget * vault.Value), context.QuantityBasisPoints);
        }
        bool bossDefeated = defeated.Any(enemy => enemy.Rarity == EnemyRarity.Boss);

        int equipmentRoute = (context.Route == MapRoute.Safe ? 12_000 : context.Route == MapRoute.LifeGarden ? 10_500 : 9_500) +
            P26AtlasEffects.EquipmentQuantityIncrease(context.Map?.AtlasSnapshot, bossDefeated);
        int equipmentCount = Math.Min(40, RollRatio(random, Scale(effectiveBudget, equipmentRoute), EquipmentCost));
        var equipment = new List<ItemInstance>(equipmentCount + 1);
        for (int index = 0; index < equipmentCount; index++)
        {
            P20DefeatedEnemy source = PickEnemy(defeated, random, context.MonsterLevel);
            ItemBaseDefinition itemBase = PickBase(source.MonsterLevel, random);
            ItemRarity rarity = itemBase.Category == ItemCategory.LifeFlask
                ? ItemRarity.Basic
                : RollRarity(source.Rarity,
                    P26AtlasEffects.EquipmentRarityIncrease(context.Map?.AtlasSnapshot, source.Rarity) +
                    (context.Map?.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == P12MapAffixKind.BountifulMark)?.Value ?? 0) * 100,
                    random);
            int itemLevel = Math.Min(120, source.MonsterLevel + (source.Rarity == EnemyRarity.Boss ? 2 :
                source.Rarity == EnemyRarity.Rare ? 1 : 0));
            ulong itemSeed = NextSeed(random);
            ItemInstance item = ItemGenerator.Generate(itemBase.StableId, itemLevel, rarity, itemSeed,
                $"drop-{context.SourceId}-{index}-{itemSeed:x8}");
            if (context.Route == MapRoute.LifeGarden && rarity == ItemRarity.Rare && random.NextBasisPoints() < 3_000)
                item = item with { IsCraftingBase = true };
            equipment.Add(item);
        }

        bool legendary = context.AllowLegendary && !context.Practice && defeated.Count > 0 &&
            random.NextBasisPoints() < LegendaryChance(context);
        if (legendary)
        {
            P14UniqueDefinition unique = P20LegendaryDrops.Pick(context.BossPool, random);
            equipment.Add(P14UniqueItems.Create(unique.StableId, Math.Min(120, context.MonsterLevel + 2),
                $"drop-{context.SourceId}-{unique.StableId.Split('.')[^1]}-{seed:x8}"));
        }

        int goldRoute = (context.Route == MapRoute.Safe ? 12_500 : 10_000) +
            P26AtlasEffects.GoldIncrease(context.Map?.AtlasSnapshot, bossDefeated);
        int gold = RollRatio(random, Scale(effectiveBudget, goldRoute), 400);
        int metalRoute = (context.Route == MapRoute.LifeGarden ? 14_000 : context.Route == MapRoute.Abyss ? 12_000 : 10_000) +
            P26AtlasEffects.MetalIncrease(context.Map?.AtlasSnapshot, bossDefeated);
        int metalCount = Math.Min(20, RollRatio(random, Scale(effectiveBudget, metalRoute), MetalCost));
        IReadOnlyList<MetalCurrencyStack> metals = RollMetals(random, metalCount, context.Route);
        int stoneRoute = (context.Route == MapRoute.Abyss ? 30_000 : 10_000) +
            P26AtlasEffects.SkillStoneIncrease(context.Map?.AtlasSnapshot, bossDefeated);
        int skillStones = Math.Min(5, RollRatio(random, Scale(effectiveBudget, stoneRoute), SkillStoneCost));
        IReadOnlyList<P1MapItem> maps = context.AllowMaps && context.Completed && !context.Practice
            ? RollMaps(context, random)
            : [];
        var trace = new P20DropTrace(defeated.Count, baseBudget, effectiveBudget, equipment.Count, gold,
            metalCount, maps.Count, skillStones, legendary, context.QuantityBasisPoints, context.MonsterQuantityBonusBasisPoints);
        return new P20RewardBatch(equipment, maps, gold, metals, skillStones, legendary, trace);
    }

    public static IReadOnlyList<P20DefeatedEnemy> ExtractDefeated(P3SceneTimeline timeline, int monsterLevel)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var result = new List<P20DefeatedEnemy>();
        foreach (P3SceneEvent defeated in timeline.Events.Where(item => item.Kind == P3SceneEventKind.EnemyDefeated))
        {
            string[] detail = defeated.Detail.Split('|');
            string entityId = detail.Length >= 2 ? detail[1] : string.Empty;
            string stableId = detail.Length >= 3 ? detail[^1] : string.Empty;
            P4EnemyFrame? frame = timeline.SpatialFrames?
                .Where(candidate => candidate.NodeIndex == defeated.NodeIndex)
                .SelectMany(candidate => candidate.Enemies)
                .FirstOrDefault(enemy => enemy.EntityId == entityId);
            EnemyRarity rarity = frame?.Rarity ?? EnemyRarity.Normal;
            stableId = frame?.EnemyStableId ?? stableId;
            int threat = EnemyProfiles.GetValueOrDefault(stableId)?.ThreatPoints ?? (rarity == EnemyRarity.Boss ? 4 : 2);
            result.Add(new P20DefeatedEnemy(stableId, rarity, monsterLevel, Math.Clamp(threat, 1, 4)));
        }
        return result;
    }

    public static IReadOnlyList<P20DefeatedEnemy> ExtractDefeated(P1MapRunResult run, int monsterLevel) =>
        run.Attempts.SelectMany(attempt => attempt.Timeline is null
                ? Array.Empty<P20DefeatedEnemy>()
                : ExtractDefeated(attempt.Timeline, monsterLevel))
            .ToArray();

    public static IReadOnlyList<P20DefeatedEnemy> SyntheticPack(int monsterLevel, bool boss = true)
    {
        int normal = 36 + monsterLevel / 8;
        int magic = Math.Max(2, normal / 5);
        int rare = Math.Max(1, normal / 18);
        var result = new List<P20DefeatedEnemy>(normal + magic + rare + (boss ? 1 : 0));
        result.AddRange(Enumerable.Repeat(new P20DefeatedEnemy("audit.normal", EnemyRarity.Normal, monsterLevel, 2), normal));
        result.AddRange(Enumerable.Repeat(new P20DefeatedEnemy("audit.magic", EnemyRarity.Magic, monsterLevel, 2), magic));
        result.AddRange(Enumerable.Repeat(new P20DefeatedEnemy("audit.rare", EnemyRarity.Rare, monsterLevel, 3), rare));
        if (boss) result.Add(new P20DefeatedEnemy("audit.boss", EnemyRarity.Boss, monsterLevel, 4));
        return result;
    }

    public static int RollScaledCount(int baseCount, int multiplierBasisPoints, ulong seed)
    {
        if (baseCount < 0 || multiplierBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(baseCount));
        return RollRatio(new Pcg32(seed), (long)baseCount * multiplierBasisPoints, 10_000);
    }

    public static int EnemyCoefficient(EnemyRarity rarity) => rarity switch
    {
        EnemyRarity.Magic => MagicBudget,
        EnemyRarity.Rare => RareBudget,
        EnemyRarity.Boss => BossBudget,
        _ => NormalBudget,
    };

    public static P20DropTrace RollAuditTrace(P20LootContext context,
        IReadOnlyList<P20DefeatedEnemy> defeated, ulong seed)
    {
        var random = new Pcg32(seed);
        long baseBudget = defeated.Sum(enemy => EnemyBudget(enemy, context.Map?.AtlasSnapshot));
        int levelMultiplier = Math.Clamp(8_000 + context.MonsterLevel * 30, 8_000, 11_600);
        int monsterMultiplier = 10_000 + Math.Clamp(context.MonsterQuantityBonusBasisPoints, 0, 12_000);
        long effectiveBudget = Scale(Scale(Scale(baseBudget, levelMultiplier), monsterMultiplier),
            Math.Max(0, context.QuantityBasisPoints));
        int equipmentRoute = context.Route == MapRoute.Safe ? 12_000 : context.Route == MapRoute.LifeGarden ? 10_500 : 9_500;
        int equipment = Math.Min(40, RollRatio(random, Scale(effectiveBudget, equipmentRoute), EquipmentCost));
        bool legendary = context.AllowLegendary && !context.Practice && defeated.Count > 0 &&
            random.NextBasisPoints() < LegendaryChance(context);
        if (legendary) equipment++;
        int goldRoute = context.Route == MapRoute.Safe ? 12_500 : 10_000;
        int gold = RollRatio(random, Scale(effectiveBudget, goldRoute), 400);
        int metalRoute = context.Route == MapRoute.LifeGarden ? 14_000 : context.Route == MapRoute.Abyss ? 12_000 : 10_000;
        int metals = Math.Min(20, RollRatio(random, Scale(effectiveBudget, metalRoute), MetalCost));
        int stoneRoute = context.Route == MapRoute.Abyss ? 30_000 : 10_000;
        int stones = Math.Min(5, RollRatio(random, Scale(effectiveBudget, stoneRoute), SkillStoneCost));
        int maps = 0;
        if (context.AllowMaps && context.Completed && !context.Practice)
        {
            long expected = (long)MapReturnBasisPoints(context.SourceTier) * context.QuantityBasisPoints / 10_000;
            maps = Math.Min(4, RollRatio(random, expected, 10_000));
        }
        return new P20DropTrace(defeated.Count, baseBudget, effectiveBudget, equipment, gold,
            metals, maps, stones, legendary, context.QuantityBasisPoints, context.MonsterQuantityBonusBasisPoints);
    }

    private static long EnemyBudget(P20DefeatedEnemy enemy, IReadOnlyList<string>? atlas) =>
        (long)EnemyCoefficient(enemy.Rarity) * (8_000 + Math.Clamp(enemy.ThreatPoints, 1, 4) * 1_000) / 10_000 *
        (10_000 + P26AtlasEffects.EnemyBudgetIncrease(atlas, enemy.Rarity)) / 10_000;

    private static int LegendaryChance(P20LootContext context)
    {
        int basis = string.IsNullOrEmpty(context.BossPool) ? RegularLegendaryChanceBasisPoints : BossLegendaryChanceBasisPoints;
        long chance = (long)basis * context.QuantityBasisPoints / 10_000;
        return Math.Clamp((int)chance, 0, 10_000);
    }

    private static P20DefeatedEnemy PickEnemy(IReadOnlyList<P20DefeatedEnemy> defeated, Pcg32 random, int fallbackLevel)
    {
        if (defeated.Count == 0) return new("fallback", EnemyRarity.Normal, fallbackLevel, 2);
        int total = defeated.Sum(enemy => EnemyCoefficient(enemy.Rarity));
        int roll = Next(random, total);
        foreach (P20DefeatedEnemy enemy in defeated)
        {
            int weight = EnemyCoefficient(enemy.Rarity);
            if (roll < weight) return enemy;
            roll -= weight;
        }
        return defeated[^1];
    }

    private static ItemBaseDefinition PickBase(int itemLevel, Pcg32 random)
    {
        ItemBaseDefinition[] candidates = P1ItemBases.All
            .Where(item => item.RequiredLevel <= Math.Max(1, itemLevel))
            .OrderBy(item => item.StableId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0) candidates = P1ItemBases.All.OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();
        int Weight(ItemBaseDefinition item) => Math.Max(100, 1_200 - Math.Abs(item.RequiredLevel - itemLevel) * 18);
        int total = candidates.Sum(Weight);
        int roll = Next(random, total);
        foreach (ItemBaseDefinition candidate in candidates)
        {
            int weight = Weight(candidate);
            if (roll < weight) return candidate;
            roll -= weight;
        }
        return candidates[^1];
    }

    private static ItemRarity RollRarity(EnemyRarity source, int increasedBasisPoints, Pcg32 random)
    {
        (int magic, int rare) = source switch
        {
            EnemyRarity.Magic => (4_000, 1_500),
            EnemyRarity.Rare => (4_500, 3_000),
            EnemyRarity.Boss => (4_000, 4_000),
            _ => (2_500, 700),
        };
        magic = Math.Min(8_500, magic * (10_000 + Math.Max(0, increasedBasisPoints)) / 10_000);
        rare = Math.Min(7_500, rare * (10_000 + Math.Max(0, increasedBasisPoints)) / 10_000);
        int roll = random.NextBasisPoints();
        if (roll < rare) return ItemRarity.Rare;
        if (roll < rare + magic) return ItemRarity.Magic;
        return ItemRarity.Basic;
    }

    private static IReadOnlyList<P1MapItem> RollMaps(P20LootContext context, Pcg32 random)
    {
        bool boss = context.Map is not null;
        long expected = (long)MapReturnBasisPoints(context.SourceTier) * context.QuantityBasisPoints / 10_000 *
            (10_000 + P26AtlasEffects.MapQuantityIncrease(context.Map?.AtlasSnapshot, boss)) / 10_000;
        int count = Math.Min(4, RollRatio(random, expected, 10_000));
        var result = new List<P1MapItem>(count);
        for (int index = 0; index < count; index++)
        {
            int tierRoll = random.NextBasisPoints();
            int delta = tierRoll switch { < 2_500 => -1, < 7_500 => 0, < 9_500 => 1, _ => 2 };
            int tier = Math.Clamp(context.SourceTier + delta, 1, context.MaximumUnlockedTier);
            byte[] sourceHash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(context.SourceId));
            string sourceToken = Convert.ToHexString(sourceHash.AsSpan(0, 6)).ToLowerInvariant();
            string id = $"map-{sourceToken}-t{tier:00}-{index}-{random.NextUInt():x8}";
            result.Add(new P1MapItem(id, tier).EnsureFormal(random.NextUInt()));
        }
        P12MapAffix? roadEcho = context.Map?.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == P12MapAffixKind.RoadEcho);
        int roadChance = (roadEcho?.Value ?? 0) * 100;
        if (roadEcho is not null && P26AtlasEffects.Has(context.Map?.AtlasSnapshot, "p26.atlas.supply.07")) roadChance *= 2;
        if (roadChance > 0 && random.NextBasisPoints() < Math.Min(10_000, roadChance))
        {
            int tier = Math.Clamp(context.SourceTier, 1, context.MaximumUnlockedTier);
            result.Add(new P1MapItem($"map-road-echo-t{tier:00}-{random.NextUInt():x8}", tier).EnsureFormal(random.NextUInt()));
        }
        return result;
    }

    private static int MapReturnBasisPoints(int sourceTier) => sourceTier switch
    {
        <= 5 => BaseMapReturnBasisPoints,
        <= 10 => 10_800,
        <= 16 => 10_000,
        _ => PinnacleMapReturnBasisPoints,
    };

    private static IReadOnlyList<MetalCurrencyStack> RollMetals(Pcg32 random, int count, MapRoute route)
    {
        var amounts = new Dictionary<MetalCurrencyKind, int>();
        MetalCurrencyDefinition[] candidates = P4MetalCurrencies.All.ToArray();
        int Weight(MetalCurrencyDefinition metal)
        {
            int multiplier = route switch
            {
                MapRoute.Abyss when metal.Tier == MetalCurrencyTier.Dangerous => 400,
                MapRoute.Abyss when metal.Tier == MetalCurrencyTier.High => 200,
                MapRoute.LifeGarden when metal.Tier == MetalCurrencyTier.Advanced => 220,
                MapRoute.Safe when metal.Tier == MetalCurrencyTier.Basic => 150,
                _ => 100,
            };
            return metal.DropWeight * multiplier;
        }
        int total = candidates.Sum(Weight);
        for (int index = 0; index < count; index++)
        {
            int roll = Next(random, total);
            MetalCurrencyDefinition selected = candidates[^1];
            foreach (MetalCurrencyDefinition candidate in candidates)
            {
                int weight = Weight(candidate);
                if (roll < weight) { selected = candidate; break; }
                roll -= weight;
            }
            amounts[selected.Kind] = amounts.GetValueOrDefault(selected.Kind) + 1;
        }
        return amounts.OrderBy(pair => pair.Key).Select(pair => new MetalCurrencyStack(pair.Key, pair.Value)).ToArray();
    }

    private static long Scale(long value, int basisPoints) => value * basisPoints / 10_000;

    private static int RollRatio(Pcg32 random, long numerator, int denominator)
    {
        if (numerator <= 0) return 0;
        long whole = numerator / denominator;
        long remainder = numerator % denominator;
        return checked((int)whole + (remainder > 0 && random.NextUInt() % (uint)denominator < remainder ? 1 : 0));
    }

    private static int Next(Pcg32 random, int exclusiveMaximum) =>
        exclusiveMaximum <= 0 ? 0 : (int)(random.NextUInt() % (uint)exclusiveMaximum);

    private static ulong NextSeed(Pcg32 random) => ((ulong)random.NextUInt() << 32) | random.NextUInt();
}

public static class P20LegendaryDrops
{
    public const int PityMarkCost = 12;
    private static readonly string[] WardenPool =
    [
        "core.unique.warden_shell", "core.unique.hollow_guard", "core.unique.last_watch", "core.unique.black_tide",
    ];
    private static readonly string[] CitadelPool =
    [
        "core.unique.grave_plate", "core.unique.ashes_memory", "core.unique.funeral_bell", "core.unique.fourth_testament",
    ];
    private static readonly P14UniqueDefinition[] Common = P14UniqueItems.All
        .Where(item => !item.Mythic && !WardenPool.Contains(item.StableId) && !CitadelPool.Contains(item.StableId))
        .OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<P14UniqueDefinition> ExchangePool => P14UniqueItems.All
        .Where(item => !item.Mythic).OrderBy(item => item.DisplayName, StringComparer.Ordinal).ToArray();

    public static string BossPool(P1MapItem map) => P5ExpeditionDirector.IsBoss(map) ? "warden" :
        P10EndgameState.IsCitadel(map) ? "citadel" : string.Empty;

    public static P14UniqueDefinition Pick(string pool, Pcg32 random)
    {
        string[] ids = pool switch { "warden" => WardenPool, "citadel" => CitadelPool, _ => [] };
        P14UniqueDefinition[] candidates = ids.Length == 0 ? Common : ids.Select(id => P14UniqueItems.All.Single(item => item.StableId == id)).ToArray();
        return candidates[(int)(random.NextUInt() % (uint)candidates.Length)];
    }
}

public static class P20ItemValue
{
    private static readonly int[] LinkValues = [0, 1, 3, 8, 20, 80, 500];

    public static int Estimate(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        int rarity = item.Rarity switch { ItemRarity.Basic => 0, ItemRarity.Magic => 35, ItemRarity.Rare => 120, _ => 600 };
        int affixes = item.Affixes.Sum(affix =>
        {
            int tier = Math.Max(1, 12 - P1Affixes.TierFor(item.Base, affix.Definition)) * 5;
            int span = Math.Max(1, affix.EffectiveMaximumValue - affix.EffectiveMinimumValue);
            int roll = Math.Clamp((affix.EffectiveValue - affix.EffectiveMinimumValue) * 20 / span, 0, 20);
            return tier + roll;
        });
        int links = LinkValues[Math.Clamp(item.LinkedSocketCount, 0, 6)];
        int special = item.IsCraftingBase ? 40 : 0;
        if (item.LegendaryRule?.StableId.StartsWith("core.mythic.", StringComparison.Ordinal) == true) special += 4_000;
        return Math.Max(1, item.Base.RequiredLevel + item.ItemLevel + rarity + affixes + links + special);
    }

    public static int SalePrice(ItemInstance item) => Math.Max(1, Estimate(item) / 20);
}

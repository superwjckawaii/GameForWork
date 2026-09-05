using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Scenes;
using GameForWork.Core.Spatial;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Endgame;
using GameForWork.Core.Maps;
using GameForWork.Core.Content;
using GameForWork.Core.Atlas;
using GameForWork.Core.Monsters;
using GameForWork.Core.Resources;
using GameForWork.Core.Builds;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Economy;

public sealed record DefeatedEnemy(string StableId, EnemyRarity Rarity, int MonsterLevel, int ThreatPoints,
    string EntityKey = "", EnemyFamily? Family = null);

public sealed record LootContext(
    string SourceId,
    int MonsterLevel,
    int QuantityBasisPoints,
    int MonsterQuantityBonusBasisPoints,
    MapRoute Route,
    int SourceTier = 0,
    int MaximumUnlockedTier = MapItem.MaximumTier,
    bool AllowMaps = false,
    bool AllowLegendary = true,
    bool Completed = true,
    bool Practice = false,
    string BossPool = "",
    MapItem? Map = null);

public sealed record DropTrace(
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

public sealed record RewardBatch(
    IReadOnlyList<ItemInstance> Equipment,
    IReadOnlyList<MapItem> Maps,
    int Gold,
    IReadOnlyList<MetalCurrencyStack> Metals,
    int SkillStones,
    bool LegendaryDropped,
    DropTrace Trace);

public static class DropFormula
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
    private static readonly IReadOnlyDictionary<string, EnemyProfile> EnemyProfiles = Enemies.NormalEnemies
        .Append(Enemies.AbyssWarden)
        .GroupBy(enemy => enemy.StableId, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public static RewardBatch Roll(LootContext context, IReadOnlyList<DefeatedEnemy> defeated, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(defeated);
        var random = new Pcg32(seed);
        long baseBudget = defeated.Sum(enemy => EnemyBudget(enemy, context.Map?.AtlasSnapshot));
        int levelMultiplier = Math.Clamp(8_000 + context.MonsterLevel * 30, 8_000, 11_600);
        int monsterMultiplier = 10_000 + Math.Clamp(context.MonsterQuantityBonusBasisPoints, 0, 12_000);
        long effectiveBudget = Scale(Scale(Scale(baseBudget, levelMultiplier), monsterMultiplier),
            Math.Max(0, context.QuantityBasisPoints));
        MapAffix? vault = context.Map?.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == MapAffixKind.SealedVault);
        if (vault is not null)
        {
            int chestBudget = vault.Rank switch { 1 => 800, 2 => 1_000, 3 => 1_200, _ => 1_500 };
            effectiveBudget += Scale(Scale(baseBudget, chestBudget * vault.Value), context.QuantityBasisPoints);
        }
        bool bossDefeated = defeated.Any(enemy => enemy.Rarity == EnemyRarity.Boss);

        int equipmentRoute = (context.Route == MapRoute.Safe ? 12_000 : context.Route == MapRoute.LifeGarden ? 10_500 : 9_500) +
            AtlasEffects.EquipmentQuantityIncrease(context.Map?.AtlasSnapshot, bossDefeated);
        int equipmentCount = Math.Min(40, RollRatio(random, Scale(effectiveBudget, equipmentRoute), EquipmentCost));
        var equipment = new List<ItemInstance>(equipmentCount + 1);
        for (int index = 0; index < equipmentCount; index++)
        {
            DefeatedEnemy source = PickEnemy(defeated, random, context.MonsterLevel);
            EnemyFamily family = ResolveFamily(source);
            SourceProfile sourceProfile = DropCatalog.Source(family, source.Rarity,
                source.Rarity == EnemyRarity.Boss ? source.StableId : string.Empty);
            ItemBaseDefinition itemBase = PickBase(source.MonsterLevel, sourceProfile, random);
            ItemRarity rarity = itemBase.Category == ItemCategory.LifeFlask
                ? ItemRarity.Basic
                : RollRarity(source.Rarity,
                    AtlasEffects.EquipmentRarityIncrease(context.Map?.AtlasSnapshot, source.Rarity) +
                    (context.Map?.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == MapAffixKind.BountifulMark)?.Value ?? 0) * 100,
                    random);
            int itemLevel = Math.Min(120, source.MonsterLevel + (source.Rarity == EnemyRarity.Boss ? 2 :
                source.Rarity == EnemyRarity.Rare ? 1 : 0));
            ulong itemSeed = NextSeed(random);
            ItemInstance item = ItemGenerator.Generate(itemBase.StableId, itemLevel, rarity, itemSeed,
                $"drop-{context.SourceId}-{index}-{itemSeed:x8}") with { DropSource = sourceProfile.StableId };
            if (context.Route == MapRoute.LifeGarden && rarity == ItemRarity.Rare && random.NextBasisPoints() < 3_000)
                item = item with { IsCraftingBase = true };
            equipment.Add(item);
        }

        bool legendary = context.AllowLegendary && !context.Practice && defeated.Count > 0 &&
            random.NextBasisPoints() < LegendaryChance(context, bossDefeated);
        if (legendary)
        {
            string pool = LegendaryDrops.ResolvePool(context, defeated);
            UniqueDefinition unique = LegendaryDrops.Pick(pool, random);
            ItemInstance dropped = UniqueItems.Create(unique.StableId, Math.Min(120, context.MonsterLevel + 2),
                $"drop-{context.SourceId}-{unique.StableId.Split('.')[^1]}-{seed:x8}");
            if (unique.StableId == "builds.unique.paired_virtue_girdle")
                dropped = VirtueViceEquipment.ApplyBeltRoll(dropped, random.NextUInt());
            equipment.Add(dropped with { DropSource = $"resources.source.legendary.{pool}" });
        }

        int goldRoute = (context.Route == MapRoute.Safe ? 12_500 : 10_000) +
            AtlasEffects.GoldIncrease(context.Map?.AtlasSnapshot, bossDefeated);
        int gold = RollRatio(random, Scale(effectiveBudget, goldRoute), 400);
        int metalRoute = (context.Route == MapRoute.LifeGarden ? 14_000 : context.Route == MapRoute.Abyss ? 12_000 : 10_000) +
            AtlasEffects.MetalIncrease(context.Map?.AtlasSnapshot, bossDefeated);
        int metalCount = Math.Min(20, RollRatio(random, Scale(effectiveBudget, metalRoute), MetalCost));
        IReadOnlyList<MetalCurrencyStack> metals = RollMetals(random, metalCount, context.Route);
        int stoneRoute = (context.Route == MapRoute.Abyss ? 30_000 : 10_000) +
            AtlasEffects.SkillStoneIncrease(context.Map?.AtlasSnapshot, bossDefeated);
        int skillStones = Math.Min(5, RollRatio(random, Scale(effectiveBudget, stoneRoute), SkillStoneCost));
        // Defeated enemies can drop maps even when the last encounter fails; completion only adds its own bonuses.
        bool mapDropEligible = context.Completed || defeated.Count > 0 && random.NextBasisPoints() <
            Math.Min(10_000, defeated.Count * 10_000 / Math.Max(1, SyntheticPack(context.MonsterLevel).Count));
        IReadOnlyList<MapItem> maps = context.AllowMaps && mapDropEligible && !context.Practice
            ? RollMaps(context, random)
            : [];
        var trace = new DropTrace(defeated.Count, baseBudget, effectiveBudget, equipment.Count, gold,
            metalCount, maps.Count, skillStones, legendary, context.QuantityBasisPoints, context.MonsterQuantityBonusBasisPoints);
        return new RewardBatch(equipment, maps, gold, metals, skillStones, legendary, trace);
    }

    public static IReadOnlyList<DefeatedEnemy> ExtractDefeated(SceneTimeline timeline, int monsterLevel)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        var result = new List<DefeatedEnemy>();
        foreach (SceneEvent defeated in timeline.Events.Where(item => item.Kind == SceneEventKind.EnemyDefeated))
        {
            string[] detail = defeated.Detail.Split('|');
            string entityId = detail.Length >= 2 ? detail[1] : string.Empty;
            string stableId = detail.Length >= 3 ? detail[^1] : string.Empty;
            EnemyFrame? frame = timeline.SpatialFrames?
                .Where(candidate => candidate.NodeIndex == defeated.NodeIndex)
                .SelectMany(candidate => candidate.Enemies)
                .FirstOrDefault(enemy => enemy.EntityId == entityId);
            EnemyRarity rarity = frame?.Rarity ?? EnemyRarity.Normal;
            if (frame?.Summoned == true) continue;
            stableId = frame?.EnemyStableId ?? stableId;
            int threat = EnemyProfiles.GetValueOrDefault(stableId)?.ThreatPoints ?? (rarity == EnemyRarity.Boss ? 4 : 2);
            result.Add(new DefeatedEnemy(stableId, rarity, monsterLevel, Math.Clamp(threat, 1, 4),
                $"{defeated.NodeIndex}:{entityId}"));
        }
        return result;
    }

    public static IReadOnlyList<DefeatedEnemy> ExtractDefeated(MapRunResult run, int monsterLevel) =>
        run.Attempts.SelectMany(attempt => attempt.Timeline is null
                ? Array.Empty<DefeatedEnemy>()
                : ExtractDefeated(attempt.Timeline, monsterLevel))
            .DistinctBy(enemy => enemy.EntityKey)
            .ToArray();

    public static IReadOnlyList<DefeatedEnemy> SyntheticPack(int monsterLevel, bool boss = true)
    {
        int normal = 36 + monsterLevel / 8;
        int magic = Math.Max(2, normal / 5);
        int rare = Math.Max(1, normal / 18);
        var result = new List<DefeatedEnemy>(normal + magic + rare + (boss ? 1 : 0));
        result.AddRange(Enumerable.Repeat(new DefeatedEnemy("audit.normal", EnemyRarity.Normal, monsterLevel, 2), normal));
        result.AddRange(Enumerable.Repeat(new DefeatedEnemy("audit.magic", EnemyRarity.Magic, monsterLevel, 2), magic));
        result.AddRange(Enumerable.Repeat(new DefeatedEnemy("audit.rare", EnemyRarity.Rare, monsterLevel, 3), rare));
        if (boss) result.Add(new DefeatedEnemy("audit.boss", EnemyRarity.Boss, monsterLevel, 4));
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

    public static DropTrace RollAuditTrace(LootContext context,
        IReadOnlyList<DefeatedEnemy> defeated, ulong seed)
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
            random.NextBasisPoints() < LegendaryChance(context, defeated.Any(enemy => enemy.Rarity == EnemyRarity.Boss));
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
        return new DropTrace(defeated.Count, baseBudget, effectiveBudget, equipment, gold,
            metals, maps, stones, legendary, context.QuantityBasisPoints, context.MonsterQuantityBonusBasisPoints);
    }

    private static long EnemyBudget(DefeatedEnemy enemy, IReadOnlyList<string>? atlas) =>
        (long)EnemyCoefficient(enemy.Rarity) * (8_000 + Math.Clamp(enemy.ThreatPoints, 1, 4) * 1_000) / 10_000 *
        (10_000 + AtlasEffects.EnemyBudgetIncrease(atlas, enemy.Rarity)) / 10_000;

    public static int LegendaryChance(LootContext context, bool bossDefeated = true)
    {
        int basis = bossDefeated ? BossLegendaryChanceBasisPoints : RegularLegendaryChanceBasisPoints;
        if (bossDefeated && AtlasEffects.Has(context.Map?.AtlasSnapshot, "atlas.atlas.boss.06")) basis += 400;
        if (bossDefeated && AtlasEffects.Has(context.Map?.AtlasSnapshot, "atlas.atlas.boss.11")) basis += 400;
        int mechanicMultiplier = !bossDefeated && (context.Route == MapRoute.Abyss && AtlasEffects.Has(context.Map?.AtlasSnapshot, "atlas.atlas.abyss.10") ||
            context.Map?.Altar == MapAltar.BlueOath && AtlasEffects.Has(context.Map?.AtlasSnapshot, "atlas.atlas.blue.05")) ? 20_000 : 10_000;
        int chance = Math.Clamp((int)((long)basis * mechanicMultiplier / 10_000 * context.QuantityBasisPoints / 10_000), 0, 9_500);
        return bossDefeated && AtlasEffects.Has(context.Map?.AtlasSnapshot, "atlas.atlas.boss.12")
            ? chance + (10_000 - chance) * (chance / 2) / 10_000
            : chance;
    }

    private static DefeatedEnemy PickEnemy(IReadOnlyList<DefeatedEnemy> defeated, Pcg32 random, int fallbackLevel)
    {
        if (defeated.Count == 0) return new("fallback", EnemyRarity.Normal, fallbackLevel, 2);
        int total = defeated.Sum(enemy => EnemyCoefficient(enemy.Rarity));
        int roll = Next(random, total);
        foreach (DefeatedEnemy enemy in defeated)
        {
            int weight = EnemyCoefficient(enemy.Rarity);
            if (roll < weight) return enemy;
            roll -= weight;
        }
        return defeated[^1];
    }

    public static ItemBaseDefinition PickBase(int itemLevel, SourceProfile source, Pcg32 random)
    {
        ItemBaseDefinition[] candidates = ItemBases.All
            .Where(item => item.RequiredLevel <= Math.Max(1, itemLevel) && !item.ItemTags.Contains("warfront", StringComparer.Ordinal))
            .OrderBy(item => item.StableId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0) candidates = ItemBases.All.OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();
        int Weight(ItemBaseDefinition item)
        {
            int tierWeight = DropCatalog.ResolveBaseTier(item) switch
            {
                BaseTier.Normal => 5_000, BaseTier.Advanced => 3_000,
                BaseTier.High => source.Rarity is EnemyRarity.Rare or EnemyRarity.Boss ? 2_500 : 1_500,
                _ => source.Rarity == EnemyRarity.Boss ? 1_500 : source.Rarity == EnemyRarity.Rare ? 900 : 500,
            };
            bool preferred = source.PreferredBaseTags.Any(tag => item.ItemTags.Contains(tag, StringComparer.Ordinal) ||
                tag == "jewellery" && item.Category is ItemCategory.Ring or ItemCategory.Amulet or ItemCategory.Belt ||
                tag == "weapon" && item.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon ||
                tag == "armor" && item.Armor > 0 || tag == "evasion" && item.Evasion > 0 ||
                tag == "energy_shield" && item.Shield > 0);
            int proximity = Math.Max(100, 1_200 - Math.Abs(item.RequiredLevel - itemLevel) * 18);
            return Math.Max(1, tierWeight * proximity / 1_000 * (preferred ? 3 : 2) / 2);
        }
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
            EnemyRarity.Magic => (2_800, 700),
            EnemyRarity.Rare => (4_000, 1_500),
            EnemyRarity.Boss => (4_500, 2_500),
            _ => (1_500, 300),
        };
        magic = Math.Min(8_500, magic * (10_000 + Math.Max(0, increasedBasisPoints)) / 10_000);
        rare = Math.Min(7_500, rare * (10_000 + Math.Max(0, increasedBasisPoints)) / 10_000);
        int roll = random.NextBasisPoints();
        if (roll < rare) return ItemRarity.Rare;
        if (roll < rare + magic) return ItemRarity.Magic;
        return ItemRarity.Basic;
    }

    private static IReadOnlyList<MapItem> RollMaps(LootContext context, Pcg32 random)
    {
        bool boss = context.Map is not null;
        long expected = (long)MapReturnBasisPoints(context.SourceTier) * context.QuantityBasisPoints / 10_000 *
            (10_000 + AtlasEffects.MapQuantityIncrease(context.Map?.AtlasSnapshot, boss)) / 10_000;
        int count = Math.Min(4, RollRatio(random, expected, 10_000));
        var result = new List<MapItem>(count);
        for (int index = 0; index < count; index++)
        {
            int tierRoll = random.NextBasisPoints();
            int delta = tierRoll switch { < 2_500 => -1, < 7_500 => 0, < 9_500 => 1, _ => 2 };
            int tier = Math.Clamp(context.SourceTier + delta, 1, context.MaximumUnlockedTier);
            byte[] sourceHash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(context.SourceId));
            string sourceToken = Convert.ToHexString(sourceHash.AsSpan(0, 6)).ToLowerInvariant();
            string id = $"map-{sourceToken}-t{tier:00}-{index}-{random.NextUInt():x8}";
            result.Add(new MapItem(id, tier).EnsureFormal(random.NextUInt()));
        }
        MapAffix? roadEcho = context.Map?.EffectiveAffixes.FirstOrDefault(affix => affix.Kind == MapAffixKind.RoadEcho);
        int roadChance = (roadEcho?.Value ?? 0) * 100;
        if (roadEcho is not null && AtlasEffects.Has(context.Map?.AtlasSnapshot, "atlas.atlas.supply.07")) roadChance *= 2;
        if (roadChance > 0 && random.NextBasisPoints() < Math.Min(10_000, roadChance))
        {
            int tier = Math.Clamp(context.SourceTier, 1, context.MaximumUnlockedTier);
            result.Add(new MapItem($"map-road-echo-t{tier:00}-{random.NextUInt():x8}", tier).EnsureFormal(random.NextUInt()));
        }
        return result;
    }

    private static int MapReturnBasisPoints(int sourceTier) => sourceTier switch
    {
        <= 5 => BaseMapReturnBasisPoints,
        <= 10 => 10_800,
        <= 16 => 10_000,
        _ => 9_000,
    };

    private static EnemyFamily ResolveFamily(DefeatedEnemy enemy)
    {
        if (enemy.Family is not null) return enemy.Family.Value;
        if (enemy.Rarity == EnemyRarity.Boss) return EnemyFamily.Boss;
        return EnemyProfiles.GetValueOrDefault(enemy.StableId)?.Family ?? MonsterCatalog.AdditionalEnemies
            .FirstOrDefault(profile => profile.StableId == enemy.StableId)?.Family ?? EnemyFamily.AshenLegion;
    }

    private static IReadOnlyList<MetalCurrencyStack> RollMetals(Pcg32 random, int count, MapRoute route)
    {
        var amounts = new Dictionary<MetalCurrencyKind, int>();
        MetalCurrencyDefinition[] candidates = MetalCurrencies.All.ToArray();
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

public static class LegendaryDrops
{
    public const int PityMarkCost = 12;
    private static readonly string[] WardenPool =
    [
        "core.unique.warden_shell", "core.unique.hollow_guard", "core.unique.last_watch", "core.unique.black_tide",
    ];
    private static readonly string[] CitadelPool =
    [
        "core.unique.grave_plate", "core.unique.ashes_memory", "core.unique.funeral_bell", "core.unique.fourth_testament",
        "core.mythic.heart_of_ash", "equipment.legendary.52.44a586da1f", "equipment.legendary.53.26839c4b94",
        "equipment.legendary.54.915c91c995", "equipment.legendary.55.54b1e3f6f0",
    ];
    private static readonly string[] AbyssPool = ["core.unique.echoing_oathbreaker", "core.unique.thorn_procession", "resources.unique.rift_fang", "resources.unique.deep_echo"];
    private static readonly string[] GardenPool = ["core.unique.gardeners_sinew", "core.unique.march_without_end", "resources.unique.seed_of_rebirth", "resources.unique.thorned_bark"];
    private static readonly string[] RedPool = ["core.unique.red_vow", "core.unique.iron_moon", "resources.unique.executioners_due", "resources.unique.blood_tithe"];
    private static readonly string[] BluePool = ["core.unique.blue_vow", "core.unique.glass_horizon", "resources.unique.frozen_moment", "resources.unique.starfall_lens"];
    private static readonly string[] WarfrontPool = ["core.unique.last_banner", "core.unique.black_tide", "resources.unique.commanders_burden", "resources.unique.broken_standard"];
    private static readonly string[] CommonIds = ["core.unique.ravens_answer", "core.unique.starless_prayer", "core.unique.pilgrims_debt", "core.unique.silent_anvil", "core.unique.hunters_eclipse", "core.unique.famine_ring", "resources.unique.wayfarers_compass", "resources.unique.void_balance",
        "builds.unique.humility_crown", "builds.unique.arrogance_grasp", "builds.unique.rage_temperance_carapace", "builds.unique.paired_virtue_girdle"];
    private static readonly UniqueDefinition[] Common = CommonIds.Select(Get).ToArray();

    public static IReadOnlyList<UniqueDefinition> ExchangePool => UniqueItems.All
        .Where(item => !item.Mythic).OrderBy(item => item.DisplayName, StringComparer.Ordinal).ToArray();

    public static string BossPool(MapItem map) => EndgameState.IsCitadel(map) ? "citadel" :
        ExpeditionDirector.IsAbyssWarden(map) ? "warden" :
        $"mapboss:{(string.IsNullOrWhiteSpace(map.AreaId) ? map.EnsureFormal().AreaId : map.AreaId)}";

    public static string ResolvePool(LootContext context, IReadOnlyList<DefeatedEnemy> defeated)
    {
        string mechanic = PoolForMap(context.Map, context.Route);
        if (mechanic is not "common") return mechanic;
        if (!string.IsNullOrWhiteSpace(context.BossPool)) return context.BossPool;
        DefeatedEnemy? boss = defeated.LastOrDefault(enemy => enemy.Rarity == EnemyRarity.Boss);
        if (boss is not null) return $"boss:{boss.StableId}";
        if (context.Route == MapRoute.Abyss) return "abyss";
        if (context.Route == MapRoute.LifeGarden) return "garden";
        if (context.Route == MapRoute.Warfront) return "warfront";
        return context.Map?.Altar switch { MapAltar.RedOath => "red", MapAltar.BlueOath => "blue", _ => "common" };
    }

    public static string PoolForMap(MapItem? map, MapRoute route)
    {
        if (map is not null && (ExpeditionDirector.IsBoss(map) || EndgameState.IsCitadel(map))) return BossPool(map);
        if (route == MapRoute.Abyss) return "abyss";
        if (route == MapRoute.LifeGarden) return "garden";
        if (route == MapRoute.Warfront) return "warfront";
        string altar = map?.Altar switch { MapAltar.RedOath => "red", MapAltar.BlueOath => "blue", _ => "common" };
        return altar != "common" ? altar : map is not null ? BossPool(map) : "common";
    }

    public static UniqueDefinition Pick(string pool, Pcg32 random)
    {
        string[] ids = pool switch
        {
            "warden" => WardenPool, "citadel" => CitadelPool, "abyss" => AbyssPool,
            "garden" => GardenPool, "red" => RedPool, "blue" => BluePool, "warfront" => WarfrontPool,
            _ when pool.StartsWith("mapboss:", StringComparison.Ordinal) || pool.StartsWith("boss:", StringComparison.Ordinal) || pool == "campaign" => WardenPool,
            _ => [],
        };
        UniqueDefinition[] candidates = ids.Length == 0 ? Common : ids.Select(Get).ToArray();
        return candidates[(int)(random.NextUInt() % (uint)candidates.Length)];
    }

    public static IReadOnlyList<UniqueDefinition> Pool(string pool) => pool switch
    {
        "abyss" => AbyssPool.Select(Get).ToArray(), "garden" => GardenPool.Select(Get).ToArray(),
        "red" => RedPool.Select(Get).ToArray(), "blue" => BluePool.Select(Get).ToArray(),
        "warfront" => WarfrontPool.Select(Get).ToArray(), "citadel" => CitadelPool.Select(Get).ToArray(),
        "warden" => WardenPool.Select(Get).ToArray(), _ => Common,
    };

    private static UniqueDefinition Get(string id) => UniqueItems.All.Single(item => item.StableId == id);
}

public static class ItemValue
{
    private static readonly int[] LinkValues = [0, 1, 3, 8, 20, 80, 500];

    public static int Estimate(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        int rarity = item.Rarity switch { ItemRarity.Basic => 0, ItemRarity.Magic => 35, ItemRarity.Rare => 120, _ => 600 };
        int affixes = item.Affixes.Sum(affix =>
        {
            int tier = Math.Max(1, 12 - Affixes.TierFor(item.Base, affix.Definition)) * 5;
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

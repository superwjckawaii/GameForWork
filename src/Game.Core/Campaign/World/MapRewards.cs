using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Spatial;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Scenes;
using GameForWork.Core.Economy;
using GameForWork.Core.Atlas;

namespace GameForWork.Core.Campaign.World;

public sealed record MapStackableRewards(
    int Gold,
    int IronScraps,
    int MemoryAshes,
    int WardenMarks,
    int SkillStones,
    IReadOnlyList<MetalCurrencyStack>? Metals = null);

public sealed record MapRewards(
    int Experience,
    IReadOnlyList<ItemInstance> Equipment,
    IReadOnlyList<MapItem> Maps,
    MapStackableRewards Stackables,
    bool LegendaryDropped,
    DropTrace? Trace = null);

public static class MapRewardGenerator
{
    public const int ExperiencePerMap = 190;
    public static MapRewards Generate(MapItem completedMap, MapRoute route, ulong seed, int maximumUnlockedTier = MapItem.MaximumTier)
    {
        IReadOnlyList<DefeatedEnemy> defeated = DropFormula.SyntheticPack(completedMap.MonsterLevel);
        return Generate(completedMap, route, seed, maximumUnlockedTier, defeated, completed: true);
    }

    public static MapRewards Generate(MapItem completedMap, MapRoute route, ulong seed,
        int maximumUnlockedTier, MapRunResult run)
    {
        IReadOnlyList<DefeatedEnemy> defeated = DropFormula.ExtractDefeated(run, completedMap.MonsterLevel);
        if (defeated.Count == 0 && run.Attempts.All(attempt => attempt.Timeline is null))
            defeated = DropFormula.SyntheticPack(completedMap.MonsterLevel);
        return Generate(completedMap, route, seed, maximumUnlockedTier, defeated, completed: true);
    }

    public static MapRewards GeneratePartial(
        MapItem map,
        MapRoute route,
        ulong seed,
        int defeatedEnemies,
        int totalEnemies,
        int maximumUnlockedTier = MapItem.MaximumTier)
    {
        if (defeatedEnemies <= 0 || totalEnemies <= 0) return new MapRewards(
            0, [], [], new MapStackableRewards(0, 0, 0, 0, 0, []), false);
        IReadOnlyList<DefeatedEnemy> synthetic = DropFormula.SyntheticPack(map.MonsterLevel);
        int count = Math.Clamp((synthetic.Count * defeatedEnemies + totalEnemies - 1) / totalEnemies, 1, synthetic.Count);
        MapRewards rewards = Generate(map, route, seed, maximumUnlockedTier,
            synthetic.Take(count).ToArray(), completed: false);
        return rewards with { Experience = Math.Max(1, ExperiencePerMap * defeatedEnemies / totalEnemies) };
    }

    public static MapRewards GeneratePartial(MapItem map, MapRoute route, ulong seed, MapRunResult run,
        int maximumUnlockedTier = MapItem.MaximumTier)
    {
        IReadOnlyList<DefeatedEnemy> defeated = DropFormula.ExtractDefeated(run, map.MonsterLevel);
        if (defeated.Count == 0) return new MapRewards(
            0, [], [], new MapStackableRewards(0, 0, 0, 0, 0, []), false);
        return Generate(map, route, seed, maximumUnlockedTier, defeated, completed: false);
    }

    private static MapRewards Generate(MapItem map, MapRoute route, ulong seed, int maximumUnlockedTier,
        IReadOnlyList<DefeatedEnemy> defeated, bool completed)
    {
        ArgumentNullException.ThrowIfNull(map);
        map.Validate();
        map = map.EnsureFormal(seed);
        string bossPool = LegendaryDrops.BossPool(map);
        var context = new LootContext(map.InstanceId, map.MonsterLevel, map.ItemQuantityBasisPoints,
            map.MonsterQuantityBasisPoints, route, map.Tier, maximumUnlockedTier, AllowMaps: true,
            Completed: completed, Practice: ExpeditionDirector.IsPractice(map), BossPool: bossPool, Map: map);
        RewardBatch rolled = DropFormula.Roll(context, defeated, seed);
        int memoryAshes = completed ? DropFormula.RollScaledCount(1, map.ItemQuantityBasisPoints, seed ^ 0x20a5UL) : 0;
        int wardenMarks = completed && ExpeditionDirector.IsBoss(map)
            ? DropFormula.RollScaledCount(1, map.ItemQuantityBasisPoints, seed ^ 0x20b5UL)
            : 0;
        int experience = completed ? ExperiencePerMap : Math.Max(1,
            ExperiencePerMap * defeated.Count / Math.Max(1, DropFormula.SyntheticPack(map.MonsterLevel).Count));
        experience = experience * (10_000 + AtlasEffects.ExperienceIncrease(map.AtlasSnapshot)) / 10_000;
        return new MapRewards(experience, rolled.Equipment, rolled.Maps,
            new MapStackableRewards(rolled.Gold, 0, memoryAshes, wardenMarks, rolled.SkillStones, rolled.Metals),
            rolled.LegendaryDropped, rolled.Trace);
    }

    public static (int Defeated, int Total) CombatProgress(MapRunResult run)
    {
        int defeated = DropFormula.ExtractDefeated(run, run.Map.MonsterLevel).Count;
        int total = run.Attempts.Sum(attempt => attempt.Timeline?.Events.Where(item =>
            item.Kind == SceneEventKind.WaveStarted).Sum(item => Math.Max(0, item.Value)) ?? 0);
        return (defeated, Math.Max(defeated, total));
    }

}

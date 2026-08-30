using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P4;
using GameForWork.Core.P5;
using GameForWork.Core.P3;
using GameForWork.Core.P20;
using GameForWork.Core.P26;

namespace GameForWork.Core.P1.World;

public sealed record MapStackableRewards(
    int Gold,
    int IronScraps,
    int MemoryAshes,
    int WardenMarks,
    int SkillStones,
    IReadOnlyList<MetalCurrencyStack>? Metals = null);

public sealed record P1MapRewards(
    int Experience,
    IReadOnlyList<ItemInstance> Equipment,
    IReadOnlyList<P1MapItem> Maps,
    MapStackableRewards Stackables,
    bool LegendaryDropped,
    P20DropTrace? Trace = null);

public static class P1MapRewardGenerator
{
    public const int ExperiencePerMap = 190;
    public static P1MapRewards Generate(P1MapItem completedMap, MapRoute route, ulong seed, int maximumUnlockedTier = P1MapItem.MaximumTier)
    {
        IReadOnlyList<P20DefeatedEnemy> defeated = P20DropFormula.SyntheticPack(completedMap.MonsterLevel);
        return Generate(completedMap, route, seed, maximumUnlockedTier, defeated, completed: true);
    }

    public static P1MapRewards Generate(P1MapItem completedMap, MapRoute route, ulong seed,
        int maximumUnlockedTier, P1MapRunResult run)
    {
        IReadOnlyList<P20DefeatedEnemy> defeated = P20DropFormula.ExtractDefeated(run, completedMap.MonsterLevel);
        if (defeated.Count == 0 && run.Attempts.All(attempt => attempt.Timeline is null))
            defeated = P20DropFormula.SyntheticPack(completedMap.MonsterLevel);
        return Generate(completedMap, route, seed, maximumUnlockedTier, defeated, completed: true);
    }

    public static P1MapRewards GeneratePartial(
        P1MapItem map,
        MapRoute route,
        ulong seed,
        int defeatedEnemies,
        int totalEnemies,
        int maximumUnlockedTier = P1MapItem.MaximumTier)
    {
        if (defeatedEnemies <= 0 || totalEnemies <= 0) return new P1MapRewards(
            0, [], [], new MapStackableRewards(0, 0, 0, 0, 0, []), false);
        IReadOnlyList<P20DefeatedEnemy> synthetic = P20DropFormula.SyntheticPack(map.MonsterLevel);
        int count = Math.Clamp((synthetic.Count * defeatedEnemies + totalEnemies - 1) / totalEnemies, 1, synthetic.Count);
        P1MapRewards rewards = Generate(map, route, seed, maximumUnlockedTier,
            synthetic.Take(count).ToArray(), completed: false);
        return rewards with { Experience = Math.Max(1, ExperiencePerMap * defeatedEnemies / totalEnemies) };
    }

    public static P1MapRewards GeneratePartial(P1MapItem map, MapRoute route, ulong seed, P1MapRunResult run,
        int maximumUnlockedTier = P1MapItem.MaximumTier)
    {
        IReadOnlyList<P20DefeatedEnemy> defeated = P20DropFormula.ExtractDefeated(run, map.MonsterLevel);
        if (defeated.Count == 0) return new P1MapRewards(
            0, [], [], new MapStackableRewards(0, 0, 0, 0, 0, []), false);
        return Generate(map, route, seed, maximumUnlockedTier, defeated, completed: false);
    }

    private static P1MapRewards Generate(P1MapItem map, MapRoute route, ulong seed, int maximumUnlockedTier,
        IReadOnlyList<P20DefeatedEnemy> defeated, bool completed)
    {
        ArgumentNullException.ThrowIfNull(map);
        map.Validate();
        map = map.EnsureFormal(seed);
        string bossPool = P20LegendaryDrops.BossPool(map);
        var context = new P20LootContext(map.InstanceId, map.MonsterLevel, map.ItemQuantityBasisPoints,
            map.MonsterQuantityBasisPoints, route, map.Tier, maximumUnlockedTier, AllowMaps: true,
            Completed: completed, Practice: P5ExpeditionDirector.IsPractice(map), BossPool: bossPool, Map: map);
        P20RewardBatch rolled = P20DropFormula.Roll(context, defeated, seed);
        int memoryAshes = completed ? P20DropFormula.RollScaledCount(1, map.ItemQuantityBasisPoints, seed ^ 0x20a5UL) : 0;
        int wardenMarks = completed && bossPool.Length > 0
            ? P20DropFormula.RollScaledCount(1, map.ItemQuantityBasisPoints, seed ^ 0x20b5UL)
            : 0;
        int experience = completed ? ExperiencePerMap : Math.Max(1,
            ExperiencePerMap * defeated.Count / Math.Max(1, P20DropFormula.SyntheticPack(map.MonsterLevel).Count));
        experience = experience * (10_000 + P26AtlasEffects.ExperienceIncrease(map.AtlasSnapshot)) / 10_000;
        return new P1MapRewards(experience, rolled.Equipment, rolled.Maps,
            new MapStackableRewards(rolled.Gold, 0, memoryAshes, wardenMarks, rolled.SkillStones, rolled.Metals),
            rolled.LegendaryDropped, rolled.Trace);
    }

    public static (int Defeated, int Total) CombatProgress(P1MapRunResult run)
    {
        int defeated = P20DropFormula.ExtractDefeated(run, run.Map.MonsterLevel).Count;
        int total = run.Attempts.Sum(attempt => attempt.Timeline?.Events.Where(item =>
            item.Kind == P3SceneEventKind.WaveStarted).Sum(item => Math.Max(0, item.Value)) ?? 0);
        return (defeated, Math.Max(defeated, total));
    }

}

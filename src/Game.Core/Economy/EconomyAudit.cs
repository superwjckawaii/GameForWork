using System.Text;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Endgame;
using GameForWork.Core.Maps;
using GameForWork.Core.Atlas;

namespace GameForWork.Core.Economy;

public sealed record AuditBracket(
    string Name,
    int MonsterLevel,
    int Tier,
    int MonsterQuantityBonusBasisPoints,
    MapRoute Route,
    bool Boss,
    int QuantityBasisPoints = 10_000,
    MapAltar Altar = MapAltar.None,
    bool FullAtlas = false,
    bool Corrupted = false);

public sealed record AuditResult(
    AuditBracket Bracket,
    int Samples,
    double AverageEquipment,
    double AverageGold,
    double AverageMetals,
    double AverageMaps,
    double AverageSkillStones,
    double LegendaryRate,
    int EquipmentPercentile10,
    int EquipmentPercentile50,
    int EquipmentPercentile90);

public static class EconomyAudit
{
    public static IReadOnlyList<AuditBracket> StandardBrackets { get; } =
    [
        new("剧情", 35, 0, 25, MapRoute.Safe, false),
        new("T1", 70, 1, 13, MapRoute.Safe, false),
        new("T6", 78, 6, 30, MapRoute.Safe, false),
        new("T10", 84, 10, 42, MapRoute.Abyss, false),
        new("T11", 86, 11, 48, MapRoute.Safe, false),
        new("T16", 94, 16, 63, MapRoute.LifeGarden, false),
        new("T20", 100, 20, 80, MapRoute.Abyss, false),
        new("Boss", 100, 20, 100, MapRoute.Abyss, true),
        .. Mechanics(),
    ];

    public static IReadOnlyList<AuditResult> Run(int samplesPerBracket = 100_000, ulong seed = 0x20ec0a11UL)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samplesPerBracket, 1);
        var results = new List<AuditResult>(StandardBrackets.Count);
        for (int bracketIndex = 0; bracketIndex < StandardBrackets.Count; bracketIndex++)
        {
            AuditBracket bracket = StandardBrackets[bracketIndex];
            IReadOnlyList<DefeatedEnemy> pack = DropFormula.SyntheticPack(bracket.MonsterLevel, bracket.Boss);
            IReadOnlyList<string>? atlas = bracket.FullAtlas ? AtlasTree.Nodes.Select(node => node.StableId).ToArray() : null;
            MapItem? map = bracket.Tier <= 0 ? null : new MapItem($"audit-map-{bracketIndex}", bracket.Tier,
                MapCatalog.Areas[bracketIndex % MapCatalog.Areas.Count].StableId,
                bracket.Corrupted ? MapRarity.Rare : MapRarity.Basic, Altar: bracket.Altar,
                AtlasSnapshot: atlas, IsCorrupted: bracket.Corrupted,
                CorruptionRule: bracket.Corrupted ? CorruptionRule.Greed : CorruptionRule.None);
            var context = new LootContext($"audit-{bracket.Name}", bracket.MonsterLevel,
                bracket.QuantityBasisPoints, bracket.MonsterQuantityBonusBasisPoints, bracket.Route, bracket.Tier,
                MapItem.MaximumTier, AllowMaps: bracket.Tier > 0, AllowLegendary: true,
                Completed: true, BossPool: bracket.Boss ? "warden" : string.Empty, Map: map);
            long equipment = 0, gold = 0, metals = 0, maps = 0, stones = 0, legendary = 0;
            var distribution = new int[42];
            for (int index = 0; index < samplesPerBracket; index++)
            {
                ulong rollSeed = seed ^ ((ulong)(bracketIndex + 1) << 56) ^ (ulong)index * 0x9e3779b97f4a7c15UL;
                DropTrace trace = DropFormula.RollAuditTrace(context, pack, rollSeed);
                equipment += trace.EquipmentCount;
                gold += trace.Gold;
                metals += trace.MetalCount;
                maps += trace.MapCount;
                stones += trace.SkillStoneCount;
                if (trace.LegendaryDropped) legendary++;
                distribution[Math.Clamp(trace.EquipmentCount, 0, distribution.Length - 1)]++;
            }
            results.Add(new AuditResult(bracket, samplesPerBracket,
                (double)equipment / samplesPerBracket, (double)gold / samplesPerBracket,
                (double)metals / samplesPerBracket, (double)maps / samplesPerBracket,
                (double)stones / samplesPerBracket, (double)legendary / samplesPerBracket,
                Percentile(distribution, samplesPerBracket, 10), Percentile(distribution, samplesPerBracket, 50),
                Percentile(distribution, samplesPerBracket, 90)));
        }
        return results;
    }

    public static void ValidateSustain(IReadOnlyList<AuditResult> results, double maximumDeviation = 0.05)
    {
        foreach ((string name, double target) in new[] { ("T1", 1.15), ("T6", 1.08), ("T11", 1.00), ("T16", 1.00), ("T20", 0.90) })
        {
            double actual = results.Single(result => result.Bracket.Name == name).AverageMaps;
            if (Math.Abs(actual - target) / target > maximumDeviation)
                throw new InvalidOperationException($"Resources sustain audit failed for {name}: {actual:F4}, target {target:F4}.");
        }
    }

    public static string RenderMarkdown(IReadOnlyList<AuditResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var text = new StringBuilder();
        text.AppendLine("# Resources 经济蒙特卡洛审计").AppendLine();
        text.AppendLine($"每个档位样本数：{results.FirstOrDefault()?.Samples ?? 0:N0}。种子固定，整数基点抽样可复现。").AppendLine();
        text.AppendLine("| 档位 | 装备/节点 | 装备第10/50/90百分位 | 金币/节点 | 金属/节点 | 地图/节点 | 技能石/节点 | 传奇率 |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (AuditResult result in results)
            text.AppendLine($"| {result.Bracket.Name} | {result.AverageEquipment:F3} | {result.EquipmentPercentile10}/{result.EquipmentPercentile50}/{result.EquipmentPercentile90} | " +
                $"{result.AverageGold:F3} | {result.AverageMetals:F3} | {result.AverageMaps:F4} | " +
                $"{result.AverageSkillStones:F4} | {result.LegendaryRate:0.000%} |");
        text.AppendLine().AppendLine("按 90 秒/节点（40 节点/小时）统一折算；实际每小时产出由构筑清图速度决定：").AppendLine();
        text.AppendLine("| 档位 | 装备/小时 | 金币/小时 | 金属/小时 | 地图/小时 | 传奇/小时 |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (AuditResult result in results)
            text.AppendLine($"| {result.Bracket.Name} | {result.AverageEquipment * 40:F2} | {result.AverageGold * 40:F2} | " +
                $"{result.AverageMetals * 40:F2} | {result.AverageMaps * 40:F2} | {result.LegendaryRate * 40:F3} |");
        text.AppendLine().AppendLine("## 审计结论").AppendLine();
        AuditResult? t1 = results.FirstOrDefault(result => result.Bracket.Name == "T1");
        AuditResult? boss = results.FirstOrDefault(result => result.Bracket.Name == "Boss");
        AuditResult? t16 = results.FirstOrDefault(result => result.Bracket.Name == "T16");
        AuditResult? t20 = results.FirstOrDefault(result => result.Bracket.Name == "T20");
        text.AppendLine($"- Atlas 无异界天赋续航目标：T1～T5 为 1.15、T6～T10 为 1.08、T11～T16 为 1.00、T17～T20 为 0.90；" +
            $"本次 T1/T16/T20 实测 {t1?.AverageMaps:F4}/{t16?.AverageMaps:F4}/{t20?.AverageMaps:F4}。");
        text.AppendLine($"- 普通地图传奇基准为 3.33%（约 1/30）；Boss 直掉基准为 8%，本次 Boss 实测 {boss?.LegendaryRate:0.000%}。");
        text.AppendLine("- 怪物数量先乘算实际怪物预算，物品数量再乘算装备、金币、金属、地图、技能石与玩法资源；首次/任务/固定 Boss 票券、异界解锁和保证传奇不受数量放大。");
        text.AppendLine($"- 按 90 秒/节点折算，T1 基线约 {t1?.AverageGold * 40:F0} 金币/小时；地图出售另按 Atlas 公开公式结算。");
        return text.ToString();
    }

    private static int Percentile(IReadOnlyList<int> distribution, int samples, int percentile)
    {
        int target = Math.Max(1, (samples * percentile + 99) / 100);
        int cumulative = 0;
        for (int value = 0; value < distribution.Count; value++)
        {
            cumulative += distribution[value];
            if (cumulative >= target) return value;
        }
        return distribution.Count - 1;
    }

    private static IReadOnlyList<AuditBracket> Mechanics()
    {
        var result = new List<AuditBracket>();
        foreach ((string name, MapRoute route, MapAltar altar) in new[]
                 {
                     ("深渊", MapRoute.Abyss, MapAltar.None), ("命能", MapRoute.LifeGarden, MapAltar.None),
                     ("赤誓", MapRoute.Safe, MapAltar.RedOath), ("苍誓", MapRoute.Safe, MapAltar.BlueOath),
                     ("战阵", MapRoute.Warfront, MapAltar.None),
                 })
        {
            result.Add(new($"{name}-普通", 94, 16, 63, route, false, 10_000, altar));
            result.Add(new($"{name}-高压", 94, 16, 100, route, false, 17_500, altar, FullAtlas: true));
            result.Add(new($"{name}-极限腐化", 100, 20, 160, route, true, 30_000, altar, FullAtlas: true, Corrupted: true));
        }
        return result;
    }
}

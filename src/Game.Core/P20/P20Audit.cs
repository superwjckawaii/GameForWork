using System.Text;
using GameForWork.Core.P1.World;

namespace GameForWork.Core.P20;

public sealed record P20AuditBracket(
    string Name,
    int MonsterLevel,
    int Tier,
    int Danger,
    MapRoute Route,
    bool Boss,
    int QuantityBasisPoints = 10_000);

public sealed record P20AuditResult(
    P20AuditBracket Bracket,
    int Samples,
    double AverageEquipment,
    double AverageGold,
    double AverageMetals,
    double AverageMaps,
    double AverageSkillStones,
    double LegendaryRate,
    int EquipmentP10,
    int EquipmentP50,
    int EquipmentP90);

public static class P20EconomyAudit
{
    public static IReadOnlyList<P20AuditBracket> StandardBrackets { get; } =
    [
        new("剧情", 35, 0, 25, MapRoute.Safe, false),
        new("T1", 70, 1, 13, MapRoute.Safe, false),
        new("T10", 84, 10, 42, MapRoute.Abyss, false),
        new("T16", 94, 16, 63, MapRoute.LifeGarden, false),
        new("T20", 100, 20, 80, MapRoute.Abyss, false),
        new("Boss", 100, 20, 100, MapRoute.Abyss, true),
    ];

    public static IReadOnlyList<P20AuditResult> Run(int samplesPerBracket = 100_000, ulong seed = 0x20ec0a11UL)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samplesPerBracket, 1);
        var results = new List<P20AuditResult>(StandardBrackets.Count);
        for (int bracketIndex = 0; bracketIndex < StandardBrackets.Count; bracketIndex++)
        {
            P20AuditBracket bracket = StandardBrackets[bracketIndex];
            IReadOnlyList<P20DefeatedEnemy> pack = P20DropFormula.SyntheticPack(bracket.MonsterLevel, bracket.Boss);
            var context = new P20LootContext($"audit-{bracket.Name}", bracket.MonsterLevel,
                bracket.QuantityBasisPoints, bracket.Danger, bracket.Route, bracket.Tier,
                P1MapItem.MaximumTier, AllowMaps: bracket.Tier > 0, AllowLegendary: true,
                Completed: true, BossPool: bracket.Boss ? "warden" : string.Empty);
            long equipment = 0, gold = 0, metals = 0, maps = 0, stones = 0, legendary = 0;
            var distribution = new int[42];
            for (int index = 0; index < samplesPerBracket; index++)
            {
                ulong rollSeed = seed ^ ((ulong)(bracketIndex + 1) << 56) ^ (ulong)index * 0x9e3779b97f4a7c15UL;
                P20DropTrace trace = P20DropFormula.RollAuditTrace(context, pack, rollSeed);
                equipment += trace.EquipmentCount;
                gold += trace.Gold;
                metals += trace.MetalCount;
                maps += trace.MapCount;
                stones += trace.SkillStoneCount;
                if (trace.LegendaryDropped) legendary++;
                distribution[Math.Clamp(trace.EquipmentCount, 0, distribution.Length - 1)]++;
            }
            results.Add(new P20AuditResult(bracket, samplesPerBracket,
                (double)equipment / samplesPerBracket, (double)gold / samplesPerBracket,
                (double)metals / samplesPerBracket, (double)maps / samplesPerBracket,
                (double)stones / samplesPerBracket, (double)legendary / samplesPerBracket,
                Percentile(distribution, samplesPerBracket, 10), Percentile(distribution, samplesPerBracket, 50),
                Percentile(distribution, samplesPerBracket, 90)));
        }
        return results;
    }

    public static string RenderMarkdown(IReadOnlyList<P20AuditResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var text = new StringBuilder();
        text.AppendLine("# P20 经济蒙特卡洛审计").AppendLine();
        text.AppendLine($"每个档位样本数：{results.FirstOrDefault()?.Samples ?? 0:N0}。种子固定，整数基点抽样可复现。").AppendLine();
        text.AppendLine("| 档位 | 装备/节点 | 装备 P10/P50/P90 | 金币/节点 | 金属/节点 | 地图/节点 | 技能石/节点 | 传奇率 |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (P20AuditResult result in results)
            text.AppendLine($"| {result.Bracket.Name} | {result.AverageEquipment:F3} | {result.EquipmentP10}/{result.EquipmentP50}/{result.EquipmentP90} | " +
                $"{result.AverageGold:F3} | {result.AverageMetals:F3} | {result.AverageMaps:F4} | " +
                $"{result.AverageSkillStones:F4} | {result.LegendaryRate:P3} |");
        text.AppendLine().AppendLine("按 90 秒/节点（40 节点/小时）统一折算；实际每小时产出由构筑清图速度决定：").AppendLine();
        text.AppendLine("| 档位 | 装备/小时 | 金币/小时 | 金属/小时 | 地图/小时 | 传奇/小时 |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (P20AuditResult result in results)
            text.AppendLine($"| {result.Bracket.Name} | {result.AverageEquipment * 40:F2} | {result.AverageGold * 40:F2} | " +
                $"{result.AverageMetals * 40:F2} | {result.AverageMaps * 40:F2} | {result.LegendaryRate * 40:F3} |");
        text.AppendLine().AppendLine("## 审计结论").AppendLine();
        P20AuditResult? t1 = results.FirstOrDefault(result => result.Bracket.Name == "T1");
        P20AuditResult? boss = results.FirstOrDefault(result => result.Bracket.Name == "Boss");
        text.AppendLine($"- 基础地图续航目标为 1.08；T1 实测 {t1?.AverageMaps:F4}，危险度会再提供小幅提升。");
        text.AppendLine($"- 普通地图传奇基准为 3.33%（约 1/30）；Boss 直掉基准为 8%，本次 Boss 实测 {boss?.LegendaryRate:P3}。");
        text.AppendLine("- 地图数量、金币、装备、金属、技能石、完成奖励与固定 Boss 碎片均读取地图数量加成；连接数独立抽取。");
        text.AppendLine("- 金币出售已改为公开估值的 5%。按 T1 标准约 1,097 金币/小时，现有建筑升级与附魔价格分档保持不变；P22 只根据完整构筑长稳结果做末次微调。");
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
}

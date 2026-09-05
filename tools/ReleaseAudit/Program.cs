using System.Text;
using GameForWork.Core.Release;

int samples = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 100;
string? output = args.Length > 1 ? args[1] : null;
IReadOnlyList<CombatBenchmarkResult> results = ReleaseTargets.RunCombatBenchmarks(samples);
var markdown = new StringBuilder()
    .AppendLine("# Release 六构筑空间战斗审计").AppendLine()
    .AppendLine($"每套构筑样本数：{samples}。全部调用正式 `SpatialCombatRunner`，固定种子可复现。").AppendLine()
    .AppendLine("| 构筑 | 阶段 | 胜利 | 成功率 | 平均节点时长 |")
    .AppendLine("|---|---|---:|---:|---:|");
foreach (CombatBenchmarkResult result in results)
    markdown.AppendLine($"| {result.DisplayName} | {(result.Endgame ? "T20/Boss" : "T1")} | " +
        $"{result.Victories}/{result.Samples} | {result.SuccessRate:0.0%} | {result.AverageDurationSeconds:F2}s |");
markdown.AppendLine().AppendLine("该审计用于跨版本回归，不把固定测试装备当作玩家实际掉落保证；死亡原因和地图全程仍由各层战斗测试及实机候选包检查。");

if (string.IsNullOrWhiteSpace(output)) Console.Write(markdown);
else
{
    string path = Path.GetFullPath(output);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, markdown.ToString());
    Console.WriteLine(path);
}

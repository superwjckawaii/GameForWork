using System.Text;
using GameForWork.Core.Harbor;

int samples = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 64;
IReadOnlyList<HarborBalanceResult> results = HarborBalanceAudit.Run(samples);
IReadOnlyList<string> failures = HarborBalanceAudit.Validate(results);
var text = new StringBuilder();
text.AppendLine("# 沉金港经济与构筑平衡审计").AppendLine();
text.AppendLine($"固定种子、每个构筑/难度 {samples:N0} 次；每次均调用正式 `HarborRunner.Run`，不使用属性评分判定。\n");
text.AppendLine("| 难度 | 构筑场景 | 费用 | 物品等级 | 成功率 | 平均耗时(s) | 中位耗时(s) | 样本 |");
text.AppendLine("|---:|---|---:|---:|---:|---:|---:|---:|");
foreach (HarborBalanceResult result in results)
    text.AppendLine($"| {result.Difficulty} | {result.Scenario} | {result.Fee:N0} | {result.ItemLevel} | {result.SuccessRate:0.0%} | {result.AverageDurationSeconds:0.00} | {result.MedianDurationSeconds:0.00} | {result.Samples:N0} |");
text.AppendLine().AppendLine("## 结论").AppendLine();
text.AppendLine("- 费用与物品等级直接来自正式三档配置；失败只消耗入口费用，成功奖励仍在统一宝箱事务中领取。");
text.AppendLine("- 高机动场景的耗时应低于重装低速场景；低输出场景可以暴露超时/失败边界，不能绕过实际战斗。");
text.AppendLine("- 普通地图 100,000 样本经济结果见 [Resources 经济审计](../docs/v0.4/Resources_ECONOMY_AUDIT.md)，36 套固定构筑结果见 [Builds 审计](../docs/v0.4/Builds_BUILD_AUDIT.md)。");
text.AppendLine(failures.Count == 0 ? "- 审计结构、配置一致性和样本完整性通过。" : $"- 审计失败：{string.Join('；', failures)}");
if (failures.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, failures));
if (args.Length > 1)
{
    string path = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    Console.WriteLine(path);
}
else
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.Write(text.ToString());
}

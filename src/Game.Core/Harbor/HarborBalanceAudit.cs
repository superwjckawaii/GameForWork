using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;

namespace GameForWork.Core.Harbor;

public sealed record HarborBalanceScenario(string Name, TeamBuild Build);

public sealed record HarborBalanceResult(
    int Difficulty,
    string Scenario,
    int Samples,
    int Successes,
    double SuccessRate,
    double AverageDurationSeconds,
    double MedianDurationSeconds,
    int Fee,
    int ItemLevel)
{
    public bool IsStable => Samples > 0 && Successes >= 0 && Successes <= Samples &&
        AverageDurationSeconds is >= 0 and <= 300;
}

/// <summary>
/// Runs a deterministic Harbor sweep over deliberately different combat profiles.
/// This is an audit tool, not a second gameplay path: every sample calls HarborRunner.Run.
/// </summary>
public static class HarborBalanceAudit
{
    public static IReadOnlyList<HarborBalanceScenario> Scenarios { get; } =
    [
        new("均衡", Build(100_000, 2_000, 2_000, 3_000, 10_000)),
        new("高机动低防御", Build(58_000, 850, 2_200, 3_300, 15_000)),
        new("重装低速", Build(150_000, 3_200, 1_700, 2_550, 7_000, HasShield: true, BlockChanceBasisPoints: 5_000)),
        new("低输出高生存", Build(150_000, 3_200, 70, 105, 9_000)),
    ];

    public static IReadOnlyList<HarborBalanceResult> Run(int samplesPerScenario = 64, ulong seed = 0x5a17b04dUL)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samplesPerScenario, 1);
        var results = new List<HarborBalanceResult>(HarborDifficulty.All.Count * Scenarios.Count);
        foreach (HarborDifficulty difficulty in HarborDifficulty.All)
        {
            foreach (HarborBalanceScenario scenario in Scenarios)
            {
                var durations = new List<double>(samplesPerScenario);
                int successes = 0;
                for (int index = 0; index < samplesPerScenario; index++)
                {
                    HarborRun run = HarborRunner.Run(
                        $"audit.harbor.{difficulty.Level}.{scenario.Name}.{index}",
                        difficulty.Level, scenario.Build,
                        seed ^ ((ulong)difficulty.Level << 48) ^ ((ulong)index * 0x9e3779b97f4a7c15UL));
                    if (run.Succeeded) successes++;
                    durations.Add(run.DurationMilliseconds / 1_000d);
                }
                durations.Sort();
                results.Add(new(difficulty.Level, scenario.Name, samplesPerScenario, successes,
                    (double)successes / samplesPerScenario,
                    durations.Average(), Median(durations), difficulty.Fee, difficulty.ItemLevel));
            }
        }
        return results;
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<HarborBalanceResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var failures = new List<string>();
        if (results.Count != HarborDifficulty.All.Count * Scenarios.Count)
            failures.Add("港口平衡审计结果数量不完整。");
        foreach (HarborBalanceResult result in results)
        {
            HarborDifficulty difficulty = HarborDifficulty.Get(result.Difficulty);
            if (result.Fee != difficulty.Fee || result.ItemLevel != difficulty.ItemLevel)
                failures.Add($"难度 {result.Difficulty} 的费用或物品等级与正式配置不一致。");
            if (!result.IsStable) failures.Add($"{result.Difficulty}/{result.Scenario} 输出无效。");
        }
        return failures;
    }

    private static double Median(IReadOnlyList<double> values) => values.Count % 2 == 1
        ? values[values.Count / 2]
        : (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2;

    private static TeamBuild Build(int life, int regeneration, int minimumDamage, int maximumDamage,
        int movementSpeed, bool HasShield = false, int BlockChanceBasisPoints = 0)
    {
        var sheet = new CharacterSheet(100, new CharacterAttributes(120, 100, 100, 100),
            new DefensiveEquipment(800, 500, HasShield ? 300 : 0),
            FlatMaximumLife: life, FlatLifeRegeneration: regeneration,
            FireResistanceBasisPoints: 7_500, ColdResistanceBasisPoints: 7_500,
            LightningResistanceBasisPoints: 7_500, VoidResistanceBasisPoints: 7_500,
            BlockChanceBasisPoints: BlockChanceBasisPoints);
        var weapon = new WeaponProfile("audit.harbor.weapon", minimumDamage, maximumDamage, 1_200, 500);
        var skill = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.Bleed, Level: 21, Quality: 20);
        return new TeamBuild(sheet, weapon, skill,
            FlatAccuracy: 100, IncreasedDamageBasisPoints: 2_000,
            IncreasedBleedChanceBasisPoints: 2_500, UseWarCry: true,
            MovementSpeedBasisPoints: movementSpeed, HasShield: HasShield,
            BlockChanceBasisPoints: BlockChanceBasisPoints, AlwaysHit: true);
    }
}

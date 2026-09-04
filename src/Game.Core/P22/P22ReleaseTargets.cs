using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P18;
using GameForWork.Core.P20;
using GameForWork.Core.P23;
using GameForWork.Core.P4;

namespace GameForWork.Core.P22;

public static class P22ReleaseTargets
{
    public const string Version = "0.3.0";
    public const int SaveFormatVersion = 24;
    public const long MaximumWorkingSetBytes = 700L * 1024 * 1024;
    public const long MaximumTwoHourGrowthBytes = 80L * 1024 * 1024;
    public const double MaximumTrayCpuPercent = 2.0;
    public const double MaximumOfflineSeconds = 3.0;
    public const double TargetSimulationTickMilliseconds = 2.0;
    public static IReadOnlyList<int> FontScaleMatrix { get; } = [80, 90, 100, 110, 120, 130, 140, 150];

    public static IReadOnlyList<string> ValidateEconomy(IReadOnlyList<P20AuditResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var failures = new List<string>();
        Check("T1", 1.05, 1.15);
        Check("T10", 1.05, 1.25);
        Check("T16", 0.90, 1.05);
        Check("T20", 0.90, 1.05);
        if (results.Single(result => result.Bracket.Name == "Boss").LegendaryRate is < 0.06 or > 0.10)
            failures.Add("Boss 传奇率未落在 6%～10%。");
        return failures;

        void Check(string bracket, double minimum, double maximum)
        {
            P20AuditResult result = results.Single(item => item.Bracket.Name == bracket);
            if (result.AverageMaps < minimum || result.AverageMaps > maximum)
                failures.Add($"{bracket} 地图续航 {result.AverageMaps:F4} 未落在 {minimum:F2}～{maximum:F2}。");
        }
    }

    public static IReadOnlyList<string> ValidateBenchmarkCatalog()
    {
        var failures = new List<string>();
        P18BenchmarkBuild[] catalog = P18BenchmarkBuilds.All.Concat(P231BenchmarkBuilds.All).ToArray();
        if (catalog.Length != 36) failures.Add("十八升华必须恰好维护三十六套基准构筑。");
        foreach (P18Ascendancy ascendancy in catalog.Select(build => build.Ascendancy).Distinct())
        {
            P18BenchmarkBuild[] builds = catalog.Where(build => build.Ascendancy == ascendancy).ToArray();
            if (builds.Length != 2 || builds.Count(build => build.EndgameGear) != 1)
                failures.Add($"{ascendancy} 必须包含一套开荒和一套终局构筑。");
            if (builds.Any(build => build.Nodes.Count != 8))
                failures.Add($"{ascendancy} 基准构筑必须封存 8 个升华节点。");
        }
        return failures;
    }

    public static IReadOnlyList<P22CombatBenchmarkResult> RunCombatBenchmarks(int samplesPerBuild = 12,
        ulong seed = 0x22ba771eUL)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samplesPerBuild, 1);
        var runner = new P4SpatialCombatRunner();
        var results = new List<P22CombatBenchmarkResult>(P18BenchmarkBuilds.All.Count);
        for (int buildIndex = 0; buildIndex < P18BenchmarkBuilds.All.Count; buildIndex++)
        {
            P18BenchmarkBuild definition = P18BenchmarkBuilds.All[buildIndex];
            P1TeamBuild build = CreateCombatBuild(definition);
            int victories = 0;
            long ticks = 0;
            for (int sample = 0; sample < samplesPerBuild; sample++)
            {
                P4NodeCombatResult result = runner.Run(new P4NodeCombatRequest(
                    build, sample + 1, definition.EndgameGear ? 100 : 70, definition.EndgameGear ? 14 : 10,
                    HasElite: true, HasBoss: definition.EndgameGear, AbyssRoute: sample % 2 == 0,
                    Formation: sample % 3, MaximumTicks: definition.EndgameGear ? 3_600 : 2_400),
                    seed ^ ((ulong)(buildIndex + 1) << 48) ^ (ulong)sample * 0x9e3779b97f4a7c15UL);
                if (result.Outcome == P1BattleOutcome.HeroVictory) victories++;
                ticks += result.Ticks;
            }
            results.Add(new P22CombatBenchmarkResult(definition.StableId, definition.DisplayName,
                definition.EndgameGear, samplesPerBuild, victories,
                ticks * P4SpatialCombatRunner.TickMilliseconds / 1000d / samplesPerBuild));
        }
        return results;
    }

    private static P1TeamBuild CreateCombatBuild(P18BenchmarkBuild definition)
    {
        bool endgame = definition.EndgameGear;
        bool bastion = definition.Ascendancy == P18Ascendancy.IronGuardian;
        CharacterSheet sheet = new(endgame ? 120 : 80,
            endgame ? new CharacterAttributes(520, 300, 260, 220) : new CharacterAttributes(300, 180, 160, 120),
            endgame ? new DefensiveEquipment(bastion ? 8_000 : 4_500, 1_200, bastion ? 2_400 : 600)
                : new DefensiveEquipment(bastion ? 3_000 : 1_800, 500, bastion ? 900 : 200),
            FlatMaximumLife: endgame ? 5_000 : 2_200,
            IncreasedMaximumLifeBasisPoints: endgame ? 8_000 : 4_000,
            IncreasedArmorBasisPoints: endgame ? 10_000 : 5_000,
            FireResistanceBasisPoints: 7_500, ColdResistanceBasisPoints: 7_500,
            LightningResistanceBasisPoints: 7_500, VoidResistanceBasisPoints: 7_500,
            BlockChanceBasisPoints: bastion ? (endgame ? 7_000 : 5_500) : 0,
            SpellSuppressionBasisPoints: bastion ? 0 : (endgame ? 7_500 : 4_000),
            FlatLifeRegeneration: endgame ? 180 : 60);
        WeaponProfile weapon = endgame
            ? new WeaponProfile($"{definition.StableId}.weapon", 760, 1_080, 1_650, 800)
            : new WeaponProfile($"{definition.StableId}.weapon", 250, 360, 1_450, 650);
        SkillSupport supports = definition.Ascendancy == P18Ascendancy.BloodFighter
            ? SkillSupport.Bleed | SkillSupport.DeepWound | SkillSupport.LifeLeech
            : definition.Ascendancy == P18Ascendancy.Warbreaker
                ? SkillSupport.Brutality | SkillSupport.Shockwave | SkillSupport.ArmorShatter
                : SkillSupport.Fortification | SkillSupport.BlockTrigger | SkillSupport.Vengeance;
        SkillConfiguration[] skills = definition.Skills
            .Select(id => new SkillConfiguration(id, SkillSupport.None))
            .Prepend(new SkillConfiguration(P1SkillIds.HeavyStrike, supports))
            .DistinctBy(skill => skill.SkillId)
            .ToArray();
        var ascendancy = new P18CombatProfile(definition.Ascendancy, definition.Nodes);
        sheet = P18AscendancyRules.ApplySheet(sheet, ascendancy);
        return new P1TeamBuild(sheet, weapon, skills[0],
            FlatAccuracy: endgame ? 5_000 : 2_500,
            IncreasedDamageBasisPoints: endgame ? 18_000 : 9_000,
            IncreasedCriticalChanceBasisPoints: endgame ? 6_000 : 2_000,
            IncreasedBleedChanceBasisPoints: definition.Ascendancy == P18Ascendancy.BloodFighter ? 8_000 : 0,
            LifeFlask: new LifeFlaskDefinition(endgame ? 1_600 : 700, 40, 10),
            AddedPhysicalDamage: endgame ? 260 : 80,
            MovementSpeedBasisPoints: endgame ? 13_000 : 11_000,
            ActiveSkills: skills,
            Flasks: [P1FlaskKind.Life, P1FlaskKind.Mana, P1FlaskKind.Armor],
            HasShield: bastion,
            BlockChanceBasisPoints: bastion ? (endgame ? 7_000 : 5_500) : 0,
            Ascendancy: ascendancy,
            CriticalMultiplierBasisPoints: endgame ? 20_000 : 16_000);
    }
}

public sealed record P22CombatBenchmarkResult(
    string StableId,
    string DisplayName,
    bool Endgame,
    int Samples,
    int Victories,
    double AverageDurationSeconds)
{
    public double SuccessRate => (double)Victories / Samples;
}

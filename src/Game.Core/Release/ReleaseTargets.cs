using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Economy;
using GameForWork.Core.Characters;
using GameForWork.Core.Spatial;

namespace GameForWork.Core.Release;

public static class ReleaseTargets
{
    public const string Version = "0.3.0";
    public const int SaveFormatVersion = Campaign.GameSession.CurrentFormatVersion;
    public const long MaximumWorkingSetBytes = 700L * 1024 * 1024;
    public const long MaximumTwoHourGrowthBytes = 80L * 1024 * 1024;
    public const double MaximumTrayCpuPercent = 2.0;
    public const double MaximumOfflineSeconds = 3.0;
    public const double TargetSimulationTickMilliseconds = 2.0;
    public static IReadOnlyList<int> FontScaleMatrix { get; } = [80, 90, 100, 110, 120, 130, 140, 150];

    public static IReadOnlyList<string> ValidateEconomy(IReadOnlyList<AuditResult> results)
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
            AuditResult result = results.Single(item => item.Bracket.Name == bracket);
            if (result.AverageMaps < minimum || result.AverageMaps > maximum)
                failures.Add($"{bracket} 地图续航 {result.AverageMaps:F4} 未落在 {minimum:F2}～{maximum:F2}。");
        }
    }

    public static IReadOnlyList<string> ValidateBenchmarkCatalog()
    {
        var failures = new List<string>();
        BenchmarkBuild[] catalog = WarriorBenchmarkBuilds.All.Concat(ClassBenchmarkBuilds.All).ToArray();
        if (catalog.Length != 36) failures.Add("十八升华必须恰好维护三十六套基准构筑。");
        foreach (Ascendancy ascendancy in catalog.Select(build => build.Ascendancy).Distinct())
        {
            BenchmarkBuild[] builds = catalog.Where(build => build.Ascendancy == ascendancy).ToArray();
            if (builds.Length != 2 || builds.Count(build => build.EndgameGear) != 1)
                failures.Add($"{ascendancy} 必须包含一套开荒和一套终局构筑。");
            if (builds.Any(build => build.Nodes.Count != 8))
                failures.Add($"{ascendancy} 基准构筑必须封存 8 个升华节点。");
        }
        return failures;
    }

    public static IReadOnlyList<CombatBenchmarkResult> RunCombatBenchmarks(int samplesPerBuild = 12,
        ulong seed = 0x22ba771eUL)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(samplesPerBuild, 1);
        var runner = new SpatialCombatRunner();
        var results = new List<CombatBenchmarkResult>(WarriorBenchmarkBuilds.All.Count);
        for (int buildIndex = 0; buildIndex < WarriorBenchmarkBuilds.All.Count; buildIndex++)
        {
            BenchmarkBuild definition = WarriorBenchmarkBuilds.All[buildIndex];
            TeamBuild build = CreateCombatBuild(definition);
            int victories = 0;
            long ticks = 0;
            for (int sample = 0; sample < samplesPerBuild; sample++)
            {
                NodeCombatResult result = runner.Run(new NodeCombatRequest(
                    build, sample + 1, definition.EndgameGear ? 100 : 70, definition.EndgameGear ? 14 : 10,
                    HasElite: true, HasBoss: definition.EndgameGear, AbyssRoute: sample % 2 == 0,
                    Formation: sample % 3, MaximumTicks: definition.EndgameGear ? 3_600 : 2_400),
                    seed ^ ((ulong)(buildIndex + 1) << 48) ^ (ulong)sample * 0x9e3779b97f4a7c15UL);
                if (result.Outcome == BattleOutcome.HeroVictory) victories++;
                ticks += result.Ticks;
            }
            results.Add(new CombatBenchmarkResult(definition.StableId, definition.DisplayName,
                definition.EndgameGear, samplesPerBuild, victories,
                ticks * SpatialCombatRunner.TickMilliseconds / 1000d / samplesPerBuild));
        }
        return results;
    }

    private static TeamBuild CreateCombatBuild(BenchmarkBuild definition)
    {
        bool endgame = definition.EndgameGear;
        bool bastion = definition.Ascendancy == Ascendancy.IronGuardian;
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
        SkillSupport supports = definition.Ascendancy == Ascendancy.BloodFighter
            ? SkillSupport.Bleed | SkillSupport.DeepWound | SkillSupport.LifeLeech
            : definition.Ascendancy == Ascendancy.Warbreaker
                ? SkillSupport.Brutality | SkillSupport.Shockwave | SkillSupport.ArmorShatter
                : SkillSupport.Fortification | SkillSupport.BlockTrigger | SkillSupport.Vengeance;
        SkillConfiguration[] skills = definition.Skills
            .Select(id => new SkillConfiguration(id, SkillSupport.None))
            .Prepend(new SkillConfiguration(SkillIds.HeavyStrike, supports))
            .DistinctBy(skill => skill.SkillId)
            .ToArray();
        var ascendancy = new CombatProfile(definition.Ascendancy, definition.Nodes);
        sheet = WarriorAscendancyRules.ApplySheet(sheet, ascendancy);
        return new TeamBuild(sheet, weapon, skills[0],
            FlatAccuracy: endgame ? 5_000 : 2_500,
            IncreasedDamageBasisPoints: endgame ? 18_000 : 9_000,
            IncreasedCriticalChanceBasisPoints: endgame ? 6_000 : 2_000,
            IncreasedBleedChanceBasisPoints: definition.Ascendancy == Ascendancy.BloodFighter ? 8_000 : 0,
            LifeFlask: new LifeFlaskDefinition(endgame ? 1_600 : 700, 40, 10),
            AddedPhysicalDamage: endgame ? 260 : 80,
            MovementSpeedBasisPoints: endgame ? 13_000 : 11_000,
            ActiveSkills: skills,
            Flasks: [FlaskKind.Life, FlaskKind.Mana, FlaskKind.Armor],
            HasShield: bastion,
            BlockChanceBasisPoints: bastion ? (endgame ? 7_000 : 5_500) : 0,
            Ascendancy: ascendancy,
            CriticalMultiplierBasisPoints: endgame ? 20_000 : 16_000);
    }
}

public sealed record CombatBenchmarkResult(
    string StableId,
    string DisplayName,
    bool Endgame,
    int Samples,
    int Victories,
    double AverageDurationSeconds)
{
    public double SuccessRate => (double)Victories / Samples;
}

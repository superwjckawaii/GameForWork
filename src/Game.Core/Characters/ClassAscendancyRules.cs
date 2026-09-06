using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Skills;
using GameForWork.Core.Ascendancies;

namespace GameForWork.Core.Characters;

public static class ClassNodeIds
{
    public const string MarksmanMultipleCore = "core.ascendancy.marksman.multiple.core";
    public const string SoulLegionCore = "core.ascendancy.soul_shepherd.legion.core";
    public const string CantorReservationCore = "core.ascendancy.spirit_cantor.reservation.core";
    public const string CantorAuraCore = "core.ascendancy.spirit_cantor.aura.core";
    public const string CantorBlessingCore = "core.ascendancy.spirit_cantor.blessing.core";
    public const string ElementalistConversionCore = "core.ascendancy.elementalist.conversion.core";
    public const string AegisMaximumCore = "core.ascendancy.aegis_mage.maximum.core";
    public const string AegisRechargeCore = "core.ascendancy.aegis_mage.recharge.core";
    public const string IdolTurretCore = "core.ascendancy.idol_forger.turret.core";
}

public enum PrimaryElement { Fire, Cold, Lightning }

public readonly record struct ProjectileProfile(
    int AdditionalProjectiles,
    int IncreasedProjectileSpeedBasisPoints,
    int MoreProjectileDamageBasisPoints,
    bool CanRepeatHitSameTarget);

public static class ClassAscendancyRules
{
    public static ProjectileProfile Projectile(CombatProfile profile) =>
        profile.Has(ClassNodeIds.MarksmanMultipleCore)
            ? new(2, 3_000, 6_500, true)
            : new(0, 0, 10_000, false);

    public static ResolvedSkill ApplyResolvedSkill(ResolvedSkill skill, SkillTag tags, CombatProfile profile)
    {
        if (!tags.HasFlag(SkillTag.Projectile)) return skill;
        ProjectileProfile projectile = Projectile(profile);
        if (projectile.AdditionalProjectiles == 0) return skill;
        return skill with
        {
            ProjectileCount = checked(skill.ProjectileCount + projectile.AdditionalProjectiles),
            ProjectileSpeedRawPerSecond = CombatRules.ApplyIncreased(
                skill.ProjectileSpeedRawPerSecond, projectile.IncreasedProjectileSpeedBasisPoints),
            DamageMultiplierBasisPoints = CombatRules.ApplyMore(
                skill.DamageMultiplierBasisPoints, [projectile.MoreProjectileDamageBasisPoints]),
        };
    }

    public static int MaximumMinions(int externalAdditionalMaximum, int slothLayers) =>
        Archetypes.CombatCaps.Clamp(Archetypes.CombatUnitKind.Minion, CombatLimits.MaximumMinions + Math.Max(0, externalAdditionalMaximum) + Math.Max(0, slothLayers));

    public static int IncreasedMinionDamageBasisPoints(int slothLayers, CombatProfile profile) =>
        profile.Has(ClassNodeIds.SoulLegionCore) ? checked(Math.Max(0, slothLayers) * 600) : 0;

    public static int ExtraPhysicalAsPrimaryElement(int originalPhysicalDamage, PrimaryElement element,
        CombatProfile profile)
    {
        _ = element;
        return profile.Has(ClassNodeIds.ElementalistConversionCore)
            ? checked(Math.Max(0, originalPhysicalDamage) * 5_000 / 10_000)
            : 0;
    }

    public static bool ConstructPrioritizes(EnemyRarity rarity, CombatProfile profile) =>
        profile.Has(ClassNodeIds.IdolTurretCore) && rarity is EnemyRarity.Rare or EnemyRarity.Boss;

    public static int ConstructDamageMultiplier(EnemyRarity rarity, CombatProfile profile) =>
        ConstructPrioritizes(rarity, profile) ? 15_000 : 10_000;
}

public static class ClassBenchmarkBuilds
{
    public static IReadOnlyList<BenchmarkBuild> All { get; } = AscendancyDefinitions.All.Where(path => path.Ascendancy is not (Ascendancy.BloodFighter or Ascendancy.IronGuardian or Ascendancy.Warbreaker))
        .SelectMany(path => new[]
        {
            Create(path, false, [0, 1, 2, 3]),
            Create(path, true, [2, 3, 4, 5]),
        }).ToArray();

    private static BenchmarkBuild Create(AscendancyData path, bool endgame, IReadOnlyList<int> directions)
    {
        string mode = endgame ? "endgame" : "entry";
        string displayMode = endgame ? "终局" : "开荒";
        string[] nodes = directions.SelectMany(direction => new[]
        {
            AscendancyDefinitions.Id(path.Ascendancy, path.Branches[direction].StableKey, NodeKind.Reinforcement),
            AscendancyDefinitions.Id(path.Ascendancy, path.Branches[direction].StableKey, NodeKind.Core),
        }).ToArray();
        return new($"core.benchmark.{path.StableKey}.{mode}",
            $"{AscendancyCatalog.DisplayName(path.Ascendancy)}·{displayMode}", path.Ascendancy, endgame,
            nodes, [], string.Join('、', directions.Select(direction => path.Branches[direction].CoreName)));
    }
}

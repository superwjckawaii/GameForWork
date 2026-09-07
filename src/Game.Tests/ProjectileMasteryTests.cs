using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class ProjectileMasteryTests
{
    private static PassiveModifiers Mastery(params int[] options) => PassiveModifiers.Empty with
    {
        MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.投射物.{option}")),
    };

    [Fact]
    public void HeavyProjectileChangesSpeedAndHitWithoutChangingRange()
    {
        ResolvedSkill baseline = Resolve();
        ResolvedSkill mastery = Resolve(0);

        Assert.Equal(baseline.RangeRaw, mastery.RangeRaw);
        Assert.Equal(baseline.ProjectileSpeedRawPerSecond / 2, mastery.ProjectileSpeedRawPerSecond);
        Assert.Equal(baseline.DamageMultiplierBasisPoints * 3 / 2, mastery.DamageMultiplierBasisPoints);
    }

    [Fact]
    public void TrackingAndSequentialVolleyExposeTheirActualActionRules()
    {
        ResolvedSkill tracking = Resolve(1);
        ResolvedSkill volley = Resolve(2);

        Assert.True(tracking.ProjectileMechanics!.TracksTargets);
        Assert.Equal(8_000, tracking.ProjectileMechanics.HitMultiplierBasisPoints);
        Assert.True(volley.ProjectileMechanics!.SequentialVolley);
        Assert.Equal(5_000, volley.ProjectileMechanics.HitMultiplierBasisPoints);
    }

    [Fact]
    public void PierceForkChainAndReturnUseStructuredProjectileRules()
    {
        ResolvedSkill pierce = Resolve(3);
        ResolvedSkill fork = Resolve(4);
        ResolvedSkill chain = Resolve(5);
        ResolvedSkill returning = Resolve(6);

        Assert.Equal(int.MaxValue, pierce.PierceCount);
        Assert.Equal(11_000, pierce.ProjectileMechanics!.PierceStepMultiplierBasisPoints);
        Assert.Equal(4, fork.ForkCount);
        Assert.Equal(6_000, fork.ProjectileMechanics!.ForkMultiplierBasisPoints);
        Assert.Equal(2, chain.MaximumChains);
        Assert.Equal(15_000, chain.ProjectileMechanics!.ChainRangeMultiplierBasisPoints);
        Assert.Equal(8_500, chain.ProjectileMechanics.ChainStepMultiplierBasisPoints);
        Assert.True(returning.Returns);
        Assert.Equal(7_500, returning.ProjectileMechanics!.ReturnMultiplierBasisPoints);
    }

    [Fact]
    public void MultipleProjectileMasteriesComposeInsteadOfReplacingEachOther()
    {
        ProjectileMechanics rules = ProjectileMasteryRules.Resolve(Mastery(0, 1, 2, 3, 4, 5, 6));

        Assert.Equal(6_000, rules.HitMultiplierBasisPoints);
        Assert.True(rules.TracksTargets);
        Assert.True(rules.SequentialVolley);
        Assert.True(rules.InfinitePierce);
        Assert.Equal(4, rules.ForkCount);
        Assert.Equal(2, rules.AdditionalChains);
        Assert.True(rules.Returns);
    }

    [Fact]
    public void ProjectileSupportsUseTheirOwnLevelsAndStagePenalties()
    {
        ResolvedSkill chain = CombatSkillRules.Resolve(new(SkillIds.SpiritBlade, SkillSupport.Chain, Level: 1), 1_000);
        ResolvedSkill pierce = CombatSkillRules.Resolve(new(SkillIds.SpiritBlade, SkillSupport.Pierce, Level: 21), 1_000);
        ResolvedSkill fork = CombatSkillRules.Resolve(new(SkillIds.SpiritBlade, SkillSupport.Fork, Level: 21, Quality: 20), 1_000);
        ResolvedSkill returning = CombatSkillRules.Resolve(new(SkillIds.SpiritBlade, SkillSupport.Return, Level: 21), 1_000);

        Assert.Equal(2, chain.MaximumChains);
        Assert.Equal(7_500, chain.DamageMultiplierBasisPoints);
        Assert.Equal(4, pierce.PierceCount);
        Assert.Equal(9_200, pierce.ProjectileMechanics!.PierceStepMultiplierBasisPoints);
        Assert.Equal(4, fork.ForkCount);
        Assert.Equal(8_500, fork.ProjectileMechanics!.ForkMultiplierBasisPoints);
        Assert.True(returning.Returns);
        Assert.Equal(9_000, returning.ProjectileMechanics!.ReturnMultiplierBasisPoints);
        ProjectileMechanics combined = ProjectileMasteryRules.Resolve(Mastery(3, 4, 5, 6));
        int staged = combined.AfterPierce(20_000);
        staged = combined.AfterFork(staged);
        staged = combined.AfterChain(staged);
        Assert.Equal(8_415, combined.OnReturn(staged));
    }

    [Fact]
    public void SequentialVolleyOccupiesTheSameLongerActionInSimulationAndPreview()
    {
        var configuration = new SkillConfiguration(SkillIds.SpiritBlade, SkillSupport.MultipleProjectiles);
        ResolvedSkill ordinary = CombatSkillRules.Resolve(configuration, 1_000);
        ResolvedSkill sequential = CombatSkillRules.Resolve(configuration, 1_000, Mastery(2));
        TeamBuild ordinaryBuild = Build(PassiveModifiers.Empty, configuration);
        TeamBuild sequentialBuild = Build(Mastery(2), configuration);
        SkillTag tags = SkillDefinitions.Get(SkillIds.SpiritBlade).Tags;

        int ordinaryDelay = CombatSkillRules.ActionDelay(ordinaryBuild, ordinary, tags);
        int sequentialDelay = CombatSkillRules.ActionDelay(sequentialBuild, sequential, tags);
        Assert.Equal(ordinaryDelay + Math.Max(1, ordinaryDelay * 15 / 100) * 2, sequentialDelay);
        Assert.True(CombatSkillRules.ActionFrequencyMilliPerSecond(sequentialBuild, sequential, tags) <
                    CombatSkillRules.ActionFrequencyMilliPerSecond(ordinaryBuild, ordinary, tags));
    }

    [Fact]
    public void SequentialVolleyLaunchesEveryProjectileAtThePrimaryTargetOverTime()
    {
        var configuration = new SkillConfiguration(SkillIds.SpiritBlade, SkillSupport.MultipleProjectiles);
        TeamBuild build = Build(Mastery(2), configuration);
        NodeCombatResult result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0,
            MaximumTicks: 160, EnemyPool: [Enemies.CorruptedWorker with
            { Life = 100_000_000, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0, MovementSpeedRawPerSecond = 0 }]), 731);
        SpatialEvent[] projectiles = result.Events.Where(item =>
            item.Detail.Contains("projectile:outbound", StringComparison.Ordinal)).Take(3).ToArray();

        Assert.Equal(3, projectiles.Length);
        Assert.Single(projectiles.Select(item => item.TargetId).Distinct());
        Assert.True(projectiles.Select(item => item.AtMilliseconds).Distinct().Count() >= 2,
            string.Join(Environment.NewLine, projectiles.Select(item => $"{item.AtMilliseconds}: {item.Detail}")));
    }

    [Fact]
    public void ProjectileMasteriesNoLongerInjectUnrelatedFallbackStats()
    {
        PassiveNodeDefinition node = Assert.Single(PassiveTree.Nodes.Where(candidate =>
            candidate.MasteryGroup == "builds.mastery.投射物").Take(1));
        Assert.All(PassiveTree.MasteryOptions(node), effect => Assert.Equal(0, effect.Value));
    }

    private static ResolvedSkill Resolve(params int[] options) => CombatSkillRules.Resolve(
        new(SkillIds.SpiritBlade, SkillSupport.None), 1_000, Mastery(options));

    private static TeamBuild Build(PassiveModifiers passives, SkillConfiguration configuration) => new(
        new(20, new(100, 100, 100, 100), new(100, 0, 100), FlatMaximumLife: 1_000, FlatMaximumMana: 1_000),
        new("test", 10, 20, 1_000, 500), new(SkillIds.HeavyStrike, SkillSupport.None), UseWarCry: false, AlwaysHit: true,
        ActiveSkills: [configuration], PassiveProfile: passives);
}

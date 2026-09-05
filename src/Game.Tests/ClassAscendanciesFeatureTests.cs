using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Characters;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;

public sealed class ClassAscendanciesFeatureTests
{
    [Fact]
    public void FifteenNewPathsContributeExactlyOneHundredAndEightyNodes()
    {
        Assert.Equal(15, ClassAscendancyCatalog.Paths.Count);
        Assert.Equal(180, ClassAscendancyCatalog.Nodes.Count);
        Assert.All(ClassAscendancyCatalog.Paths, path =>
        {
            Assert.Equal(6, path.Branches.Count);
            Assert.Equal(12, WarriorAscendancyCatalog.For(path.Ascendancy).Count);
            Assert.True(WarriorAscendancyCatalog.IsImplemented(path.Ascendancy));
        });
        Assert.Equal(216, WarriorAscendancyCatalog.Nodes.Select(node => node.StableId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void IncreasedMoreAndTotalReductionUseTheirConfirmedMultiplierGroups()
    {
        Assert.Equal(150, ModifierMath.ApplyIncreased(100, 2_000, 3_000));
        Assert.Equal(156, ModifierMath.ApplyMore(100, 12_000, 13_000));
        Assert.Equal(48, ModifierMath.ApplyTotalReduction(100, 8_000, 6_000));
    }

    [Fact]
    public void MarksmanMultipleProjectilesStacksWithSupportAndAllowsShotgunning()
    {
        CombatProfile profile = Profile(Ascendancy.Marksman, ClassNodeIds.MarksmanMultipleCore);
        ResolvedSkill baseSkill = CombatSkillRules.Resolve(
            new SkillConfiguration(SkillIds.SpiritBlade, SkillSupport.MultipleProjectiles), 1_000);

        ResolvedSkill result = WarriorAscendancyRules.ApplySkillCost(
            baseSkill, SkillDefinitions.Get(SkillIds.SpiritBlade).Tags, 1_000, profile);

        Assert.Equal(5, result.ProjectileCount);
        Assert.Equal(15_600, result.ProjectileSpeedRawPerSecond);
        Assert.Equal(4_875, result.DamageMultiplierBasisPoints);
        Assert.True(ClassAscendancyRules.Projectile(profile).CanRepeatHitSameTarget);
    }

    [Fact]
    public void LegionAddsTwoToEveryOtherMaximumSourceAndOnlyScalesAboveEight()
    {
        CombatProfile profile = Profile(Ascendancy.SoulShepherd, ClassNodeIds.SoulLegionCore);

        Assert.Equal(8, ClassAscendancyRules.MaximumMinions(0, profile));
        Assert.Equal(12, ClassAscendancyRules.MaximumMinions(4, profile));
        Assert.Equal(0, ClassAscendancyRules.IncreasedMinionDamageBasisPoints(8, profile));
        Assert.Equal(1_500, ClassAscendancyRules.IncreasedMinionDamageBasisPoints(9, profile));
        Assert.Equal(6_000, ClassAscendancyRules.IncreasedMinionDamageBasisPoints(12, profile));
    }

    [Fact]
    public void CantorCoreNodesApplyMultiplicativeReservationAdditiveAuraAndOneMercenary()
    {
        CombatProfile profile = Profile(Ascendancy.SpiritCantor,
            ClassNodeIds.CantorReservationCore, ClassNodeIds.CantorAuraCore, ClassNodeIds.CantorBlessingCore);

        AuraProfile aura = ClassAscendancyRules.Aura(profile);
        Assert.Equal(6_000, aura.ReservationMultiplierBasisPoints);
        Assert.Equal(5_000, aura.IncreasedEffectBasisPoints);
        Assert.Equal(1, aura.AdditionalHeroPartyMercenaries);
    }

    [Fact]
    public void ElementalistGainsHalfOriginalPhysicalAsTheSelectedPrimaryElement()
    {
        CombatProfile profile = Profile(Ascendancy.Elementalist, ClassNodeIds.ElementalistConversionCore);

        Assert.Equal(500, ClassAscendancyRules.ExtraPhysicalAsPrimaryElement(
            1_000, PrimaryElement.Fire, profile));
        Assert.Equal(500, ClassAscendancyRules.ExtraPhysicalAsPrimaryElement(
            1_000, PrimaryElement.Cold, profile));
        Assert.Equal(500, ClassAscendancyRules.ExtraPhysicalAsPrimaryElement(
            1_000, PrimaryElement.Lightning, profile));
    }

    [Fact]
    public void AegisMaximumAndRechargeCoreNodesUseMoreAndIncreasedCorrectly()
    {
        CombatProfile profile = Profile(Ascendancy.AegisMage,
            ClassNodeIds.AegisMaximumCore, ClassNodeIds.AegisRechargeCore);
        var shield = new EnergyShieldState(100, profile);

        Assert.Equal(130, shield.Maximum);
        Assert.Equal(20, shield.RechargeDelay);
        Assert.Equal(3_000, shield.RechargeRateBasisPointsPerSecond);
        Assert.Equal(0, shield.AbsorbHit(130));
        for (int tick = 0; tick < 19; tick++) shield.AdvanceTick();
        Assert.False(shield.IsRecharging);
        shield.AdvanceTick();
        Assert.True(shield.IsRecharging);
    }

    [Theory]
    [InlineData(EnemyRarity.Normal, false, 10000)]
    [InlineData(EnemyRarity.Magic, false, 10000)]
    [InlineData(EnemyRarity.Rare, true, 15000)]
    [InlineData(EnemyRarity.Boss, true, 15000)]
    public void TurretOnlyPrioritizesAndMultipliesRareAndBoss(
        EnemyRarity rarity, bool prioritized, int multiplier)
    {
        CombatProfile profile = Profile(Ascendancy.IdolForger, ClassNodeIds.IdolTurretCore);
        Assert.Equal(prioritized, ClassAscendancyRules.ConstructPrioritizes(rarity, profile));
        Assert.Equal(multiplier, ClassAscendancyRules.ConstructDamageMultiplier(rarity, profile));
    }

    [Fact]
    public void EveryNewPathHasTwoEightPointBenchmarkRoutesUsingOnlyItsOwnNodes()
    {
        foreach (PathSpec path in ClassAscendancyCatalog.Paths)
        {
            BenchmarkBuild[] builds = ClassBenchmarkBuilds.All
                .Where(build => build.Ascendancy == path.Ascendancy).ToArray();
            Assert.Equal(2, builds.Length);
            Assert.All(builds, build =>
            {
                Assert.Equal(8, build.Nodes.Count);
                Assert.All(build.Nodes, id => Assert.Equal(path.Ascendancy, WarriorAscendancyCatalog.Get(id).Ascendancy));
            });
        }
    }

    private static CombatProfile Profile(Ascendancy ascendancy, params string[] cores)
    {
        string[] allocated = cores.SelectMany(core => new[] { WarriorAscendancyCatalog.Get(core).PrerequisiteId!, core })
            .Distinct(StringComparer.Ordinal).ToArray();
        return new(ascendancy, allocated);
    }
}

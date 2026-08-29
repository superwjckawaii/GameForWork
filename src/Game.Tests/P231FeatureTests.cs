using GameForWork.Core.P1.Combat;
using GameForWork.Core.P18;
using GameForWork.Core.P23;
using GameForWork.Core.P6;

namespace GameForWork.Tests;

public sealed class P231FeatureTests
{
    [Fact]
    public void FifteenNewPathsContributeExactlyOneHundredAndEightyNodes()
    {
        Assert.Equal(15, P231AscendancyCatalog.Paths.Count);
        Assert.Equal(180, P231AscendancyCatalog.Nodes.Count);
        Assert.All(P231AscendancyCatalog.Paths, path =>
        {
            Assert.Equal(6, path.Branches.Count);
            Assert.Equal(12, P18AscendancyCatalog.For(path.Ascendancy).Count);
            Assert.True(P18AscendancyCatalog.IsImplemented(path.Ascendancy));
        });
        Assert.Equal(216, P18AscendancyCatalog.Nodes.Select(node => node.StableId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void IncreasedMoreAndTotalReductionUseTheirConfirmedMultiplierGroups()
    {
        Assert.Equal(150, P231ModifierMath.ApplyIncreased(100, 2_000, 3_000));
        Assert.Equal(156, P231ModifierMath.ApplyMore(100, 12_000, 13_000));
        Assert.Equal(48, P231ModifierMath.ApplyTotalReduction(100, 8_000, 6_000));
    }

    [Fact]
    public void MarksmanMultipleProjectilesStacksWithSupportAndAllowsShotgunning()
    {
        P18CombatProfile profile = Profile(P18Ascendancy.Marksman, P231NodeIds.MarksmanMultipleCore);
        P6ResolvedSkill baseSkill = P6CombatSkillRules.Resolve(
            new SkillConfiguration(P1SkillIds.SpiritBlade, SkillSupport.MultipleProjectiles), 1_000);

        P6ResolvedSkill result = P18AscendancyRules.ApplySkillCost(
            baseSkill, P1Skills.Get(P1SkillIds.SpiritBlade).Tags, 1_000, profile);

        Assert.Equal(5, result.ProjectileCount);
        Assert.Equal(13_000, result.ProjectileSpeedRawPerSecond);
        Assert.Equal(5_200, result.DamageMultiplierBasisPoints);
        Assert.True(P231AscendancyRules.Projectile(profile).CanRepeatHitSameTarget);
    }

    [Fact]
    public void LegionAddsTwoToEveryOtherMaximumSourceAndOnlyScalesAboveEight()
    {
        P18CombatProfile profile = Profile(P18Ascendancy.SoulShepherd, P231NodeIds.SoulLegionCore);

        Assert.Equal(8, P231AscendancyRules.MaximumMinions(0, profile));
        Assert.Equal(12, P231AscendancyRules.MaximumMinions(4, profile));
        Assert.Equal(0, P231AscendancyRules.IncreasedMinionDamageBasisPoints(8, profile));
        Assert.Equal(1_500, P231AscendancyRules.IncreasedMinionDamageBasisPoints(9, profile));
        Assert.Equal(6_000, P231AscendancyRules.IncreasedMinionDamageBasisPoints(12, profile));
    }

    [Fact]
    public void CantorCoreNodesApplyMultiplicativeReservationAdditiveAuraAndOneMercenary()
    {
        P18CombatProfile profile = Profile(P18Ascendancy.SpiritCantor,
            P231NodeIds.CantorReservationCore, P231NodeIds.CantorAuraCore, P231NodeIds.CantorBlessingCore);

        P231AuraProfile aura = P231AscendancyRules.Aura(profile);
        Assert.Equal(6_000, aura.ReservationMultiplierBasisPoints);
        Assert.Equal(5_000, aura.IncreasedEffectBasisPoints);
        Assert.Equal(1, aura.AdditionalHeroPartyMercenaries);
    }

    [Fact]
    public void ElementalistGainsHalfOriginalPhysicalAsTheSelectedPrimaryElement()
    {
        P18CombatProfile profile = Profile(P18Ascendancy.Elementalist, P231NodeIds.ElementalistConversionCore);

        Assert.Equal(500, P231AscendancyRules.ExtraPhysicalAsPrimaryElement(
            1_000, P231PrimaryElement.Fire, profile));
        Assert.Equal(500, P231AscendancyRules.ExtraPhysicalAsPrimaryElement(
            1_000, P231PrimaryElement.Cold, profile));
        Assert.Equal(500, P231AscendancyRules.ExtraPhysicalAsPrimaryElement(
            1_000, P231PrimaryElement.Lightning, profile));
    }

    [Fact]
    public void AegisMaximumAndRechargeCoreNodesUseMoreAndIncreasedCorrectly()
    {
        P18CombatProfile profile = Profile(P18Ascendancy.AegisMage,
            P231NodeIds.AegisMaximumCore, P231NodeIds.AegisRechargeCore);
        var shield = new P23EnergyShieldState(100, profile);

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
    [InlineData(EnemyRarity.Rare, true, 13000)]
    [InlineData(EnemyRarity.Boss, true, 13000)]
    public void TurretOnlyPrioritizesAndMultipliesRareAndBoss(
        EnemyRarity rarity, bool prioritized, int multiplier)
    {
        P18CombatProfile profile = Profile(P18Ascendancy.IdolForger, P231NodeIds.IdolTurretCore);
        Assert.Equal(prioritized, P231AscendancyRules.ConstructPrioritizes(rarity, profile));
        Assert.Equal(multiplier, P231AscendancyRules.ConstructDamageMultiplier(rarity, profile));
    }

    [Fact]
    public void EveryNewPathHasTwoEightPointBenchmarkRoutesUsingOnlyItsOwnNodes()
    {
        foreach (P231PathSpec path in P231AscendancyCatalog.Paths)
        {
            P18BenchmarkBuild[] builds = P231BenchmarkBuilds.All
                .Where(build => build.Ascendancy == path.Ascendancy).ToArray();
            Assert.Equal(2, builds.Length);
            Assert.All(builds, build =>
            {
                Assert.Equal(8, build.Nodes.Count);
                Assert.All(build.Nodes, id => Assert.Equal(path.Ascendancy, P18AscendancyCatalog.Get(id).Ascendancy));
            });
        }
    }

    private static P18CombatProfile Profile(P18Ascendancy ascendancy, params string[] cores)
    {
        string[] allocated = cores.SelectMany(core => new[] { P18AscendancyCatalog.Get(core).PrerequisiteId!, core })
            .Distinct(StringComparer.Ordinal).ToArray();
        return new(ascendancy, allocated);
    }
}

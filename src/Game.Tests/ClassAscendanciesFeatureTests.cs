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
    public void CantorSpiritIsAssembledBeforeAuraEffectAndDoesNotReduceReservation()
    {
        CombatProfile profile = Profile(Ascendancy.SpiritCantor,
            ClassNodeIds.CantorReservationCore, ClassNodeIds.CantorAuraCore, ClassNodeIds.CantorBlessingCore);

        var assembled = GameForWork.Core.Campaign.Items.CharacterBuildAssembler.Assemble(1, new(0, 0, 80, 0), new(), new(),
            new(SkillIds.HeavyStrike, SkillSupport.None), ascendancy: profile);
        Assert.Equal(216, assembled.Sheet.Attributes.Spirit);
        Assert.Equal(600, assembled.Sheet.FlatSpiritBarrier);
        var team = new GameForWork.Core.Campaign.World.TeamBuild(assembled.Sheet, new("test", 1, 1, 1_000, 0),
            new(SkillIds.HeavyStrike, SkillSupport.None), Ascendancy: profile,
            ActiveSkills: [new(SkillIds.IronOathBanner, SkillSupport.None)]);
        var aura = GameForWork.Core.Combat.AuraCombatProfile.Resolve(team);
        Assert.Equal((assembled.Sheet.MaximumMana().Value * 1_500 + 9_999) / 10_000, aura.ReservedMana);
        Assert.Equal(11_460, aura.Build.Sheet.IncreasedArmorBasisPoints);
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
        var build = GameForWork.Core.Campaign.Items.CharacterBuildAssembler.Assemble(1, new(0, 0, 0, 50), new(), new(),
            new(SkillIds.HeavyStrike, SkillSupport.None), ascendancy: profile);
        var shield = new ResourceState(build.Sheet, ascendancy: profile);
        Assert.Equal(156, shield.MaximumShield);
        Assert.Equal(93, shield.MaximumOvercharge);
        Assert.Equal(9_500, build.Sheet.IncreasedShieldRechargeRateBasisPoints);
        shield.ApplyEnemyDamage(shield.MaximumShield, true, 0);
        for (int tick = 1; tick < 20; tick++) shield.AdvanceRegenerationTick(tick);
        Assert.Equal(0, shield.Shield);
        shield.AdvanceRegenerationTick(20);
        Assert.True(shield.Shield > 0);
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

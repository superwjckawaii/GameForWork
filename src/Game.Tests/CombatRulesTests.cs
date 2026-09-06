using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Simulation;
using GameForWork.Core.Spatial;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Equipment;

namespace GameForWork.Tests;

public sealed class CombatRulesTests
{
    [Fact]
    public void LegendaryHeavyStrikeUsesSlowerProfileAndActualAftershock()
    {
        var weapon = new WeaponProfile("legendary-test", 100, 100, 1_000, 0);
        var config = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        var normal = SkillRules.BuildHeavyStrike(config, weapon, 500);
        var profile = LegendaryRules.ApplyToHeavyStrike(normal, Legendary.EchoingOathbreakerRule);
        var build = new TeamBuild(new(10, new(100, 100, 100, 100), new(500, 100, 100), FlatMaximumLife: 500),
            weapon, config, FlatAccuracy: 1_000, UseWarCry: false, HeavyStrikeProfile: profile,
            ActiveSkills: [config], AlwaysHit: true, CombatEquipment: EquipmentCombatLoadout.Empty with
            { LegendaryIds = [EquipmentCatalog.LegendaryItems.Single(item => item.DisplayName == "回响破誓者").Id] });
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, MaximumTicks: 150,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 42);
        Assert.Contains(result.Events, item => item.Detail == "equipment:回响破誓者" && item.Value > 0);
        Assert.True(profile.AttackIntervalTicks > normal.AttackIntervalTicks);
    }

    [Fact]
    public void IronOathStartingResourcesMatchSpecification()
    {
        CharacterSheet sheet = StartingSheet();
        Assert.Equal(108, sheet.MaximumLife().Value);
        Assert.Equal(62, sheet.MaximumMana().Value);
        Assert.Equal(20, sheet.MaximumShield().Value);
        Assert.Equal(20, sheet.Accuracy().Value);
        Assert.Equal(10, sheet.Evasion().Value);
        Assert.Equal(400, sheet.AttackDamageIncreaseFromPhysique().Value);
        Assert.Equal(200, sheet.AilmentDurationReductionBasisPoints().Value);
        Assert.Equal(500, sheet.ShieldRecoverySpeedIncreaseBasisPoints().Value);
    }

    [Fact]
    public void HitChanceUsesFiveAndOneHundredPercentCaps()
    {
        Assert.Equal(500, DamageRules.HitChance(1, 10_000, false).Value);
        Assert.Equal(10_000, DamageRules.HitChance(10_000, 0, false).Value);
        Assert.Equal(10_000, DamageRules.HitChance(0, 10_000, true).Value);
    }

    [Fact]
    public void ArmorUsesPerHitFormula()
    {
        CalculatedValue result = DamageRules.ArmorReduction(100, 20);
        Assert.Equal(5_000, result.Value);
        Assert.Single(result.Steps);
    }

    [Fact]
    public void DamagePipelineAppliesIncreaseMoreCriticalArmorAndBleedInOrder()
    {
        var weapon = new WeaponProfile("test", 10, 10, 1_000, 10_000);
        var request = new DamageRequest(
            weapon,
            IncreasedDamageBasisPoints: 10_000,
            MoreDamageMultipliersBasisPoints: [14_000],
            CriticalChanceBasisPoints: 10_000,
            TargetArmor: 0,
            IsSpell: true,
            BleedChanceBasisPoints: 10_000);

        DamageResult result = DamageRules.Resolve(request, new Pcg32(42));

        Assert.True(result.Hit);
        Assert.True(result.Critical);
        Assert.Equal(42, result.PreMitigationPhysicalDamage);
        Assert.Equal(42, result.FinalPhysicalDamage);
        Assert.True(result.AppliedBleed);
        Assert.Equal(29, result.BleedTotalDamage);
        Assert.Contains(result.DamageTrace.Steps, step => step.Label == "护甲缓解");
    }

    [Fact]
    public void DamageFirstConsumesShieldAndInterruptsRecovery()
    {
        var sheet = new CharacterSheet(1, new CharacterAttributes(20, 10, 10, 10), new DefensiveEquipment(0, 0, 100));
        var resources = new ResourceState(sheet);
        resources.ApplyDamage(50, 0);
        Assert.Equal(70, resources.Shield);
        Assert.Equal(resources.MaximumLife, resources.Life);

        for (int tick = 1; tick < 40; tick++)
        {
            resources.AdvanceRegenerationTick(tick);
        }

        Assert.Equal(70, resources.Shield);
        resources.AdvanceRegenerationTick(40);
        Assert.True(resources.Shield > 70);
    }

    [Fact]
    public void HeavyStrikeSupportsModifyRangeSpeedCostAndDamage()
    {
        CharacterSheet sheet = StartingSheet();
        SkillUseProfile profile = SkillRules.BuildHeavyStrike(
            new SkillConfiguration(
                SkillIds.HeavyStrike,
                SkillSupport.IncreasedArea | SkillSupport.AttackSpeed | SkillSupport.Bleed | SkillSupport.LifeCost),
            Weapons.RustedGreatsword,
            sheet.MaximumLife().Value);

        Assert.Equal(0, profile.ManaCost);
        Assert.Equal(8, profile.LifeCost);
        Assert.Equal(2_025, profile.RangeRaw);
        Assert.Equal(14, profile.AttackIntervalTicks);
        Assert.Equal(6_000, profile.BleedChanceBasisPoints);
        Assert.Equal([14_000, 9_000, 13_000], profile.MoreDamageMultipliersBasisPoints);
        Assert.True(SkillDefinitions.HeavyStrike.Tags.HasFlag(SkillTag.Physical));
        Assert.True(SkillDefinitions.HeavyStrike.Tags.HasFlag(SkillTag.Strike));
        Assert.True(SkillDefinitions.WarCry.Tags.HasFlag(SkillTag.WarCry));
    }

    [Fact]
    public void LifeCostCannotKillCaster()
    {
        var sheet = new CharacterSheet(1, new CharacterAttributes(0, 0, 0, 0), new DefensiveEquipment(0, 0, 0));
        var resources = new ResourceState(sheet);
        resources.ApplyDamage(resources.MaximumLife - 5, 0);
        Assert.False(resources.TryPayLifeCost(5));
        Assert.Equal(5, resources.Life);
    }

    [Fact]
    public void WarCryEmpowersThreeStrikesAndEchoChangesFourStrikeMultiplier()
    {
        var normal = new WarCryState();
        var normalResources = new ResourceState(StartingSheet());
        Assert.True(normal.TryActivate(normalResources, 0));
        Assert.Equal(12_500, normal.ConsumeHeavyStrikeMultiplier(1));
        Assert.Equal(12_500, normal.ConsumeHeavyStrikeMultiplier(2));
        Assert.Equal(12_500, normal.ConsumeHeavyStrikeMultiplier(3));
        Assert.Equal(10_000, normal.ConsumeHeavyStrikeMultiplier(4));

        var echo = new WarCryState { EchoNotableAllocated = true };
        var echoResources = new ResourceState(StartingSheet());
        Assert.True(echo.TryActivate(echoResources, 0));
        Assert.Equal([12_000, 12_000, 12_000, 12_000],
            Enumerable.Range(1, 4).Select(tick => echo.ConsumeHeavyStrikeMultiplier(tick)));
    }

    [Fact]
    public void SameSourceBleedKeepsHighestLayerByDefault()
    {
        var bleeds = new BleedCollection();
        bleeds.Apply(1, 50, 0, 100);
        bleeds.Apply(1, 40, 1, 100);
        bleeds.Apply(1, 60, 2, 100);
        Assert.Single(bleeds.Instances);
        Assert.Equal(60, bleeds.Instances[0].TotalDamage);
    }

    [Fact]
    public void DeepWoundKeepsTwoReducedLayers()
    {
        var bleeds = new BleedCollection(deepWoundAllocated: true);
        bleeds.Apply(1, 100, 0, 100);
        bleeds.Apply(1, 80, 1, 100);
        bleeds.Apply(1, 50, 2, 100);
        Assert.Equal(2, bleeds.Instances.Count);
        Assert.Equal([48, 60], bleeds.Instances.Select(item => item.TotalDamage).Order());
    }

    [Fact]
    public void AreaLevelScalingAndAbyssRouteUseSpecifiedMultipliers()
    {
        ScaledEnemy worker = EnemyRules.Scale(Enemies.CorruptedWorker, 10);
        Assert.Equal(91, worker.Life);
        Assert.Equal(7, worker.MinimumPhysicalDamage);
        Assert.Equal(11, worker.MaximumPhysicalDamage);
        Assert.Equal(4, worker.Armor);
        Assert.Equal(4, EnemyRules.ThreatBudget(10));

        ScaledEnemy abyss = EnemyRules.Scale(Enemies.CorruptedWorker, 1, abyssRoute: true);
        Assert.Equal(42, abyss.Life);
        Assert.Equal(4, abyss.MinimumPhysicalDamage);
        Assert.Equal(6, abyss.MaximumPhysicalDamage);
    }

    [Fact]
    public void EliteAlwaysRollsTwoDistinctAffixesDeterministically()
    {
        IReadOnlyList<EliteAffix> first = EnemyRules.RollEliteAffixes(new Pcg32(777));
        IReadOnlyList<EliteAffix> second = EnemyRules.RollEliteAffixes(new Pcg32(777));
        Assert.Equal(2, first.Count);
        Assert.Equal(2, first.Distinct().Count());
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(250, 0, BossPhase.Opening)]
    [InlineData(174, 100, BossPhase.Summoning)]
    [InlineData(87, 100, BossPhase.Frenzy)]
    [InlineData(250, 1_800, BossPhase.Enraged)]
    public void BossPhaseFollowsLifeAndEnrageThresholds(int life, int ticks, BossPhase expected)
    {
        Assert.Equal(expected, AbyssWardenRules.DeterminePhase(life, 250, ticks).Phase);
    }

    [Fact]
    public void CombatPreviewContainsAllRequiredFormulaGroups()
    {
        CharacterSheet sheet = StartingSheet();
        SkillUseProfile skill = SkillRules.BuildHeavyStrike(
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.Bleed),
            Weapons.RustedGreatsword,
            sheet.MaximumLife().Value);

        CombatPreview preview = CombatPreviewRules.Calculate(sheet, Weapons.RustedGreatsword, skill, 100, 20, 25, 10);

        Assert.True(preview.AverageHitDamage.Value >= 1);
        Assert.True(preview.AttacksPerSecondMilli.Value > 0);
        Assert.InRange(preview.HitChanceBasisPoints.Value, 500, 10_000);
        Assert.NotEmpty(preview.AverageHitDamage.Steps);
        Assert.True(preview.EffectiveLife.Value >= sheet.MaximumLife().Value);
    }

    [Fact]
    public void OneHundredProductionSeedsFinishWithoutInvalidResources()
    {
        var config = new SkillConfiguration("archetypes.skill.backstab", SkillSupport.None);
        var build = new TeamBuild(new(10, new(100, 100, 100, 100), new(100, 20, 0), FlatMaximumLife: 500),
            new("test.weapon", 30, 30, 1_200, 1_000), new(SkillIds.HeavyStrike, SkillSupport.None),
            FlatAccuracy: 500, UseWarCry: false, ActiveSkills: [config]);
        var request = new NodeCombatRequest(build, 1, 1, 1, false, false, false, 0, EnemyPool: [Enemies.OathlessGuard]);
        var runner = new SpatialCombatRunner();
        for (ulong seed = 0; seed < 100; seed++)
        {
            var result = runner.Run(request, seed);
            Assert.NotEqual(BattleOutcome.Timeout, result.Outcome);
            Assert.InRange(result.HeroLife, 0, build.Sheet.MaximumLife().Value);
            Assert.InRange(result.HeroMana, 0, build.Sheet.MaximumMana().Value);
            Assert.All(result.Frames, frame => Assert.All(frame.Enemies, enemy => Assert.InRange(enemy.Life, 0, enemy.MaximumLife)));
        }
    }

    private static CharacterSheet StartingSheet() => new(
        Level: 1,
        Attributes: CharacterAttributes.IronOathStarting,
        Equipment: new DefensiveEquipment(0, 0, 0));

}

using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.Simulation;

namespace GameForWork.Tests;

public sealed class P1CombatRulesTests
{
    [Fact]
    public void LegendaryHeavyStrikeUsesSlowerProfileAndCreatesAftershockEvent()
    {
        var weapon = new WeaponProfile("legendary-test", 100, 100, 1_000, 0);
        SkillUseProfile profile = SkillRules.BuildHeavyStrike(
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None),
            weapon,
            500);
        profile = P1LegendaryRules.ApplyToHeavyStrike(profile, P1Legendary.EchoingOathbreakerRule);
        var request = new P1EncounterRequest(
            new CharacterSheet(
                10,
                new CharacterAttributes(100, 100, 100, 100),
                new DefensiveEquipment(500, 100, 100),
                FlatMaximumLife: 500),
            weapon,
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None),
            EnemyRules.Scale(P1Enemies.AbyssWarden, 1),
            HeroFlatAccuracy: 1_000,
            UseWarCry: false,
            MaximumTicks: 1_000,
            HeavyStrikeProfile: profile,
            WeaponLegendaryRule: P1Legendary.EchoingOathbreakerRule);

        P1EncounterResult result = new P1EncounterRunner().Run(request, 42);

        Assert.Equal(P1BattleOutcome.HeroVictory, result.Outcome);
        Assert.Contains(result.Events, item => item.Kind == P1CombatEventKind.LegendaryAftershock);
        Assert.True(profile.AttackIntervalTicks > SkillRules.BuildHeavyStrike(
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None), weapon, 500).AttackIntervalTicks);
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
    public void HitChanceUsesFiveAndNinetyFivePercentCaps()
    {
        Assert.Equal(500, DamageRules.HitChance(1, 10_000, false).Value);
        Assert.Equal(9_500, DamageRules.HitChance(10_000, 0, false).Value);
        Assert.Equal(10_000, DamageRules.HitChance(0, 10_000, true).Value);
    }

    [Fact]
    public void ArmorUsesPerHitFormula()
    {
        CalculatedValue result = DamageRules.ArmorReduction(100, 20);
        Assert.Equal(5_000, result.Value);
        Assert.Equal(2, result.Steps.Count);
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
                P1SkillIds.HeavyStrike,
                SkillSupport.IncreasedArea | SkillSupport.AttackSpeed | SkillSupport.Bleed | SkillSupport.LifeCost),
            P1Weapons.RustedGreatsword,
            sheet.MaximumLife().Value);

        Assert.Equal(0, profile.ManaCost);
        Assert.Equal(8, profile.LifeCost);
        Assert.Equal(2_025, profile.RangeRaw);
        Assert.Equal(14, profile.AttackIntervalTicks);
        Assert.Equal(6_000, profile.BleedChanceBasisPoints);
        Assert.Equal([14_000, 9_000, 13_000], profile.MoreDamageMultipliersBasisPoints);
        Assert.True(P1Skills.HeavyStrike.Tags.HasFlag(SkillTag.Physical));
        Assert.True(P1Skills.HeavyStrike.Tags.HasFlag(SkillTag.Strike));
        Assert.True(P1Skills.WarCry.Tags.HasFlag(SkillTag.WarCry));
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
        ScaledEnemy worker = EnemyRules.Scale(P1Enemies.CorruptedWorker, 10);
        Assert.Equal(91, worker.Life);
        Assert.Equal(7, worker.MinimumPhysicalDamage);
        Assert.Equal(11, worker.MaximumPhysicalDamage);
        Assert.Equal(4, worker.Armor);
        Assert.Equal(4, EnemyRules.ThreatBudget(10));

        ScaledEnemy abyss = EnemyRules.Scale(P1Enemies.CorruptedWorker, 1, abyssRoute: true);
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
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed),
            P1Weapons.RustedGreatsword,
            sheet.MaximumLife().Value);

        CombatPreview preview = CombatPreviewRules.Calculate(sheet, P1Weapons.RustedGreatsword, skill, 100, 20, 25, 10);

        Assert.True(preview.AverageHitDamage.Value >= 1);
        Assert.True(preview.AttacksPerSecondMilli.Value > 0);
        Assert.InRange(preview.HitChanceBasisPoints.Value, 500, 9_500);
        Assert.NotEmpty(preview.AverageHitDamage.Steps);
        Assert.True(preview.EffectiveLife.Value >= sheet.MaximumLife().Value);
    }

    [Fact]
    public void EncounterIsDeterministicAndProducesStableHash()
    {
        P1EncounterRequest request = EasyEncounter(P1Enemies.CorruptedWorker);
        var runner = new P1EncounterRunner();

        P1EncounterResult first = runner.Run(request, 12345);
        P1EncounterResult second = runner.Run(request, 12345);

        Assert.Equal(first.FinalHash, second.FinalHash);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(P1BattleOutcome.HeroVictory, first.Outcome);
    }

    [Fact]
    public void OneHundredSeedsFinishWithoutInvalidResources()
    {
        P1EncounterRequest request = EasyEncounter(P1Enemies.OathlessGuard);
        var runner = new P1EncounterRunner();
        for (ulong seed = 0; seed < 100; seed++)
        {
            P1EncounterResult result = runner.Run(request, seed);
            Assert.NotEqual(P1BattleOutcome.Timeout, result.Outcome);
            Assert.InRange(result.HeroLife, 0, request.Hero.MaximumLife().Value);
            Assert.InRange(result.HeroMana, 0, request.Hero.MaximumMana().Value);
            Assert.InRange(result.EnemyLife, 0, request.Enemy.Life);
        }
    }

    [Fact]
    public void NoProgressDeadlockEndsAsDrawWithoutRestoringBattleTimer()
    {
        CharacterSheet hero = StartingSheet() with { FlatLifeRegeneration = 10_000 };
        SkillUseProfile unusableSkill = SkillRules.BuildHeavyStrike(
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None),
            P1Weapons.RustedGreatsword,
            hero.MaximumLife().Value) with { ManaCost = int.MaxValue };
        P1EncounterRequest request = EasyEncounter(P1Enemies.CorruptedWorker) with
        {
            Hero = hero,
            HeavyStrikeProfile = unusableSkill,
            UseWarCry = false,
            MaximumTicks = 0,
        };

        P1EncounterResult result = new P1EncounterRunner().Run(request, 2345);

        Assert.Equal(P1BattleOutcome.Draw, result.Outcome);
        Assert.InRange(result.Ticks, 1_200, 2_400);
    }

    [Fact]
    public void ExtremelyLongBattleFastForwardsToProjectedOutcomeInsteadOfTimingOut()
    {
        CharacterSheet hero = StartingSheet() with { FlatLifeRegeneration = 10_000 };
        var slowWeapon = new WeaponProfile("slow-progress", 1, 1, 1_000, 0);
        P1EncounterRequest request = EasyEncounter(P1Enemies.AbyssWarden) with
        {
            Hero = hero,
            HeroWeapon = slowWeapon,
            HeavyStrikeProfile = SkillRules.BuildHeavyStrike(
                new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None),
                slowWeapon,
                hero.MaximumLife().Value),
            UseWarCry = false,
            MaximumTicks = 0,
        };

        P1EncounterResult result = new P1EncounterRunner().Run(request, 9876);

        Assert.NotEqual(P1BattleOutcome.Timeout, result.Outcome);
        Assert.InRange(result.Ticks, 1, 20_001);
    }

    [Fact]
    public void BossEncounterEmitsPhaseSummonAndHazardEvents()
    {
        P1EncounterRequest request = EasyEncounter(P1Enemies.AbyssWarden) with
        {
            MaximumTicks = 2_000,
        };
        P1EncounterResult result = new P1EncounterRunner().Run(request, 8);

        Assert.Contains(result.Events, item => item.Kind == P1CombatEventKind.BossPhaseChanged && item.Detail == BossPhase.Summoning.ToString());
        Assert.Contains(result.Events, item => item.Kind == P1CombatEventKind.BossSummonedWorkers && item.Value == 3);
        Assert.Contains(result.Events, item => item.Kind == P1CombatEventKind.BossHazardCreated);
    }

    [Fact]
    public void CorpseExplosionWaitsOneSecondAfterEliteDeath()
    {
        ScaledEnemy enemy = EnemyRules.Scale(
            P1Enemies.CorruptedWorker,
            1,
            [EliteAffix.CorpseExplosion, EliteAffix.Massive]);
        P1EncounterRequest request = EasyEncounter(enemy);
        P1EncounterResult result = new P1EncounterRunner().Run(request, 99);
        P1CombatEvent explosion = Assert.Single(result.Events, item => item.Kind == P1CombatEventKind.CorpseExplosion);
        int lastHitTick = result.Events.Last(item => item.Kind == P1CombatEventKind.HeavyStrikeHit).Tick;
        Assert.Equal(20, explosion.Tick - lastHitTick);
    }

    private static CharacterSheet StartingSheet() => new(
        Level: 1,
        Attributes: CharacterAttributes.IronOathStarting,
        Equipment: new DefensiveEquipment(0, 0, 0));

    private static P1EncounterRequest EasyEncounter(EnemyProfile enemyProfile)
    {
        var hero = new CharacterSheet(
            10,
            CharacterAttributes.IronOathStarting,
            new DefensiveEquipment(100, 20, 0),
            FlatMaximumLife: 50);
        var weapon = new WeaponProfile("test.weapon", 30, 30, 1_200, 1_000);
        return new P1EncounterRequest(
            hero,
            weapon,
            new SkillConfiguration(
                P1SkillIds.HeavyStrike,
                SkillSupport.AttackSpeed | SkillSupport.Bleed | SkillSupport.LifeCost),
            EnemyRules.Scale(enemyProfile, 1),
            HeroFlatAccuracy: 500,
            UseWarCry: true);
    }

    private static P1EncounterRequest EasyEncounter(ScaledEnemy enemy)
    {
        P1EncounterRequest baseRequest = EasyEncounter(enemy.Base);
        return baseRequest with { Enemy = enemy };
    }
}

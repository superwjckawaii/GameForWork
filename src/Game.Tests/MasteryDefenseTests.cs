using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Tests;

public sealed class MasteryDefenseTests
{
    private static PassiveModifiers Rules(params string[] ids) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', ids.Select(id => "builds.mastery.rule." + id)) };
    private static readonly WeaponProfile Weapon = new("test", 10, 10, 1_000, 0);

    [Theory]
    [InlineData(EnemyDamageType.Physical, 500)]
    [InlineData(EnemyDamageType.Fire, 150)]
    [InlineData(EnemyDamageType.Void, 0)]
    public void ProductionArmorUsesTheScaledIncomingHitAndOnlyEligibleDamageTypes(EnemyDamageType type, int effectiveArmor)
    {
        var enemy = Enemies.CorruptedWorker with
        {
            Life = 1_000_000,
            MinimumPhysicalDamage = 800,
            MaximumPhysicalDamage = 800,
            Accuracy = 10_000,
            Skills = [new(EnemySkillKind.BasicStrike, "armor-check", type, 20_000, RangeRaw: 40_000)]
        };
        int FirstHit(int armor)
        {
            var skill = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
            var sheet = new CharacterSheet(1, new(0, 0, 0, 0), new(armor, 0, 0), FlatMaximumLife: 100_000);
            var build = new GameForWork.Core.Campaign.World.TeamBuild(sheet, Weapon, skill, UseWarCry: false,
                ActiveSkills: [skill], PassiveProfile: Rules("护甲.2"));
            var result = new GameForWork.Core.Spatial.SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0,
                MaximumTicks: 30, EnemyPool: [enemy]), 73);
            return result.Events.First(item => item.Kind == GameForWork.Core.Spatial.SpatialEventKind.EnemyAttack && item.Value > 0).Value;
        }
        int raw = FirstHit(0);
        Assert.True(raw > 0);
        Assert.Equal(CombatRules.ApplyMore(raw, [10_000 - CombatRules.ArmorReduction(effectiveArmor, raw)]), FirstHit(500));
    }

    [Fact]
    public void RecoveryCacheTracksSheetChangesWithoutRestoringResourcesOnRefresh()
    {
        var sheet = new CharacterSheet(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 912,
            FlatMaximumMana: 958, FlatLifeRegeneration: 20);
        var hero = new ResourceState(sheet, initialLife: 500, initialMana: 0);
        for (int tick = 0; tick < 20; tick++) { hero.UpdateSheet(sheet with { }); hero.AdvanceRegenerationTick(tick); }
        Assert.Equal(520, hero.Life);
        Assert.Equal(60, hero.Mana);
        hero.UpdateSheet(sheet with { FlatLifeRegeneration = 40, IncreasedManaRegenerationBasisPoints = 10_000 });
        Assert.Equal(520, hero.Life);
        Assert.Equal(60, hero.Mana);
        for (int tick = 20; tick < 40; tick++) hero.AdvanceRegenerationTick(tick);
        Assert.Equal(560, hero.Life);
        Assert.Equal(180, hero.Mana);
    }

    [Fact]
    public void LargePhysicalHitsUseDoubleArmorAtTheResourceThreshold()
    {
        var hero = new ResourceState(new(1, new(0, 0, 0, 0), new(1_000, 0, 100), FlatMaximumLife: 912), passives: Rules("护甲.4", "护甲.2"));
        Assert.Equal(1_000, hero.MasteryArmor(1_000, 219, EnemyDamageType.Physical, 0));
        Assert.Equal(2_000, hero.MasteryArmor(1_000, 220, EnemyDamageType.Physical, 0));
        Assert.Equal(300, hero.MasteryArmor(1_000, 500, EnemyDamageType.Fire, 0));
        Assert.Equal(300, hero.MasteryArmor(1_000, 500, EnemyDamageType.Cold, 0));
        Assert.Equal(300, hero.MasteryArmor(1_000, 500, EnemyDamageType.Lightning, 0));
        Assert.Equal(0, hero.MasteryArmor(1_000, 500, EnemyDamageType.Void, 0));
    }

    [Fact]
    public void EnemyHitBuffsOnlyProtectLaterHitsAndHaveSeparateDurations()
    {
        var hero = new ResourceState(new(1, new(0, 0, 0, 0), new(1_000, 100, 0)), passives: Rules("护甲.5", "闪避.4"));
        Assert.Equal(1_000, hero.MasteryArmor(1_000, 10, EnemyDamageType.Physical, 0));
        Assert.Equal(100, hero.MasteryEvasion(0));
        hero.ObserveEnemyHit(true, 0);
        Assert.Equal(1_500, hero.MasteryArmor(1_000, 10, EnemyDamageType.Physical, 79));
        Assert.Equal(1_000, hero.MasteryArmor(1_000, 10, EnemyDamageType.Physical, 80));
        Assert.Equal(200, hero.MasteryEvasion(39));
        Assert.Equal(100, hero.MasteryEvasion(40));
        hero.ObserveEnemyHit(false, 40);
        Assert.Equal(100, hero.MasteryEvasion(41));
    }

    [Fact]
    public void EvadeBonusHasOneChargeAndDoesNotGetConsumedByFailedPaymentsOrTriggers()
    {
        var hero = new ResourceState(new(1, new(0, 0, 0, 0), new(0, 100, 0)), passives: Rules("闪避.1", "闪避.5"));
        hero.ObserveEvade(0);
        Assert.Equal(8_500, hero.RecentEvadeHitMultiplier(79));
        Assert.Equal(10_000, hero.RecentEvadeHitMultiplier(80));
        Assert.False(hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 1_000));
        Assert.True(hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 0, selfCast: false));
        Assert.Equal(10_000, hero.LastSkillPaymentMultiplier);
        Assert.True(hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 0));
        Assert.Equal(14_000, hero.LastSkillPaymentMultiplier);
        hero.ObserveEvade(1);
        Assert.True(hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 0));
        Assert.Equal(10_000, hero.LastSkillPaymentMultiplier);
        hero.ObserveEvade(20);
        hero.AdvanceRegenerationTick(100);
        Assert.True(hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 0));
        Assert.Equal(10_000, hero.LastSkillPaymentMultiplier);
    }

    [Fact]
    public void CriticalReductionAffectsOnlyTheExtraPortionAndCapsAtTheNormalHit()
    {
        var hero = new ResourceState(new(1, new(0, 0, 0, 0), new(0, 0, 0)), passives: Rules("护甲.3"));
        Assert.Equal(12_500, hero.IncomingMasteryCriticalMultiplier(15_000));
        Assert.Equal(11_250, hero.IncomingMasteryCriticalMultiplier(15_000, 2_500));
        Assert.Equal(10_000, hero.IncomingMasteryCriticalMultiplier(15_000, 8_000));
    }

    [Fact]
    public void LuckyEvasionKeepsTheNinetyFivePercentCapAndDoesNotAffectSpellsOrAlwaysHits()
    {
        var random = new GameForWork.Core.Simulation.Pcg32(19);
        var request = new DamageRequest(Weapon, TargetEvasion: 1_000_000, Accuracy: 1, LuckyTargetEvasion: true);
        Assert.Equal(500, DamageRules.Resolve(request, random).HitChance.Value);
        Assert.Equal(10_000, DamageRules.Resolve(request with { IsSpell = true }, random).HitChance.Value);
        Assert.Equal(10_000, DamageRules.Resolve(request with { TargetEvasion = 0, Accuracy = 100 }, random).HitChance.Value);
        request = request with { TargetEvasion = 1_000, Accuracy = 100 };
        int chance = DamageRules.HitChance(request.Accuracy, request.TargetEvasion, false).Value;
        Assert.Equal(chance * chance / 10_000, DamageRules.Resolve(request, random).HitChance.Value);
    }

    [Fact]
    public void ResourceConditionsCheckOrdinaryShieldAndSeparateHitsFromDamageOverTime()
    {
        var profile = Rules("生命.1", "生命.2", "生命.6", "法力.2", "能量护盾.3", "能量护盾.4", "能量护盾.5", "虚空.6");
        var hero = new ResourceState(new(1, new(0, 0, 0, 0), new(0, 0, 100), FlatMaximumLife: 912, FlatMaximumMana: 958));
        Assert.Equal(6_120, MasteryRuntime.IncomingResourceMultiplier(profile, hero, true));
        Assert.Equal(7_200, MasteryRuntime.IncomingResourceMultiplier(profile, hero, false));
        Assert.Equal(13_000, MasteryRuntime.OffensiveResourceMultiplier(profile, hero));
        hero.ApplyEnemyDamage(600, true, 0);
        Assert.Equal(7_200, MasteryRuntime.IncomingResourceMultiplier(profile, hero, true));
        Assert.Equal(9_000, MasteryRuntime.IncomingResourceMultiplier(profile, hero, false));
        Assert.Equal(14_000, MasteryRuntime.OffensiveResourceMultiplier(profile, hero));
        Assert.True(hero.TryPayMana(501));
        Assert.Equal(8_000, MasteryRuntime.IncomingResourceMultiplier(profile, hero, true));
    }

    [Fact]
    public void ManaTradeoffsUseMoreCostsAndNeverScaleDamageOverTime()
    {
        var profile = Rules("法力.3", "法力.6", "能量护盾.5");
        Assert.Equal(90, MasteryRuntime.ManaCost(profile, SkillTag.Spell, 100));
        Assert.Equal(75, MasteryRuntime.ManaCost(profile, SkillTag.WarCry, 100));
        Assert.Equal(11_900, MasteryRuntime.OffensiveMultiplier(profile, SkillTag.Spell, Weapon, 1, 1));
        Assert.Equal(10_000, MasteryRuntime.OffensiveMultiplier(profile, SkillTag.Spell, Weapon, 1, 1, hit: false));
    }

    [Fact]
    public void NewlyImplementedMasteriesDoNotKeepTheirUnrelatedFlatFallbackBonuses()
    {
        foreach (string key in new[] { "生命", "能量护盾", "法力", "护甲", "闪避" })
        {
            var node = PassiveTree.Nodes.First(node => node.MasteryGroup == "builds.mastery." + key && node.Kind == PassiveNodeKind.Mastery);
            for (int option = 0; option < 7; option++) Assert.Equal(0, PassiveTreeCatalog.MasteryChoices(node)[option].Effect.Value);
        }
    }

    [Fact]
    public void EvasionMoreIncludesDexterityAndComposesWithIncreased()
    {
        var sheet = new CharacterSheet(1, new(0, 100, 0, 0), new(100, 100, 0), IncreasedEvasionBasisPoints: 5_000);
        var actual = MasteryRuntime.ApplySheet(sheet, Rules("闪避.0"), Weapon, false);
        Assert.Equal(480, actual.Evasion().Value);
        Assert.Equal(50, actual.Armor().Value);
        Assert.Equal(100, actual.Equipment.Evasion);
    }

    [Fact]
    public void EvasionConversionMovesOnlyBaseValuesAndCannotEvadeAfterGlobalBonuses()
    {
        var sheet = new CharacterSheet(1, new(0, 100, 0, 0), new(100, 100, 0),
            IncreasedArmorBasisPoints: 2_000, IncreasedEvasionBasisPoints: 9_000);
        var actual = MasteryRuntime.ApplySheet(sheet, Rules("护甲.1", "闪避.0"), Weapon, false);
        Assert.Equal(180, actual.Armor().Value);
        Assert.Equal(0, actual.Evasion().Value);
    }

    [Fact]
    public void LifeDefenseCountsFinalMaximumAndCapsBothIncreases()
    {
        var sheet = new CharacterSheet(1, new(0, 0, 0, 0), new(100, 0, 0), FlatMaximumLife: 912,
            MaximumLifeMultiplierBasisPoints: 15_000);
        var actual = MasteryRuntime.ApplySheet(sheet, Rules("生命.5"), Weapon, false);
        Assert.Equal(3_000, actual.IncreasedArmorBasisPoints);
        Assert.Equal(3_000, actual.IncreasedSpiritBarrierBasisPoints);
        var capped = MasteryRuntime.ApplySheet(sheet with { FlatMaximumLife = 100_000 }, Rules("生命.5"), Weapon, false);
        Assert.Equal(10_000, capped.IncreasedArmorBasisPoints);
    }

    [Fact]
    public void EvasionSuppressionCountsFinalEvasionAndFreedomRequiresNoShield()
    {
        var sheet = new CharacterSheet(1, new(0, 1_000, 0, 0), new(0, 1_000, 0), IncreasedEvasionBasisPoints: 5_000);
        var actual = MasteryRuntime.ApplySheet(sheet, Rules("闪避.3", "闪避.6"), Weapon, false);
        Assert.Equal(4_050, actual.Evasion().Value);
        Assert.Equal(400, actual.SpellSuppressionBasisPoints);
        Assert.Equal(1_500, actual.IncreasedMovementSpeedBasisPoints);
        var shielded = MasteryRuntime.ApplySheet(sheet, Rules("闪避.3", "闪避.6"), Weapon, true);
        Assert.Equal(3_000, shielded.Evasion().Value);
        Assert.Equal(0, shielded.IncreasedMovementSpeedBasisPoints);
    }

    [Fact]
    public void ExtraManaRegenerationEntersBaseRecoveryBeforeModifiers()
    {
        var sheet = new CharacterSheet(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumMana: 958,
            IncreasedManaRegenerationBasisPoints: 5_000);
        var actual = MasteryRuntime.ApplySheet(sheet, Rules("法力.4"), Weapon, false);
        Assert.Equal(180, actual.ManaRegenerationPerSecond().Value);
        Assert.Equal(sheet.MaximumMana().Value, actual.MaximumMana().Value);
    }

    [Fact]
    public void FractionalShieldRegenerationContinuesWhileEnemyDamageInterruptsRecharge()
    {
        var sheet = MasteryRuntime.ApplySheet(new(1, new(0, 0, 0, 0), new(0, 0, 10), FlatMaximumLife: 1_000),
            Rules("能量护盾.2"), Weapon, false);
        var resources = new ResourceState(sheet, initialShield: 0);
        for (int tick = 0; tick < 100; tick++)
        {
            resources.ApplyEnemyDamage(1, true, tick);
            resources.AdvanceRegenerationTick(tick);
        }
        Assert.Equal(1, resources.Shield);
        Assert.Equal(10, resources.MaximumShield);
    }
}

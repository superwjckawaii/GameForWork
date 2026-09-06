using GameForWork.Core.Ascendancies;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Equipment;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class UnarmedCombatTests
{
    private const string Fists = "archetypes.skill.chain_fists", Finisher = "archetypes.skill.tenfold_finisher";
    private static CombatProfile Profile(params string[] nodes) => new(Ascendancy.MartialMonk,
        nodes.Select(node => "core.ascendancy.martial_monk." + node).ToArray());
    private static TeamBuild Build(CombatProfile? profile = null, string skill = Fists) => new(
        new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000, FlatMaximumMana: 100_000), Weapons.Unequipped,
        new(SkillIds.HeavyStrike, SkillSupport.None), UseWarCry: false, AlwaysHit: true, HasUsableWeapon: false,
        Ascendancy: profile, ActiveSkills: [new(skill, SkillSupport.None)]);

    [Fact]
    public void UnarmedPreviewUsesItsOwnBaseDamageAndCriticalChance()
    {
        var config = new SkillConfiguration(Fists, SkillSupport.None);
        var normal = BuildSummaryRules.CalculateOffense(Build(), config);
        var weapon = BuildSummaryRules.CalculateOffense(Build() with { Weapon = new("unused", 99_000, 99_000, 60_000, 10_000) }, config);
        Assert.Equal(normal, weapon);
        Assert.Equal((3, 5, 800), (normal.BaseMinimumDamage, normal.BaseMaximumDamage, normal.CriticalChanceBasisPoints));
        Assert.True(normal.DamagePerSecond > 0);
        Assert.False(BuildSummaryRules.CalculateOffense(Build() with { HasUsableWeapon = true }, config).IsDirectHitEstimate);
    }

    [Fact]
    public void FocusUsesItsOwnLevelQualityAndAddsSpeedToExistingIncreases()
    {
        var plain = new SkillConfiguration(Fists, SkillSupport.None, Level: 1);
        var linked = plain with { SupportLinks = [new(ActiveSkillCatalog.SupportFor(SupportMechanic.UnarmedFocus).StoneId, 21, 20)] };
        var resolved = CombatSkillRules.Resolve(linked, 1_000);
        Assert.Equal(17_000, resolved.DamageMultiplierBasisPoints);
        Assert.Equal(2_000, resolved.AdditionalAttackSpeedBasisPoints);
        Assert.Equal(1_000, BuildSummaryRules.CalculateOffense(Build(), linked).CriticalChanceBasisPoints);
        var legacy = CombatSkillRules.Resolve(plain with { ArchetypeSupports = [SupportMechanic.UnarmedFocus] }, 1_000);
        Assert.Equal(14_000, legacy.DamageMultiplierBasisPoints);
        Assert.Equal(1_000, legacy.AdditionalAttackSpeedBasisPoints);
        var fast = Build() with { IncreasedAttackSpeedBasisPoints = 8_000 };
        Assert.Equal(10, CombatSkillRules.ActionDelay(fast, 20, SkillTag.Attack, resolved.AdditionalAttackSpeedBasisPoints));
    }

    [Fact]
    public void ComboSupportCountsItsOwnSuccessfulActionsAndUsesIndependentQuality()
    {
        var state = new UnarmedCombatState(null, true);
        var config = new SkillConfiguration(Fists, SkillSupport.None, Level: 1, SupportLinks:
            [new(ActiveSkillCatalog.SupportFor(SupportMechanic.ComboDuration).StoneId, 21, 20)]);
        state.Hit("one", config, 0, false);
        state.Hit("unlinked", config with { SupportLinks = null }, 1, false);
        state.Hit("two", config, 2, false);
        state.Hit("trigger", config, 3, true);
        state.Hit("two", config, 3, false);
        state.Hit("three", config, 4, false);
        Assert.Equal(5, state.Combo(203));
        Assert.Equal(0, state.Combo(204));
    }

    private static PassiveModifiers Mastery(int index) => PassiveModifiers.Empty with { MasteryMechanics = $"builds.mastery.rule.徒手.{index}" };

    [Fact]
    public void EmptyHandsMasteryRejectsAnyOffHandAndDoesNotAddAnIncrease()
    {
        var config = new SkillConfiguration(Fists, SkillSupport.None);
        var skill = CombatSkillRules.Resolve(config, 1_000) with { BaseDamageBasisPoints = 10_000 };
        var build = Build() with { PassiveProfile = Mastery(0) };
        Assert.Equal(160, CombatSkillRules.ScaleOffensiveDamage(100, skill, config, build, SkillTag.Attack, 100, 100));
        Assert.Equal(100, CombatSkillRules.ScaleOffensiveDamage(100, skill, config, build with { HasOffHand = true }, SkillTag.Attack, 100, 100));
    }

    [Fact]
    public void AttributeMasteryAddsPhysicalRangeAndAccuracyBeforeDamageScaling()
    {
        var build = Build() with { PassiveProfile = Mastery(1) };
        build = build with { Sheet = build.Sheet with { Attributes = new(25, 39, 0, 0) } };
        Assert.Equal((9, 14), (UnarmedRules.Source(Fists, build).MinimumPhysicalDamage, UnarmedRules.Source(Fists, build).MaximumPhysicalDamage));
        var preview = BuildSummaryRules.CalculateOffense(build, new(Fists, SkillSupport.None));
        Assert.Equal((6, 9), (preview.BaseMinimumDamage, preview.BaseMaximumDamage));
        Assert.Equal(build.Sheet.Accuracy(build.FlatAccuracy + 60).Value, preview.Accuracy);
    }

    [Fact]
    public void LightningMasteryRetainsPhysicalSourceAndExtraUsesPreConversionBase()
    {
        var config = new SkillConfiguration(Fists, SkillSupport.None);
        var result = DamagePacketRules.ResolveMixed(100, SkillDamageType.Physical, default, SkillSupport.None, 0, 0, 0, 0, 0,
            configuration: config, mastery: new(Mastery(3)));
        Assert.Equal((40, 80, 120), (result.Physical, result.Lightning, result.Total));
        var unrelated = DamagePacketRules.ResolveMixed(100, SkillDamageType.Physical, default, SkillSupport.None, 0, 0, 0, 0, 0,
            configuration: new(SkillIds.HeavyStrike, SkillSupport.None), mastery: new(Mastery(3)));
        Assert.Equal((100, 0), (unrelated.Physical, unrelated.Lightning));
    }

    [Fact]
    public void FinisherMasteryAndAscendancyShareOneConsumptionAndIndependentMultipliers()
    {
        var profile = Profile("finisher.core");
        var state = new UnarmedCombatState(profile, true);
        for (int index = 0; index < 10; index++) state.Hit($"hit:{index}", new(Fists, SkillSupport.None), index, false);
        var bonus = state.Begin("finish", new(Finisher, SkillSupport.None), Build(profile) with { PassiveProfile = Mastery(4) }, "enemy", 10, false);
        Assert.Equal(46_200, bonus.Multiplier);
        Assert.Equal(10, bonus.ConsumedCombo);
        Assert.Equal(0, state.Combo(10));
    }

    [Fact]
    public void AvoidRecoverySharesTwoSecondCooldownAndRejectsArmedBuilds()
    {
        var build = Build() with { PassiveProfile = Mastery(5) };
        var hero = new ResourceState(build.Sheet);
        hero.ApplyDamage(50_000, 0);
        int before = hero.Life;
        var state = new UnarmedCombatState(null, false);
        Assert.True(state.RecoverOnAvoid(build, hero, 0));
        Assert.Equal(before + hero.MaximumLife / 20, hero.Life);
        Assert.False(state.RecoverOnAvoid(build, hero, 39));
        Assert.False(state.RecoverOnAvoid(build with { HasUsableWeapon = true }, hero, 40));
        Assert.True(state.RecoverOnAvoid(build, hero, 40));
    }

    [Fact]
    public void RepeatConsumesAnotherActionDurationAndCannotRepeatItself()
    {
        var build = Build() with { PassiveProfile = Mastery(2), CannotCrit = true };
        var config = new SkillConfiguration(Fists, SkillSupport.None);
        int delay = CombatSkillRules.ActionDelay(build, CombatSkillRules.Resolve(config, 1_000).CastTimeTicks, SkillTag.Attack) * 50;
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0,
            MaximumTicks: 180, EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, Armor = 0, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
        var originals = result.Events.Where(e => e.Detail == "unarmed-combo").ToArray();
        var copies = result.Events.Where(e => e.SourceId == "mastery:unarmed-repeat" && e.Detail.StartsWith("copy:")).ToArray();
        Assert.NotEmpty(copies);
        Assert.NotEmpty(originals);
        Assert.Equal(originals[0].AtMilliseconds + delay, copies[0].AtMilliseconds);
        Assert.All(originals.Zip(originals.Skip(1)), pair => Assert.True(pair.Second.AtMilliseconds - pair.First.AtMilliseconds >= delay * 2));
        Assert.True(copies.Length <= originals.Length);
        Assert.All(copies, copy => Assert.Contains("|scale:6500", copy.Detail));
    }

    [Fact]
    public void WideFistsOnlyExpandsAreasAndBoostsEquipmentGrantedAdditionalStrikes()
    {
        var fists = CombatSkillRules.Resolve(new(Fists, SkillSupport.None), 1_000, Mastery(6));
        Assert.Equal(SkillShape.Single, fists.Shape);
        Assert.Equal(1_500, fists.RangeRaw);
        Assert.Equal(0, fists.AreaIncreasedBasisPoints);
        Assert.Equal(8_000, CombatSkillRules.Resolve(new("archetypes.skill.skyquake_palm", SkillSupport.None), 1_000, Mastery(6)).AreaIncreasedBasisPoints);
        var build = Build() with
        {
            CannotCrit = true,
            AddedPhysicalDamage = 1_000,
            PassiveProfile = Mastery(6) with { IncreasedSkillRangeBasisPoints = 100_000 },
            CombatEquipment = EquipmentCombatLoadout.Empty with { Modifiers = new Dictionary<ItemModifierKind, int> { [ItemModifierKind.AdditionalStrikeTarget] = 1 } }
        };
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 2, false, false, false, 0,
            MaximumTicks: 140, EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, Armor = 0, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
        var pair = result.Events.Where(e => e.Detail.StartsWith($"skill:{Fists}|damage:") && e.Value > 0)
            .GroupBy(e => e.AtMilliseconds).First(group => group.Count() == 2).ToArray();
        Assert.NotEqual(pair[0].TargetId, pair[1].TargetId);
        Assert.InRange(pair[1].Value * 10_000 / pair[0].Value, 12_900, 13_100);
    }

    [Theory]
    [InlineData(1, 0, 6_000)]
    [InlineData(21, 0, 8_000)]
    [InlineData(1, 20, 9_000)]
    public void MovementEchoOnlyRepeatsDamageAtTheOriginalEndpoint(int level, int quality, int scale)
    {
        const string kick = "archetypes.skill.gale_kick";
        var config = new SkillConfiguration(kick, SkillSupport.None, SupportLinks:
            [new(ActiveSkillCatalog.SupportFor(SupportMechanic.MovementEcho).StoneId, level, quality)]);
        Assert.Equal(10_000, CombatSkillRules.Resolve(config, 1_000).DamageMultiplierBasisPoints);
        var build = Build(skill: kick) with { CannotCrit = true, ActiveSkills = [config] };
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0,
            MaximumTicks: 160, EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, Armor = 0, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
        var echo = result.Events.First(e => e.SourceId == "support:movement-echo" && e.Detail.StartsWith("copy:"));
        var original = result.Events.First(e => e.Detail.StartsWith($"skill:{kick}|damage:") && e.Value > 0);
        Assert.Contains($"|scale:{scale}", echo.Detail);
        Assert.True(echo.AtMilliseconds > original.AtMilliseconds);
        Assert.Equal(original.SourcePosition, echo.SourcePosition);
        Assert.Equal(original.TargetId, echo.TargetId);
        Assert.False(LinkedSupportRules.MovementEcho(config with { SkillId = SkillIds.FlameStep }));
    }

    [Fact]
    public void FoundationsKeepAttributeIncreaseSeparateFromMore()
    {
        var build = Build(Profile("unarmed.small", "unarmed.core"));
        build = build with { Sheet = build.Sheet with { Attributes = new(0, 60, 40, 0) } };
        var config = new SkillConfiguration(Fists, SkillSupport.None);
        var skill = CombatSkillRules.Resolve(config, 1_000) with { BaseDamageBasisPoints = 10_000 };
        Assert.Equal(1_000, CombatSkillRules.OffensiveIncreases(build, SkillTag.Attack).InitialIncreasedBasisPoints);
        Assert.Equal(2_000, UnarmedRules.AttackSpeed(build));
        Assert.Equal(159, CombatSkillRules.ScaleOffensiveDamage(100, skill, config, build, SkillTag.Attack, 1_000, 1_000));
        Assert.Equal(0, UnarmedRules.AttackSpeed(build with { HasUsableWeapon = true }));
    }

    [Fact]
    public void ComboGainsOncePerSuccessfulOriginalActionAndExpiresAtFourSeconds()
    {
        var state = new UnarmedCombatState(Profile("combo.small"), true);
        var config = new SkillConfiguration(Fists, SkillSupport.None);
        Assert.True(state.Hit("first", config, 0, false));
        Assert.False(state.Hit("first", config, 0, false));
        Assert.False(state.Hit("trigger", config, 1, true));
        Assert.True(state.Hit("second", config, 1, false));
        Assert.False(state.Hit("first", config, 2, false));
        Assert.Equal(2, state.Combo(80));
        Assert.Equal(0, state.Combo(81));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void SwitchingTargetsOnlyPreservesComboWithItsCore(bool core, int expected)
    {
        var profile = core ? Profile("combo.small", "combo.core") : Profile("combo.small");
        var state = new UnarmedCombatState(profile, true);
        var config = new SkillConfiguration(Fists, SkillSupport.None);
        state.Begin("one", config, Build(profile), "old", 0, false);
        state.Hit("one", config, 0, false);
        state.Begin("two", config, Build(profile), "new", 1, false);
        Assert.Equal(expected, state.Combo(1));
    }

    [Fact]
    public void FinisherUsesOnePreConsumptionCountAndRestoresFiveOnlyOnceOnHit()
    {
        var profile = Profile("combo.small", "combo.core", "finisher.small", "finisher.core");
        var state = new UnarmedCombatState(profile, true);
        var fists = new SkillConfiguration(Fists, SkillSupport.None);
        for (int index = 0; index < 10; index++) state.Hit($"hit:{index}", fists, index, false);
        var finisher = new SkillConfiguration(Finisher, SkillSupport.None);
        var bonus = state.Begin("finish", finisher, Build(profile), "enemy", 10, false);
        Assert.Equal(new UnarmedActionBonus(49_500, 8_000, false, 10), bonus);
        Assert.Equal(0, state.Combo(10));
        Assert.Equal(bonus, state.Begin("finish", finisher, Build(profile), "another", 10, false));
        Assert.True(state.Hit("finish", finisher, 10, false));
        Assert.Equal(5, state.Combo(10));
        Assert.False(state.Hit("finish", finisher, 10, false));
        Assert.Equal(5, state.Combo(10));
    }

    [Fact]
    public void MovementThresholdCooldownAndTeleportExclusionAreIndependent()
    {
        var profile = Profile("movement.core");
        var state = new UnarmedCombatState(profile, false);
        var config = new SkillConfiguration(Fists, SkillSupport.None);
        state.Moved(9_000, 0, SkillIds.FlameStep);
        state.Moved(1_999, 0);
        Assert.False(state.Begin("first", config, Build(profile), "enemy", 0, false).ForceCritical);
        state.Moved(1, 0);
        var empowered = state.Begin("second", config, Build(profile), "enemy", 0, false);
        Assert.True(empowered.ForceCritical);
        Assert.Equal(16_000, empowered.Multiplier);
        state.Moved(2_000, 1);
        Assert.False(state.Begin("cooldown", config, Build(profile), "enemy", 39, false).ForceCritical);
        Assert.True(state.Begin("ready", config, Build(profile), "enemy", 40, false).ForceCritical);
    }

    [Fact]
    public void PalmAreaUsesCurrentComboAndKickHasItsConfirmedCooldown()
    {
        var state = new UnarmedCombatState(Profile("movement.small"), true);
        var config = new SkillConfiguration(Fists, SkillSupport.None);
        for (int i = 0; i < 10; i++) state.Hit($"hit:{i}", config, i, false);
        var palm = state.Resolve(CombatSkillRules.Resolve(new("archetypes.skill.skyquake_palm", SkillSupport.None), 1_000), 10);
        Assert.Equal(4_000, palm.AreaIncreasedBasisPoints);
        var kick = state.Resolve(CombatSkillRules.Resolve(new("archetypes.skill.gale_kick", SkillSupport.None), 1_000), 10);
        Assert.Equal((6_000, 16, SkillShape.Single), (kick.RangeRaw, kick.CooldownTicks, kick.Shape));
    }

    [Fact]
    public void EmptyWeaponBuildDealsRealUnarmedDamageAndAccumulatesComboInProduction()
    {
        var result = new SpatialCombatRunner().Run(new(Build() with { CannotCrit = true }, 1, 1, 1,
            false, false, false, 0, MaximumTicks: 500, EnemyPool: [Enemies.CorruptedWorker with
            { Life = 1_000_000, Armor = 0, MovementSpeedRawPerSecond = 0, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
        Assert.Contains(result.Events, item => item.Detail.StartsWith($"skill:{Fists}|damage:", StringComparison.Ordinal) && item.Value >= 3);
        Assert.Contains(result.Events, item => item.Detail == "unarmed-combo" && item.Value == 10);
    }

    [Fact]
    public void StanceCoreKeepsPreviousBonusesForThreeSecondsAndHonorsFixedModes()
    {
        var profile = Profile("stance.small", "stance.core");
        var state = new CombatBuffState(profile);
        var auto = new SkillConfiguration("archetypes.skill.yin_yang_stance", SkillSupport.None);
        Assert.True(state.Activate(auto, true, 0));
        Assert.True(state.Activate(auto, true, 16));
        var overlap = state.Apply(Build(profile), 16);
        Assert.Equal((4_500, 1_200, 600), (overlap.IncreasedDamageBasisPoints, overlap.IncreasedAttackSpeedBasisPoints, overlap.BlockChanceBasisPoints));
        Assert.Equal(8_280, state.IncomingHitMultiplier(true, 16));
        Assert.Equal(9_000, state.IncomingDamageMultiplier(true, 16, false));
        Assert.False(state.CanUse(auto, 75));
        Assert.True(state.CanUse(auto, 76));
        Assert.False(state.CanUse(auto with { Mode = "Yin" }, 76));
        Assert.Equal(0, state.Apply(Build(profile), 76).IncreasedDamageBasisPoints);
        Assert.True(state.Activate(auto, true, 76));
        Assert.Equal(8_280, state.IncomingHitMultiplier(true, 135));
        Assert.Equal(10_000, state.IncomingHitMultiplier(true, 136));
    }

    [Fact]
    public void MercyDurationAndExtraPerLayerEffectsUseTheirOwnRules()
    {
        var state = new VirtueViceState(increasedDuration: new Dictionary<VirtueViceKind, int> { [VirtueViceKind.Mercy] = 5_000 });
        state.Gain(VirtueViceKind.Mercy);
        state.Advance(17_999);
        Assert.Equal(1, state.Layers(VirtueViceKind.Mercy));
        state.Advance(1);
        Assert.Equal(0, state.Layers(VirtueViceKind.Mercy));
        var profile = Profile("stance.core");
        var unarmed = new UnarmedCombatState(profile, false);
        Assert.Equal(1_500, unarmed.Apply(Build(profile), 0, 3).IncreasedDamageBasisPoints);
        var palm = unarmed.Resolve(CombatSkillRules.Resolve(new("archetypes.skill.skyquake_palm", SkillSupport.None), 1_000), 0, 3);
        Assert.Equal(1_500, palm.AreaIncreasedBasisPoints);
    }

    [Fact]
    public void OnlyAvoidedAttacksTriggerTheCounterAndItsCooldownIsShared()
    {
        var state = new UnarmedCombatState(Profile("counter.small", "counter.core"), false);
        Assert.False(state.Avoided(0, false, true));
        Assert.Equal(4_000, state.CounterIncrease(0));
        Assert.True(state.Avoided(0, true, true));
        Assert.False(state.Avoided(1, true, true));
        Assert.False(state.Avoided(17, true, false));
        Assert.True(state.Avoided(17, true, true));
        Assert.Equal(0, state.CounterIncrease(57));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(10_000)]
    public void CounterUsesUnarmedBaseDamageRatherThanEnemyDamage(int enemyDamage)
    {
        var build = Build(Profile("counter.small", "counter.core")) with { CannotCrit = true, BlockChanceBasisPoints = 7_500 };
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0,
            MaximumTicks: 500, EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, Armor = 0,
                MinimumPhysicalDamage = enemyDamage, MaximumPhysicalDamage = enemyDamage }]), 731);
        var counters = result.Events.Where(item => item.Value > 0 && item.Detail.StartsWith($"skill:{Fists}|damage:", StringComparison.Ordinal) &&
            item.Detail.Contains("|counter", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(counters);
        Assert.All(counters, item => Assert.InRange(item.Value, 16, 28));
    }
}

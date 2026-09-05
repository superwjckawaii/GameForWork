using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Equipment;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Spatial;
using GameForWork.Core.Encounters;
using GameForWork.Core.Maps;
using GameForWork.Core.Content;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;

public sealed class CombatClosureTests
{
    [Fact]
    public void StrongestAilmentExpiresAndWeakerCandidateTakesOverWithinTheSameStep()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Ignite, DamageType.Fire, 100, 1_000, 0, "strong");
        state.Apply(Ailment.Ignite, DamageType.Fire, 50, 2_000, 0, "weak");
        Assert.Equal(125, state.Advance(1_500, (_, dps) => dps).Sum(pulse => pulse.Damage));
        Assert.Equal(25, state.Advance(500, (_, dps) => dps).Sum(pulse => pulse.Damage));
        Assert.Empty(state.Instances);
    }

    [Fact]
    public void FasterAilmentsPreserveDamageAndReadTargetDefensesAtEachStep()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Bleed, DamageType.Physical, 100, 4_000, 10_000, "source");
        Assert.Equal(200, state.Advance(1_000, (_, dps) => dps).Sum(pulse => pulse.Damage));
        Assert.Equal(100, state.Advance(1_000, (_, dps) => dps / 2).Sum(pulse => pulse.Damage));
        Assert.Empty(state.Instances);
    }

    [Fact]
    public void PoisonStacksAndFractionalTickRecoveryDoesNotLoseDamage()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Poison, DamageType.Void, 11, 2_000, 0, "first");
        state.Apply(Ailment.Poison, DamageType.Void, 9, 2_000, 0, "second");
        int total = Enumerable.Range(0, 40).Sum(_ => state.Advance(50, (_, dps) => dps).Sum(pulse => pulse.Damage));
        Assert.Equal(40, total);
    }

    [Fact]
    public void ConvertedPhysicalCannotBleedAndAttackIncreaseDoesNotDoubleDipIntoBleed()
    {
        var baseline = Equipment(new() { [ItemModifierKind.BleedChanceBasisPoints] = 10_000 });
        NodeCombatResult RunBleed(int increase) => Run(Team() with { IncreasedDamageBasisPoints = increase, CombatEquipment = baseline });
        string first = RunBleed(0).Events.First(e => e.Detail.Contains("ailment:bleed|dps:")).Detail;
        Assert.Equal(first, RunBleed(20_000).Events.First(e => e.Detail.Contains("ailment:bleed|dps:")).Detail);
        var converted = Run(Team() with { CombatEquipment = Equipment(new()
        { [ItemModifierKind.BleedChanceBasisPoints] = 10_000, [ItemModifierKind.PhysicalToFireConversionBasisPoints] = 10_000 }) });
        Assert.DoesNotContain(converted.Events, e => e.Kind == SpatialEventKind.Bleed);
    }

    [Fact]
    public void HundredReturnActuallyRepeatsWithIndependentIdsAndCannotRecurse()
    {
        var result = Run(Team() with { HasUsableWeapon = false, CombatEquipment = Equipment([], "百式回身") }, 700);
        var repeats = result.Events.Where(e => e.SourceId == "equipment:百式回身").ToArray();
        Assert.NotEmpty(repeats);
        var group = repeats.GroupBy(e => e.Detail.Split("|source-action:")[1].Split('|')[0]).First().ToArray();
        Assert.Equal(4, group.Length);
        Assert.Equal(4, group.Select(e => e.Detail.Split('|')[0]).Distinct().Count());
        Assert.All(group.Zip(group.Skip(1)), pair => Assert.InRange(pair.Second.AtMilliseconds - pair.First.AtMilliseconds, 100, 150));
        Assert.All(repeats, e => Assert.Contains("source-action:equipment-action:", e.Detail));
    }

    [Fact]
    public void PhantomStepCreatesUntargetableUnitsAndCopiesACompletedAttack()
    {
        TeamBuild team = Team() with { ActiveSkills = [new("archetypes.skill.phantom_step", SkillSupport.None, Priority: 0), new(SkillIds.HeavyStrike, SkillSupport.None, Priority: 1)] };
        var result = Run(team, 500);
        Assert.Contains(result.Frames, frame => frame.Allies!.Any(ally => ally.EntityId.StartsWith("phantom:")));
        Assert.Contains(result.Events, e => e.SourceId.StartsWith("phantom:") && e.Value > 0);
        Assert.DoesNotContain(result.Events, e => e.Kind == SpatialEventKind.EnemyAttack && e.TargetId.StartsWith("phantom:"));
    }

    private static TeamBuild Team() => new(new(20, new(100, 100, 100, 100), new(100, 0, 100), FlatMaximumLife: 10_000, FlatMaximumMana: 10_000),
        new("test", 100, 100, 2_000, 0), new(SkillIds.HeavyStrike, SkillSupport.None), UseWarCry: false,
        AlwaysHit: true, CannotCrit: true, ActiveSkills: [new(SkillIds.HeavyStrike, SkillSupport.None)]);
    private static EquipmentCombatLoadout Equipment(Dictionary<ItemModifierKind, int> modifiers, string? name = null) =>
        new(modifiers, name is null ? [] : [EquipmentCatalog.LegendaryItems.Single(item => item.DisplayName == name).Id], new Dictionary<string, int>());
    private static NodeCombatResult Run(TeamBuild team, int ticks = 250) => new SpatialCombatRunner().Run(
        new(team, 1, 1, 1, false, false, false, 0, MaximumTicks: ticks,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MovementSpeedRawPerSecond = 0 }]), 731);

    [Fact]
    public void ReservationCannotBeRecoveredAndUnaffordableAurasStayInactive()
    {
        var build = Team() with { ActiveSkills = [new("builds.skill.undying_sanctuary", SkillSupport.None),
            new("builds.skill.hundred_soul_army", SkillSupport.None), new("builds.skill.hunter_banner", SkillSupport.None)] };
        var aura = AuraCombatProfile.Resolve(build);
        Assert.Equal(2, aura.ActiveIds.Count);
        var hero = new ResourceState(aura.Build.Sheet);
        hero.ReserveMana(aura.ReservedMana);
        hero.RestoreMana(int.MaxValue);
        Assert.Equal(hero.MaximumMana - aura.ReservedMana, hero.Mana);
        Assert.False(hero.TryPayMana(hero.AvailableMaximumMana + 1));
    }

    [Fact]
    public void FlaskPaymentIsAtomicAndIdenticalKindsKeepIndependentCharges()
    {
        TeamBuild build = Team() with { CombatEquipment = Equipment([]) with { Flasks = [
            new(FlaskKind.Life, "expensive", 0, new Dictionary<ItemModifierKind, int> { [ItemModifierKind.FlaskLifeRemovedFromManaBasisPoints] = 10_000 }),
            new(FlaskKind.Life, "plain", 1, new Dictionary<ItemModifierKind, int>())] } };
        var rack = new FlaskRack(build); var hero = new ResourceState(build.Sheet); var rng = new GameForWork.Core.Simulation.Pcg32(9);
        hero.ApplyDamage(hero.MaximumLife / 2, 0); hero.TryPayMana(hero.Mana);
        Assert.Equal("plain", rack.TryUse(FlaskKind.Life, hero, rng)!.Id);
        Assert.Equal(30, rack.Bottles[0].Charges); Assert.Equal(20, rack.Bottles[1].Charges);
        int life = hero.Life; rack.Advance(hero, 50);
        Assert.InRange(hero.Life - life, 1, hero.MaximumLife / 10);
        rack.EndEncounter();
        Assert.All(rack.Bottles, bottle => Assert.False(bottle.Active));
        Assert.Equal(20, rack.Bottles[1].Charges);
    }

    [Fact]
    public void FlaskSpeedChangesDurationWithoutIncreasingTotalAndEchoDoesNotSpendCharges()
    {
        TeamBuild build = Team() with { CombatEquipment = Equipment([]) with { Flasks = [new(FlaskKind.Life, "echo", 0,
            new Dictionary<ItemModifierKind, int> { [ItemModifierKind.IncreasedFlaskRecoveryRateBasisPoints] = 10_000,
                [ItemModifierKind.FlaskRepeatEffect] = 1, [ItemModifierKind.FlaskRecoveryAtEnd] = 1 })] } };
        var rack = new FlaskRack(build); var hero = new ResourceState(build.Sheet);
        hero.TryPayLifeCost(hero.MaximumLife - 1);
        rack.TryUse(FlaskKind.Life, hero, new GameForWork.Core.Simulation.Pcg32(9));
        Assert.Equal(1_500, rack.Bottles[0].DurationMilliseconds);
        rack.Advance(hero, 1_450); Assert.Equal(1, hero.Life);
        rack.Advance(hero, 50); Assert.Equal(1 + hero.MaximumLife / 2, hero.Life);
        Assert.True(rack.Bottles[0].Echo); Assert.Equal(20, rack.Bottles[0].Charges);
        rack.Advance(hero, 1_500); Assert.Equal(hero.MaximumLife, hero.Life);
        Assert.False(rack.Bottles[0].Active);
    }

    [Fact]
    public void CursesRespectCapacityRefreshAndExpireWithoutStackingCopies()
    {
        var state = new CurseState();
        state.Apply("first", 1_000, 0, 10, 1, 0);
        state.Apply("second", 2_000, 500, 20, 1, 0);
        Assert.Equal(0, state.Effect("first", 1));
        state.Apply("second", 3_000, 700, 30, 1, 1);
        Assert.Single(state.Active(2)); Assert.Equal(700, state.Secondary("second", 29));
        Assert.Empty(state.Active(30)); Assert.Equal(0, state.Effect("second", 30));
    }

    [Fact]
    public void AltarEquipmentSnapshotSurvivesSerializationAndAppliesOncePerMap()
    {
        var map = new MapItem("altar-test", 1, EquipmentSnapshot: new(true, true));
        map = System.Text.Json.JsonSerializer.Deserialize<MapItem>(System.Text.Json.JsonSerializer.Serialize(map))!;
        var red = new EncounterRule(Mechanic.Red, Choice: new Choice("red", "red", RewardPreference.HighBases, Cost.MaximumLife, 1_000, "life", "enemy", "cost"));
        var blue = new EncounterRule(Mechanic.Blue, Choice: new Choice("blue", "blue", RewardPreference.HighBases, Cost.BossLife, 2_000, "life", "enemy", "cost"));
        var plan = new MapPlan("test", MapRoute.Safe, [new(1, MapNodeKind.Encounter, "before", 1),
            new(2, MapNodeKind.Altar, "red", 1, Gameplay: red), new(3, MapNodeKind.Altar, "red again", 1, Gameplay: red),
            new(4, MapNodeKind.Altar, "blue", 1, Gameplay: blue), new(5, MapNodeKind.Boss, "boss", 1)], MapAltar.None, [], 0, "boss");
        Assert.Equal(0, EncounterModifiers.For(plan, 1, map).MoreBleedDamage);
        Assert.Equal(6_000, EncounterModifiers.For(plan, 3, map).Apply(Team()).MoreBleedDamageBasisPoints);
        Assert.Equal(15_000, EncounterModifiers.For(plan, 5, map).Life);
        Assert.Equal(12_500, EncounterModifiers.For(plan, 5, map).Damage);
        Assert.Equal(12_500, map.EquipmentSnapshot!.RewardMultiplier(Mechanic.Red));
        Assert.Equal(20_000, map.EquipmentSnapshot.RewardMultiplier(Mechanic.Blue));
        Assert.Equal(10_000, map.EquipmentSnapshot.RewardMultiplier(Mechanic.Garden));
    }

    [Fact]
    public void NonProjectileSpellEchoProducesOneTerminalCopyPerOriginalAction()
    {
        var result = new SpatialCombatRunner().Run(new(Team() with { ActiveSkills = [new(SkillIds.EmberNova, SkillSupport.SpellEcho)] },
            1, 1, 4, false, false, false, 0, MaximumTicks: 400,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MovementSpeedRawPerSecond = 0 }]), 731);
        var copies = result.Events.Where(e => e.SourceId == "support:spell-echo").ToArray();
        Assert.NotEmpty(copies);
        Assert.All(copies.GroupBy(e => (e.Detail.Split("|source-action:")[1].Split('|')[0], e.TargetId)), group => Assert.Single(group));
        Assert.All(copies, copy => Assert.Contains("source-action:equipment-action:", copy.Detail));
    }

    [Fact]
    public void FlaskAffixesAndEnchantmentsBelongToTheirBottleInsteadOfGlobalCharacter()
    {
        var definition = EquipmentCatalog.GetAffix("equipmentImport.affix.flaskpartialinstantrecovery", 1);
        var first = ItemGenerator.Generate("core.base.life_flask", 20, ItemRarity.Basic, 12) with
        {
            Affixes = [new(definition, definition.MinimumValue, Components: definition.EffectComponents.Select(component => new RolledAffixComponent(component.Kind, component.MinimumValue, component.Scope)).ToArray())],
            Enchantment = new("echo", "echo", ItemModifierKind.FlaskRepeatEffect, 1, 1, 0, ItemModifierScope.Rule),
        };
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.Flask1, first));
        Assert.True(loadout.TryEquip(EquipmentSlot.Flask2, ItemGenerator.Generate("core.base.life_flask", 20, ItemRarity.Basic, 13)));
        var summary = loadout.CalculateSummary();
        Assert.Equal(0, summary.Modifiers.Value(ItemModifierKind.InstantFlaskRecoveryPortionBasisPoints));
        var input = EquipmentCombatLoadout.From(loadout, summary);
        Assert.Equal(5_000, input.Flasks![0].Modifiers[ItemModifierKind.InstantFlaskRecoveryPortionBasisPoints]);
        Assert.Equal(1, input.Flasks[0].Modifiers[ItemModifierKind.FlaskRepeatEffect]);
        Assert.False(input.Flasks[1].Modifiers.ContainsKey(ItemModifierKind.FlaskRepeatEffect));
    }

    [Theory]
    [InlineData("猛攻", 350)]
    [InlineData("守护", 525)]
    [InlineData("追猎", 350)]
    public void BeastFormsUseTheirOwnLifePool(string form, int maximum)
    {
        var result = Run(Team() with { ActiveSkills = [new("archetypes.skill.summon_spirit_beast", SkillSupport.None),
            new("archetypes.skill.beast_shapeshift", SkillSupport.None, Mode: form)] }, 250);
        Assert.Contains(result.Events, e => e.Detail == $"beast-form:{form}" && e.Value == maximum);
    }

    [Fact]
    public void GuardsConsumeCapacityReplaceThePoolAndRespectDamageTypeAndExpiry()
    {
        var hero = new ResourceState(Team().Sheet); var guard = new GuardState();
        Assert.True(guard.Activate(SkillIds.IronGuard, hero, 1, 0, 0));
        int capacity = (hero.MaximumLife + hero.MaximumShield) / 4;
        Assert.Equal(capacity, guard.Remaining);
        Assert.Equal(0, guard.Absorb(100, EnemyDamageType.Physical, 1));
        Assert.Equal(100, guard.Absorb(capacity, EnemyDamageType.Fire, 2));
        Assert.Equal(0, guard.Remaining);
        guard.Activate(SkillIds.PrismaticGuard, hero, 1, 0, 10);
        int remaining = guard.Remaining;
        Assert.Equal(123, guard.Absorb(123, EnemyDamageType.Physical, 11));
        Assert.Equal(remaining, guard.Remaining);
        Assert.Equal(123, guard.Absorb(123, EnemyDamageType.Fire, 90));
    }

    [Fact]
    public void SpellArmorShieldPaymentDoesNotInterruptRechargeOrCountAsEnemyDamage()
    {
        var hero = new ResourceState(Team().Sheet); var guard = new GuardState();
        int before = hero.Shield, last = hero.LastDamageTick;
        Assert.True(guard.Activate("archetypes.skill.spellarmor_activate", hero, 1, 0, 0));
        Assert.Equal(before - before / 5, hero.Shield);
        Assert.Equal(last, hero.LastDamageTick);
        Assert.Equal(10, guard.ArmorEnergy);
        hero.TryPayShield(hero.Shield);
        Assert.False(guard.Activate("archetypes.skill.spellarmor_activate", hero, 1, 0, 1));
    }

    [Fact]
    public void FlaskCleanseRemovesActualAilmentsAndEchoDoesNotRefreshImmunity()
    {
        var build = Team() with { CombatEquipment = Equipment([]) with { Flasks = [new(FlaskKind.Armor, "cleanser", 0,
            new Dictionary<ItemModifierKind, int> { [ItemModifierKind.FlaskCleanseBleedPoison] = 400,
                [ItemModifierKind.FlaskRepeatEffect] = 1 })] } };
        var hero = new ResourceState(build.Sheet); var rack = new FlaskRack(build);
        Assert.True(hero.HarmfulStatus.ApplyDot(Ailment.Poison, DamageType.Void, 100, 10_000, "enemy"));
        rack.TryUse(FlaskKind.Armor, hero, new GameForWork.Core.Simulation.Pcg32(1));
        Assert.Empty(hero.HarmfulStatus.DamageOverTime.Instances);
        hero.HarmfulStatus.Tick = 79;
        Assert.False(hero.HarmfulStatus.ApplyDot(Ailment.Poison, DamageType.Void, 100, 1_000, "enemy"));
        hero.HarmfulStatus.Tick = 100;
        rack.Advance(hero, 5_000);
        Assert.True(rack.Bottles[0].Echo);
        Assert.True(hero.HarmfulStatus.ApplyDot(Ailment.Poison, DamageType.Void, 100, 1_000, "enemy"));
    }

    [Fact]
    public void EnemyAilmentProfilesDealPersistentDamageThroughProductionSimulation()
    {
        var enemy = Enemies.CorruptedWorker with { Life = 1_000_000, MinimumPhysicalDamage = 800, MaximumPhysicalDamage = 800,
            Accuracy = 10_000, Skills = [new(EnemySkillKind.BasicStrike, "poison", EnemyDamageType.Physical, 10_000,
                RangeRaw: 40_000, Ailment: Ailment.Poison, AilmentChanceBasisPoints: 10_000)] };
        var result = new SpatialCombatRunner().Run(new(Team(), 1, 1, 1, false, false, false, 0,
            MaximumTicks: 150, EnemyPool: [enemy]), 73);
        Assert.Contains(result.Events, e => e.TargetId == "hero" && e.Detail == "dot:Poison" && e.Value > 0);
    }

    [Fact]
    public void OverlappingGroundFromOneSkillUsesOnlyTheStrongestWhileOtherSkillsStack()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Ground, DamageType.Void, 20, 1_000, 0, "rift");
        state.Apply(Ailment.Ground, DamageType.Void, 10, 2_000, 0, "rift");
        state.Apply(Ailment.Ground, DamageType.Void, 5, 2_000, 0, "field");
        Assert.Equal(25, state.Advance(1_000, (_, dps) => dps).Sum(pulse => pulse.Damage));
        Assert.Equal(15, state.Advance(1_000, (_, dps) => dps).Sum(pulse => pulse.Damage));
    }

    private static NodeCombatResult SingleCast(string skillId, int mana, int ticks = 350)
    {
        var team = Team();
        return new SpatialCombatRunner().Run(new(team with
        {
            Sheet = team.Sheet with { IncreasedManaRegenerationBasisPoints = -10_000 },
            ActiveSkills = [new(skillId, SkillSupport.None)], CombatEquipment = Equipment([]) with { Flasks = [] },
        }, 1, 1, 1, false, false, false, 0, InitialHeroMana: mana, MaximumTicks: ticks,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MovementSpeedRawPerSecond = 0,
                MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
    }

    [Fact]
    public void VoidFieldDealsGroundDamageForSixSecondsWithoutHitOrCriticalEvents()
    {
        var result = SingleCast(SkillIds.VoidDecayField, 20);
        var created = Assert.Single(result.Events, e => e.Detail.Contains($"skill:{SkillIds.VoidDecayField}|area-created"));
        var dots = result.Events.Where(e => e.Detail == "dot:ground").ToArray();
        Assert.NotEmpty(dots);
        Assert.True(dots.Max(e => e.AtMilliseconds) - created.AtMilliseconds >= 5_950);
        Assert.DoesNotContain(result.Events, e => e.Detail.StartsWith($"skill:{SkillIds.VoidDecayField}|damage:"));
        Assert.All(dots, e => Assert.InRange(e.AtMilliseconds, created.AtMilliseconds + 50, created.AtMilliseconds + 6_000));
    }

    [Theory]
    [InlineData(SkillIds.StormBrand, 14, 8, 15)]
    [InlineData("archetypes.skill.thunderstorm", 17, 10, 10)]
    public void PeriodicSkillsPulseAtTheirOwnIntervals(string skillId, int mana, int count, int interval)
    {
        var result = SingleCast(skillId, mana);
        var created = Assert.Single(result.Events, e => e.Detail.StartsWith($"skill:{skillId}|area-created"));
        var pulses = result.Events.Where(e => e.Detail.StartsWith($"skill:{skillId}|area-pulse")).ToArray();
        Assert.Equal(count, pulses.Length);
        Assert.Equal(Enumerable.Range(1, count).Select(index => created.AtMilliseconds + interval * index * 50), pulses.Select(pulse => pulse.AtMilliseconds));
    }

    [Fact]
    public void RiftInitialHitIsSeparateFromTheFollowingGroundDamage()
    {
        var result = SingleCast("archetypes.skill.void_rift", 18);
        Assert.Single(result.Events, e => e.Detail.StartsWith("skill:archetypes.skill.void_rift|damage:"));
        Assert.Contains(result.Events, e => e.Detail == "dot:ground" && e.Value > 0);
    }

    [Fact]
    public void SpellDamageUsesPointUnitsAndItsOwnCriticalChance()
    {
        var skill = CombatSkillRules.Resolve(new(SkillIds.EmberNova, SkillSupport.None), 1_000);
        Assert.Equal(45, CombatSkillRules.BaseDamage(skill, SkillTag.Spell, Team().Weapon, 0));
        var random = new GameForWork.Core.Simulation.Pcg32(19);
        Assert.All(Enumerable.Range(0, 100).Select(_ => SpellHitRules.Roll(skill, 1, random)), damage => Assert.InRange(damage, 36, 54));
        Assert.Equal(500, SpellHitRules.BaseCriticalChance(skill.SkillId, 0, 0));
        Assert.Equal(1_700, SpellHitRules.BaseCriticalChance("archetypes.skill.ice_lance", 7_000, 20));
    }

    [Fact]
    public void AddedSupportDamageUsesTheLinkedStoneLevelAndFlatDamageEffectiveness()
    {
        var fire = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.AddedFire, Level: 1,
            SupportLinks: [new(ActiveSkillCatalog.SupportFor(SkillSupport.AddedFire).StoneId, 21, 0)]);
        var added = DamagePacketRules.ResolveMixed(100, SkillDamageType.Physical, default, fire.Supports, 0, 0, 0, 0, 0, configuration: fire);
        Assert.Equal(35, added.Fire);
        var cold = DamagePacketRules.ResolveMixed(1_000, SkillDamageType.Fire, default, SkillSupport.AddedCold, 0, 0, 0, 0, 0,
            addedDamageEffectiveness: 5_000);
        Assert.Equal(5, cold.Cold);
        var dot = DamagePacketRules.ResolveMixed(1_000, SkillDamageType.Fire, default, SkillSupport.AddedCold, 0, 0, 0, 0, 0, allowAddedHitDamage: false);
        Assert.Equal(0, dot.Cold);
    }

    [Fact]
    public void AegisPulsePaysShieldOnceAndDealsTheInitialSpellHit()
    {
        var hero = new ResourceState(Team().Sheet); var guard = new GuardState();
        int cost = GuardState.ShieldCost("archetypes.skill.aegis_pulse", hero.MaximumShield);
        int shield = hero.Shield;
        Assert.True(guard.Activate("archetypes.skill.aegis_pulse", hero, 1, 0, 0));
        Assert.Equal(shield - cost, hero.Shield);
        Assert.Equal(cost * 15_000 / 10_000, guard.Remaining);
        var result = SingleCast("archetypes.skill.aegis_pulse", 18);
        Assert.Contains(result.Events, e => e.Detail.StartsWith("skill:archetypes.skill.aegis_pulse|damage:") && e.Value > 0);
    }

    [Fact]
    public void OverlappingConvertedGroundChoosesAWholeInstanceRatherThanEachBestElement()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Ground, DamageType.Fire, 80, 1_000, 0, "field", instanceId: "first");
        state.Apply(Ailment.Ground, DamageType.Void, 20, 1_000, 0, "field", instanceId: "first");
        state.Apply(Ailment.Ground, DamageType.Fire, 10, 1_000, 0, "field", instanceId: "second");
        state.Apply(Ailment.Ground, DamageType.Void, 60, 1_000, 0, "field", instanceId: "second");
        var damage = state.Advance(1_000, (_, dps) => dps);
        Assert.Equal(100, damage.Sum(pulse => pulse.Damage));
        Assert.Equal(20, Assert.Single(damage, pulse => pulse.Type == DamageType.Void).Damage);
    }
}

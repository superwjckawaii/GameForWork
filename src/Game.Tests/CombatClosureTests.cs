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
}

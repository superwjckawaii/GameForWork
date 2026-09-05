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
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Endgame;
using System.Text.Json;

namespace GameForWork.Tests;

public sealed class CombatClosureTests
{
    [Fact]
    public void HeatUsesCompletedActionsThenStopsForTwoSecondsAndResets()
    {
        var heat = new ConstructHeatState(new());
        for (int tick = 0; tick < 9; tick++) heat.Complete(tick);
        Assert.Equal(90, heat.Heat);
        Assert.True(heat.FinalAction);
        Assert.Equal(4_500, heat.DamageIncrease);
        Assert.Equal(9_100, heat.HitMultiplier);
        heat.Complete(9);
        Assert.False(heat.CanAct(48));
        heat.Advance(48); Assert.Equal(100, heat.Heat);
        heat.Advance(49); Assert.Equal(0, heat.Heat);
        Assert.True(heat.CanAct(49));
        heat.Complete(50); heat.Advance(70); Assert.Equal(10, heat.Heat);
        heat.Advance(75); Assert.Equal(5, heat.Heat);
    }

    [Fact]
    public void StabilizerCapsHeatWithoutStoppingAndCountsEveryTenthAction()
    {
        var heat = new ConstructHeatState(new(Modules: [ConstructModule.Stabilizer, ConstructModule.Reforge]));
        for (int tick = 0; tick < 29; tick++)
        {
            Assert.Equal((tick + 1) % 10 == 0, heat.FinalAction);
            heat.Complete(tick);
            Assert.True(heat.CanAct(tick));
            Assert.InRange(heat.Heat, 0, 70);
        }
        Assert.True(heat.FinalAction);
        heat.Reset(100); Assert.Equal(0, heat.Heat); Assert.False(heat.FinalAction);
    }

    [Fact]
    public void ModuleConfigurationRejectsDuplicatesAndSnapshotsTheSelectedPair()
    {
        var state = new EndgameState(); state.AwardBreakthroughPoint(4);
        Assert.True(state.TrySelectAscendancy(Ascendancy.IdolForger));
        var pair = new[] { ConstructModule.Stabilizer, ConstructModule.Reforge };
        Assert.True(state.ConfigureCombat(new(PhantomReplayMode.Reverse, pair)));
        pair[0] = ConstructModule.Firepower;
        Assert.True(state.CombatConfiguration.Has(ConstructModule.Stabilizer));
        Assert.False(state.ConfigureCombat(new(Modules: [ConstructModule.Reforge, ConstructModule.Reforge])));
        Assert.False(state.ConfigureCombat(new(Modules: [ConstructModule.Reforge])));
        var restored = EndgameState.Restore(JsonSerializer.Deserialize<EndgameSnapshot>(JsonSerializer.Serialize(state.Capture())));
        Assert.True(restored.CombatConfiguration.Has(ConstructModule.Stabilizer));
        Assert.Equal(PhantomReplayMode.Reverse, restored.CombatConfiguration.PhantomMode);
        Assert.True(EndgameState.Restore(state.Capture() with { CombatConfiguration = null }).CombatConfiguration.Has(ConstructModule.Firepower));
    }

    [Fact]
    public void PhantomMemoryFocusSnapshotsLatestActionAndDoesNotRecurse()
    {
        var profile = new CombatProfile(Ascendancy.PhantomMaster,
            ["core.ascendancy.phantom_master.spawn.small", "core.ascendancy.phantom_master.spawn.core",
             "core.ascendancy.phantom_master.afterimage.small", "core.ascendancy.phantom_master.afterimage.core",
             "core.ascendancy.phantom_master.copy.core"], new(PhantomReplayMode.Focus));
        var queue = new CombatActionQueue(profile);
        var action = RecordedAttack().LatestAttack!;
        for (int index = 0; index < 3; index++)
        {
            queue.Record($"action-{index}", action.Hits[0], index * 20, false);
            queue.CompleteReady((index + 1) * 1_000, new HashSet<string>(), false, false);
        }
        Assert.Equal(3, queue.Memory.Count);
        Assert.Single(queue.PhantomFrames(60));
        Assert.Equal(new[] { 3_000, 4_000, 5_000 }, queue.Pending.Select(copy => copy.DueMilliseconds));
        Assert.All(queue.Pending, copy => { Assert.Equal("action-2", copy.Action.Id); Assert.Equal(3_000, copy.Multiplier); });
        foreach (var copy in queue.TakeDue(5_000)) queue.Replayed(copy, copy.Action.Hits);
        queue.Record("triggered", action.Hits[0], 110, true);
        queue.CompleteReady(6_000, new HashSet<string>(), false, false);
        Assert.Equal(3, queue.Memory.Count); Assert.Empty(queue.Pending);
    }

    [Fact]
    public void PhantomSwapUsesActualLossFarthestPositionAndSharedCooldown()
    {
        var queue = new CombatActionQueue(new(Ascendancy.PhantomMaster,
            ["core.ascendancy.phantom_master.swap.small", "core.ascendancy.phantom_master.swap.core"]));
        queue.SpawnPhantom(new(1_000, 0), 0, 200, 4, 3_000);
        queue.SpawnPhantom(new(4_000, 0), 0, 200, 4, 3_000);
        Assert.Null(queue.TrySwap(new(0, 0), 10, 199, 1_000));
        Assert.Equal(new Point(4_000, 0), queue.TrySwap(new(0, 0), 10, 200, 1_000));
        Assert.Equal(30, queue.UntargetableUntil);
        Assert.Contains(queue.PhantomFrames(10), phantom => phantom.Position == new Point(0, 0));
        Assert.Null(queue.TrySwap(new(4_000, 0), 49, 200, 1_000));
        Assert.Equal(new Point(0, 0), queue.TrySwap(new(4_000, 0), 50, 200, 1_000));
    }

    [Fact]
    public void UnityConsumesAllPhantomsForTerminalCopiesWithoutRecoveryOrSacrifice()
    {
        var queue = new CombatActionQueue(new(Ascendancy.PhantomMaster, ["core.ascendancy.phantom_master.unity.core"]));
        var action = RecordedAttack().LatestAttack!;
        queue.Record("latest", action.Hits[0], 0, false);
        queue.CompleteReady(1_000, new HashSet<string>(), false, false);
        for (int index = 0; index < 4; index++) queue.SpawnPhantom(new(index * 1_000, 0), 20, 200, 4, 3_000, sacrifice: 5_000);
        Assert.True(queue.TryUnity(21));
        Assert.Empty(queue.PhantomFrames(21));
        Assert.Equal(4, queue.Pending.Count);
        Assert.All(queue.Pending, copy => { Assert.Equal(7_500, copy.Multiplier); Assert.False(copy.Sacrifice); Assert.Equal("latest", copy.Action.Id); });
        Assert.False(queue.TryUnity(22));
    }

    [Fact]
    public void RuneFieldStacksCapAtThreeAndLeavingImmediatelyRemovesBonusesWithoutHealing()
    {
        var state = new RuneFieldState(new(Ascendancy.IdolForger,
            ["core.ascendancy.idol_forger.rune_field.small", "core.ascendancy.idol_forger.rune_field.core"]));
        state.Update([new(0, 0), new(0, 0), new(0, 0), new(0, 0)]);
        Assert.Equal(3_000, state.DamageIncrease(new(3_000, 0)));
        Assert.Equal(8_500, state.HitMultiplier(new(3_000, 0)));
        Assert.Equal(10_000, state.HitMultiplier(new(3_001, 0)));
        var sheet = Team().Sheet;
        Assert.Equal(sheet.IncreasedShieldBasisPoints + 1_500, state.Apply(sheet, new(0, 0)).IncreasedShieldBasisPoints);
        var hero = new ResourceState(sheet); int shield = hero.Shield;
        hero.UpdateSheet(state.Apply(sheet, new(0, 0)));
        Assert.Equal(shield, hero.Shield);
        hero.RestoreShield(hero.MaximumShield);
        hero.UpdateSheet(state.Apply(sheet, new(3_001, 0)));
        Assert.Equal(shield, hero.Shield);
        state.Update([]); Assert.Equal(0, state.Layers(new(0, 0)));
    }

    [Fact]
    public void ProductionCombatCreatesMemoryPhantomsAndConstructHeat()
    {
        var phantom = Run(Team() with { Ascendancy = new(Ascendancy.PhantomMaster, []) }, 250);
        Assert.Contains(phantom.Frames, frame => frame.Allies?.Any(ally => ally.EntityId.StartsWith("phantom:", StringComparison.Ordinal)) == true);
        var turrets = Run(Team() with { Ascendancy = new(Ascendancy.IdolForger, []), ActiveSkills = [new("archetypes.skill.forge_turret", SkillSupport.None)] }, 350);
        Assert.Contains(turrets.Events, e => e.Detail.StartsWith("construct-heat:100|", StringComparison.Ordinal));
    }

    [Fact]
    public void UnitProjectilesTravelAndQualityIncreasesTheirSpeed()
    {
        NodeCombatResult Bow(int quality) => Run(Team() with { ActiveSkills = [new("archetypes.skill.summon_soulbow", SkillSupport.None, Quality: quality)] }, 100);
        var ordinary = Bow(0); var quality = Bow(20);
        var launched = ordinary.Events.First(e => e.Detail.Contains("|projectile-launch|", StringComparison.Ordinal));
        var impact = ordinary.Events.First(e => e.SourceId == launched.SourceId && e.Detail.EndsWith("|projectile-hit", StringComparison.Ordinal));
        Assert.True(impact.AtMilliseconds > launched.AtMilliseconds);
        Assert.EndsWith("|speed:12000", launched.Detail);
        Assert.Contains(quality.Events, e => e.Detail.EndsWith("|projectile-launch|speed:15600", StringComparison.Ordinal));
    }

    [Fact]
    public void ProductionOverheatedConstructCannotLaunchUntilItsStopEnds()
    {
        var result = Run(Team() with { Ascendancy = new(Ascendancy.IdolForger, []), ActiveSkills = [new("archetypes.skill.forge_turret", SkillSupport.None)] }, 350);
        var overheated = result.Events.First(e => e.Detail.StartsWith("construct-heat:100|", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Events, e => e.SourceId == overheated.SourceId &&
            e.AtMilliseconds > overheated.AtMilliseconds && e.AtMilliseconds < overheated.AtMilliseconds + 2_000 &&
            e.Detail.Contains("|projectile-launch|", StringComparison.Ordinal));
        Assert.Contains(result.Events, e => e.SourceId == overheated.SourceId && e.AtMilliseconds >= overheated.AtMilliseconds + 2_000 &&
            e.Detail.StartsWith("construct-heat:10|", StringComparison.Ordinal));
    }
    [Fact]
    public void DurationTagDoesNotRemoveHitMasteriesAndDistanceIsExplicit()
    {
        var profile = GameForWork.Core.Campaign.Progression.PassiveModifiers.Empty with
        { MasteryMechanics = "builds.mastery.rule.攻击.0|builds.mastery.rule.近战打击.3" };
        var tags = SkillTag.Attack | SkillTag.Melee | SkillTag.Duration;
        Assert.Equal(18_900, MasteryRuntime.OffensiveMultiplier(profile, tags, Team().Weapon, 100, 100, distanceRaw: 1_000));
        Assert.Equal(14_000, MasteryRuntime.OffensiveMultiplier(profile, tags, Team().Weapon, 100, 100, distanceRaw: 5_000));
        Assert.Equal(10_000, MasteryRuntime.OffensiveMultiplier(profile, tags, Team().Weapon, 100, 100, hit: false));
    }

    [Fact]
    public void PeriodicEquipmentSnapshotKeepsPaidLifeAndFullLifeConditions()
    {
        var build = Team(); var hero = new ResourceState(build.Sheet);
        var loadout = Equipment([], "血税契据");
        var equipment = new EquipmentCombatRuntime(loadout, 3);
        equipment.BeginAction(SkillIds.EmberNova, 5, 0, false, null);
        var snapshot = equipment.SnapshotOffense(hero);
        equipment.BeginAction(SkillIds.HeavyStrike, 0, 1, false, null);
        int Value(EquipmentOffenseSnapshot? state) => equipment.HitMultiplier(build, hero, SkillTag.Spell,
            "enemy", 100, 100, false, false, false, 0, 2, 20, snapshot: state);
        Assert.Equal(10_000, Value(null));
        Assert.Equal(16_000, Value(snapshot));
    }

    [Fact]
    public void TriggeredCopiesDoNotGrantEquipmentRecoveryOrLeech()
    {
        var hero = new ResourceState(Team().Sheet);
        hero.ApplyDamage(hero.MaximumShield + 1_000, 0);
        int life = hero.Life;
        var equipment = new EquipmentCombatRuntime(Equipment(new() { [ItemModifierKind.LifeOnHit] = 100,
            [ItemModifierKind.LifeLeechBasisPoints] = 5_000 }), 3);
        equipment.InAction(equipment.CreateTriggeredAction("enemy", copy: true), () =>
            equipment.OnHit(hero, SkillTag.Attack, "enemy", false, true, 1_000, null));
        Assert.Equal(life, hero.Life);
    }

    [Fact]
    public void ShieldCountersFollowTheEnemyHitAndPaymentCannotTriggerThem()
    {
        var team = Team() with { HasUsableWeapon = false, ActiveSkills =
            [new(SkillIds.HeavyStrike, SkillSupport.None), new(ReactionState.Mirror, SkillSupport.None), new(ReactionState.ShieldBreak, SkillSupport.None)] };
        var result = new SpatialCombatRunner().Run(new(team, 1, 1, 1, false, false, false, 0, MaximumTicks: 200,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MinimumPhysicalDamage = 15_000, MaximumPhysicalDamage = 15_000 }]), 731);
        Assert.Contains(result.Events, e => e.Detail == $"reaction:{ReactionState.Mirror}");
        Assert.Contains(result.Events, e => e.Detail == $"reaction:{ReactionState.ShieldBreak}");
        int reactionIndex = result.Events.ToList().FindIndex(e => e.Detail == $"reaction:{ReactionState.Mirror}");
        Assert.Contains(result.Events.Take(reactionIndex), e => e.Kind == SpatialEventKind.EnemyAttack && e.Value > 0);
        Assert.DoesNotContain(SingleCast(ReactionState.Mirror, 0).Events, e => e.Detail.StartsWith("reaction:"));
    }

    [Fact]
    public void AttackTriggeredGuardPaysOnceAndDoesNotCastWithoutAnAttack()
    {
        var guard = new SkillConfiguration(SkillIds.PrismaticGuard, SkillSupport.None, SupportLinks:
            [new(ActiveSkillCatalog.SupportFor(GameForWork.Core.Archetypes.SupportMechanic.AttackTrigger).StoneId, 1, 0)]);
        var team = Team() with { ActiveSkills = [new(SkillIds.HeavyStrike, SkillSupport.None), guard] };
        var result = Run(team, 100);
        Assert.Contains(result.Events, e => e.Detail == $"skill:{SkillIds.PrismaticGuard}|triggered-guard");
        var idle = Run(team with { ActiveSkills = [guard] }, 100);
        Assert.DoesNotContain(idle.Events, e => e.Detail.Contains("triggered-guard"));
    }

    [Theory]
    [InlineData("archetypes.skill.withering_ray", 10)]
    [InlineData("archetypes.skill.shield_drain", 8)]
    public void ChannelPulsesPayExactlyOneSecondOfManaAndStopWhenEmpty(string id, int mana)
    {
        var result = SingleCast(id, mana, 160);
        var pulses = result.Events.Where(e => e.Detail.StartsWith($"skill:{id}|damage:")).ToArray();
        Assert.Equal(4, pulses.Length);
        Assert.All(pulses.Zip(pulses.Skip(1)), pair => Assert.Equal(250, pair.Second.AtMilliseconds - pair.First.AtMilliseconds));
        Assert.Equal(0, result.Frames.Last().HeroMana);
    }

    [Fact]
    public void DoomWaitsFiveSecondsAndExplodesOnceAtFiveStacks()
    {
        var result = SingleCast("archetypes.skill.doom_brand", 22, 180);
        var placed = Assert.Single(result.Events, e => e.Detail.Contains("skill:archetypes.skill.doom_brand|area-created"));
        var explosion = Assert.Single(result.Events, e => e.Detail == "doom-detonated");
        Assert.Equal(5_000, explosion.AtMilliseconds - placed.AtMilliseconds);
        Assert.Equal(5, explosion.Value);
        var hit = Assert.Single(result.Events, e => e.Detail.StartsWith("skill:archetypes.skill.doom_brand|damage:"));
        Assert.Equal(explosion.AtMilliseconds, hit.AtMilliseconds);
        Assert.True(hit.Value > 0);
    }

    [Fact]
    public void ArmorBoostIsConsumedAtActionStartAndItsShockwaveCannotArmItself()
    {
        var state = new ReactionState(); var guard = new GuardState();
        var config = new SkillConfiguration(ReactionState.Overload, SkillSupport.None, Quality: 20);
        Assert.False(state.Arm(config, guard));
        guard.GainEnergy(10);
        Assert.True(state.Arm(config, guard));
        Assert.Equal(0, guard.ArmorEnergy);
        state.Begin("buff", ReactionState.Overload);
        Assert.Empty(state.Drain());
        state.Begin("missed-attack", SkillIds.HeavyStrike);
        Assert.Equal(17_000, state.AttackMultiplier("missed-attack"));
        Assert.Equal(ReactionState.Overload, Assert.Single(state.Drain()).SkillId);
        state.Begin("next", SkillIds.HeavyStrike);
        Assert.Equal(10_000, state.AttackMultiplier("next"));
        Assert.Empty(state.Drain());
        guard.GainEnergy(5); state.Arm(config, guard); state.Tick = 80;
        state.Begin("expired", SkillIds.HeavyStrike);
        Assert.Equal(10_000, state.AttackMultiplier("expired"));
    }

    [Fact]
    public void AnsweringFormulaTriggersAfterAttackDamageAndCannotTriggerItself()
    {
        var result = Run(Team() with { ActiveSkills = [new(SkillIds.HeavyStrike, SkillSupport.None), new(ReactionState.Answer, SkillSupport.None)] }, 160);
        var reactions = result.Events.Where(e => e.Detail == $"reaction:{ReactionState.Answer}").ToArray();
        Assert.NotEmpty(reactions);
        Assert.All(reactions, reaction => Assert.Contains(result.Events, e => e.AtMilliseconds <= reaction.AtMilliseconds &&
            e.Value > 0 && e.Detail.StartsWith($"skill:{SkillIds.HeavyStrike}|damage:")));
        Assert.All(reactions.Zip(reactions.Skip(1)), pair => Assert.True(pair.Second.AtMilliseconds - pair.First.AtMilliseconds >= 2_200));
        Assert.Contains(result.Events, e => e.Detail.StartsWith($"skill:{ReactionState.Answer}|damage:") && e.Value > 0);
    }

    [Fact]
    public void PhantomSubstitutionSelectsNearestCancelsReplayAndDoesNotSacrifice()
    {
        var queue = RecordedAttack();
        queue.SpawnPhantom(new(1_000, 0), 1, 100, 6, 3_000, 10_000);
        queue.SpawnPhantom(new(4_000, 0), 1, 100, 6, 3_000, 10_000);
        var first = queue.TakeDue(350);
        foreach (var copy in first) queue.Replayed(copy, copy.Action.Hits);
        queue.CommandPhantoms(8);
        Assert.True(queue.TrySubstitute(new(0, 0), 8));
        Assert.Equal(new Point(4_000, 0), Assert.Single(queue.PhantomFrames(8)).Position);
        Assert.False(queue.TrySubstitute(new(0, 0), 9));
        Assert.Single(queue.TakeDue(400));
        Assert.DoesNotContain(queue.Pending, copy => copy.Sacrifice);
        Assert.True(queue.TrySubstitute(new(0, 0), 68));
        Assert.Empty(queue.Pending);
    }

    [Fact]
    public void PhantomExpiryUsesLastReplayRatioAndRecoveryHasSharedCooldown()
    {
        var queue = RecordedAttack(); var hero = new ResourceState(Team().Sheet);
        hero.ApplyDamage(hero.MaximumShield + 1_000, 0);
        int life = hero.Life;
        for (int i = 0; i < 2; i++) queue.SpawnPhantom(new(0, 0), 1, 20, 6, 3_000, 6_000);
        foreach (var copy in queue.TakeDue(350)) queue.Replayed(copy, copy.Action.Hits);
        queue.ExpirePhantoms(21, hero, true);
        Assert.Equal(life + hero.MaximumLife / 50, hero.Life);
        var explosions = queue.TakeDue(1_050);
        Assert.Equal(2, explosions.Count);
        Assert.All(explosions, copy => { Assert.True(copy.Sacrifice); Assert.Equal(1_800, copy.Multiplier); });
        queue.ExpirePhantoms(22, hero, true);
        Assert.Empty(queue.Pending);
    }

    private static CombatActionQueue RecordedAttack()
    {
        var queue = new CombatActionQueue(); var config = Team().HeavyStrike;
        var skill = CombatSkillRules.Resolve(config, 10_000);
        queue.Record("original", new("target", new(0, 0), skill, config, Team(),
            new(100, 0, 0, 0, 0, [new(100, DamageType.Physical, [DamageType.Physical], [])], []), [], false), 0, false);
        queue.CompleteReady(50_000, new HashSet<string>(), false, false);
        return queue;
    }

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

    [Fact]
    public void BlessingAndWarsongRespectTheirRecipientsRangeAndExpiry()
    {
        var buffs = new CombatBuffState(); var build = Team();
        buffs.Activate(new("archetypes.skill.fellowship_blessing", SkillSupport.None), false, 0);
        buffs.Activate(new("archetypes.skill.soul_warsong", SkillSupport.None), false, 0);
        Assert.Equal(build.IncreasedActionSpeedBasisPoints + 1_200, buffs.Apply(build, 1).IncreasedActionSpeedBasisPoints);
        Assert.Equal(new UnitBuff(2_500, 3_700, 3_200, 1_000), buffs.ForUnit(1, new(0, 0), new(1_000, 0)));
        Assert.Equal(new UnitBuff(0, 2_500, 2_000, 0), buffs.ForUnit(1, new(0, 0), new(9_500, 0)));
        Assert.Equal(default, buffs.ForUnit(1, new(0, 0), new(11_000, 0)));
        Assert.Equal(build, buffs.Apply(build, 160));
        var result = SingleCast("archetypes.skill.fellowship_blessing", 24);
        Assert.Contains(result.Events, e => e.Detail == "skill:archetypes.skill.fellowship_blessing|buff-applied");
    }

    [Fact]
    public void StanceSwitchReplacesBonusesAndOnlyAffectsUnarmedBuilds()
    {
        var state = new CombatBuffState(); var build = Team() with { HasUsableWeapon = false };
        var yang = new SkillConfiguration("archetypes.skill.yin_yang_stance", SkillSupport.None, Mode: "Yang");
        Assert.True(state.Activate(yang, true, 0));
        Assert.Equal(build.IncreasedAttackSpeedBasisPoints + 1_200, state.Apply(build, 1).IncreasedAttackSpeedBasisPoints);
        Assert.False(state.Activate(yang with { Mode = "Yin" }, true, 15));
        Assert.True(state.Activate(yang with { Mode = "Yin" }, true, 16));
        Assert.Equal(build.IncreasedAttackSpeedBasisPoints, state.Apply(build, 16).IncreasedAttackSpeedBasisPoints);
        Assert.Equal(600, state.Apply(build, 16).BlockChanceBasisPoints - build.BlockChanceBasisPoints);
        Assert.Equal(9_200, state.IncomingHitMultiplier(true));
        var armed = Team();
        Assert.Equal(armed, state.Apply(armed, 17));
    }

    [Fact]
    public void CurseCapacityRetainsHigherPriorityAndRejectsLowerPriorityReplacement()
    {
        var curse = new CurseState();
        Assert.True(curse.Apply("primary", 100, 0, 50, 1, 0, priority: 1));
        Assert.False(curse.Apply("secondary", 200, 0, 50, 1, 1, priority: 2));
        Assert.Equal(100, curse.Effect("primary", 2));
        Assert.True(curse.Apply("secondary", 200, 0, 100, 1, 50, priority: 2));
    }

    [Fact]
    public void CommandTargetsBossAndGrantsOnlyMinionsItsMovementBonus()
    {
        var state = new CombatBuffState();
        state.Activate(new("archetypes.skill.king_soul_command", SkillSupport.None, Level: 21), false, 0, "boss");
        Assert.Equal("boss", state.Command(1)!.TargetId);
        Assert.Equal(3_500, state.Command(1)!.MoreDamage);
        Assert.Equal(3_000, state.ForUnit(1, new(0, 0), new(1_000, 0), true).MovementSpeed);
        Assert.Equal(0, state.ForUnit(1, new(0, 0), new(1_000, 0)).MovementSpeed);
        Assert.Null(state.Command(120));
    }

    [Fact]
    public void UnitSupportLifeUsesItsOwnStoneLevelAndQuality()
    {
        var stone = new SkillConfiguration("archetypes.skill.summon_boneguard", SkillSupport.None,
            SupportLinks: [new(ActiveSkillCatalog.SupportFor(GameForWork.Core.Archetypes.SupportMechanic.Bodyguard).StoneId, 21, 20)]);
        var result = Run(Team() with { ActiveSkills = [stone] }, 20);
        var units = result.Frames.SelectMany(frame => frame.Allies ?? []).ToArray();
        Assert.NotEmpty(units);
        Assert.All(units, unit => Assert.Equal(342, unit.MaximumLife));
    }
}

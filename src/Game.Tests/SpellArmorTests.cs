using GameForWork.Core.Ascendancies;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Characters;
using GameForWork.Core.Combat;
using GameForWork.Core.Equipment;
using GameForWork.Core.Encounters;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed partial class CombatClosureTests
{
    private static CombatProfile ArmorProfile(params string[] nodes) => new(Ascendancy.Spellarmor,
        nodes.Select(node => "core.ascendancy.spellarmor." + node).ToArray());

    [Fact]
    public void ArmorFoundationEntersAttributeAssemblyBeforeDerivedLifeShieldAndAttack()
    {
        var attributes = new CharacterAttributes(120, 10, 10, 220);
        var build = CharacterBuildAssembler.Assemble(20, attributes, new(), new(), Team().HeavyStrike,
            ascendancy: ArmorProfile("hybrid.small", "hybrid.core"));
        Assert.Equal(210, build.Sheet.Attributes.Physique);
        Assert.Equal(315, build.Sheet.Attributes.Energy);
        Assert.Equal(400, build.Sheet.Equipment.Armor);
        Assert.Equal(600, build.Sheet.FlatMaximumShield);
        Assert.Equal(1_230, build.Sheet.MaximumShield().Value);
    }

    [Fact]
    public void ArmorEnergyBoostReachesTheActualSelfCastAndIsNotAppliedTwice()
    {
        var build = Team() with
        {
            ActiveSkills = [new("archetypes.skill.spellarmor_activate", SkillSupport.None, Priority: 0),
            new(SkillIds.EmberNova, SkillSupport.None, Priority: 1)]
        };
        int FirstHit(GameForWork.Core.Campaign.World.TeamBuild team) => new SpatialCombatRunner().Run(
            new(team, 1, 1, 3, false, false, false, 0, MaximumTicks: 250,
                EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731).Events.First(e =>
            e.Detail.StartsWith($"skill:{SkillIds.EmberNova}|damage:", StringComparison.Ordinal)).Value;
        int normal = FirstHit(build);
        int charged = FirstHit(build with { Ascendancy = ArmorProfile("charge.core") });
        Assert.InRange(charged, normal * 252 / 100 - 3, normal * 252 / 100 + 3);
    }

    [Fact]
    public void ArmorHybridUsesFinalAttributesAndSeparateHundredPointThresholds()
    {
        var build = Team();
        var profile = ArmorProfile("hybrid.core");
        var sheet = CharacterBuildAssembler.Assemble(20, new(199, 10, 10, 200), new(), new(), build.HeavyStrike, ascendancy: profile).Sheet;
        Assert.Equal(200, sheet.Equipment.Armor);
        Assert.Equal(400, sheet.FlatMaximumShield);
        var attack = CombatSkillRules.OffensiveIncreases(build with { Sheet = sheet, Ascendancy = profile }, SkillTag.Attack);
        var spell = CombatSkillRules.OffensiveIncreases(build with { Sheet = sheet, Ascendancy = profile }, SkillTag.Spell);
        Assert.Equal(5_000, attack.InitialIncreasedBasisPoints);
        Assert.Equal(10_000, spell.InitialIncreasedBasisPoints);
    }

    [Fact]
    public void ArmorChargeIsRateLimitedAndConsumedOncePerChannel()
    {
        var state = new GuardState(ArmorProfile("charge.small", "charge.core"));
        state.EnemyHit(0); state.EnemyHit(1); state.EnemyHit(4);
        Assert.Equal(1, state.ArmorEnergy);
        state.EnemyHit(5); Assert.Equal(2, state.ArmorEnergy);
        state.GainEnergy(8);
        var actions = new ReactionState();
        actions.Begin("start", "archetypes.skill.shield_drain", state);
        Assert.Equal(18_000, actions.ActionMultiplier("start"));
        Assert.Equal(4_000, actions.SpellIncrease("start"));
        Assert.Equal(0, state.ArmorEnergy);
        state.GainEnergy(3);
        actions.Tick = 5;
        actions.Begin("continue", "archetypes.skill.shield_drain", state);
        Assert.Equal(18_000, actions.ActionMultiplier("continue"));
        Assert.Equal(4_000, actions.SpellIncrease("continue"));
        Assert.Equal(3, state.ArmorEnergy);
        actions.Tick = 11;
        actions.Begin("restart", "archetypes.skill.shield_drain", state);
        Assert.Equal(12_400, actions.ActionMultiplier("restart"));
        Assert.Equal(0, state.ArmorEnergy);
    }

    [Fact]
    public void ArmorShieldRecoveryUsesActualLossAndDoesNotConvertOverflowHealing()
    {
        var hero = new ResourceState(Team().Sheet, initialLife: Team().Sheet.MaximumLife().Value - 1);
        var guard = new GuardState(ArmorProfile("absorb.small", "absorb.core"));
        var equipment = new EquipmentCombatRuntime(EquipmentCombatLoadout.Empty, 1) { EnemyDamageApplied = guard.ObserveEnemyDamage };
        equipment.ApplyEnemyDamage(hero, 100, true, 0, null);
        Assert.Equal(hero.MaximumLife, hero.Life);
        Assert.Equal(0, guard.ReverseBarrier(0));
        equipment.ApplyEnemyDamage(hero, 100, false, 1, null);
        Assert.Equal(25, guard.ReverseBarrier(1));
        Assert.Equal(5, guard.AbsorbBarriers(30, 2));
        Assert.Equal(0, guard.ReverseBarrier(2));
        guard.ObserveEnemyDamage(hero, new(100_000, 100_000, true, false, false, 2));
        Assert.Equal(hero.MaximumLife * 3 / 10, guard.ReverseBarrier(2));
        Assert.Equal(0, guard.ReverseBarrier(82));
    }

    [Fact]
    public void ArmorOverloadPaymentDoesNotTriggerDamageAndHasIndependentDurationAndCooldown()
    {
        var hero = new ResourceState(Team().Sheet);
        var state = new GuardState(ArmorProfile("overload.small", "overload.core", "absorb.small", "absorb.core", "break.small"));
        int shield = hero.Shield;
        Assert.True(state.TryOverload(hero, 0));
        Assert.Equal(shield - shield / 2, hero.Shield);
        Assert.Equal(0, state.ReverseBarrier(0));
        Assert.Equal(0, state.TakeShieldBurst().Damage);
        Assert.Equal(5_000, state.ApplyArmorBonuses(Team(), hero, 119).MoreSpellDamageBasisPoints);
        Assert.Equal(0, state.ApplyArmorBonuses(Team(), hero, 120).MoreSpellDamageBasisPoints);
        Assert.False(state.TryOverload(hero, 159));
        Assert.True(state.TryOverload(hero, 160));
    }

    [Fact]
    public void ArmorGuardRefreshOccursOnlyOnceAndBarrierFollowsDuration()
    {
        var hero = new ResourceState(Team().Sheet);
        var state = new GuardState(ArmorProfile("guard.small", "guard.core"));
        Assert.True(state.Activate(SkillIds.PrismaticGuard, hero, 1, 0, 0));
        state.GainEnergy(9); state.EnemyHit(79);
        Assert.Equal(159, state.Expires);
        Assert.Equal(hero.MaximumShield * 15 / 100, state.GuardBarrier(158));
        state.ConsumeEnergy(); state.GainEnergy(9); state.EnemyHit(100);
        Assert.Equal(159, state.Expires);
        Assert.Equal(0, state.GuardBarrier(159));
    }

    [Fact]
    public void ArmorBreakWorksForEnemyDamageOverTimeButNotShieldPayments()
    {
        var hero = new ResourceState(Team().Sheet);
        var guard = new GuardState(ArmorProfile("break.small", "break.core"));
        var equipment = new EquipmentCombatRuntime(EquipmentCombatLoadout.Empty, 1) { EnemyDamageApplied = guard.ObserveEnemyDamage };
        Assert.True(hero.TryPayShield(hero.Shield));
        Assert.Equal(0, guard.TakeShieldBurst().Damage);
        hero.RestoreShield(100);
        equipment.ApplyEnemyDamage(hero, 100, false, 0, null);
        Assert.Equal((hero.MaximumShield, true), guard.TakeShieldBurst());
        hero.RestoreShield(100);
        equipment.ApplyEnemyDamage(hero, 100, true, 119, null);
        Assert.Equal(0, guard.TakeShieldBurst().Damage);
        hero.RestoreShield(100);
        equipment.ApplyEnemyDamage(hero, 100, true, 120, null);
        Assert.Equal(hero.MaximumShield, guard.TakeShieldBurst().Damage);
    }

    [Fact]
    public void ArmorBreakProductionSettlesAfterEnemyHitAndIgnoresSpellAndAttackModifiers()
    {
        var build = Team() with { Ascendancy = ArmorProfile("break.small", "break.core") };
        var enemy = Enemies.CorruptedWorker with
        {
            Life = 1_000_000,
            MinimumPhysicalDamage = 6_000,
            MaximumPhysicalDamage = 6_000,
            Accuracy = 10_000,
            MovementSpeedRawPerSecond = 8_000,
            Skills = [new(EnemySkillKind.BasicStrike, "strike", EnemyDamageType.Lightning, 10_000, RangeRaw: 1_500)]
        };
        NodeCombatResult Fight(GameForWork.Core.Campaign.World.TeamBuild team) => new SpatialCombatRunner().Run(
            new(team, 1, 1, 1, false, false, false, 0, MaximumTicks: 150, InitialHeroShield: 1, EnemyPool: [enemy]), 731);
        var result = Fight(build);
        var bursts = result.Events.Where(e => e.Detail.StartsWith("spellarmor-break|", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(bursts);
        Assert.All(bursts, e => Assert.DoesNotContain("critical", e.Detail));
        var enhanced = Fight(build with
        {
            IncreasedDamageBasisPoints = 80_000,
            IncreasedSpellDamageBasisPoints = 80_000,
            MoreAttackDamageBasisPoints = 80_000,
            MoreSpellDamageBasisPoints = 80_000
        });
        Assert.Equal(bursts.Select(e => e.Value), enhanced.Events.Where(e => e.Detail.StartsWith("spellarmor-break|", StringComparison.Ordinal)).Select(e => e.Value));
        int index = result.Events.ToList().IndexOf(bursts[0]);
        Assert.Contains(result.Events.Take(index), e => e.Kind == SpatialEventKind.EnemyAttack && e.AtMilliseconds == bursts[0].AtMilliseconds);
    }
}

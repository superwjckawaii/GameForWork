using GameForWork.Core.Ascendancies;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Combat;
using GameForWork.Core.Equipment;
using GameForWork.Core.Encounters;
using GameForWork.Core.Spatial;
using GameForWork.Core.Skills;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Tests;

public sealed partial class CombatClosureTests
{
    private static CombatProfile AegisProfile(params string[] nodes) => new(Ascendancy.AegisMage,
        nodes.Select(node => "core.ascendancy.aegis_mage." + node).ToArray());
    private static ResourceState ChargedAegis(params string[] nodes)
    {
        var hero = new ResourceState(new(1, new(0, 0, 0, 0), new(0, 0, 1_000), FlatMaximumLife: 10_000), ascendancy: AegisProfile(nodes));
        for (int tick = 0; tick < 60; tick++) hero.AdvanceRegenerationTick(tick);
        return hero;
    }

    [Fact]
    public void NonShieldBlockSourcesSurviveWithoutShieldOnlyAscendancyBonuses()
    {
        var profile = new CombatProfile(Ascendancy.IronGuardian,
            [WarriorNodeIds.BastionAttackBlockSmall, WarriorNodeIds.BastionAttackBlockCore,
             WarriorNodeIds.BastionSpellBlockSmall, WarriorNodeIds.BastionSpellBlockCore]);
        Assert.Equal(600, WarriorAscendancyRules.AttackBlockChanceBasisPoints(600, profile, false));
        Assert.Equal(960, WarriorAscendancyRules.SpellBlockChanceBasisPoints(600, 600, profile, false));
    }

    [Fact]
    public void AegisOverchargeComesOnlyFromRechargeAndUsesItsOwnCapAndDecay()
    {
        var hero = ChargedAegis();
        Assert.Equal(1_000, hero.Shield); Assert.Equal(300, hero.Overcharge);
        hero.ApplyEnemyDamage(100, true, 60);
        Assert.Equal(200, hero.Overcharge); Assert.Equal(1_000, hero.Shield);
        hero.RestoreShield(500); Assert.Equal(200, hero.Overcharge);
        for (int tick = 61; tick < 80; tick++) hero.AdvanceRegenerationTick(tick);
        Assert.Equal(200, hero.Overcharge);
        hero.AdvanceRegenerationTick(80); Assert.Equal(195, hero.Overcharge);
        hero.UpdateSheet(hero.Sheet with { Equipment = new(0, 0, 500) });
        Assert.Equal(150, hero.Overcharge);
    }

    [Fact]
    public void AegisOverchargeHitReductionDoesNotProtectOrdinaryShieldOrDamageOverTime()
    {
        var hero = ChargedAegis("absorb.small");
        Assert.Equal(80, hero.ApplyEnemyDamage(100, true, 60));
        Assert.Equal(220, hero.Overcharge);
        Assert.Equal(100, hero.ApplyEnemyDamage(100, false, 61));
        Assert.Equal(120, hero.Overcharge);
        Assert.Equal(170, hero.ApplyEnemyDamage(200, true, 62));
        Assert.Equal(0, hero.Overcharge); Assert.Equal(950, hero.Shield);
    }

    [Fact]
    public void AegisGateCapsOneEnemyHitAndCannotRearmDuringCooldown()
    {
        var hero = ChargedAegis("absorb.core");
        Assert.True(hero.ShieldGate);
        int life = hero.Life;
        hero.ApplyEnemyDamage(20_000, true, 60);
        Assert.Equal(life, hero.Life); Assert.Equal(1, hero.Shield); Assert.Equal(0, hero.Overcharge);
        Assert.False(hero.ShieldGate);
        hero.RestoreShield(1_000);
        for (int tick = 61; tick < 260; tick++) hero.AdvanceRegenerationTick(tick);
        Assert.Equal(300, hero.Overcharge); Assert.False(hero.ShieldGate);
        hero.AdvanceRegenerationTick(260); Assert.True(hero.ShieldGate);
        hero.ApplyEnemyDamage(400, false, 261);
        Assert.True(hero.ShieldGate); Assert.Equal(900, hero.Shield);
    }

    [Fact]
    public void AegisSpellPaymentIsAtomicAndOnlyFullOverchargePaymentGetsMoreDamage()
    {
        var hero = ChargedAegis("casting.core");
        int mana = hero.Mana, lastDamage = hero.LastDamageTick;
        Assert.False(hero.TryPaySpellMana(hero.AvailableSpellMana + 1));
        Assert.Equal(300, hero.Overcharge); Assert.Equal(mana, hero.Mana);
        Assert.True(hero.TryPaySpellMana(200));
        Assert.True(hero.LastSpellFullyFunded); Assert.Equal(100, hero.Overcharge); Assert.Equal(mana, hero.Mana);
        Assert.True(hero.TryPaySpellMana(120));
        Assert.False(hero.LastSpellFullyFunded); Assert.Equal(mana - 20, hero.Mana);
        Assert.Equal(lastDamage, hero.LastDamageTick);
        hero.AdvanceRegenerationTick(60); Assert.Equal(10, hero.Overcharge);
        Assert.True(hero.TryPaySpellMana(1, allowOvercharge: false)); Assert.Equal(10, hero.Overcharge);
        Assert.False(hero.LastSpellFullyFunded);
    }

    [Fact]
    public void AegisChannelFundingIsReevaluatedForEachPaidPulseWithoutConsumingArmorAgain()
    {
        var reactions = new ReactionState();
        var guard = new GuardState(ArmorProfile("charge.core")); guard.GainEnergy(10);
        reactions.Begin("first", "archetypes.skill.shield_drain", guard, 15_000);
        Assert.Equal(27_000, reactions.ActionMultiplier("first"));
        guard.GainEnergy(1); reactions.Tick = 5;
        reactions.Begin("second", "archetypes.skill.shield_drain", guard, 10_000);
        Assert.Equal(18_000, reactions.ActionMultiplier("second"));
        Assert.Equal(1, guard.ArmorEnergy);
    }

    [Fact]
    public void AegisFundedMoreDamageReachesTheActualSpellWithoutIncreasingMaximumShield()
    {
        var skill = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        var build = Team() with { ActiveSkills = [skill] };
        NodeCombatResult RunSpell(GameForWork.Core.Campaign.World.TeamBuild team) => new SpatialCombatRunner().Run(
            new(team, 1, 1, 3, false, false, false, 0, MaximumTicks: 200,
                EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
        var normal = RunSpell(build);
        var funded = RunSpell(build with { Ascendancy = AegisProfile("casting.core") });
        int First(NodeCombatResult result) => result.Events.First(e => e.Detail.StartsWith($"skill:{SkillIds.EmberNova}|damage:", StringComparison.Ordinal)).Value;
        Assert.InRange(First(funded), First(normal) * 3 / 2 - 2, First(normal) * 3 / 2 + 2);
        Assert.Equal(normal.Frames[0].HeroMaximumShield, funded.Frames[0].HeroMaximumShield);
    }

    [Fact]
    public void AegisReactionsHaveIndependentCooldownsAndIgnoreShieldPayments()
    {
        var profile = AegisProfile("counter.small", "counter.core", "break.small", "break.core");
        var hero = new ResourceState(Team().Sheet, ascendancy: profile);
        var guard = new GuardState(profile);
        var equipment = new EquipmentCombatRuntime(EquipmentCombatLoadout.Empty, 1) { EnemyDamageApplied = guard.ObserveEnemyDamage, AbsorbEnemyDamage = (damage, _, tick) => guard.AbsorbBarriers(damage, tick) };
        guard.EnemySpellHit("source", false, 0); Assert.Equal(2_000, guard.SpellBlockBonus(39)); Assert.Equal(0, guard.SpellBlockBonus(40));
        guard.EnemySpellHit("source", true, 1); Assert.Equal(("source", 6_000), Assert.Single(guard.TakeAegisReplays()));
        guard.EnemySpellHit("source", true, 20); Assert.Empty(guard.TakeAegisReplays());
        hero.TryPayShield(hero.Shield); Assert.False(guard.Immune(20));
        hero.RestoreShield(100); equipment.ApplyEnemyDamage(hero, 100, false, 21, null);
        Assert.Equal(("", 10_000), Assert.Single(guard.TakeAegisReplays()));
        Assert.True(guard.Immune(60)); Assert.False(guard.Immune(61));
        Assert.Equal(0, equipment.ApplyEnemyDamage(hero, 1_000, false, 30, null));
        Assert.Equal(3_000, guard.ApplyBonuses(Team(), hero, 100).IncreasedSpellDamageBasisPoints);
        Assert.Equal(0, guard.ApplyBonuses(Team(), hero, 101).IncreasedSpellDamageBasisPoints);
    }

    [Fact]
    public void AegisProductionReplayKeepsSelfCastHistoryAndShowsOverchargeSeparately()
    {
        var skill = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        // Three enemies satisfy the nova targeting rule before testing spell-block replays.

        var build = Team() with
        {
            Ascendancy = AegisProfile("counter.core"),
            ActiveSkills = [skill],
            Sheet = Team().Sheet with { SpellBlockChanceBasisPoints = 9_000, MaximumSpellBlockChanceBasisPoints = 9_000 }
        };
        var enemy = Enemies.CorruptedWorker with
        {
            Life = 1_000_000,
            MinimumPhysicalDamage = 20,
            MaximumPhysicalDamage = 20,
            Skills = [new(EnemySkillKind.BasicStrike, "spell", EnemyDamageType.Lightning, 10_000, RangeRaw: 8_000, IsSpell: true)]
        };
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 3, false, false, false, 0, MaximumTicks: 250, EnemyPool: [enemy]), 731);
        Assert.Contains(result.Frames, frame => frame.HeroOvercharge > 0 && frame.HeroMaximumOvercharge == frame.HeroMaximumShield * 3 / 10);
        Assert.Contains(result.Events, e => e.Detail == $"reaction:{SkillIds.EmberNova}");
    }
}

using GameForWork.Core.Ascendancies;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class SoulShepherdTests
{
    private const string Bone = "archetypes.skill.summon_boneguard";
    private static CombatProfile Profile(params string[] nodes) => new(Ascendancy.SoulShepherd,
        nodes.Select(node => "core.ascendancy.soul_shepherd." + node).ToArray());
    private static TeamBuild Build(CombatProfile profile) => new(
        new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 1_000_000, FlatMaximumMana: 10_000), Weapons.Unequipped,
        new(SkillIds.HeavyStrike, SkillSupport.None), UseWarCry: false, HasUsableWeapon: false,
        Ascendancy: profile, ActiveSkills: [new(Bone, SkillSupport.None)]);

    [Theory]
    [InlineData(50)]
    [InlineData(150)]
    [InlineData(300)]
    [InlineData(1_000)]
    public void LethalPredictionUsesActualManaAndShieldAbsorptionWithoutSpendingResources(int damage)
    {
        var hero = new ResourceState(new(1, new(0, 0, 0, 0), new(0, 0, 50), FlatMaximumLife: 100, FlatMaximumMana: 100),
            passives: PassiveModifiers.Empty with { MasteryMechanics = "builds.mastery.rule.法力.0" });
        var before = (hero.Life, hero.Mana, hero.Shield);
        bool lethal = hero.WouldEnemyHitBeLethal(damage);
        Assert.Equal(before, (hero.Life, hero.Mana, hero.Shield));
        hero.ApplyEnemyDamage(damage, true, 0);
        Assert.Equal(lethal, !hero.IsAlive);
    }

    [Fact]
    public void LethalHitSacrificesOneLivingMinionBeforeHeroResourcesAreSpent()
    {
        var build = Build(Profile("sacrifice.small", "sacrifice.core"));
        build = build with { Sheet = build.Sheet with { FlatMaximumLife = 1_000 } };
        var enemy = Enemies.CorruptedWorker with
        {
            Life = 1_000_000,
            Accuracy = 1_000_000,
            MinimumPhysicalDamage = 10_000,
            MaximumPhysicalDamage = 10_000,
            Skills = [new(EnemySkillKind.HeavySlam, "lethal-test", EnemyDamageType.Physical, 10_000, RangeRaw: 30_000, Area: true, Avoidable: false)]
        };
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, MaximumTicks: 10, EnemyPool: [enemy]), 731);
        var sacrifice = Assert.Single(result.Events, e => e.Detail == "soul-sacrifice|lethal-hit");
        Assert.Single(result.Events, e => e.Detail == "unit:Minion|death" && e.SourceId == sacrifice.TargetId);
        Assert.True(result.Frames.Last().HeroLife > 0);
    }

    [Fact]
    public void InheritanceOnlyCopiesGenericDamageAndDoesNotCopyAttackSpecificModifiers()
    {
        var baseline = Build(Profile("inheritance.core"));
        int Damage(TeamBuild build) => new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0,
            MaximumTicks: 200, EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, Armor = 0, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731)
            .Events.Where(e => e.SourceId.StartsWith("army:") && e.Detail.Contains("|unit:Minion|attack")).Sum(e => e.Value);
        int normal = Damage(baseline);
        Assert.True(normal > 0);
        Assert.Equal(normal, Damage(baseline with { IncreasedDamageBasisPoints = 10_000 }));
        Assert.InRange(Damage(baseline with { IncreasedGenericDamageBasisPoints = 10_000 }) * 10_000 / normal, 14_000, 15_100);
    }

    [Fact]
    public void ExpiringSlothRemovesExcessMinionsWithoutDeathRewards()
    {
        var resource = new VirtueViceState();
        resource.Gain(VirtueViceKind.Sloth, 2);
        var result = new SpatialCombatRunner().Run(new(Build(Profile("sacrifice.small")), 1, 1, 1, false, false, false, 0,
            MaximumTicks: 260, VirtueVice: resource, EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
        Assert.Equal(8, result.Frames.First().Allies!.Count(ally => ally.EntityId.StartsWith("army:")));
        Assert.Equal(6, result.Frames.Last().Allies!.Count(ally => ally.EntityId.StartsWith("army:")));
        Assert.DoesNotContain(result.Events, e => e.Detail == "unit:Minion|death");
    }

    [Fact]
    public void ArmyStartsWithActualPermanentSlothCapacity()
    {
        var result = new SpatialCombatRunner().Run(new(Build(Profile("legion.core")), 1, 1, 1, false, false, false, 0, MaximumTicks: 1), 731);
        Assert.Equal(9, result.Frames.First().Allies!.Count(ally => ally.EntityId.StartsWith("army:")));
    }

    [Theory]
    [InlineData(false, 4_000)]
    [InlineData(true, 2_000)]
    public void RebirthUsesPerUnitDelayAndCooldown(bool core, int delay)
    {
        var result = new SpatialCombatRunner().Run(new(Build(Profile(core ? "rebirth.core" : "rebirth.small")), 1, 1, 1, false, false, false, 0,
            MaximumTicks: 700, EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MinimumPhysicalDamage = 10_000, MaximumPhysicalDamage = 10_000 }]), 731);
        var revives = result.Events.Where(e => e.Detail == "unit:Minion|revive").ToArray();
        Assert.NotEmpty(revives);
        foreach (var revive in revives)
        {
            var death = result.Events.Last(e => e.SourceId == revive.SourceId && e.Detail == "unit:Minion|death" && e.AtMilliseconds < revive.AtMilliseconds);
            Assert.Equal(delay, revive.AtMilliseconds - death.AtMilliseconds);
        }
        foreach (var group in revives.GroupBy(e => e.SourceId))
            Assert.All(group.Zip(group.Skip(1)), pair => Assert.True(pair.Second.AtMilliseconds - pair.First.AtMilliseconds >= 12_000));
    }

    [Fact]
    public void CommandCoreKeepsTargetAfterTemporaryDamageAndSpeedExpire()
    {
        var state = new CombatBuffState(Profile("command.core"));
        Assert.Null(state.Command(0));
        Assert.True(state.Activate(new("archetypes.skill.king_soul_command", SkillSupport.None), true, 0, "designated"));
        Assert.Equal(2_000, state.Command(119)!.MoreDamage);
        Assert.Equal("designated", state.Command(120)!.TargetId);
        Assert.Equal(0, state.Command(120)!.MoreDamage);
        Assert.Equal(0, state.ForUnit(120, new(0, 0), new(0, 0), true).MovementSpeed);
    }

    [Theory]
    [InlineData(Ascendancy.Marksman, "gale", VirtueViceKind.Arrogance)]
    [InlineData(Ascendancy.SoulShepherd, "legion", VirtueViceKind.Sloth)]
    [InlineData(Ascendancy.Elementalist, "resonance", VirtueViceKind.Temperance)]
    [InlineData(Ascendancy.MartialMonk, "stance", VirtueViceKind.Mercy)]
    [InlineData(Ascendancy.Runecarver, "spellblade", VirtueViceKind.Humility)]
    public void ResourceDurationUsesNamedReinforcementAndPreservesOtherResources(Ascendancy ascendancy, string branch, VirtueViceKind resource)
    {
        var profile = new CombatProfile(ascendancy, [AscendancyDefinitions.Id(ascendancy, branch, NodeKind.Reinforcement)]);
        var state = new VirtueViceState(increasedDuration: AscendancyDefinitions.ResourceDuration(profile));
        state.Gain(resource);
        state.Advance(17_999);
        Assert.Equal(1, state.Layers(resource));
        state.Advance(1);
        Assert.Equal(0, state.Layers(resource));
        Assert.Empty(AscendancyDefinitions.PermanentVirtueVice(profile));
        Assert.Equal(resource, Assert.Single(AscendancyDefinitions.PermanentVirtueVice(new(ascendancy, [AscendancyDefinitions.Id(ascendancy, branch, NodeKind.Core)]))));
    }
}

using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Equipment;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class VoidConditionTests
{
    [Theory]
    [InlineData(DamageType.Void, 300, 360)]
    [InlineData(DamageType.Physical, 600, 780)]
    public void ConditionalVoidIncreaseAddsToItsOwnConversionStage(DamageType source, int normal, int conditional)
    {
        var packet = CombatRules.ConvertAndScale(100, source,
            source == DamageType.Physical ? [new(DamageType.Physical, DamageType.Void, 10_000, "test")] : [], [],
            new(new Dictionary<DamageType, int> { [DamageType.Physical] = 10_000, [DamageType.Void] = 10_000 },
                InitialIncreasedBasisPoints: 10_000, VoidDebuffIncreaseBasisPoints: 6_000));
        var branch = Assert.Single(packet.Branches);
        Assert.Equal(normal, branch.BaseDamage);
        Assert.Equal(conditional, branch.DebuffedBaseDamage);
    }

    [Fact]
    public void MasteryMoreScalesBothConditionalAndNormalSnapshots()
    {
        DamageBranch? captured = null;
        var result = DamagePacketRules.ResolveMixed(100, SkillDamageType.Void, default, SkillSupport.None,
            0, 0, 0, 0, 0, modifiers: new(VoidDebuffIncreaseBasisPoints: 6_000),
            scaleBranch: branch => { captured = branch; return branch.DebuffedBaseDamage!.Value; },
            mastery: new(PassiveModifiers.Empty with { MasteryMechanics = "builds.mastery.rule.虚空.0" }));
        Assert.NotNull(captured);
        Assert.Equal(150, captured.BaseDamage);
        Assert.Equal(240, captured.DebuffedBaseDamage);
        Assert.Equal(240, result.Void);
    }

    [Fact]
    public void ExistingPoisonRespondsToDebuffGainAndExpiryWithoutRefreshingDuration()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Poison, DamageType.Void, 100, 3_000, 0, "poison", debuffedDamagePerSecond: 160);
        Assert.Equal(100, Assert.Single(state.Advance(1_000, (_, damage) => damage)).Damage);
        Assert.Equal(160, Assert.Single(state.Advance(1_000, (_, damage) => damage, true)).Damage);
        Assert.Equal(100, Assert.Single(state.Advance(1_000, (_, damage) => damage)).Damage);
        Assert.Empty(state.Instances);
    }

    [Fact]
    public void ConditionalGroundSelectionKeepsOneWholeInstance()
    {
        var state = new AilmentState();
        state.Apply(Ailment.Ground, DamageType.Fire, 100, 2_000, 0, "field", instanceId: "first");
        state.Apply(Ailment.Ground, DamageType.Void, 50, 2_000, 0, "field", instanceId: "first", debuffedDamagePerSecond: 60);
        state.Apply(Ailment.Ground, DamageType.Void, 140, 2_000, 0, "field", instanceId: "second", debuffedDamagePerSecond: 200);
        Assert.Equal(150, state.Advance(1_000, (_, damage) => damage).Sum(pulse => pulse.Damage));
        var conditional = Assert.Single(state.Advance(1_000, (_, damage) => damage, true));
        Assert.Equal(DamageType.Void, conditional.Type);
        Assert.Equal(200, conditional.Damage);
    }

    [Fact]
    public void GroundCastBeforeErosionGainsConditionalDamageAfterErosionAppears()
    {
        var baseline = PassiveModifiers.Empty with
        {
            IncreasedVoidDamageBasisPoints = 10_000,
            Specialized = new Dictionary<PassiveEffectKind, int> { [PassiveEffectKind.IncreasedAreaEffectBasisPoints] = 8_000 }
        };
        var conditional = baseline with { Specialized = null, MasteryMechanics = "builds.mastery.rule.虚空.4" };
        var normal = Run(baseline);
        var enhanced = Run(conditional);
        var created = Assert.Single(normal.Events, item => item.Detail.Contains("area-created", StringComparison.Ordinal));
        Assert.Single(enhanced.Events, item => item.Detail.Contains("area-created", StringComparison.Ordinal));
        long Damage(NodeCombatResult result, int from, int until) => result.Events
            .Where(item => item.Detail == "dot:ground" && item.AtMilliseconds >= created.AtMilliseconds + from &&
                item.AtMilliseconds < created.AtMilliseconds + until).Sum(item => (long)item.Value);
        Assert.Equal(Damage(normal, 50, 950), Damage(enhanced, 50, 950));
        Assert.True(Damage(normal, 1_100, 3_000) > 0);
        Assert.InRange(Damage(enhanced, 1_100, 3_000) / (double)Damage(normal, 1_100, 3_000), 1.25, 1.35);
    }

    private static NodeCombatResult Run(PassiveModifiers profile)
    {
        var config = new SkillConfiguration(SkillIds.VoidDecayField, SkillSupport.None);
        var build = new TeamBuild(new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000,
                IncreasedManaRegenerationBasisPoints: -10_000), new("test", 100, 100, 1_000, 0),
            new(SkillIds.HeavyStrike, SkillSupport.None), UseWarCry: false, AlwaysHit: true, CannotCrit: true,
            ActiveSkills: [config], PassiveProfile: profile, CombatEquipment: EquipmentCombatLoadout.Empty with { Flasks = [] });
        return new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0,
            InitialHeroMana: 20, MaximumTicks: 500, EnemyPool: [Enemies.CorruptedWorker with
            { Life = 1_000_000, MovementSpeedRawPerSecond = 0, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
    }
}

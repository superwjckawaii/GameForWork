using GameForWork.Core.Archetypes;
using GameForWork.Core.Builds;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class UnitSupportMatrixTests
{
    public static TheoryData<CombatUnitKind, SupportMechanic> ValidPairs => new()
    {
        { CombatUnitKind.Minion, SupportMechanic.MinionAmplify },
        { CombatUnitKind.Minion, SupportMechanic.SwiftMinions },
        { CombatUnitKind.Minion, SupportMechanic.ExpandedArmy },
        { CombatUnitKind.Minion, SupportMechanic.Bodyguard },
        { CombatUnitKind.Companion, SupportMechanic.FerociousBeast },
        { CombatUnitKind.Companion, SupportMechanic.GuardianBeast },
        { CombatUnitKind.Construct, SupportMechanic.ConstructAmplify },
        { CombatUnitKind.Construct, SupportMechanic.RapidRebuild },
    };

    [Theory]
    [MemberData(nameof(ValidPairs))]
    public void SpecializedUnitSupportsOnlyApplyToTheirOwnedUnitKind(
        CombatUnitKind owner, SupportMechanic support)
    {
        Assert.True(UnitSupportRules.Applies(owner, support));
        Assert.All(Enum.GetValues<CombatUnitKind>().Where(kind => kind != owner),
            kind => Assert.False(UnitSupportRules.Applies(kind, support)));
    }

    [Theory]
    [MemberData(nameof(ValidPairs))]
    public void LevelAndQualityAreReadOnlyForTheOwnedUnitKind(
        CombatUnitKind owner, SupportMechanic support)
    {
        SkillConfiguration skill = new("matrix.unit", SkillSupport.None, SupportLinks:
            [new(ActiveSkillCatalog.SupportFor(support).StoneId, 21, 20)]);

        Assert.True(UnitSupportRules.Has(skill, owner, support));
        Assert.True(UnitSupportRules.Value(skill, owner, support, 100, 200) > 0);
        Assert.Equal(20, UnitSupportRules.Quality(skill, owner, support));
        Assert.All(Enum.GetValues<CombatUnitKind>().Where(kind => kind != owner), kind =>
        {
            Assert.False(UnitSupportRules.Has(skill, kind, support));
            Assert.Equal(0, UnitSupportRules.Value(skill, kind, support, 100, 200));
            Assert.Equal(0, UnitSupportRules.Quality(skill, kind, support));
        });
    }

    [Fact]
    public void BlessingCoreAddsTheConfiguredMercenaryToAuthoritativeCombat()
    {
        var mercenary = new TeamBuild(
            new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 2_000),
            new("mercenary-weapon", 80, 120, 1_000, 0),
            new(SkillIds.HeavyStrike, SkillSupport.None),
            FlatAccuracy: 10_000,
            UseWarCry: false);
        var hero = new TeamBuild(
            new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 1_000_000, FlatMaximumMana: 1_000),
            new("hero-weapon", 1, 1, 1_000, 0),
            new(SkillIds.HeavyStrike, SkillSupport.None),
            UseWarCry: false,
            Ascendancy: new(Ascendancy.SpiritCantor, ["core.ascendancy.spirit_cantor.blessing.core"]),
            AttachedMercenary: mercenary);
        var enemy = Enemies.CorruptedWorker with { Life = 1_000_000, MovementSpeedRawPerSecond = 0 };

        NodeCombatResult result = new SpatialCombatRunner().Run(new(hero, 1, 1, 1, false, false, false, 0,
            MaximumTicks: 160, EnemyDamageBasisPoints: 1_000, EnemyPool: [enemy]), 73);

        Assert.Contains(result.Frames, frame => frame.Allies?.Any(ally => ally.EntityId == "mercenary") == true);
        Assert.Contains(result.Events, item => item.SourceId == "mercenary" && item.Value > 0);
    }

    [Fact]
    public void AttachedMercenaryRequiresTheBlessingCore()
    {
        TeamBuild mercenary = BasicBuild(2_000);
        TeamBuild hero = BasicBuild(1_000_000) with { AttachedMercenary = mercenary };
        NodeCombatResult result = new SpatialCombatRunner().Run(new(hero, 1, 1, 1, false, false, false, 0,
            MaximumTicks: 1, EnemyDamageBasisPoints: 1_000), 73);

        Assert.DoesNotContain(result.Frames.SelectMany(frame => frame.Allies ?? []), ally => ally.EntityId == "mercenary");
    }

    [Fact]
    public void MercenaryContinuesAfterTheHeroFallsAndCanFinishTheBattle()
    {
        TeamBuild mercenary = BasicBuild(20_000) with
        {
            Weapon = new("mercenary-weapon", 100, 100, 2_000, 0),
            FlatAccuracy = 10_000,
        };
        TeamBuild hero = BasicBuild(1) with
        {
            Ascendancy = new(Ascendancy.SpiritCantor, ["core.ascendancy.spirit_cantor.blessing.core"]),
            AttachedMercenary = mercenary,
        };
        var enemy = Enemies.CorruptedWorker with
        {
            Life = 2_000,
            MinimumPhysicalDamage = 500,
            MaximumPhysicalDamage = 500,
            MovementSpeedRawPerSecond = 0,
            Skills = [new(EnemySkillKind.HeavySlam, "处决重击", EnemyDamageType.Physical, 10_000,
                RangeRaw: 20_000, Area: true, Avoidable: false)],
        };

        NodeCombatResult result = new SpatialCombatRunner().Run(new(hero, 1, 1, 1, false, false, false, 0,
            MaximumTicks: 600, EnemyDamageBasisPoints: 10_000, EnemyPool: [enemy]), 73);

        Assert.Equal(BattleOutcome.HeroVictory, result.Outcome);
        Assert.Equal(0, result.HeroLife);
        SpatialEvent heroDeathHit = result.Events.Last(
            item => item.Kind == SpatialEventKind.EnemyAttack && item.TargetId == "hero");
        Assert.Contains(result.Events, item => item.SourceId == "mercenary" && item.Value > 0 && item.AtMilliseconds > heroDeathHit.AtMilliseconds);
    }

    private static TeamBuild BasicBuild(int life) => new(
        new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: life, FlatMaximumMana: 1_000),
        new("weapon", 1, 1, 1_000, 0),
        new(SkillIds.HeavyStrike, SkillSupport.None),
        UseWarCry: false);
}

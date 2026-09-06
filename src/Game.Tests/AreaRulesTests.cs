using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class AreaRulesTests
{
    private static PassiveModifiers Rules(params int[] options) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', options.Select(option => $"builds.mastery.rule.范围_距离.{option}")) };

    [Fact]
    public void AreaAndCastDistanceHaveIndependentModifiers()
    {
        var config = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        var baseline = CombatSkillRules.Resolve(config, 1_000);
        var distance = CombatSkillRules.Resolve(config, 1_000, PassiveModifiers.Empty with { IncreasedSkillRangeBasisPoints = 10_000 });
        var area = CombatSkillRules.Resolve(config, 1_000, PassiveModifiers.Empty with
        { Specialized = new Dictionary<PassiveEffectKind, int> { [PassiveEffectKind.IncreasedAreaEffectBasisPoints] = 30_000 } });
        Assert.Equal(baseline.RangeRaw * 2, distance.RangeRaw);
        Assert.Equal(baseline.AreaRadiusRaw, distance.AreaRadiusRaw);
        Assert.Equal(baseline.RangeRaw, area.RangeRaw);
        Assert.Equal(baseline.AreaRadiusRaw * 2, area.AreaRadiusRaw);
        Assert.Equal(area.AreaRadiusRaw, AreaRules.EngagementRange(area));
    }

    [Fact]
    public void EquipmentAreaAddsToPassiveAreaBeforeMoreAndDoesNotExtendCastRange()
    {
        var config = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        var profile = Rules(0) with
        { Specialized = new Dictionary<PassiveEffectKind, int> { [PassiveEffectKind.IncreasedAreaEffectBasisPoints] = 10_000 } };
        var skill = CombatSkillRules.Resolve(config, 1_000, profile);
        var equipment = new GameForWork.Core.Equipment.EquipmentCombatRuntime(
            GameForWork.Core.Equipment.EquipmentCombatLoadout.Empty with
            {
                Modifiers = new Dictionary<GameForWork.Core.Campaign.Items.ItemModifierKind, int>
                { [GameForWork.Core.Campaign.Items.ItemModifierKind.SkillAreaBasisPoints] = 20_000 }
            }, 1);
        var resolved = equipment.Resolve(skill);
        Assert.Equal(20_000, resolved.AreaMultiplierBasisPoints);
        Assert.Equal(skill.RangeRaw, resolved.RangeRaw);
        Assert.InRange(resolved.AreaRadiusRaw / (double)skill.BaseAreaRadiusRaw, 1.414, 1.415);
    }

    [Fact]
    public void AreaMoreAndLessModifyAreaBeforeSquareRootAndDoNotChangeCastDistance()
    {
        var config = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        var baseline = CombatSkillRules.Resolve(config, 1_000);
        var concentrated = CombatSkillRules.Resolve(config, 1_000, Rules(0));
        var expanded = CombatSkillRules.Resolve(config, 1_000, Rules(1));
        Assert.InRange(concentrated.AreaRadiusRaw / (double)baseline.AreaRadiusRaw, .706, .708);
        Assert.InRange(expanded.AreaRadiusRaw / (double)baseline.AreaRadiusRaw, 1.414, 1.415);
        Assert.Equal(baseline.RangeRaw, concentrated.RangeRaw);
        Assert.Equal(baseline.RangeRaw, expanded.RangeRaw);
        Assert.Equal(baseline.AreaRadiusRaw, CombatSkillRules.Resolve(config, 1_000, Rules(0, 1)).AreaRadiusRaw);
    }

    [Theory]
    [InlineData(2, 250, 15_000)]
    [InlineData(2, 251, 8_000)]
    [InlineData(3, 749, 8_000)]
    [InlineData(3, 750, 15_000)]
    public void CenterAndOuterRingUseFinalRadiusBoundaries(int option, int distance, int expected) =>
        Assert.Equal(expected, AreaRules.PositionMultiplier(Rules(option), distance, 1_000));

    [Fact]
    public void OverlappingAreasShareOneActionLimitWithoutBlockingOtherTargetsOrLaterPulses()
    {
        var queue = new CombatActionQueue();
        Assert.True(queue.TryAreaHit("normal", "target", 1, false));
        Assert.False(queue.TryAreaHit("normal", "target", 1, false));
        Assert.True(queue.TryAreaHit("overlap", "target", 1, true));
        Assert.True(queue.TryAreaHit("overlap", "target", 1, true));
        Assert.False(queue.TryAreaHit("overlap", "target", 1, true));
        Assert.True(queue.TryAreaHit("overlap", "other", 1, true));
        Assert.True(queue.TryAreaHit("overlap", "target", 2, true));
        var weapon = new WeaponProfile("test", 10, 10, 1_000, 0);
        Assert.Equal(6_000, MasteryRuntime.OffensiveMultiplier(Rules(4), SkillTag.Area, weapon, 100, 100));
        Assert.Equal(10_000, MasteryRuntime.OffensiveMultiplier(Rules(4), SkillTag.Area, weapon, 100, 100, hit: false));
    }

    [Fact]
    public void StructuredNotablesKeepBothEffectsAndSmallNodesAlternate()
    {
        var nodes = PassiveTreeCatalog.Build();
        var area = Assert.Single(nodes, node => node.Kind == PassiveNodeKind.Notable && node.DisplayName == "广域核心");
        Assert.Contains(new(PassiveEffectKind.IncreasedAreaEffectBasisPoints, 6_000), area.Effects);
        Assert.Contains(new(PassiveEffectKind.IncreasedAreaDamageBasisPoints, 3_000), area.Effects);
        var distant = Assert.Single(nodes, node => node.Kind == PassiveNodeKind.Notable && node.DisplayName == "极距施展");
        Assert.Contains(new(PassiveEffectKind.IncreasedSkillRangeBasisPoints, 4_000), distant.Effects);
        Assert.Contains(new(PassiveEffectKind.DistantHitMoreBasisPoints, 3_000), distant.Effects);
        string prefix = area.StableId[..area.StableId.LastIndexOf('.')];
        var smalls = nodes.Where(node => node.Kind == PassiveNodeKind.Small && node.StableId.StartsWith(prefix + ".", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, smalls.Count(node => node.Effects.Contains(new(PassiveEffectKind.IncreasedAreaDamageBasisPoints, 1_200))));
        Assert.Equal(2, smalls.Count(node => node.Effects.Contains(new(PassiveEffectKind.IncreasedAreaEffectBasisPoints, 2_000))));
    }

    [Fact]
    public void DistantNotableUsesFinalCastRangeAndOnlyAffectsHits()
    {
        var config = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        var skill = CombatSkillRules.Resolve(config, 1_000) with { RangeRaw = 10_000 };
        var build = Build(config, PassiveModifiers.Empty with
        { Specialized = new Dictionary<PassiveEffectKind, int> { [PassiveEffectKind.DistantHitMoreBasisPoints] = 3_000 } });
        int Damage(int distance, bool dot = false) => CombatSkillRules.ScaleOffensiveDamage(100,
            dot ? skill with { Role = SkillRole.DamageOverTime } : skill, config, build,
            SkillTag.Spell | SkillTag.Area, 100, 100, applyIncreased: false, distanceRaw: distance);
        Assert.Equal(100, Damage(6_999));
        Assert.Equal(130, Damage(7_000));
        Assert.Equal(100, Damage(9_000, true));
    }

    [Fact]
    public void AftershockUsesOneTerminalActionAndTheOriginalHitClock()
    {
        var config = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        var skill = CombatSkillRules.Resolve(config, 1_000);
        var build = Build(config, Rules(6));
        var queue = new CombatActionQueue();
        var hit = new CombatHitSnapshot("first", new(1_000, 2_000), skill, config, build,
            new(0, 0, 0, 0, 0, [new(100, DamageType.Fire, [DamageType.Fire], [])], []), [], false);
        queue.Record("original", hit, 10, false);
        queue.Record("original", hit with { TargetId = "second" }, 10, false);
        Assert.Empty(queue.TakeDue(899));
        var copy = Assert.Single(queue.TakeDue(900));
        Assert.Equal("mastery:aftershock", copy.Source);
        Assert.Equal(4_000, copy.Multiplier);
        Assert.Equal(2, copy.Action.Hits.Count);
        queue.Record("triggered", hit, 18, true);
        Assert.Empty(queue.TakeDue(2_000));
    }

    [Fact]
    public void AftershockHitsAfterFourTenthsOfASecondWithinItsOriginalArea()
    {
        var config = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        var result = new SpatialCombatRunner().Run(new(Build(config, Rules(6)), 1, 1, 5,
            false, false, false, 0, MaximumTicks: 200,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, Armor = 0 }]), 711);
        var first = result.Events.First(item => item.Kind == SpatialEventKind.EmberNova && item.Value > 0);
        var copy = result.Events.First(item => item.SourceId == "mastery:aftershock" && item.Value > 0);
        Assert.Equal(first.AtMilliseconds + 400, copy.AtMilliseconds);
        Assert.Equal(first.SourcePosition, copy.SourcePosition);
        int radius = CombatSkillRules.Resolve(config, 1_000).AreaRadiusRaw;
        Assert.All(result.Events.Where(item => item.SourceId == "mastery:aftershock" && item.Value > 0),
            item => Assert.True(Point.DistanceSquared(item.SourcePosition, item.TargetPosition) <= (long)radius * radius));
        Assert.DoesNotContain(result.Events, item => item.Detail.Contains("reaction:mastery:aftershock", StringComparison.Ordinal));
    }

    [Fact]
    public void PersistentAreaDurationAndDamageAreAppliedInProduction()
    {
        var config = new SkillConfiguration(SkillIds.VoidDecayField, SkillSupport.None);
        NodeCombatResult Run(PassiveModifiers profile) => new SpatialCombatRunner().Run(new(Build(config, profile),
            1, 1, 3, false, false, false, 0, MaximumTicks: 180,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, Armor = 0 }]), 711);
        var normal = Run(PassiveModifiers.Empty);
        var lasting = Run(Rules(5));
        var created = lasting.Events.First(item => item.Detail.Contains("area-created", StringComparison.Ordinal));
        Assert.Contains($"expires:{created.AtMilliseconds / 50 + 240}", created.Detail);
        var original = normal.Events.First(item => item.Detail.Contains("area-created", StringComparison.Ordinal));
        Assert.Contains($"expires:{original.AtMilliseconds / 50 + 120}", original.Detail);
        long limit = original.AtMilliseconds + 1_000;
        long Damage(NodeCombatResult result) => result.Events.Where(item => item.Detail == "dot:ground" && item.AtMilliseconds < limit).Sum(item => (long)item.Value);
        Assert.True(Damage(normal) > 0);
        Assert.InRange(Damage(lasting) / (double)Damage(normal), .70, .85);
    }

    private static TeamBuild Build(SkillConfiguration config, PassiveModifiers profile) => new(
        new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000, FlatMaximumMana: 100_000),
        new("test", 100, 100, 2_000, 0), new(SkillIds.HeavyStrike, SkillSupport.None), UseWarCry: false, AlwaysHit: true, CannotCrit: true,
        ActiveSkills: [config], PassiveProfile: profile);
}

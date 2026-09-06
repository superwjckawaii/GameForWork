using GameForWork.Core.Ascendancies;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Characters;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class ElementalistTests
{
    private static CombatProfile Profile(PrimaryElement element, params string[] nodes) => new(Ascendancy.Elementalist,
        nodes.Select(node => "core.ascendancy.elementalist." + node).ToArray(), new(PrimaryElement: element));

    [Theory]
    [InlineData(PrimaryElement.Fire, DamageType.Fire)]
    [InlineData(PrimaryElement.Cold, DamageType.Cold)]
    [InlineData(PrimaryElement.Lightning, DamageType.Lightning)]
    public void PrimaryExtraUsesOriginalPhysicalAndRetainsSourceWithoutRecursion(PrimaryElement element, DamageType type)
    {
        IReadOnlyList<DamageBranch> branches = [];
        var result = DamagePacketRules.ResolveMixed(100, SkillDamageType.Physical, default, SkillSupport.PhysicalToLightning,
            0, 0, 0, 0, 0, captureSource: value => branches = value, ascendancy: Profile(element, "conversion.core"));
        Assert.Equal(160, result.Total);
        var extra = Assert.Single(branches, branch => branch.IsExtra);
        Assert.Equal(60, extra.BaseDamage);
        Assert.Equal(type, extra.CurrentType);
        Assert.Equal(DamageType.Physical, extra.History.First());
    }

    [Theory]
    [InlineData(Ailment.Bleed)]
    [InlineData(Ailment.Ignite)]
    public void DualAilmentPenaltyOnlyAppliesWhileBothInstancesAreActive(Ailment ailment)
    {
        var state = new AilmentState { BleedMaximum = 2, IgniteMaximum = 2, BleedMultiplier = 8_000, IgniteMultiplier = 8_000 };
        state.Apply(ailment, DamageType.Fire, 100, 1_000, 0, "long");
        Assert.Equal(100, state.Remaining(ailment));
        state.Apply(ailment, DamageType.Fire, 100, 500, 0, "short");
        Assert.Equal(130, state.Advance(1_000, (_, damage) => damage).Sum(pulse => pulse.Damage));
    }

    [Fact]
    public void ColdUsesOneConditionAndElementalFamiliesCountColdOnlyOnce()
    {
        var profile = Profile(PrimaryElement.Cold, "cold.core", "ailment.core", "fire.core", "lightning.core");
        var all = new ElementalAilments(true, true, true, true);
        Assert.Equal(21_750, ElementalRules.TargetMultiplier(profile, DamageType.Cold, all, true, false));
        Assert.Equal(18_125, ElementalRules.TargetMultiplier(profile, DamageType.Cold, all with { Frozen = false }, false, false));
        Assert.Equal(18_850, ElementalRules.TargetMultiplier(profile, DamageType.Fire, all, true, false));
        Assert.Equal(14_500, ElementalRules.TargetMultiplier(profile, DamageType.Fire, all, false, false));
        Assert.Equal(14_500, ElementalRules.TargetMultiplier(profile, DamageType.Lightning, all, true, false));
        Assert.Equal(21_750, ElementalRules.TargetMultiplier(profile, DamageType.Lightning, all, true, true));
        Assert.Equal(10_000, ElementalRules.TargetMultiplier(profile, DamageType.Physical, all, true, true));
    }

    [Fact]
    public void ResonanceUsesThreeLiveElementsAndConsumesOncePerOriginalAction()
    {
        var state = new ElementalCombatState(Profile(PrimaryElement.Fire, "resonance.small", "resonance.core"));
        var three = new DamageBreakdown(0, 10, 10, 10, 0, 30, []);
        state.Observe(three, 0, true);
        Assert.Equal(0, state.Count(0));
        state.Observe(three, 1, false);
        Assert.Equal(3, state.Count(160));
        Assert.Equal(10_000, state.Begin("trigger", SkillTag.Fire, 160, true));
        Assert.Equal(13_000, state.Begin("channel", SkillTag.Fire, 160, false));
        Assert.Equal(0, state.Count(160));
        state.Observe(three, 160, false);
        Assert.Equal(13_000, state.Begin("channel", SkillTag.Fire, 165, false));
        Assert.Equal(3, state.Count(165));
        Assert.Equal(13_000, state.Begin("next", SkillTag.Cold, 166, false));
        state.Observe(three, 170, false);
        Assert.Equal(10_000, state.Begin("expired", SkillTag.Lightning, 330, false));
    }

    [Fact]
    public void OldCombatConfigurationDefaultsToFireAndRejectsInvalidElements()
    {
        var config = System.Text.Json.JsonSerializer.Deserialize<CombatConfiguration>("{}")!;
        Assert.Equal(PrimaryElement.Fire, config.PrimaryElement);
        Assert.True(config.Valid);
        Assert.False((config with { PrimaryElement = (PrimaryElement)99 }).Valid);
        var selected = config with { PrimaryElement = PrimaryElement.Lightning };
        Assert.Equal(selected, System.Text.Json.JsonSerializer.Deserialize<CombatConfiguration>(System.Text.Json.JsonSerializer.Serialize(selected)));
    }

    [Theory]
    [InlineData(PrimaryElement.Fire, "fire")]
    [InlineData(PrimaryElement.Cold, "cold")]
    [InlineData(PrimaryElement.Lightning, "lightning")]
    public void SelectedExtraElementReachesAuthoritativeHits(PrimaryElement primary, string field)
    {
        var config = new SkillConfiguration("archetypes.skill.backstab", SkillSupport.None);
        var build = new TeamBuild(new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 10_000, FlatMaximumMana: 10_000),
            new("core.base.rusted_greatsword", 100, 100, 1_000, 0), new(SkillIds.HeavyStrike, SkillSupport.None),
            AlwaysHit: true, CannotCrit: true, UseWarCry: false, ActiveSkills: [config], Ascendancy: Profile(primary, "conversion.core"));
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, MaximumTicks: 120,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, Armor = 0, MinimumPhysicalDamage = 0, MaximumPhysicalDamage = 0 }]), 731);
        var hit = result.Events.First(e => e.Value > 0 && e.Detail.StartsWith($"skill:{config.SkillId}|damage:"));
        Assert.Matches($"{field}:[1-9][0-9]*", hit.Detail);
        Assert.Matches("physical:[1-9][0-9]*", hit.Detail);
    }

    [Fact]
    public void NonDamagingAilmentEffectIsScaledBeforeThresholdAndThenCapped()
    {
        Assert.Equal(3_000, CombatRules.Chill(1_000, 1_000, increasedEffectBasisPoints: 10_000).EffectBasisPoints);
        Assert.Equal(5_000, CombatRules.Shock(1_000, 1_000, increasedEffectBasisPoints: 10_000).EffectBasisPoints);
        Assert.Equal(7_500, CombatRules.Shock(1_000, 1_000, 7_500, 10_000).EffectBasisPoints);
        Assert.Equal(0, CombatRules.Shock(1, 1_000).EffectBasisPoints);
        Assert.True(CombatRules.Shock(1, 1_000, increasedEffectBasisPoints: 10_000).EffectBasisPoints >= 500);
    }

    [Fact]
    public void PrimaryAndBranchIncreasesShareTheElementStage()
    {
        var build = new TeamBuild(new(1, new(0, 0, 0, 0), new(0, 0, 0)), Weapons.Unequipped, new(SkillIds.HeavyStrike, SkillSupport.None),
            Ascendancy: Profile(PrimaryElement.Cold, "conversion.small", "cold.small", "fire.small"));
        var modifiers = CombatSkillRules.OffensiveIncreases(build, SkillTag.Spell);
        Assert.Equal(4_500, modifiers.IncreasedByType![DamageType.Cold]);
        Assert.Equal(2_500, modifiers.IncreasedByType[DamageType.Fire]);
        Assert.Equal(0, modifiers.IncreasedByType[DamageType.Lightning]);
    }
}

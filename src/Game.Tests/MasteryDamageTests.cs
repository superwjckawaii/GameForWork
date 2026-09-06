using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Tests;

public sealed class MasteryDamageTests
{
    [Fact]
    public void LuckyPhysicalRollsAndPreviewAgreeOnTheDiscreteWeaponRange()
    {
        const int count = 20_000;
        long total = 0;
        var random = new GameForWork.Core.Simulation.Pcg32(971);
        for (int i = 0; i < count; i++) total += MasteryDamageRules.RollPhysical(10, 99, random, true);
        Assert.InRange(total / (double)count, 69.0, 70.0);
        Assert.Equal(69, MasteryDamageRules.ExpectedPhysicalRoll(10, 99, true));
        Assert.Equal(54, MasteryDamageRules.ExpectedPhysicalRoll(10, 99, false));
        Assert.Equal(10, MasteryDamageRules.ExpectedPhysicalRoll(10, 10, true));
    }

    [Fact]
    public void PassiveTypeIncreasesFollowConversionStagesAndDoNotLeakOntoUnrelatedAddedDamage()
    {
        var skill = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        var profile = PassiveModifiers.Empty with
        {
            IncreasedPhysicalDamageBasisPoints = 10_000,
            IncreasedElementalDamageBasisPoints = 10_000,
            IncreasedVoidDamageBasisPoints = 10_000
        };
        var build = new GameForWork.Core.Campaign.World.TeamBuild(new(1, new(0, 0, 0, 0), new(0, 0, 0)),
            new("test", 100, 100, 1_000, 0), skill, PassiveProfile: profile);
        var increases = GameForWork.Core.Skills.CombatSkillRules.OffensiveIncreases(build, SkillTag.Attack | SkillTag.Physical);
        var damage = DamagePacketRules.ResolveMixed(100, SkillDamageType.Physical, new AddedWeaponDamage(Fire: 100),
            SkillSupport.None, 0, 0, 0, 0, 0, equipment: new Dictionary<ItemModifierKind, int>
            { [ItemModifierKind.PhysicalToFireConversionBasisPoints] = 10_000, [ItemModifierKind.FireToVoidConversionBasisPoints] = 10_000 },
            modifiers: increases);
        Assert.Equal(1_200, damage.Void); // Physical -> fire -> void: 800; native fire -> void: 400.
    }

    [Fact]
    public void WeaponSkillsPreservePhysicalSourceAndApplyTheirOwnConversionsOnce()
    {
        var javelin = DamagePacketRules.ResolveMixed(100, SkillDamageType.Fire, default, SkillSupport.None, 0, 0, 0, 0, 0,
            configuration: new(SkillIds.AshJavelin, SkillSupport.None), mastery: new(Rules("物理.0")));
        Assert.Equal(80, javelin.Physical);
        Assert.Equal(80, javelin.Total);
        javelin = DamagePacketRules.ResolveMixed(100, SkillDamageType.Fire, default, SkillSupport.None, 0, 0, 0, 0, 0,
            configuration: new(SkillIds.AshJavelin, SkillSupport.None));
        Assert.Equal((50, 75), (javelin.Physical, javelin.Fire));
        foreach (string skill in new[] { "archetypes.skill.venom_blades", "archetypes.skill.corrosive_trap" })
        {
            var damage = DamagePacketRules.ResolveMixed(100, SkillDamageType.Void, default, SkillSupport.None, 0, 0, 0, 0, 0,
                configuration: new(skill, SkillSupport.None), mastery: new(Rules("物理.1")));
            Assert.Equal(140, damage.Void);
            Assert.Equal(0, damage.Physical);
        }
    }

    [Fact]
    public void VenomBladesPoisonChanceDoesNotInheritBleedSupportChance()
    {
        var skill = GameForWork.Core.Skills.CombatSkillRules.Resolve(new("archetypes.skill.venom_blades", SkillSupport.Bleed, Quality: 20), 1_000);
        Assert.Equal(Ailment.Poison, skill.Ailment);
        Assert.Equal(8_000, skill.AilmentChanceBasisPoints);
        Assert.True(skill.BleedChanceBasisPoints > 0);
    }

    [Fact]
    public void PurePhysicalKillsCreateTerminalCorpseBurstsInProduction()
    {
        var skill = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        var build = new GameForWork.Core.Campaign.World.TeamBuild(
            new(1, new(0, 0, 0, 0), new(0, 0, 0), FlatMaximumLife: 100_000), new("test", 100, 100, 2_000, 0),
            skill, UseWarCry: false, AlwaysHit: true, CannotCrit: true, ActiveSkills: [skill], PassiveProfile: Rules("物理.4"));
        var result = new GameForWork.Core.Spatial.SpatialCombatRunner().Run(new(build, 1, 1, 10, false, false, false, 0,
            MaximumTicks: 200, EnemyPool: [Enemies.CorruptedWorker with { Life = 50, Armor = 0 }]), 73);
        var bursts = result.Events.Where(item => item.Detail.StartsWith("physical-corpse-burst|", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(bursts);
        Assert.All(bursts, item => Assert.InRange(item.Value, 1, 5));
        Assert.DoesNotContain(result.Events, item => item.Detail.Contains("reaction:physical-corpse-burst", StringComparison.Ordinal));
    }

    private static PassiveModifiers Rules(params string[] ids) => PassiveModifiers.Empty with
    { MasteryMechanics = string.Join('|', ids.Select(id => "builds.mastery.rule." + id)) };
    private static DamageBreakdown Packet(PassiveModifiers profile, SkillDamageType type = SkillDamageType.Physical,
        IReadOnlyDictionary<ItemModifierKind, int>? equipment = null, bool hit = true, int targetLife = 100,
        Action<IReadOnlyList<DamageBranch>>? capture = null) => DamagePacketRules.ResolveMixed(100, type, default,
            SkillSupport.None, 0, 0, 0, 0, 0, equipment: equipment, captureSource: capture,
            mastery: new(profile, hit, targetLife, 100));

    [Fact]
    public void PhysicalExclusivityFiltersConvertedAndExtraDamageInsteadOfScalingBySkillTag()
    {
        var damage = Packet(Rules("物理.0", "物理.2"), equipment: new Dictionary<ItemModifierKind, int>
        { [ItemModifierKind.PhysicalToFireConversionBasisPoints] = 5_000 });
        Assert.Equal(80, damage.Physical);
        Assert.Equal(80, damage.Total);
        Assert.Equal(0, Packet(Rules("物理.0"), SkillDamageType.Fire).Total);
    }

    [Fact]
    public void FullConversionBonusExcludesExtraBranchesAndSurvivesLaterConversion()
    {
        var equipment = new Dictionary<ItemModifierKind, int>
        {
            [ItemModifierKind.PhysicalToFireConversionBasisPoints] = 10_000,
            [ItemModifierKind.PhysicalAsExtraFireBasisPoints] = 5_000,
            [ItemModifierKind.FireToVoidConversionBasisPoints] = 5_000
        };
        var source = new List<DamageBranch>();
        var damage = Packet(Rules("物理.1"), equipment: equipment, capture: branches => source.AddRange(branches));
        Assert.Equal(95, damage.Fire);
        Assert.Equal(95, damage.Void);
        Assert.Equal(100, source.Where(branch => !branch.IsExtra && branch.FullyConvertedPhysical).Sum(branch => branch.BaseDamage));
        Assert.Equal(50, source.Where(branch => branch.IsExtra).Sum(branch => branch.BaseDamage));
        Assert.Equal(150, Packet(Rules("物理.1"), equipment: equipment, hit: false).Total);
        equipment[ItemModifierKind.PhysicalToFireConversionBasisPoints] = 5_000;
        Assert.Equal(150, Packet(Rules("物理.1"), equipment: equipment).Total);
    }

    [Fact]
    public void PhysicalExtrasUseOriginalBaseAndEnterLaterFireConversion()
    {
        var damage = Packet(Rules("物理.2", "虚空.2"));
        Assert.Equal((100, 5, 10, 10, 7), (damage.Physical, damage.Fire, damage.Cold, damage.Lightning, damage.Void));
    }

    [Fact]
    public void VoidExclusivityPreservesPhysicalAndScalesOnlyFinalVoid()
    {
        var damage = Packet(Rules("虚空.0", "虚空.1", "物理.2"));
        Assert.Equal(50, damage.Physical);
        Assert.Equal(75, damage.Void);
        Assert.Equal(125, damage.Total);
        Assert.Equal(0, Packet(Rules("虚空.0"), SkillDamageType.Lightning).Total);
    }

    [Fact]
    public void HitTradeoffsDoNotReduceAilmentSourcesOrGroundDamage()
    {
        var profile = Rules("物理.3", "虚空.1", "虚空.3");
        var source = new List<DamageBranch>();
        var damage = Packet(profile, capture: branches => source.AddRange(branches));
        Assert.Equal(40, damage.Physical);
        Assert.Equal(32, damage.Void);
        Assert.Equal(100, Packet(profile, hit: false).Total);
        Assert.Equal(100, source.Sum(branch => branch.BaseDamage));
        Assert.Equal(15_000, MasteryDamageRules.AilmentMultiplier(profile, source.Single(branch => branch.CurrentType == DamageType.Physical), DamageType.Void, true));
        Assert.Equal(22_500, MasteryDamageRules.AilmentMultiplier(profile, source.Single(branch => branch.CurrentType == DamageType.Void), DamageType.Void, true));
        Assert.Equal(0, MasteryDamageRules.AilmentMultiplier(Rules("物理.0"), source[0], DamageType.Void, true));
    }

    [Fact]
    public void PhysicalExecuteChecksStrictCurrentLifeThresholdAndOnlyHits()
    {
        var profile = Rules("物理.6");
        Assert.Equal(100, Packet(profile, targetLife: 35).Total);
        Assert.Equal(160, Packet(profile, targetLife: 34).Total);
        Assert.Equal(100, Packet(profile, targetLife: 34, hit: false).Total);
        Assert.Equal(100, Packet(profile, SkillDamageType.Void, targetLife: 34).Total);
    }

    [Fact]
    public void RecordedOffensiveBranchesIncludeMasteryMultipliersBeforeTargetDefense()
    {
        var recorded = new List<DamageBranch>();
        var result = DamagePacketRules.ResolveMixed(100, SkillDamageType.Physical, default, SkillSupport.None,
            0, 0, 0, 0, 0, scaleBranch: branch => { recorded.Add(branch); return branch.BaseDamage * 2; },
            mastery: new(Rules("物理.0")));
        Assert.Equal(160, Assert.Single(recorded).BaseDamage);
        Assert.Equal(320, result.Total);
    }
}

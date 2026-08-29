using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P10;
using GameForWork.Core.P18;
using GameForWork.Core.P4;
using GameForWork.Core.P6;

namespace GameForWork.Tests;

public sealed class P18FeatureTests
{
    [Fact]
    public void ThreeAscendanciesHaveSixTwoNodeDirections()
    {
        Assert.Equal(36, P18AscendancyCatalog.Nodes.Count);
        foreach (P18Ascendancy path in Enum.GetValues<P18Ascendancy>().Where(value => value != P18Ascendancy.None))
        {
            IReadOnlyList<P18AscendancyNode> nodes = P18AscendancyCatalog.For(path);
            Assert.Equal(12, nodes.Count);
            Assert.Equal(6, nodes.Select(node => node.Direction).Distinct().Count());
            Assert.All(nodes.GroupBy(node => node.Direction), branch =>
            {
                Assert.Equal(2, branch.Count());
                P18AscendancyNode small = Assert.Single(branch, node => node.Kind == P18NodeKind.Reinforcement);
                Assert.Equal(small.StableId, Assert.Single(branch, node => node.Kind == P18NodeKind.Core).PrerequisiteId);
            });
        }
    }

    [Fact]
    public void SixBenchmarkBuildsCoverEntryAndEndgameForEveryPath()
    {
        Assert.Equal(6, P18BenchmarkBuilds.All.Count);
        foreach (P18Ascendancy path in Enum.GetValues<P18Ascendancy>().Where(value => value != P18Ascendancy.None))
        {
            P18BenchmarkBuild[] builds = P18BenchmarkBuilds.All.Where(build => build.Ascendancy == path).ToArray();
            Assert.Equal(2, builds.Length);
            Assert.Contains(builds, build => !build.EndgameGear);
            Assert.Contains(builds, build => build.EndgameGear);
            Assert.All(builds, build => Assert.Equal(8, build.Nodes.Count));
        }
    }

    [Fact]
    public void PointAwardsCapAtEightAndPathCannotMix()
    {
        var state = new P10EndgameState();
        Assert.True(state.AwardCampaignAscendancyPoints(3));
        Assert.True(state.AwardCampaignAscendancyPoints(5));
        state.AwardBreakthroughPoint(2);
        state.AwardBreakthroughPoint(2);
        state.AwardBreakthroughPoint(10);
        Assert.Equal(8, state.BreakthroughPoints);
        Assert.True(state.TrySelectAscendancy(P18Ascendancy.BastionEnvoy));
        Assert.True(state.TryAllocateAscendancy(P18NodeIds.BastionArmorSmall));
        Assert.True(state.TryAllocateAscendancy(P18NodeIds.BastionArmorCore));
        Assert.False(state.TryAllocateAscendancy(P18NodeIds.BloodLifeSmall));
    }

    [Fact]
    public void AscendancySelectionAndNodesRoundTrip()
    {
        var state = new P10EndgameState();
        state.AwardBreakthroughPoint(4);
        Assert.True(state.TrySelectAscendancy(P18Ascendancy.Linebreaker));
        Assert.True(state.TryAllocateAscendancy(P18NodeIds.BreakerAftershockSmall));
        Assert.True(state.TryAllocateAscendancy(P18NodeIds.BreakerAftershockCore));

        P10EndgameState restored = P10EndgameState.Restore(state.Capture());
        Assert.Equal(P18Ascendancy.Linebreaker, restored.SelectedAscendancy);
        Assert.Equal(state.AscendancyPassives.Order(), restored.AscendancyPassives.Order());
    }

    [Fact]
    public void StarterReceivesLifeAndManaFlasks()
    {
        P1GameSession session = NewSession();
        Assert.Equal("core.base.life_flask", session.HeroEquipment.Items[EquipmentSlot.Flask1].Base.StableId);
        Assert.Equal("core.base.mana_flask", session.HeroEquipment.Items[EquipmentSlot.Flask2].Base.StableId);
    }

    [Fact]
    public void NonSpellAttackCostsLessAndDefaultManaRegenerationIsSixPercent()
    {
        SkillDefinition attack = P1Skills.HeavyStrike;
        SkillConfiguration configuration = new(attack.StableId, SkillSupport.None);
        P6ResolvedSkill resolved = P6CombatSkillRules.Resolve(configuration, 1_000);
        Assert.Equal((attack.BaseManaCost * 8 + 9) / 10, resolved.ManaCost);

        var sheet = new CharacterSheet(1, new CharacterAttributes(10, 10, 10, 10), new DefensiveEquipment(0, 0, 0));
        Assert.Equal(sheet.MaximumMana().Value * 600 / 10_000, sheet.ManaRegenerationPerSecond().Value);
    }

    [Fact]
    public void BloodContractUsesLifeWithoutAnAiSafetyFloor()
    {
        P18CombatProfile profile = new(P18Ascendancy.BloodConqueror,
            [P18NodeIds.BloodLifeSmall, P18NodeIds.BloodLifeCore]);
        P6ResolvedSkill original = P6CombatSkillRules.Resolve(new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None), 1_000);
        P6ResolvedSkill result = P18AscendancyRules.ApplySkillCost(original, P1Skills.HeavyStrike.Tags, 1_000, profile);
        Assert.Equal(0, result.ManaCost);
        Assert.True(result.LifeCost >= 18);
        var resources = new ResourceState(new CharacterSheet(1, new CharacterAttributes(10, 10, 10, 10), new DefensiveEquipment(0, 0, 0)), initialLife: result.LifeCost + 1);
        Assert.True(P6CombatSkillRules.TryPay(resources, result));
        Assert.Equal(1, resources.Life);
    }

    [Fact]
    public void BastionLayersUseUnblockedAttackAndClearOnBlock()
    {
        var runtime = new P18CombatRuntime(new(P18Ascendancy.BastionEnvoy,
            [P18NodeIds.BastionLayersSmall, P18NodeIds.BastionLayersCore]));
        for (int index = 0; index < 5; index++) runtime.OnUnblockedAttack();
        Assert.Equal(5, runtime.BastionLayers);
        Assert.Equal(7_500, runtime.IncomingHitMultiplier(false, false, 100));
        runtime.OnAttackBlock();
        Assert.Equal(0, runtime.BastionLayers);
    }

    [Fact]
    public void MarchRequiresSixMetersAndNextSlamConsumesIt()
    {
        var runtime = new P18CombatRuntime(new(P18Ascendancy.Linebreaker,
            [P18NodeIds.BreakerMarchSmall, P18NodeIds.BreakerMarchCore]));
        runtime.Moved(5_999); Assert.False(runtime.MarchReady);
        runtime.Moved(1); Assert.True(runtime.MarchReady);
        int multiplier = runtime.ConsumeAttackMultiplier(SkillTag.Attack | SkillTag.Slam, false, true, new(0, false));
        Assert.Equal(16_000, multiplier);
        Assert.False(runtime.MarchReady);
    }

    [Fact]
    public void KillsRechargeLifeAndManaFlasksInSpatialCombat()
    {
        P1TeamBuild build = PowerfulBuild();
        P4NodeCombatResult result = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            build, 1, 20, 10, true, false, false, 0), 1801);
        Assert.Equal(P1BattleOutcome.HeroVictory, result.Outcome);
        Assert.Contains(result.Events, item => item.Kind == P4SpatialEventKind.FlaskCharge && item.Detail.Contains("life+", StringComparison.Ordinal));
    }

    private static P1GameSession NewSession() => P1GameSession.CreateNew(new PlayerIdentity(
        "升华测试", CharacterGender.Androgynous, CharacterSkinTone.Umber, CharacterHairStyle.Cropped,
        P1Ascendancy.IronOath), 1801);

    private static P1TeamBuild PowerfulBuild() => new(
        new CharacterSheet(80, new CharacterAttributes(300, 180, 160, 120), new DefensiveEquipment(900, 200, 0), FlatMaximumLife: 2_000),
        new WeaponProfile("p18.test", 260, 320, 1_600, 800),
        new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed),
        FlatAccuracy: 2_000, IncreasedDamageBasisPoints: 8_000, IncreasedBleedChanceBasisPoints: 5_000,
        LifeFlask: new LifeFlaskDefinition(100, 30, 10),
        ActiveSkills: [new(P1SkillIds.HeavyStrike, SkillSupport.Bleed), new(P1SkillIds.EarthCleave, SkillSupport.Bleed)],
        Flasks: [P1FlaskKind.Life, P1FlaskKind.Mana]);
}

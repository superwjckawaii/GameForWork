using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Endgame;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Spatial;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;

public sealed class AscendanciesFeatureTests
{
    [Fact]
    public void EighteenAscendanciesHaveSixTwoNodeDirections()
    {
        Assert.Equal(216, WarriorAscendancyCatalog.Nodes.Count);
        foreach (Ascendancy path in Enum.GetValues<Ascendancy>().Where(WarriorAscendancyCatalog.IsImplemented))
        {
            IReadOnlyList<AscendancyNode> nodes = WarriorAscendancyCatalog.For(path);
            Assert.Equal(12, nodes.Count);
            Assert.Equal(6, nodes.Select(node => node.Direction).Distinct().Count());
            Assert.All(nodes.GroupBy(node => node.Direction), branch =>
            {
                Assert.Equal(2, branch.Count());
                AscendancyNode small = Assert.Single(branch, node => node.Kind == NodeKind.Reinforcement);
                Assert.Equal(small.StableId, Assert.Single(branch, node => node.Kind == NodeKind.Core).PrerequisiteId);
            });
        }
    }

    [Fact]
    public void SixLegacyBenchmarkBuildsStillCoverEntryAndEndgameForFighterPaths()
    {
        Assert.Equal(6, WarriorBenchmarkBuilds.All.Count);
        foreach (Ascendancy path in new[]
                 { Ascendancy.BloodFighter, Ascendancy.IronGuardian, Ascendancy.Warbreaker })
        {
            BenchmarkBuild[] builds = WarriorBenchmarkBuilds.All.Where(build => build.Ascendancy == path).ToArray();
            Assert.Equal(2, builds.Length);
            Assert.Contains(builds, build => !build.EndgameGear);
            Assert.Contains(builds, build => build.EndgameGear);
            Assert.All(builds, build => Assert.Equal(8, build.Nodes.Count));
        }
    }

    [Fact]
    public void PointAwardsCapAtEightAndPathCannotMix()
    {
        var state = new EndgameState();
        Assert.True(state.AwardCampaignAscendancyPoints(3));
        Assert.True(state.AwardCampaignAscendancyPoints(5));
        state.AwardBreakthroughPoint(2);
        state.AwardBreakthroughPoint(2);
        state.AwardBreakthroughPoint(10);
        Assert.Equal(8, state.BreakthroughPoints);
        Assert.True(state.TrySelectAscendancy(Ascendancy.IronGuardian));
        Assert.True(state.TryAllocateAscendancy(WarriorNodeIds.BastionArmorSmall));
        Assert.True(state.TryAllocateAscendancy(WarriorNodeIds.BastionArmorCore));
        Assert.False(state.TryAllocateAscendancy(WarriorNodeIds.BloodLifeSmall));
    }

    [Fact]
    public void AscendancySelectionAndNodesRoundTrip()
    {
        var state = new EndgameState();
        state.AwardBreakthroughPoint(4);
        Assert.True(state.TrySelectAscendancy(Ascendancy.Warbreaker));
        Assert.True(state.TryAllocateAscendancy(WarriorNodeIds.BreakerAftershockSmall));
        Assert.True(state.TryAllocateAscendancy(WarriorNodeIds.BreakerAftershockCore));

        EndgameState restored = EndgameState.Restore(state.Capture());
        Assert.Equal(Ascendancy.Warbreaker, restored.SelectedAscendancy);
        Assert.Equal(state.AscendancyPassives.Order(), restored.AscendancyPassives.Order());
    }

    [Fact]
    public void StarterReceivesLifeAndManaFlasks()
    {
        GameSession session = NewSession();
        Assert.Equal("equipment.base.life_flask", session.HeroEquipment.Items[EquipmentSlot.Flask1].Base.StableId);
        Assert.Equal("equipment.base.mana_flask", session.HeroEquipment.Items[EquipmentSlot.Flask2].Base.StableId);
    }

    [Fact]
    public void NonSpellAttackCostsLessAndDefaultManaRegenerationIsSixPercent()
    {
        SkillDefinition attack = SkillDefinitions.HeavyStrike;
        SkillConfiguration configuration = new(attack.StableId, SkillSupport.None);
        ResolvedSkill resolved = CombatSkillRules.Resolve(configuration, 1_000);
        Assert.Equal((attack.BaseManaCost * 8 + 9) / 10, resolved.ManaCost);

        var sheet = new CharacterSheet(1, new CharacterAttributes(10, 10, 10, 10), new DefensiveEquipment(0, 0, 0));
        Assert.Equal(sheet.MaximumMana().Value * 600 / 10_000, sheet.ManaRegenerationPerSecond().Value);
    }

    [Fact]
    public void BloodContractUsesLifeWithoutAnAiSafetyFloor()
    {
        CombatProfile profile = new(Ascendancy.BloodFighter,
            [WarriorNodeIds.BloodLifeSmall, WarriorNodeIds.BloodLifeCore]);
        ResolvedSkill original = CombatSkillRules.Resolve(new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None), 1_000);
        ResolvedSkill result = WarriorAscendancyRules.ApplySkillCost(original, SkillDefinitions.HeavyStrike.Tags, 1_000, profile);
        Assert.Equal(0, result.ManaCost);
        Assert.True(result.LifeCost >= 18);
        var resources = new ResourceState(new CharacterSheet(1, new CharacterAttributes(10, 10, 10, 10), new DefensiveEquipment(0, 0, 0)), initialLife: result.LifeCost + 1);
        Assert.True(CombatSkillRules.TryPay(resources, result));
        Assert.Equal(1, resources.Life);
    }

    [Fact]
    public void BastionLayersUseUnblockedAttackAndClearOnBlock()
    {
        var runtime = new CombatRuntime(new(Ascendancy.IronGuardian,
            [WarriorNodeIds.BastionLayersSmall, WarriorNodeIds.BastionLayersCore]));
        for (int index = 0; index < 5; index++) runtime.OnUnblockedAttack();
        Assert.Equal(5, runtime.BastionLayers);
        Assert.Equal(7_500, runtime.IncomingHitMultiplier(false, false, 100));
        runtime.OnAttackBlock();
        Assert.Equal(0, runtime.BastionLayers);
    }

    [Fact]
    public void IronGuardianCurrentDescriptionsDriveAttributesArmorAndBlockMath()
    {
        CombatProfile profile = new(Ascendancy.IronGuardian,
        [
            WarriorNodeIds.BastionArmorSmall, WarriorNodeIds.BastionArmorCore,
            WarriorNodeIds.BastionAttackBlockSmall, WarriorNodeIds.BastionAttackBlockCore,
            WarriorNodeIds.BastionSpellBlockSmall, WarriorNodeIds.BastionSpellBlockCore,
        ]);
        var original = new CharacterSheet(50, new CharacterAttributes(480, 10, 10, 10),
            new DefensiveEquipment(1_000, 0, 0));

        CharacterSheet sheet = WarriorAscendancyRules.ApplySheet(original, profile, shieldArmor: 400);
        Assert.Equal(648, sheet.Attributes.Physique);
        Assert.Equal(2_900, sheet.Equipment.Armor);
        Assert.Equal(48_000, WarriorAscendancyRules.IncreasedAttackDamageBasisPoints(profile,
            sheet.Attributes.Physique));

        int attackMaximum = WarriorAscendancyRules.AttackBlockMaximumBasisPoints(7_500, profile, true);
        int attack = Math.Clamp(WarriorAscendancyRules.AttackBlockChanceBasisPoints(6_500, profile, true),
            0, attackMaximum);
        int spell = WarriorAscendancyRules.SpellBlockChanceBasisPoints(300, attack, profile, true);
        Assert.Equal(8_000, attackMaximum);
        Assert.Equal(8_000, attack);
        Assert.Equal(5_900, spell);
        Assert.Equal(0, new CombatRuntime(profile).IncomingHitMultiplier(true, true, 0));
    }

    [Fact]
    public void MarchRequiresSixMetersAndNextSlamConsumesIt()
    {
        var runtime = new CombatRuntime(new(Ascendancy.Warbreaker,
            [WarriorNodeIds.BreakerMarchSmall, WarriorNodeIds.BreakerMarchCore]));
        runtime.Moved(5_999); Assert.False(runtime.MarchReady);
        runtime.Moved(1); Assert.True(runtime.MarchReady);
        int multiplier = runtime.ConsumeAttackMultiplier(SkillTag.Attack | SkillTag.Slam, false, true, new(0, false));
        Assert.Equal(16_000, multiplier);
        Assert.False(runtime.MarchReady);
    }

    [Fact]
    public void KillsRechargeLifeAndManaFlasksInSpatialCombat()
    {
        TeamBuild build = PowerfulBuild();
        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            build, 1, 20, 10, true, false, false, 0), 1801);
        Assert.Equal(BattleOutcome.HeroVictory, result.Outcome);
        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.FlaskCharge && item.Detail.Contains("life+", StringComparison.Ordinal));
    }

    private static GameSession NewSession() => GameSession.CreateNew(new PlayerIdentity(
        "升华测试", CharacterGender.Androgynous, CharacterSkinTone.Umber, CharacterHairStyle.Cropped,
        BaseClass.Fighter), 1801);

    private static TeamBuild PowerfulBuild() => new(
        new CharacterSheet(80, new CharacterAttributes(300, 180, 160, 120), new DefensiveEquipment(900, 200, 0), FlatMaximumLife: 2_000),
        new WeaponProfile("ascendancies.test", 260, 320, 1_600, 800),
        new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.Bleed),
        FlatAccuracy: 2_000, IncreasedDamageBasisPoints: 8_000, IncreasedBleedChanceBasisPoints: 5_000,
        LifeFlask: new LifeFlaskDefinition(100, 30, 10),
        ActiveSkills: [new(SkillIds.HeavyStrike, SkillSupport.Bleed), new(SkillIds.EarthCleave, SkillSupport.Bleed)],
        Flasks: [FlaskKind.Life, FlaskKind.Mana]);
}

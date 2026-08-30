using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P10;
using GameForWork.Core.P12;
using GameForWork.Core.P14;

namespace GameForWork.Tests;

public sealed class P14FeatureTests
{
    [Fact]
    public void DemoContentMeetsTheSealedCatalogCounts()
    {
        Assert.Equal(80, P1Skills.All.Count);
        Assert.Equal(48, Enum.GetValues<SkillSupport>().Count(value => value != SkillSupport.None));
        Assert.Equal(88, P2SkillStones.All.Count(item => item.Kind == SkillStoneKind.Support));
        Assert.Equal(130, P1ItemBases.All.Count);
        Assert.Equal(24, P14UniqueItems.All.Count(item => !item.Mythic));
        Assert.Single(P14UniqueItems.All, item => item.Mythic);
        Assert.All(P14UniqueItems.All, item => Assert.False(string.IsNullOrWhiteSpace(item.RuleText)));
        Assert.Equal(P14UniqueItems.All.Count, P14UniqueItems.All.Select(item => item.RuleText).Distinct().Count());
        Assert.Equal(5, P14Flasks.All.Count);
        Assert.All(P14Flasks.All, flask =>
        {
            Assert.False(string.IsNullOrWhiteSpace(flask.EffectDescription));
            Assert.False(string.IsNullOrWhiteSpace(flask.AutoCondition));
            Assert.True(flask.MaximumCharges >= flask.ChargesPerUse);
        });
        Assert.Equal(80, P1Enemies.NormalEnemies.Count);
        Assert.Equal(18, Enum.GetValues<EliteAffix>().Length);
        Assert.Equal(12, P14Bosses.MapBosses.Count);
    }

    [Fact]
    public void MapPlanHasChoiceMechanicNodesBossAndImmutableAtlasSnapshot()
    {
        var map = new P1MapItem("p14-plan", 12, P12MapCatalog.Areas[0].StableId,
            RouteCandidates: [MapRoute.Safe, MapRoute.Abyss, MapRoute.LifeGarden], SelectedRoute: MapRoute.Abyss,
            Altar: P12MapAltar.RedOath, AtlasSnapshot: ["core.atlas.01.00"]);
        P14MapPlan plan = P14MapPlanner.Build(map, MapRoute.Abyss, ["core.atlas.02.00"], 55);

        Assert.InRange(plan.Nodes.Count, 5, 8);
        Assert.InRange(plan.RouteChoiceIndex, 2, 3);
        Assert.InRange(plan.Nodes.Count(node => node.Kind == P14MapNodeKind.AbyssFissure), 2, 4);
        Assert.Equal(P14MapNodeKind.Boss, plan.Nodes[^1].Kind);
        Assert.Equal(["core.atlas.01.00"], plan.AtlasSnapshot);
    }

    [Fact]
    public void MechanicFailureDoesNotFailMapAndGardenUsesThreePlots()
    {
        P14MechanicResult failed = P14MechanicRules.ResolveAbyss(1, 3, -1);
        Assert.False(failed.Completed);
        Assert.True(failed.MapMayContinue);
        Assert.Equal(0, failed.CurrencyBundles);

        P14MechanicResult garden = P14MechanicRules.ResolveGarden([0, 1, 2], 14);
        Assert.True(garden.Completed);
        Assert.True(garden.LifeForce > 0);
        Assert.Equal(3, P14Altars.Choices(P12MapAltar.RedOath, 14).Count);
        Assert.Equal(3, P14Altars.Choices(P12MapAltar.BlueOath, 14).Count);
    }

    [Fact]
    public void BreakthroughAndCitadelGrantSealedFirstAndRepeatRewards()
    {
        var progression = new CharacterProgression();
        progression.AddExperience(CharacterProgression.TotalExperienceToCap);
        var state = new P10EndgameState();

        Assert.True(state.TryCompleteFinalBreakthrough(progression.Level, true));
        Assert.True(progression.UnlockFinalBreakthrough());
        Assert.Equal(2, state.BreakthroughPoints);
        Assert.True(state.RecordCitadelVictory());
        Assert.Equal(4, state.BreakthroughPoints);
        Assert.Equal(5, state.BonusAtlasPoints);
        Assert.True(state.TryClaimCitadelMythic());
        Assert.False(state.TryClaimCitadelMythic());
        Assert.False(state.RecordCitadelVictory());
        Assert.Equal(1, state.MythicReforgeMaterials);
    }

    [Fact]
    public void EveryBossHasThreeTelegraphedSkillsPhaseAndEnrage()
    {
        P14BossDefinition[] bosses = P14Bosses.MapBosses.Concat([P14Bosses.Breakthrough])
            .Concat(P14Bosses.CitadelStages).ToArray();
        Assert.Equal(16, bosses.Length);
        Assert.All(bosses, boss =>
        {
            Assert.True(boss.Skills.Count >= 3);
            Assert.All(boss.Skills, skill => Assert.False(string.IsNullOrWhiteSpace(skill.Telegraph)));
            Assert.InRange(boss.PhaseThresholdBasisPoints, 1, 9_999);
            Assert.True(boss.EnrageSeconds > 0);
        });
    }

    [Fact]
    public void FinalTiersHaveDistinctRules()
    {
        Assert.Equal([17, 18, 19, 20], P14TierRules.FinalTiers.Select(rule => rule.Tier));
        Assert.Equal(4, P14TierRules.FinalTiers.Select(rule => rule.StableId).Distinct().Count());
        Assert.True(P12MapCombatModifiers.From(new P1MapItem("t17", 17)).EnemyDamageBasisPoints > 10_000);
        Assert.True(P12MapCombatModifiers.From(new P1MapItem("t18", 18)).PlayerRecoveryBasisPoints < 10_000);
        Assert.True(P12MapCombatModifiers.From(new P1MapItem("t19", 19)).ExtraElites);
        Assert.True(P12MapCombatModifiers.From(new P1MapItem("t20", 20)).EnemyLifeBasisPoints > 10_000);
    }

    [Fact]
    public void FiveEquippedFlasksEnterTheBuildAndHaveChargeDurationRules()
    {
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, ItemGenerator.Generate("core.base.rusted_greatsword", 20, ItemRarity.Basic, 1)));
        string[] flasks = ["life", "mana", "armor", "movement", "resistance"];
        for (int index = 0; index < flasks.Length; index++)
            Assert.True(loadout.TryEquip((EquipmentSlot)((int)EquipmentSlot.Flask1 + index),
                ItemGenerator.Generate($"core.base.{flasks[index]}_flask", 20, ItemRarity.Basic, (ulong)index + 2)));
        AssembledCharacterBuild build = CharacterBuildAssembler.Assemble(20,
            new CharacterAttributes(40, 30, 30, 20), loadout, new PassiveTreeAllocation(),
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None));
        Assert.Equal(5, build.Flasks.Count);
        var state = new P1UtilityFlaskState();
        Assert.True(state.TryUse());
        Assert.True(state.Active);
        for (int tick = 0; tick < 100; tick++) state.AdvanceTick();
        Assert.False(state.Active);
    }
}

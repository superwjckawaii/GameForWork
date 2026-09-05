using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Endgame;
using GameForWork.Core.Maps;
using GameForWork.Core.Content;

namespace GameForWork.Tests;

public sealed class ContentFeatureTests
{
    [Fact]
    public void DemoContentMeetsTheSealedCatalogCounts()
    {
        Assert.Equal(86, SkillDefinitions.All.Count);
        Assert.Equal(48, Enum.GetValues<SkillSupport>().Count(value => value != SkillSupport.None));
        Assert.Equal(98, SkillStoneCatalog.All.Count(item => item.Kind == SkillStoneKind.Support));
        Assert.Equal(244, ItemBases.All.Count);
        Assert.Equal(50, UniqueItems.All.Count(item => !item.Mythic));
        Assert.Equal(5, UniqueItems.All.Count(item => item.Mythic));
        Assert.All(UniqueItems.All, item => Assert.False(string.IsNullOrWhiteSpace(item.RuleText)));
        Assert.Equal(UniqueItems.All.Count, UniqueItems.All.Select(item => item.RuleText).Distinct().Count());
        Assert.Equal(5, Flasks.All.Count);
        Assert.All(Flasks.All, flask =>
        {
            Assert.False(string.IsNullOrWhiteSpace(flask.EffectDescription));
            Assert.False(string.IsNullOrWhiteSpace(flask.AutoCondition));
            Assert.True(flask.MaximumCharges >= flask.ChargesPerUse);
        });
        Assert.Equal(80, Enemies.NormalEnemies.Count);
        Assert.Equal(18, Enum.GetValues<EliteAffix>().Length);
        Assert.Equal(12, Bosses.MapBosses.Count);
    }

    [Fact]
    public void MapPlanHasChoiceMechanicNodesBossAndImmutableAtlasSnapshot()
    {
        var map = new MapItem("content-plan", 12, MapCatalog.Areas[0].StableId,
            RouteCandidates: [MapRoute.Safe, MapRoute.Abyss, MapRoute.LifeGarden], SelectedRoute: MapRoute.Abyss,
            Altar: MapAltar.RedOath, AtlasSnapshot: ["core.atlas.01.00"]);
        MapPlan plan = MapPlanner.Build(map, MapRoute.Abyss, ["core.atlas.02.00"], 55);

        Assert.Equal(9, plan.Nodes.Count);
        Assert.Contains(plan.Nodes, node => node.Kind == MapNodeKind.Altar && node.Gameplay?.Choice is not null);
        Assert.InRange(plan.Nodes.Count(node => node.Kind == MapNodeKind.AbyssFissure), 2, 4);
        Assert.Equal(MapNodeKind.Boss, plan.Nodes[^1].Kind);
        Assert.Equal(["core.atlas.01.00"], plan.AtlasSnapshot);
    }

    [Fact]
    public void MechanicFailureDoesNotFailMapAndGardenUsesThreePlots()
    {
        MechanicResult failed = MechanicRules.ResolveAbyss(1, 3, -1);
        Assert.False(failed.Completed);
        Assert.True(failed.MapMayContinue);
        Assert.Equal(0, failed.CurrencyBundles);

        MechanicResult garden = MechanicRules.ResolveGarden([0, 1, 2], 14);
        Assert.True(garden.Completed);
        Assert.True(garden.LifeForce > 0);
        Assert.Equal(3, Altars.Choices(MapAltar.RedOath, 14).Count);
        Assert.Equal(3, Altars.Choices(MapAltar.BlueOath, 14).Count);
    }

    [Fact]
    public void BreakthroughAndCitadelGrantSealedFirstAndRepeatRewards()
    {
        var progression = new CharacterProgression();
        progression.AddExperience(CharacterProgression.TotalExperienceToCap);
        var state = new EndgameState();

        Assert.True(state.TryCompleteFinalBreakthrough(progression.Level, true));
        Assert.True(progression.UnlockFinalBreakthrough());
        Assert.Equal(2, state.BreakthroughPoints);
        Assert.True(state.RecordCitadelVictory());
        Assert.Equal(4, state.BreakthroughPoints);
        Assert.Equal(5, state.BonusAtlasPoints);
        Assert.False(state.RecordCitadelVictory());
        Assert.Equal(1, state.MythicReforgeMaterials);
    }

    [Fact]
    public void EveryBossHasThreeTelegraphedSkillsPhaseAndEnrage()
    {
        BossDefinition[] bosses = Bosses.MapBosses.Concat([Bosses.Breakthrough])
            .Concat(Bosses.CitadelStages).ToArray();
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
        Assert.Equal([17, 18, 19, 20], TierRules.FinalTiers.Select(rule => rule.Tier));
        Assert.Equal(4, TierRules.FinalTiers.Select(rule => rule.StableId).Distinct().Count());
        Assert.True(MapCombatModifiers.From(new MapItem("t17", 17)).EnemyDamageBasisPoints > 10_000);
        Assert.True(MapCombatModifiers.From(new MapItem("t18", 18)).PlayerRecoveryBasisPoints < 10_000);
        Assert.True(MapCombatModifiers.From(new MapItem("t19", 19)).ExtraElites);
        Assert.True(MapCombatModifiers.From(new MapItem("t20", 20)).EnemyLifeBasisPoints > 10_000);
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
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None));
        Assert.Equal(5, build.Flasks.Count);
        var sheet = new CharacterSheet(20, new(40, 30, 30, 20), new(0, 0, 0));
        var state = new GameForWork.Core.Combat.FlaskRack(new(sheet, new("test", 1, 1, 1_000, 0),
            new(SkillIds.HeavyStrike, SkillSupport.None), Flasks: [FlaskKind.Armor]));
        var hero = new ResourceState(sheet);
        Assert.NotNull(state.TryUse(FlaskKind.Armor, hero, new GameForWork.Core.Simulation.Pcg32(1)));
        Assert.True(state.Active(FlaskKind.Armor));
        state.Advance(hero, 5_000);
        Assert.False(state.Active(FlaskKind.Armor));
    }
}

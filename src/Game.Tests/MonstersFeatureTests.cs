using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Endgame;
using GameForWork.Core.Maps;
using GameForWork.Core.Content;
using GameForWork.Core.Art;
using GameForWork.Core.Monsters;
using GameForWork.Core.Simulation;

namespace GameForWork.Tests;

public sealed class MonstersFeatureTests
{
    [Fact]
    public void TenFamiliesContainEightMultiSkillMonstersEach()
    {
        Assert.Equal(10, MonsterCatalog.Families.Count);
        Assert.Equal(80, Enemies.NormalEnemies.Count);
        Assert.All(Enemies.NormalEnemies.GroupBy(enemy => enemy.Family), family => Assert.Equal(8, family.Count()));
        Assert.All(Enemies.NormalEnemies, enemy => Assert.NotEmpty(enemy.EffectiveSkills));
        Assert.True(Enemies.NormalEnemies.Count(enemy => enemy.EffectiveSkills.Count > 1) >= 40);
        Assert.All(Enemies.NormalEnemies.SelectMany(enemy => enemy.EffectiveSkills), skill =>
        {
            Assert.False(string.IsNullOrWhiteSpace(skill.DisplayName));
            Assert.InRange(skill.DamageMultiplierBasisPoints, 1_000, 30_000);
            Assert.InRange(skill.CooldownMultiplierBasisPoints, 5_000, 30_000);
        });
    }

    [Fact]
    public void PackPlannerAllowsHomogeneousAndSingleRoleExtremes()
    {
        IReadOnlyList<EnemyProfile> pool = Enemies.ForEncounter(90, EnemyFamily.Warfront);
        bool homogeneous = false;
        bool supportOnly = false;
        for (ulong seed = 1; seed <= 500 && (!homogeneous || !supportOnly); seed++)
        {
            IReadOnlyList<EnemyProfile> selected = MonsterCatalog.SelectPackPool(pool, new Pcg32(seed));
            homogeneous |= selected.Count == 1;
            supportOnly |= selected.Count > 0 && selected.All(enemy => enemy.Role == EnemyRole.Support);
        }
        Assert.True(homogeneous);
        Assert.True(supportOnly);
    }

    [Fact]
    public void AllMapAreasHaveTwoFamiliesAndUniqueBosses()
    {
        Assert.Equal(12, MapCatalog.Areas.Count);
        Assert.Equal(12, Bosses.MapBosses.Count);
        Assert.Equal(12, Bosses.MapBosses.Select(boss => boss.StableId).Distinct().Count());
        Assert.Equal(12, Bosses.MapBosses.Select(boss => boss.AreaStableId).Distinct().Count());
        foreach (MapArea area in MapCatalog.Areas)
        {
            EnemyFamily first = MonsterCatalog.FamilyForEncounter(area.StableId, MapNodeKind.Encounter,
                MapAltar.None, 1, 0);
            EnemyFamily second = MonsterCatalog.FamilyForEncounter(area.StableId, MapNodeKind.Encounter,
                MapAltar.None, 2, 0);
            Assert.NotEqual(first, second);
        }
        Assert.Equal(5, Bosses.CampaignBosses.Count);
    }

    [Fact]
    public void WarfrontUsesFiveNodeRouteAndFailureStillDiscoversIt()
    {
        var map = new MapItem("monsters-warfront", 8, MapCatalog.Areas[3].StableId,
            RouteCandidates: [MapRoute.Safe, MapRoute.Warfront], SelectedRoute: MapRoute.Warfront,
            Altar: MapAltar.RedOath);
        MapPlan plan = MapPlanner.Build(map, MapRoute.Warfront, [], 27);
        Assert.Equal(6, plan.Nodes.Count); // Five warfront nodes plus the independent map altar.
        Assert.Equal(MapNodeKind.WarfrontEncounter, plan.Nodes[0].Kind);
        Assert.Equal(MapNodeKind.RouteChoice, plan.Nodes[2].Kind);
        Assert.Equal(MapNodeKind.WarfrontOfficer, plan.Nodes[3].Kind);
        Assert.Equal(MapNodeKind.WarfrontCommander, plan.Nodes[^1].Kind);
        Assert.Equal(MapAltar.RedOath, plan.Altar);

        var state = new EndgameState();
        state.RecordWarfrontAttempt(8, false);
        Assert.True(state.WarfrontDiscovered);
        Assert.Equal(0, state.WarfrontMerit); // No flat consolation currency without defeated enemies.
        Assert.Equal(0, state.WarfrontReputation);
        EndgameState restored = EndgameState.Restore(state.Capture());
        Assert.True(restored.WarfrontDiscovered);
        Assert.Equal(state.WarfrontMerit, restored.WarfrontMerit);
    }

    [Fact]
    public void EveryEnemyResolvesToExpandedAnimationAtlas()
    {
        Assert.Equal(26, ArtContract.EnemyBodyRigCount);
        Assert.All(Enemies.NormalEnemies, enemy =>
            Assert.InRange(ArtContract.EnemyRig(enemy.StableId), 0, ArtContract.EnemyBodyRigCount - 1));
        Assert.Contains(Enemies.NormalEnemies.Where(enemy => enemy.Family == EnemyFamily.Warfront),
            enemy => ArtContract.EnemyRig(enemy.StableId) >= 20);
    }
}

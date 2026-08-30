using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.World;
using GameForWork.Core.P10;
using GameForWork.Core.P12;
using GameForWork.Core.P14;
using GameForWork.Core.P21;
using GameForWork.Core.P27;
using GameForWork.Core.Simulation;

namespace GameForWork.Tests;

public sealed class P27FeatureTests
{
    [Fact]
    public void TenFamiliesContainEightMultiSkillMonstersEach()
    {
        Assert.Equal(10, P27MonsterCatalog.Families.Count);
        Assert.Equal(80, P1Enemies.NormalEnemies.Count);
        Assert.All(P1Enemies.NormalEnemies.GroupBy(enemy => enemy.Family), family => Assert.Equal(8, family.Count()));
        Assert.All(P1Enemies.NormalEnemies, enemy => Assert.NotEmpty(enemy.EffectiveSkills));
        Assert.True(P1Enemies.NormalEnemies.Count(enemy => enemy.EffectiveSkills.Count > 1) >= 40);
        Assert.All(P1Enemies.NormalEnemies.SelectMany(enemy => enemy.EffectiveSkills), skill =>
        {
            Assert.False(string.IsNullOrWhiteSpace(skill.DisplayName));
            Assert.InRange(skill.DamageMultiplierBasisPoints, 1_000, 30_000);
            Assert.InRange(skill.CooldownMultiplierBasisPoints, 5_000, 30_000);
        });
    }

    [Fact]
    public void PackPlannerAllowsHomogeneousAndSingleRoleExtremes()
    {
        IReadOnlyList<EnemyProfile> pool = P1Enemies.ForEncounter(90, EnemyFamily.Warfront);
        bool homogeneous = false;
        bool supportOnly = false;
        for (ulong seed = 1; seed <= 500 && (!homogeneous || !supportOnly); seed++)
        {
            IReadOnlyList<EnemyProfile> selected = P27MonsterCatalog.SelectPackPool(pool, new Pcg32(seed));
            homogeneous |= selected.Count == 1;
            supportOnly |= selected.Count > 0 && selected.All(enemy => enemy.Role == EnemyRole.Support);
        }
        Assert.True(homogeneous);
        Assert.True(supportOnly);
    }

    [Fact]
    public void AllMapAreasHaveTwoFamiliesAndUniqueBosses()
    {
        Assert.Equal(12, P12MapCatalog.Areas.Count);
        Assert.Equal(12, P14Bosses.MapBosses.Count);
        Assert.Equal(12, P14Bosses.MapBosses.Select(boss => boss.StableId).Distinct().Count());
        Assert.Equal(12, P14Bosses.MapBosses.Select(boss => boss.AreaStableId).Distinct().Count());
        foreach (P12MapArea area in P12MapCatalog.Areas)
        {
            EnemyFamily first = P27MonsterCatalog.FamilyForEncounter(area.StableId, P14MapNodeKind.Encounter,
                P12MapAltar.None, 1, 0);
            EnemyFamily second = P27MonsterCatalog.FamilyForEncounter(area.StableId, P14MapNodeKind.Encounter,
                P12MapAltar.None, 2, 0);
            Assert.NotEqual(first, second);
        }
        Assert.Equal(5, P14Bosses.CampaignBosses.Count);
    }

    [Fact]
    public void WarfrontUsesFiveNodeRouteAndFailureStillDiscoversIt()
    {
        var map = new P1MapItem("p27-warfront", 8, P12MapCatalog.Areas[3].StableId,
            RouteCandidates: [MapRoute.Safe, MapRoute.Warfront], SelectedRoute: MapRoute.Warfront,
            Altar: P12MapAltar.RedOath);
        P14MapPlan plan = P14MapPlanner.Build(map, MapRoute.Warfront, [], 27);
        Assert.Equal(5, plan.Nodes.Count);
        Assert.Equal(P14MapNodeKind.WarfrontEncounter, plan.Nodes[0].Kind);
        Assert.Equal(P14MapNodeKind.RouteChoice, plan.Nodes[2].Kind);
        Assert.Equal(P14MapNodeKind.WarfrontOfficer, plan.Nodes[3].Kind);
        Assert.Equal(P14MapNodeKind.WarfrontCommander, plan.Nodes[4].Kind);
        Assert.Equal(P12MapAltar.RedOath, plan.Altar);

        var state = new P10EndgameState();
        state.RecordWarfrontAttempt(8, false);
        Assert.True(state.WarfrontDiscovered);
        Assert.Equal(24, state.WarfrontMerit);
        Assert.Equal(0, state.WarfrontReputation);
        P10EndgameState restored = P10EndgameState.Restore(state.Capture());
        Assert.True(restored.WarfrontDiscovered);
        Assert.Equal(state.WarfrontMerit, restored.WarfrontMerit);
    }

    [Fact]
    public void EveryEnemyResolvesToExpandedAnimationAtlas()
    {
        Assert.Equal(26, P21ArtContract.EnemyBodyRigCount);
        Assert.All(P1Enemies.NormalEnemies, enemy =>
            Assert.InRange(P21ArtContract.EnemyRig(enemy.StableId), 0, P21ArtContract.EnemyBodyRigCount - 1));
        Assert.Contains(P1Enemies.NormalEnemies.Where(enemy => enemy.Family == EnemyFamily.Warfront),
            enemy => P21ArtContract.EnemyRig(enemy.StableId) >= 20);
    }
}

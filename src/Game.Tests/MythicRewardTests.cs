using GameForWork.Core.Atlas;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Economy;
using GameForWork.Core.Encounters;
using GameForWork.Core.Endgame;
using GameForWork.Core.Maps;

namespace GameForWork.Tests;

public sealed class MythicRewardTests
{
    private static string[] Atlas => AtlasCatalog.All.Select(node => node.StableId).ToArray();

    [Theory]
    [InlineData(MapRoute.Abyss, MapAltar.None, MythicRewardRules.WorldEater)]
    [InlineData(MapRoute.Safe, MapAltar.BlueOath, MythicRewardRules.FinalMagic)]
    [InlineData(MapRoute.LifeGarden, MapAltar.None, MythicRewardRules.PrimalCrown)]
    [InlineData(MapRoute.Warfront, MapAltar.None, MythicRewardRules.EternalCore)]
    public void HighestIntensityEndgamesHaveTheirOwnMythicFirstClear(
        MapRoute route, MapAltar altar, string expected)
    {
        var policy = new GameplayPolicy(AbyssIntensity: 5, AbyssFinalGuardian: true,
            Garden: GardenMode.Triple, Blue: AltarMode.Extreme, Warfront: WarfrontMode.Decisive);
        var map = new MapItem("mythic-source", 20, MapCatalog.Areas[0].StableId,
            RouteCandidates: [route], SelectedRoute: route, Altar: altar, AtlasSnapshot: Atlas, Gameplay: policy);

        Assert.Contains(expected, MythicRewardRules.ForCompletion(map, route));
    }

    [Fact]
    public void LesserModesDoNotGrantMythicsAndCitadelOnlyGrantsHeartOfAsh()
    {
        var ordinary = new MapItem("ordinary", 20, MapCatalog.Areas[0].StableId,
            RouteCandidates: [MapRoute.Abyss], SelectedRoute: MapRoute.Abyss, AtlasSnapshot: Atlas,
            Gameplay: new(AbyssIntensity: 4, Garden: GardenMode.Twin, Blue: AltarMode.HighPressure,
                Warfront: WarfrontMode.Expanded));
        var citadel = ordinary with { InstanceId = EndgameState.CitadelMapPrefix + "reward" };

        Assert.Empty(MythicRewardRules.ForCompletion(ordinary, MapRoute.Abyss));
        Assert.Equal([MythicRewardRules.HeartOfAsh], MythicRewardRules.ForCompletion(citadel, MapRoute.Safe));
        Assert.DoesNotContain(LegendaryDrops.Pool("citadel"), item => item.Mythic);
    }

    [Fact]
    public void MythicRewardIdentityPersistsAndCannotBeGrantedTwice()
    {
        var state = new EndgameState();
        Assert.True(state.TryRecordMythicReward(MythicRewardRules.WorldEater));
        Assert.False(state.TryRecordMythicReward(MythicRewardRules.WorldEater));

        EndgameState restored = EndgameState.Restore(state.Capture());
        Assert.Contains(MythicRewardRules.WorldEater, restored.GrantedMythics);
        Assert.False(restored.TryRecordMythicReward(MythicRewardRules.WorldEater));
    }
}

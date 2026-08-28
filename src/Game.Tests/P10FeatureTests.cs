using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P10;
using GameForWork.Core.P1;

namespace GameForWork.Tests;

public sealed class P10FeatureTests
{
    [Fact]
    public void AtlasHasSixFunctionalRoutesAndThreeHundredSixtyNodes()
    {
        Assert.Equal(360, P10AtlasTree.Nodes.Count);
        Assert.Equal(6, P10AtlasTree.Nodes.Select(node => node.Theme).Distinct().Count());
        Assert.Equal(36, P10AtlasTree.Nodes.Count(node => node.Notable));
    }

    [Fact]
    public void TierCompletionAwardsAtlasPointsAndOneToThreeMechanics()
    {
        var state = new P10EndgameState();
        IReadOnlyList<P10MapMechanic> mechanics = state.RecordMapCompletion(new P1MapItem("p10-t11", 11), MapRoute.Abyss, 77);

        Assert.InRange(mechanics.Count, 1, 3);
        Assert.Equal(1, state.EarnedAtlasPoints);
        Assert.Contains(11, state.CompletedTiers);
        Assert.Equal(1, state.CitadelFragments);
        Assert.True(state.TryAllocateAtlas("core.atlas.00.00"));
    }

    [Fact]
    public void HighTierMapsCreateCitadelTicketAndBreakthroughPoint()
    {
        var state = new P10EndgameState();
        for (int index = 0; index < P10EndgameState.CitadelFragmentsPerTicket; index++)
            state.RecordMapCompletion(new P1MapItem($"p10-fragment-{index}", 20), MapRoute.Abyss, (ulong)index + 1);

        Assert.Equal(1, state.CitadelTickets);
        Assert.True(state.TryConsumeCitadelTicket());
        state.RecordCitadelVictory();
        Assert.True(state.CitadelDefeated);
        Assert.Equal(1, state.BreakthroughPoints);
        Assert.True(state.TryAllocateAscendancy("core.ascendancy.iron_oath.01"));
    }

    [Fact]
    public void MasteryChoiceAndRadiusJewelAffectModifiersAndRestore()
    {
        var allocation = new PassiveTreeAllocation();
        Assert.True(allocation.TryAllocate("core.passive.heavy.1", 4));
        Assert.True(allocation.TryAllocate("core.passive.constellation.00.00", 4));
        for (int index = 1; index <= 45; index++)
            Assert.True(allocation.TryAllocate($"core.passive.constellation.00.{index:00}", 120));
        Assert.True(allocation.TrySelectMastery("core.passive.constellation.00.45", 0));

        PassiveTreeAllocation restored = PassiveTreeAllocation.Restore(allocation.Allocated, 5, allocation.MasterySelections, allocation.SocketedJewels);
        Assert.Equal(allocation.CalculateModifiers(), restored.CalculateModifiers());
    }

    [Fact]
    public void P10SnapshotRoundTrips()
    {
        var state = new P10EndgameState();
        state.RecordMapCompletion(new P1MapItem("p10-roundtrip", 15), MapRoute.Safe, 99);
        Assert.True(state.TryAllocateAtlas("core.atlas.01.00"));

        P10EndgameState restored = P10EndgameState.Restore(state.Capture());
        Assert.Equal(state.CompletedTiers.Order(), restored.CompletedTiers.Order());
        Assert.Equal(state.AtlasPassives.Order(), restored.AtlasPassives.Order());
        Assert.Equal(state.RedFavor, restored.RedFavor);
        Assert.Equal(state.CitadelFragments, restored.CitadelFragments);
    }

    [Fact]
    public void CitadelTicketSchedulesARealHeroMap()
    {
        P1GameSession session = P1GameSession.CreateNew(new PlayerIdentity("攻坚测试", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, P1Ascendancy.IronOath), 101);
        for (int index = 0; index < P10EndgameState.CitadelFragmentsPerTicket; index++)
            session.Endgame.RecordMapCompletion(new P1MapItem($"ticket-{index}", 20), MapRoute.Abyss, (ulong)index + 1);

        Assert.True(session.TryChallengeCitadel());
        Assert.Single(session.World.Hero.Queue.Maps);
        Assert.True(P10EndgameState.IsCitadel(session.World.Hero.Queue.Maps[0]));
        Assert.Equal(0, session.Endgame.CitadelTickets);
    }
}

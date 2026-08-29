using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P10;
using GameForWork.Core.P1;
using GameForWork.Core.P18;

namespace GameForWork.Tests;

public sealed class P10FeatureTests
{
    [Fact]
    public void AtlasHasSixFunctionalRoutesAndThreeHundredSixtyNodes()
    {
        Assert.Equal(360, P10AtlasTree.Nodes.Count);
        Assert.Equal(6, P10AtlasTree.Nodes.Select(node => node.Theme).Distinct().Count());
        Assert.Equal(36, P10AtlasTree.Nodes.Count(node => node.Notable));
        Assert.All(P10AtlasTree.Nodes, node =>
        {
            Assert.InRange(node.X, -P10AtlasTree.LayoutExtent, P10AtlasTree.LayoutExtent);
            Assert.InRange(node.Y, -P10AtlasTree.LayoutExtent, P10AtlasTree.LayoutExtent);
        });
        Assert.Equal(P10AtlasTree.Nodes.Count,
            P10AtlasTree.Nodes.Select(node => (MathF.Round(node.X, 2), MathF.Round(node.Y, 2))).Distinct().Count());
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
        Assert.Equal(2, state.BreakthroughPoints);
        Assert.True(state.TrySelectAscendancy(P18Ascendancy.BloodFighter));
        Assert.True(state.TryAllocateAscendancy(P18NodeIds.BloodLifeSmall));
    }

    [Fact]
    public void MasteryChoiceAndRadiusJewelAffectModifiersAndRestore()
    {
        var allocation = new PassiveTreeAllocation();
        Assert.True(allocation.TryAllocatePath("core.passive.v3.cluster.00.00.mastery", 120));
        Assert.True(allocation.TrySelectMastery("core.passive.v3.cluster.00.00.mastery", 0));
        Assert.True(allocation.TryAllocatePath("core.passive.v3.jewel.00.00", 120));
        Assert.True(allocation.TrySocketJewel("core.passive.v3.jewel.00.00", PassiveJewelKind.CrimsonMemory));

        PassiveTreeAllocation restored = PassiveTreeAllocation.Restore(allocation.Allocated, 5, allocation.MasterySelections, allocation.SocketedJewels);
        PassiveBuildModifiers expected = allocation.CalculateModifiers();
        PassiveBuildModifiers actual = restored.CalculateModifiers();
        Assert.Equal(expected with { Advanced = null }, actual with { Advanced = null });
        Assert.Equal(expected.Advanced! with { Specialized = null }, actual.Advanced! with { Specialized = null });
        Assert.Equal(expected.Advanced.Specialized!.OrderBy(pair => pair.Key), actual.Advanced.Specialized!.OrderBy(pair => pair.Key));
    }

    [Fact]
    public void PassiveRestoreAcceptsAConnectedRingPathInSnapshotOrder()
    {
        string[] capturedOrder =
        [
            "core.passive.v3.travel.00.13",
            "core.passive.v3.travel.00.15",
            "core.passive.v3.travel.00.14",
        ];

        PassiveTreeAllocation restored = PassiveTreeAllocation.Restore(capturedOrder, 0);

        Assert.Equal(capturedOrder.Order(), restored.Allocated.Order());
    }

    [Fact]
    public void ShortestPathIsAtomicAndMasteryAndJewelChoicesAreUnique()
    {
        var allocation = new PassiveTreeAllocation();
        string farMastery = "core.passive.v3.cluster.00.00.mastery";
        Assert.False(allocation.TryAllocatePath(farMastery, 2));
        Assert.Empty(allocation.Allocated);
        Assert.True(allocation.TryAllocatePath(farMastery, 120));
        Assert.True(allocation.TryAllocatePath("core.passive.v3.cluster.00.01.mastery", 120));
        Assert.True(allocation.TrySelectMastery(farMastery, 0));
        Assert.True(allocation.TrySelectMastery("core.passive.v3.cluster.00.01.mastery", 0));

        Assert.True(allocation.TryAllocatePath("core.passive.v3.jewel.00.00", 120));
        Assert.True(allocation.TryAllocatePath("core.passive.v3.jewel.00.01", 120));
        Assert.True(allocation.TrySocketJewel("core.passive.v3.jewel.00.00", PassiveJewelKind.CrimsonMemory));
        Assert.False(allocation.TrySocketJewel("core.passive.v3.jewel.00.01", PassiveJewelKind.CrimsonMemory));
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
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, P23BaseClass.Fighter), 101);
        for (int index = 0; index < P10EndgameState.CitadelFragmentsPerTicket; index++)
            session.Endgame.RecordMapCompletion(new P1MapItem($"ticket-{index}", 20), MapRoute.Abyss, (ulong)index + 1);

        Assert.True(session.TryChallengeCitadel());
        Assert.Single(session.World.Hero.Queue.Maps);
        Assert.True(P10EndgameState.IsCitadel(session.World.Hero.Queue.Maps[0]));
        Assert.Equal(0, session.Endgame.CitadelTickets);
    }
}

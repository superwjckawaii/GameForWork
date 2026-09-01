using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P10;
using GameForWork.Core.P1;
using GameForWork.Core.P18;
using GameForWork.Core.P26;

namespace GameForWork.Tests;

public sealed class P10FeatureTests
{
    [Fact]
    public void AtlasHasTenCategoriesAndOneHundredTwentyGoldNodes()
    {
        Assert.Equal(120, P10AtlasTree.Nodes.Count);
        Assert.Equal(10, P10AtlasTree.Nodes.Select(node => node.Theme).Distinct().Count());
        Assert.Equal(477_000, P10AtlasTree.Nodes.Sum(node => node.GoldCost));
        Assert.All(P10AtlasTree.Nodes, node =>
        {
            Assert.InRange(node.X, -P10AtlasTree.LayoutExtent, P10AtlasTree.LayoutExtent);
            Assert.InRange(node.Y, -P10AtlasTree.LayoutExtent, P10AtlasTree.LayoutExtent);
        });
        Assert.Equal(P10AtlasTree.Nodes.Count,
            P10AtlasTree.Nodes.Select(node => (MathF.Round(node.X, 2), MathF.Round(node.Y, 2))).Distinct().Count());
    }

    [Fact]
    public void TierCompletionUnlocksMechanicsWhileAtlasUsesGold()
    {
        var state = new P10EndgameState();
        IReadOnlyList<P10MapMechanic> mechanics = state.RecordMapCompletion(new P1MapItem("p10-t11", 11), MapRoute.Abyss, 77);

        Assert.InRange(mechanics.Count, 1, 3);
        Assert.Equal(120, state.EarnedAtlasPoints);
        Assert.Contains(11, state.CompletedTiers);
        Assert.Equal(1, state.CitadelFragments);
        var economy = new TownEconomyState(gold: 100);
        Assert.True(state.TryPurchaseAtlas("p26.atlas.map.01", economy));
        Assert.Equal(0, economy.Gold);
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
        string mastery = P1PassiveTree.Nodes.First(node => node.Kind == PassiveNodeKind.Mastery).StableId;
        string socket = P1PassiveTree.Nodes.First(node => node.Kind == PassiveNodeKind.JewelSocket).StableId;
        Assert.True(allocation.TryAllocatePath(mastery, 149));
        Assert.True(allocation.TrySelectMastery(mastery, 0));
        Assert.True(allocation.TryAllocatePath(socket, 149));
        Assert.True(allocation.TrySocketJewel(socket, PassiveJewelKind.CrimsonMemory));

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
        var allocation = new PassiveTreeAllocation();
        string target = P1PassiveTree.Nodes.First(node => node.Kind == PassiveNodeKind.Notable).StableId;
        Assert.True(allocation.TryAllocatePath(target, 149));
        string[] capturedOrder = allocation.Allocated.Reverse().ToArray();

        PassiveTreeAllocation restored = PassiveTreeAllocation.Restore(capturedOrder, 0);

        Assert.Equal(capturedOrder.Order(), restored.Allocated.Order());
    }

    [Fact]
    public void ShortestPathIsAtomicAndMasteryAndJewelChoicesAreUnique()
    {
        var allocation = new PassiveTreeAllocation();
        string[] masteries = P1PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Mastery)
            .Select(node => node.StableId).Take(2).ToArray();
        string farMastery = masteries[0];
        Assert.False(allocation.TryAllocatePath(farMastery, 2));
        Assert.Empty(allocation.Allocated);
        Assert.True(allocation.TryAllocatePath(farMastery, 149));
        Assert.True(allocation.TryAllocatePath(masteries[1], 149));
        Assert.True(allocation.TrySelectMastery(farMastery, 0));
        Assert.True(allocation.TrySelectMastery(masteries[1], 0));

        string[] sockets = P1PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.JewelSocket)
            .Select(node => node.StableId).Take(2).ToArray();
        Assert.True(allocation.TryAllocatePath(sockets[0], 149));
        Assert.True(allocation.TryAllocatePath(sockets[1], 149));
        Assert.True(allocation.TrySocketJewel(sockets[0], PassiveJewelKind.CrimsonMemory));
        Assert.False(allocation.TrySocketJewel(sockets[1], PassiveJewelKind.CrimsonMemory));
    }

    [Fact]
    public void P10SnapshotRoundTrips()
    {
        var state = new P10EndgameState();
        state.RecordMapCompletion(new P1MapItem("p10-roundtrip", 15), MapRoute.Safe, 99);
        Assert.True(state.TryPurchaseAtlas("p26.atlas.supply.01", new TownEconomyState(gold: 100)));

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

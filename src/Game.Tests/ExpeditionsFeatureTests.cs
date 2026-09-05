using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Endgame;

namespace GameForWork.Tests;

public sealed class ExpeditionsFeatureTests
{
    [Fact]
    public void DispatchSelectsHighestMapAndKeepsOneVisibleAssignment()
    {
        GameSession session = CreateSession();
        session.World.MapInventory.Add(new MapItem("expeditions-low", 2));
        session.World.MapInventory.Add(new MapItem("expeditions-high", 9));

        session.AssignExpedition(
            ExpeditionTeamKind.Hero,
            ExpeditionTarget.HighestTierMaps,
            DispatchMode.Once);

        Assert.Equal("expeditions-high", Assert.Single(session.World.Hero.Queue.Maps).InstanceId);
        Assert.Equal(MapRoute.Abyss, session.World.Hero.Policy.PreferredRoute);
        Assert.False(session.World.Expedition.Get(ExpeditionTeamKind.Hero)!.Enabled);
        Assert.Single(session.World.MapInventory);
    }

    [Fact]
    public void TwelveMapCompletionsAutomaticallyCreateOneBossTicket()
    {
        var director = new ExpeditionDirector();
        for (int index = 0; index < 12; index++)
        {
            director.RecordResolved(new MapItem($"map-{index}", 5), succeeded: true);
        }

        Assert.Equal(0, director.AbyssWardenFragments);
        Assert.Equal(1, director.AbyssWardenTickets);
        Assert.Equal(0, director.MapsTowardNextFragment);
    }

    [Fact]
    public void AutomaticDispatchDoesNotResumeAStoppedTeam()
    {
        GameSession session = CreateSession();
        session.World.MapInventory.Add(new MapItem("expeditions-waiting", 3));
        session.World.Expedition.Assign(
            ExpeditionTeamKind.Hero,
            ExpeditionTarget.SafeMaps,
            DispatchMode.Repeat);
        session.World.Hero.Stop("storage_full");

        Assert.False(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
        Assert.Empty(session.World.Hero.Queue.Maps);
        Assert.Single(session.World.MapInventory);
        Assert.True(session.World.Hero.IsStopped);
    }

    [Fact]
    public void ManualRedispatchStartsANewConsecutiveFailureWindow()
    {
        GameSession session = CreateSession();
        var failedMap = new MapItem("expeditions-failed", 1);
        var failedRun = new MapRunResult(
            failedMap, MapRoute.Safe, false, 3, 0, [], "test_failure");
        for (int index = 0; index < 3; index++) session.World.Hero.RecordRun(failedRun);
        session.World.Hero.Stop("consecutive_failures");
        session.World.MapInventory.Add(new MapItem("expedition-retry", 1, RouteCandidates: [MapRoute.Safe], SelectedRoute: MapRoute.Safe));

        session.AssignExpedition(
            ExpeditionTeamKind.Hero,
            ExpeditionTarget.SafeMaps,
            DispatchMode.Once);

        Assert.False(session.World.Hero.IsStopped);
        Assert.Equal(0, session.World.Hero.ConsecutiveFailures);
        Assert.Equal("expedition-retry", Assert.Single(session.World.Hero.Queue.Maps).InstanceId);
    }

    [Fact]
    public void SpecifiedRunCountSchedulesExactlyRequestedMaps()
    {
        GameSession session = CreateSession();
        session.World.MapInventory.AddRange([
            new MapItem("expeditions-count-1", 1),
            new MapItem("expeditions-count-2", 1),
            new MapItem("expeditions-count-3", 1),
            new MapItem("expeditions-count-4", 1),
        ]);
        session.World.Expedition.Assign(ExpeditionTeamKind.Hero, ExpeditionTarget.HighestTierMaps,
            DispatchMode.Once, 3);

        for (int index = 0; index < 3; index++)
        {
            Assert.True(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
            Assert.True(session.World.Hero.Queue.TryDequeue(out _));
        }

        Assert.False(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
        TeamDispatchSnapshot dispatch = session.World.Expedition.Get(ExpeditionTeamKind.Hero)!;
        Assert.False(dispatch.Enabled);
        Assert.Equal(0, dispatch.RemainingRuns);
        Assert.Single(session.World.MapInventory);
    }

    [Fact]
    public void AbandonExpeditionConsumesActiveMapAndStopsTeam()
    {
        GameSession session = CreateSession();
        session.World.MapInventory.Add(new MapItem("expeditions-abandon", 1));
        session.AssignExpedition(ExpeditionTeamKind.Hero, ExpeditionTarget.HighestTierMaps,
            DispatchMode.Repeat);
        new WorldSimulator(new MapAttemptResolver()).Simulate(session.World, 1, 55);

        Assert.NotNull(session.World.Hero.ActiveMap);
        Assert.True(session.AbandonExpedition(ExpeditionTeamKind.Hero));

        Assert.Null(session.World.Hero.ActiveMap);
        Assert.True(session.World.Hero.IsStopped);
        Assert.Equal("abandoned", session.World.Hero.StopReason);
        Assert.DoesNotContain(session.World.MapInventory, map => map.InstanceId == "expeditions-abandon");
    }

    [Fact]
    public void RepeatedWardenChallengeConsumesTicketsUntilExhausted()
    {
        GameSession session = CreateSession();
        for (int index = 0; index < 24; index++)
            session.World.Expedition.RecordResolved(new MapItem($"warden-ticket-{index}", 10), true);
        session.World.Expedition.Assign(ExpeditionTeamKind.Hero, ExpeditionTarget.AbyssWarden, DispatchMode.Repeat);

        Assert.True(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
        Assert.True(session.World.Hero.Queue.TryDequeue(out _));
        Assert.True(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
        Assert.True(session.World.Hero.Queue.TryDequeue(out _));
        Assert.False(session.World.Expedition.PrepareNext(session.World, session.World.Hero));

        Assert.Equal(0, session.World.Expedition.AbyssWardenTickets);
        Assert.Equal("boss_ticket_missing", session.World.Hero.StopReason);
    }

    [Fact]
    public void RepeatedCitadelChallengeQueuesFormalMapsAcrossSessionRestore()
    {
        GameSession session = CreateSession();
        for (int index = 0; index < EndgameState.CitadelFragmentsPerTicket * 4; index++)
            session.Endgame.RecordMapCompletion(new MapItem($"citadel-ticket-{index}", 20), MapRoute.Abyss, (ulong)index);

        Assert.True(session.AssignBossChallenge(ExpeditionTarget.AshenCitadel, DispatchMode.Repeat));
        session = GameSession.Restore(session.Capture());
        for (int index = 0; index < 4; index++)
        {
            MapItem map = Assert.Single(session.World.Hero.Queue.Maps);
            Assert.True(EndgameState.IsCitadel(map));
            Assert.False(string.IsNullOrWhiteSpace(map.AreaId));
            Assert.Contains(MapRoute.Abyss, map.EffectiveRouteCandidates);
            Assert.Equal(MapRoute.Abyss, map.SelectedRoute);
            map.Validate();
            Assert.True(session.World.Hero.Queue.TryDequeue(out _));
            if (index < 3)
                Assert.True(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
        }
    }

    [Fact]
    public void LegacyQueuedCitadelMapIsFormalizedBeforeItStarts()
    {
        GameSession session = CreateSession();
        var legacy = new MapItem("endgame-ashen-citadel-063556", 20,
            RouteCandidates: [MapRoute.Abyss], SelectedRoute: MapRoute.Abyss);
        Assert.True(session.World.Hero.Queue.TryEnqueue(legacy));

        var simulator = new WorldSimulator(new MapAttemptResolver());
        simulator.Simulate(session.World, 1, 20_260_827);

        MapItem active = Assert.IsType<MapItem>(session.World.Hero.ActiveMap);
        Assert.False(string.IsNullOrWhiteSpace(active.AreaId));
        Assert.Contains(MapRoute.Abyss, active.EffectiveRouteCandidates);
        Assert.Equal(MapRoute.Abyss, active.SelectedRoute);
        active.Validate();
    }

    [Fact]
    public void ExpeditionProgressAndAssignmentsSurviveSessionSnapshot()
    {
        GameSession session = CreateSession();
        session.World.Expedition.RecordResolved(new MapItem("map-one", 1), succeeded: true);
        session.World.MapInventory.Add(new MapItem("map-two", 2));
        session.AssignExpedition(ExpeditionTeamKind.Mercenaries, ExpeditionTarget.SafeMaps, DispatchMode.Repeat);

        GameSession restored = GameSession.Restore(session.Capture());

        Assert.Equal(1, restored.World.Expedition.MapsTowardNextFragment);
        Assert.Equal(ExpeditionTarget.SafeMaps,
            restored.World.Expedition.Get(ExpeditionTeamKind.Mercenaries)!.Target);
        Assert.Single(restored.World.Mercenaries.Queue.Maps);
    }

    [Fact]
    public void StarterEquipmentGeneratesCharacterOwnedSocketGroups()
    {
        GameSession session = CreateSession();

        IReadOnlyList<SkillChainDefinition> chains = session.GetSkillChains();

        Assert.Equal(4, chains.Count);
        Assert.Equal(4, session.Management.SkillLinks.Count(link => !string.IsNullOrEmpty(link.ChainId)));
        Assert.All(chains, chain => Assert.Equal(
            session.HeroEquipment.Items[chain.SourceSlot].LinkedSocketCount,
            chain.TotalSockets));
    }

    [Fact]
    public void EquipmentChainCapacityRejectsOverflowWithoutDestroyingSupports()
    {
        GameSession session = CreateSession();
        SkillStoneInstance heavy = session.Management.SkillStones.Single(
            stone => stone.DefinitionId == "core.skill_stone.heavy_strike");
        SkillStoneInstance speed = session.Management.SkillStones.Single(
            stone => stone.DefinitionId == "core.skill_stone.attack_speed");
        SkillStoneInstance life = session.Management.SkillStones.Single(
            stone => stone.DefinitionId == "core.skill_stone.life_cost");

        Assert.True(session.TryLinkSkillSupport(heavy.InstanceId, speed.InstanceId));
        Assert.False(session.TryLinkSkillSupport(heavy.InstanceId, life.InstanceId));
        Assert.Contains(session.Management.SkillStones, stone => stone.InstanceId == life.InstanceId);
    }

    [Fact]
    public void RefundChecksRemainingGraphConnectivity()
    {
        var allocation = new PassiveTreeAllocation(memoryAshes: 5);
        string first = PassiveTree.Neighbors(PassiveTree.StartNode(allocation.StartKind)).First();
        string second = PassiveTree.Neighbors(first).First(id => id != PassiveTree.StartNode(allocation.StartKind));
        Assert.True(allocation.TryAllocate(first, 2));
        Assert.True(allocation.TryAllocate(second, 2));

        Assert.False(allocation.TryRefund(first));
        Assert.True(allocation.TryRefund(second));
        Assert.Equal(4, allocation.MemoryAshes);
    }

    [Fact]
    public void FirstFourMasteriesProvideBuildDefiningEffects()
    {
        PassiveNodeDefinition[] masteries = PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Mastery).ToArray();

        Assert.Equal(168, masteries.Length);
        Assert.All(masteries, mastery => Assert.Equal(7, PassiveTree.MasteryOptions(mastery).Count));
        Assert.Equal(6, masteries.Select(mastery => mastery.Branch).Distinct().Count());
    }

    private static GameSession CreateSession() => GameSession.CreateNew(new PlayerIdentity(
        "ExpeditionTester",
        CharacterGender.Androgynous,
        CharacterSkinTone.Umber,
        CharacterHairStyle.Cropped,
        BaseClass.Fighter), 5_005);
}

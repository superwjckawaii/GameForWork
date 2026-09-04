using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P5;
using GameForWork.Core.P10;

namespace GameForWork.Tests;

public sealed class P5FeatureTests
{
    [Fact]
    public void DispatchSelectsHighestMapAndKeepsOneVisibleAssignment()
    {
        P1GameSession session = CreateSession();
        session.World.MapInventory.Add(new P1MapItem("p5-low", 2));
        session.World.MapInventory.Add(new P1MapItem("p5-high", 9));

        session.AssignExpedition(
            ExpeditionTeamKind.Hero,
            P5ExpeditionTarget.HighestTierMaps,
            P5DispatchMode.Once);

        Assert.Equal("p5-high", Assert.Single(session.World.Hero.Queue.Maps).InstanceId);
        Assert.Equal(MapRoute.Abyss, session.World.Hero.Policy.PreferredRoute);
        Assert.False(session.World.Expedition.Get(ExpeditionTeamKind.Hero)!.Enabled);
        Assert.Single(session.World.MapInventory);
    }

    [Fact]
    public void TwelveMapCompletionsAutomaticallyCreateOneBossTicket()
    {
        var director = new P5ExpeditionDirector();
        for (int index = 0; index < 12; index++)
        {
            director.RecordResolved(new P1MapItem($"map-{index}", 5), succeeded: true);
        }

        Assert.Equal(0, director.AbyssWardenFragments);
        Assert.Equal(1, director.AbyssWardenTickets);
        Assert.Equal(0, director.MapsTowardNextFragment);
    }

    [Fact]
    public void AutomaticDispatchDoesNotResumeAStoppedTeam()
    {
        P1GameSession session = CreateSession();
        session.World.MapInventory.Add(new P1MapItem("p5-waiting", 3));
        session.World.Expedition.Assign(
            ExpeditionTeamKind.Hero,
            P5ExpeditionTarget.SafeMaps,
            P5DispatchMode.Repeat);
        session.World.Hero.Stop("storage_full");

        Assert.False(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
        Assert.Empty(session.World.Hero.Queue.Maps);
        Assert.Single(session.World.MapInventory);
        Assert.True(session.World.Hero.IsStopped);
    }

    [Fact]
    public void ManualRedispatchStartsANewConsecutiveFailureWindow()
    {
        P1GameSession session = CreateSession();
        var failedMap = new P1MapItem("p5-failed", 1);
        var failedRun = new P1MapRunResult(
            failedMap, MapRoute.Safe, false, 3, 0, [], "test_failure");
        for (int index = 0; index < 3; index++) session.World.Hero.RecordRun(failedRun);
        session.World.Hero.Stop("consecutive_failures");
        session.World.MapInventory.Add(new P1MapItem("p5-retry", 1));

        session.AssignExpedition(
            ExpeditionTeamKind.Hero,
            P5ExpeditionTarget.SafeMaps,
            P5DispatchMode.Once);

        Assert.False(session.World.Hero.IsStopped);
        Assert.Equal(0, session.World.Hero.ConsecutiveFailures);
        Assert.Equal("p5-retry", Assert.Single(session.World.Hero.Queue.Maps).InstanceId);
    }

    [Fact]
    public void SpecifiedRunCountSchedulesExactlyRequestedMaps()
    {
        P1GameSession session = CreateSession();
        session.World.MapInventory.AddRange([
            new P1MapItem("p5-count-1", 1),
            new P1MapItem("p5-count-2", 1),
            new P1MapItem("p5-count-3", 1),
            new P1MapItem("p5-count-4", 1),
        ]);
        session.World.Expedition.Assign(ExpeditionTeamKind.Hero, P5ExpeditionTarget.HighestTierMaps,
            P5DispatchMode.Once, 3);

        for (int index = 0; index < 3; index++)
        {
            Assert.True(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
            Assert.True(session.World.Hero.Queue.TryDequeue(out _));
        }

        Assert.False(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
        P5TeamDispatchSnapshot dispatch = session.World.Expedition.Get(ExpeditionTeamKind.Hero)!;
        Assert.False(dispatch.Enabled);
        Assert.Equal(0, dispatch.RemainingRuns);
        Assert.Single(session.World.MapInventory);
    }

    [Fact]
    public void AbandonExpeditionConsumesActiveMapAndStopsTeam()
    {
        P1GameSession session = CreateSession();
        session.World.MapInventory.Add(new P1MapItem("p5-abandon", 1));
        session.AssignExpedition(ExpeditionTeamKind.Hero, P5ExpeditionTarget.HighestTierMaps,
            P5DispatchMode.Repeat);
        new P1WorldSimulator(new P1MapAttemptResolver()).Simulate(session.World, 1, 55);

        Assert.NotNull(session.World.Hero.ActiveMap);
        Assert.True(session.AbandonExpedition(ExpeditionTeamKind.Hero));

        Assert.Null(session.World.Hero.ActiveMap);
        Assert.True(session.World.Hero.IsStopped);
        Assert.Equal("abandoned", session.World.Hero.StopReason);
        Assert.DoesNotContain(session.World.MapInventory, map => map.InstanceId == "p5-abandon");
    }

    [Fact]
    public void RepeatedWardenChallengeConsumesTicketsUntilExhausted()
    {
        P1GameSession session = CreateSession();
        for (int index = 0; index < 24; index++)
            session.World.Expedition.RecordResolved(new P1MapItem($"warden-ticket-{index}", 10), true);
        session.World.Expedition.Assign(ExpeditionTeamKind.Hero, P5ExpeditionTarget.AbyssWarden, P5DispatchMode.Repeat);

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
        P1GameSession session = CreateSession();
        for (int index = 0; index < P10EndgameState.CitadelFragmentsPerTicket * 4; index++)
            session.Endgame.RecordMapCompletion(new P1MapItem($"citadel-ticket-{index}", 20), MapRoute.Abyss, (ulong)index);

        Assert.True(session.AssignBossChallenge(P5ExpeditionTarget.AshenCitadel, P5DispatchMode.Repeat));
        session = P1GameSession.Restore(session.Capture());
        for (int index = 0; index < 4; index++)
        {
            P1MapItem map = Assert.Single(session.World.Hero.Queue.Maps);
            Assert.True(P10EndgameState.IsCitadel(map));
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
        P1GameSession session = CreateSession();
        var legacy = new P1MapItem("p10-ashen-citadel-063556", 20,
            RouteCandidates: [MapRoute.Abyss], SelectedRoute: MapRoute.Abyss);
        Assert.True(session.World.Hero.Queue.TryEnqueue(legacy));

        var simulator = new P1WorldSimulator(new P1MapAttemptResolver());
        simulator.Simulate(session.World, 1, 20_260_827);

        P1MapItem active = Assert.IsType<P1MapItem>(session.World.Hero.ActiveMap);
        Assert.False(string.IsNullOrWhiteSpace(active.AreaId));
        Assert.Contains(MapRoute.Abyss, active.EffectiveRouteCandidates);
        Assert.Equal(MapRoute.Abyss, active.SelectedRoute);
        active.Validate();
    }

    [Fact]
    public void ExpeditionProgressAndAssignmentsSurviveSessionSnapshot()
    {
        P1GameSession session = CreateSession();
        session.World.Expedition.RecordResolved(new P1MapItem("map-one", 1), succeeded: true);
        session.World.MapInventory.Add(new P1MapItem("map-two", 2));
        session.AssignExpedition(ExpeditionTeamKind.Mercenaries, P5ExpeditionTarget.SafeMaps, P5DispatchMode.Repeat);

        P1GameSession restored = P1GameSession.Restore(session.Capture());

        Assert.Equal(1, restored.World.Expedition.MapsTowardNextFragment);
        Assert.Equal(P5ExpeditionTarget.SafeMaps,
            restored.World.Expedition.Get(ExpeditionTeamKind.Mercenaries)!.Target);
        Assert.Single(restored.World.Mercenaries.Queue.Maps);
    }

    [Fact]
    public void StarterEquipmentGeneratesCharacterOwnedSocketGroups()
    {
        P1GameSession session = CreateSession();

        IReadOnlyList<P5SkillChainDefinition> chains = session.GetSkillChains();

        Assert.Equal(4, chains.Count);
        Assert.Equal(4, session.Management.SkillLinks.Count(link => !string.IsNullOrEmpty(link.ChainId)));
        Assert.All(chains, chain => Assert.Equal(
            session.HeroEquipment.Items[chain.SourceSlot].LinkedSocketCount,
            chain.TotalSockets));
    }

    [Fact]
    public void EquipmentChainCapacityRejectsOverflowWithoutDestroyingSupports()
    {
        P1GameSession session = CreateSession();
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
        string first = P1PassiveTree.Neighbors(P1PassiveTree.StartNode(allocation.StartKind)).First();
        string second = P1PassiveTree.Neighbors(first).First(id => id != P1PassiveTree.StartNode(allocation.StartKind));
        Assert.True(allocation.TryAllocate(first, 2));
        Assert.True(allocation.TryAllocate(second, 2));

        Assert.False(allocation.TryRefund(first));
        Assert.True(allocation.TryRefund(second));
        Assert.Equal(4, allocation.MemoryAshes);
    }

    [Fact]
    public void FirstFourMasteriesProvideBuildDefiningEffects()
    {
        PassiveNodeDefinition[] masteries = P1PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Mastery).ToArray();

        Assert.Equal(168, masteries.Length);
        Assert.All(masteries, mastery => Assert.Equal(7, P1PassiveTree.MasteryOptions(mastery).Count));
        Assert.Equal(6, masteries.Select(mastery => mastery.Branch).Distinct().Count());
    }

    private static P1GameSession CreateSession() => P1GameSession.CreateNew(new PlayerIdentity(
        "P5Tester",
        CharacterGender.Androgynous,
        CharacterSkinTone.Umber,
        CharacterHairStyle.Cropped,
        P23BaseClass.Fighter), 5_005);
}

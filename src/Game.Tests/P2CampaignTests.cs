using System.Text.Json;
using GameForWork.Core.P1;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.Offline;

namespace GameForWork.Tests;

public sealed class P2CampaignTests
{
    [Fact]
    public void CatalogContainsFiveActsAndThirtySealedNodes()
    {
        Assert.Equal(30, P2CampaignCatalog.Nodes.Count);
        Assert.Equal(5, P2CampaignCatalog.Nodes.Select(node => node.Act).Distinct().Count());
        Assert.All(P2CampaignCatalog.Nodes.GroupBy(node => node.Act), act =>
        {
            Assert.Equal(6, act.Count());
            Assert.Equal(3, act.Count(node => node.Kind == CampaignNodeKind.NormalCombat));
            Assert.Single(act, node => node.Kind == CampaignNodeKind.StoryEvent);
            Assert.Single(act, node => node.Kind == CampaignNodeKind.EliteCombat);
            Assert.Single(act, node => node.Kind == CampaignNodeKind.ActBoss);
        });
        Assert.Equal(3_600_000, P2CampaignCatalog.Nodes.Sum(node => node.DurationMilliseconds));
    }

    [Fact]
    public void StarterStopsAtCampaignBuildCheckBeforeExpeditionStarts()
    {
        P1GameSession session = Session();

        P1OfflineResult result = session.AdvanceOffline(OfflineTime.MaximumMilliseconds);

        Assert.False(session.Campaign.Completed);
        Assert.True(session.Campaign.Defeated);
        Assert.False(session.IsExpeditionUnlocked);
        Assert.NotEmpty(session.Campaign.CompletedNodeIds);
        Assert.Equal(0, session.World.Hero.Queue.Count);
        Assert.Equal(0, result.TotalMapsCompleted);
    }

    [Fact]
    public void CampaignTimeSegmentationIsDeterministic()
    {
        P1GameSession whole = Session();
        P1GameSession segmented = Session();

        whole.AdvanceOffline(600_000);
        for (int index = 0; index < 10; index++)
        {
            segmented.AdvanceOffline(60_000);
        }

        Assert.Equal(
            JsonSerializer.Serialize(whole.Capture()),
            JsonSerializer.Serialize(segmented.Capture()));
    }

    [Fact]
    public void FormatFourTestSaveIsRejected()
    {
        P1GameSession session = Session();
        P1GameSessionSnapshot legacy = session.Capture() with { FormatVersion = 4, Campaign = null };

        Assert.Throws<InvalidDataException>(() => P1GameSession.Restore(legacy));
    }

    [Fact]
    public void MapQueueMovesAreAtomicAndUnavailableBeforeCompletion()
    {
        P1GameSession session = Session();
        session.World.MapInventory.Add(new P1MapItem("locked-map", 1));
        var commands = new P2MapCommandService(session);

        Assert.Equal("expedition_locked", commands.AddToQueue(0, ExpeditionTeamKind.Hero).Code);
        P1GameSession unlocked = MigrateAsCompleted(session);
        var unlockedCommands = new P2MapCommandService(unlocked);
        int inventoryBefore = unlocked.World.MapInventory.Count;
        Assert.True(unlockedCommands.AddToQueue(0, ExpeditionTeamKind.Hero).Succeeded);
        Assert.Equal(inventoryBefore - 1, unlocked.World.MapInventory.Count);
        Assert.Equal(1, unlocked.World.Hero.Queue.Count);
    }

    [Fact]
    public void PolicyChangesWaitForCurrentMapAndStopConditionsUseOrSemantics()
    {
        P1GameSession session = CompletedSession();
        P1TeamExpeditionState team = session.World.Hero;
        team.Queue.TryEnqueue(new P1MapItem("one", 1));
        session.Advance(1);
        ExpeditionPolicy next = ExpeditionPolicy.Recommended with
        {
            MaximumContinuousMaps = 1,
            MinimumStorageFreeSlots = 999,
        };

        team.ApplyPolicy(next);

        Assert.NotNull(team.ActiveMap);
        Assert.NotNull(team.PendingPolicy);
        Assert.NotEqual(next, team.Policy);
    }

    private static P1GameSession CompletedSession()
    {
        return MigrateAsCompleted(Session());
    }

    private static P1GameSession MigrateAsCompleted(P1GameSession session)
    {
        P1GameSessionSnapshot snapshot = session.Capture();
        string[] completed = P2CampaignCatalog.Nodes.Select(node => node.StableId).ToArray();
        P2CampaignSnapshot campaign = new(P2CampaignCatalog.Nodes.Count, 0, false, true,
            completed, completed, ["测试：五幕主线已完成。"], null);
        return P1GameSession.Restore(snapshot with { Campaign = campaign });
    }

    private static P1GameSession Session() => P1GameSession.CreateNew(new PlayerIdentity(
        "行路者",
        CharacterGender.Androgynous,
        CharacterSkinTone.Umber,
        CharacterHairStyle.Braided,
        P1Ascendancy.IronOath), 4_242);
}

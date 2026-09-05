using System.Text.Json;
using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Offline;

namespace GameForWork.Tests;

public sealed class CampaignTests
{
    [Fact]
    public void CatalogContainsFiveActsAndThirtySealedNodes()
    {
        Assert.Equal(30, CampaignCatalog.Nodes.Count);
        Assert.Equal(5, CampaignCatalog.Nodes.Select(node => node.Act).Distinct().Count());
        Assert.All(CampaignCatalog.Nodes.GroupBy(node => node.Act), act =>
        {
            Assert.Equal(6, act.Count());
            Assert.Equal(3, act.Count(node => node.Kind == CampaignNodeKind.NormalCombat));
            Assert.Single(act, node => node.Kind == CampaignNodeKind.StoryEvent);
            Assert.Single(act, node => node.Kind == CampaignNodeKind.EliteCombat);
            Assert.Single(act, node => node.Kind == CampaignNodeKind.ActBoss);
        });
        Assert.Equal(3_600_000, CampaignCatalog.Nodes.Sum(node => node.DurationMilliseconds));
    }

    [Fact]
    public void StarterStopsAtCampaignBuildCheckBeforeExpeditionStarts()
    {
        GameSession session = Session();

        GameForWork.Core.Campaign.World.OfflineResult result = session.AdvanceOffline(OfflineTime.MaximumMilliseconds);

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
        GameSession whole = Session();
        GameSession segmented = Session();

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
        GameSession session = Session();
        GameSessionSnapshot legacy = session.Capture() with { FormatVersion = 4, Campaign = null };

        Assert.Throws<InvalidDataException>(() => GameSession.Restore(legacy));
    }

    [Fact]
    public void MapQueueMovesAreAtomicAndUnavailableBeforeCompletion()
    {
        GameSession session = Session();
        session.World.MapInventory.Add(new MapItem("locked-map", 1));
        var commands = new MapCommandService(session);

        Assert.Equal("expedition_locked", commands.AddToQueue(0, ExpeditionTeamKind.Hero).Code);
        GameSession unlocked = MigrateAsCompleted(session);
        var unlockedCommands = new MapCommandService(unlocked);
        int inventoryBefore = unlocked.World.MapInventory.Count;
        Assert.True(unlockedCommands.AddToQueue(0, ExpeditionTeamKind.Hero).Succeeded);
        Assert.Equal(inventoryBefore - 1, unlocked.World.MapInventory.Count);
        Assert.Equal(1, unlocked.World.Hero.Queue.Count);
    }

    [Fact]
    public void PolicyChangesWaitForCurrentMapAndStopConditionsUseOrSemantics()
    {
        GameSession session = CompletedSession();
        TeamExpeditionState team = session.World.Hero;
        team.Queue.TryEnqueue(new MapItem("one", 1));
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

    private static GameSession CompletedSession()
    {
        return MigrateAsCompleted(Session());
    }

    private static GameSession MigrateAsCompleted(GameSession session)
    {
        GameSessionSnapshot snapshot = session.Capture();
        string[] completed = CampaignCatalog.Nodes.Select(node => node.StableId).ToArray();
        CampaignSnapshot campaign = new(CampaignCatalog.Nodes.Count, 0, false, true,
            completed, completed, ["测试：五幕主线已完成。"], null);
        return GameSession.Restore(snapshot with { Campaign = campaign });
    }

    private static GameSession Session() => GameSession.CreateNew(new PlayerIdentity(
        "行路者",
        CharacterGender.Androgynous,
        CharacterSkinTone.Umber,
        CharacterHairStyle.Braided,
        BaseClass.Fighter), 4_242);
}

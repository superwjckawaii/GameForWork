using System.Diagnostics;
using GameForWork.Core.Offline;
using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Endgame;
using GameForWork.Core.Economy;
using GameForWork.Core.Release;

namespace GameForWork.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceCollection
{
    public const string Name = "Release release performance";
}

[Collection(PerformanceCollection.Name)]
public sealed class ReleaseFeatureTests
{
    [Fact]
    public void ReleaseTargetsSealVersionSaveAndSixBenchmarkBuilds()
    {
        Assert.Equal("0.3.0", ReleaseTargets.Version);
        Assert.Equal(GameSession.CurrentFormatVersion, ReleaseTargets.SaveFormatVersion);
        Assert.Empty(ReleaseTargets.ValidateBenchmarkCatalog());
        Assert.Equal([80, 90, 100, 110, 120, 130, 140, 150], ReleaseTargets.FontScaleMatrix);
    }

    [Fact]
    public void EconomyAuditMeetsEarlyAndPinnacleMapSustainTargets()
    {
        IReadOnlyList<AuditResult> audit = EconomyAudit.Run(10_000, 0x22ec0a11UL);
        Assert.Empty(ReleaseTargets.ValidateEconomy(audit));
    }

    [Fact]
    public void SixAscendancyBenchmarksRunInAuthoritativeSpatialCombat()
    {
        IReadOnlyList<CombatBenchmarkResult> results = ReleaseTargets.RunCombatBenchmarks(4, 22);

        Assert.Equal(6, results.Count);
        Assert.All(results, result => Assert.InRange(result.SuccessRate, 0.50, 1.00));
        Assert.All(results, result => Assert.InRange(result.AverageDurationSeconds, 0.05, 180.0));
    }

    [Fact]
    public void OfflineFortyEightHourSettlementStaysWithinReleaseBudget()
    {
        GameSession session = Session(tutorialEnabled: false);
        Stopwatch timer = Stopwatch.StartNew();

        GameForWork.Core.Campaign.World.OfflineResult result = session.AdvanceOffline(OfflineTime.MaximumMilliseconds);

        timer.Stop();
        Assert.Equal(OfflineTime.MaximumMilliseconds, result.EffectiveMilliseconds);
        Assert.True(timer.Elapsed.TotalSeconds < ReleaseTargets.MaximumOfflineSeconds,
            $"48h settlement took {timer.Elapsed.TotalSeconds:F3}s; budget is {ReleaseTargets.MaximumOfflineSeconds:F1}s.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcceleratedJourneyCanReachEveryV02Milestone(bool tutorialEnabled)
    {
        GameSession session = Session(tutorialEnabled);
        session = CompleteCampaignCheckpoint(session);
        Assert.True(session.Campaign.Completed);
        Assert.True(session.IsExpeditionUnlocked);

        for (int tier = 1; tier <= 16; tier++)
            session.Endgame.RecordMapCompletion(new MapItem($"release-t{tier}", tier), MapRoute.Safe, (ulong)tier);
        Assert.Contains(16, session.Endgame.CompletedTiers);

        session.World.Hero.Progression.AddExperience(CharacterProgression.TotalExperienceToCap);
        Assert.Equal(100, session.World.Hero.Progression.Level);
        Assert.True(session.RecordFinalBreakthroughTrialVictory());
        Assert.Equal(120, session.World.Hero.Progression.LevelCap);

        for (int tier = 17; tier <= 20; tier++)
            session.Endgame.RecordMapCompletion(new MapItem($"release-t{tier}", tier), MapRoute.Abyss, (ulong)tier);
        for (int index = 0; index < EndgameState.CitadelFragmentsPerTicket; index++)
            session.Endgame.RecordMapCompletion(new MapItem($"release-fragment-{index}", 20), MapRoute.Abyss, (ulong)index);
        Assert.True(session.Endgame.CitadelTickets > 0);
        Assert.True(session.Endgame.RecordCitadelVictory());
        Assert.True(session.Endgame.CitadelDefeated);
        session.Advance(1);

        if (tutorialEnabled)
            Assert.True(session.Journey.TutorialEnabled);
        else
            Assert.True(session.Journey.TutorialAllowsPage(JourneyStep.DefeatCitadel,
                requireGateCompletion: true));
    }

    private static GameSession Session(bool tutorialEnabled) => GameSession.CreateNew(new PlayerIdentity(
        "Release 封版巡行者", CharacterGender.Androgynous, CharacterSkinTone.Fair, CharacterHairStyle.Cropped,
        BaseClass.Fighter), 0x220022UL, tutorialEnabled);

    private static GameSession CompleteCampaignCheckpoint(GameSession session)
    {
        string[] completed = CampaignCatalog.Nodes.Select(node => node.StableId).ToArray();
        CampaignSnapshot campaign = new(CampaignCatalog.Nodes.Count, 0, false, true,
            completed, completed, ["Release 加速回归：五幕主线战斗已由分层战斗测试验证。"], null);
        return GameSession.Restore(session.Capture() with { Campaign = campaign });
    }
}

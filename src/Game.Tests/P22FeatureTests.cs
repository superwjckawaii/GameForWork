using System.Diagnostics;
using GameForWork.Core.Offline;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P10;
using GameForWork.Core.P20;
using GameForWork.Core.P22;

namespace GameForWork.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class P22PerformanceCollection
{
    public const string Name = "P22 release performance";
}

[Collection(P22PerformanceCollection.Name)]
public sealed class P22FeatureTests
{
    [Fact]
    public void ReleaseTargetsSealVersionSaveAndSixBenchmarkBuilds()
    {
        Assert.Equal("0.3.0", P22ReleaseTargets.Version);
        Assert.Equal(P1GameSession.CurrentFormatVersion, P22ReleaseTargets.SaveFormatVersion);
        Assert.Empty(P22ReleaseTargets.ValidateBenchmarkCatalog());
        Assert.Equal([80, 90, 100, 110, 120, 130, 140, 150], P22ReleaseTargets.FontScaleMatrix);
    }

    [Fact]
    public void EconomyAuditMeetsEarlyAndPinnacleMapSustainTargets()
    {
        IReadOnlyList<P20AuditResult> audit = P20EconomyAudit.Run(10_000, 0x22ec0a11UL);
        Assert.Empty(P22ReleaseTargets.ValidateEconomy(audit));
    }

    [Fact]
    public void SixAscendancyBenchmarksRunInAuthoritativeSpatialCombat()
    {
        IReadOnlyList<P22CombatBenchmarkResult> results = P22ReleaseTargets.RunCombatBenchmarks(4, 22);

        Assert.Equal(6, results.Count);
        Assert.All(results, result => Assert.InRange(result.SuccessRate, 0.50, 1.00));
        Assert.All(results, result => Assert.InRange(result.AverageDurationSeconds, 0.05, 180.0));
    }

    [Fact]
    public void OfflineFortyEightHourSettlementStaysWithinReleaseBudget()
    {
        P1GameSession session = Session(tutorialEnabled: false);
        Stopwatch timer = Stopwatch.StartNew();

        P1OfflineResult result = session.AdvanceOffline(OfflineTime.MaximumMilliseconds);

        timer.Stop();
        Assert.Equal(OfflineTime.MaximumMilliseconds, result.EffectiveMilliseconds);
        Assert.True(timer.Elapsed.TotalSeconds < P22ReleaseTargets.MaximumOfflineSeconds,
            $"48h settlement took {timer.Elapsed.TotalSeconds:F3}s; budget is {P22ReleaseTargets.MaximumOfflineSeconds:F1}s.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcceleratedJourneyCanReachEveryV02Milestone(bool tutorialEnabled)
    {
        P1GameSession session = Session(tutorialEnabled);
        session = CompleteCampaignCheckpoint(session);
        Assert.True(session.Campaign.Completed);
        Assert.True(session.IsExpeditionUnlocked);

        for (int tier = 1; tier <= 16; tier++)
            session.Endgame.RecordMapCompletion(new P1MapItem($"p22-t{tier}", tier), MapRoute.Safe, (ulong)tier);
        Assert.Contains(16, session.Endgame.CompletedTiers);

        session.World.Hero.Progression.AddExperience(CharacterProgression.TotalExperienceToCap);
        Assert.Equal(100, session.World.Hero.Progression.Level);
        Assert.True(session.RecordFinalBreakthroughTrialVictory());
        Assert.Equal(120, session.World.Hero.Progression.LevelCap);

        for (int tier = 17; tier <= 20; tier++)
            session.Endgame.RecordMapCompletion(new P1MapItem($"p22-t{tier}", tier), MapRoute.Abyss, (ulong)tier);
        for (int index = 0; index < P10EndgameState.CitadelFragmentsPerTicket; index++)
            session.Endgame.RecordMapCompletion(new P1MapItem($"p22-fragment-{index}", 20), MapRoute.Abyss, (ulong)index);
        Assert.True(session.Endgame.CitadelTickets > 0);
        Assert.True(session.Endgame.RecordCitadelVictory());
        Assert.True(session.Endgame.CitadelDefeated);
        session.Advance(1);

        if (tutorialEnabled)
            Assert.True(session.Journey.TutorialEnabled);
        else
            Assert.True(session.Journey.TutorialAllowsPage(P8JourneyStep.DefeatCitadel,
                requireGateCompletion: true));
    }

    private static P1GameSession Session(bool tutorialEnabled) => P1GameSession.CreateNew(new PlayerIdentity(
        "P22 封版巡行者", CharacterGender.Androgynous, CharacterSkinTone.Fair, CharacterHairStyle.Cropped,
        P23BaseClass.Fighter), 0x220022UL, tutorialEnabled);

    private static P1GameSession CompleteCampaignCheckpoint(P1GameSession session)
    {
        string[] completed = P2CampaignCatalog.Nodes.Select(node => node.StableId).ToArray();
        P2CampaignSnapshot campaign = new(P2CampaignCatalog.Nodes.Count, 0, false, true,
            completed, completed, ["P22 加速回归：五幕主线战斗已由分层战斗测试验证。"], null);
        return P1GameSession.Restore(session.Capture() with { Campaign = campaign });
    }
}

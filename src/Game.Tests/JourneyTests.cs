using System.Text.Json;
using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;

public sealed class JourneyTests
{
    [Fact]
    public void NewGuidedSessionAdvancesSingleJourneyAndPersistsPresentation()
    {
        GameSession session = CreateSession(tutorial: true);
        int originalGold = session.World.Economy.Gold;
        Assert.Equal(JourneyStep.ObserveBattle, session.Journey.CurrentStep!.Step);
        Assert.True(session.Journey.TryPresentCurrentStep());
        Assert.False(session.Journey.TryPresentCurrentStep());

        session.Advance(1_000);
        Assert.Equal(originalGold + 5, session.World.Economy.Gold);
        Assert.Equal(JourneyStep.EquipItem, session.Journey.CurrentStep!.Step);
        session.RecordJourneyEvent(JourneyEvent.EquippedItem);
        Assert.Equal(JourneyStep.CompleteActOne, session.Journey.CurrentStep!.Step);

        string json = JsonSerializer.Serialize(session.Capture());
        GameSession restored = GameSession.Restore(JsonSerializer.Deserialize<GameSessionSnapshot>(json)!);
        Assert.True(restored.Journey.TutorialEnabled);
        Assert.Equal(JourneyStep.CompleteActOne, restored.Journey.CurrentStep!.Step);
        Assert.Equal(session.World.Economy.Gold, restored.World.Economy.Gold);
        restored.Journey.Synchronize(restored);
        Assert.Equal(session.World.Economy.Gold, restored.World.Economy.Gold);
    }

    [Fact]
    public void TutorialCanOnlyBeSkippedWhenSessionIsCreated()
    {
        GameSession session = CreateSession(tutorial: false);
        session.Advance(1_000);

        Assert.False(session.Journey.TutorialEnabled);
        Assert.False(session.Journey.TryPresentCurrentStep());
        Assert.Equal(JourneyStep.CompleteActOne, session.Journey.CurrentStep!.Step);
        Assert.False(GameSession.Restore(session.Capture()).Journey.TutorialEnabled);
    }

    [Fact]
    public void SkippingTutorialUnlocksEveryTutorialGatedPageImmediately()
    {
        GameSession skipped = CreateSession(tutorial: false);
        GameSession guided = CreateSession(tutorial: true);

        Assert.True(skipped.Journey.TutorialAllowsPage(JourneyStep.EquipItem));
        Assert.True(skipped.Journey.TutorialAllowsPage(JourneyStep.CraftItem, requireGateCompletion: true));
        Assert.True(skipped.Journey.TutorialAllowsPage(JourneyStep.AllocatePassive));
        Assert.True(skipped.Journey.TutorialAllowsPage(JourneyStep.ConfigureSkillTarget, requireGateCompletion: true));
        Assert.False(guided.Journey.TutorialAllowsPage(JourneyStep.EquipItem));
        Assert.False(guided.Journey.TutorialAllowsPage(JourneyStep.CraftItem, requireGateCompletion: true));
    }

    [Fact]
    public void VersionNineJourneySaveIsRejectedAtSessionBoundary()
    {
        GameSession current = CreateSession(tutorial: true);
        current.Advance(1_000);
        GameSessionSnapshot legacy = current.Capture() with { FormatVersion = 9, Journey = null };

        Assert.Throws<InvalidDataException>(() => GameSession.Restore(legacy));
    }

    [Fact]
    public void OnlineAndOfflineJourneyTimeAreTrackedSeparately()
    {
        GameSession session = CreateSession(tutorial: false);

        session.Advance(2_000);
        session.AdvanceOffline(3_000);
        GameSession restored = GameSession.Restore(session.Capture());

        Assert.Equal(2_000, restored.Journey.RealPlayMilliseconds);
        Assert.Equal(3_000, restored.Journey.OfflineMilliseconds);
    }

    [Fact]
    public void RealCitadelVictoryCompletesDemoOnlyOnce()
    {
        GameSession session = CreateSession(tutorial: false);
        session.Endgame.RecordCitadelVictory();

        session.Journey.Synchronize(session);

        Assert.True(session.Journey.DemoCompleted);
        Assert.True(session.Journey.TryMarkCompletionShown());
        Assert.False(session.Journey.TryMarkCompletionShown());
        Assert.True(GameSession.Restore(session.Capture()).Journey.CompletionShown);
    }

    [Fact]
    public void TierSixteenGuidanceExplainsLevelHundredAndAscendancyProgression()
    {
        GameSession session = CreateSession(tutorial: false);
        JourneyStepDefinition tier16 = session.Journey.AllSteps.Single(step => step.Step == JourneyStep.CompleteTier16);
        JourneyStepDefinition level100 = session.Journey.AllSteps.Single(step => step.Step == JourneyStep.ReachLevel100);
        JourneyStepDefinition breakthrough = session.Journey.AllSteps.Single(step => step.Step == JourneyStep.CompleteBreakthrough);

        Assert.Contains("100 级", tier16.HelpText, StringComparison.Ordinal);
        Assert.Contains("升华 4/4", tier16.HelpText, StringComparison.Ordinal);
        Assert.Contains("远征 → 异界与突破", level100.HelpText, StringComparison.Ordinal);
        Assert.Contains("2 个升华点", breakthrough.HelpText, StringComparison.Ordinal);
    }

    private static GameSession CreateSession(bool tutorial) => GameSession.CreateNew(new PlayerIdentity(
        "引路者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
        CharacterHairStyle.Braided, BaseClass.Fighter), 0x8080, tutorial);
}

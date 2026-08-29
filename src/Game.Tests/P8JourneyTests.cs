using System.Text.Json;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P6;

namespace GameForWork.Tests;

public sealed class P8JourneyTests
{
    [Fact]
    public void NewGuidedSessionAdvancesSingleJourneyAndPersistsPresentation()
    {
        P1GameSession session = CreateSession(tutorial: true);
        int originalGold = session.World.Economy.Gold;
        Assert.Equal(P8JourneyStep.ObserveBattle, session.Journey.CurrentStep!.Step);
        Assert.True(session.Journey.TryPresentCurrentStep());
        Assert.False(session.Journey.TryPresentCurrentStep());

        session.Advance(1_000);
        Assert.Equal(originalGold + 5, session.World.Economy.Gold);
        Assert.Equal(P8JourneyStep.EquipItem, session.Journey.CurrentStep!.Step);
        session.RecordJourneyEvent(P8JourneyEvent.EquippedItem);
        Assert.Equal(P8JourneyStep.CompleteActOne, session.Journey.CurrentStep!.Step);

        string json = JsonSerializer.Serialize(session.Capture());
        P1GameSession restored = P1GameSession.Restore(JsonSerializer.Deserialize<P1GameSessionSnapshot>(json)!);
        Assert.True(restored.Journey.TutorialEnabled);
        Assert.Equal(P8JourneyStep.CompleteActOne, restored.Journey.CurrentStep!.Step);
        Assert.Equal(session.World.Economy.Gold, restored.World.Economy.Gold);
        restored.Journey.Synchronize(restored);
        Assert.Equal(session.World.Economy.Gold, restored.World.Economy.Gold);
    }

    [Fact]
    public void TutorialCanOnlyBeSkippedWhenSessionIsCreated()
    {
        P1GameSession session = CreateSession(tutorial: false);
        session.Advance(1_000);

        Assert.False(session.Journey.TutorialEnabled);
        Assert.False(session.Journey.TryPresentCurrentStep());
        Assert.Equal(P8JourneyStep.CompleteActOne, session.Journey.CurrentStep!.Step);
        Assert.False(P1GameSession.Restore(session.Capture()).Journey.TutorialEnabled);
    }

    [Fact]
    public void SkippingTutorialUnlocksEveryTutorialGatedPageImmediately()
    {
        P1GameSession skipped = CreateSession(tutorial: false);
        P1GameSession guided = CreateSession(tutorial: true);

        Assert.True(skipped.Journey.TutorialAllowsPage(P8JourneyStep.EquipItem));
        Assert.True(skipped.Journey.TutorialAllowsPage(P8JourneyStep.CraftItem, requireGateCompletion: true));
        Assert.True(skipped.Journey.TutorialAllowsPage(P8JourneyStep.AllocatePassive));
        Assert.True(skipped.Journey.TutorialAllowsPage(P8JourneyStep.ConfigureSkillTarget, requireGateCompletion: true));
        Assert.False(guided.Journey.TutorialAllowsPage(P8JourneyStep.EquipItem));
        Assert.False(guided.Journey.TutorialAllowsPage(P8JourneyStep.CraftItem, requireGateCompletion: true));
    }

    [Fact]
    public void VersionNineJourneySaveIsRejectedAtSessionBoundary()
    {
        P1GameSession current = CreateSession(tutorial: true);
        current.Advance(1_000);
        P1GameSessionSnapshot legacy = current.Capture() with { FormatVersion = 9, Journey = null };

        Assert.Throws<InvalidDataException>(() => P1GameSession.Restore(legacy));
    }

    [Fact]
    public void OnlineAndOfflineJourneyTimeAreTrackedSeparately()
    {
        P1GameSession session = CreateSession(tutorial: false);

        session.Advance(2_000);
        session.AdvanceOffline(3_000);
        P1GameSession restored = P1GameSession.Restore(session.Capture());

        Assert.Equal(2_000, restored.Journey.RealPlayMilliseconds);
        Assert.Equal(3_000, restored.Journey.OfflineMilliseconds);
    }

    [Fact]
    public void RealCitadelVictoryCompletesDemoOnlyOnce()
    {
        P1GameSession session = CreateSession(tutorial: false);
        session.Endgame.RecordCitadelVictory();

        session.Journey.Synchronize(session);

        Assert.True(session.Journey.DemoCompleted);
        Assert.True(session.Journey.TryMarkCompletionShown());
        Assert.False(session.Journey.TryMarkCompletionShown());
        Assert.True(P1GameSession.Restore(session.Capture()).Journey.CompletionShown);
    }

    [Fact]
    public void TierSixteenGuidanceExplainsLevelHundredAndAscendancyProgression()
    {
        P1GameSession session = CreateSession(tutorial: false);
        P8JourneyStepDefinition tier16 = session.Journey.AllSteps.Single(step => step.Step == P8JourneyStep.CompleteTier16);
        P8JourneyStepDefinition level100 = session.Journey.AllSteps.Single(step => step.Step == P8JourneyStep.ReachLevel100);
        P8JourneyStepDefinition breakthrough = session.Journey.AllSteps.Single(step => step.Step == P8JourneyStep.CompleteBreakthrough);

        Assert.Contains("100 级", tier16.HelpText, StringComparison.Ordinal);
        Assert.Contains("升华 4/4", tier16.HelpText, StringComparison.Ordinal);
        Assert.Contains("远征 → 异界与突破", level100.HelpText, StringComparison.Ordinal);
        Assert.Contains("2 个升华点", breakthrough.HelpText, StringComparison.Ordinal);
    }

    private static P1GameSession CreateSession(bool tutorial) => P1GameSession.CreateNew(new PlayerIdentity(
        "引路者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
        CharacterHairStyle.Braided, P1Ascendancy.IronOath), 0x8080, tutorial);
}

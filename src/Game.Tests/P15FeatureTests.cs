using GameForWork.Core.Offline;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Progression;

namespace GameForWork.Tests;

public sealed class P15FeatureTests
{
    [Fact]
    public void OfflineFortyEightHoursUsesAuthoritativeSessionAndIsDeterministic()
    {
        P1GameSession first = Session(15);
        P1GameSession second = Session(15);

        var left = first.AdvanceOffline(OfflineTime.MaximumMilliseconds);
        var right = second.AdvanceOffline(OfflineTime.MaximumMilliseconds);

        Assert.Equal(OfflineTime.MaximumMilliseconds, left.EffectiveMilliseconds);
        Assert.Equal(left.FinalHash, right.FinalHash);
        Assert.Equal(left.TotalMapsCompleted, right.TotalMapsCompleted);
        Assert.Equal(first.Campaign.CompletedNodeIds, second.Campaign.CompletedNodeIds);
    }

    [Fact]
    public void SkippedTutorialKeepsPagesOpenAndReplayDoesNotResetProgress()
    {
        P1GameSession session = Session(16, tutorial: false);
        Assert.True(session.Journey.TutorialAllowsPage(P8JourneyStep.ConfigureSkillTarget, true));
        int before = session.Journey.CurrentStepIndex;

        session.Journey.ReplayTutorial();

        Assert.Equal(before, session.Journey.CurrentStepIndex);
        Assert.Equal(1, session.Journey.TutorialReplayCount);
        Assert.True(session.Journey.TutorialAllowsPage(P8JourneyStep.DefeatCitadel, true));
    }

    [Fact]
    public void NewProgressionStopsAtOneHundredBeforeFinalGate()
    {
        var progression = new CharacterProgression();
        progression.AddExperience(CharacterProgression.TotalExperienceToCap);
        Assert.Equal(100, progression.Level);
        Assert.Equal(100, progression.LevelCap);
        progression.UnlockFinalBreakthrough();
        Assert.Equal(120, progression.LevelCap);
    }

    private static P1GameSession Session(ulong seed, bool tutorial = true) => P1GameSession.CreateNew(new PlayerIdentity(
        "封版铁誓", CharacterGender.Androgynous, CharacterSkinTone.Fair, CharacterHairStyle.Cropped,
        P23BaseClass.Fighter), seed, tutorial);
}

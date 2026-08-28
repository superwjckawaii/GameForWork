using System.Text.Json;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;

namespace GameForWork.Tests;

public sealed class P1SessionTests
{
    [Fact]
    public void NewSessionCreatesCharacterMercenaryTownAndLockedExpedition()
    {
        P1GameSession session = CreateSession();

        Assert.Equal("阿斯特", session.Player.Name);
        Assert.False(string.IsNullOrWhiteSpace(session.MercenaryName));
        Assert.Equal(5, session.Passives.MemoryAshes);
        Assert.Equal(0, session.World.Hero.Queue.Count);
        Assert.Equal(0, session.World.Mercenaries.Queue.Count);
        Assert.False(session.IsExpeditionUnlocked);
        Assert.Equal("core.campaign.act1.node1", session.Campaign.CurrentNode?.StableId);
        Assert.Equal(2, session.HeroBuild.Equipment.CoreSkillCapacity);
        Assert.Equal(5, session.HeroBuild.Equipment.SupportLinkCapacity);
    }

    [Fact]
    public void CharacterIdentityTrimsNameAndRejectsInvalidLength()
    {
        PlayerIdentity identity = Identity("  阿斯特  ").Validate();
        Assert.Equal("阿斯特", identity.Name);
        Assert.Throws<ArgumentException>(() => Identity("A").Validate());
    }

    [Fact]
    public void SessionSnapshotSurvivesJsonRoundTrip()
    {
        P1GameSession session = CreateSession();
        session.DebugTwentyTimes = true;
        session.SetHeavyStrikeSupports(SkillSupport.Bleed | SkillSupport.AttackSpeed);
        session.Advance(1_000);

        string json = JsonSerializer.Serialize(session.Capture());
        P1GameSessionSnapshot snapshot = JsonSerializer.Deserialize<P1GameSessionSnapshot>(json)!;
        P1GameSession restored = P1GameSession.Restore(snapshot);

        Assert.Equal(session.Player, restored.Player);
        Assert.Equal(session.MercenaryName, restored.MercenaryName);
        Assert.Equal(session.DebugTwentyTimes, restored.DebugTwentyTimes);
        Assert.Equal(session.HeavyStrikeSupports, restored.HeavyStrikeSupports);
        Assert.Equal(session.World.Hero.Queue.Maps, restored.World.Hero.Queue.Maps);
        Assert.Equal(session.World.Hero.ActiveMap, restored.World.Hero.ActiveMap);
        Assert.Equal(
            session.World.Hero.RemainingMapTimeMilliseconds,
            restored.World.Hero.RemainingMapTimeMilliseconds);
    }

    [Fact]
    public void DebugSpeedAdvancesTwentySecondsPerRealSecond()
    {
        P1GameSession session = CreateSession();
        session.DebugTwentyTimes = true;

        session.Advance(1_000);

        Assert.Equal(20_000, session.Campaign.CurrentNodeElapsedMilliseconds);
    }

    [Fact]
    public void ResponsiveAdvancePublishesPreparedCampaignTimeline()
    {
        P1GameSession session = CreateSession();

        for (int attempt = 0; attempt < 2_000 && session.Campaign.ActiveTimeline is null; attempt++)
        {
            session.AdvanceResponsive(50);
            Thread.Sleep(1);
        }

        Assert.NotNull(session.Campaign.ActiveTimeline);
        Assert.True(session.Campaign.CurrentNodeElapsedMilliseconds > 0);
    }

    [Fact]
    public void CombatPreviewIncludesExpandedCalculationSteps()
    {
        CombatPreview preview = CreateSession().GetCombatPreview();

        Assert.True(preview.AverageHitDamage.Value > 0);
        Assert.Contains(preview.AverageHitDamage.Steps, step => step.Label == "伤害增加总和");
        Assert.True(preview.EffectiveLife.Value > 0);
    }

    [Fact]
    public void StarterBuildCanResolveItsFirstCampaignNode()
    {
        P1GameSession session = CreateSession();

        var result = session.Advance(240_000);

        Assert.Equal(0, result.TotalMapsCompleted + result.TotalMapsFailed);
        Assert.True(session.Campaign.CompletedNodeIds.Contains("core.campaign.act1.node1"),
            $"defeated={session.Campaign.Defeated}; reason={session.Campaign.StoryLog.LastOrDefault()}; " +
            $"elapsed={session.Campaign.CurrentNodeElapsedMilliseconds}; " +
            $"timeline={session.Campaign.ActiveTimeline?.DurationMilliseconds}; " +
            $"outcome={session.Campaign.ActiveTimeline?.Outcome}; life={session.Campaign.ActiveTimeline?.FinalHeroLife}");
        Assert.NotEqual("core.campaign.act1.node1", session.Campaign.CurrentNode?.StableId);
    }

    [Fact]
    public void OnlineAndOfflineTimeSegmentationProduceSameSessionState()
    {
        P1GameSession whole = CreateSession();
        P1GameSession segmented = CreateSession();

        whole.Advance(90_000);
        for (int index = 0; index < 90; index++)
        {
            segmented.Advance(1_000);
        }

        Assert.Equal(
            JsonSerializer.Serialize(whole.Capture()),
            JsonSerializer.Serialize(segmented.Capture()));
    }

    [Fact]
    public void PausedBattleCanAdvanceTownWithoutStartingQueues()
    {
        P1GameSession session = CreateSession();

        session.AdvanceTownOnly(150_000);

        Assert.Equal(0, session.World.Hero.Queue.Count);
        Assert.Null(session.World.Hero.ActiveMap);
    }

    [Fact]
    public void DebugSpeedDoesNotMultiplyTrueOfflineTime()
    {
        P1GameSession session = CreateSession();
        session.DebugTwentyTimes = true;

        session.AdvanceOffline(1_000);

        Assert.Equal(1_000, session.Campaign.CurrentNodeElapsedMilliseconds);
    }

    [Fact]
    public void VersionOneSnapshotUpgradesMercenaryStarterLoadout()
    {
        P1GameSessionSnapshot legacy = CreateSession().Capture() with { FormatVersion = 1 };

        P1GameSession restored = P1GameSession.Restore(legacy);

        Assert.NotNull(restored.World.Mercenaries.Build.LifeFlask);
        Assert.NotNull(restored.World.Mercenaries.Build.HeavyStrikeProfile);
        Assert.Equal(3, restored.Town.Roster.Count);
        Assert.Equal(3, restored.World.Mercenaries.Build.PartySize);
        Assert.Equal(105, restored.World.Mercenaries.Build.Sheet.Equipment.Armor);
        Assert.Equal(1_720, restored.World.Mercenaries.Build.Sheet.MaximumLife().Value);
    }

    private static P1GameSession CreateSession() => P1GameSession.CreateNew(Identity("阿斯特"), 1234);

    private static PlayerIdentity Identity(string name) => new(
        name,
        CharacterGender.Androgynous,
        CharacterSkinTone.Umber,
        CharacterHairStyle.Braided,
        P1Ascendancy.IronOath);
}

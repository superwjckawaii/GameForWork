using System.Text.Json;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;

namespace GameForWork.Tests;

public sealed class P1SessionTests
{
    [Fact]
    public void NewSessionCreatesCharacterMercenaryTownAndTenMaps()
    {
        P1GameSession session = CreateSession();

        Assert.Equal("阿斯特", session.Player.Name);
        Assert.False(string.IsNullOrWhiteSpace(session.MercenaryName));
        Assert.Equal(10, session.World.Economy.ExpeditionSupplies);
        Assert.Equal(5, session.Passives.MemoryAshes);
        Assert.Equal(5, session.World.Hero.Queue.Count);
        Assert.Equal(5, session.World.Mercenaries.Queue.Count);
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

        Assert.Equal(70_000, session.World.Hero.RemainingMapTimeMilliseconds);
        Assert.Equal(70_000, session.World.Mercenaries.RemainingMapTimeMilliseconds);
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
    public void StarterBuildCanResolveItsFirstSafeMapCycle()
    {
        P1GameSession session = CreateSession();

        var result = session.Advance(90_000);

        Assert.Equal(2, result.TotalMapsCompleted + result.TotalMapsFailed);
        Assert.True(session.World.Hero.MapsCompleted > 0, "Starter hero should clear the first area-level 1 map.");
        Assert.True(session.World.Mercenaries.MapsCompleted > 0, "Starter mercenary should clear the first area-level 1 map.");
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

        int produced = session.AdvanceTownOnly(150_000);

        Assert.Equal(1, produced);
        Assert.Equal(11, session.World.Economy.ExpeditionSupplies);
        Assert.Equal(5, session.World.Hero.Queue.Count);
        Assert.Null(session.World.Hero.ActiveMap);
    }

    [Fact]
    public void DebugSpeedDoesNotMultiplyTrueOfflineTime()
    {
        P1GameSession session = CreateSession();
        session.DebugTwentyTimes = true;

        session.AdvanceOffline(1_000);

        Assert.Equal(89_000, session.World.Hero.RemainingMapTimeMilliseconds);
    }

    [Fact]
    public void VersionOneSnapshotUpgradesMercenaryStarterLoadout()
    {
        P1GameSessionSnapshot legacy = CreateSession().Capture() with { FormatVersion = 1 };

        P1GameSession restored = P1GameSession.Restore(legacy);

        Assert.NotNull(restored.World.Mercenaries.Build.LifeFlask);
        Assert.NotNull(restored.World.Mercenaries.Build.HeavyStrikeProfile);
        Assert.Equal(45, restored.World.Mercenaries.Build.Sheet.Equipment.Armor);
        Assert.Equal(112, restored.World.Mercenaries.Build.Sheet.MaximumLife().Value);
    }

    private static P1GameSession CreateSession() => P1GameSession.CreateNew(Identity("阿斯特"), 1234);

    private static PlayerIdentity Identity(string name) => new(
        name,
        CharacterGender.Androgynous,
        CharacterSkinTone.Umber,
        CharacterHairStyle.Braided,
        P1Ascendancy.IronOath);
}

using System.Text.Json;
using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Characters;
using GameForWork.Core.Harbor;
using GameForWork.Core.Management;
using GameForWork.Core.Spatial;
using GameForWork.Core.Equipment;

namespace GameForWork.Tests;

public sealed class HarborTests
{
    [Fact]
    public void DifficultyConfigurationKeepsTheConfirmedFeesAndEnemyLevels()
    {
        Assert.Equal(new[] { 1000, 3000, 8000 }, HarborDifficulty.All.Select(value => value.Fee));
        Assert.Equal(new[] { 100, 110, 120 }, HarborDifficulty.All.Select(value => value.ItemLevel));
    }

    [Fact]
    public void ViewingChestPersistsWithoutClaimingOrChangingCandidates()
    {
        GameSession session = Session();
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        session.Advance(300_000);
        HarborChest chest = Assert.Single(session.HarborChests);
        long gold = session.World.Economy.Gold;
        Assert.True(session.MarkHarborChestViewed(chest.Id));
        Assert.False(session.MarkHarborChestViewed(chest.Id));
        Assert.False(session.MarkHarborChestViewed("missing"));
        session = GameSession.Restore(JsonSerializer.Deserialize<GameSessionSnapshot>(JsonSerializer.Serialize(session.Capture()))!);
        HarborChest restored = Assert.Single(session.HarborChests);
        Assert.False(restored.IsNew);
        Assert.Equal(chest.Candidates.Select(item => item.InstanceId), restored.Candidates.Select(item => item.InstanceId));
        Assert.Equal(gold, session.World.Economy.Gold);
        Assert.True(session.ClaimHarborChest(chest.Id, 1));
        Assert.Empty(session.HarborChests);
    }

    private static GameSession Session(bool unlocked = true)
    {
        GameSession session = GameSession.CreateNew(new("港口测试", CharacterGender.Androgynous,
            CharacterSkinTone.Fair, CharacterHairStyle.Cropped, BaseClass.Fighter), 100, tutorialEnabled: false);
        session = GameSession.Restore(session.Capture() with { Campaign = CampaignState.CreateLegacyCompleted().Capture() });
        if (unlocked) Assert.True(session.Endgame.TryCompleteFinalBreakthrough(100, true));
        session.World.Economy.AddDispositionProceeds(100_000, 0);
        foreach (TeamExpeditionState team in session.World.Teams)
            team.UpdateBuild(StrongBuild(team.Build));
        return session;
    }

    private static TeamBuild StrongBuild(TeamBuild build) => build with
    {
        Sheet = build.Sheet with { Level = 120, FlatMaximumLife = 100_000, FlatLifeRegeneration = 2000 },
        Weapon = build.Weapon with { MinimumPhysicalDamage = 10_000, MaximumPhysicalDamage = 15_000 },
        MovementSpeedBasisPoints = 20_000, AlwaysHit = true,
    };

    [Fact]
    public void EntryValidatesUnlockDifficultyFundsAndOccupancyBeforeSpending()
    {
        GameSession session = Session(false);
        int gold = session.World.Economy.Gold;
        Assert.False(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        Assert.True(session.Endgame.TryCompleteFinalBreakthrough(100, true));
        Assert.False(session.StartHarbor(ExpeditionTeamKind.Hero, 4));
        session.World.Hero.Queue.TryEnqueue(new("occupied", 1));
        Assert.False(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        Assert.Equal(gold, session.World.Economy.Gold);
    }

    [Fact]
    public void SuccessHasThreeRegionsAndOnlyGrantsOneProtectedChoiceAfterCompletion()
    {
        GameSession session = Session();
        int gold = session.World.Economy.Gold;
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        HarborRun run = session.HarborDispatchFor(ExpeditionTeamKind.Hero)!.ActiveRun!;
        Assert.True(run.Succeeded);
        Assert.True(run.IsValid);
        Assert.Equal(3, run.Regions.Count);
        Assert.Empty(session.HarborChests);
        Assert.Equal(gold - 1000, session.World.Economy.Gold);
        Assert.False(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        session.Advance(run.DurationMilliseconds - 1);
        Assert.Empty(session.HarborChests);
        session.Advance(1);
        HarborChest chest = Assert.Single(session.HarborChests);
        Assert.Equal(3, chest.Candidates.Select(item => item.Base.StableId).Distinct().Count());
        Assert.False(session.ClaimHarborChest(chest.Id, -1));
        Assert.True(session.ClaimHarborChest(chest.Id, 1));
        Assert.False(session.ClaimHarborChest(chest.Id, 1));
        Assert.Contains(session.World.Storage.Items.Concat(session.Management.Recovery),
            item => item.InstanceId == chest.Candidates[1].InstanceId && item.IsLocked);
        session.Advance(1000);
        Assert.Empty(session.HarborChests);
    }

    [Fact]
    public void CancelLosesOnlyFeeAndStopRepeatFinishesCurrentRun()
    {
        GameSession session = Session();
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 2, 3));
        int gold = session.World.Economy.Gold;
        Assert.True(session.CancelHarbor(ExpeditionTeamKind.Hero));
        Assert.False(session.CancelHarbor(ExpeditionTeamKind.Hero));
        Assert.Equal(gold, session.World.Economy.Gold);
        Assert.Empty(session.HarborChests);
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 1, 3));
        Assert.True(session.StopHarborRepeat(ExpeditionTeamKind.Hero));
        session.AdvanceOffline(300_000);
        Assert.Single(session.HarborChests);
        Assert.False(session.IsHarborActive(ExpeditionTeamKind.Hero));
    }

    [Fact]
    public void MidRunJsonRestoreMatchesOnlineAndOfflineWithoutRecharging()
    {
        GameSession online = Session();
        Assert.True(online.StartHarbor(ExpeditionTeamKind.Hero, 1));
        online.Advance(1500);
        GameSession restored = GameSession.Restore(JsonSerializer.Deserialize<GameSessionSnapshot>(JsonSerializer.Serialize(online.Capture()))!);
        Assert.True(restored.IsHarborActive(ExpeditionTeamKind.Hero));
        for (int i = 0; i < 600; i++) online.Advance(500);
        restored.AdvanceOffline(300_000);
        Assert.Equal(online.World.Economy.Gold, restored.World.Economy.Gold);
        Assert.Equal(JsonSerializer.Serialize(online.HarborChests), JsonSerializer.Serialize(restored.HarborChests));
    }

    [Fact]
    public void CorruptRunRefundsOnceAndDoesNotCreateAChest()
    {
        GameSession session = Session();
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        GameSessionSnapshot snapshot = session.Capture();
        HarborDispatch dispatch = snapshot.Harbor!.Dispatches.Single();
        snapshot = snapshot with { Harbor = snapshot.Harbor with { Dispatches = [dispatch with
            { ActiveRun = dispatch.ActiveRun! with { Checksum = "damaged" } }] } };
        GameSession recovered = GameSession.Restore(snapshot);
        Assert.Equal(session.World.Economy.Gold + 1000, recovered.World.Economy.Gold);
        Assert.False(recovered.IsHarborActive(ExpeditionTeamKind.Hero));
        Assert.Empty(recovered.HarborChests);
        GameSession again = GameSession.Restore(recovered.Capture());
        Assert.Equal(recovered.World.Economy.Gold, again.World.Economy.Gold);
    }

    [Fact]
    public void BothTeamsCanRunButCannotStartMapsOrBossChallenges()
    {
        GameSession session = Session();
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Mercenaries, 1));
        session.AssignExpedition(ExpeditionTeamKind.Hero, GameForWork.Core.Expeditions.ExpeditionTarget.SafeMaps,
            GameForWork.Core.Expeditions.DispatchMode.Once);
        Assert.Empty(session.World.Hero.Queue.Maps);
        Assert.False(session.TryChallengeCitadel());
        Assert.Throws<InvalidOperationException>(() => session.World.Hero.StartMap(new("invalid", 1), MapRoute.Safe, 1000));
        session.AdvanceOffline(300_000);
        Assert.Equal(2, session.HarborChests.Count);
    }

    [Fact]
    public void MovementAndSurvivalAreMeasuredByActualCombatAndObjectiveEvents()
    {
        TeamBuild build = Session().World.Hero.Build;
        HarborRun fast = HarborRunner.Run("fast", 1, build, 500);
        HarborRun slow = HarborRunner.Run("slow", 1, build with { MovementSpeedBasisPoints = 5000 }, 500);
        Assert.True(fast.Succeeded);
        Assert.True(fast.DurationMilliseconds < slow.DurationMilliseconds);
        Assert.Contains(fast.Regions.SelectMany(region => region.Combat.Events), value => value.Detail == "自动撤离" && value.Presentation?.Trajectory.Count == 2);
        Assert.Contains(fast.Regions.SelectMany(region => region.Combat.Events), value => value.Presentation?.Shape == "circle");
        Assert.Contains(fast.Regions.SelectMany(region => region.Combat.Events), value => value.SourceId == "harbor.pursuit");
        TeamBuild fragile = build with { Sheet = build.Sheet with { FlatMaximumLife = 0, FlatLifeRegeneration = 0 },
            Weapon = build.Weapon with { MinimumPhysicalDamage = 1, MaximumPhysicalDamage = 1 } };
        Assert.False(HarborRunner.Run("fragile", 3, fragile, 500).Succeeded);
    }

    [Fact]
    public void AllHarborBasesUseRealModifiersAndExplicitExistingArt()
    {
        Assert.Equal(18, EquipmentCatalog.Bases.Count(value => value.ItemTags.Contains("harbor")));
        Assert.Equal(18, HarborRunner.CandidateBaseIds.Count);
        Assert.Equal(HarborRunner.CandidateBaseIds.Order(), Enumerable.Range(1, 256)
            .SelectMany(seed => HarborRunner.SelectCandidateBaseIds((ulong)seed)).Distinct().Order());
        Assert.All(Enumerable.Range(1, 32), seed =>
            Assert.Equal(3, HarborRunner.SelectCandidateBaseIds((ulong)seed).Distinct().Count()));
        foreach (string id in HarborRunner.CandidateBaseIds)
        {
            ItemInstance item = ItemGenerator.Generate(id, 120, ItemRarity.Rare, 100);
            ItemInstance rebound = EquipmentItemRebinder.Rebind(item);
            Assert.Equal(item.InstanceId, rebound.InstanceId);
            Assert.Same(item.Base, rebound.Base);
            Assert.Equal(item.EffectiveImplicitComponents, rebound.EffectiveImplicitComponents);
            Assert.Equal(item.Affixes.Select(affix => affix.Value), rebound.Affixes.Select(affix => affix.Value));
            Assert.InRange(EquipmentBaseArt.IconIndex(item.Base), 0, 243);
        }
        Assert.Equal(new[] { ItemModifierKind.MercyMaximum, ItemModifierKind.RageMaximum },
            ItemBases.Get("harbor.base.returning_tide_sword").BaseImplicitComponents.Select(component => component.ModifierKind));
    }

    [Fact]
    public void FortyEightHoursKeepsUnopenedChestsAndStopsWhenSpendableGoldRunsOut()
    {
        GameSession session = Session();
        session.World.Economy.TrySpendGold(session.World.Economy.Gold - 2000);
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 1, 999));
        session.AdvanceOffline(48L * 60 * 60 * 1000);
        Assert.Equal(2, session.HarborChests.Count);
        // Existing town milestones may still pay gold; unopened chests never do.
        Assert.InRange(session.World.Economy.Gold, 0, 999);
        Assert.False(session.IsHarborActive(ExpeditionTeamKind.Hero));
        Assert.Equal("金币不足，停止派遣", session.HarborDispatchFor(ExpeditionTeamKind.Hero)!.Status);
        Assert.False(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        GameSession restored = GameSession.Restore(session.Capture());
        Assert.Equal(2, restored.HarborChests.Count);
    }

    [Theory]
    [InlineData(false, 1000)]
    [InlineData(true, 2000)]
    public void FailedRunsNeverRewardAndOnlyRepeatWhenRequested(bool continueOnFailure, int spent)
    {
        GameSession session = Session();
        TeamBuild build = session.World.Hero.Build;
        session.World.Hero.UpdateBuild(build with { Sheet = build.Sheet with { FlatMaximumLife = 0, FlatLifeRegeneration = 0 },
            Weapon = build.Weapon with { MinimumPhysicalDamage = 1, MaximumPhysicalDamage = 1 } });
        int gold = session.World.Economy.Gold;
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 1, 2, continueOnFailure));
        Assert.False(session.HarborDispatchFor(ExpeditionTeamKind.Hero)!.ActiveRun!.Succeeded);
        session.AdvanceOffline(600_000);
        Assert.Equal(gold - spent, session.World.Economy.Gold);
        Assert.Empty(session.HarborChests);
    }

    [Fact]
    public void FullStorageUsesExistingRecoveryWithoutAutomaticDisposition()
    {
        GameSession session = Session();
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        session.AdvanceOffline(300_000);
        for (int index = session.World.Storage.Count; index < session.World.Storage.Capacity; index++)
            Assert.True(session.World.Storage.TryStore(ItemGenerator.Generate("equipment.base.heavy_battleaxe", 1,
                ItemRarity.Basic, (ulong)index, $"filler.{index}")));
        HarborChest chest = Assert.Single(session.HarborChests);
        Assert.True(session.ClaimHarborChest(chest.Id, 0));
        Assert.Contains(session.Management.Recovery, item => item.InstanceId == chest.Candidates[0].InstanceId && item.IsLocked);
        Assert.False(session.ClaimHarborChest(chest.Id, 2));
    }

    [Fact]
    public void ObjectiveTimeoutDoesNotBecomeSuccessAfterEnemiesDie()
    {
        TeamBuild build = Session().World.Hero.Build with { MovementSpeedBasisPoints = 1 };
        HarborRun run = HarborRunner.Run("timeout", 1, build, 101);
        Assert.False(run.Succeeded);
        Assert.InRange(run.DurationMilliseconds, 1, 300_000);
        Assert.Equal(BattleOutcome.Timeout, run.Regions.Last().Combat.Outcome);
    }

    [Fact]
    public void OldSaveHasNoSyntheticChestsAndDuplicateActionsAreRejected()
    {
        GameSession session = Session();
        GameSession old = GameSession.Restore(session.Capture() with { FormatVersion = 25, Harbor = null });
        Assert.Empty(old.HarborChests);
        Assert.Equal(session.World.Economy.Gold, old.World.Economy.Gold);
        Assert.True(session.StartHarbor(ExpeditionTeamKind.Hero, 1));
        GameSessionSnapshot snapshot = session.Capture();
        HarborDispatch dispatch = snapshot.Harbor!.Dispatches.Single();
        Assert.Throws<InvalidDataException>(() => GameSession.Restore(snapshot with
        { Harbor = snapshot.Harbor with { Dispatches = [dispatch, dispatch with { Team = ExpeditionTeamKind.Mercenaries }] } }));
    }
}

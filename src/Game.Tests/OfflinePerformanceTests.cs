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

// Wall-clock budgets must not compete with unrelated combat simulations in parallel test collections.
[CollectionDefinition("Offline performance", DisableParallelization = true)]
public sealed class OfflinePerformanceCollection;

[Collection("Offline performance")]
public sealed class OfflinePerformanceTests
{
    [Fact]
    public void OfflineFortyEightHourSettlementStaysWithinReleaseBudget()
    {
        GameSession session = GameSession.CreateNew(new PlayerIdentity(
            "离线性能回归", CharacterGender.Androgynous, CharacterSkinTone.Fair, CharacterHairStyle.Cropped,
            BaseClass.Fighter), 0x220022UL, tutorialEnabled: false);
        Stopwatch timer = Stopwatch.StartNew();

        GameForWork.Core.Campaign.World.OfflineResult result = session.AdvanceOffline(OfflineTime.MaximumMilliseconds);

        timer.Stop();
        Assert.Equal(OfflineTime.MaximumMilliseconds, result.EffectiveMilliseconds);
        Assert.True(timer.Elapsed.TotalSeconds < ReleaseTargets.MaximumOfflineSeconds,
            $"48h settlement took {timer.Elapsed.TotalSeconds:F3}s; budget is {ReleaseTargets.MaximumOfflineSeconds:F1}s.");
    }

}

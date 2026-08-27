using GameForWork.Core.Offline;
using GameForWork.Core.Persistence;
using GameForWork.Core.Combat;
using System.Diagnostics;
using Xunit.Abstractions;

namespace GameForWork.Tests;

public sealed class OfflineAndSettingsTests(ITestOutputHelper output)
{
    [Fact]
    public void BackwardClockProducesNoOfflineTime()
    {
        OfflineElapsed elapsed = OfflineTime.Calculate(2_000, 1_000);
        Assert.Equal(0, elapsed.EffectiveMilliseconds);
        Assert.True(elapsed.ClockMovedBackward);
    }

    [Fact]
    public void OfflineTimeClampsAtFortyEightHours()
    {
        OfflineElapsed elapsed = OfflineTime.Calculate(0, OfflineTime.MaximumMilliseconds + 1);
        Assert.Equal(OfflineTime.MaximumMilliseconds, elapsed.EffectiveMilliseconds);
        Assert.True(elapsed.WasClamped);
    }

    [Fact]
    public void FortyEightHoursConsumes1920PermitsPerTeam()
    {
        var watch = Stopwatch.StartNew();
        OfflineResult result = new OfflineSimulator().Simulate(OfflineTime.MaximumMilliseconds, 5);
        watch.Stop();
        output.WriteLine($"48-hour exact simulation elapsed: {watch.ElapsedMilliseconds} ms");
        Assert.Equal(1_920, result.BattlesPerTeam);
        Assert.Equal(3_840, result.TotalBattles);
        Assert.Equal(result.TotalBattles, result.HeroVictories + result.EnemyVictories + result.DrawsOrTimeouts);
    }

    [Fact]
    public void OutcomeOnlyAndFullReplayMatchPerFight()
    {
        var runner = new BattleRunner();
        for (ulong seed = 0; seed < 100; seed++)
        {
            BattleRun full = runner.RunAutomatic(seed);
            BattleOutcomeResult fast = runner.RunOutcomeOnly(seed);
            Assert.Equal(full.FinalState.Outcome, fast.Outcome);
            Assert.Equal(full.FinalHash, fast.FinalHash);
        }
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GameForWork.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            var expected = new GameSettings
            {
                AlwaysOnTop = false,
                SnapEnabled = false,
                OpacityPercent = 75,
                FontScalePercent = 125,
                CloseToTray = true,
            };
            store.Save(expected);
            Assert.Equal(expected, store.Load());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

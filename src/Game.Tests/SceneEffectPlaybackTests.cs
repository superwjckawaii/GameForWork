using GameForWork.Core.Scenes;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class SceneEffectPlaybackTests
{
    [Theory]
    [InlineData("skill:hit|critical", 12, true)]
    [InlineData("critical|skill:hit", 12, true)]
    [InlineData("skill:hit|critical", 0, false)]
    [InlineData("skill:hit|supports:18446744073709551615", 12, false)]
    [InlineData("skill:critical_strike|critical:false", 12, false)]
    public void CriticalAccentRequiresActualPositiveCriticalHit(string detail, int damage, bool expected)
    {
        var item = Warning("hit") with { Kind = SceneEventKind.SkillEffect, Detail = detail, Value = damage };
        Assert.Equal(expected, SceneEffectPlayback.IsCriticalHit(item));
    }

    [Fact]
    public void PresentationClockControlsStartProgressAndExpiry()
    {
        SceneEvent item = Warning("delayed");
        item = item with { Presentation = item.Presentation! with { StartsAtMilliseconds = 1_000, EndsAtMilliseconds = 5_000 } };
        Assert.Empty(SceneEffectPlayback.Select([item], 999, 1));
        Assert.Equal(0, SceneEffectPlayback.Progress(item, 999));
        Assert.Equal(.5f, SceneEffectPlayback.Progress(item, 3_000));
        Assert.Single(SceneEffectPlayback.Select([item], 4_999, 1));
        Assert.Equal(1, SceneEffectPlayback.Progress(item, 5_000));
        Assert.Empty(SceneEffectPlayback.Select([item], 5_000, 1));
    }

    [Fact]
    public void TrajectoryKeepsCornersAndReturnLegWithoutCuttingAcrossThem()
    {
        var path = Warning("path").Presentation! with
        { Trajectory = [new(0, 0), new(100, 0), new(100, 300), new(0, 0)] };
        Assert.Equal(new Point(0, 0), SceneEffectPlayback.TrajectoryPosition(path, -1));
        Assert.Equal(new Point(100, 79), SceneEffectPlayback.TrajectoryPosition(path, .25f));
        Assert.Equal(new Point(100, 258), SceneEffectPlayback.TrajectoryPosition(path, .5f));
        Assert.Equal(new Point(0, 0), SceneEffectPlayback.TrajectoryPosition(path, 1));
        var stationary = path with { Trajectory = [new(50, 60), new(50, 60)] };
        Assert.Equal(new Point(50, 60), SceneEffectPlayback.TrajectoryPosition(stationary, .5f));
    }

    [Fact]
    public void LongWarningsSurviveRecentWindowAndZeroDecorationBudget()
    {
        SceneEvent[] warnings = Enumerable.Range(0, 12).Select(index => Warning(index.ToString())).ToArray();
        Assert.Equal(12, SceneEffectPlayback.Select(warnings, 2_000, 0).Count);
        Assert.Empty(SceneEffectPlayback.Select(warnings, 3_000, 100));
        Assert.Empty(SceneEffectPlayback.Select(warnings, -1, 100));
    }

    [Fact]
    public void RepeatedHazardPulsesDrawOnceWithoutMergingDifferentNodes()
    {
        SceneEvent first = Warning("ground");
        SceneEvent pulse = first with { AtMilliseconds = 100 };
        SceneEvent otherNode = first with { NodeIndex = 2 };
        Assert.Equal(2, SceneEffectPlayback.Select([first, pulse, otherNode], 1_000, 0).Count);
    }

    [Fact]
    public void DecorationBudgetDoesNotConsumeWarningSlots()
    {
        SceneEvent decoration = Warning("decoration") with { Presentation = null, Kind = SceneEventKind.SkillEffect };
        Assert.Equal(2, SceneEffectPlayback.Select([Warning("danger"), decoration], 100, 1).Count);
        Assert.Single(SceneEffectPlayback.Select([Warning("danger"), decoration], 1_000, 1));
    }

    private static SceneEvent Warning(string id) => new(0, SceneEventKind.BossPhase, 1, 1, 0,
        "warning", 100, 100, 100, 100, 0, 0, 100, 100, new(6, 12),
        new(6_000, 12_000), new(1_000, 2_000), new(id, 0, 3_000, "circle", 2_000, new(0, 0), []));
}

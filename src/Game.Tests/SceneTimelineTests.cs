using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Scenes;

namespace GameForWork.Tests;

public sealed class SceneTimelineTests
{
    [Fact]
    public void SafeMapUsesMultipleTwelveByTwentyFourNodes()
    {
        SceneTimeline timeline = SceneTimelineBuilder.BuildMapAttempt(
            PowerfulBuild(), new MapItem("grid-map", 3), MapRoute.Safe, 1, 41);

        Assert.Equal(12, timeline.GridWidth);
        Assert.Equal(24, timeline.GridHeight);
        Assert.InRange(timeline.NodeCount, 5, 8);
        Assert.Equal(timeline.TotalWaves, timeline.Encounters.Select(item => item.NodeIndex).Distinct().Count());
        Assert.InRange(timeline.TotalWaves, 3, timeline.NodeCount);
        Assert.NotNull(timeline.SpatialFrames);
        Assert.Contains(timeline.SpatialFrames!, frame => frame.Enemies.Count >= 8);
        Assert.Contains(timeline.Events, item => item.Kind == SceneEventKind.TravelStarted);
        Assert.Contains(timeline.Events, item => item.Kind == SceneEventKind.SceneCompleted);
    }

    [Fact]
    public void MovementSpeedShortensTheSameSceneWithoutChangingCombatOutcome()
    {
        TeamBuild normal = PowerfulBuild();
        TeamBuild fast = normal with { MovementSpeedBasisPoints = 15_000 };
        var map = new MapItem("movement-map", 3);

        SceneTimeline normalTimeline = SceneTimelineBuilder.BuildMapAttempt(normal, map, MapRoute.Safe, 1, 77);
        SceneTimeline fastTimeline = SceneTimelineBuilder.BuildMapAttempt(fast, map, MapRoute.Safe, 1, 77);

        Assert.True(fastTimeline.DurationMilliseconds < normalTimeline.DurationMilliseconds);
        Assert.Equal(normalTimeline.Outcome, fastTimeline.Outcome);
        Assert.NotEqual(normalTimeline.FinalHash, fastTimeline.FinalHash);
    }

    [Fact]
    public void SceneTimelineIsDeterministicForTheSameSeed()
    {
        var map = new MapItem("deterministic-map", 5);

        SceneTimeline first = SceneTimelineBuilder.BuildMapAttempt(
            PowerfulBuild(), map, MapRoute.Abyss, 2, 1_337);
        SceneTimeline second = SceneTimelineBuilder.BuildMapAttempt(
            PowerfulBuild(), map, MapRoute.Abyss, 2, 1_337);

        Assert.Equal(first.FinalHash, second.FinalHash);
        Assert.Equal(first.DurationMilliseconds, second.DurationMilliseconds);
        Assert.Equal(first.Events, second.Events);
    }

    [Fact]
    public void FormalMapRunDurationComesFromAttemptTimelines()
    {
        var map = new MapItem("duration-map", 2);
        MapRunResult run = new MapRunner(new MapAttemptResolver()).Run(
            map, MapRoute.Safe, PowerfulBuild(), 991);

        Assert.True(run.DurationMilliseconds > 0);
        Assert.Equal(
            run.Attempts.Sum(attempt => attempt.Timeline?.DurationMilliseconds ?? 0),
            run.DurationMilliseconds);
        Assert.NotEqual(90_000, run.DurationMilliseconds);
    }

    private static TeamBuild PowerfulBuild() => new(
        new CharacterSheet(
            60,
            new CharacterAttributes(250, 130, 120, 100),
            new DefensiveEquipment(600, 100, 250),
            FlatMaximumLife: 1_500),
        new WeaponProfile("test.scenes", 800, 1_000, 1_500, 10_000),
        new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.AttackSpeed),
        FlatAccuracy: 1_000);
}

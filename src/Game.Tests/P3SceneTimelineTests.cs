using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.World;
using GameForWork.Core.P3;

namespace GameForWork.Tests;

public sealed class P3SceneTimelineTests
{
    [Fact]
    public void SafeMapUsesMultipleTwelveByTwentyFourNodes()
    {
        P3SceneTimeline timeline = P3SceneTimelineBuilder.BuildMapAttempt(
            PowerfulBuild(), new P1MapItem("grid-map", 3), MapRoute.Safe, 1, 41);

        Assert.Equal(12, timeline.GridWidth);
        Assert.Equal(24, timeline.GridHeight);
        Assert.Equal(8, timeline.NodeCount);
        Assert.Equal(8, timeline.Encounters.Select(item => item.NodeIndex).Distinct().Count());
        Assert.Equal(timeline.NodeCount, timeline.TotalWaves);
        Assert.NotNull(timeline.SpatialFrames);
        Assert.Contains(timeline.SpatialFrames!, frame => frame.Enemies.Count >= 8);
        Assert.Contains(timeline.Events, item => item.Kind == P3SceneEventKind.TravelStarted);
        Assert.Contains(timeline.Events, item => item.Kind == P3SceneEventKind.SceneCompleted);
    }

    [Fact]
    public void MovementSpeedShortensTheSameSceneWithoutChangingCombatOutcome()
    {
        P1TeamBuild normal = PowerfulBuild();
        P1TeamBuild fast = normal with { MovementSpeedBasisPoints = 15_000 };
        var map = new P1MapItem("movement-map", 3);

        P3SceneTimeline normalTimeline = P3SceneTimelineBuilder.BuildMapAttempt(normal, map, MapRoute.Safe, 1, 77);
        P3SceneTimeline fastTimeline = P3SceneTimelineBuilder.BuildMapAttempt(fast, map, MapRoute.Safe, 1, 77);

        Assert.True(fastTimeline.DurationMilliseconds < normalTimeline.DurationMilliseconds);
        Assert.Equal(normalTimeline.Outcome, fastTimeline.Outcome);
        Assert.NotEqual(normalTimeline.FinalHash, fastTimeline.FinalHash);
    }

    [Fact]
    public void SceneTimelineIsDeterministicForTheSameSeed()
    {
        var map = new P1MapItem("deterministic-map", 5);

        P3SceneTimeline first = P3SceneTimelineBuilder.BuildMapAttempt(
            PowerfulBuild(), map, MapRoute.Abyss, 2, 1_337);
        P3SceneTimeline second = P3SceneTimelineBuilder.BuildMapAttempt(
            PowerfulBuild(), map, MapRoute.Abyss, 2, 1_337);

        Assert.Equal(first.FinalHash, second.FinalHash);
        Assert.Equal(first.DurationMilliseconds, second.DurationMilliseconds);
        Assert.Equal(first.Events, second.Events);
    }

    [Fact]
    public void FormalMapRunDurationComesFromAttemptTimelines()
    {
        var map = new P1MapItem("duration-map", 2);
        P1MapRunResult run = new P1MapRunner(new P1MapAttemptResolver()).Run(
            map, MapRoute.Safe, PowerfulBuild(), 991);

        Assert.True(run.DurationMilliseconds > 0);
        Assert.Equal(
            run.Attempts.Sum(attempt => attempt.Timeline?.DurationMilliseconds ?? 0),
            run.DurationMilliseconds);
        Assert.NotEqual(90_000, run.DurationMilliseconds);
    }

    private static P1TeamBuild PowerfulBuild() => new(
        new CharacterSheet(
            60,
            new CharacterAttributes(250, 130, 120, 100),
            new DefensiveEquipment(600, 100, 250),
            FlatMaximumLife: 1_500),
        new WeaponProfile("test.p3", 800, 1_000, 1_500, 10_000),
        new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.AttackSpeed),
        FlatAccuracy: 1_000);
}

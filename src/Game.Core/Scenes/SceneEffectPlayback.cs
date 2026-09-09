using GameForWork.Core.Spatial;

namespace GameForWork.Core.Scenes;

/// <summary>Playback selection is independent of render quality and window size.</summary>
public static class SceneEffectPlayback
{
    public static bool IsCriticalHit(SceneEvent item) => item.Value > 0 &&
        item.Detail.Split('|').Contains("critical", StringComparer.Ordinal);

    public static float Progress(SceneEvent item, long elapsed, int fallbackLifetime = 900)
    {
        long start = item.Presentation?.StartsAtMilliseconds ?? item.AtMilliseconds;
        long duration = item.Presentation is { } p ? p.EndsAtMilliseconds - start : fallbackLifetime;
        return Math.Clamp((elapsed - start) / (float)Math.Max(1, duration), 0, 1);
    }

    // Trajectory points have no individual timestamps; traverse recorded segments at constant speed.
    public static Point TrajectoryPosition(SpatialPresentation path, float progress)
    {
        if (path.Trajectory.Count == 0) throw new ArgumentException("Trajectory must contain a point.", nameof(path));
        double total = 0;
        for (int i = 1; i < path.Trajectory.Count; i++) total += Length(path.Trajectory[i - 1], path.Trajectory[i]);
        double remaining = total * Math.Clamp(progress, 0, 1);
        for (int i = 1; i < path.Trajectory.Count; i++)
        {
            Point from = path.Trajectory[i - 1], to = path.Trajectory[i];
            double length = Length(from, to);
            if (length == 0) continue;
            if (remaining <= length)
                return new((int)Math.Round(from.XRaw + ((double)to.XRaw - from.XRaw) * remaining / length),
                    (int)Math.Round(from.YRaw + ((double)to.YRaw - from.YRaw) * remaining / length));
            remaining -= length;
        }
        return path.Trajectory[^1];

        static double Length(Point from, Point to) => Math.Sqrt(
            Math.Pow((double)to.XRaw - from.XRaw, 2) + Math.Pow((double)to.YRaw - from.YRaw, 2));
    }

    public static bool IsDanger(SceneEvent item) => item.Presentation is { RadiusRaw: > 0 } &&
        (item.Kind == SceneEventKind.BossPhase || item.Detail.Contains("持续危险地面", StringComparison.Ordinal));

    public static IReadOnlyList<SceneEvent> Select(IReadOnlyList<SceneEvent> events, long elapsed, int decorationLimit)
    {
        var active = events.Where(item => item.AtMilliseconds <= elapsed &&
            (item.Presentation is null || item.Presentation.StartsAtMilliseconds <= elapsed) &&
            (item.Presentation is { } p ? elapsed < p.EndsAtMilliseconds : elapsed - item.AtMilliseconds < 900));
        return active.Where(IsDanger).DistinctBy(item => (item.NodeIndex, item.Presentation!.ActionId))
            .Concat(active.Where(item => !IsDanger(item)).OrderByDescending(item => item.AtMilliseconds)
                .Take(Math.Max(0, decorationLimit))).ToArray();
    }
}

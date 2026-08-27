using GameForWork.Core.Combat;

namespace GameForWork.Core.Offline;

public sealed record OfflineElapsed(long EffectiveMilliseconds, bool ClockMovedBackward, bool WasClamped);

public sealed record OfflineResult(
    long EffectiveMilliseconds,
    int BattlesPerTeam,
    int TotalBattles,
    int HeroVictories,
    int EnemyVictories,
    int DrawsOrTimeouts,
    string LastHash);

public static class OfflineTime
{
    public const long MaximumMilliseconds = 48L * 60 * 60 * 1_000;

    public static OfflineElapsed Calculate(long lastObservedUtcMs, long nowUtcMs)
    {
        long raw = nowUtcMs - lastObservedUtcMs;
        if (raw < 0)
        {
            return new OfflineElapsed(0, true, false);
        }

        return new OfflineElapsed(Math.Min(raw, MaximumMilliseconds), false, raw > MaximumMilliseconds);
    }
}

public sealed class OfflineSimulator
{
    public const int PermitCountPerTeam = 2_000;
    public const int BattleIntervalSeconds = 90;

    public OfflineResult Simulate(long elapsedMilliseconds, ulong seed)
    {
        long effective = Math.Clamp(elapsedMilliseconds, 0, OfflineTime.MaximumMilliseconds);
        int battlesPerTeam = Math.Min(PermitCountPerTeam, checked((int)(effective / (BattleIntervalSeconds * 1_000L))));
        int totalBattles = checked(battlesPerTeam * 2);
        int heroVictories = 0;
        int enemyVictories = 0;
        int drawsOrTimeouts = 0;
        string lastHash = string.Empty;
        var runner = new BattleRunner();
        for (int index = 0; index < totalBattles; index++)
        {
            BattleOutcomeResult run = runner.RunOutcomeOnly(unchecked(seed + (ulong)index));
            lastHash = run.FinalHash;
            switch (run.Outcome)
            {
                case BattleOutcome.HeroVictory:
                    heroVictories++;
                    break;
                case BattleOutcome.EnemyVictory:
                    enemyVictories++;
                    break;
                case BattleOutcome.Draw:
                case BattleOutcome.Timeout:
                    drawsOrTimeouts++;
                    break;
                case BattleOutcome.Running:
                    throw new InvalidOperationException("Offline battle did not finish.");
                default:
                    throw new InvalidOperationException("Offline battle has an unknown outcome.");
            }
        }

        return new OfflineResult(effective, battlesPerTeam, totalBattles, heroVictories, enemyVictories, drawsOrTimeouts, lastHash);
    }
}

namespace GameForWork.Core.P1.Progression;

public sealed record ExperienceGainResult(
    int PreviousLevel,
    int NewLevel,
    int ExperienceAdded,
    int PassivePointsGained,
    bool ReachedLevelCap);

public sealed class CharacterProgression
{
    private static readonly int[] ExperienceToNextLevel = BuildExperienceCurve();

    public const int MaximumLevel = 60;
    public static readonly int TotalExperienceToCap = ExperienceToNextLevel.Sum();

    public int Level { get; private set; } = 1;
    public int Experience { get; private set; }
    public int EarnedPassivePoints { get; private set; }
    public bool FirstBossPassivePointClaimed { get; private set; }

    public ExperienceGainResult AddExperience(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        int previousLevel = Level;
        int previousPoints = EarnedPassivePoints;
        if (Level < MaximumLevel)
        {
            Experience = Math.Min(TotalExperienceToCap, checked(Experience + amount));
            while (Level < MaximumLevel && Experience >= CumulativeExperienceForLevel(Level + 1))
            {
                Level++;
                EarnedPassivePoints++;
            }
        }

        return new ExperienceGainResult(
            previousLevel,
            Level,
            amount,
            EarnedPassivePoints - previousPoints,
            Level == MaximumLevel);
    }

    public bool ClaimFirstBossPassivePoint()
    {
        if (FirstBossPassivePointClaimed)
        {
            return false;
        }

        FirstBossPassivePointClaimed = true;
        EarnedPassivePoints++;
        return true;
    }

    public void Restore(int level, int experience, int earnedPassivePoints, bool firstBossPassivePointClaimed)
    {
        if (level is < 1 or > MaximumLevel ||
            experience < CumulativeExperienceForLevel(level) ||
            experience > TotalExperienceToCap ||
            earnedPassivePoints is < 0 or > MaximumLevel)
        {
            throw new InvalidDataException("Character progression snapshot is invalid.");
        }

        Level = level;
        Experience = experience;
        EarnedPassivePoints = earnedPassivePoints;
        FirstBossPassivePointClaimed = firstBossPassivePointClaimed;
    }

    public void MigrateToMinimumLevel(int minimumLevel)
    {
        if (minimumLevel is < 1 or > MaximumLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLevel));
        }

        if (Level >= minimumLevel)
        {
            return;
        }

        Level = minimumLevel;
        Experience = CumulativeExperienceForLevel(minimumLevel);
        EarnedPassivePoints = Math.Max(EarnedPassivePoints, minimumLevel - 1 + (FirstBossPassivePointClaimed ? 1 : 0));
    }

    public static int RequiredExperience(int fromLevel)
    {
        if (fromLevel is < 1 or >= MaximumLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(fromLevel));
        }

        return ExperienceToNextLevel[fromLevel - 1];
    }

    public static int CumulativeExperienceForLevel(int level)
    {
        if (level is < 1 or > MaximumLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        int total = 0;
        for (int index = 0; index < level - 1; index++)
        {
            total = checked(total + ExperienceToNextLevel[index]);
        }

        return total;
    }

    private static int[] BuildExperienceCurve()
    {
        int[] curve = new int[MaximumLevel - 1];
        int[] opening = [100, 160, 240, 340, 460, 600, 760, 940, 1_140];
        Array.Copy(opening, curve, opening.Length);
        for (int index = opening.Length; index < curve.Length; index++)
        {
            int level = index + 1;
            curve[index] = checked(1_000 + level * level * 12);
        }

        return curve;
    }
}

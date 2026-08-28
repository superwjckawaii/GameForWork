using GameForWork.Core.Simulation;

namespace GameForWork.Core.P1.Combat;

public sealed record EnemyProfile(
    string StableId,
    string DisplayName,
    int Life,
    int MinimumPhysicalDamage,
    int MaximumPhysicalDamage,
    int Armor,
    int Evasion,
    int Accuracy,
    int MovementSpeedRawPerSecond,
    int AttacksPerSecondMilli,
    int ThreatPoints);

public static class P1Enemies
{
    public static readonly EnemyProfile CorruptedWorker = new(
        "core.enemy.corrupted_worker", "腐化工役", 35, 4, 6, 2, 5, 50, 2_200, 1_100, 1);

    public static readonly EnemyProfile GateHound = new(
        "core.enemy.gate_hound", "门扉猎犬", 25, 3, 5, 0, 20, 65, 3_500, 1_400, 1);

    public static readonly EnemyProfile OathlessGuard = new(
        "core.enemy.oathless_guard", "失誓守卫", 70, 7, 10, 25, 3, 55, 1_800, 800, 2);

    public static readonly EnemyProfile AbyssWarden = new(
        "core.enemy.abyss_warden", "裂渊监守者", 250, 8, 12, 20, 5, 70, 2_000, 1_000, 0);

    public static IReadOnlyList<EnemyProfile> NormalEnemies { get; } =
    [
        CorruptedWorker, GateHound, OathlessGuard,
        new("core.enemy.ash_bone_archer", "烬骨弓手", 31, 4, 7, 1, 14, 62, 2_100, 1_050, 1),
        new("core.enemy.drowned_corpse", "溺尸", 48, 5, 8, 8, 2, 48, 1_600, 850, 1),
        new("core.enemy.crypt_beetle", "墓穴甲虫", 39, 4, 6, 18, 4, 45, 1_900, 950, 1),
        new("core.enemy.thorn_beast", "棘兽", 62, 7, 11, 12, 5, 58, 2_400, 900, 2),
        new("core.enemy.iron_dryad", "铁皮树妖", 76, 6, 9, 24, 2, 52, 1_500, 780, 2),
        new("core.enemy.oathless_crossbow", "失誓弩手", 44, 6, 10, 4, 12, 70, 1_900, 1_000, 1),
        new("core.enemy.bog_beast", "泥沼兽", 68, 7, 12, 10, 3, 55, 1_800, 850, 2),
        new("core.enemy.blood_leech", "血蛭", 28, 3, 6, 0, 22, 68, 3_200, 1_300, 1),
        new("core.enemy.crystal_scarab", "晶壳虫", 52, 5, 9, 20, 8, 56, 2_000, 950, 1),
        new("core.enemy.mine_thrall", "矿奴", 57, 6, 10, 14, 4, 52, 1_700, 900, 1),
        new("core.enemy.penitent", "赎罪者", 54, 5, 9, 6, 9, 64, 2_000, 1_000, 1),
        new("core.enemy.bell_wraith", "钟灵", 42, 5, 8, 0, 16, 72, 2_500, 1_100, 1),
        new("core.enemy.tide_raider", "潮盗", 65, 7, 11, 11, 8, 67, 2_300, 1_050, 2),
        new("core.enemy.salt_corpse", "盐尸", 59, 6, 9, 16, 2, 50, 1_600, 800, 1),
        new("core.enemy.cinder_raven", "烟羽鸦", 33, 4, 7, 0, 25, 75, 3_600, 1_350, 1),
    ];
}

public enum EliteAffix
{
    Massive,
    Swift,
    IronSkin,
    Lacerating,
    CorpseExplosion,
    ArcaneWard,
}

public sealed record ScaledEnemy(
    EnemyProfile Base,
    int AreaLevel,
    int Life,
    int MinimumPhysicalDamage,
    int MaximumPhysicalDamage,
    int Armor,
    int Evasion,
    int AttacksPerSecondMilli,
    IReadOnlyList<EliteAffix> EliteAffixes,
    bool AbyssRoute);

public static class EnemyRules
{
    public static int ThreatBudget(int areaLevel) =>
        checked(3 + ((ValidateAreaLevel(areaLevel) - 1) / 2));

    public static ScaledEnemy Scale(
        EnemyProfile profile,
        int areaLevel,
        IReadOnlyList<EliteAffix>? eliteAffixes = null,
        bool abyssRoute = false)
    {
        ValidateAreaLevel(areaLevel);
        EliteAffix[] affixes = eliteAffixes?.Distinct().OrderBy(value => value).ToArray() ?? [];
        if (affixes.Length > 2)
        {
            throw new ArgumentException("An elite can have at most two affixes.", nameof(eliteAffixes));
        }

        int lifeMultiplier = checked(10_000 + (1_500 * (areaLevel - 1)));
        int damageMultiplier = checked(10_000 + (1_000 * (areaLevel - 1)));
        int defenseMultiplier = checked(10_000 + (1_200 * (areaLevel - 1)));
        int life = ScaleAtLeastOne(profile.Life, lifeMultiplier);
        int minimumDamage = ScaleAtLeastOne(profile.MinimumPhysicalDamage, damageMultiplier);
        int maximumDamage = ScaleAtLeastOne(profile.MaximumPhysicalDamage, damageMultiplier);
        int armor = ScaleNonNegative(profile.Armor, defenseMultiplier);
        int evasion = ScaleNonNegative(profile.Evasion, defenseMultiplier);
        int attackRate = profile.AttacksPerSecondMilli;

        if (abyssRoute)
        {
            life = ScaleAtLeastOne(life, 12_000);
            minimumDamage = ScaleAtLeastOne(minimumDamage, 11_500);
            maximumDamage = ScaleAtLeastOne(maximumDamage, 11_500);
        }

        foreach (EliteAffix affix in affixes)
        {
            switch (affix)
            {
                case EliteAffix.Massive:
                    life = ScaleAtLeastOne(life, 15_000);
                    break;
                case EliteAffix.Swift:
                    attackRate = ScaleAtLeastOne(attackRate, 12_500);
                    break;
                case EliteAffix.IronSkin:
                    armor = ScaleNonNegative(armor, 16_000);
                    break;
                case EliteAffix.Lacerating:
                case EliteAffix.CorpseExplosion:
                    break;
                case EliteAffix.ArcaneWard:
                    life = ScaleAtLeastOne(life, 12_500);
                    evasion = ScaleNonNegative(evasion + 8, 13_000);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eliteAffixes), affix, "Unknown elite affix.");
            }
        }

        return new ScaledEnemy(
            profile,
            areaLevel,
            life,
            minimumDamage,
            maximumDamage,
            armor,
            evasion,
            attackRate,
            affixes,
            abyssRoute);
    }

    public static IReadOnlyList<EliteAffix> RollEliteAffixes(Pcg32 random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var available = Enum.GetValues<EliteAffix>().ToList();
        var selected = new List<EliteAffix>(2);
        for (int index = 0; index < 2; index++)
        {
            int choice = (int)(random.NextUInt() % (uint)available.Count);
            selected.Add(available[choice]);
            available.RemoveAt(choice);
        }

        return selected.OrderBy(value => value).ToArray();
    }

    private static int ValidateAreaLevel(int areaLevel)
    {
        if (areaLevel is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(areaLevel), "Area level must be 1 through 20.");
        }

        return areaLevel;
    }

    private static int ScaleAtLeastOne(int value, int multiplierBasisPoints) =>
        Math.Max(1, checked((int)((long)value * multiplierBasisPoints / 10_000)));

    private static int ScaleNonNegative(int value, int multiplierBasisPoints) =>
        Math.Max(0, checked((int)((long)value * multiplierBasisPoints / 10_000)));
}

public enum BossPhase
{
    Opening,
    Summoning,
    Frenzy,
    Enraged,
}

public sealed record BossPhaseState(
    BossPhase Phase,
    int AttackSpeedMoreBasisPoints,
    int DamageMoreBasisPoints,
    bool SummonsWorkers,
    bool CreatesHazardZone);

public static class AbyssWardenRules
{
    public static BossPhaseState DeterminePhase(int currentLife, int maximumLife, int elapsedTicks)
    {
        if (maximumLife <= 0 || currentLife < 0 || currentLife > maximumLife)
        {
            throw new ArgumentOutOfRangeException(nameof(currentLife), "Boss life values are invalid.");
        }

        if (elapsedTicks >= 90 * 20)
        {
            return new BossPhaseState(BossPhase.Enraged, 10_000, 20_000, false, true);
        }

        int lifeBasisPoints = checked((int)((long)currentLife * 10_000 / maximumLife));
        if (lifeBasisPoints < 3_500)
        {
            return new BossPhaseState(BossPhase.Frenzy, 13_000, 11_500, false, true);
        }

        if (lifeBasisPoints < 7_000)
        {
            return new BossPhaseState(BossPhase.Summoning, 10_000, 10_000, true, true);
        }

        return new BossPhaseState(BossPhase.Opening, 10_000, 10_000, false, false);
    }
}

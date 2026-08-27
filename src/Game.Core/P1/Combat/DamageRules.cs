using GameForWork.Core.Simulation;

namespace GameForWork.Core.P1.Combat;

public sealed record WeaponProfile(
    string StableId,
    int MinimumPhysicalDamage,
    int MaximumPhysicalDamage,
    int AttacksPerSecondMilli,
    int CriticalChanceBasisPoints);

public static class P1Weapons
{
    public static readonly WeaponProfile RustedGreatsword = new("core.weapon.rusted_greatsword", 8, 12, 1_200, 500);
    public static readonly WeaponProfile HeavyBattleaxe = new("core.weapon.heavy_battleaxe", 12, 18, 900, 500);
    public static readonly WeaponProfile PoleWarhammer = new("core.weapon.pole_warhammer", 10, 15, 1_000, 600);
}

public sealed record DamageRequest(
    WeaponProfile Weapon,
    int AddedMinimumPhysicalDamage = 0,
    int AddedMaximumPhysicalDamage = 0,
    int IncreasedDamageBasisPoints = 0,
    IReadOnlyList<int>? MoreDamageMultipliersBasisPoints = null,
    int CriticalChanceBasisPoints = 500,
    int CriticalMultiplierBasisPoints = 15_000,
    int TargetArmor = 0,
    int TargetEvasion = 0,
    int Accuracy = 0,
    bool IsSpell = false,
    int BleedChanceBasisPoints = 0,
    int BleedTotalDamageBasisPoints = 7_000,
    int BleedDurationTicks = 100);

public sealed record DamageResult(
    bool Hit,
    bool Critical,
    int HitRollBasisPoints,
    int CriticalRollBasisPoints,
    int WeaponRoll,
    int PreMitigationPhysicalDamage,
    int ArmorReductionBasisPoints,
    int FinalPhysicalDamage,
    bool AppliedBleed,
    int BleedRollBasisPoints,
    int BleedTotalDamage,
    int BleedDurationTicks,
    CalculatedValue HitChance,
    CalculatedValue DamageTrace);

public static class DamageRules
{
    public static CalculatedValue HitChance(int accuracy, int targetEvasion, bool isSpell)
    {
        if (isSpell)
        {
            return CalculatedValue.Single("法术命中率", "法术默认必中", 10_000);
        }

        int denominator = checked(accuracy + targetEvasion);
        int raw = denominator == 0 ? 9_500 : checked((int)((long)accuracy * 10_000 / denominator));
        int clamped = Math.Clamp(raw, 500, 9_500);
        var trace = new FormulaTraceBuilder();
        trace.Add("原始命中率", $"{accuracy} × 10000 / ({accuracy} + {targetEvasion})", raw);
        trace.Add("命中率上下限", $"clamp({raw}, 500, 9500)", clamped);
        return trace.Build(clamped);
    }

    public static CalculatedValue ArmorReduction(int armor, int physicalHitDamage)
    {
        if (physicalHitDamage <= 0)
        {
            return CalculatedValue.Single("物理减伤率", "伤害为 0", 0);
        }

        int denominator = checked(armor + (5 * physicalHitDamage));
        int raw = denominator == 0 ? 0 : checked((int)((long)armor * 10_000 / denominator));
        int clamped = Math.Clamp(raw, 0, 9_000);
        var trace = new FormulaTraceBuilder();
        trace.Add("原始物理减伤率", $"{armor} × 10000 / ({armor} + 5 × {physicalHitDamage})", raw);
        trace.Add("护甲减伤上限", $"min({raw}, 9000)", clamped);
        return trace.Build(clamped);
    }

    public static DamageResult Resolve(DamageRequest request, Pcg32 random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        ValidateRequest(request);

        CalculatedValue hitChance = HitChance(request.Accuracy, request.TargetEvasion, request.IsSpell);
        int hitRoll = random.NextBasisPoints();
        if (hitRoll >= hitChance.Value)
        {
            return new DamageResult(
                false, false, hitRoll, -1, 0, 0, 0, 0, false, -1, 0,
                request.BleedDurationTicks, hitChance,
                CalculatedValue.Single("最终伤害", "命中失败", 0));
        }

        int minimum = checked(request.Weapon.MinimumPhysicalDamage + request.AddedMinimumPhysicalDamage);
        int maximum = checked(request.Weapon.MaximumPhysicalDamage + request.AddedMaximumPhysicalDamage);
        int weaponRoll = minimum + (int)(random.NextUInt() % checked((uint)(maximum - minimum + 1)));
        var trace = new FormulaTraceBuilder();
        trace.Add("武器与附加物理伤害", $"roll({minimum}, {maximum})", weaponRoll);

        int damage = checked((int)((long)weaponRoll * (10_000 + request.IncreasedDamageBasisPoints) / 10_000));
        trace.Add("伤害增加", $"{weaponRoll} × (10000 + {request.IncreasedDamageBasisPoints}) / 10000", damage);
        foreach (int multiplier in request.MoreDamageMultipliersBasisPoints ?? Array.Empty<int>())
        {
            damage = checked((int)((long)damage * multiplier / 10_000));
            trace.Add("伤害总增/总降", $"previous × {multiplier} / 10000", damage);
        }

        int criticalRoll = random.NextBasisPoints();
        bool critical = criticalRoll < Math.Clamp(request.CriticalChanceBasisPoints, 0, 10_000);
        if (critical)
        {
            damage = checked((int)((long)damage * request.CriticalMultiplierBasisPoints / 10_000));
            trace.Add("暴击", $"previous × {request.CriticalMultiplierBasisPoints} / 10000", damage);
        }

        int preMitigation = damage;
        CalculatedValue reduction = ArmorReduction(request.TargetArmor, preMitigation);
        int finalDamage = Math.Max(1, checked((int)((long)preMitigation * (10_000 - reduction.Value) / 10_000)));
        trace.Add("护甲缓解", $"{preMitigation} × (10000 - {reduction.Value}) / 10000", finalDamage);

        int bleedRoll = random.NextBasisPoints();
        bool appliedBleed = bleedRoll < Math.Clamp(request.BleedChanceBasisPoints, 0, 10_000);
        int bleedTotal = appliedBleed
            ? checked((int)((long)preMitigation * request.BleedTotalDamageBasisPoints / 10_000))
            : 0;
        if (appliedBleed)
        {
            trace.Add("流血总伤害", $"{preMitigation} × {request.BleedTotalDamageBasisPoints} / 10000", bleedTotal);
        }

        return new DamageResult(
            true,
            critical,
            hitRoll,
            criticalRoll,
            weaponRoll,
            preMitigation,
            reduction.Value,
            finalDamage,
            appliedBleed,
            bleedRoll,
            bleedTotal,
            request.BleedDurationTicks,
            hitChance,
            trace.Build(finalDamage));
    }

    private static void ValidateRequest(DamageRequest request)
    {
        if (request.Weapon.MinimumPhysicalDamage < 0 ||
            request.Weapon.MaximumPhysicalDamage < request.Weapon.MinimumPhysicalDamage ||
            request.AddedMaximumPhysicalDamage < request.AddedMinimumPhysicalDamage ||
            request.CriticalMultiplierBasisPoints < 10_000 ||
            request.BleedDurationTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Damage request contains invalid values.");
        }
    }
}

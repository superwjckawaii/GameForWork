using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Core.Builds;

public readonly record struct MasteryDamageContext(PassiveModifiers Profile, bool Hit = true,
    int TargetLife = 1, int TargetMaximumLife = 1);

public static class MasteryDamageRules
{
    public static int RollPhysical(int minimum, int maximum, GameForWork.Core.Simulation.Pcg32 random, bool lucky)
    {
        uint span = (uint)Math.Max(1L, (long)maximum - minimum + 1);
        int first = minimum + (int)(random.NextUInt() % span);
        return lucky ? Math.Max(first, minimum + (int)(random.NextUInt() % span)) : first;
    }

    public static int ExpectedPhysicalRoll(int minimum, int maximum, bool lucky)
    {
        decimal span = Math.Max(1L, (long)maximum - minimum + 1);
        decimal average = lucky ? minimum + (span - 1) * (4 * span + 1) / (6 * span) : ((decimal)minimum + maximum) / 2;
        return (int)Math.Clamp(average, 0, int.MaxValue);
    }

    public static void Configure(PassiveModifiers profile, ICollection<Conversion> conversions, ICollection<ExtraDamage> extras)
    {
        if (MasteryRuntime.Has(profile, "物理", 2))
            foreach (var element in new[] { DamageType.Fire, DamageType.Cold, DamageType.Lightning })
                extras.Add(new(DamageType.Physical, element, 1_000, $"mastery.physical.extra.{element}"));
        if (MasteryRuntime.Has(profile, "虚空", 1)) conversions.Add(new(DamageType.Physical, DamageType.Void, 5_000, "mastery.physical_to_void"));
        if (MasteryRuntime.Has(profile, "虚空", 2))
        {
            conversions.Add(new(DamageType.Fire, DamageType.Void, 5_000, "mastery.fire_to_void"));
            extras.Add(new(DamageType.Fire, DamageType.Void, 2_000, "mastery.fire_extra_void"));
        }
    }

    public static bool Allows(PassiveModifiers profile, DamageType type) =>
        (!MasteryRuntime.Has(profile, "物理", 0) || type == DamageType.Physical) &&
        (!MasteryRuntime.Has(profile, "虚空", 0) || type is DamageType.Physical or DamageType.Void);

    private static int TypeMultiplier(PassiveModifiers profile, DamageType type) => !Allows(profile, type) ? 0 :
        type == DamageType.Physical && MasteryRuntime.Has(profile, "物理", 0) ? 16_000 :
        type == DamageType.Void && MasteryRuntime.Has(profile, "虚空", 0) ? 15_000 : 10_000;

    public static int BranchMultiplier(MasteryDamageContext context, DamageBranch branch)
    {
        var profile = context.Profile;
        int result = TypeMultiplier(profile, branch.CurrentType);
        if (!context.Hit || result == 0) return result;
        bool physicalSource = branch.History.Contains(DamageType.Physical);
        if (physicalSource && !branch.IsExtra && branch.FullyConvertedPhysical && MasteryRuntime.Has(profile, "物理", 1)) result = Scale(result, 14_000);
        if (physicalSource && MasteryRuntime.Has(profile, "物理", 3)) result = Scale(result, 8_000);
        if (physicalSource && MasteryRuntime.Has(profile, "物理", 6) && context.TargetLife * 100L < context.TargetMaximumLife * 35L) result = Scale(result, 16_000);
        if (branch.CurrentType == DamageType.Void && MasteryRuntime.Has(profile, "虚空", 3)) result = Scale(result, 8_000);
        return result;
    }

    public static int AilmentMultiplier(PassiveModifiers profile, DamageBranch source, DamageType output, bool poison)
    {
        int result = TypeMultiplier(profile, output);
        if (source.History.Contains(DamageType.Physical) && MasteryRuntime.Has(profile, "物理", 3)) result = Scale(result, 15_000);
        if (poison && source.CurrentType == DamageType.Void && MasteryRuntime.Has(profile, "虚空", 3)) result = Scale(result, 15_000);
        return result;
    }

    private static int Scale(int value, int multiplier) => CombatRules.ApplyMore(value, [multiplier]);
}

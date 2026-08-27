namespace GameForWork.Core.P1.Combat;

[Flags]
public enum SkillTag
{
    None = 0,
    Attack = 1 << 0,
    Melee = 1 << 1,
    Area = 1 << 2,
    Physical = 1 << 3,
    WarCry = 1 << 4,
    Buff = 1 << 5,
}

[Flags]
public enum SkillSupport
{
    None = 0,
    IncreasedArea = 1 << 0,
    AttackSpeed = 1 << 1,
    Bleed = 1 << 2,
    LifeCost = 1 << 3,
}

public static class P1SkillIds
{
    public const string HeavyStrike = "core.skill.heavy_strike";
    public const string WarCry = "core.skill.war_cry";
}

public sealed record SkillConfiguration(string SkillId, SkillSupport Supports);

public sealed record SkillDefinition(
    string StableId,
    SkillTag Tags,
    int BaseManaCost,
    int RangeRaw,
    int CastTimeTicks,
    int CooldownTicks);

public static class P1Skills
{
    public static readonly SkillDefinition HeavyStrike = new(
        P1SkillIds.HeavyStrike,
        SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical,
        BaseManaCost: 8,
        RangeRaw: 1_500,
        CastTimeTicks: 0,
        CooldownTicks: 0);

    public static readonly SkillDefinition WarCry = new(
        P1SkillIds.WarCry,
        SkillTag.WarCry | SkillTag.Buff | SkillTag.Area,
        BaseManaCost: 12,
        RangeRaw: 6_000,
        CastTimeTicks: 10,
        CooldownTicks: 120);
}

public sealed record SkillUseProfile(
    string SkillId,
    int ManaCost,
    int LifeCost,
    int RangeRaw,
    int AttackIntervalTicks,
    int CastTimeTicks,
    int CooldownTicks,
    int BleedChanceBasisPoints,
    IReadOnlyList<int> MoreDamageMultipliersBasisPoints,
    int IncreasedAttackSpeedBasisPoints);

public sealed class WarCryState
{
    public int CooldownRemainingTicks { get; private set; }
    public int EmpoweredHeavyStrikes { get; private set; }
    public int ExpireTick { get; private set; } = -1;
    public bool EchoNotableAllocated { get; set; }

    public bool TryActivate(ResourceState resources, int tick)
    {
        if (CooldownRemainingTicks > 0 || !resources.TryPayMana(P1Skills.WarCry.BaseManaCost))
        {
            return false;
        }

        CooldownRemainingTicks = P1Skills.WarCry.CooldownTicks;
        EmpoweredHeavyStrikes = EchoNotableAllocated ? 4 : 3;
        ExpireTick = tick + 160;
        return true;
    }

    public int ConsumeHeavyStrikeMultiplier(int tick)
    {
        if (tick > ExpireTick)
        {
            EmpoweredHeavyStrikes = 0;
        }

        if (EmpoweredHeavyStrikes <= 0)
        {
            return 10_000;
        }

        EmpoweredHeavyStrikes--;
        return EchoNotableAllocated ? 12_000 : 12_500;
    }

    public void AdvanceTick()
    {
        CooldownRemainingTicks = Math.Max(0, CooldownRemainingTicks - 1);
    }
}

public static class SkillRules
{
    public static SkillUseProfile BuildHeavyStrike(
        SkillConfiguration configuration,
        WeaponProfile weapon,
        int maximumLife,
        int additionalIncreasedAttackSpeedBasisPoints = 0)
    {
        if (configuration.SkillId != P1SkillIds.HeavyStrike)
        {
            throw new ArgumentException("Configuration is not Heavy Strike.", nameof(configuration));
        }

        int range = P1Skills.HeavyStrike.RangeRaw;
        int manaCost = P1Skills.HeavyStrike.BaseManaCost;
        int lifeCost = 0;
        int bleedChance = 0;
        int increasedAttackSpeed = additionalIncreasedAttackSpeedBasisPoints;
        var moreMultipliers = new List<int> { 14_000 };

        if (configuration.Supports.HasFlag(SkillSupport.IncreasedArea))
        {
            range = checked(range * 13_500 / 10_000);
            moreMultipliers.Add(9_000);
        }

        if (configuration.Supports.HasFlag(SkillSupport.AttackSpeed))
        {
            increasedAttackSpeed += 2_500;
        }

        if (configuration.Supports.HasFlag(SkillSupport.Bleed))
        {
            bleedChance += 6_000;
        }

        if (configuration.Supports.HasFlag(SkillSupport.LifeCost))
        {
            manaCost = 0;
            lifeCost = Math.Max(1, checked(maximumLife * 800 / 10_000));
            moreMultipliers.Add(13_000);
        }

        int adjustedRateMilli = checked((int)((long)weapon.AttacksPerSecondMilli * (10_000 + increasedAttackSpeed) / 10_000));
        int attackIntervalTicks = Math.Max(1, DivideRoundUp(20_000, adjustedRateMilli));
        return new SkillUseProfile(
            configuration.SkillId,
            manaCost,
            lifeCost,
            range,
            attackIntervalTicks,
            0,
            0,
            bleedChance,
            moreMultipliers,
            increasedAttackSpeed);
    }

    public static SkillUseProfile BuildWarCry() => new(
        P1SkillIds.WarCry,
        ManaCost: P1Skills.WarCry.BaseManaCost,
        LifeCost: 0,
        RangeRaw: P1Skills.WarCry.RangeRaw,
        AttackIntervalTicks: 0,
        CastTimeTicks: P1Skills.WarCry.CastTimeTicks,
        CooldownTicks: P1Skills.WarCry.CooldownTicks,
        BleedChanceBasisPoints: 0,
        MoreDamageMultipliersBasisPoints: Array.Empty<int>(),
        IncreasedAttackSpeedBasisPoints: 0);

    public static bool TryPaySkillCost(ResourceState resources, SkillUseProfile profile)
    {
        if (profile.LifeCost > 0)
        {
            return resources.TryPayLifeCost(profile.LifeCost);
        }

        return resources.TryPayMana(profile.ManaCost);
    }

    private static int DivideRoundUp(int numerator, int denominator) =>
        checked((numerator + denominator - 1) / denominator);
}

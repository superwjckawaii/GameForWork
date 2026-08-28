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
    Projectile = 1 << 6,
    Chaining = 1 << 7,
    Bleed = 1 << 8,
    Movement = 1 << 9,
    Reservation = 1 << 10,
}

[Flags]
public enum SkillSupport
{
    None = 0,
    IncreasedArea = 1 << 0,
    AttackSpeed = 1 << 1,
    Bleed = 1 << 2,
    LifeCost = 1 << 3,
    Chain = 1 << 4,
    Brutality = 1 << 5,
    MultipleProjectiles = 1 << 6,
    FasterProjectiles = 1 << 7,
    UrgentWarCry = 1 << 8,
    LifeLeech = 1 << 9,
    Execution = 1 << 10,
}

public static class P1SkillIds
{
    public const string HeavyStrike = "core.skill.heavy_strike";
    public const string WarCry = "core.skill.war_cry";
    public const string EarthCleave = "core.skill.earth_cleave";
    public const string SpiritBlade = "core.skill.spirit_blade";
    public const string SeismicCharge = "core.skill.seismic_charge";
    public const string BloodTideSpin = "core.skill.blood_tide_spin";
    public const string IronOathBanner = "core.skill.iron_oath_banner";
}

public sealed record SkillAiRule(
    bool MatchAll = true,
    int MinimumLifeBasisPoints = 0,
    int MinimumManaBasisPoints = 0,
    int MinimumEnemyCount = 1,
    string EnemyRarity = "任意",
    int MinimumDistanceRaw = 0,
    int MaximumDistanceRaw = 30_000,
    int DangerThreshold = 0,
    bool BossOnly = false,
    bool Engage = false,
    bool Pursue = false,
    bool EscapeDanger = false);

public sealed record SkillConfiguration(
    string SkillId,
    SkillSupport Supports,
    int Priority = 50,
    SkillAiRule? AiRule = null,
    int Level = 1,
    string StoneInstanceId = "");

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

    public static readonly SkillDefinition EarthCleave = new(
        P1SkillIds.EarthCleave,
        SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical,
        BaseManaCost: 10,
        RangeRaw: 2_800,
        CastTimeTicks: 4,
        CooldownTicks: 24);

    public static readonly SkillDefinition SpiritBlade = new(
        P1SkillIds.SpiritBlade,
        SkillTag.Attack | SkillTag.Projectile | SkillTag.Chaining | SkillTag.Physical,
        BaseManaCost: 9,
        RangeRaw: 8_000,
        CastTimeTicks: 3,
        CooldownTicks: 20);

    public static readonly SkillDefinition SeismicCharge = new(
        P1SkillIds.SeismicCharge,
        SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical | SkillTag.Movement,
        BaseManaCost: 14, RangeRaw: 5_000, CastTimeTicks: 5, CooldownTicks: 80);

    public static readonly SkillDefinition BloodTideSpin = new(
        P1SkillIds.BloodTideSpin,
        SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical | SkillTag.Bleed,
        BaseManaCost: 12, RangeRaw: 2_500, CastTimeTicks: 5, CooldownTicks: 18);

    public static readonly SkillDefinition IronOathBanner = new(
        P1SkillIds.IronOathBanner,
        SkillTag.Buff | SkillTag.Area | SkillTag.Reservation,
        BaseManaCost: 0, RangeRaw: 8_000, CastTimeTicks: 4, CooldownTicks: 0);

    public static SkillDefinition Get(string stableId) => stableId switch
    {
        P1SkillIds.HeavyStrike => HeavyStrike,
        P1SkillIds.WarCry => WarCry,
        P1SkillIds.EarthCleave => EarthCleave,
        P1SkillIds.SpiritBlade => SpiritBlade,
        P1SkillIds.SeismicCharge => SeismicCharge,
        P1SkillIds.BloodTideSpin => BloodTideSpin,
        P1SkillIds.IronOathBanner => IronOathBanner,
        _ => throw new KeyNotFoundException($"Unknown skill: {stableId}"),
    };
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
    public bool IsReady => CooldownRemainingTicks <= 0;
    public int ManaCost { get; set; } = P1Skills.WarCry.BaseManaCost;
    public int CooldownDurationTicks { get; set; } = P1Skills.WarCry.CooldownTicks;
    public int EffectMultiplierBasisPoints { get; set; } = 10_000;

    public bool TryActivate(ResourceState resources, int tick)
    {
        if (CooldownRemainingTicks > 0 || !resources.TryPayMana(ManaCost))
        {
            return false;
        }

        CooldownRemainingTicks = CooldownDurationTicks;
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
        int bonus = EchoNotableAllocated ? 2_000 : 2_500;
        return checked(10_000 + bonus * EffectMultiplierBasisPoints / 10_000);
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
        if (configuration.Level > 1)
        {
            moreMultipliers.Add(checked(10_000 + (Math.Clamp(configuration.Level, 1, 20) - 1) * 250));
        }

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

using GameForWork.Core.P17;
using GameForWork.Core.P24;
using GameForWork.Core.P30;

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
    Spell = 1 << 11,
    Fire = 1 << 12,
    Cold = 1 << 13,
    Lightning = 1 << 14,
    Void = 1 << 15,
    Strike = 1 << 16,
    Slam = 1 << 17,
    Duration = 1 << 18,
    Channelling = 1 << 19,
    Aura = 1 << 20,
    Guard = 1 << 21,
    Counter = 1 << 22,
    Trigger = 1 << 23,
    Stun = 1 << 24,
    ArmorBreak = 1 << 25,
    Returning = 1 << 26,
    Elemental = Fire | Cold | Lightning,
}

[Flags]
public enum SkillSupport : ulong
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
    SpellEcho = 1 << 11,
    ElementalFocus = 1 << 12,
    AddedFire = 1 << 13,
    AddedCold = 1 << 14,
    AddedLightning = 1 << 15,
    CriticalStrikes = 1 << 16,
    ConcentratedEffect = 1 << 17,
    HeavyMomentum = 1UL << 18,
    TripleImpact = 1UL << 19,
    TremorField = 1UL << 20,
    Shockwave = 1UL << 21,
    CloseCombat = 1UL << 22,
    ArmorShatter = 1UL << 23,
    ArmorPierce = 1UL << 24,
    Suppression = 1UL << 25,
    StunSpread = 1UL << 26,
    DeepWound = 1UL << 27,
    SwiftBleed = 1UL << 28,
    BleedSpread = 1UL << 29,
    Cruelty = 1UL << 30,
    Bloodlust = 1UL << 31,
    Trauma = 1UL << 32,
    Fortification = 1UL << 33,
    Vengeance = 1UL << 34,
    BlockTrigger = 1UL << 35,
    WarCryPotency = 1UL << 36,
    WarCryEcho = 1UL << 37,
    BannerPotency = 1UL << 38,
    Pierce = 1UL << 39,
    Fork = 1UL << 40,
    Return = 1UL << 41,
    FasterCasting = 1UL << 42,
    PhysicalToLightning = 1UL << 43,
    LightningToCold = 1UL << 44,
    ColdToFire = 1UL << 45,
    FireToVoid = 1UL << 46,
    CastWhenDamaged = 1UL << 47,
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
    public const string AshJavelin = "core.skill.ash_javelin";
    public const string EmberNova = "core.skill.ember_nova";
    public const string StormBrand = "core.skill.storm_brand";
    public const string ArmorBreakStrike = "core.skill.armor_break_strike";
    public const string ExecutionCleave = "core.skill.execution_cleave";
    public const string MountainSlam = "core.skill.mountain_slam";
    public const string AftershockMaul = "core.skill.aftershock_maul";
    public const string VeinRend = "core.skill.vein_rend";
    public const string BloodBurst = "core.skill.blood_burst";
    public const string BloodMarkAxe = "core.skill.blood_mark_axe";
    public const string IronHook = "core.skill.iron_hook";
    public const string BreakerCry = "core.skill.breaker_cry";
    public const string DefiantCry = "core.skill.defiant_cry";
    public const string IronGuard = "core.skill.iron_guard";
    public const string VengefulCounter = "core.skill.vengeful_counter";
    public const string BreachBanner = "core.skill.breach_banner";
    public const string LeapQuake = "core.skill.leap_quake";
    public const string FrostShard = "core.skill.frost_shard";
    public const string ChainLightning = "core.skill.chain_lightning";
    public const string FlameStep = "core.skill.flame_step";
    public const string VoidDecayField = "core.skill.void_decay_field";
    public const string PrismaticGuard = "core.skill.prismatic_guard";
    public const string ElementalResonance = "core.skill.elemental_resonance";
}

public enum SkillTargetPolicy
{
    AllEnemies,
    EliteAndBoss,
    BossOnly,
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
    bool EscapeDanger = false,
    SkillTargetPolicy TargetPolicy = SkillTargetPolicy.AllEnemies);

public sealed record SkillConfiguration(
    string SkillId,
    SkillSupport Supports,
    int Priority = 50,
    SkillAiRule? AiRule = null,
    int Level = 1,
    string StoneInstanceId = "",
    IReadOnlyList<P24SupportMechanic>? P24Supports = null, int Quality = 0,
    IReadOnlyList<string>? P30Supports = null,
    IReadOnlyList<P30LinkedSupport>? P30SupportLinks = null)
{
    public IReadOnlyList<P24SupportMechanic> ExtendedSupports => P24Supports ?? Array.Empty<P24SupportMechanic>();
    public IReadOnlyList<string> ExtendedP30Supports => P30Supports ?? Array.Empty<string>();
    public IReadOnlyList<P30LinkedSupport> ExtendedP30SupportLinks => P30SupportLinks ?? Array.Empty<P30LinkedSupport>();
}

public sealed record SkillDefinition(
    string StableId,
    SkillTag Tags,
    int BaseManaCost,
    int RangeRaw,
    int CastTimeTicks,
    int CooldownTicks);

public static class P1Skills
{
    private static readonly IReadOnlyDictionary<string, SkillDefinition> Catalog = P30SkillCatalog.Active
        .Select(item => new SkillDefinition(item.Combat.SkillId, item.Combat.Tags, item.LevelOneMana,
            item.Combat.RangeRaw, item.Combat.CastTimeTicks, item.Combat.CooldownTicks))
        .ToDictionary(item => item.StableId, StringComparer.Ordinal);

    public static SkillDefinition HeavyStrike => Get(P1SkillIds.HeavyStrike);
    public static SkillDefinition WarCry => Get(P1SkillIds.WarCry);
    public static SkillDefinition EarthCleave => Get(P1SkillIds.EarthCleave);
    public static SkillDefinition SpiritBlade => Get(P1SkillIds.SpiritBlade);
    public static SkillDefinition SeismicCharge => Get(P1SkillIds.SeismicCharge);
    public static SkillDefinition BloodTideSpin => Get(P1SkillIds.BloodTideSpin);
    public static SkillDefinition IronOathBanner => Get(P1SkillIds.IronOathBanner);
    public static SkillDefinition AshJavelin => Get(P1SkillIds.AshJavelin);
    public static SkillDefinition EmberNova => Get(P1SkillIds.EmberNova);
    public static SkillDefinition StormBrand => Get(P1SkillIds.StormBrand);
    public static IReadOnlyList<SkillDefinition> All { get; } = Catalog.Values.OrderBy(item => item.StableId).ToArray();

    public static SkillDefinition Get(string stableId) => Catalog.TryGetValue(stableId, out SkillDefinition? definition)
        ? definition : throw new KeyNotFoundException($"Unknown skill: {stableId}");
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
        int manaCost = P18.P18AscendancyRules.AttackManaCost(P1Skills.HeavyStrike.BaseManaCost, P1Skills.HeavyStrike.Tags);
        int lifeCost = 0;
        int bleedChance = 0;
        int increasedAttackSpeed = additionalIncreasedAttackSpeedBasisPoints;
        var moreMultipliers = new List<int> { 14_000 };
        if (configuration.Level > 1)
        {
            moreMultipliers.Add(checked(10_000 + (Math.Clamp(configuration.Level, 1, 21) - 1) * 250));
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

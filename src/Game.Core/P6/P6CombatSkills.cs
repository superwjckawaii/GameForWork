using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P17;
using GameForWork.Core.P24;
using GameForWork.Core.P30;

namespace GameForWork.Core.P6;

public sealed record P6ResolvedSkill(
    string SkillId,
    int ManaCost,
    int LifeCost,
    int RangeRaw,
    int CastTimeTicks,
    int CooldownTicks,
    int DamageMultiplierBasisPoints,
    int BleedChanceBasisPoints,
    int ProjectileCount,
    int ProjectileSpeedRawPerSecond,
    int MaximumChains,
    int LifeLeechBasisPoints,
    int ExecuteThresholdBasisPoints,
    int ExecuteMultiplierBasisPoints,
    int NonExecuteMultiplierBasisPoints,
    P17DamageType DamageType,
    P17SkillRole Role,
    P17SkillShape Shape,
    int BaseDamageBasisPoints,
    P17Ailment Ailment,
    int AilmentChanceBasisPoints,
    int PierceCount,
    int ForkCount,
    bool Returns,
    bool RequiresShield,
    int ResourceMultiplierBasisPoints = 10_000,
    bool SingleTargetOnly = false,
    bool ExplodesOnKill = false,
    bool OverloadRepeatsEveryThirdUse = false,
    int TemperanceLevelPerLayer = 0,
    int TemperanceQualityPerLayer = 0);

public static class P6CombatSkillRules
{
    public static P6ResolvedSkill Resolve(SkillConfiguration configuration, int maximumLife, P205PassiveModifiers? passive = null)
    {
        SkillDefinition definition = P1Skills.Get(configuration.SkillId);
        P30ActiveSkillDefinition p30Active = P30SkillCatalog.ActiveForSkill(configuration.SkillId);
        P30SupportRuntimeProfile p30Supports = configuration.ExtendedP30SupportLinks.Count > 0
            ? P30SkillCatalog.ResolveSupports(p30Active, configuration.ExtendedP30SupportLinks)
            : P30SkillCatalog.ResolveSupports(p30Active, configuration.ExtendedP30Supports,
                configuration.Level, configuration.Quality);
        P17ActiveSkillDefinition active = p30Active.Combat;
        int mana = P18.P18AscendancyRules.AttackManaCost(p30Active.ManaAt(configuration.Level), definition.Tags);
        int life = 0;
        int range = definition.RangeRaw;
        int cooldown = definition.CooldownTicks;
        int castTime = definition.CastTimeTicks;
        int damage = 10_000;
        int bleed = 0;
        int projectiles = 1;
        int projectileSpeed = 10_000;
        int chains = configuration.Supports.HasFlag(SkillSupport.Chain) ? 3 : 0;
        int leech = 0;
        int executeThreshold = 0;
        int execute = 10_000;
        int nonExecute = 10_000;
        int baseDamage = p30Active.DamageAt(configuration.Level);
        int ailmentChance = active.AilmentChanceBasisPoints;
        int pierce = 0;
        int fork = 0;
        bool returns = active.Tags.HasFlag(SkillTag.Returning);
        if (configuration.Supports.HasFlag(SkillSupport.IncreasedArea))
        {
            range = checked(range * (10_000 + LegacyValue(configuration, SkillSupport.IncreasedArea) * 100) / 10_000);
            damage = checked(damage * 9_000 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Bleed))
            bleed += Math.Min(10_000, LegacyValue(configuration, SkillSupport.Bleed) * 100);
        if (configuration.Supports.HasFlag(SkillSupport.LifeCost))
        {
            life = Math.Max(1, (mana * 15 + 9) / 10);
            mana = 0;
            damage = More(damage, LegacyValue(configuration, SkillSupport.LifeCost));
        }
        if (configuration.Supports.HasFlag(SkillSupport.Brutality))
            damage = More(damage, LegacyValue(configuration, SkillSupport.Brutality));
        if (configuration.Supports.HasFlag(SkillSupport.MultipleProjectiles))
        {
            projectiles += 2;
            int less = P30SkillCatalog.Interpolate(25, 15, configuration.Level, false);
            damage = checked(damage * (100 - less) / 100);
            projectileSpeed = checked(projectileSpeed * (10_000 + LegacyValue(configuration, SkillSupport.MultipleProjectiles) * 100) / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.FasterProjectiles))
        {
            int value = LegacyValue(configuration, SkillSupport.FasterProjectiles);
            projectileSpeed = checked(projectileSpeed * (10_000 + value * 100) / 10_000);
            int rangeIncrease = P30SkillCatalog.Interpolate(20, 35, configuration.Level, false);
            range = checked(range * (10_000 + rangeIncrease * 100) / 10_000);
            damage = More(damage, rangeIncrease);
        }
        if (configuration.Supports.HasFlag(SkillSupport.UrgentWarCry))
            cooldown = Math.Max(1, cooldown * 10_000 / (10_000 + LegacyValue(configuration, SkillSupport.UrgentWarCry) * 100));
        if (configuration.Supports.HasFlag(SkillSupport.LifeLeech))
        {
            leech = LegacyValue(configuration, SkillSupport.LifeLeech);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Execution))
        {
            executeThreshold = 2_000;
            execute = 10_000 + LegacyValue(configuration, SkillSupport.Execution) * 100;
            nonExecute = 9_000;
        }
        if (configuration.Supports.HasFlag(SkillSupport.SpellEcho) && definition.Tags.HasFlag(SkillTag.Spell))
        {
            projectiles += 1;
            int less = LegacyValue(configuration, SkillSupport.SpellEcho);
            damage = checked(damage * (100 - less) / 100);
        }
        if (configuration.Supports.HasFlag(SkillSupport.ElementalFocus) && (definition.Tags & SkillTag.Elemental) != 0)
            damage = More(damage, LegacyValue(configuration, SkillSupport.ElementalFocus));
        if (configuration.Supports.HasFlag(SkillSupport.AddedFire)) damage = checked(damage * 11_800 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.AddedCold)) damage = checked(damage * 11_500 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.AddedLightning)) damage = checked(damage * 11_700 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.CriticalStrikes)) damage = checked(damage * 11_200 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.ConcentratedEffect) && definition.Tags.HasFlag(SkillTag.Area))
        {
            range = checked(range * 7_500 / 10_000);
            damage = More(damage, LegacyValue(configuration, SkillSupport.ConcentratedEffect));
        }
        if (configuration.Supports.HasFlag(SkillSupport.AttackSpeed) && definition.Tags.HasFlag(SkillTag.Attack))
        {
            cooldown = Math.Max(1, cooldown * 10_000 / (10_000 + LegacyValue(configuration, SkillSupport.AttackSpeed) * 100));
        }
        if (configuration.Supports.HasFlag(SkillSupport.HeavyMomentum))
            damage = More(damage, LegacyValue(configuration, SkillSupport.HeavyMomentum));
        if (configuration.Supports.HasFlag(SkillSupport.TripleImpact))
            damage = More(damage, LegacyValue(configuration, SkillSupport.TripleImpact) / 3);
        if (configuration.Supports.HasFlag(SkillSupport.TremorField))
        {
            range = checked(range * 13_000 / 10_000);
            damage = checked(damage * 12_500 / 10_000);
            cooldown = Math.Max(1, cooldown * 11_500 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Shockwave)) damage = checked(damage * 11_000 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.CloseCombat)) damage = checked(damage * 12_000 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.ArmorShatter)) damage = checked(damage * 9_000 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.Suppression)) damage = checked(damage * 8_500 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.DeepWound))
        {
            damage = checked(damage * 9_000 / 10_000);
            bleed += 5_000;
        }
        if (configuration.Supports.HasFlag(SkillSupport.Vengeance)) damage = checked(damage * 14_000 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.BlockTrigger)) damage = checked(damage * 7_500 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.CastWhenDamaged)) damage = checked(damage * 7_000 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.FasterCasting) && definition.Tags.HasFlag(SkillTag.Spell))
        {
            castTime = Math.Max(1, castTime * 10_000 / 12_500);
            mana = checked((mana * 11_000 + 9_999) / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Pierce)) pierce += 2;
        if (configuration.Supports.HasFlag(SkillSupport.Fork)) fork += 2;
        if (configuration.Supports.HasFlag(SkillSupport.Return)) returns = true;
        P24SupportProfile p24 = P24SupportRules.Resolve(configuration.ExtendedSupports);
        damage = checked(damage * p24.DamageMultiplierBasisPoints / 10_000);
        range = checked(range * p24.RangeMultiplierBasisPoints / 10_000);
        castTime = Math.Max(1, checked(castTime * 10_000 / Math.Max(1, p24.CastSpeedBasisPoints)));
        cooldown = Math.Max(1, checked(cooldown * 10_000 / Math.Max(1, p24.CooldownRecoveryBasisPoints)));
        projectiles += p24.ProjectileCount;
        pierce += p24.PierceCount;
        chains += p24.ChainCount;
        damage = checked(damage * p30Supports.DamageMultiplierBasisPoints / 10_000);
        mana = checked((mana * p30Supports.ResourceMultiplierBasisPoints + 9_999) / 10_000);
        life = checked((life * p30Supports.ResourceMultiplierBasisPoints + 9_999) / 10_000);
        passive ??= P205PassiveModifiers.Empty;
        mana = Math.Max(0, checked(mana * Math.Max(0, 10_000 - passive.ReducedSkillCostBasisPoints) / 10_000));
        life = Math.Max(0, checked(life * Math.Max(0, 10_000 - passive.ReducedSkillCostBasisPoints) / 10_000));
        range = Math.Max(1, checked(range * (10_000 + passive.IncreasedSkillRangeBasisPoints) / 10_000));
        cooldown = Math.Max(1, checked(cooldown * 10_000 / Math.Max(1, 10_000 + passive.IncreasedCooldownRecoveryBasisPoints)));
        ailmentChance = Math.Clamp(ailmentChance + bleed, 0, 10_000);
        return new P6ResolvedSkill(configuration.SkillId, mana, life, range, castTime, cooldown,
            damage, bleed, projectiles, projectileSpeed, chains, leech, executeThreshold, execute, nonExecute,
            active.DamageType, active.Role, active.Shape, baseDamage, active.Ailment, ailmentChance,
            pierce, fork, returns, active.Capabilities.HasFlag(P17SkillCapability.RequiresShield),
            p30Supports.ResourceMultiplierBasisPoints, p30Supports.SingleTargetOnly, p30Supports.ExplodesOnKill,
            p30Supports.OverloadRepeatsEveryThirdUse, p30Supports.TemperanceLevelPerLayer,
            p30Supports.TemperanceQualityPerLayer);
    }

    public static bool TryPay(ResourceState resources, P6ResolvedSkill skill) => skill.LifeCost > 0
        ? resources.TryPayLifeCost(skill.LifeCost)
        : resources.TryPayMana(skill.ManaCost);

    public static int DamageMultiplier(P6ResolvedSkill skill, int life, int maximumLife)
    {
        int conditional = skill.ExecuteThresholdBasisPoints > 0 &&
                          (long)life * 10_000 < (long)maximumLife * skill.ExecuteThresholdBasisPoints
            ? skill.ExecuteMultiplierBasisPoints
            : skill.NonExecuteMultiplierBasisPoints;
        return SaturatingInt((long)skill.DamageMultiplierBasisPoints * conditional / 10_000);
    }

    public static int BaseDamage(P6ResolvedSkill skill, SkillTag tags, WeaponProfile weapon,
        int addedPhysicalDamage, int? weaponRoll = null)
    {
        if (!tags.HasFlag(SkillTag.Attack)) return Math.Max(1, skill.BaseDamageBasisPoints);
        int physical = weaponRoll ?? checked((weapon.MinimumPhysicalDamage + weapon.MaximumPhysicalDamage) / 2);
        return Math.Max(1, checked(physical + addedPhysicalDamage));
    }

    public static int ScaleOffensiveDamage(int rawDamage, P6ResolvedSkill skill,
        SkillConfiguration configuration, P1TeamBuild build, SkillTag tags,
        int targetLife, int targetMaximumLife, int actionMultiplierBasisPoints = 10_000,
        bool targetRareOrBoss = false, P17DamageType? damageType = null,
        int additionalIncreasedBasisPoints = 0)
    {
        P205PassiveModifiers passive = build.PassiveProfile ?? P205PassiveModifiers.Empty;
        long value = rawDamage;
        long increasedMultiplier = 10_000L + (tags.HasFlag(SkillTag.Attack) ? build.IncreasedDamageBasisPoints : 0) +
            passive.DamageFor(tags) + (long)configuration.Quality * 100 + additionalIncreasedBasisPoints;
        increasedMultiplier += build.CombatEquipment?.DamageIncrease(tags, damageType ?? skill.DamageType,
            skill.Role == P17SkillRole.DamageOverTime) ?? 0;
        if (tags.HasFlag(SkillTag.Attack)) increasedMultiplier -= build.CombatEquipment?.PhysicalIncreaseIncludedInAttack ?? 0;
        if (tags.HasFlag(SkillTag.Attack))
            increasedMultiplier += P18.P18AscendancyRules.IncreasedAttackDamageBasisPoints(
                build.Ascendancy ?? P18.P18CombatProfile.Empty, build.Sheet.Attributes.Physique);
        if (tags.HasFlag(SkillTag.Spell)) increasedMultiplier += build.IncreasedSpellDamageBasisPoints;
        value = Scale(value, increasedMultiplier);
        value = Scale(value, 10_000L + passive.MoreDamageBasisPoints);
        if (skill.Role == P17SkillRole.DamageOverTime)
            value = Scale(value, 10_000L + build.MoreDamageOverTimeBasisPoints);
        if (tags.HasFlag(SkillTag.Attack))
            value = Scale(value, 10_000L + build.MoreAttackDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Attack) && P30SkillCatalog.ActiveForSkill(skill.SkillId).Curve == P30SkillCurve.UnarmedAttack)
            value = Scale(value, 10_000L + (build.CombatEquipment?.UnarmedMoreDamageBasisPoints ?? 0));
        if (tags.HasFlag(SkillTag.Spell))
            value = Scale(value, 10_000L + build.MoreSpellDamageBasisPoints);
        bool elementalDamage = damageType is P17DamageType.Fire or P17DamageType.Cold or P17DamageType.Lightning ||
                               damageType is null && (tags & SkillTag.Elemental) != 0;
        bool voidDamage = damageType == P17DamageType.Void ||
                          damageType is null && tags.HasFlag(SkillTag.Void);
        if (elementalDamage)
            value = Scale(value, 10_000L + build.MoreElementalDamageBasisPoints);
        if (voidDamage)
            value = Scale(value, 10_000L + build.MoreVoidDamageBasisPoints);
        if (targetRareOrBoss)
            value = Scale(value, 10_000L + build.MoreRareBossDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Attack))
            value = Scale(value, skill.BaseDamageBasisPoints);
        value = Scale(value, DamageMultiplier(skill, targetLife, targetMaximumLife));
        value = Scale(value, P30MasteryRuntime.OffensiveMultiplier(passive, tags, build.Weapon,
            targetLife, targetMaximumLife, hasOffHand: build.HasOffHand));
        return SaturatingInt(Scale(value, actionMultiplierBasisPoints));
    }

    public static int ActionDelay(P1TeamBuild build, int baseTicks, SkillTag tags)
    {
        P205PassiveModifiers passive = build.PassiveProfile ?? P205PassiveModifiers.Empty;
        int masterySpeed = P30MasteryRuntime.ActionSpeedMultiplier(passive, tags, build.Weapon);
        int increasedSpeed = build.IncreasedActionSpeedBasisPoints;
        if (tags.HasFlag(SkillTag.Attack)) increasedSpeed = checked(increasedSpeed + build.IncreasedAttackSpeedBasisPoints);
        if (tags.HasFlag(SkillTag.Spell)) increasedSpeed = checked(increasedSpeed +
            (build.CombatEquipment?.Value(GameForWork.Core.P1.Items.ItemModifierKind.IncreasedCastSpeedBasisPoints) ?? 0));
        return Math.Max(1, checked((int)((long)Math.Max(1, baseTicks) * 10_000 * 10_000 /
            Math.Max(10_000_000, (long)(10_000 + increasedSpeed) * masterySpeed))));
    }

    public static int ActionFrequencyMilliPerSecond(P1TeamBuild build, int baseTicks, int cooldownTicks,
        SkillTag tags)
    {
        int actionDelay = ActionDelay(build, baseTicks, tags);
        if (!tags.HasFlag(SkillTag.Attack)) return checked(20_000 / actionDelay);

        P205PassiveModifiers passive = build.PassiveProfile ?? P205PassiveModifiers.Empty;
        int masterySpeed = P30MasteryRuntime.ActionSpeedMultiplier(passive, tags, build.Weapon);
        int increasedSpeed = checked(build.IncreasedActionSpeedBasisPoints + build.IncreasedAttackSpeedBasisPoints);
        int baseFrequency = baseTicks <= 1
            ? Math.Max(1, build.Weapon.AttacksPerSecondMilli)
            : Math.Max(1, 20_000 / baseTicks);
        int frequency = P30CombatRules.AttackFrequencyMilliPerSecond(baseFrequency, increasedSpeed,
            [masterySpeed]);
        if (cooldownTicks > 1) frequency = Math.Min(frequency, 20_000 / cooldownTicks);
        return frequency;
    }

    private static int LegacyValue(SkillConfiguration configuration, SkillSupport support) =>
        P30SkillCatalog.SupportFor(support).ValueAt(configuration.Level, configuration.Quality);

    private static int More(int basisPoints, int percent) => checked(basisPoints * (10_000 + percent * 100) / 10_000);

    private static long Scale(long value, long basisPoints)
    {
        if (value <= 0 || basisPoints <= 0) return 0;
        if (value > long.MaxValue / basisPoints) return long.MaxValue;
        return value * basisPoints / 10_000;
    }

    private static int SaturatingInt(long value) => (int)Math.Clamp(value, 0, int.MaxValue);
}

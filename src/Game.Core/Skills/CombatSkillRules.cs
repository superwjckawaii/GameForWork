using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Builds;

namespace GameForWork.Core.Skills;

public sealed record ResolvedSkill(
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
    SkillDamageType DamageType,
    SkillRole Role,
    SkillShape Shape,
    int BaseDamageBasisPoints,
    Ailment Ailment,
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

public static class CombatSkillRules
{
    public static ResolvedSkill Resolve(SkillConfiguration configuration, int maximumLife, PassiveModifiers? passive = null)
    {
        SkillDefinition definition = SkillDefinitions.Get(configuration.SkillId);
        ActiveSkillDefinition buildsActive = ActiveSkillCatalog.ActiveForSkill(configuration.SkillId);
        SupportRuntimeProfile buildsSupports = configuration.ExtendedBuildsSupportLinks.Count > 0
            ? ActiveSkillCatalog.ResolveSupports(buildsActive, configuration.ExtendedBuildsSupportLinks)
            : ActiveSkillCatalog.ResolveSupports(buildsActive, configuration.ExtendedBuildsSupports,
                configuration.Level, configuration.Quality);
        SkillCombatDefinition active = buildsActive.Combat;
        int mana = Ascendancies.WarriorAscendancyRules.AttackManaCost(buildsActive.ManaAt(configuration.Level), definition.Tags);
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
        int baseDamage = buildsActive.DamageAt(configuration.Level);
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
            int less = ActiveSkillCatalog.Interpolate(25, 15, configuration.Level, false);
            damage = checked(damage * (100 - less) / 100);
            projectileSpeed = checked(projectileSpeed * (10_000 + LegacyValue(configuration, SkillSupport.MultipleProjectiles) * 100) / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.FasterProjectiles))
        {
            int value = LegacyValue(configuration, SkillSupport.FasterProjectiles);
            projectileSpeed = checked(projectileSpeed * (10_000 + value * 100) / 10_000);
            int rangeIncrease = ActiveSkillCatalog.Interpolate(20, 35, configuration.Level, false);
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
            int less = LegacyValue(configuration, SkillSupport.SpellEcho);
            damage = checked(damage * (100 - less) / 100);
        }
        if (configuration.Supports.HasFlag(SkillSupport.ElementalFocus) && (definition.Tags & SkillTag.Elemental) != 0)
            damage = More(damage, LegacyValue(configuration, SkillSupport.ElementalFocus));
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
        SupportProfile archetypes = SupportRules.Resolve(configuration.ExtendedSupports);
        damage = checked(damage * archetypes.DamageMultiplierBasisPoints / 10_000);
        range = checked(range * archetypes.RangeMultiplierBasisPoints / 10_000);
        castTime = Math.Max(1, checked(castTime * 10_000 / Math.Max(1, archetypes.CastSpeedBasisPoints)));
        cooldown = Math.Max(1, checked(cooldown * 10_000 / Math.Max(1, archetypes.CooldownRecoveryBasisPoints)));
        projectiles += archetypes.ProjectileCount;
        pierce += archetypes.PierceCount;
        chains += archetypes.ChainCount;
        damage = checked(damage * buildsSupports.DamageMultiplierBasisPoints / 10_000);
        mana = checked((mana * buildsSupports.ResourceMultiplierBasisPoints + 9_999) / 10_000);
        life = checked((life * buildsSupports.ResourceMultiplierBasisPoints + 9_999) / 10_000);
        passive ??= PassiveModifiers.Empty;
        mana = Math.Max(0, checked(mana * Math.Max(0, 10_000 - passive.ReducedSkillCostBasisPoints) / 10_000));
        life = Math.Max(0, checked(life * Math.Max(0, 10_000 - passive.ReducedSkillCostBasisPoints) / 10_000));
        range = Math.Max(1, checked(range * (10_000 + passive.IncreasedSkillRangeBasisPoints) / 10_000));
        cooldown = Math.Max(1, checked(cooldown * 10_000 / Math.Max(1, 10_000 + passive.IncreasedCooldownRecoveryBasisPoints)));
        ailmentChance = Math.Clamp(ailmentChance + bleed, 0, 10_000);
        return new ResolvedSkill(configuration.SkillId, mana, life, range, castTime, cooldown,
            damage, bleed, projectiles, projectileSpeed, chains, leech, executeThreshold, execute, nonExecute,
            active.DamageType, active.Role, active.Shape, baseDamage, active.Ailment, ailmentChance,
            pierce, fork, returns, active.Capabilities.HasFlag(SkillCapability.RequiresShield),
            buildsSupports.ResourceMultiplierBasisPoints, buildsSupports.SingleTargetOnly, buildsSupports.ExplodesOnKill,
            buildsSupports.OverloadRepeatsEveryThirdUse, buildsSupports.TemperanceLevelPerLayer,
            buildsSupports.TemperanceQualityPerLayer);
    }

    public static bool TryPay(ResourceState resources, ResolvedSkill skill) => skill.LifeCost > 0
        ? resources.TryPayLifeCost(skill.LifeCost)
        : resources.TryPayMana(skill.ManaCost);

    public static int DamageMultiplier(ResolvedSkill skill, int life, int maximumLife)
    {
        int conditional = skill.ExecuteThresholdBasisPoints > 0 &&
                          (long)life * 10_000 < (long)maximumLife * skill.ExecuteThresholdBasisPoints
            ? skill.ExecuteMultiplierBasisPoints
            : skill.NonExecuteMultiplierBasisPoints;
        return SaturatingInt((long)skill.DamageMultiplierBasisPoints * conditional / 10_000);
    }

    public static int BaseDamage(ResolvedSkill skill, SkillTag tags, WeaponProfile weapon,
        int addedPhysicalDamage, int? weaponRoll = null)
    {
        if (!tags.HasFlag(SkillTag.Attack)) return Math.Max(1, skill.BaseDamageBasisPoints);
        int physical = weaponRoll ?? checked((weapon.MinimumPhysicalDamage + weapon.MaximumPhysicalDamage) / 2);
        return Math.Max(1, checked(physical + addedPhysicalDamage));
    }

    public static int ScaleOffensiveDamage(int rawDamage, ResolvedSkill skill,
        SkillConfiguration configuration, TeamBuild build, SkillTag tags,
        int targetLife, int targetMaximumLife, int actionMultiplierBasisPoints = 10_000,
        bool targetRareOrBoss = false, SkillDamageType? damageType = null,
        int additionalIncreasedBasisPoints = 0,
        bool applyIncreased = true,
        IReadOnlyList<DamageType>? damageHistory = null)
    {
        PassiveModifiers passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        long value = rawDamage;
        if (applyIncreased)
        {
            DamageModifiers increases = OffensiveIncreases(skill, configuration, build, tags, additionalIncreasedBasisPoints);
            DamageType type = (damageType ?? skill.DamageType) switch
            {
                SkillDamageType.Fire => DamageType.Fire, SkillDamageType.Cold => DamageType.Cold,
                SkillDamageType.Lightning => DamageType.Lightning, SkillDamageType.Void => DamageType.Void,
                _ => DamageType.Physical,
            };
            value = Scale(value, 10_000L + increases.InitialIncreasedBasisPoints + increases.IncreasedByType!.GetValueOrDefault(type) +
                (type is DamageType.Fire or DamageType.Cold or DamageType.Lightning ? increases.ElementalIncreasedBasisPoints : 0));
        }
        value = Scale(value, 10_000L + passive.MoreDamageBasisPoints);
        if (skill.Role == SkillRole.DamageOverTime)
            value = Scale(value, 10_000L + build.MoreDamageOverTimeBasisPoints);
        if (tags.HasFlag(SkillTag.Attack))
            value = Scale(value, 10_000L + build.MoreAttackDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Attack) && ActiveSkillCatalog.ActiveForSkill(skill.SkillId).Curve == SkillCurve.UnarmedAttack)
            value = Scale(value, 10_000L + (build.CombatEquipment?.UnarmedMoreDamageBasisPoints ?? 0));
        if (tags.HasFlag(SkillTag.Spell))
            value = Scale(value, 10_000L + build.MoreSpellDamageBasisPoints);
        bool elementalDamage = damageType is SkillDamageType.Fire or SkillDamageType.Cold or SkillDamageType.Lightning ||
                               damageType is null && (tags & SkillTag.Elemental) != 0;
        bool voidDamage = damageType == SkillDamageType.Void ||
                          damageType is null && tags.HasFlag(SkillTag.Void);
        if (damageHistory is not null)
        {
            elementalDamage = damageHistory.Any(type => type is DamageType.Fire or DamageType.Cold or DamageType.Lightning);
            voidDamage = damageHistory.Contains(DamageType.Void);
        }
        if (elementalDamage)
            value = Scale(value, 10_000L + build.MoreElementalDamageBasisPoints);
        if (voidDamage)
            value = Scale(value, 10_000L + build.MoreVoidDamageBasisPoints);
        if (targetRareOrBoss)
            value = Scale(value, 10_000L + build.MoreRareBossDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Attack))
            value = Scale(value, skill.BaseDamageBasisPoints);
        value = Scale(value, DamageMultiplier(skill, targetLife, targetMaximumLife));
        value = Scale(value, MasteryRuntime.OffensiveMultiplier(passive, tags, build.Weapon,
            targetLife, targetMaximumLife, hasOffHand: build.HasOffHand));
        return SaturatingInt(Scale(value, actionMultiplierBasisPoints));
    }

    public static DamageModifiers OffensiveIncreases(ResolvedSkill skill, SkillConfiguration configuration,
        TeamBuild build, SkillTag tags, int additionalIncreasedBasisPoints = 0)
    {
        var equipment = build.CombatEquipment ?? Equipment.EquipmentCombatLoadout.Empty;
        int common = (tags.HasFlag(SkillTag.Attack) ? build.IncreasedDamageBasisPoints - equipment.PhysicalIncreaseIncludedInAttack : 0) +
            (build.PassiveProfile ?? PassiveModifiers.Empty).DamageFor(tags) + configuration.Quality * 100 + additionalIncreasedBasisPoints;
        if (tags.HasFlag(SkillTag.Attack)) common += Ascendancies.WarriorAscendancyRules.IncreasedAttackDamageBasisPoints(
            build.Ascendancy ?? Ascendancies.CombatProfile.Empty, build.Sheet.Attributes.Physique);
        if (tags.HasFlag(SkillTag.Spell)) common += build.IncreasedSpellDamageBasisPoints;
        int Value(Campaign.Items.ItemModifierKind kind) => equipment.Value(kind);
        if (tags.HasFlag(SkillTag.Melee)) common += Value(Campaign.Items.ItemModifierKind.IncreasedMeleeDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Projectile)) common += Value(Campaign.Items.ItemModifierKind.IncreasedProjectileDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Area)) common += Value(Campaign.Items.ItemModifierKind.IncreasedAreaDamageBasisPoints);
        if (skill.Role == SkillRole.DamageOverTime) common += Value(Campaign.Items.ItemModifierKind.IncreasedDamageOverTimeBasisPoints);
        return new(new Dictionary<DamageType, int>
        {
            [DamageType.Physical] = Value(Campaign.Items.ItemModifierKind.IncreasedPhysicalDamageBasisPoints),
            [DamageType.Fire] = Value(Campaign.Items.ItemModifierKind.IncreasedFireDamageBasisPoints),
            [DamageType.Cold] = Value(Campaign.Items.ItemModifierKind.IncreasedColdDamageBasisPoints),
            [DamageType.Lightning] = Value(Campaign.Items.ItemModifierKind.IncreasedLightningDamageBasisPoints),
            [DamageType.Void] = Value(Campaign.Items.ItemModifierKind.IncreasedVoidDamageBasisPoints),
        }, common, Value(Campaign.Items.ItemModifierKind.IncreasedElementalDamageBasisPoints));
    }

    public static int ActionDelay(TeamBuild build, int baseTicks, SkillTag tags)
    {
        PassiveModifiers passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        int masterySpeed = MasteryRuntime.ActionSpeedMultiplier(passive, tags, build.Weapon);
        int increasedSpeed = build.IncreasedActionSpeedBasisPoints;
        if (tags.HasFlag(SkillTag.Attack)) increasedSpeed = checked(increasedSpeed + build.IncreasedAttackSpeedBasisPoints);
        if (tags.HasFlag(SkillTag.Spell)) increasedSpeed = checked(increasedSpeed +
            (build.CombatEquipment?.Value(GameForWork.Core.Campaign.Items.ItemModifierKind.IncreasedCastSpeedBasisPoints) ?? 0));
        return Math.Max(1, checked((int)((long)Math.Max(1, baseTicks) * 10_000 * 10_000 /
            Math.Max(10_000_000, (long)(10_000 + increasedSpeed) * masterySpeed))));
    }

    public static int ActionFrequencyMilliPerSecond(TeamBuild build, int baseTicks, int cooldownTicks,
        SkillTag tags)
    {
        int actionDelay = ActionDelay(build, baseTicks, tags);
        if (!tags.HasFlag(SkillTag.Attack)) return checked(20_000 / actionDelay);

        PassiveModifiers passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        int masterySpeed = MasteryRuntime.ActionSpeedMultiplier(passive, tags, build.Weapon);
        int increasedSpeed = checked(build.IncreasedActionSpeedBasisPoints + build.IncreasedAttackSpeedBasisPoints);
        int baseFrequency = baseTicks <= 1
            ? Math.Max(1, build.Weapon.AttacksPerSecondMilli)
            : Math.Max(1, 20_000 / baseTicks);
        int frequency = CombatRules.AttackFrequencyMilliPerSecond(baseFrequency, increasedSpeed,
            [masterySpeed]);
        if (cooldownTicks > 1) frequency = Math.Min(frequency, 20_000 / cooldownTicks);
        return frequency;
    }

    private static int LegacyValue(SkillConfiguration configuration, SkillSupport support) =>
        ActiveSkillCatalog.SupportFor(support).ValueAt(configuration.Level, configuration.Quality);

    private static int More(int basisPoints, int percent) => checked(basisPoints * (10_000 + percent * 100) / 10_000);

    private static long Scale(long value, long basisPoints)
    {
        if (value <= 0 || basisPoints <= 0) return 0;
        if (value > long.MaxValue / basisPoints) return long.MaxValue;
        return value * basisPoints / 10_000;
    }

    private static int SaturatingInt(long value) => (int)Math.Clamp(value, 0, int.MaxValue);
}

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
    int TemperanceQualityPerLayer = 0, int BaseAreaRadiusRaw = 0, int AreaMoreBasisPoints = 10_000, int AreaIncreasedBasisPoints = 0, bool AlwaysHit = false, SkillTag AdditionalTags = SkillTag.None, int AdditionalAttackSpeedBasisPoints = 0, int AdditionalCastSpeedBasisPoints = 0,
    ProjectileMechanics? ProjectileMechanics = null, bool WaiveManaCost = false)
{
    public int AreaMultiplierBasisPoints => Math.Max(2_500, CombatRules.ApplyMore(Math.Max(0, 10_000 + AreaIncreasedBasisPoints), [AreaMoreBasisPoints]));
    public int AreaRadiusRaw => AreaRules.Radius(BaseAreaRadiusRaw > 0 ? BaseAreaRadiusRaw : RangeRaw, AreaMultiplierBasisPoints);
}

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
        int castTime = UnarmedRules.IsSkill(configuration.SkillId) ? UnarmedRules.CastTicks(configuration.SkillId, configuration.Quality) : definition.CastTimeTicks;
        int damage = 10_000;
        int bleed = 0;
        int projectiles = 1;
        int projectileSpeed = 10_000;
        int chains = 0;
        int leech = 0;
        int executeThreshold = 0;
        int execute = 10_000;
        int nonExecute = 10_000;
        int baseDamage = buildsActive.DamageAt(configuration.Level);
        int ailmentChance = active.AilmentChanceBasisPoints;
        if (configuration.SkillId is SkillIds.AshJavelin or "archetypes.skill.venom_blades" or "archetypes.skill.corrosive_trap")
            ailmentChance += Math.Clamp(configuration.Quality, 0, 20) * 100;
        int pierce = 0;
        int fork = 0;
        bool returns = active.Tags.HasFlag(SkillTag.Returning);
        if (configuration.Supports.HasFlag(SkillSupport.IncreasedArea))
        {
            damage = checked(damage * 9_000 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Bleed))
            bleed += Math.Min(10_000, SupportValue(configuration, SkillSupport.Bleed) * 100);
        if (configuration.Supports.HasFlag(SkillSupport.LifeCost))
        {
            life = Math.Max(1, (mana * 15 + 9) / 10);
            mana = 0;
            damage = More(damage, SupportValue(configuration, SkillSupport.LifeCost));
        }
        if (configuration.Supports.HasFlag(SkillSupport.Brutality))
            damage = More(damage, SupportValue(configuration, SkillSupport.Brutality));
        if (configuration.Supports.HasFlag(SkillSupport.MultipleProjectiles))
        {
            projectiles += 2;
            int less = ActiveSkillCatalog.Interpolate(25, 15, configuration.Level, false);
            damage = checked(damage * (100 - less) / 100);
            projectileSpeed = checked(projectileSpeed * (10_000 + SupportValue(configuration, SkillSupport.MultipleProjectiles) * 100) / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.FasterProjectiles))
        {
            int value = SupportValue(configuration, SkillSupport.FasterProjectiles);
            projectileSpeed = checked(projectileSpeed * (10_000 + value * 100) / 10_000);
            int rangeIncrease = ActiveSkillCatalog.Interpolate(20, 35, configuration.Level, false);
            range = checked(range * (10_000 + rangeIncrease * 100) / 10_000);
            damage = More(damage, rangeIncrease);
        }
        if (configuration.Supports.HasFlag(SkillSupport.UrgentWarCry))
            cooldown = Math.Max(1, cooldown * 10_000 / (10_000 + SupportValue(configuration, SkillSupport.UrgentWarCry) * 100));
        if (configuration.Supports.HasFlag(SkillSupport.LifeLeech))
        {
            leech = SupportValue(configuration, SkillSupport.LifeLeech);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Execution))
        {
            executeThreshold = 2_000;
            execute = 10_000 + SupportValue(configuration, SkillSupport.Execution) * 100;
            nonExecute = 9_000;
        }
        if (configuration.Supports.HasFlag(SkillSupport.SpellEcho) && definition.Tags.HasFlag(SkillTag.Spell))
        {
            int less = SupportValue(configuration, SkillSupport.SpellEcho);
            damage = checked(damage * (100 - less) / 100);
        }
        if (configuration.Supports.HasFlag(SkillSupport.ElementalFocus) && (definition.Tags & SkillTag.Elemental) != 0)
            damage = More(damage, SupportValue(configuration, SkillSupport.ElementalFocus));
        if (configuration.Supports.HasFlag(SkillSupport.ConcentratedEffect) && definition.Tags.HasFlag(SkillTag.Area))
        {
            damage = More(damage, SupportValue(configuration, SkillSupport.ConcentratedEffect));
        }
        if (configuration.Supports.HasFlag(SkillSupport.HeavyMomentum))
            damage = More(damage, SupportValue(configuration, SkillSupport.HeavyMomentum));
        if (configuration.Supports.HasFlag(SkillSupport.TripleImpact))
            damage = More(damage, SupportValue(configuration, SkillSupport.TripleImpact) / 3);
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
        if (configuration.Supports.HasFlag(SkillSupport.BlockTrigger)) damage = checked(damage *
            (10_000 - ActiveSkillCatalog.Interpolate(3_500, 2_000, SupportLink(configuration, SkillSupport.BlockTrigger).Level, false)) / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.CastWhenDamaged)) damage = checked(damage *
            (10_000 - ActiveSkillCatalog.Interpolate(3_000, 2_000, SupportLink(configuration, SkillSupport.CastWhenDamaged).Level, false)) / 10_000);
        if (LinkedSupportRules.Support(configuration, SupportMechanic.AttackTrigger)) damage = checked(damage *
            (10_000 - LinkedSupportRules.SupportValue(configuration, SupportMechanic.AttackTrigger, 4_000, 2_500)) / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.Chain))
        {
            chains += SupportValue(configuration, SkillSupport.Chain);
            damage = checked(damage * (10_000 - ActiveSkillCatalog.Interpolate(2_500, 1_500,
                SupportLink(configuration, SkillSupport.Chain).Level, false)) / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Pierce)) pierce += SupportValue(configuration, SkillSupport.Pierce);
        if (configuration.Supports.HasFlag(SkillSupport.Fork)) fork += SupportValue(configuration, SkillSupport.Fork);
        if (configuration.Supports.HasFlag(SkillSupport.Return)) returns = true;
        SupportProfile archetypes = SupportRules.Resolve(configuration.ExtendedSupports);
        damage = checked(damage * archetypes.DamageMultiplierBasisPoints / 10_000);
        range = checked(range * archetypes.RangeMultiplierBasisPoints / 10_000);
        castTime = Math.Max(1, checked(castTime * 10_000 / Math.Max(1, archetypes.CastSpeedBasisPoints)));
        cooldown = Math.Max(1, checked(cooldown * 10_000 / Math.Max(1, archetypes.CooldownRecoveryBasisPoints)));
        projectiles += archetypes.ProjectileCount;
        pierce += archetypes.PierceCount;
        chains += archetypes.ChainCount;
        if (UnarmedRules.IsSkill(configuration.SkillId))
            damage = CombatRules.ApplyMore(damage, [10_000 + LinkedSupportRules.SupportValue(configuration, SupportMechanic.UnarmedFocus, 4_000, 7_000)]);
        damage = checked(damage * buildsSupports.DamageMultiplierBasisPoints / 10_000);
        mana = checked((mana * buildsSupports.ResourceMultiplierBasisPoints + 9_999) / 10_000);
        life = checked((life * buildsSupports.ResourceMultiplierBasisPoints + 9_999) / 10_000);
        passive ??= PassiveModifiers.Empty;
        ProjectileMechanics projectileMechanics = ProjectileMasteryRules.Resolve(passive);
        if (definition.Tags.HasFlag(SkillTag.Projectile))
        {
            int pierceStep = configuration.Supports.HasFlag(SkillSupport.Pierce)
                ? SupportLink(configuration, SkillSupport.Pierce).Quality >= 20 ? 9_400 : 9_200 : 10_000;
            int forkStep = configuration.Supports.HasFlag(SkillSupport.Fork)
                ? SupportLink(configuration, SkillSupport.Fork).Quality >= 20 ? 8_500 : 10_000 - ActiveSkillCatalog.Interpolate(3_000, 2_000,
                    SupportLink(configuration, SkillSupport.Fork).Level, false) : 10_000;
            int returnStep = configuration.Supports.HasFlag(SkillSupport.Return)
                ? 10_000 - SupportValue(configuration, SkillSupport.Return) * 100 : 10_000;
            projectileMechanics = projectileMechanics with
            {
                PierceStepMultiplierBasisPoints = Compose(projectileMechanics.PierceStepMultiplierBasisPoints, pierceStep),
                ForkMultiplierBasisPoints = Compose(projectileMechanics.ForkMultiplierBasisPoints, forkStep),
                ReturnMultiplierBasisPoints = Compose(projectileMechanics.ReturnMultiplierBasisPoints, returnStep),
            };
            projectileSpeed = checked(projectileSpeed * projectileMechanics.SpeedMultiplierBasisPoints / 10_000);
            damage = checked(damage * projectileMechanics.HitMultiplierBasisPoints / 10_000);
            if (projectileMechanics.InfinitePierce) pierce = int.MaxValue;
            fork = Math.Max(fork, projectileMechanics.ForkCount);
            chains = checked(chains + projectileMechanics.AdditionalChains);
            returns |= projectileMechanics.Returns;
        }
        mana = Math.Max(0, checked(mana * Math.Max(0, 10_000 - passive.ReducedSkillCostBasisPoints) / 10_000));
        mana = MasteryRuntime.ManaCost(passive, definition.Tags, mana);
        life = Math.Max(0, checked(life * Math.Max(0, 10_000 - passive.ReducedSkillCostBasisPoints) / 10_000));
        range = Math.Max(1, checked(range * (10_000 + passive.IncreasedSkillRangeBasisPoints) / 10_000));
        cooldown = Math.Max(1, checked(cooldown * 10_000 / Math.Max(1, 10_000 + passive.IncreasedCooldownRecoveryBasisPoints + AttackMasteryRules.CooldownRecovery(passive, definition.Tags))));
        if (cooldown > 1 && MasteryRuntime.Has(passive, "触发_冷却", 4))
            cooldown = Math.Max(2, checked(cooldown * 13_000 / 10_000));
        ailmentChance = Math.Clamp(ailmentChance + (active.Ailment == Ailment.Bleed ? bleed : 0), 0, 10_000);
        return new ResolvedSkill(configuration.SkillId, mana, life, range, castTime, cooldown,
            damage, bleed, projectiles, projectileSpeed, chains, leech, executeThreshold, execute, nonExecute,
            active.DamageType, active.Role, active.Shape, baseDamage, active.Ailment, ailmentChance,
            pierce, fork, returns, active.Capabilities.HasFlag(SkillCapability.RequiresShield),
            buildsSupports.ResourceMultiplierBasisPoints, buildsSupports.SingleTargetOnly, buildsSupports.ExplodesOnKill,
            buildsSupports.OverloadRepeatsEveryThirdUse, buildsSupports.TemperanceLevelPerLayer,
            buildsSupports.TemperanceQualityPerLayer, configuration.SkillId == SkillIds.SeismicCharge ? 1_800 : definition.RangeRaw, AreaRules.More(configuration, passive), AreaRules.Increased(configuration, passive),
            AdditionalTags: active.Role == SkillRole.Counter ? SkillTag.Counter : SkillTag.None,
            AdditionalAttackSpeedBasisPoints: SupportSpeed(configuration, SkillSupport.AttackSpeed, SkillTag.Attack) +
                (UnarmedRules.IsSkill(configuration.SkillId) ? LinkedSupportRules.SupportValue(configuration, SupportMechanic.UnarmedFocus, 1_000, 2_000) : 0),
            AdditionalCastSpeedBasisPoints: SupportSpeed(configuration, SkillSupport.FasterCasting, SkillTag.Spell),
            ProjectileMechanics: projectileMechanics);
    }

    public static bool TryPay(ResourceState resources, ResolvedSkill skill, bool allowOvercharge = true, bool selfCast = true) =>
        resources.TryPaySkillCost(skill.SkillId, skill.LifeCost, skill.ManaCost, selfCast, allowOvercharge,
            Combat.GuardState.ShieldCost(skill.SkillId, resources.MaximumShield), waiveMana: skill.WaiveManaCost);

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
        if (!tags.HasFlag(SkillTag.Attack)) return Math.Max(1, (skill.BaseDamageBasisPoints + 50) / 100);
        if (weaponRoll is null) weapon = UnarmedRules.Source(skill.SkillId, weapon);
        int physical = weaponRoll ?? checked((weapon.MinimumPhysicalDamage + weapon.MaximumPhysicalDamage) / 2);
        return Math.Max(1, checked(physical + addedPhysicalDamage));
    }

    public static int ScaleOffensiveDamage(int rawDamage, ResolvedSkill skill,
        SkillConfiguration configuration, TeamBuild build, SkillTag tags,
        int targetLife, int targetMaximumLife, int actionMultiplierBasisPoints = 10_000,
        bool targetRareOrBoss = false, SkillDamageType? damageType = null,
        int additionalIncreasedBasisPoints = 0,
        bool applyIncreased = true,
        IReadOnlyList<DamageType>? damageHistory = null, int nearbyEnemyCount = 1, int distanceRaw = 1_000)
    {
        PassiveModifiers passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        long value = rawDamage;
        if (applyIncreased)
        {
            DamageModifiers increases = OffensiveIncreases(build, tags, skill.Role == SkillRole.DamageOverTime, additionalIncreasedBasisPoints);
            DamageType type = (damageType ?? skill.DamageType) switch
            {
                SkillDamageType.Fire => DamageType.Fire,
                SkillDamageType.Cold => DamageType.Cold,
                SkillDamageType.Lightning => DamageType.Lightning,
                SkillDamageType.Void => DamageType.Void,
                _ => DamageType.Physical,
            };
            value = Scale(value, 10_000L + increases.InitialIncreasedBasisPoints + increases.IncreasedByType!.GetValueOrDefault(type) +
                (type is DamageType.Fire or DamageType.Cold or DamageType.Lightning ? increases.ElementalIncreasedBasisPoints : 0));
        }
        if (UnarmedRules.IsSkill(skill.SkillId) && !build.HasUsableWeapon && UnarmedRules.Has(build.Ascendancy, "unarmed", "core"))
            value = Scale(value, 14_500);
        if (UnarmedRules.IsSkill(skill.SkillId) && !build.HasUsableWeapon && !build.HasOffHand && MasteryRuntime.Has(passive, "徒手", 0))
            value = Scale(value, 16_000);
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
            targetLife, targetMaximumLife, nearbyEnemyCount, distanceRaw, hasOffHand: build.HasOffHand, hit: skill.Role != SkillRole.DamageOverTime));
        if (skill.Role != SkillRole.DamageOverTime && skill.RangeRaw > 0 && (long)distanceRaw * 10 >= (long)skill.RangeRaw * 7)
            value = Scale(value, 10_000L + passive.SpecializedValue(PassiveEffectKind.DistantHitMoreBasisPoints));
        return SaturatingInt(Scale(value, actionMultiplierBasisPoints));
    }

    public static DamageModifiers OffensiveIncreases(TeamBuild build, SkillTag tags,
        bool damageOverTime = false, int additionalIncreasedBasisPoints = 0, int? armor = null)
    {
        var equipment = build.CombatEquipment ?? Equipment.EquipmentCombatLoadout.Empty;
        var passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        int common = AttackMasteryRules.DamageIncrease(passive, tags) + build.IncreasedGenericDamageBasisPoints + (tags.HasFlag(SkillTag.Attack) ? build.IncreasedDamageBasisPoints - equipment.PhysicalIncreaseIncludedInAttack : 0) +
            passive.DamageFor(tags & ~(SkillTag.Physical | SkillTag.Elemental | SkillTag.Void), damageOverTime) + additionalIncreasedBasisPoints;
        if (damageOverTime) common += SpiritBarrierMasteryRules.DamageIncrease(build.Sheet, passive);
        if (!damageOverTime) common += SuppressionMasteryRules.OverflowHitIncrease(passive, build.Sheet.SpellSuppressionBasisPoints) + passive.SpecializedValue(PassiveEffectKind.IncreasedHitDamageBasisPoints) + CriticalMasteryRules.ConvertedHitIncrease(passive, build.IncreasedCriticalChanceBasisPoints);
        if (tags.HasFlag(SkillTag.Attack)) common += Ascendancies.WarriorAscendancyRules.IncreasedAttackDamageBasisPoints(
            build.Ascendancy ?? Ascendancies.CombatProfile.Empty, build.Sheet.Attributes.Physique);
        if (tags.HasFlag(SkillTag.Attack)) common += UnarmedRules.DamageIncrease(build);
        if (tags.HasFlag(SkillTag.Spell)) common += build.IncreasedSpellDamageBasisPoints;
        if (build.Ascendancy?.Has("core.ascendancy.spellarmor.hybrid.core") == true)
        {
            if (tags.HasFlag(SkillTag.Attack)) common += build.Sheet.Attributes.Physique / 100 * 5_000;
            if (tags.HasFlag(SkillTag.Spell)) common += build.Sheet.Attributes.Energy / 100 * 5_000;
        }
        int Value(Campaign.Items.ItemModifierKind kind) => equipment.Value(kind);
        if (tags.HasFlag(SkillTag.Melee)) common += Value(Campaign.Items.ItemModifierKind.IncreasedMeleeDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Projectile)) common += Value(Campaign.Items.ItemModifierKind.IncreasedProjectileDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Area)) common += Value(Campaign.Items.ItemModifierKind.IncreasedAreaDamageBasisPoints);
        if (damageOverTime) common += Value(Campaign.Items.ItemModifierKind.IncreasedDamageOverTimeBasisPoints);
        return new(new Dictionary<DamageType, int>
        {
            [DamageType.Physical] = Value(Campaign.Items.ItemModifierKind.IncreasedPhysicalDamageBasisPoints) + passive.IncreasedPhysicalDamageBasisPoints +
                (damageOverTime ? passive.SpecializedValue(PassiveEffectKind.IncreasedPhysicalDamageOverTimeBasisPoints) : 0) +
                (!damageOverTime && MasteryRuntime.Has(build.PassiveProfile ?? PassiveModifiers.Empty, "护甲", 6) ? (armor ?? build.Sheet.Armor().Value) / 1_000 * 400 : 0),
            [DamageType.Fire] = Value(Campaign.Items.ItemModifierKind.IncreasedFireDamageBasisPoints) + ElementalRules.TypeIncrease(build, DamageType.Fire),
            [DamageType.Cold] = Value(Campaign.Items.ItemModifierKind.IncreasedColdDamageBasisPoints) + ElementalRules.TypeIncrease(build, DamageType.Cold),
            [DamageType.Lightning] = Value(Campaign.Items.ItemModifierKind.IncreasedLightningDamageBasisPoints) + ElementalRules.TypeIncrease(build, DamageType.Lightning),
            [DamageType.Void] = Value(Campaign.Items.ItemModifierKind.IncreasedVoidDamageBasisPoints) + passive.IncreasedVoidDamageBasisPoints,
        }, common, Value(Campaign.Items.ItemModifierKind.IncreasedElementalDamageBasisPoints) + passive.IncreasedElementalDamageBasisPoints,
            VoidDebuffIncreaseBasisPoints: MasteryRuntime.Has(passive, "虚空", 4) ? 6_000 : 0);
    }

    private static int SupportSpeed(SkillConfiguration configuration, SkillSupport support, SkillTag tag) =>
        configuration.Supports.HasFlag(support) && SkillDefinitions.Get(configuration.SkillId).Tags.HasFlag(tag)
            ? SupportValue(configuration, support) * 100 + SupportLink(configuration, support).Quality * 50 : 0;

    public static int ActionDelay(TeamBuild build, ResolvedSkill skill, SkillTag tags)
    {
        int delay = ActionDelay(build, skill.CastTimeTicks, tags, skill.AdditionalAttackSpeedBasisPoints, skill.AdditionalCastSpeedBasisPoints);
        if (skill.ProjectileMechanics?.SequentialVolley == true && skill.ProjectileCount > 1)
            delay = checked(delay + Math.Max(1, delay * 15 / 100) * (skill.ProjectileCount - 1));
        return delay;
    }

    public static int ActionDelay(TeamBuild build, int baseTicks, SkillTag tags, int additionalAttackSpeed = 0, int additionalCastSpeed = 0)
    {
        PassiveModifiers passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        int masterySpeed = MasteryRuntime.ActionSpeedMultiplier(passive, tags, build.Weapon);
        if ((tags & (SkillTag.Attack | SkillTag.Spell)) != 0)
            masterySpeed = (int)((long)masterySpeed * build.AttackCastSpeedMultiplierBasisPoints / 10_000);
        int increasedSpeed = build.IncreasedActionSpeedBasisPoints;
        if (tags.HasFlag(SkillTag.Attack)) increasedSpeed = checked(increasedSpeed + build.IncreasedAttackSpeedBasisPoints + UnarmedRules.AttackSpeed(build) + additionalAttackSpeed);
        if (tags.HasFlag(SkillTag.Spell)) increasedSpeed = checked(increasedSpeed + build.IncreasedCastSpeedBasisPoints + passive.SpecializedValue(PassiveEffectKind.IncreasedCastSpeedBasisPoints) + additionalCastSpeed +
            (build.CombatEquipment?.Value(GameForWork.Core.Campaign.Items.ItemModifierKind.IncreasedCastSpeedBasisPoints) ?? 0));
        return Math.Max(1, checked((int)((long)Math.Max(1, baseTicks) * 10_000 * 10_000 /
            Math.Max(10_000_000, (long)(10_000 + increasedSpeed) * masterySpeed))));
    }

    public static int ActionFrequencyMilliPerSecond(TeamBuild build, int baseTicks, int cooldownTicks,
        SkillTag tags, int additionalAttackSpeed = 0, int additionalCastSpeed = 0)
    {
        int actionDelay = ActionDelay(build, baseTicks, tags, additionalAttackSpeed, additionalCastSpeed);
        if (!tags.HasFlag(SkillTag.Attack)) return checked(20_000 / actionDelay);

        PassiveModifiers passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        int masterySpeed = MasteryRuntime.ActionSpeedMultiplier(passive, tags, build.Weapon);
        if ((tags & (SkillTag.Attack | SkillTag.Spell)) != 0)
            masterySpeed = (int)((long)masterySpeed * build.AttackCastSpeedMultiplierBasisPoints / 10_000);
        int increasedSpeed = checked(build.IncreasedActionSpeedBasisPoints + build.IncreasedAttackSpeedBasisPoints + UnarmedRules.AttackSpeed(build) + additionalAttackSpeed);
        int baseFrequency = baseTicks <= 1
            ? Math.Max(1, build.Weapon.AttacksPerSecondMilli)
            : Math.Max(1, 20_000 / baseTicks);
        int frequency = CombatRules.AttackFrequencyMilliPerSecond(baseFrequency, increasedSpeed,
            [masterySpeed]);
        if (cooldownTicks > 1) frequency = Math.Min(frequency, 20_000 / cooldownTicks);
        return frequency;
    }

    public static int ActionFrequencyMilliPerSecond(TeamBuild build, ResolvedSkill skill, SkillTag tags)
    {
        int frequency = ActionFrequencyMilliPerSecond(build, skill.CastTimeTicks, skill.CooldownTicks, tags,
            skill.AdditionalAttackSpeedBasisPoints, skill.AdditionalCastSpeedBasisPoints);
        if (skill.ProjectileMechanics?.SequentialVolley == true && skill.ProjectileCount > 1)
            frequency = checked(frequency * 100 / (100 + 15 * (skill.ProjectileCount - 1)));
        return frequency;
    }

    public static LinkedSupport SupportLink(SkillConfiguration configuration, SkillSupport support)
    {
        var definition = ActiveSkillCatalog.SupportFor(support);
        return configuration.ExtendedBuildsSupportLinks.FirstOrDefault(link => link.StoneId == definition.StoneId) ??
            new(definition.StoneId, configuration.Level, configuration.Quality);
    }
    public static int SupportValue(SkillConfiguration configuration, SkillSupport support)
    {
        var link = SupportLink(configuration, support);
        return ActiveSkillCatalog.SupportFor(support).ValueAt(link.Level, link.Quality);
    }

    private static int More(int basisPoints, int percent) => checked(basisPoints * (10_000 + percent * 100) / 10_000);

    private static int Compose(int left, int right) => checked(left * right / 10_000);

    private static long Scale(long value, long basisPoints)
    {
        if (value <= 0 || basisPoints <= 0) return 0;
        if (value > long.MaxValue / basisPoints) return long.MaxValue;
        return value * basisPoints / 10_000;
    }

    private static int SaturatingInt(long value) => (int)Math.Clamp(value, 0, int.MaxValue);
}

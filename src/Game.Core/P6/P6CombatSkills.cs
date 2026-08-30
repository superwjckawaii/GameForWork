using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P17;
using GameForWork.Core.P24;

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
    bool RequiresShield);

public static class P6CombatSkillRules
{
    public static P6ResolvedSkill Resolve(SkillConfiguration configuration, int maximumLife, P205PassiveModifiers? passive = null)
    {
        SkillDefinition definition = P1Skills.Get(configuration.SkillId);
        P17ActiveSkillDefinition active = P24SkillCatalog.TryActiveForSkill(configuration.SkillId, out P24ActiveSkillDefinition? p24Active)
            ? p24Active!.Combat
            : P17SkillCatalog.ActiveForSkill(configuration.SkillId);
        int mana = P18.P18AscendancyRules.AttackManaCost(definition.BaseManaCost, definition.Tags);
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
        int baseDamage = active.DamageBasisPoints;
        int ailmentChance = active.AilmentChanceBasisPoints;
        int pierce = 0;
        int fork = 0;
        bool returns = active.Tags.HasFlag(SkillTag.Returning);
        damage = checked(damage * (10_000 + (Math.Clamp(configuration.Level, 1, 21) - 1) * 250) / 10_000);

        if (configuration.Supports.HasFlag(SkillSupport.IncreasedArea))
        {
            range = checked(range * 13_500 / 10_000);
            damage = checked(damage * 9_000 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Bleed)) bleed += 6_000;
        if (configuration.Supports.HasFlag(SkillSupport.LifeCost))
        {
            life = Math.Max(1, (mana * 15 + 9) / 10);
            mana = 0;
            damage = checked(damage * 12_000 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Brutality)) damage = checked(damage * 13_500 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.MultipleProjectiles))
        {
            projectiles += 2;
            damage = checked(damage * 8_000 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.FasterProjectiles))
        {
            projectileSpeed = 15_000;
            range = checked(range * 11_500 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.UrgentWarCry)) cooldown = Math.Max(1, cooldown * 10_000 / 13_000);
        if (configuration.Supports.HasFlag(SkillSupport.LifeLeech))
        {
            leech = 200;
            mana = checked((mana * 12_000 + 9_999) / 10_000);
            life = checked((life * 12_000 + 9_999) / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Execution))
        {
            executeThreshold = 2_000;
            execute = 14_000;
            nonExecute = 9_000;
        }
        if (configuration.Supports.HasFlag(SkillSupport.SpellEcho) && definition.Tags.HasFlag(SkillTag.Spell))
        {
            projectiles += 1;
            damage = checked(damage * 8_200 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.ElementalFocus) && (definition.Tags & SkillTag.Elemental) != 0)
            damage = checked(damage * 12_800 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.AddedFire)) damage = checked(damage * 11_800 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.AddedCold)) damage = checked(damage * 11_500 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.AddedLightning)) damage = checked(damage * 11_700 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.CriticalStrikes)) damage = checked(damage * 11_200 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.ConcentratedEffect) && definition.Tags.HasFlag(SkillTag.Area))
        {
            range = checked(range * 7_500 / 10_000);
            damage = checked(damage * 13_200 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.AttackSpeed) && definition.Tags.HasFlag(SkillTag.Attack))
        {
            cooldown = Math.Max(1, cooldown * 10_000 / 12_500);
        }
        if (configuration.Supports.HasFlag(SkillSupport.HeavyMomentum)) damage = checked(damage * 14_500 / 10_000);
        if (configuration.Supports.HasFlag(SkillSupport.TripleImpact)) damage = checked(damage * 12_667 / 10_000);
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
        passive ??= P205PassiveModifiers.Empty;
        mana = Math.Max(0, checked(mana * Math.Max(0, 10_000 - passive.ReducedSkillCostBasisPoints) / 10_000));
        life = Math.Max(0, checked(life * Math.Max(0, 10_000 - passive.ReducedSkillCostBasisPoints) / 10_000));
        range = Math.Max(1, checked(range * (10_000 + passive.IncreasedSkillRangeBasisPoints) / 10_000));
        cooldown = Math.Max(1, checked(cooldown * 10_000 / Math.Max(1, 10_000 + passive.IncreasedCooldownRecoveryBasisPoints)));
        ailmentChance = Math.Clamp(ailmentChance + bleed, 0, 10_000);
        return new P6ResolvedSkill(configuration.SkillId, mana, life, range, castTime, cooldown,
            damage, bleed, projectiles, projectileSpeed, chains, leech, executeThreshold, execute, nonExecute,
            active.DamageType, active.Role, active.Shape, baseDamage, active.Ailment, ailmentChance,
            pierce, fork, returns, active.Capabilities.HasFlag(P17SkillCapability.RequiresShield));
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
        return checked(skill.DamageMultiplierBasisPoints * conditional / 10_000);
    }
}

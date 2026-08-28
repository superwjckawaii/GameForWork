using GameForWork.Core.P1.Combat;

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
    int NonExecuteMultiplierBasisPoints);

public static class P6CombatSkillRules
{
    public static P6ResolvedSkill Resolve(SkillConfiguration configuration, int maximumLife)
    {
        SkillDefinition definition = P1Skills.Get(configuration.SkillId);
        int mana = definition.BaseManaCost;
        int life = 0;
        int range = definition.RangeRaw;
        int cooldown = definition.CooldownTicks;
        int damage = 10_000;
        int bleed = 0;
        int projectiles = 1;
        int projectileSpeed = 10_000;
        int chains = configuration.Supports.HasFlag(SkillSupport.Chain) ? 3 : 0;
        int leech = 0;
        int executeThreshold = 0;
        int execute = 10_000;
        int nonExecute = 10_000;
        damage = checked(damage * (10_000 + (Math.Clamp(configuration.Level, 1, 20) - 1) * 250) / 10_000);

        if (configuration.Supports.HasFlag(SkillSupport.IncreasedArea))
        {
            range = checked(range * 13_500 / 10_000);
            damage = checked(damage * 9_000 / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.Bleed)) bleed += 6_000;
        if (configuration.Supports.HasFlag(SkillSupport.LifeCost))
        {
            life = Math.Max(1, maximumLife * 800 / 10_000);
            mana = 0;
            damage = checked(damage * 13_000 / 10_000);
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
        if (configuration.Supports.HasFlag(SkillSupport.AttackSpeed) && definition.Tags.HasFlag(SkillTag.Attack))
        {
            cooldown = Math.Max(1, cooldown * 10_000 / 12_500);
        }
        return new P6ResolvedSkill(configuration.SkillId, mana, life, range, definition.CastTimeTicks, cooldown,
            damage, bleed, projectiles, projectileSpeed, chains, leech, executeThreshold, execute, nonExecute);
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

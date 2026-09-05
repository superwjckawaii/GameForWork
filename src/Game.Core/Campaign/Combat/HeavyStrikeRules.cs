using GameForWork.Core.Simulation;
using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Core.Campaign.Combat;

public sealed record HeavyStrikeRequest(
    ResourceState AttackerResources,
    SkillUseProfile Skill,
    WeaponProfile Weapon,
    int Accuracy,
    int TargetEvasion,
    int TargetArmor,
    int IncreasedDamageBasisPoints = 0,
    int AddedMinimumPhysicalDamage = 0,
    int AddedMaximumPhysicalDamage = 0,
    int IncreasedCriticalChanceBasisPoints = 0,
    int IncreasedBleedChanceBasisPoints = 0,
    WarCryState? WarCry = null,
    ChargedHeavyStrikeState? ChargedHeavyStrike = null);

public sealed record HeavyStrikeResult(bool CastSucceeded, string FailureReason, DamageResult? Damage);

public static class HeavyStrikeRules
{
    public static HeavyStrikeResult Resolve(HeavyStrikeRequest request, Pcg32 random, int tick)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(random);
        if (request.Skill.SkillId != SkillIds.HeavyStrike)
        {
            return new HeavyStrikeResult(false, "wrong_skill", null);
        }

        if (!SkillRules.TryPaySkillCost(request.AttackerResources, request.Skill))
        {
            return new HeavyStrikeResult(false, "insufficient_resource", null);
        }

        var multipliers = request.Skill.MoreDamageMultipliersBasisPoints.ToList();
        if (request.WarCry is not null)
        {
            multipliers.Add(request.WarCry.ConsumeHeavyStrikeMultiplier(tick));
        }

        if (request.ChargedHeavyStrike is not null)
        {
            multipliers.Add(request.ChargedHeavyStrike.ConsumeForHeavyStrike(tick));
        }

        int criticalChance = checked((int)((long)request.Weapon.CriticalChanceBasisPoints *
            (10_000 + request.IncreasedCriticalChanceBasisPoints) / 10_000));
        int physiqueIncrease = request.AttackerResources.Sheet.AttackDamageIncreaseFromPhysique().Value;
        var damageRequest = new DamageRequest(
            request.Weapon,
            AddedMinimumPhysicalDamage: request.AddedMinimumPhysicalDamage,
            AddedMaximumPhysicalDamage: request.AddedMaximumPhysicalDamage,
            IncreasedDamageBasisPoints: checked(request.IncreasedDamageBasisPoints + physiqueIncrease),
            MoreDamageMultipliersBasisPoints: multipliers,
            CriticalChanceBasisPoints: criticalChance,
            TargetArmor: request.TargetArmor,
            TargetEvasion: request.TargetEvasion,
            Accuracy: request.Accuracy,
            BleedChanceBasisPoints: checked(request.Skill.BleedChanceBasisPoints + request.IncreasedBleedChanceBasisPoints));
        return new HeavyStrikeResult(true, string.Empty, DamageRules.Resolve(damageRequest, random));
    }
}

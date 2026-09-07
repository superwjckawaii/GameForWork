using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Combat;

/// <summary>Charges each quarter second up front, retaining fractional credit across interruptions.</summary>
public sealed class ChannelCostState
{
    private readonly Dictionary<(string Skill, bool Life), int> _credit = [];
    private int Payment(ResolvedSkill skill) => Math.Max(0,
        ((skill.LifeCost > 0 ? skill.LifeCost : skill.ManaCost) - _credit.GetValueOrDefault((skill.SkillId, skill.LifeCost > 0)) + 3) / 4);
    public bool CanPay(ResourceState hero, ResolvedSkill skill) => hero.CanPaySkillCost(
        skill.LifeCost > 0 ? Payment(skill) : 0, skill.LifeCost > 0 ? 0 : Payment(skill), SkillDefinitions.Get(skill.SkillId).Tags,
        extraShield: GuardState.ShieldCost(skill.SkillId, hero.MaximumShield), waiveMana: skill.WaiveManaCost);
    public bool TryPay(ResourceState hero, ResolvedSkill skill, out int paid)
    {
        bool life = skill.LifeCost > 0;
        var key = (skill.SkillId, life);
        int numerator = (life ? skill.LifeCost : skill.ManaCost) - _credit.GetValueOrDefault(key);
        paid = Payment(skill);
        if (!hero.TryPaySkillCost(skill.SkillId, life ? paid : 0, life ? 0 : paid,
            extraShield: GuardState.ShieldCost(skill.SkillId, hero.MaximumShield), waiveMana: skill.WaiveManaCost)) return false;
        _credit[key] = paid * 4 - numerator;
        return true;
    }
}

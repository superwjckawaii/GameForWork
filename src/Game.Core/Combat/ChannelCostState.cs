using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Combat;

/// <summary>Charges each quarter second up front, retaining fractional credit across interruptions.</summary>
public sealed class ChannelCostState
{
    private readonly Dictionary<(string Skill, bool Life), int> _credit = [];
    public bool TryPay(ResourceState hero, ResolvedSkill skill, out int paid)
    {
        bool life = skill.LifeCost > 0;
        var key = (skill.SkillId, life);
        int numerator = (life ? skill.LifeCost : skill.ManaCost) - _credit.GetValueOrDefault(key);
        paid = Math.Max(0, (numerator + 3) / 4);
        if (!(life ? hero.TryPayLifeCost(paid) : hero.TryPayMana(paid))) return false;
        _credit[key] = paid * 4 - numerator;
        return true;
    }
}

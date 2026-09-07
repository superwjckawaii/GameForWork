using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;

namespace GameForWork.Core.Skills;

public static class CriticalHitRules
{
    public static int Chance(TeamBuild build, ResolvedSkill skill, SkillConfiguration configuration, int distance, int runtimeBaseBonus = 0)
    {
        var passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        SkillTag tags = SkillDefinitions.Get(skill.SkillId).Tags | skill.AdditionalTags;
        if (build.CannotCrit || MasteryRuntime.CannotCrit(passive, tags, build.Weapon) || skill.Role == SkillRole.DamageOverTime) return 0;
        int basis = tags.HasFlag(SkillTag.Spell) ? SpellHitRules.BaseCriticalChance(skill.SkillId, distance, configuration.Quality)
            : UnarmedRules.Source(skill.SkillId, build.Weapon).CriticalChanceBasisPoints + UnarmedRules.CriticalBonus(configuration);
        return CombatRules.CriticalChance(basis + runtimeBaseBonus + CriticalMasteryRules.BaseChanceBonus(passive, tags) +
            (configuration.Supports.HasFlag(SkillSupport.CriticalStrikes) ? CombatSkillRules.SupportValue(configuration, SkillSupport.CriticalStrikes) * 100 : 0), build.IncreasedCriticalChanceBasisPoints);
    }
    public static int Multiplier(TeamBuild build, SkillConfiguration configuration)
    {
        var link = configuration.Supports.HasFlag(SkillSupport.CriticalStrikes) ? CombatSkillRules.SupportLink(configuration, SkillSupport.CriticalStrikes) : null;
        return Math.Max(0, build.CriticalMultiplierBasisPoints + CriticalMasteryRules.MultiplierBonus(build.PassiveProfile ?? PassiveModifiers.Empty) +
            (link is null ? 0 : ActiveSkillCatalog.Interpolate(1_500, 3_000, link.Level, false) + link.Quality * 50));
    }
}

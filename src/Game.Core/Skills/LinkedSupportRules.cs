using GameForWork.Core.Archetypes;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;

namespace GameForWork.Core.Skills;

public static class LinkedSupportRules
{
    public static bool Support(SkillConfiguration skill, SupportMechanic support)
    {
        string id = ActiveSkillCatalog.SupportFor(support).StoneId;
        return skill.ExtendedSupports.Contains(support) || skill.ExtendedBuildsSupports.Contains(id) ||
            skill.ExtendedBuildsSupportLinks.Any(link => link.StoneId == id);
    }
    public static int SupportValue(SkillConfiguration skill, SupportMechanic support, int one, int maximum) => !Support(skill, support) ? 0 :
        ActiveSkillCatalog.Interpolate(one, maximum, skill.ExtendedBuildsSupportLinks.FirstOrDefault(link =>
            link.StoneId == ActiveSkillCatalog.SupportFor(support).StoneId)?.Level ?? skill.Level, false);
    public static int SupportQuality(SkillConfiguration skill, SupportMechanic support) => !Support(skill, support) ? 0 :
        Math.Clamp(skill.ExtendedBuildsSupportLinks.FirstOrDefault(link => link.StoneId == ActiveSkillCatalog.SupportFor(support).StoneId)?.Quality ?? skill.Quality, 0, 20);
    public static int QualityOverride(SkillConfiguration skill, SupportMechanic support, int normal, int atTwenty) =>
        normal + (atTwenty - normal) * SupportQuality(skill, support) / 20;
}

using GameForWork.Core.Art;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;

namespace GameForWork.Core.Presentation;

public static class SkillAnimation
{
    public static SpriteAction ForSkill(string skillId)
    {
        return ActiveSkillCatalog.TryActiveForSkill(skillId, out var skill) &&
            skill!.Combat.Tags.HasFlag(SkillTag.Attack) ? SpriteAction.Attack : SpriteAction.Cast;
    }
}

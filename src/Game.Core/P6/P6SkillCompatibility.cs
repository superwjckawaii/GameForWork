using GameForWork.Core.P2;

namespace GameForWork.Core.P6;

public sealed record P6CompatibilityResult(bool Compatible, string Reason);

public static class P6SkillCompatibility
{
    public static P6CompatibilityResult Check(SkillStoneDefinition active, SkillStoneDefinition support)
    {
        if (active.Kind != SkillStoneKind.Active || support.Kind != SkillStoneKind.Support)
        {
            return new P6CompatibilityResult(false, "需要一个主动技能和一个辅助技能。");
        }
        if ((active.Tags & support.SupportedTags) == 0)
        {
            return new P6CompatibilityResult(false, $"{support.DisplayName} 不支持 {active.DisplayName} 的标签。");
        }
        if ((active.Tags & support.ExcludedTags) != 0)
        {
            return new P6CompatibilityResult(false, $"{support.DisplayName} 排除了该技能标签。");
        }
        return new P6CompatibilityResult(true, string.Empty);
    }
}

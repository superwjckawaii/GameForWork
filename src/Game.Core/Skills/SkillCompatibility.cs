using GameForWork.Core.Management;

namespace GameForWork.Core.Skills;

public sealed record CompatibilityResult(bool Compatible, string Reason);

public static class SkillCompatibility
{
    public static CompatibilityResult Check(SkillStoneDefinition active, SkillStoneDefinition support)
    {
        if (active.Kind != SkillStoneKind.Active || support.Kind != SkillStoneKind.Support)
        {
            return new CompatibilityResult(false, "需要一个主动技能和一个辅助技能。");
        }
        if ((active.Capabilities & support.RequiredAllCapabilities) != support.RequiredAllCapabilities)
        {
            return new CompatibilityResult(false, $"{support.DisplayName} 要求的技能行为不完整。");
        }
        if (support.RequiredAnyCapabilities != 0 &&
            (active.Capabilities & support.RequiredAnyCapabilities) == 0)
        {
            return new CompatibilityResult(false, $"{support.DisplayName} 不支持 {active.DisplayName} 的执行能力。");
        }
        if ((active.Capabilities & support.ExcludedCapabilities) != 0)
        {
            return new CompatibilityResult(false, $"{support.DisplayName} 排除了该技能行为。");
        }
        return new CompatibilityResult(true, string.Empty);
    }

    public static CompatibilityResult CheckGroup(
        SkillStoneDefinition active,
        SkillStoneDefinition support,
        IEnumerable<SkillStoneDefinition> installedSupports)
    {
        CompatibilityResult basic = Check(active, support);
        if (!basic.Compatible) return basic;
        foreach (SkillStoneDefinition installed in installedSupports)
        {
            if ((support.ConflictsWith & installed.ProvidesConflict) != 0 ||
                (installed.ConflictsWith & support.ProvidesConflict) != 0)
                return new CompatibilityResult(false, $"{support.DisplayName} 与 {installed.DisplayName} 的规则互斥。");
        }
        return new CompatibilityResult(true, string.Empty);
    }
}

using GameForWork.Core.Campaign.Items;

namespace GameForWork.Core.Management;

public sealed record EquipmentComparison(
    int MaximumLifeDelta,
    int MaximumManaDelta,
    int ArmorDelta,
    int EvasionDelta,
    int ShieldDelta,
    int CoreCapacityDelta,
    int LinkCapacityDelta,
    int AverageHitDelta,
    int EffectiveLifeDelta,
    bool RequirementsMet,
    int DisabledSkillLinks,
    EquipmentSlot TargetSlot,
    int LinkedSocketDelta = 0,
    IReadOnlyList<string>? EjectedSkillStones = null,
    IReadOnlyList<string>? RetainedSkillStones = null);

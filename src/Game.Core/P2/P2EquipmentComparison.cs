using GameForWork.Core.P1.Items;

namespace GameForWork.Core.P2;

public sealed record P2EquipmentComparison(
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
    EquipmentSlot TargetSlot);

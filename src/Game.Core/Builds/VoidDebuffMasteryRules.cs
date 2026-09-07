using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;

namespace GameForWork.Core.Builds;

public static class VoidDebuffMasteryRules
{
    public static int Maximum(PassiveModifiers profile, Ailment kind) => kind == Ailment.Erosion
        ? Math.Min(10, 5 + profile.SpecializedValue(PassiveEffectKind.AdditionalErosionMaximum) + (MasteryRuntime.Has(profile, "侵蚀_凋零", 0) ? 3 : 0))
        : 10 + (MasteryRuntime.Has(profile, "侵蚀_凋零", 1) ? 5 : 0);
    public static int HitMultiplier(PassiveModifiers profile, AilmentState target, int tick) =>
        MasteryRuntime.Has(profile, "侵蚀_凋零", 5) && target.Stack(Ailment.Erosion, tick) >= 5 && target.Stack(Ailment.Wither, tick) >= 10 ? 14_000 : 10_000;
    public static int IncomingHitMultiplier(PassiveModifiers profile, AilmentState target, int tick) =>
        MasteryRuntime.Has(profile, "侵蚀_凋零", 6) && target.Stack(Ailment.Wither, tick) >= 10 ? 8_500 : 10_000;
}

using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Core.Builds;

public static class AilmentMasteryRules
{
    public static string? ExclusiveMode(string? group, int option) => (group, option) switch
    {
        ("builds.mastery.流血", 0 or 1) or ("builds.mastery.斧类", 2) => "bleed",
        ("builds.mastery.点燃", 0 or 1) => "ignite",
        _ => null,
    };
    public static void Configure(AilmentState state, PassiveModifiers passive, bool twinBleeds = false, bool twinIgnites = false)
    {
        bool Bleed(int option) => MasteryRuntime.Has(passive, "流血", option);
        bool Ignite(int option) => MasteryRuntime.Has(passive, "点燃", option);
        state.BleedMaximum = Bleed(0) ? 1 : Bleed(1) ? 8 : twinBleeds ? 2 : 1;
        state.BleedMultiplierAlways = Bleed(1) && !Bleed(0);
        state.BleedMultiplier = state.BleedMultiplierAlways ? 5_000 : twinBleeds && !Bleed(0) ? 8_000 : 10_000;
        state.IgniteMaximum = Ignite(0) ? 1 : Ignite(1) || twinIgnites ? 2 : 1;
        state.IgniteMultiplierAlways = Ignite(1) && !Ignite(0);
        state.IgniteMultiplier = state.IgniteMultiplierAlways ? 7_000 : twinIgnites && !Ignite(0) ? 8_000 : 10_000;
    }
    public static int BleedingTargetHitMultiplier(PassiveModifiers passive, AilmentState target) =>
        MasteryRuntime.Has(passive, "流血", 2) && target.Count(Ailment.Bleed) > 0 ? 13_500 : 10_000;
    public static int LifeRecoveryMultiplier(PassiveModifiers passive, AilmentState target) =>
        MasteryRuntime.Has(passive, "点燃", 6) && target.Count(Ailment.Ignite) > 0 ? 4_000 : 10_000;
}

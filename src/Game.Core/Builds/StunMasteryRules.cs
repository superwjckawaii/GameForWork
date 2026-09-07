using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Core.Builds;

public static class StunMasteryRules
{
    private static bool Has(PassiveModifiers profile, int option) => MasteryRuntime.Has(profile, "眩晕", option);
    public static int Chance(PassiveModifiers profile, int damage, int threshold) => CombatRules.StunChance(
        (int)Math.Min(int.MaxValue, (long)damage * (Has(profile, 0) ? 2 : 1)), threshold, (Has(profile, 1) ? 4_000 : 0) + profile.SpecializedValue(PassiveEffectKind.ReducedStunThresholdBasisPoints),
        profile.SpecializedValue(PassiveEffectKind.IncreasedStunChanceBasisPoints));
    public static int Duration(PassiveModifiers profile, int maximum, int reduction = 0) => Math.Min(maximum,
        CombatRules.ApplyIncreased(CombatRules.ApplyIncreased(350, (Has(profile, 2) ? 15_000 : 0) + profile.SpecializedValue(PassiveEffectKind.IncreasedStunDurationBasisPoints)), -Math.Clamp(reduction, 0, 10_000)));
    public static int HitMultiplier(PassiveModifiers profile, int tick, int until) => Has(profile, 3) && tick < until ? 13_500 : 10_000;
    public static int SpeedMultiplier(PassiveModifiers profile, int tick, int until) => Has(profile, 5) && tick < until ? 12_000 : 10_000;
}

using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Builds;

public static class CriticalMasteryRules
{
    private static bool Has(PassiveModifiers profile, int option) => MasteryRuntime.Has(profile, "暴击", option);
    public static int BaseChanceBonus(PassiveModifiers profile, SkillTag tags) =>
        (tags & (SkillTag.Attack | SkillTag.Spell)) != 0 && Has(profile, 0) ? 200 : 0;
    public static bool Roll(PassiveModifiers profile, int chance, Pcg32 random)
    {
        chance = Math.Clamp(chance, 0, 10_000);
        bool first = random.NextBasisPoints() < chance;
        bool second = Has(profile, 1) && random.NextBasisPoints() < chance;
        return first || second;
    }
    public static int ExpectedChance(PassiveModifiers profile, int chance) => Has(profile, 1)
        ? 10_000 - (int)((long)(10_000 - chance) * (10_000 - chance) / 10_000) : chance;
    public static int MultiplierBonus(PassiveModifiers profile) => (Has(profile, 2) ? 10_000 : 0) - (Has(profile, 4) ? 5_000 : 0);
    public static int AilmentMultiplierBonus(PassiveModifiers profile) => Has(profile, 4) ? 10_000 : 0;
    public static int AilmentDamageMultiplier(PassiveModifiers profile) => Has(profile, 2) ? 7_500 : 10_000;
    public static int TargetMultiplier(PassiveModifiers profile, bool critical, bool controlled) => critical && controlled && Has(profile, 3) ? 13_000 : 10_000;
    public static int ConvertedHitIncrease(PassiveModifiers profile, int increasedCriticalChance) => Has(profile, 6) ? (int)((long)increasedCriticalChance * 75 / 100) : 0;
    public static int RecentChanceIncrease(PassiveModifiers profile, int tick, int until) => Has(profile, 5) && tick < until ? 15_000 : 0;
    public static int RecentMultiplierBonus(PassiveModifiers profile, int tick, int until) => Has(profile, 5) && tick < until ? 5_000 : 0;
}

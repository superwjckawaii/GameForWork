using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Builds;

public static class SuppressionMasteryRules
{
    private static bool Has(PassiveModifiers profile, int option) => MasteryRuntime.Has(profile, "法术压制", option);
    public static int ChanceBonus(PassiveModifiers profile) => (Has(profile, 0) ? 2_000 : 0) - (Has(profile, 1) ? 2_000 : 0);
    public static int EffectBonus(PassiveModifiers profile) => (Has(profile, 0) ? 500 : 0) + (Has(profile, 1) ? 1_000 : 0);
    public static bool Roll(PassiveModifiers profile, int chance, Pcg32 random)
    {
        chance = Math.Clamp(chance, 0, 10_000);
        bool first = random.NextBasisPoints() < chance;
        bool second = Has(profile, 2) && random.NextBasisPoints() < chance;
        return first || second;
    }
    public static int ExpectedChance(PassiveModifiers profile, int chance)
    {
        chance = Math.Clamp(chance, 0, 10_000);
        return Has(profile, 2) ? 10_000 - (int)((long)(10_000 - chance) * (10_000 - chance) / 10_000) : chance;
    }
    public static int UnsuppressedMultiplier(PassiveModifiers profile) => Has(profile, 3) ? 8_000 : 10_000;
    public static int HitMultiplier(PassiveModifiers profile, int tick, int until) => Has(profile, 5) && tick < until ? 13_500 : 10_000;
    public static int OverflowHitIncrease(PassiveModifiers profile, int rawChance) => Has(profile, 6) ?
        (int)Math.Min(int.MaxValue, Math.Max(0, (long)rawChance - 10_000) / 100 * 300) : 0;
    public static CharacterSheet Recovery(CharacterSheet sheet, PassiveModifiers profile, int tick, int until) => Has(profile, 4) && tick < until
        ? sheet with
        {
            MaximumLifeRegenerationBasisPoints = sheet.MaximumLifeRegenerationBasisPoints + 200,
            MaximumShieldRegenerationBasisPoints = sheet.MaximumShieldRegenerationBasisPoints + 200
        } : sheet;
}

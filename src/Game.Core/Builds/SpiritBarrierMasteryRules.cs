using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
namespace GameForWork.Core.Builds;
public static class SpiritBarrierMasteryRules
{
    public static bool Has(PassiveModifiers p, int option) => MasteryRuntime.Has(p, "灵障", option);
    public static CharacterSheet Dynamic(CharacterSheet s, PassiveModifiers p, bool lowLife, int tick, int hitUntil) => s with
    { ConditionalSpiritBarrierMultiplierBasisPoints = CombatRules.ApplyMore(10000, [Has(p, 3) && lowLife ? 20000 : 10000, Has(p, 5) && tick >= hitUntil ? 18000 : 10000]) };
    public static int DamageIncrease(CharacterSheet s, PassiveModifiers p) => Has(p, 6) ? (int)Math.Min(int.MaxValue, s.SpiritBarrier().Value / 100L * 800) : 0;
    public static CharacterSheet Recovery(CharacterSheet s, PassiveModifiers p, int tick, int dotUntil) => Has(p, 4) && tick < dotUntil
        ? s with { MaximumLifeRegenerationBasisPoints = s.MaximumLifeRegenerationBasisPoints + 150, MaximumShieldRegenerationBasisPoints = s.MaximumShieldRegenerationBasisPoints + 150 } : s;
}

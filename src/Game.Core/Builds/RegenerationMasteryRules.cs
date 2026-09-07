using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
namespace GameForWork.Core.Builds;

public static class RegenerationMasteryRules
{
    public static bool Has(PassiveModifiers p, int option) => MasteryRuntime.Has(p, "再生_持续恢复", option);
    public static CharacterSheet Apply(CharacterSheet s, PassiveModifiers p) => s with
    {
        LifeRegenerationMultiplierBasisPoints = CombatRules.ApplyMore(s.LifeRegenerationMultiplierBasisPoints, [Has(p, 0) ? 15000 : 10000, Has(p, 1) ? 7000 : 10000, Has(p, 2) ? 13000 : 10000]),
        ShieldRegenerationMultiplierBasisPoints = CombatRules.ApplyMore(s.ShieldRegenerationMultiplierBasisPoints, [Has(p, 0) ? 7000 : 10000, Has(p, 1) ? 16000 : 10000, Has(p, 2) ? 13000 : 10000]),
        IncreasedLifeRegenerationBasisPoints = s.IncreasedLifeRegenerationBasisPoints + (Has(p, 6) ? 10000 : 0),
        IncreasedShieldRegenerationBasisPoints = s.IncreasedShieldRegenerationBasisPoints + (Has(p, 6) ? 10000 : 0),
        ConvertLifeRegenerationToShield = s.ConvertLifeRegenerationToShield || Has(p, 5)
    };
    public static CharacterSheet Recent(CharacterSheet s, PassiveModifiers p, int tick, int hitUntil, bool lowLife = false, bool lowShield = false)
    {
        int multiplier = tick < hitUntil ? Has(p, 3) ? 14000 : 10000 : Has(p, 4) ? 16000 : 10000;
        return s with
        {
            LifeRegenerationMultiplierBasisPoints = CombatRules.ApplyMore(s.LifeRegenerationMultiplierBasisPoints, [multiplier, lowLife ? 10000 + p.SpecializedValue(PassiveEffectKind.LowLifeMoreRegenerationBasisPoints) : 10000]),
            ShieldRegenerationMultiplierBasisPoints = CombatRules.ApplyMore(s.ShieldRegenerationMultiplierBasisPoints, [multiplier, lowShield ? 10000 + p.SpecializedValue(PassiveEffectKind.LowShieldMoreRegenerationBasisPoints) : 10000])
        };
    }
}

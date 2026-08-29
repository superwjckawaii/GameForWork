namespace GameForWork.Core.P24;

public sealed record P24SupportProfile(
    int DamageMultiplierBasisPoints = 10_000,
    int RangeMultiplierBasisPoints = 10_000,
    int CastSpeedBasisPoints = 10_000,
    int CooldownRecoveryBasisPoints = 10_000,
    int ProjectileCount = 0,
    int PierceCount = 0,
    int ChainCount = 0,
    int AdditionalMinionMaximum = 0,
    int AdditionalTrapCount = 0,
    int AdditionalPhantomMaximum = 0,
    int ResistancePenetrationBasisPoints = 0,
    int EnergyShieldLeechBasisPoints = 0,
    bool MoveWhileUsing = false,
    bool PaysEnergyShield = false,
    bool Propagates = false,
    bool RepeatsAtDestination = false);

public static class P24SupportRules
{
    public static P24SupportProfile Resolve(IEnumerable<P24SupportMechanic> supports)
    {
        int damage = 10_000;
        int range = 10_000;
        int cast = 10_000;
        int recovery = 10_000;
        int projectiles = 0;
        int pierce = 0;
        int chains = 0;
        int minions = 0;
        int traps = 0;
        int phantoms = 0;
        int penetration = 0;
        int shieldLeech = 0;
        bool moving = false;
        bool shieldCost = false;
        bool propagates = false;
        bool repeats = false;
        foreach (P24SupportMechanic support in supports.Distinct())
        {
            switch (support)
            {
                case P24SupportMechanic.FarShot: range = Mul(range, 12_000); break;
                case P24SupportMechanic.PrecisionPierce: pierce += 2; damage = Mul(damage, 9_000); break;
                case P24SupportMechanic.SeekingChain: chains += 2; damage = Mul(damage, 8_000); break;
                case P24SupportMechanic.MobileAttack: moving = true; damage = Mul(damage, 8_800); break;
                case P24SupportMechanic.ToxinSpread or P24SupportMechanic.HexSpread: propagates = true; break;
                case P24SupportMechanic.MultipleTraps: traps += 2; damage = Mul(damage, 7_500); break;
                case P24SupportMechanic.MarkAmplify: damage = Mul(damage, 11_000); break;
                case P24SupportMechanic.BackstabAmplify: damage = Mul(damage, 13_000); break;
                case P24SupportMechanic.MinionAmplify or P24SupportMechanic.ConstructAmplify: damage = Mul(damage, 13_000); break;
                case P24SupportMechanic.SwiftMinions: recovery = Mul(recovery, 13_000); break;
                case P24SupportMechanic.ExpandedArmy: minions += 1; damage = Mul(damage, 8_500); break;
                case P24SupportMechanic.AuraAmplify or P24SupportMechanic.StanceAmplify: damage = Mul(damage, 12_500); break;
                case P24SupportMechanic.LastingBlessing or P24SupportMechanic.VoidDuration: recovery = Mul(recovery, 8_000); break;
                case P24SupportMechanic.DeepHex or P24SupportMechanic.DeepWither: cast = Mul(cast, 8_500); damage = Mul(damage, 13_000); break;
                case P24SupportMechanic.FirePenetration or P24SupportMechanic.ColdPenetration or P24SupportMechanic.LightningPenetration:
                    penetration = Math.Max(penetration, 2_500); break;
                case P24SupportMechanic.ElementalAilment: damage = Mul(damage, 9_000); break;
                case P24SupportMechanic.ShieldLeech: shieldLeech += 200; break;
                case P24SupportMechanic.ShieldCasting: shieldCost = true; damage = Mul(damage, 12_000); break;
                case P24SupportMechanic.UnarmedFocus: damage = Mul(damage, 13_500); break;
                case P24SupportMechanic.MovementEcho: repeats = true; damage = Mul(damage, 6_000); break;
                case P24SupportMechanic.FerociousBeast: damage = Mul(damage, 13_500); break;
                case P24SupportMechanic.PhantomCopy: phantoms += 1; damage = Mul(damage, 7_500); break;
                case P24SupportMechanic.PhantomSacrifice: damage = Mul(damage, 12_000); break;
                case P24SupportMechanic.Spellblade: damage = Mul(damage, 11_500); break;
                case P24SupportMechanic.AttackTrigger: damage = Mul(damage, 6_500); break;
                case P24SupportMechanic.ImprintGain: damage = Mul(damage, 8_500); break;
                case P24SupportMechanic.ImprintBurst: damage = Mul(damage, 12_500); break;
                case P24SupportMechanic.ShieldBreakAmplify: damage = Mul(damage, 14_000); break;
                case P24SupportMechanic.RapidRebuild: recovery = Mul(recovery, 20_000); break;
            }
        }
        return new P24SupportProfile(damage, range, cast, recovery, projectiles, pierce, chains, minions, traps,
            phantoms, penetration, shieldLeech, moving, shieldCost, propagates, repeats);
    }

    private static int Mul(int left, int right) => checked(left * right / 10_000);
}

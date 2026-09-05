namespace GameForWork.Core.Archetypes;

public sealed record SupportProfile(
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

public static class SupportRules
{
    public static SupportProfile Resolve(IEnumerable<SupportMechanic> supports)
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
        foreach (SupportMechanic support in supports.Distinct())
        {
            switch (support)
            {
                case SupportMechanic.FarShot: range = Mul(range, 12_000); break;
                case SupportMechanic.PrecisionPierce: pierce += 2; damage = Mul(damage, 9_000); break;
                case SupportMechanic.SeekingChain: chains += 2; damage = Mul(damage, 8_000); break;
                case SupportMechanic.MobileAttack: moving = true; damage = Mul(damage, 8_800); break;
                case SupportMechanic.ToxinSpread or SupportMechanic.HexSpread: propagates = true; break;
                case SupportMechanic.MultipleTraps: traps += 2; damage = Mul(damage, 7_500); break;
                case SupportMechanic.MarkAmplify: damage = Mul(damage, 11_000); break;
                case SupportMechanic.BackstabAmplify: damage = Mul(damage, 13_000); break;
                case SupportMechanic.MinionAmplify or SupportMechanic.ConstructAmplify: damage = Mul(damage, 13_000); break;
                case SupportMechanic.SwiftMinions: recovery = Mul(recovery, 13_000); break;
                case SupportMechanic.ExpandedArmy: minions += 1; damage = Mul(damage, 8_500); break;
                case SupportMechanic.AuraAmplify or SupportMechanic.StanceAmplify: damage = Mul(damage, 12_500); break;
                case SupportMechanic.LastingBlessing or SupportMechanic.VoidDuration: recovery = Mul(recovery, 8_000); break;
                case SupportMechanic.DeepHex or SupportMechanic.DeepWither: cast = Mul(cast, 8_500); damage = Mul(damage, 13_000); break;
                case SupportMechanic.FirePenetration or SupportMechanic.ColdPenetration or SupportMechanic.LightningPenetration:
                    penetration = Math.Max(penetration, 2_500); break;
                case SupportMechanic.ElementalAilment: damage = Mul(damage, 9_000); break;
                case SupportMechanic.ShieldLeech: shieldLeech += 200; break;
                case SupportMechanic.ShieldCasting: shieldCost = true; damage = Mul(damage, 12_000); break;
                case SupportMechanic.UnarmedFocus: damage = Mul(damage, 13_500); break;
                case SupportMechanic.MovementEcho: repeats = true; damage = Mul(damage, 6_000); break;
                case SupportMechanic.FerociousBeast: damage = Mul(damage, 13_500); break;
                case SupportMechanic.PhantomCopy: phantoms += 1; damage = Mul(damage, 7_500); break;
                case SupportMechanic.PhantomSacrifice: damage = Mul(damage, 12_000); break;
                case SupportMechanic.Spellblade: damage = Mul(damage, 11_500); break;
                case SupportMechanic.AttackTrigger: damage = Mul(damage, 6_500); break;
                case SupportMechanic.ImprintGain: damage = Mul(damage, 8_500); break;
                case SupportMechanic.ImprintBurst: damage = Mul(damage, 12_500); break;
                case SupportMechanic.ShieldBreakAmplify: damage = Mul(damage, 14_000); break;
                case SupportMechanic.RapidRebuild: recovery = Mul(recovery, 20_000); break;
            }
        }
        return new SupportProfile(damage, range, cast, recovery, projectiles, pierce, chains, minions, traps,
            phantoms, penetration, shieldLeech, moving, shieldCost, propagates, repeats);
    }

    private static int Mul(int left, int right) => checked(left * right / 10_000);
}

using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
namespace GameForWork.Core.Builds;
public static class BlockMasteryRules
{
    public static bool Has(PassiveModifiers p, int option) => MasteryRuntime.Has(p, "格挡", option);
    public static CharacterSheet Apply(CharacterSheet s, PassiveModifiers p) => s with
    {
        BlockChanceBasisPoints = s.BlockChanceBasisPoints + (Has(p, 0) ? 1000 : 0),
        MaximumBlockChanceBasisPoints = Math.Min(9000, s.MaximumBlockChanceBasisPoints + (Has(p, 0) ? 500 : 0)),
        SpellBlockChanceBasisPoints = s.SpellBlockChanceBasisPoints + (Has(p, 1) ? 1000 : 0),
        MaximumSpellBlockChanceBasisPoints = Math.Min(9000, s.MaximumSpellBlockChanceBasisPoints + (Has(p, 1) ? 500 : 0))
    };
    public static int UnblockedMultiplier(PassiveModifiers p, int tick, int until) => tick < until ? Math.Max(0, 10000 - p.SpecializedValue(PassiveEffectKind.RecentBlockUnblockedHitLessBasisPoints)) : 10000;
    public static int Chance(PassiveModifiers p, int raw) => (int)Math.Min(int.MaxValue, (long)Math.Max(0, raw) * (Has(p, 3) ? 2 : 1));
    public static int RemainingMultiplier(PassiveModifiers p, int other = 0) => Math.Max(other, Has(p, 3) ? 3500 : 0);
    public static CharacterSheet Recovery(CharacterSheet s, PassiveModifiers p, int tick, int until) => Has(p, 4) && tick < until
        ? s with { MaximumLifeRegenerationBasisPoints = s.MaximumLifeRegenerationBasisPoints + 200, MaximumShieldRegenerationBasisPoints = s.MaximumShieldRegenerationBasisPoints + 200 } : s;
}

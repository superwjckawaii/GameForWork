using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Core.Builds;

public static class ResistanceMasteryRules
{
    private static bool Element(PassiveModifiers p, int option) => MasteryRuntime.Has(p, "元素抗性", option);
    private static bool Void(PassiveModifiers p, int option) => MasteryRuntime.Has(p, "虚空抗性", option);
    public static CharacterSheet Apply(CharacterSheet s, PassiveModifiers p)
    {
        int resistance = (Element(p, 0) ? 1_500 : 0) - (Element(p, 1) ? 4_000 : 0);
        return s with
        {
            MaximumElementalResistanceBasisPoints = s.MaximumElementalResistanceBasisPoints + (Element(p, 0) ? 300 : 0) - (Void(p, 1) ? 300 : 0),
            MaximumVoidResistanceBasisPoints = s.MaximumVoidResistanceBasisPoints + (Void(p, 0) ? 300 : 0) + (Void(p, 1) ? 500 : 0),
            FireResistanceBasisPoints = s.FireResistanceBasisPoints + resistance,
            ColdResistanceBasisPoints = s.ColdResistanceBasisPoints + resistance,
            LightningResistanceBasisPoints = s.LightningResistanceBasisPoints + resistance,
            VoidResistanceBasisPoints = s.VoidResistanceBasisPoints + (Void(p, 0) ? 2_000 : 0),
            EqualElementalMaximum = s.EqualElementalMaximum || Element(p, 1),
            ElementalOverflowDefenseRate = s.ElementalOverflowDefenseRate + (Element(p, 2) ? 400 : 0),
            VoidOverflowBarrierRate = s.VoidOverflowBarrierRate + (Void(p, 2) ? 600 : 0),
        };
    }
    public static bool ElementalCapped(CharacterSheet s) => s.FireResistanceBasisPoints >= s.ResistanceMaximum(EnemyDamageType.Fire) &&
        s.ColdResistanceBasisPoints >= s.ResistanceMaximum(EnemyDamageType.Cold) && s.LightningResistanceBasisPoints >= s.ResistanceMaximum(EnemyDamageType.Lightning);
    public static int IncomingMultiplier(CharacterSheet s, PassiveModifiers p, EnemyDamageType type, bool hit)
    {
        int result = 10_000;
        if (type is EnemyDamageType.Fire or EnemyDamageType.Cold or EnemyDamageType.Lightning)
        {
            if (hit && Element(p, 3)) result = result * 8_500 / 10_000;
            if (!hit && Element(p, 5)) result = result * 8_000 / 10_000;
            if (Element(p, 6) && ElementalCapped(s)) result = result * 9_000 / 10_000;
        }
        if (type == EnemyDamageType.Physical && hit && Element(p, 3)) result = result * 11_000 / 10_000;
        if (type == EnemyDamageType.Void)
        {
            if (!hit && Void(p, 3)) result = result * 7_500 / 10_000;
            if (hit && Void(p, 6) && s.VoidResistanceBasisPoints >= s.ResistanceMaximum(type)) result = result * 8_500 / 10_000;
        }
        return result;
    }
    public static int OutgoingMultiplier(PassiveModifiers p, DamageType type, bool hit, int tick, int elementalUntil, int voidUntil) =>
        type == DamageType.Void && Void(p, 5) && tick < voidUntil ||
        type is DamageType.Fire or DamageType.Cold or DamageType.Lightning && hit && Element(p, 4) && tick < elementalUntil ? 14_000 : 10_000;
    public static CharacterSheet Recovery(CharacterSheet s, PassiveModifiers p, int tick, int voidUntil) => Void(p, 4) && tick < voidUntil
        ? s with { MaximumLifeRegenerationBasisPoints = s.MaximumLifeRegenerationBasisPoints + 200, MaximumShieldRegenerationBasisPoints = s.MaximumShieldRegenerationBasisPoints + 200 } : s;
}

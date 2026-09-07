using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Core.Builds;

public static class ElementalControlMasteryRules
{
    public static int ChillMaximum(PassiveModifiers profile) => MasteryRuntime.Has(profile, "冰缓_冻结", 0) ? 5_000 : 3_000;
    public static int FreezeDamage(PassiveModifiers profile, int damage) => (int)Math.Min(int.MaxValue, (long)damage * (MasteryRuntime.Has(profile, "冰缓_冻结", 2) ? 3 : 1));
    public static int ColdHitMultiplier(PassiveModifiers profile, int tick, int until) => MasteryRuntime.Has(profile, "冰缓_冻结", 3) && tick < until ? 13_000 : 10_000;
    public static int ColdGroundChill(PassiveModifiers profile) => MasteryRuntime.Has(profile, "冰缓_冻结", 4) ? 1_500 : 0;
    public static bool CanSpreadChill(PassiveModifiers profile, int effect, int tick, int until, bool propagated) =>
        MasteryRuntime.Has(profile, "冰缓_冻结", 5) && effect > 0 && tick < until && !propagated;
    public static int FrozenExplosionDamage(PassiveModifiers profile, int maximumLife, bool frozen) =>
        frozen && MasteryRuntime.Has(profile, "冰缓_冻结", 6) ? Math.Max(1, maximumLife * 800 / 10_000) : 0;
    public static int ShockMaximum(PassiveModifiers profile, int maximum) => Math.Min(10_000, maximum + (MasteryRuntime.Has(profile, "感电_麻痹", 0) ? 5_000 : 0));
    public static bool CanShock(PassiveModifiers profile) => !MasteryRuntime.Has(profile, "感电_麻痹", 2);
    public static bool CanParalyze(PassiveModifiers profile) => !MasteryRuntime.Has(profile, "感电_麻痹", 4);
    public static int ShockEffect(PassiveModifiers profile, int effect)
    {
        if (effect <= 0) return MasteryRuntime.Has(profile, "感电_麻痹", 1) ? 1_500 : 0;
        return MasteryRuntime.Has(profile, "感电_麻痹", 1) ? Math.Max(1_500, effect) : effect;
    }
    public static int ParalysisChance(PassiveModifiers profile, int chance) => MasteryRuntime.Has(profile, "感电_麻痹", 2) ? chance + 10_000 : chance;
    public static int ParalysisAccumulationIncrease(PassiveModifiers profile) =>
        (MasteryRuntime.Has(profile, "感电_麻痹", 2) ? 5_000 : 0) + (MasteryRuntime.Has(profile, "感电_麻痹", 3) ? 15_000 : 0);
    public static int LightningDamage(PassiveModifiers profile, int damage, bool critical) => (int)Math.Min(int.MaxValue, (long)damage * (!critical && MasteryRuntime.Has(profile, "感电_麻痹", 6) ? 2 : 1));
    public static int ParalysisDecayDelayTicks(PassiveModifiers profile) => MasteryRuntime.Has(profile, "感电_麻痹", 3) ? 100 : 40;
    public static int HitMultiplier(PassiveModifiers profile, int tick, int until) => MasteryRuntime.Has(profile, "感电_麻痹", 5) && tick < until ? 14_000 : 10_000;
}

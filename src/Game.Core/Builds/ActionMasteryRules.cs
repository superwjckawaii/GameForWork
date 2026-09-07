using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.Combat;

namespace GameForWork.Core.Builds;

public static class ActionMasteryRules
{
    public static int SkillCostMultiplier(PassiveModifiers profile, bool triggered) => triggered && MasteryRuntime.Has(profile, "触发_冷却", 0) ? 5_000 : 10_000;
    public static int DamageMultiplier(PassiveModifiers profile, SkillTag tags, bool triggered)
    {
        int result = triggered && MasteryRuntime.Has(profile, "触发_冷却", 0) ? 8_000 : 10_000;
        return tags.HasFlag(SkillTag.Channelling) && MasteryRuntime.Has(profile, "重复_引导", 3) ? Multiply(result, 8_000) : result;
    }
    public static int IncomingHitMultiplier(PassiveModifiers profile, bool channeling) => channeling && MasteryRuntime.Has(profile, "重复_引导", 4) ? 8_500 : 10_000;
    public static int RepeatDelayMultiplier(PassiveModifiers profile) => MasteryRuntime.Has(profile, "重复_引导", 1) ? 7_500 : 10_000;
    public static int RepeatDamageMultiplier(PassiveModifiers profile, int repeatIndex, int repeatCount)
    {
        int result = MasteryRuntime.Has(profile, "重复_引导", 1) ? 8_000 : 10_000;
        return MasteryRuntime.Has(profile, "重复_引导", 0) ? Multiply(result, repeatIndex == repeatCount ? 20_000 : 8_000) : result;
    }
    private static int Multiply(int left, int right) => checked((int)((long)left * right / 10_000));
}

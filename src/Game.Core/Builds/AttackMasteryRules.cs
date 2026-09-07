using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Core.Builds;

public static class AttackMasteryRules
{
    public static int AddedDamage(PassiveModifiers profile, int damage) => CombatRules.ApplyMore(damage, [MasteryRuntime.Has(profile, "攻击", 1) ? 14_000 : 10_000]);
    public static int CooldownRecovery(PassiveModifiers profile, SkillTag tags) => tags.HasFlag(SkillTag.Attack) && MasteryRuntime.Has(profile, "攻击", 3) ? 6_000 : 0;
    public static int DamageIncrease(PassiveModifiers profile, SkillTag tags) => tags.HasFlag(SkillTag.Attack) && MasteryRuntime.Has(profile, "攻击", 3) ? 5_000 : 0;
    public static int TargetMultiplier(PassiveModifiers profile, bool rare) => MasteryRuntime.Has(profile, "攻击", 4) ? rare ? 13_500 : 8_000 : 10_000;
    public static int ActivationMultiplier(PassiveModifiers profile, bool self) => MasteryRuntime.Has(profile, "攻击", 5) ? self ? 13_000 : 7_000 : 10_000;
}

using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;

namespace GameForWork.Core.Builds;

public static class DamageOverTimeMasteryRules
{
    private const string Group = "持续伤害通用";

    public static int HitMultiplier(PassiveModifiers passives) => Has(passives, 0) ? 7_000 : 10_000;
    public static int DurationMultiplier(PassiveModifiers passives) => Has(passives, 1) ? 18_000 : 10_000;
    public static int FasterAilments(PassiveModifiers passives) => Has(passives, 2) ? 5_000 : 0;
    public static int OutputMultiplier(PassiveModifiers passives, bool ailment)
    {
        int multiplier = Has(passives, 0) ? 14_000 : 10_000;
        if (Has(passives, 3)) multiplier = Apply(multiplier, ailment ? 7_500 : 14_000);
        return multiplier;
    }
    public static int NewEffectMultiplier(PassiveModifiers passives, AilmentState target, DamageType type) =>
        Has(passives, 4) && target.HasActiveTypeOtherThan(type) ? 13_000 : 10_000;
    public static int IncomingHitMultiplier(PassiveModifiers passives, AilmentState source) =>
        Has(passives, 5) && source.HasActiveDamage ? 9_000 : 10_000;
    public static bool RecoversOnKill(PassiveModifiers passives) => Has(passives, 6);

    private static bool Has(PassiveModifiers passives, int option) => MasteryRuntime.Has(passives, Group, option);
    private static int Apply(int value, int multiplier) => checked(value * multiplier / 10_000);
}

public sealed class DamageOverTimeRecoveryState
{
    private int _windowSecond = -1;
    private int _uses;

    public bool TryRecover(int tick)
    {
        int second = tick / 20;
        if (second != _windowSecond) { _windowSecond = second; _uses = 0; }
        if (_uses >= 5) return false;
        _uses++;
        return true;
    }
}

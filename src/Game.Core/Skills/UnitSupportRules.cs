using GameForWork.Core.Archetypes;
using GameForWork.Core.Campaign.Combat;

namespace GameForWork.Core.Skills;

public static class UnitSupportRules
{
    public static bool Applies(CombatUnitKind kind, SupportMechanic support) => support switch
    {
        SupportMechanic.MinionAmplify or SupportMechanic.SwiftMinions or
            SupportMechanic.ExpandedArmy or SupportMechanic.Bodyguard => kind == CombatUnitKind.Minion,
        SupportMechanic.FerociousBeast or SupportMechanic.GuardianBeast => kind == CombatUnitKind.Companion,
        SupportMechanic.ConstructAmplify or SupportMechanic.RapidRebuild => kind == CombatUnitKind.Construct,
        _ => false,
    };

    public static int Value(SkillConfiguration skill, CombatUnitKind kind, SupportMechanic support,
        int levelOne, int levelTwentyOne) => Applies(kind, support)
            ? LinkedSupportRules.SupportValue(skill, support, levelOne, levelTwentyOne)
            : 0;

    public static int Quality(SkillConfiguration skill, CombatUnitKind kind, SupportMechanic support) =>
        Applies(kind, support) ? LinkedSupportRules.SupportQuality(skill, support) : 0;

    public static bool Has(SkillConfiguration skill, CombatUnitKind kind, SupportMechanic support) =>
        Applies(kind, support) && LinkedSupportRules.Support(skill, support);
}

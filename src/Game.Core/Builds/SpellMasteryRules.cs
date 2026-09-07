using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
namespace GameForWork.Core.Builds;
public static class SpellMasteryRules
{
    public static bool Has(PassiveModifiers p, int option) => MasteryRuntime.Has(p, "法术", option);
    public static bool Self(string id, SkillTag tags, bool triggered = false) => tags.HasFlag(SkillTag.Spell) && !triggered && !tags.HasFlag(SkillTag.Trigger) && !tags.HasFlag(SkillTag.Counter) && id != "archetypes.skill.corrosive_trap";
    public static int ActivationMultiplier(PassiveModifiers p, bool self) => Has(p, 0) ? self ? 13500 : 6500 : 10000;
    public static int ManaMultiplier(PassiveModifiers p) => CombatRules.ApplyMore(10000, [Has(p, 2) ? 13000 : 10000, Has(p, 3) ? 12000 : 10000, Has(p, 4) ? 12500 : 10000]);
    public static int SpeedMultiplier(PassiveModifiers p) => CombatRules.ApplyMore(10000, [Has(p, 3) ? 13000 : 10000, Has(p, 4) ? 6500 : 10000]);
    public static int HitMultiplier(PassiveModifiers p) => Has(p, 4) ? 20000 : 10000;
    public static int AilmentMultiplier(PassiveModifiers p) => Has(p, 4) ? 6500 : 10000;
}

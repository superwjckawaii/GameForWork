using GameForWork.Core.Ascendancies;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;

namespace GameForWork.Core.Skills;

public static class UnarmedRules
{
    public static readonly WeaponProfile Base = new("core.attack.unarmed", 5, 8, 1_800, 800);
    public static bool IsSkill(string id) => ActiveSkillCatalog.ActiveForSkill(id).Curve == SkillCurve.UnarmedAttack;
    public static bool Has(CombatProfile? profile, string branch, string size = "small") =>
        profile?.Has($"core.ascendancy.martial_monk.{branch}.{size}") == true;
    public static WeaponProfile Source(string id, WeaponProfile weapon) => IsSkill(id) ? Base : weapon;
    public static int CriticalBonus(SkillConfiguration configuration) => IsSkill(configuration.SkillId)
        ? LinkedSupportRules.SupportQuality(configuration, SupportMechanic.UnarmedFocus) * 10 : 0;
    public static int AttackSpeed(TeamBuild build) => !build.HasUsableWeapon && Has(build.Ascendancy, "unarmed") ? 2_000 : 0;
    public static int DamageIncrease(TeamBuild build) => !build.HasUsableWeapon && Has(build.Ascendancy, "unarmed", "core")
        ? (build.Sheet.Attributes.Dexterity + build.Sheet.Attributes.Spirit) / 10 * 100 : 0;
    public static int CastTicks(string id, int quality) => (int)Math.Ceiling(200_000_000d / (Base.AttacksPerSecondMilli * (id switch
    {
        "archetypes.skill.chain_fists" => 16_000 + Math.Clamp(quality, 0, 20) * 100,
        "archetypes.skill.skyquake_palm" => 9_500,
        "archetypes.skill.gale_kick" => 11_000,
        "archetypes.skill.tenfold_finisher" => 7_000,
        _ => 10_000
    })));
}

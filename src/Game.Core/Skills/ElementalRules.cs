using GameForWork.Core.Ascendancies;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Characters;
namespace GameForWork.Core.Skills;

public readonly record struct ElementalAilments(bool Ignited, bool Chilled, bool Frozen, bool Shocked, bool Paralyzed = false);

public static class ElementalRules
{
    public static bool Has(CombatProfile? profile, string branch, string size = "small") => profile?.Has($"core.ascendancy.elementalist.{branch}.{size}") == true;
    public static DamageType Primary(CombatProfile? profile) => (profile?.Configuration?.PrimaryElement ?? PrimaryElement.Fire) switch
    { PrimaryElement.Cold => DamageType.Cold, PrimaryElement.Lightning => DamageType.Lightning, _ => DamageType.Fire };
    public static int TypeIncrease(TeamBuild build, DamageType type) => type is DamageType.Fire or DamageType.Cold or DamageType.Lightning
        ? (Has(build.Ascendancy, "conversion") && Primary(build.Ascendancy) == type ? 2_000 : 0) +
          (Has(build.Ascendancy, type == DamageType.Fire ? "fire" : type == DamageType.Cold ? "cold" : "lightning") ? 2_500 : 0) : 0;
    public static int TargetMultiplier(CombatProfile? profile, DamageType type, ElementalAilments ailments, bool hit, bool critical)
    {
        if (type is not (DamageType.Fire or DamageType.Cold or DamageType.Lightning)) return 10_000;
        int result = 10_000;
        if (type == DamageType.Fire && hit && ailments.Ignited && Has(profile, "fire", "core")) result = 13_000;
        if (type == DamageType.Cold && Has(profile, "cold", "core")) result = ailments.Frozen ? 15_000 : ailments.Chilled ? 12_500 : 10_000;
        if (type == DamageType.Lightning && hit && critical && ailments.Shocked && Has(profile, "lightning", "core")) result = 15_000;
        int families = (ailments.Ignited ? 1 : 0) + (ailments.Chilled || ailments.Frozen ? 1 : 0) + (ailments.Shocked || ailments.Paralyzed ? 1 : 0);
        return CombatRules.ApplyMore(result, [Has(profile, "ailment", "core") ? 10_000 + families * 1_500 : 10_000]);
    }
}


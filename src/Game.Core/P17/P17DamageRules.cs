using GameForWork.Core.P1.Combat;

namespace GameForWork.Core.P17;

public sealed record P17DamageBreakdown(
    int Physical, int Fire, int Cold, int Lightning, int Void, int Total,
    IReadOnlyList<string> Trace)
{
    public string Compact => $"physical:{Physical},fire:{Fire},cold:{Cold},lightning:{Lightning},void:{Void}";
}

public static class P17DamageRules
{
    public static P17DamageBreakdown Resolve(
        int rawDamage,
        P17DamageType baseType,
        SkillSupport supports,
        int targetArmor,
        int fireResistance,
        int coldResistance,
        int lightningResistance,
        int voidResistance)
    {
        int physical = baseType == P17DamageType.Physical ? rawDamage : 0;
        int fire = baseType == P17DamageType.Fire ? rawDamage : 0;
        int cold = baseType == P17DamageType.Cold ? rawDamage : 0;
        int lightning = baseType == P17DamageType.Lightning ? rawDamage : 0;
        int voidDamage = baseType == P17DamageType.Void ? rawDamage : 0;
        int originalPhysical = physical;
        var trace = new List<string> { $"base:{baseType.ToString().ToLowerInvariant()}={rawDamage}" };

        Convert(ref physical, ref lightning, supports.HasFlag(SkillSupport.PhysicalToLightning), "physical->lightning", trace);
        Convert(ref lightning, ref cold, supports.HasFlag(SkillSupport.LightningToCold), "lightning->cold", trace);
        Convert(ref cold, ref fire, supports.HasFlag(SkillSupport.ColdToFire), "cold->fire", trace);
        Convert(ref fire, ref voidDamage, supports.HasFlag(SkillSupport.FireToVoid), "fire->void", trace);
        if (supports.HasFlag(SkillSupport.AddedFire) && originalPhysical > 0)
        {
            int added = originalPhysical * 1_800 / 10_000;
            fire += added;
            trace.Add($"extra-fire:{added}");
        }
        if (supports.HasFlag(SkillSupport.AddedCold)) cold += Math.Max(1, rawDamage * 1_500 / 10_000);
        if (supports.HasFlag(SkillSupport.AddedLightning)) lightning += Math.Max(1, rawDamage * 1_700 / 10_000);
        if (supports.HasFlag(SkillSupport.Brutality)) fire = cold = lightning = voidDamage = 0;

        int armorReduction = DamageRules.ArmorReduction(Math.Max(0, targetArmor), Math.Max(0, physical)).Value;
        physical = Mitigate(physical, armorReduction);
        fire = Mitigate(fire, fireResistance);
        cold = Mitigate(cold, coldResistance);
        lightning = Mitigate(lightning, lightningResistance);
        voidDamage = Mitigate(voidDamage, voidResistance);
        int total = checked(physical + fire + cold + lightning + voidDamage);
        trace.Add($"mitigated:armor={armorReduction},fire-res={fireResistance},cold-res={coldResistance},lightning-res={lightningResistance},void-res={voidResistance}");
        return new(physical, fire, cold, lightning, voidDamage, total, trace);
    }

    private static void Convert(ref int source, ref int target, bool enabled, string label, ICollection<string> trace)
    {
        if (!enabled || source <= 0) return;
        int converted = source / 2;
        source -= converted;
        target += converted;
        trace.Add($"{label}:{converted}");
    }

    private static int Mitigate(int damage, int resistanceBasisPoints) => damage <= 0 ? 0 :
        Math.Max(1, checked(damage * (10_000 - Math.Clamp(resistanceBasisPoints, -10_000, 9_000)) / 10_000));
}

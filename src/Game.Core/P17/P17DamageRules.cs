using GameForWork.Core.P1.Combat;
using GameForWork.Core.P30;

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
        int voidResistance,
        int physicalResistance = 0)
    {
        P30DamageType type = baseType switch
        {
            P17DamageType.Fire => P30DamageType.Fire,
            P17DamageType.Cold => P30DamageType.Cold,
            P17DamageType.Lightning => P30DamageType.Lightning,
            P17DamageType.Void => P30DamageType.Void,
            _ => P30DamageType.Physical,
        };
        var conversions = new List<P30Conversion>();
        if (supports.HasFlag(SkillSupport.PhysicalToLightning))
            conversions.Add(new(P30DamageType.Physical, P30DamageType.Lightning, 5_000, "support.physical_to_lightning"));
        // P30 禁止闪电转冰；旧辅助仍可被旧存档识别，但不再参与伤害转化。
        if (supports.HasFlag(SkillSupport.ColdToFire))
            conversions.Add(new(P30DamageType.Cold, P30DamageType.Fire, 5_000, "support.cold_to_fire"));
        if (supports.HasFlag(SkillSupport.FireToVoid))
            conversions.Add(new(P30DamageType.Fire, P30DamageType.Void, 5_000, "support.fire_to_void"));
        var extras = new List<P30ExtraDamage>();
        if (supports.HasFlag(SkillSupport.AddedFire))
            extras.Add(new(P30DamageType.Physical, P30DamageType.Fire, 1_800, "support.added_fire"));
        if (supports.HasFlag(SkillSupport.AddedCold))
            extras.Add(new(type, P30DamageType.Cold, 1_500, "support.added_cold"));
        if (supports.HasFlag(SkillSupport.AddedLightning))
            extras.Add(new(type, P30DamageType.Lightning, 1_700, "support.added_lightning"));

        P30DamagePacket packet = P30CombatRules.ConvertAndScale(rawDamage, type, conversions, extras);
        if (supports.HasFlag(SkillSupport.Brutality))
            packet = packet with
            {
                Fire = 0, Cold = 0, Lightning = 0, Void = 0,
                Branches = packet.Branches.Where(branch => branch.CurrentType == P30DamageType.Physical).ToArray(),
            };
        packet = P30CombatRules.Mitigate(packet, Math.Max(0, targetArmor),
            new P30ResistanceProfile(physicalResistance, fireResistance, coldResistance, lightningResistance, voidResistance));
        int total = packet.Total;
        return new(packet.Physical, packet.Fire, packet.Cold, packet.Lightning, packet.Void, total, packet.Trace);
    }
}

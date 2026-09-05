using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Items;

namespace GameForWork.Core.SkillCatalog;

public sealed record DamageBreakdown(
    int Physical, int Fire, int Cold, int Lightning, int Void, int Total,
    IReadOnlyList<string> Trace)
{
    public string Compact => $"physical:{Physical},fire:{Fire},cold:{Cold},lightning:{Lightning},void:{Void}";
    public DamageBreakdown Scale(int basisPoints)
    {
        int Part(int value) => (int)Math.Min(int.MaxValue, (long)value * Math.Max(0, basisPoints) / 10_000);
        int physical = Part(Physical), fire = Part(Fire), cold = Part(Cold), lightning = Part(Lightning), abyss = Part(Void);
        return new(physical, fire, cold, lightning, abyss,
            (int)Math.Min(int.MaxValue, (long)physical + fire + cold + lightning + abyss), Trace);
    }
}

public readonly record struct AddedWeaponDamage(int Fire = 0, int Cold = 0, int Lightning = 0, int Void = 0);

public static class DamagePacketRules
{
    public static DamageBreakdown Resolve(
        int rawDamage,
        SkillDamageType baseType,
        SkillSupport supports,
        int targetArmor,
        int fireResistance,
        int coldResistance,
        int lightningResistance,
        int voidResistance,
        int physicalResistance = 0) => ResolveMixed(rawDamage, baseType, default, supports, targetArmor,
        fireResistance, coldResistance, lightningResistance, voidResistance, physicalResistance);

    public static DamageBreakdown ResolveMixed(
        int rawDamage,
        SkillDamageType baseType,
        AddedWeaponDamage addedWeaponDamage,
        SkillSupport supports,
        int targetArmor,
        int fireResistance,
        int coldResistance,
        int lightningResistance,
        int voidResistance,
        int physicalResistance = 0,
        IReadOnlyDictionary<ItemModifierKind, int>? equipment = null,
        DamageModifiers? modifiers = null,
        Func<DamageBranch, int>? scaleBranch = null,
        Action<IReadOnlyList<DamageBranch>>? captureSource = null)
    {
        DamageType type = baseType switch
        {
            SkillDamageType.Fire => DamageType.Fire,
            SkillDamageType.Cold => DamageType.Cold,
            SkillDamageType.Lightning => DamageType.Lightning,
            SkillDamageType.Void => DamageType.Void,
            _ => DamageType.Physical,
        };
        var conversions = new List<Conversion>();
        if (supports.HasFlag(SkillSupport.PhysicalToLightning))
            conversions.Add(new(DamageType.Physical, DamageType.Lightning, 5_000, "support.physical_to_lightning"));
        // Builds 将旧“闪电转冰”稳定 ID 迁移为合法的物理转冰，避免破坏旧存档引用。
        if (supports.HasFlag(SkillSupport.LightningToCold))
            conversions.Add(new(DamageType.Physical, DamageType.Cold, 5_000, "support.physical_to_cold"));
        if (supports.HasFlag(SkillSupport.ColdToFire))
            conversions.Add(new(DamageType.Cold, DamageType.Fire, 5_000, "support.cold_to_fire"));
        if (supports.HasFlag(SkillSupport.FireToVoid))
            conversions.Add(new(DamageType.Fire, DamageType.Void, 5_000, "support.fire_to_void"));
        var extras = new List<ExtraDamage>();
        if (supports.HasFlag(SkillSupport.AddedFire))
            extras.Add(new(DamageType.Physical, DamageType.Fire, 1_800, "support.added_fire"));
        if (supports.HasFlag(SkillSupport.AddedCold))
            extras.Add(new(type, DamageType.Cold, 1_500, "support.added_cold"));
        if (supports.HasFlag(SkillSupport.AddedLightning))
            extras.Add(new(type, DamageType.Lightning, 1_700, "support.added_lightning"));

        AddConversion(ItemModifierKind.PhysicalToFireConversionBasisPoints, DamageType.Physical, DamageType.Fire);
        AddConversion(ItemModifierKind.PhysicalToColdConversionBasisPoints, DamageType.Physical, DamageType.Cold);
        AddConversion(ItemModifierKind.PhysicalToLightningConversionBasisPoints, DamageType.Physical, DamageType.Lightning);
        AddConversion(ItemModifierKind.PhysicalToVoidConversionBasisPoints, DamageType.Physical, DamageType.Void);
        AddConversion(ItemModifierKind.ColdToFireConversionBasisPoints, DamageType.Cold, DamageType.Fire);
        AddConversion(ItemModifierKind.LightningToFireConversionBasisPoints, DamageType.Lightning, DamageType.Fire);
        AddConversion(ItemModifierKind.FireToVoidConversionBasisPoints, DamageType.Fire, DamageType.Void);
        AddConversion(ItemModifierKind.ColdToVoidConversionBasisPoints, DamageType.Cold, DamageType.Void);
        AddConversion(ItemModifierKind.LightningToVoidConversionBasisPoints, DamageType.Lightning, DamageType.Void);
        AddExtra(ItemModifierKind.PhysicalAsExtraFireBasisPoints, DamageType.Physical, DamageType.Fire);
        AddExtra(ItemModifierKind.PhysicalAsExtraColdBasisPoints, DamageType.Physical, DamageType.Cold);
        AddExtra(ItemModifierKind.PhysicalAsExtraLightningBasisPoints, DamageType.Physical, DamageType.Lightning);
        foreach (DamageType element in new[] { DamageType.Fire, DamageType.Cold, DamageType.Lightning })
            AddExtra(ItemModifierKind.ElementalAsExtraVoidBasisPoints, element, DamageType.Void);

        var packets = new List<DamagePacket>
        {
            ConvertPacket(rawDamage, type),
        };
        AddWeaponPacket(addedWeaponDamage.Fire, DamageType.Fire);
        AddWeaponPacket(addedWeaponDamage.Cold, DamageType.Cold);
        AddWeaponPacket(addedWeaponDamage.Lightning, DamageType.Lightning);
        AddWeaponPacket(addedWeaponDamage.Void, DamageType.Void);
        DamagePacket packet = new(
            packets.Sum(value => value.Physical), packets.Sum(value => value.Fire),
            packets.Sum(value => value.Cold), packets.Sum(value => value.Lightning),
            packets.Sum(value => value.Void), packets.SelectMany(value => value.Branches).ToArray(),
            packets.SelectMany(value => value.Trace).ToArray());
        if (supports.HasFlag(SkillSupport.Brutality))
            packet = packet with
            {
                Fire = 0, Cold = 0, Lightning = 0, Void = 0,
                Branches = packet.Branches.Where(branch => branch.CurrentType == DamageType.Physical).ToArray(),
            };
        packet = CombatRules.Mitigate(packet, Math.Max(0, targetArmor),
            new ResistanceProfile(physicalResistance, fireResistance, coldResistance, lightningResistance, voidResistance));
        int total = packet.Total;
        return new(packet.Physical, packet.Fire, packet.Cold, packet.Lightning, packet.Void, total, packet.Trace);

        void AddWeaponPacket(int damage, DamageType damageType)
        {
            if (damage > 0)
                packets.Add(ConvertPacket(damage, damageType));
        }

        DamagePacket ConvertPacket(int damage, DamageType damageType)
        {
            DamagePacket converted = CombatRules.ConvertAndScale(damage, damageType, conversions, extras, modifiers, captureSource);
            if (scaleBranch is null) return converted;
            DamageBranch[] branches = converted.Branches.Select(branch => branch with { BaseDamage = scaleBranch(branch) }).ToArray();
            int Sum(DamageType target) => (int)Math.Clamp(branches.Where(branch => branch.CurrentType == target)
                .Sum(branch => (long)branch.BaseDamage), 0, int.MaxValue);
            return new(Sum(DamageType.Physical), Sum(DamageType.Fire), Sum(DamageType.Cold),
                Sum(DamageType.Lightning), Sum(DamageType.Void), branches, converted.Trace);
        }
        void AddConversion(ItemModifierKind kind, DamageType source, DamageType target)
        {
            if (equipment?.GetValueOrDefault(kind) is > 0 and var value)
                conversions.Add(new(source, target, value, $"equipment.{kind}"));
        }
        void AddExtra(ItemModifierKind kind, DamageType source, DamageType target)
        {
            if (equipment?.GetValueOrDefault(kind) is > 0 and var value)
                extras.Add(new(source, target, value, $"equipment.{kind}"));
        }
    }
}

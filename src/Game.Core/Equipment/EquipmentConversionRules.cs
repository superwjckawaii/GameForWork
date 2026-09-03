using GameForWork.Core.P1.Items;

namespace GameForWork.Core.Equipment;

public sealed record EquipmentConversionAllocation(
    int ToFireBasisPoints,
    int ToColdBasisPoints,
    int ToLightningBasisPoints,
    int ToVoidBasisPoints)
{
    public int TotalBasisPoints => ToFireBasisPoints + ToColdBasisPoints + ToLightningBasisPoints + ToVoidBasisPoints;
}

public static class EquipmentConversionRules
{
    public static EquipmentConversionAllocation NormalizePhysical(IEnumerable<RolledAffixComponent> effects)
    {
        int Sum(ItemModifierKind kind) => effects.Where(effect => effect.Kind == kind).Sum(effect => Math.Max(0, effect.Value));
        int fire = Sum(ItemModifierKind.PhysicalToFireConversionBasisPoints);
        int cold = Sum(ItemModifierKind.PhysicalToColdConversionBasisPoints);
        int lightning = Sum(ItemModifierKind.PhysicalToLightningConversionBasisPoints);
        int @void = Sum(ItemModifierKind.PhysicalToVoidConversionBasisPoints);
        int total = fire + cold + lightning + @void;
        if (total <= 10_000) return new(fire, cold, lightning, @void);
        int scaledFire = fire * 10_000 / total;
        int scaledCold = cold * 10_000 / total;
        int scaledLightning = lightning * 10_000 / total;
        int scaledVoid = 10_000 - scaledFire - scaledCold - scaledLightning;
        return new(scaledFire, scaledCold, scaledLightning, scaledVoid);
    }
}

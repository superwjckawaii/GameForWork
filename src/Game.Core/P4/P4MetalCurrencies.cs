namespace GameForWork.Core.P4;

public enum MetalCurrencyKind
{
    TemperingIron,
    WardSteel,
    VitalSilver,
    ChaosGold,
    DivineSilver,
    FractureSteel,
}

public sealed record MetalCurrencyDefinition(
    MetalCurrencyKind Kind,
    string StableId,
    string DisplayName,
    string Description,
    int DropWeight);

public sealed record MetalCurrencyStack(MetalCurrencyKind Kind, int Amount);

public static class P4MetalCurrencies
{
    private static readonly IReadOnlyDictionary<MetalCurrencyKind, MetalCurrencyDefinition> Definitions =
        new[]
        {
            new MetalCurrencyDefinition(MetalCurrencyKind.TemperingIron, "core.metal.tempering_iron", "淬刃铁", "为武器烙入固定物理加工词缀。", 36),
            new MetalCurrencyDefinition(MetalCurrencyKind.WardSteel, "core.metal.ward_steel", "守壁钢", "为防具烙入固定防御加工词缀。", 30),
            new MetalCurrencyDefinition(MetalCurrencyKind.VitalSilver, "core.metal.vital_silver", "活血银", "为非武器装备烙入固定生命加工词缀。", 24),
            new MetalCurrencyDefinition(MetalCurrencyKind.ChaosGold, "core.metal.chaos_gold", "混沌金", "重铸稀有装备的随机词缀；配方将在后续阶段开放。", 7),
            new MetalCurrencyDefinition(MetalCurrencyKind.DivineSilver, "core.metal.divine_silver", "神铸银", "重掷词缀数值；配方将在后续阶段开放。", 2),
            new MetalCurrencyDefinition(MetalCurrencyKind.FractureSteel, "core.metal.fracture_steel", "破溃钢", "固化一条词缀；配方将在后续阶段开放。", 1),
        }.ToDictionary(item => item.Kind);

    public static IReadOnlyCollection<MetalCurrencyDefinition> All => Definitions.Values.ToArray();

    public static MetalCurrencyDefinition Get(MetalCurrencyKind kind) => Definitions[kind];
}

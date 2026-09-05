namespace GameForWork.Core.Spatial;

public enum MetalCurrencyKind
{
    TemperingIron,
    WardSteel,
    VitalSilver,
    ChaosGold,
    DivineSilver,
    FractureSteel,
    ChainSteel,
    AwakeningCopper,
    AugmentingTin,
    MutableMercury,
    FatefulGold,
    AlchemicalGold,
    RegalGold,
    ExaltedGold,
    DissolutionSilver,
    ScouringLead,
    BlessedSilver,
    PolishingCobalt,
    CorruptionIron,
}

public enum MetalCurrencyTier
{
    Basic,
    Advanced,
    High,
    Dangerous,
}

public sealed record MetalCurrencyDefinition(
    MetalCurrencyKind Kind,
    string StableId,
    string DisplayName,
    string Description,
    int DropWeight,
    MetalCurrencyTier Tier = MetalCurrencyTier.Basic);

public sealed record MetalCurrencyStack(MetalCurrencyKind Kind, int Amount);

public static class MetalCurrencies
{
    private static readonly IReadOnlyDictionary<MetalCurrencyKind, MetalCurrencyDefinition> Definitions =
        new[]
        {
            new MetalCurrencyDefinition(MetalCurrencyKind.TemperingIron, "core.metal.tempering_iron", "淬刃铁", "为武器烙入固定物理加工词缀。", 36),
            new MetalCurrencyDefinition(MetalCurrencyKind.WardSteel, "core.metal.ward_steel", "守壁钢", "为防具烙入固定防御加工词缀。", 30),
            new MetalCurrencyDefinition(MetalCurrencyKind.VitalSilver, "core.metal.vital_silver", "活血银", "为非武器装备烙入固定生命加工词缀。", 24),
            new MetalCurrencyDefinition(MetalCurrencyKind.ChaosGold, "core.metal.chaos_gold", "混沌金", "重铸稀有装备的随机词缀并保护破溃与工匠词缀。", 7, MetalCurrencyTier.Advanced),
            new MetalCurrencyDefinition(MetalCurrencyKind.DivineSilver, "core.metal.divine_silver", "神铸银", "重掷现有自然词缀的数值。", 2, MetalCurrencyTier.High),
            new MetalCurrencyDefinition(MetalCurrencyKind.FractureSteel, "core.metal.fracture_steel", "破溃钢", "随机固化至少四条自然词缀装备上的一条词缀。", 1, MetalCurrencyTier.High),
            new MetalCurrencyDefinition(MetalCurrencyKind.ChainSteel, "core.metal.chain_steel", "链铸钢", "重铸或保证提高装备连接数。", 5, MetalCurrencyTier.Advanced),
            new MetalCurrencyDefinition(MetalCurrencyKind.AwakeningCopper, "core.metal.awakening_copper", "启灵铜", "将普通装备转化为魔法装备。", 28),
            new MetalCurrencyDefinition(MetalCurrencyKind.AugmentingTin, "core.metal.augmenting_tin", "添铸锡", "为只有一条词缀的魔法装备增加一条词缀。", 25),
            new MetalCurrencyDefinition(MetalCurrencyKind.MutableMercury, "core.metal.mutable_mercury", "易变汞", "重铸魔法装备的自然词缀。", 22),
            new MetalCurrencyDefinition(MetalCurrencyKind.FatefulGold, "core.metal.fateful_gold", "命铸金", "将普通装备随机转化为魔法、稀有或极低概率传奇。", 9, MetalCurrencyTier.Advanced),
            new MetalCurrencyDefinition(MetalCurrencyKind.AlchemicalGold, "core.metal.alchemical_gold", "炼真金", "将普通装备直接转化为稀有装备。", 12, MetalCurrencyTier.Advanced),
            new MetalCurrencyDefinition(MetalCurrencyKind.RegalGold, "core.metal.regal_gold", "王铸金", "将魔法装备升级为稀有并增加一条词缀。", 6, MetalCurrencyTier.Advanced),
            new MetalCurrencyDefinition(MetalCurrencyKind.ExaltedGold, "core.metal.exalted_gold", "崇高金", "为未满词缀的稀有装备增加一条随机词缀。", 2, MetalCurrencyTier.High),
            new MetalCurrencyDefinition(MetalCurrencyKind.DissolutionSilver, "core.metal.dissolution_silver", "消解银", "随机移除一条非破溃显式词缀。", 3, MetalCurrencyTier.High),
            new MetalCurrencyDefinition(MetalCurrencyKind.ScouringLead, "core.metal.scouring_lead", "洗炼铅", "移除所有可变显式词缀，使装备回归基础状态。", 12, MetalCurrencyTier.Advanced),
            new MetalCurrencyDefinition(MetalCurrencyKind.BlessedSilver, "core.metal.blessed_silver", "祝铸银", "重掷底材固有词缀数值。", 7, MetalCurrencyTier.Advanced),
            new MetalCurrencyDefinition(MetalCurrencyKind.PolishingCobalt, "core.metal.polishing_cobalt", "精磨钴", "提高武器、防具或药剂品质，最高 20%。", 14, MetalCurrencyTier.Advanced),
            new MetalCurrencyDefinition(MetalCurrencyKind.CorruptionIron, "core.metal.corruption_iron", "赤蚀铁", "腐化装备；可能强化、锁定、负面改变或摧毁。", 1, MetalCurrencyTier.Dangerous),
        }.ToDictionary(item => item.Kind);

    public static IReadOnlyCollection<MetalCurrencyDefinition> All => Definitions.Values
        .OrderBy(item => item.Tier).ThenByDescending(item => item.DropWeight).ThenBy(item => item.DisplayName, StringComparer.Ordinal).ToArray();

    public static MetalCurrencyDefinition Get(MetalCurrencyKind kind) => Definitions[kind];
}

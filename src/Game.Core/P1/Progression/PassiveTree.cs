namespace GameForWork.Core.P1.Progression;

public enum PassiveBranch
{
    HeavyWeapon,
    Bleed,
    Defense,
    WarCry,
}

public enum PassiveNodeKind
{
    Small,
    Notable,
    Rule,
}

public enum PassiveEffectKind
{
    IncreasedAttackDamageBasisPoints,
    FlatAccuracy,
    IncreasedAttackSpeedBasisPoints,
    IncreasedTwoHandDamageBasisPoints,
    IncreasedBleedDamageBasisPoints,
    IncreasedBleedChanceBasisPoints,
    IncreasedBleedDurationBasisPoints,
    IncreasedPhysicalDamageOverTimeBasisPoints,
    IncreasedMaximumLifeBasisPoints,
    IncreasedArmorBasisPoints,
    FlatMaximumLife,
    IncreasedLifeFlaskEffectBasisPoints,
    IncreasedWarCryCooldownRecoveryBasisPoints,
    FlatMaximumMana,
    IncreasedManaRegenerationBasisPoints,
    IncreasedWarCryRangeBasisPoints,
    FasterBleeding,
    DeepWound,
    Tenacious,
    Echo,
    ChargedHeavyStrike,
}

public sealed record PassiveEffect(PassiveEffectKind Kind, int Value = 0);

public sealed record PassiveNodeDefinition(
    string StableId,
    string DisplayName,
    PassiveBranch Branch,
    PassiveNodeKind Kind,
    string? PrerequisiteId,
    IReadOnlyList<PassiveEffect> Effects);

public sealed record PassiveBuildModifiers(
    int IncreasedAttackDamageBasisPoints,
    int FlatAccuracy,
    int IncreasedAttackSpeedBasisPoints,
    int IncreasedTwoHandDamageBasisPoints,
    int IncreasedBleedDamageBasisPoints,
    int IncreasedBleedChanceBasisPoints,
    int IncreasedBleedDurationBasisPoints,
    int IncreasedPhysicalDamageOverTimeBasisPoints,
    int IncreasedMaximumLifeBasisPoints,
    int IncreasedArmorBasisPoints,
    int FlatMaximumLife,
    int IncreasedLifeFlaskEffectBasisPoints,
    int IncreasedWarCryCooldownRecoveryBasisPoints,
    int FlatMaximumMana,
    int IncreasedManaRegenerationBasisPoints,
    int IncreasedWarCryRangeBasisPoints,
    bool FasterBleeding,
    bool DeepWound,
    bool Tenacious,
    bool Echo,
    bool ChargedHeavyStrike);

public static class P1PassiveTree
{
    private static readonly IReadOnlyDictionary<string, PassiveNodeDefinition> NodeMap = BuildNodes()
        .ToDictionary(node => node.StableId, StringComparer.Ordinal);

    public static IReadOnlyCollection<PassiveNodeDefinition> Nodes => NodeMap.Values.ToArray();

    public static PassiveNodeDefinition Get(string stableId) =>
        NodeMap.TryGetValue(stableId, out PassiveNodeDefinition? node)
            ? node
            : throw new KeyNotFoundException($"Unknown passive node: {stableId}");

    private static IReadOnlyList<PassiveNodeDefinition> BuildNodes() =>
    [
        Small("heavy.1", "重击伤害", PassiveBranch.HeavyWeapon, null, PassiveEffectKind.IncreasedAttackDamageBasisPoints, 500),
        Small("heavy.2", "稳定握持", PassiveBranch.HeavyWeapon, "heavy.1", PassiveEffectKind.FlatAccuracy, 10),
        Small("heavy.3", "沉重打击", PassiveBranch.HeavyWeapon, "heavy.2", PassiveEffectKind.IncreasedAttackDamageBasisPoints, 500),
        Small("heavy.4", "挥击节奏", PassiveBranch.HeavyWeapon, "heavy.3", PassiveEffectKind.IncreasedAttackSpeedBasisPoints, 400),
        Small("heavy.5", "双手专精", PassiveBranch.HeavyWeapon, "heavy.4", PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, 600),
        Node("heavy.notable", "重兵训练", PassiveBranch.HeavyWeapon, PassiveNodeKind.Notable, "heavy.5",
            new PassiveEffect(PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, 1_200),
            new PassiveEffect(PassiveEffectKind.IncreasedAttackSpeedBasisPoints, 600)),
        Node("heavy.rule", "蓄势重击", PassiveBranch.HeavyWeapon, PassiveNodeKind.Rule, "heavy.notable",
            new PassiveEffect(PassiveEffectKind.ChargedHeavyStrike)),

        Small("bleed.1", "流血伤害 I", PassiveBranch.Bleed, null, PassiveEffectKind.IncreasedBleedDamageBasisPoints, 1_000),
        Small("bleed.2", "流血伤害 II", PassiveBranch.Bleed, "bleed.1", PassiveEffectKind.IncreasedBleedDamageBasisPoints, 1_000),
        Small("bleed.3", "割裂概率", PassiveBranch.Bleed, "bleed.2", PassiveEffectKind.IncreasedBleedChanceBasisPoints, 1_000),
        Small("bleed.4", "延长创口", PassiveBranch.Bleed, "bleed.3", PassiveEffectKind.IncreasedBleedDurationBasisPoints, 1_000),
        Small("bleed.5", "持续创伤", PassiveBranch.Bleed, "bleed.4", PassiveEffectKind.IncreasedPhysicalDamageOverTimeBasisPoints, 1_000),
        Node("bleed.notable", "撕裂", PassiveBranch.Bleed, PassiveNodeKind.Notable, "bleed.5",
            new PassiveEffect(PassiveEffectKind.FasterBleeding)),
        Node("bleed.rule", "深创", PassiveBranch.Bleed, PassiveNodeKind.Rule, "bleed.notable",
            new PassiveEffect(PassiveEffectKind.DeepWound)),

        Small("defense.1", "生命 I", PassiveBranch.Defense, null, PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 400),
        Small("defense.2", "生命 II", PassiveBranch.Defense, "defense.1", PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 400),
        Small("defense.3", "护甲 I", PassiveBranch.Defense, "defense.2", PassiveEffectKind.IncreasedArmorBasisPoints, 800),
        Small("defense.4", "护甲 II", PassiveBranch.Defense, "defense.3", PassiveEffectKind.IncreasedArmorBasisPoints, 800),
        Small("defense.5", "坚实血肉", PassiveBranch.Defense, "defense.4", PassiveEffectKind.FlatMaximumLife, 5),
        Node("defense.notable", "顽强", PassiveBranch.Defense, PassiveNodeKind.Notable, "defense.5",
            new PassiveEffect(PassiveEffectKind.Tenacious),
            new PassiveEffect(PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints, 2_000)),

        Small("warcry.1", "战吼恢复 I", PassiveBranch.WarCry, null, PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints, 500),
        Small("warcry.2", "战吼恢复 II", PassiveBranch.WarCry, "warcry.1", PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints, 500),
        Small("warcry.3", "法力储备", PassiveBranch.WarCry, "warcry.2", PassiveEffectKind.FlatMaximumMana, 5),
        Small("warcry.4", "法力循环", PassiveBranch.WarCry, "warcry.3", PassiveEffectKind.IncreasedManaRegenerationBasisPoints, 1_000),
        Small("warcry.5", "广域战吼", PassiveBranch.WarCry, "warcry.4", PassiveEffectKind.IncreasedWarCryRangeBasisPoints, 1_000),
        Node("warcry.notable", "余音", PassiveBranch.WarCry, PassiveNodeKind.Notable, "warcry.5",
            new PassiveEffect(PassiveEffectKind.Echo)),
    ];

    private static PassiveNodeDefinition Small(
        string id,
        string name,
        PassiveBranch branch,
        string? prerequisite,
        PassiveEffectKind effect,
        int value) =>
        Node(id, name, branch, PassiveNodeKind.Small, prerequisite, new PassiveEffect(effect, value));

    private static PassiveNodeDefinition Node(
        string id,
        string name,
        PassiveBranch branch,
        PassiveNodeKind kind,
        string? prerequisite,
        params PassiveEffect[] effects) =>
        new($"core.passive.{id}", name, branch, kind,
            prerequisite is null ? null : $"core.passive.{prerequisite}", effects);
}

public sealed class PassiveTreeAllocation
{
    public const int MaximumAllocatedPoints = 10;

    private readonly HashSet<string> _allocated = new(StringComparer.Ordinal);

    public PassiveTreeAllocation(int memoryAshes = 5)
    {
        MemoryAshes = memoryAshes;
    }

    public IReadOnlySet<string> Allocated => _allocated;
    public int MemoryAshes { get; private set; }

    public bool TryAllocate(string stableId, int earnedPassivePoints)
    {
        PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
        int availablePoints = Math.Min(earnedPassivePoints, MaximumAllocatedPoints);
        if (_allocated.Contains(stableId) || _allocated.Count >= availablePoints)
        {
            return false;
        }

        if (node.PrerequisiteId is not null && !_allocated.Contains(node.PrerequisiteId))
        {
            return false;
        }

        return _allocated.Add(stableId);
    }

    public bool TryRefund(string stableId)
    {
        if (!_allocated.Contains(stableId) || MemoryAshes < 1 ||
            P1PassiveTree.Nodes.Any(node => node.PrerequisiteId == stableId && _allocated.Contains(node.StableId)))
        {
            return false;
        }

        _allocated.Remove(stableId);
        MemoryAshes--;
        return true;
    }

    public bool TryReset()
    {
        if (_allocated.Count == 0 || MemoryAshes < 10)
        {
            return false;
        }

        _allocated.Clear();
        MemoryAshes -= 10;
        return true;
    }

    public void AddMemoryAshes(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        MemoryAshes = checked(MemoryAshes + amount);
    }

    public PassiveBuildModifiers CalculateModifiers()
    {
        int[] sums = new int[Enum.GetValues<PassiveEffectKind>().Length];
        foreach (PassiveEffect effect in _allocated
                     .Select(P1PassiveTree.Get)
                     .SelectMany(node => node.Effects))
        {
            sums[(int)effect.Kind] = checked(sums[(int)effect.Kind] + effect.Value);
        }

        return new PassiveBuildModifiers(
            sums[(int)PassiveEffectKind.IncreasedAttackDamageBasisPoints],
            sums[(int)PassiveEffectKind.FlatAccuracy],
            sums[(int)PassiveEffectKind.IncreasedAttackSpeedBasisPoints],
            sums[(int)PassiveEffectKind.IncreasedTwoHandDamageBasisPoints],
            sums[(int)PassiveEffectKind.IncreasedBleedDamageBasisPoints],
            sums[(int)PassiveEffectKind.IncreasedBleedChanceBasisPoints],
            sums[(int)PassiveEffectKind.IncreasedBleedDurationBasisPoints],
            sums[(int)PassiveEffectKind.IncreasedPhysicalDamageOverTimeBasisPoints],
            sums[(int)PassiveEffectKind.IncreasedMaximumLifeBasisPoints],
            sums[(int)PassiveEffectKind.IncreasedArmorBasisPoints],
            sums[(int)PassiveEffectKind.FlatMaximumLife],
            sums[(int)PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints],
            sums[(int)PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints],
            sums[(int)PassiveEffectKind.FlatMaximumMana],
            sums[(int)PassiveEffectKind.IncreasedManaRegenerationBasisPoints],
            sums[(int)PassiveEffectKind.IncreasedWarCryRangeBasisPoints],
            sums[(int)PassiveEffectKind.FasterBleeding] > 0,
            sums[(int)PassiveEffectKind.DeepWound] > 0,
            sums[(int)PassiveEffectKind.Tenacious] > 0,
            sums[(int)PassiveEffectKind.Echo] > 0,
            sums[(int)PassiveEffectKind.ChargedHeavyStrike] > 0);
    }
}

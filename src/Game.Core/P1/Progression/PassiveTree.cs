namespace GameForWork.Core.P1.Progression;

public enum PassiveBranch
{
    HeavyWeapon,
    Bleed,
    Defense,
    WarCry,
    Mobility,
    Critical,
    Accuracy,
    Mana,
    Shield,
    Flask,
}

public enum PassiveNodeKind
{
    Small,
    Notable,
    Mastery,
    Rule,
    JewelSocket,
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
    IncreasedMovementSpeedBasisPoints,
    FasterBleeding,
    DeepWound,
    Tenacious,
    Echo,
    ChargedHeavyStrike,
    HeavyWeaponMastery,
    BleedMastery,
    DefenseMastery,
    WarCryMastery,
}

public sealed record PassiveEffect(PassiveEffectKind Kind, int Value = 0);

public enum PassiveJewelKind { CrimsonMemory, VerdantMemory, AzureMemory }

public sealed record PassiveNodeDefinition(
    string StableId,
    string DisplayName,
    PassiveBranch Branch,
    PassiveNodeKind Kind,
    string? PrerequisiteId,
    IReadOnlyList<PassiveEffect> Effects,
    IReadOnlyList<string>? Connections = null,
    float X = 0,
    float Y = 0,
    int ThemeGroup = 0)
{
    public IReadOnlyList<string> LinkedNodes => Connections ??
        (PrerequisiteId is null ? Array.Empty<string>() : [PrerequisiteId]);
}

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
    int IncreasedMovementSpeedBasisPoints,
    bool FasterBleeding,
    bool DeepWound,
    bool Tenacious,
    bool Echo,
    bool ChargedHeavyStrike,
    bool HeavyWeaponMastery,
    bool BleedMastery,
    bool DefenseMastery,
    bool WarCryMastery);

public static class P1PassiveTree
{
    private static readonly IReadOnlyDictionary<string, PassiveNodeDefinition> NodeMap = BuildNodes()
        .ToDictionary(node => node.StableId, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AdjacencyMap = BuildAdjacency(NodeMap.Values);

    public static IReadOnlyCollection<PassiveNodeDefinition> Nodes => NodeMap.Values.ToArray();

    public static PassiveNodeDefinition Get(string stableId) =>
        NodeMap.TryGetValue(stableId, out PassiveNodeDefinition? node)
            ? node
            : throw new KeyNotFoundException($"Unknown passive node: {stableId}");

    public static IReadOnlyList<string> Neighbors(string stableId) =>
        AdjacencyMap.TryGetValue(stableId, out IReadOnlyList<string>? result) ? result : [];

    public static IReadOnlyList<PassiveEffect> MasteryOptions(PassiveNodeDefinition node)
    {
        if (node.Kind != PassiveNodeKind.Mastery) return [];
        return node.Branch switch
        {
            PassiveBranch.Defense or PassiveBranch.Shield =>
                [new(PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 800), new(PassiveEffectKind.IncreasedArmorBasisPoints, 1_500), new(PassiveEffectKind.FlatMaximumLife, 12)],
            PassiveBranch.Mobility =>
                [new(PassiveEffectKind.IncreasedMovementSpeedBasisPoints, 300), new(PassiveEffectKind.IncreasedAttackSpeedBasisPoints, 500), new(PassiveEffectKind.FlatAccuracy, 20)],
            _ => [new(PassiveEffectKind.IncreasedAttackDamageBasisPoints, 1_000), new(PassiveEffectKind.IncreasedBleedDamageBasisPoints, 1_200), new(PassiveEffectKind.IncreasedAttackSpeedBasisPoints, 600)],
        };
    }

    private static IReadOnlyList<PassiveNodeDefinition> BuildNodes()
    {
        var nodes = new List<PassiveNodeDefinition>
        {
        Small("heavy.1", "重击伤害", PassiveBranch.HeavyWeapon, null, PassiveEffectKind.IncreasedAttackDamageBasisPoints, 500),
        Small("heavy.2", "稳定握持", PassiveBranch.HeavyWeapon, "heavy.1", PassiveEffectKind.FlatAccuracy, 10),
        Small("heavy.3", "沉重打击", PassiveBranch.HeavyWeapon, "heavy.2", PassiveEffectKind.IncreasedAttackDamageBasisPoints, 500),
        Small("heavy.4", "挥击节奏", PassiveBranch.HeavyWeapon, "heavy.3", PassiveEffectKind.IncreasedAttackSpeedBasisPoints, 400),
        Small("heavy.5", "双手专精", PassiveBranch.HeavyWeapon, "heavy.4", PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, 600),
        Node("heavy.notable", "重兵训练", PassiveBranch.HeavyWeapon, PassiveNodeKind.Notable, "heavy.5",
            new PassiveEffect(PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, 1_200),
            new PassiveEffect(PassiveEffectKind.IncreasedAttackSpeedBasisPoints, 600)),
        Node("heavy.mastery", "震岳专精", PassiveBranch.HeavyWeapon, PassiveNodeKind.Mastery, "heavy.notable",
            new PassiveEffect(PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, 2_500),
            new PassiveEffect(PassiveEffectKind.HeavyWeaponMastery)),
        Node("heavy.rule", "蓄势重击", PassiveBranch.HeavyWeapon, PassiveNodeKind.Rule, "heavy.notable",
            new PassiveEffect(PassiveEffectKind.ChargedHeavyStrike),
            new PassiveEffect(PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, 3_000)),

        Small("bleed.1", "流血伤害 I", PassiveBranch.Bleed, null, PassiveEffectKind.IncreasedBleedDamageBasisPoints, 1_000),
        Small("bleed.2", "流血伤害 II", PassiveBranch.Bleed, "bleed.1", PassiveEffectKind.IncreasedBleedDamageBasisPoints, 1_000),
        Small("bleed.3", "割裂概率", PassiveBranch.Bleed, "bleed.2", PassiveEffectKind.IncreasedBleedChanceBasisPoints, 1_000),
        Small("bleed.4", "延长创口", PassiveBranch.Bleed, "bleed.3", PassiveEffectKind.IncreasedBleedDurationBasisPoints, 1_000),
        Small("bleed.5", "持续创伤", PassiveBranch.Bleed, "bleed.4", PassiveEffectKind.IncreasedPhysicalDamageOverTimeBasisPoints, 1_000),
        Node("bleed.notable", "撕裂", PassiveBranch.Bleed, PassiveNodeKind.Notable, "bleed.5",
            new PassiveEffect(PassiveEffectKind.FasterBleeding)),
        Node("bleed.mastery", "孤创专精", PassiveBranch.Bleed, PassiveNodeKind.Mastery, "bleed.notable",
            new PassiveEffect(PassiveEffectKind.IncreasedBleedDamageBasisPoints, 3_500),
            new PassiveEffect(PassiveEffectKind.BleedMastery)),
        Node("bleed.rule", "深创", PassiveBranch.Bleed, PassiveNodeKind.Rule, "bleed.notable",
            new PassiveEffect(PassiveEffectKind.DeepWound),
            new PassiveEffect(PassiveEffectKind.IncreasedBleedDamageBasisPoints, 4_000)),

        Small("defense.1", "生命 I", PassiveBranch.Defense, null, PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 400),
        Small("defense.2", "生命 II", PassiveBranch.Defense, "defense.1", PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 400),
        Small("defense.3", "护甲 I", PassiveBranch.Defense, "defense.2", PassiveEffectKind.IncreasedArmorBasisPoints, 800),
        Small("defense.4", "护甲 II", PassiveBranch.Defense, "defense.3", PassiveEffectKind.IncreasedArmorBasisPoints, 800),
        Small("defense.5", "坚实血肉", PassiveBranch.Defense, "defense.4", PassiveEffectKind.FlatMaximumLife, 5),
        Node("defense.notable", "顽强", PassiveBranch.Defense, PassiveNodeKind.Notable, "defense.5",
            new PassiveEffect(PassiveEffectKind.Tenacious),
            new PassiveEffect(PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints, 2_000)),
        Node("defense.mastery", "钢躯专精", PassiveBranch.Defense, PassiveNodeKind.Mastery, "defense.notable",
            new PassiveEffect(PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 1_500),
            new PassiveEffect(PassiveEffectKind.IncreasedArmorBasisPoints, 3_000),
            new PassiveEffect(PassiveEffectKind.DefenseMastery)),

        Small("warcry.1", "战吼恢复 I", PassiveBranch.WarCry, null, PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints, 500),
        Small("warcry.2", "战吼恢复 II", PassiveBranch.WarCry, "warcry.1", PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints, 500),
        Small("warcry.3", "法力储备", PassiveBranch.WarCry, "warcry.2", PassiveEffectKind.FlatMaximumMana, 5),
        Small("warcry.4", "法力循环", PassiveBranch.WarCry, "warcry.3", PassiveEffectKind.IncreasedManaRegenerationBasisPoints, 1_000),
        Small("warcry.5", "广域战吼", PassiveBranch.WarCry, "warcry.4", PassiveEffectKind.IncreasedWarCryRangeBasisPoints, 1_000),
        Node("warcry.notable", "余音", PassiveBranch.WarCry, PassiveNodeKind.Notable, "warcry.5",
            new PassiveEffect(PassiveEffectKind.Echo)),
        Node("warcry.mastery", "震令专精", PassiveBranch.WarCry, PassiveNodeKind.Mastery, "warcry.notable",
            new PassiveEffect(PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints, 3_000),
            new PassiveEffect(PassiveEffectKind.IncreasedWarCryRangeBasisPoints, 3_000),
            new PassiveEffect(PassiveEffectKind.WarCryMastery)),
        };

        ExtendBranch(nodes, PassiveBranch.HeavyWeapon, "heavy", "heavy.rule", 11,
            PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, 350);
        ExtendBranch(nodes, PassiveBranch.Bleed, "bleed", "bleed.rule", 11,
            PassiveEffectKind.IncreasedBleedDamageBasisPoints, 500);
        ExtendBranch(nodes, PassiveBranch.Defense, "defense", "defense.notable", 12,
            PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 250);
        ExtendBranch(nodes, PassiveBranch.WarCry, "warcry", "warcry.notable", 12,
            PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints, 300);
        AddCluster(nodes, PassiveBranch.Mobility, "mobility", PassiveEffectKind.IncreasedMovementSpeedBasisPoints, 175);
        AddCluster(nodes, PassiveBranch.Critical, "critical", PassiveEffectKind.IncreasedAttackDamageBasisPoints, 350);
        AddCluster(nodes, PassiveBranch.Accuracy, "accuracy", PassiveEffectKind.FlatAccuracy, 8);
        AddCluster(nodes, PassiveBranch.Mana, "mana", PassiveEffectKind.FlatMaximumMana, 2);
        AddCluster(nodes, PassiveBranch.Shield, "shield", PassiveEffectKind.IncreasedArmorBasisPoints, 350);
        AddCluster(nodes, PassiveBranch.Flask, "flask", PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints, 250);
        AddConstellation(nodes);
        return nodes;
    }

    private static void AddConstellation(ICollection<PassiveNodeDefinition> nodes)
    {
        const int groupCount = 30;
        const int nodesPerGroup = 48;
        string[] roots = ["heavy.1", "bleed.1", "defense.1", "warcry.1", "mobility.1",
            "critical.1", "accuracy.1", "mana.1", "shield.1", "flask.1"];
        string[] themes = ["铸锋", "裂创", "壁垒", "战令", "逐风", "暴烈", "洞察", "源流", "铁卫", "炼金",
            "破阵", "血潮", "磐石", "回声", "长途", "致命", "鹰眼", "灵泉", "守望", "药理",
            "巨兵", "创痕", "不屈", "号角", "疾驰", "处决", "猎手", "秘能", "城塞", "回春"];
        for (int group = 0; group < groupCount; group++)
        {
            PassiveBranch branch = (PassiveBranch)(group % 10);
            int belt = group / 10;
            float groupAngle = -MathF.PI / 2 + (group % 10) * MathF.Tau / 10 + belt * 0.085f;
            float groupRadius = 520 + belt * 390;
            float centerX = MathF.Cos(groupAngle) * groupRadius;
            float centerY = MathF.Sin(groupAngle) * groupRadius * 0.78f;
            for (int index = 0; index < nodesPerGroup; index++)
            {
                string local = $"constellation.{group:00}.{index:00}";
                string id = $"core.passive.{local}";
                string previous = index == 0
                    ? $"core.passive.{roots[group % roots.Length]}"
                    : $"core.passive.constellation.{group:00}.{index - 1:00}";
                string next = $"core.passive.constellation.{group:00}.{(index + 1) % nodesPerGroup:00}";
                var links = new List<string> { previous, next };
                if (index == 0 && group > 0)
                {
                    links.Add($"core.passive.constellation.{group - 1:00}.24");
                }
                if (index == 24 && group + 1 < groupCount)
                {
                    links.Add($"core.passive.constellation.{group + 1:00}.00");
                }

                PassiveNodeKind kind = index switch
                {
                    8 or 18 or 28 or 38 => PassiveNodeKind.Notable,
                    45 => PassiveNodeKind.Mastery,
                    46 => PassiveNodeKind.Rule,
                    47 when group < 16 => PassiveNodeKind.JewelSocket,
                    _ => PassiveNodeKind.Small,
                };
                int orbit = index / 12;
                float radius = 72 + orbit * 30;
                float angle = index % 12 * MathF.Tau / 12 + orbit * 0.14f;
                PassiveEffect effect = EffectFor(branch, kind, group, index);
                nodes.Add(new PassiveNodeDefinition(
                    id,
                    kind switch
                    {
                        PassiveNodeKind.Notable => $"{themes[group]}·{(index / 10) + 1}式",
                        PassiveNodeKind.Mastery => $"{themes[group]}专精",
                        PassiveNodeKind.Rule => $"{themes[group]}誓律",
                        PassiveNodeKind.JewelSocket => "记忆棱孔",
                        _ => $"{themes[group]}·{index + 1:00}",
                    },
                    branch,
                    kind,
                    previous,
                    [effect],
                    links.Distinct(StringComparer.Ordinal).ToArray(),
                    centerX + MathF.Cos(angle) * radius,
                    centerY + MathF.Sin(angle) * radius,
                    group));
            }
        }
    }

    private static PassiveEffect EffectFor(PassiveBranch branch, PassiveNodeKind kind, int group, int index)
    {
        int multiplier = kind switch
        {
            PassiveNodeKind.Notable => 4,
            PassiveNodeKind.Mastery => 6,
            PassiveNodeKind.Rule => 8,
            PassiveNodeKind.JewelSocket => 2,
            _ => 1,
        };
        return branch switch
        {
            PassiveBranch.HeavyWeapon => new(PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, 180 * multiplier),
            PassiveBranch.Bleed => new(PassiveEffectKind.IncreasedBleedDamageBasisPoints, 220 * multiplier),
            PassiveBranch.Defense => new(PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 120 * multiplier),
            PassiveBranch.WarCry => new(PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints, 140 * multiplier),
            PassiveBranch.Mobility => new(PassiveEffectKind.IncreasedMovementSpeedBasisPoints, 55 * multiplier),
            PassiveBranch.Critical => new(PassiveEffectKind.IncreasedAttackDamageBasisPoints, 190 * multiplier),
            PassiveBranch.Accuracy => new(PassiveEffectKind.FlatAccuracy, Math.Max(1, multiplier * 2)),
            PassiveBranch.Mana => new(PassiveEffectKind.FlatMaximumMana, Math.Max(1, multiplier)),
            PassiveBranch.Shield => new(PassiveEffectKind.IncreasedArmorBasisPoints, 210 * multiplier),
            _ => new(PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints, 120 * multiplier),
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildAdjacency(
        IEnumerable<PassiveNodeDefinition> nodes)
    {
        PassiveNodeDefinition[] all = nodes.ToArray();
        var result = all.ToDictionary(node => node.StableId, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (PassiveNodeDefinition node in all)
        {
            foreach (string linked in node.LinkedNodes)
            {
                if (!result.ContainsKey(linked) || linked == node.StableId) continue;
                result[node.StableId].Add(linked);
                result[linked].Add(node.StableId);
            }
        }
        return result.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value.ToArray(), StringComparer.Ordinal);
    }

    private static void AddCluster(
        ICollection<PassiveNodeDefinition> nodes,
        PassiveBranch branch,
        string prefix,
        PassiveEffectKind effect,
        int value)
    {
        string? previous = null;
        for (int index = 1; index <= 18; index++)
        {
            string id = $"{prefix}.{index}";
            PassiveNodeKind kind = index == 18
                ? PassiveNodeKind.Rule
                : index is 6 or 12 ? PassiveNodeKind.Notable : PassiveNodeKind.Small;
            int nodeValue = kind switch
            {
                PassiveNodeKind.Small => value,
                PassiveNodeKind.Notable => value * 2,
                _ => value * 5,
            };
            nodes.Add(Node(id, $"{BranchName(branch)}·{index:00}", branch, kind, previous,
                new PassiveEffect(effect, nodeValue)));
            previous = id;
        }
    }

    private static void ExtendBranch(
        ICollection<PassiveNodeDefinition> nodes,
        PassiveBranch branch,
        string prefix,
        string prerequisite,
        int count,
        PassiveEffectKind effect,
        int value)
    {
        string previous = prerequisite;
        for (int index = 1; index <= count; index++)
        {
            string id = $"{prefix}.path.{index}";
            nodes.Add(Small(id, $"{BranchName(branch)}进阶 {index}", branch, previous, effect, value));
            previous = id;
        }
    }

    private static string BranchName(PassiveBranch branch) => branch switch
    {
        PassiveBranch.HeavyWeapon => "重兵",
        PassiveBranch.Bleed => "流血",
        PassiveBranch.Defense => "守御",
        PassiveBranch.WarCry => "战吼",
        PassiveBranch.Mobility => "行路",
        PassiveBranch.Critical => "暴烈",
        PassiveBranch.Accuracy => "洞察",
        PassiveBranch.Mana => "源流",
        PassiveBranch.Shield => "壁垒",
        PassiveBranch.Flask => "炼金",
        _ => string.Empty,
    };

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
    public const int MaximumAllocatedPoints = 120;

    private readonly HashSet<string> _allocated = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _masterySelections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PassiveJewelKind> _socketedJewels = new(StringComparer.Ordinal);

    public PassiveTreeAllocation(int memoryAshes = 5)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(memoryAshes);
        MemoryAshes = memoryAshes;
    }

    public IReadOnlySet<string> Allocated => _allocated;
    public IReadOnlyDictionary<string, int> MasterySelections => _masterySelections;
    public IReadOnlyDictionary<string, PassiveJewelKind> SocketedJewels => _socketedJewels;
    public int MemoryAshes { get; private set; }

    public bool TryAllocate(string stableId, int earnedPassivePoints)
    {
        PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
        int availablePoints = Math.Min(earnedPassivePoints, MaximumAllocatedPoints);
        if (_allocated.Contains(stableId) || _allocated.Count >= availablePoints)
        {
            return false;
        }

        if (node.PrerequisiteId is not null && !P1PassiveTree.Neighbors(stableId).Any(_allocated.Contains))
        {
            return false;
        }

        return _allocated.Add(stableId);
    }

    public bool TryRefund(string stableId)
    {
        if (!_allocated.Contains(stableId) || MemoryAshes < 1 || !CanRefundWithoutDisconnecting(stableId))
        {
            return false;
        }

        _allocated.Remove(stableId);
        _masterySelections.Remove(stableId);
        _socketedJewels.Remove(stableId);
        MemoryAshes--;
        return true;
    }

    public bool CanRefundWithoutDisconnecting(string stableId)
    {
        if (!_allocated.Contains(stableId))
        {
            return false;
        }

        HashSet<string> remaining = _allocated.Where(id => id != stableId).ToHashSet(StringComparer.Ordinal);
        if (remaining.Count == 0)
        {
            return true;
        }

        Dictionary<string, List<string>> edges = remaining.ToDictionary(id => id,
            id => P1PassiveTree.Neighbors(id).Where(remaining.Contains).ToList(), StringComparer.Ordinal);

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(remaining.Where(id => P1PassiveTree.Get(id).PrerequisiteId is null));
        while (pending.TryDequeue(out string? current))
        {
            if (!reachable.Add(current))
            {
                continue;
            }

            foreach (string neighbor in edges[current])
            {
                pending.Enqueue(neighbor);
            }
        }

        return reachable.SetEquals(remaining);
    }

    public bool TryReset()
    {
        if (_allocated.Count == 0 || MemoryAshes < 10)
        {
            return false;
        }

        _allocated.Clear();
        _masterySelections.Clear();
        _socketedJewels.Clear();
        MemoryAshes -= 10;
        return true;
    }

    public bool ForceReset()
    {
        if (_allocated.Count == 0)
        {
            return false;
        }

        _allocated.Clear();
        _masterySelections.Clear();
        _socketedJewels.Clear();
        return true;
    }

    public bool TrySelectMastery(string stableId, int option)
    {
        PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
        if (!_allocated.Contains(stableId) || node.Kind != PassiveNodeKind.Mastery || option < 0 || option >= P1PassiveTree.MasteryOptions(node).Count) return false;
        _masterySelections[stableId] = option;
        return true;
    }

    public bool TrySocketJewel(string stableId, PassiveJewelKind jewel)
    {
        PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
        if (!_allocated.Contains(stableId) || node.Kind != PassiveNodeKind.JewelSocket || !Enum.IsDefined(jewel)) return false;
        _socketedJewels[stableId] = jewel;
        return true;
    }

    public void AddMemoryAshes(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        MemoryAshes = checked(MemoryAshes + amount);
    }

    public static PassiveTreeAllocation Restore(IEnumerable<string> allocated, int memoryAshes,
        IReadOnlyDictionary<string, int>? masteries = null,
        IReadOnlyDictionary<string, PassiveJewelKind>? jewels = null)
    {
        ArgumentNullException.ThrowIfNull(allocated);
        var result = new PassiveTreeAllocation(memoryAshes);
        string[] nodes = allocated.ToArray();
        if (nodes.Length > MaximumAllocatedPoints)
        {
            throw new InvalidDataException("Passive allocation exceeds the supported point cap.");
        }

        foreach (string stableId in nodes)
        {
            PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
            if (node.PrerequisiteId is not null && !P1PassiveTree.Neighbors(stableId).Any(result._allocated.Contains) ||
                !result._allocated.Add(stableId))
            {
                throw new InvalidDataException("Passive allocation snapshot is not a valid path.");
            }
        }

        foreach ((string id, int option) in masteries ?? new Dictionary<string, int>())
            if (!result.TrySelectMastery(id, option)) throw new InvalidDataException("Passive mastery selection is invalid.");
        foreach ((string id, PassiveJewelKind jewel) in jewels ?? new Dictionary<string, PassiveJewelKind>())
            if (!result.TrySocketJewel(id, jewel)) throw new InvalidDataException("Passive jewel socket is invalid.");

        return result;
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
        foreach ((string id, int option) in _masterySelections)
        {
            PassiveEffect effect = P1PassiveTree.MasteryOptions(P1PassiveTree.Get(id))[option];
            sums[(int)effect.Kind] = checked(sums[(int)effect.Kind] + effect.Value);
        }
        foreach ((string socket, PassiveJewelKind jewel) in _socketedJewels)
        {
            int radiusMultiplier = 1 + P1PassiveTree.Neighbors(socket).Count(_allocated.Contains) / 2;
            PassiveEffect effect = jewel switch
            {
                PassiveJewelKind.CrimsonMemory => new(PassiveEffectKind.IncreasedAttackDamageBasisPoints, 800 * radiusMultiplier),
                PassiveJewelKind.VerdantMemory => new(PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 600 * radiusMultiplier),
                _ => new(PassiveEffectKind.IncreasedMovementSpeedBasisPoints, 250 * radiusMultiplier),
            };
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
            sums[(int)PassiveEffectKind.IncreasedMovementSpeedBasisPoints],
            sums[(int)PassiveEffectKind.FasterBleeding] > 0,
            sums[(int)PassiveEffectKind.DeepWound] > 0,
            sums[(int)PassiveEffectKind.Tenacious] > 0,
            sums[(int)PassiveEffectKind.Echo] > 0,
            sums[(int)PassiveEffectKind.ChargedHeavyStrike] > 0,
            sums[(int)PassiveEffectKind.HeavyWeaponMastery] > 0,
            sums[(int)PassiveEffectKind.BleedMastery] > 0,
            sums[(int)PassiveEffectKind.DefenseMastery] > 0,
            sums[(int)PassiveEffectKind.WarCryMastery] > 0);
    }
}

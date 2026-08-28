using GameForWork.Core.P1.World;
using GameForWork.Core.P12;

namespace GameForWork.Core.P10;

public enum P10MapMechanic { Abyss, LifeGarden, RedAltar, BlueAltar }
public enum P10AtlasTheme { MapSupply, Abyss, LifeGarden, RedAltar, BlueAltar, Boss }

public sealed record P10AtlasNode(
    string StableId,
    string DisplayName,
    P10AtlasTheme Theme,
    int Ring,
    int OrbitIndex,
    string? PrerequisiteId,
    int RewardBasisPoints,
    bool Notable);

public static class P10AtlasTree
{
    private static readonly IReadOnlyDictionary<string, P10AtlasNode> NodeMap = Build()
        .ToDictionary(node => node.StableId, StringComparer.Ordinal);
    public static IReadOnlyCollection<P10AtlasNode> Nodes => NodeMap.Values.ToArray();
    public static P10AtlasNode Get(string id) => NodeMap.TryGetValue(id, out P10AtlasNode? node)
        ? node : throw new KeyNotFoundException($"Unknown atlas passive: {id}");

    private static IReadOnlyList<P10AtlasNode> Build()
    {
        string[] themeNames = ["路印回响", "裂渊追猎", "命能培植", "赤誓祭坛", "苍誓祭坛", "终局攻坚"];
        var result = new List<P10AtlasNode>(360);
        for (int lane = 0; lane < 12; lane++)
        for (int index = 0; index < 30; index++)
        {
            P10AtlasTheme theme = (P10AtlasTheme)(lane % 6);
            string id = $"core.atlas.{lane:00}.{index:00}";
            string? prerequisite = index == 0 ? null : $"core.atlas.{lane:00}.{index - 1:00}";
            bool notable = index is 9 or 19 or 29;
            result.Add(new P10AtlasNode(id,
                notable ? $"{themeNames[(int)theme]}·枢纽 {index / 10 + 1}" : $"{themeNames[(int)theme]} {index + 1:00}",
                theme, index / 10, lane * 30 + index, prerequisite, notable ? 900 : 180, notable));
        }
        return result;
    }
}

public sealed record P10AscendancyNode(string StableId, string DisplayName, string? PrerequisiteId, string Effect);

public static class P10IronOathAscendancy
{
    public static IReadOnlyList<P10AscendancyNode> Nodes { get; } =
    [
        new("core.ascendancy.iron_oath.01", "铸铁宣誓", null, "护甲提高 20%，近战伤害提高 10%"),
        new("core.ascendancy.iron_oath.02", "不退阵线", "core.ascendancy.iron_oath.01", "移动接敌期间承伤降低 8%"),
        new("core.ascendancy.iron_oath.03", "震令回响", "core.ascendancy.iron_oath.01", "战吼冷却恢复提高 25%"),
        new("core.ascendancy.iron_oath.04", "血火锻身", "core.ascendancy.iron_oath.02", "流血伤害提高 25%"),
        new("core.ascendancy.iron_oath.05", "重岳落势", "core.ascendancy.iron_oath.03", "双手攻击伤害提高 25%"),
        new("core.ascendancy.iron_oath.06", "钢魂", "core.ascendancy.iron_oath.04", "最大生命提高 12%"),
        new("core.ascendancy.iron_oath.07", "军锋号令", "core.ascendancy.iron_oath.05", "队伍攻击速度提高 8%"),
        new("core.ascendancy.iron_oath.08", "永续铁誓", "core.ascendancy.iron_oath.06", "重伤时药剂效果提高 35%"),
        new("core.ascendancy.iron_oath.09", "破阵者", "core.ascendancy.iron_oath.07", "对 Boss 伤害提高 20%"),
        new("core.ascendancy.iron_oath.10", "余烬甲胄", "core.ascendancy.iron_oath.08", "元素承伤降低 8%"),
        new("core.ascendancy.iron_oath.11", "百战锋芒", "core.ascendancy.iron_oath.09", "击败精英后获得锋芒"),
        new("core.ascendancy.iron_oath.12", "铁誓不灭", "core.ascendancy.iron_oath.10", "濒死时每场战斗触发一次不灭"),
    ];
}

public sealed record P10EndgameSnapshot(
    IReadOnlyList<int> CompletedTiers,
    IReadOnlyList<string> AtlasPassives,
    IReadOnlyDictionary<P10MapMechanic, int> MechanicEncounters,
    int LifeForce,
    int RedFavor,
    int BlueFavor,
    int CitadelFragments,
    int CitadelTickets,
    bool CitadelDefeated,
    IReadOnlyList<string> AscendancyPassives,
    int BreakthroughPoints,
    bool FinalBreakthroughCompleted = false,
    IReadOnlyList<P12AtlasSchemeSnapshot>? AtlasSchemes = null,
    int ActiveAtlasSchemeIndex = 0);

public sealed record P12AtlasSchemeSnapshot(string Name, IReadOnlyList<string> AllocatedPassives);

public sealed class P10EndgameState
{
    public const int CitadelFragmentsPerTicket = 8;
    public const string CitadelMapPrefix = "p10-ashen-citadel-";
    private readonly HashSet<int> _completedTiers = [];
    private readonly List<(string Name, HashSet<string> Nodes)> _atlasSchemes =
    [
        ("续航方案", new HashSet<string>(StringComparer.Ordinal)),
        ("机制方案", new HashSet<string>(StringComparer.Ordinal)),
        ("攻坚方案", new HashSet<string>(StringComparer.Ordinal)),
    ];
    private readonly Dictionary<P10MapMechanic, int> _mechanics = Enum.GetValues<P10MapMechanic>().ToDictionary(kind => kind, _ => 0);
    private readonly HashSet<string> _ascendancy = new(StringComparer.Ordinal);
    public IReadOnlySet<int> CompletedTiers => _completedTiers;
    public IReadOnlySet<string> AtlasPassives => _atlasSchemes[ActiveAtlasSchemeIndex].Nodes;
    public IReadOnlyList<string> AtlasSchemeNames => _atlasSchemes.Select(scheme => scheme.Name).ToArray();
    public int ActiveAtlasSchemeIndex { get; private set; }
    public IReadOnlyDictionary<P10MapMechanic, int> MechanicEncounters => _mechanics;
    public IReadOnlySet<string> AscendancyPassives => _ascendancy;
    public int EarnedAtlasPoints => _completedTiers.Count + (CitadelDefeated ? 5 : 0);
    public int LifeForce { get; private set; }
    public int RedFavor { get; private set; }
    public int BlueFavor { get; private set; }
    public int CitadelFragments { get; private set; }
    public int CitadelTickets { get; private set; }
    public bool CitadelDefeated { get; private set; }
    public int BreakthroughPoints { get; private set; }
    public bool FinalBreakthroughCompleted { get; private set; }

    public IReadOnlyList<P10MapMechanic> RecordMapCompletion(P1MapItem map, MapRoute route, ulong seed)
    {
        map.Validate();
        _completedTiers.Add(map.AreaLevel);
        var selected = new List<P10MapMechanic>(2);
        if (route == MapRoute.Abyss) selected.Add(P10MapMechanic.Abyss);
        if (route == MapRoute.LifeGarden) selected.Add(P10MapMechanic.LifeGarden);
        if (map.Altar == P12MapAltar.RedOath) selected.Add(P10MapMechanic.RedAltar);
        if (map.Altar == P12MapAltar.BlueOath) selected.Add(P10MapMechanic.BlueAltar);
        if (selected.Count == 0)
        {
            P10MapMechanic[] choices = Enum.GetValues<P10MapMechanic>();
            selected.Add(choices[(int)((seed + (ulong)map.AreaLevel) % (ulong)choices.Length)]);
        }
        P10MapMechanic[] result = selected.Distinct().Take(3).ToArray();
        foreach (P10MapMechanic mechanic in result)
        {
            _mechanics[mechanic]++;
            int bonus = 100 + AtlasBonus((P10AtlasTheme)((int)mechanic + 1));
            switch (mechanic)
            {
                case P10MapMechanic.LifeGarden: LifeForce = checked(LifeForce + map.AreaLevel * bonus / 100); break;
                case P10MapMechanic.RedAltar: RedFavor = checked(RedFavor + bonus); break;
                case P10MapMechanic.BlueAltar: BlueFavor = checked(BlueFavor + bonus); break;
            }
        }
        if (map.AreaLevel >= 11)
        {
            CitadelFragments++;
            while (CitadelFragments >= CitadelFragmentsPerTicket) { CitadelFragments -= CitadelFragmentsPerTicket; CitadelTickets++; }
        }
        return result;
    }

    public bool TryAllocateAtlas(string id)
    {
        P10AtlasNode node = P10AtlasTree.Get(id);
        HashSet<string> atlas = _atlasSchemes[ActiveAtlasSchemeIndex].Nodes;
        if (atlas.Contains(id) || atlas.Count >= EarnedAtlasPoints || node.PrerequisiteId is not null && !atlas.Contains(node.PrerequisiteId)) return false;
        return atlas.Add(id);
    }

    public bool TryRenameAtlasScheme(int index, string name)
    {
        if (index is < 0 or > 2 || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 12) return false;
        _atlasSchemes[index] = (name.Trim(), _atlasSchemes[index].Nodes);
        return true;
    }

    public bool TrySwitchAtlasScheme(int index)
    {
        if (index is < 0 or > 2 || index == ActiveAtlasSchemeIndex) return false;
        ActiveAtlasSchemeIndex = index;
        return true;
    }

    public bool TryCompleteFinalBreakthrough(int level, bool trialWon)
    {
        if (FinalBreakthroughCompleted || level < 100 || !trialWon) return false;
        FinalBreakthroughCompleted = true;
        return true;
    }

    public bool TryAllocateAscendancy(string id)
    {
        P10AscendancyNode node = P10IronOathAscendancy.Nodes.Single(item => item.StableId == id);
        if (_ascendancy.Contains(id) || _ascendancy.Count >= BreakthroughPoints || node.PrerequisiteId is not null && !_ascendancy.Contains(node.PrerequisiteId)) return false;
        return _ascendancy.Add(id);
    }

    public void AwardBreakthroughPoint() { if (BreakthroughPoints < 4) BreakthroughPoints++; }
    public bool TryConsumeCitadelTicket()
    {
        if (CitadelTickets <= 0) return false;
        CitadelTickets--;
        return true;
    }

    public void RecordCitadelVictory()
    {
        CitadelDefeated = true;
        AwardBreakthroughPoint();
    }

    public static bool IsCitadel(P1MapItem map) => map.InstanceId.StartsWith(CitadelMapPrefix, StringComparison.Ordinal);

    public int AtlasBonus(P10AtlasTheme theme) => AtlasPassives.Select(P10AtlasTree.Get).Where(node => node.Theme == theme).Sum(node => node.RewardBasisPoints) / 100;

    public P10EndgameSnapshot Capture() => new(_completedTiers.Order().ToArray(), AtlasPassives.Order().ToArray(),
        new Dictionary<P10MapMechanic, int>(_mechanics), LifeForce, RedFavor, BlueFavor, CitadelFragments,
        CitadelTickets, CitadelDefeated, _ascendancy.Order().ToArray(), BreakthroughPoints,
        FinalBreakthroughCompleted,
        _atlasSchemes.Select(scheme => new P12AtlasSchemeSnapshot(scheme.Name, scheme.Nodes.Order().ToArray())).ToArray(),
        ActiveAtlasSchemeIndex);

    public static P10EndgameState Restore(P10EndgameSnapshot? snapshot)
    {
        var state = new P10EndgameState();
        if (snapshot is null) return state;
        if (snapshot.CompletedTiers.Any(tier => tier is < 1 or > 20) || snapshot.AtlasPassives.Count > 25 ||
            snapshot.CitadelFragments is < 0 or >= CitadelFragmentsPerTicket || snapshot.CitadelTickets < 0 ||
            snapshot.BreakthroughPoints is < 0 or > 4 || snapshot.AscendancyPassives.Count > snapshot.BreakthroughPoints)
            throw new InvalidDataException("P10 endgame snapshot is invalid.");
        foreach (int tier in snapshot.CompletedTiers) state._completedTiers.Add(tier);
        IReadOnlyList<P12AtlasSchemeSnapshot> schemes = snapshot.AtlasSchemes ??
            [new("续航方案", snapshot.AtlasPassives), new("机制方案", []), new("攻坚方案", [])];
        if (schemes.Count != 3 || snapshot.ActiveAtlasSchemeIndex is < 0 or > 2)
            throw new InvalidDataException("P12 atlas schemes are invalid.");
        for (int index = 0; index < 3; index++)
        {
            if (!state.TryRenameAtlasScheme(index, schemes[index].Name)) throw new InvalidDataException("P12 atlas scheme name is invalid.");
            foreach (string id in schemes[index].AllocatedPassives) { P10AtlasTree.Get(id); state._atlasSchemes[index].Nodes.Add(id); }
        }
        state.ActiveAtlasSchemeIndex = snapshot.ActiveAtlasSchemeIndex;
        foreach (P10MapMechanic kind in Enum.GetValues<P10MapMechanic>()) state._mechanics[kind] = snapshot.MechanicEncounters.GetValueOrDefault(kind);
        state.LifeForce = snapshot.LifeForce; state.RedFavor = snapshot.RedFavor; state.BlueFavor = snapshot.BlueFavor;
        state.CitadelFragments = snapshot.CitadelFragments; state.CitadelTickets = snapshot.CitadelTickets; state.CitadelDefeated = snapshot.CitadelDefeated;
        state.BreakthroughPoints = snapshot.BreakthroughPoints;
        state.FinalBreakthroughCompleted = snapshot.FinalBreakthroughCompleted;
        foreach (string id in snapshot.AscendancyPassives) { _ = P10IronOathAscendancy.Nodes.Single(node => node.StableId == id); state._ascendancy.Add(id); }
        return state;
    }
}

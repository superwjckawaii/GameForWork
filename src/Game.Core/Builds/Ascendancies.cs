using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameForWork.Core.Ascendancies;

namespace GameForWork.Core.Builds;

public sealed record AscendancyBranchData(string Direction, string ReinforcementName,
    string ReinforcementEffect, string CoreName, string CoreEffect, string StableKey);
public sealed record AscendancyData(Ascendancy Ascendancy, string DisplayName,
    IReadOnlyList<AscendancyBranchData> Branches, string StableKey);

public static class AscendancyDefinitions
{
    public static IReadOnlyList<AscendancyData> All { get; } = Load();

    public static IReadOnlyList<AscendancyNode> Nodes { get; } = BuildNodes();
    public static string Id(Ascendancy ascendancy, string branch, NodeKind kind) =>
        $"core.ascendancy.{All.Single(path => path.Ascendancy == ascendancy).StableKey}.{branch}.{(kind == NodeKind.Core ? "core" : "small")}";

    private static IReadOnlyList<AscendancyNode> BuildNodes()
    {
        var nodes = new List<AscendancyNode>(216);
        foreach (AscendancyData path in All)
        {
            for (int direction = 0; direction < path.Branches.Count; direction++)
            {
                var branch = path.Branches[direction];
                string small = Id(path.Ascendancy, branch.StableKey, NodeKind.Reinforcement);
                (int x, int y, int coreX, int coreY) = direction switch
                {
                    0 => (0, -92, 0, -190),
                    1 => (80, -46, 165, -95),
                    2 => (80, 46, 165, 95),
                    3 => (0, 92, 0, 190),
                    4 => (-80, 46, -165, 95),
                    _ => (-80, -46, -165, -95),
                };
                nodes.Add(new(small, path.Ascendancy, direction, NodeKind.Reinforcement,
                    branch.ReinforcementName, branch.ReinforcementEffect, null, x, y));
                nodes.Add(new(Id(path.Ascendancy, branch.StableKey, NodeKind.Core), path.Ascendancy, direction, NodeKind.Core,
                    branch.CoreName, branch.CoreEffect, small, coreX, coreY));
            }
        }
        return nodes;
    }

    public static IReadOnlyList<VirtueViceKind> PermanentVirtueVice(CombatProfile profile)
    {
        VirtueViceKind? kind = VirtueViceSources.Ascendancy(profile.Ascendancy);
        if (kind is null) return [];
        AscendancyData path = All.Single(item => item.Ascendancy == profile.Ascendancy);
        int direction = kind.Value switch
        {
            VirtueViceKind.Rage => 3,
            VirtueViceKind.Arrogance => 5,
            VirtueViceKind.Sloth => 0,
            VirtueViceKind.Temperance => 5,
            VirtueViceKind.Mercy => 2,
            _ => 4,
        };
        AscendancyNode core = AscendancyCatalog.For(profile.Ascendancy)
            .Single(node => node.Direction == direction && node.Kind == NodeKind.Core);
        return profile.Has(core.StableId) ? [kind.Value] : [];
    }

    private static IReadOnlyList<AscendancyData> Load()
    {
        Assembly assembly = typeof(AscendancyDefinitions).Assembly;
        string resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("builds-ascendancies.json", StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidDataException("Missing Builds ascendancy resource.");
        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        AscendancyData[] result = JsonSerializer.Deserialize<AscendancyData[]>(stream, options) ?? [];
        if (result.Length != 18 || result.Any(path => path.Branches.Count != 6) || result.Select(path => path.Ascendancy).Distinct().Count() != 18)
            throw new InvalidDataException("Builds ascendancy resource must contain 18 paths and 108 branches.");
        if (result.Any(path => string.IsNullOrWhiteSpace(path.StableKey) || path.Branches.Any(branch => string.IsNullOrWhiteSpace(branch.StableKey)) ||
                path.Branches.Select(branch => branch.StableKey).Distinct(StringComparer.Ordinal).Count() != 6) ||
            result.Select(path => path.StableKey).Distinct(StringComparer.Ordinal).Count() != 18)
            throw new InvalidDataException("Ascendancy stable keys must be present and unique.");
        return result;
    }
}

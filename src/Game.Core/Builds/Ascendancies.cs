using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameForWork.Core.Ascendancies;

namespace GameForWork.Core.Builds;

public sealed record AscendancyBranchData(string Direction, string ReinforcementName,
    string ReinforcementEffect, string CoreName, string CoreEffect);
public sealed record AscendancyData(Ascendancy Ascendancy, string DisplayName,
    IReadOnlyList<AscendancyBranchData> Branches);

public static class AscendancyDefinitions
{
    public static IReadOnlyList<AscendancyData> All { get; } = Load();

    public static IReadOnlyList<AscendancyNode> Apply(IReadOnlyList<AscendancyNode> legacy)
    {
        if (legacy.Count != 216) throw new InvalidDataException("Builds requires exactly 216 ascendancy nodes.");
        return legacy.Select(node =>
        {
            AscendancyData path = All.Single(item => item.Ascendancy == node.Ascendancy);
            AscendancyBranchData branch = path.Branches[node.Direction];
            return node with
            {
                DisplayName = node.Kind == NodeKind.Core ? branch.CoreName : branch.ReinforcementName,
                Effect = node.Kind == NodeKind.Core ? branch.CoreEffect : branch.ReinforcementEffect,
            };
        }).ToArray();
    }

    public static IReadOnlyList<VirtueViceKind> PermanentVirtueVice(CombatProfile profile)
    {
        VirtueViceKind? kind = VirtueViceSources.Ascendancy(profile.Ascendancy);
        if (kind is null) return [];
        AscendancyData path = All.Single(item => item.Ascendancy == profile.Ascendancy);
        int direction = kind.Value switch
        {
            VirtueViceKind.Rage => 3, VirtueViceKind.Arrogance => 5,
            VirtueViceKind.Sloth => 0, VirtueViceKind.Temperance => 5,
            VirtueViceKind.Mercy => 2, _ => 4,
        };
        AscendancyNode core = WarriorAscendancyCatalog.For(profile.Ascendancy)
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
        return result;
    }
}

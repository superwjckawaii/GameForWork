using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameForWork.Core.P18;

namespace GameForWork.Core.P30;

public sealed record P30AscendancyBranchData(string Direction, string ReinforcementName,
    string ReinforcementEffect, string CoreName, string CoreEffect);
public sealed record P30AscendancyData(P18Ascendancy Ascendancy, string DisplayName,
    IReadOnlyList<P30AscendancyBranchData> Branches);

public static class P30Ascendancies
{
    public static IReadOnlyList<P30AscendancyData> All { get; } = Load();

    public static IReadOnlyList<P18AscendancyNode> Apply(IReadOnlyList<P18AscendancyNode> legacy)
    {
        if (legacy.Count != 216) throw new InvalidDataException("P30 requires exactly 216 ascendancy nodes.");
        return legacy.Select(node =>
        {
            P30AscendancyData path = All.Single(item => item.Ascendancy == node.Ascendancy);
            P30AscendancyBranchData branch = path.Branches[node.Direction];
            return node with
            {
                DisplayName = node.Kind == P18NodeKind.Core ? branch.CoreName : branch.ReinforcementName,
                Effect = node.Kind == P18NodeKind.Core ? branch.CoreEffect : branch.ReinforcementEffect,
            };
        }).ToArray();
    }

    public static IReadOnlyList<P30VirtueViceKind> PermanentVirtueVice(P18CombatProfile profile)
    {
        P30VirtueViceKind? kind = P30VirtueViceSources.Ascendancy(profile.Ascendancy);
        if (kind is null) return [];
        P30AscendancyData path = All.Single(item => item.Ascendancy == profile.Ascendancy);
        int direction = kind.Value switch
        {
            P30VirtueViceKind.Rage => 3, P30VirtueViceKind.Arrogance => 5,
            P30VirtueViceKind.Sloth => 0, P30VirtueViceKind.Temperance => 5,
            P30VirtueViceKind.Mercy => 2, _ => 4,
        };
        P18AscendancyNode core = P18AscendancyCatalog.For(profile.Ascendancy)
            .Single(node => node.Direction == direction && node.Kind == P18NodeKind.Core);
        return profile.Has(core.StableId) ? [kind.Value] : [];
    }

    private static IReadOnlyList<P30AscendancyData> Load()
    {
        Assembly assembly = typeof(P30Ascendancies).Assembly;
        string resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("p30-ascendancies.json", StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resource) ?? throw new InvalidDataException("Missing P30 ascendancy resource.");
        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        P30AscendancyData[] result = JsonSerializer.Deserialize<P30AscendancyData[]>(stream, options) ?? [];
        if (result.Length != 18 || result.Any(path => path.Branches.Count != 6) || result.Select(path => path.Ascendancy).Distinct().Count() != 18)
            throw new InvalidDataException("P30 ascendancy resource must contain 18 paths and 108 branches.");
        return result;
    }
}

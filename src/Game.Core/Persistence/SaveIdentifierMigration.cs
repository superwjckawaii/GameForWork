using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GameForWork.Core.Campaign;

namespace GameForWork.Core.Persistence;

/// <summary>Upgrades retired numeric identifier namespaces at the persistence boundary.</summary>
public static partial class SaveIdentifierMigration
{
    private static readonly IReadOnlyDictionary<int, string> Domains = new Dictionary<int, string>
    {
        [1] = "campaign", [5] = "expeditions", [6] = "skills", [9] = "town", [10] = "endgame", [14] = "content", [19] = "equipmentImport",
        [24] = "archetypes", [26] = "atlas", [27] = "monsters", [29] = "resources", [30] = "builds",
    };

    [GeneratedRegex(@"^[pP](\d+)(?=[._-])", RegexOptions.CultureInvariant)]
    private static partial Regex NumericNamespace();
    [GeneratedRegex(@"^(Extended)?[pP](\d+)([A-Z].*)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericProperty();

    public static string Normalize(string id) => NumericNamespace().Replace(id, match =>
        int.TryParse(match.Groups[1].Value, out int number) && Domains.TryGetValue(number, out string? domain)
            ? domain : match.Value);

    public static GameSessionSnapshot Deserialize(string json)
    {
        JsonNode root = JsonNode.Parse(json) ?? throw new InvalidDataException("Empty save snapshot.");
        if (root["FormatVersion"]?.GetValue<int>() < GameSession.CurrentFormatVersion) Rewrite(root);
        return root.Deserialize<GameSessionSnapshot>() ?? throw new InvalidDataException("Empty save snapshot.");
    }

    public static GameSessionSnapshot Upgrade(GameSessionSnapshot snapshot)
    {
        if (snapshot.FormatVersion >= GameSession.CurrentFormatVersion) return snapshot;
        JsonNode root = JsonSerializer.SerializeToNode(snapshot)!;
        Rewrite(root);
        return root.Deserialize<GameSessionSnapshot>() ?? throw new InvalidDataException("Empty save snapshot.");
    }

    private static void Rewrite(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach ((string key, JsonNode? value) in obj.ToArray())
            {
                // User labels and instance identities are opaque and must retain their exact bytes.
                if (key is "Name" or "DisplayName" or "MercenaryName" or "InstanceId" or "CustomName") continue;
                string normalizedKey = NumericProperty().Replace(Normalize(key), match =>
                {
                    if (!int.TryParse(match.Groups[2].Value, out int number)) return match.Value;
                    string suffix = match.Groups[3].Value;
                    if (match.Groups[1].Success) return (number, suffix) switch
                    {
                        (30, "Supports") => "ExtendedBuildsSupports",
                        (30, "SupportLinks") => "ExtendedBuildsSupportLinks",
                        _ => match.Value,
                    };
                    return (number, suffix) switch
                    {
                        (24, "Supports") => "ArchetypeSupports",
                        (30, "Supports") => "SupportIds",
                        (30, "SupportLinks") => "SupportLinks",
                        (30, "Jewels") => "Jewels",
                        (5, "Expedition") => "ExpeditionsExpedition",
                        _ => match.Value,
                    };
                });
                if (normalizedKey != key)
                {
                    if (obj.ContainsKey(normalizedKey)) throw new InvalidDataException("Conflicting saved identifiers.");
                    obj.Remove(key);
                    obj.Add(normalizedKey, value);
                }
                if (value is JsonValue scalar && scalar.TryGetValue<string>(out string? text))
                    obj[normalizedKey] = Normalize(text);
                else if (value is not null) Rewrite(value);
            }
        }
        else if (node is JsonArray array)
        {
            for (int index = 0; index < array.Count; index++)
                if (array[index] is JsonValue scalar && scalar.TryGetValue<string>(out string? text))
                    array[index] = Normalize(text);
                else if (array[index] is { } child) Rewrite(child);
        }
    }
}

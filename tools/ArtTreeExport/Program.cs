using System.Text.Json;
using System.Text.Json.Serialization;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Endgame;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Presentation;

if (args.Length != 1) throw new ArgumentException("Expected one output JSON path.");

var passiveNodes = PassiveTree.Nodes.OrderBy(node => node.StableId, StringComparer.Ordinal)
    .Select(node => Project(node.StableId, node.X, node.Y, node.Kind.ToString(), false,
        PassiveTree.LayoutExtent)).ToArray();
var passiveEdges = PassiveTree.Nodes
    .SelectMany(node => PassiveTree.Neighbors(node.StableId)
        .Where(neighbor => string.CompareOrdinal(node.StableId, neighbor) < 0)
        .Select(neighbor => new TreeEdge(node.StableId, neighbor)))
    .OrderBy(edge => edge.From, StringComparer.Ordinal).ThenBy(edge => edge.To, StringComparer.Ordinal).ToArray();

var ascendancies = Enum.GetValues<Ascendancy>().Where(value => value != Ascendancy.None)
    .Select(value =>
    {
        TreeNode[] nodes = WarriorAscendancyCatalog.For(value)
            .Select(node => Project(node.StableId, node.X, node.Y, node.Kind.ToString(),
                node.Kind == NodeKind.Core, 240))
            .ToArray();
        TreeEdge[] edges = WarriorAscendancyCatalog.For(value)
            .Select(node => new TreeEdge(node.PrerequisiteId ?? $"center:{value}", node.StableId)).ToArray();
        return new NamedTree(value.ToString(), nodes, edges, 240);
    }).ToArray();

TreeNode[] atlasNodes = AtlasTree.Nodes.OrderBy(node => node.StableId, StringComparer.Ordinal)
    .Select(node => Project(node.StableId, node.X, node.Y, node.Notable ? "Notable" : "Small",
        node.Notable, AtlasTree.LayoutExtent)).ToArray();
TreeEdge[] atlasEdges = AtlasTree.Nodes.Where(node => node.PrerequisiteId is not null)
    .Select(node => new TreeEdge(node.PrerequisiteId!, node.StableId)).ToArray();

var document = new TreeDocument(
    new NamedTree("Passive", passiveNodes, passiveEdges, PassiveTree.LayoutExtent),
    ascendancies,
    new NamedTree("Atlas", atlasNodes, atlasEdges, AtlasTree.LayoutExtent),
    GameForWork.Core.Equipment.SkillStoneArt.StableIds.Select((id, index) =>
        new ArtStone(id, index, index < GameForWork.Core.Builds.ActiveSkillCatalog.Active.Count)).ToArray());
var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
options.Converters.Add(new JsonStringEnumConverter());
string destination = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
File.WriteAllText(destination, JsonSerializer.Serialize(document, options));
Console.WriteLine(destination);

static TreeNode Project(string id, float x, float y, string kind, bool major, float extent) =>
    new(id, x, y, TreeProjection.Normalize(x, extent), TreeProjection.Normalize(y, extent), kind, major);

internal sealed record TreeNode(string Id, float X, float Y, float NormalizedX, float NormalizedY, string Kind, bool Major);
internal sealed record TreeEdge(string From, string To);
internal sealed record NamedTree(string Name, IReadOnlyList<TreeNode> Nodes, IReadOnlyList<TreeEdge> Edges, float Extent);
internal sealed record ArtStone(string Id, int Index, bool Active);
internal sealed record TreeDocument(NamedTree Passive, IReadOnlyList<NamedTree> Ascendancies, NamedTree Atlas, IReadOnlyList<ArtStone> Skills);

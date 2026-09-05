using System.Text.Json;
using System.Text.Json.Serialization;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P10;
using GameForWork.Core.P18;
using GameForWork.Core.P31;

if (args.Length != 1) throw new ArgumentException("Expected one output JSON path.");

var passiveNodes = P1PassiveTree.Nodes.OrderBy(node => node.StableId, StringComparer.Ordinal)
    .Select(node => Project(node.StableId, node.X, node.Y, node.Kind.ToString(), false,
        P1PassiveTree.LayoutExtent)).ToArray();
var passiveEdges = P1PassiveTree.Nodes
    .SelectMany(node => P1PassiveTree.Neighbors(node.StableId)
        .Where(neighbor => string.CompareOrdinal(node.StableId, neighbor) < 0)
        .Select(neighbor => new TreeEdge(node.StableId, neighbor)))
    .OrderBy(edge => edge.From, StringComparer.Ordinal).ThenBy(edge => edge.To, StringComparer.Ordinal).ToArray();

var ascendancies = Enum.GetValues<P18Ascendancy>().Where(value => value != P18Ascendancy.None)
    .Select(value =>
    {
        TreeNode[] nodes = P18AscendancyCatalog.For(value)
            .Select(node => Project(node.StableId, node.X, node.Y, node.Kind.ToString(),
                node.Kind == P18NodeKind.Core, 240))
            .ToArray();
        TreeEdge[] edges = P18AscendancyCatalog.For(value)
            .Select(node => new TreeEdge(node.PrerequisiteId ?? $"center:{value}", node.StableId)).ToArray();
        return new NamedTree(value.ToString(), nodes, edges, 240);
    }).ToArray();

TreeNode[] atlasNodes = P10AtlasTree.Nodes.OrderBy(node => node.StableId, StringComparer.Ordinal)
    .Select(node => Project(node.StableId, node.X, node.Y, node.Notable ? "Notable" : "Small",
        node.Notable, P10AtlasTree.LayoutExtent)).ToArray();
TreeEdge[] atlasEdges = P10AtlasTree.Nodes.Where(node => node.PrerequisiteId is not null)
    .Select(node => new TreeEdge(node.PrerequisiteId!, node.StableId)).ToArray();

var document = new TreeDocument(
    new NamedTree("Passive", passiveNodes, passiveEdges, P1PassiveTree.LayoutExtent),
    ascendancies,
    new NamedTree("Atlas", atlasNodes, atlasEdges, P10AtlasTree.LayoutExtent),
    GameForWork.Core.Equipment.SkillStoneArt.StableIds.Select((id, index) =>
        new ArtStone(id, index, index < GameForWork.Core.P30.P30SkillCatalog.Active.Count)).ToArray());
var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
options.Converters.Add(new JsonStringEnumConverter());
string destination = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
File.WriteAllText(destination, JsonSerializer.Serialize(document, options));
Console.WriteLine(destination);

static TreeNode Project(string id, float x, float y, string kind, bool major, float extent) =>
    new(id, x, y, P31TreeProjection.Normalize(x, extent), P31TreeProjection.Normalize(y, extent), kind, major);

internal sealed record TreeNode(string Id, float X, float Y, float NormalizedX, float NormalizedY, string Kind, bool Major);
internal sealed record TreeEdge(string From, string To);
internal sealed record NamedTree(string Name, IReadOnlyList<TreeNode> Nodes, IReadOnlyList<TreeEdge> Edges, float Extent);
internal sealed record ArtStone(string Id, int Index, bool Active);
internal sealed record TreeDocument(NamedTree Passive, IReadOnlyList<NamedTree> Ascendancies, NamedTree Atlas, IReadOnlyList<ArtStone> Skills);

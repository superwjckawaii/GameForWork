using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.Equipment;

public static class EquipmentBaseArt
{
    public const int Columns = 13;
    public const int Rows = 19;
    public static IReadOnlyList<string> ItemBaseIds { get; } = EquipmentCatalog.Snapshot.Bases.Select(item => item.Id).ToArray();
    private static readonly IReadOnlyDictionary<string, int> Indices = ItemBaseIds
        .Select((stableId, index) => (stableId, index)).ToDictionary(pair => pair.stableId, pair => pair.index,
            StringComparer.Ordinal);

    public static int IconIndex(ItemBaseDefinition itemBase)
    {
        string canonical = EquipmentCatalog.ResolveBaseId(itemBase.StableId);
        return Indices.TryGetValue(canonical, out int index)
            ? index
            : throw new KeyNotFoundException($"Equipment art mapping missing for {itemBase.StableId}.");
    }
}

public static class EquipmentLegendaryArt
{
    public const int Columns = 5;
    public const int Rows = 11;
    public static IReadOnlyList<string> StableIds { get; } = Content.UniqueItems.All.Select(value => value.StableId).ToArray();
    private static readonly IReadOnlyDictionary<string, int> Indices = StableIds
        .Select((stableId, index) => (stableId, index)).ToDictionary(pair => pair.stableId, pair => pair.index,
            StringComparer.Ordinal);

    public static int IconIndex(string stableId)
    {
        return Indices.TryGetValue(stableId, out int index)
            ? index : throw new KeyNotFoundException($"Legendary art mapping missing for {stableId}.");
    }
}

public static class SkillStoneArt
{
    public const int Columns = 10;
    public const int Rows = 19;
    public static IReadOnlyList<string> StableIds { get; } = Builds.ActiveSkillCatalog.Active
        .Select(skill => skill.Combat.StoneId).Concat(Builds.ActiveSkillCatalog.Supports.Select(skill => skill.StoneId)).ToArray();
    private static readonly IReadOnlyDictionary<string, int> Indices = StableIds
        .Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);

    public static int IconIndex(string stableId)
    {
        return Indices.TryGetValue(stableId, out int index) ? index
            : throw new KeyNotFoundException($"Skill-stone art mapping missing for {stableId}.");
    }
}


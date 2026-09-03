using GameForWork.Core.P1.Items;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.Equipment;

public static class EquipmentBaseArt
{
    public const int Columns = 13;
    public const int Rows = 12;
    public static IReadOnlyList<string> ItemBaseIds { get; } = EquipmentCatalog.Snapshot.Bases.Select(item => item.Id).ToArray();
    private static readonly IReadOnlyDictionary<string, int> Indices = ItemBaseIds
        .Select((stableId, index) => (stableId, index)).ToDictionary(pair => pair.stableId, pair => pair.index,
            StringComparer.Ordinal);

    public static int IconIndex(ItemBaseDefinition itemBase)
    {
        string canonical = EquipmentCatalog.ResolveBaseId(itemBase.StableId);
        return Indices.TryGetValue(canonical, out int index)
            ? index % 130
            : throw new KeyNotFoundException($"Equipment art mapping missing for {itemBase.StableId}.");
    }
}

public static class EquipmentLegendaryArt
{
    public const int Columns = 5;
    public static IReadOnlyList<string> StableIds { get; } = P14.P14UniqueItems.All.Select(value => value.StableId).ToArray();
    private static readonly IReadOnlyDictionary<string, int> Indices = StableIds
        .Select((stableId, index) => (stableId, index)).ToDictionary(pair => pair.stableId, pair => pair.index,
            StringComparer.Ordinal);

    public static int IconIndex(string stableId)
    {
        if (stableId == "p30.unique.humility_crown") return 2;
        if (stableId == "p30.unique.arrogance_grasp") return 14;
        if (stableId == "p30.unique.rage_temperance_carapace") return 9;
        if (stableId == "p30.unique.paired_virtue_girdle") return 16;
        if (stableId == "core.mythic.heart_of_ash") return 36;
        return Indices.TryGetValue(stableId, out int index)
            ? index % 45 : throw new KeyNotFoundException($"Legendary art mapping missing for {stableId}.");
    }
}

public static class SkillStoneArt
{
    public const int Columns = 10;
    public const int Rows = 9;

    public static int IconIndex(string stableId)
    {
        int active = P24.P24SkillCatalog.Active.ToList().FindIndex(skill => skill.Combat.StoneId == stableId);
        if (active >= 0) return active;
        int support = P24.P24SkillCatalog.Supports.ToList().FindIndex(skill => skill.StoneId == stableId);
        if (support >= 0) return P24.P24SkillCatalog.Active.Count + support;
        throw new KeyNotFoundException($"P25 skill-stone art mapping missing for {stableId}.");
    }
}


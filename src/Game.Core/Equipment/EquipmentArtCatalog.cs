using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.Equipment;

public static class EquipmentBaseArt
{
    public const int Columns = 13;
    public const int Rows = 19;
    public static IReadOnlyList<string> ItemBaseIds { get; } = EquipmentCatalog.Snapshot.Bases
        .Where(item => !item.Tags.Contains("harbor", StringComparer.Ordinal)).Select(item => item.Id).ToArray();
    private static readonly IReadOnlyDictionary<string, int> Indices = ItemBaseIds
        .Select((stableId, index) => (stableId, index)).ToDictionary(pair => pair.stableId, pair => pair.index,
            StringComparer.Ordinal);

    public static int IconIndex(ItemBaseDefinition itemBase)
    {
        string canonical = EquipmentCatalog.ResolveBaseId(itemBase.StableId);
        // Harbor prototype deliberately reuses existing art; dedicated content art is a later delivery.
        canonical = canonical switch
        {
            "harbor.base.returning_tide_sword" => "equipment.base.216.6a136197b0",
            "harbor.base.downstream_quiver" => "equipment.base.quiver.5",
            "harbor.base.still_tide_hood" => "equipment.base.158.12d24b0315",
            "harbor.base.wavechaser_ring" => "equipment.base.iron_ring",
            "harbor.base.harbor_guard_ring" => "equipment.base.life_ring",
            "harbor.base.tidewalker_rapier" => "equipment.base.204.e415283651",
            "harbor.base.cablecleaver_axe" => "equipment.base.208.dcac9f64e5",
            "harbor.base.sunken_anchor_maul" => "equipment.base.224.a30ef959af",
            "harbor.base.tideskimmer_bow" => "equipment.base.228.1b2753f9a3",
            "harbor.base.tidal_wand" => "equipment.base.236.5b39f77ba1",
            "harbor.base.deep_tide_staff" => "equipment.base.236.5b39f77ba1",
            "harbor.base.returning_tide_shield" => "equipment.base.ash_iron_shield",
            "harbor.base.ballast_plate" => "equipment.base.crude_chainmail",
            "harbor.base.wavebreaker_gloves" => "equipment.base.iron_gauntlets",
            "harbor.base.tidewading_boots" => "equipment.base.march_boots",
            "harbor.base.three_tides_amulet" => "equipment.base.ember_amulet",
            "harbor.base.backflow_belt" => "equipment.base.chain_belt",
            "harbor.base.anchored_belt" => "equipment.base.chain_belt",
            _ => canonical,
        };
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


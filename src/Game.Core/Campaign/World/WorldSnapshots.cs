using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Spatial;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Skills;
using GameForWork.Core.Maps;
using GameForWork.Core.Atlas;
using GameForWork.Core.Builds;

namespace GameForWork.Core.Campaign.World;

public sealed record TownEconomySnapshot(
    int Gold,
    int IronScraps,
    int MemoryAshes,
    int WardenMarks,
    int SkillStones,
    IReadOnlyList<MetalCurrencyStack>? Metals = null);

public sealed record EquipmentStorageSnapshot(
    int Capacity,
    IReadOnlyList<ItemInstance> Items,
    IReadOnlyList<string> DiscoveredBases,
    IReadOnlyList<string> DiscoveredLegendaryRules);

public sealed record TeamExpeditionSnapshot(
    ExpeditionTeamKind Kind,
    TeamBuild Build,
    ExpeditionPolicy Policy,
    IReadOnlyList<MapItem> Queue,
    int Level,
    int Experience,
    int EarnedPassivePoints,
    bool FirstBossPassivePointClaimed,
    bool IsStopped,
    string StopReason,
    int MapsCompleted,
    int MapsFailed,
    MapItem? ActiveMap,
    MapRoute ActiveRoute,
    long RemainingMapTimeMilliseconds,
    IReadOnlyList<ItemInstance>? BackpackItems = null,
    int ConsecutiveFailures = 0,
    int MapsRunSincePolicyApplied = 0,
    ExpeditionPolicy? ActivePolicySnapshot = null,
    ExpeditionPolicy? PendingPolicy = null,
    MapRunResult? ActiveRun = null,
    long RouteDecisionRemainingMilliseconds = 0,
    int ConsecutiveCompletedWithoutMapDrop = 0,
    IReadOnlyDictionary<string, int>? LegendaryPoolMisses = null);

public sealed record WorldSnapshot(
    TownEconomySnapshot Economy,
    EquipmentStorageSnapshot Storage,
    IReadOnlyList<LootFilterRule> FilterRules,
    IReadOnlyList<MapItem> MapInventory,
    TeamExpeditionSnapshot Hero,
    TeamExpeditionSnapshot Mercenaries,
    int TeleporterLevel,
    ExpeditionSnapshot? ExpeditionsExpedition = null,
    int MaximumUnlockedMapTier = 16,
    MapFilter? MapCraftFilter = null,
    MapFilter? MapSaleFilter = null,
    MapFilter? AutoSellMapFilter = null,
    long NextMapAcquiredSequence = 1,
    MapBatchRule? MapCraftRule = null);

public static class WorldSnapshots
{
    public static WorldSnapshot Capture(WorldState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var economy = new TownEconomySnapshot(
            state.Economy.Gold,
            state.Economy.IronScraps,
            state.Economy.MemoryAshes,
            state.Economy.WardenMarks,
            state.Economy.SkillStones,
            state.Economy.MetalCurrencies.Select(pair => new MetalCurrencyStack(pair.Key, pair.Value)).ToArray());
        var storage = new EquipmentStorageSnapshot(
            state.Storage.Capacity,
            state.Storage.Items.ToArray(),
            state.Storage.DiscoveredBases.Order(StringComparer.Ordinal).ToArray(),
            state.Storage.DiscoveredLegendaryRules.Order(StringComparer.Ordinal).ToArray());
        return new WorldSnapshot(
            economy,
            storage,
            state.Filter.Rules.ToArray(),
            state.MapInventory.ToArray(),
            CaptureTeam(state.Hero),
            CaptureTeam(state.Mercenaries),
            state.Teleporter.Level,
            state.Expedition.Capture(),
            state.MaximumUnlockedMapTier,
            state.MapCraftFilter,
            state.MapSaleFilter,
            state.AutoSellMapFilter,
            state.NextMapAcquiredSequence,
            state.MapCraftRule);
    }

    public static WorldState Restore(WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var economy = new TownEconomyState(
            snapshot.Economy.Gold,
            snapshot.Economy.IronScraps,
            snapshot.Economy.MemoryAshes,
            snapshot.Economy.WardenMarks,
            snapshot.Economy.SkillStones,
            (snapshot.Economy.Metals ?? []).ToDictionary(item => item.Kind, item => item.Amount));
        var storage = new EquipmentStorage(snapshot.Storage.Capacity);
        foreach (ItemInstance item in snapshot.Storage.Items)
        {
            if (!storage.TryStore(SocketRules.Ensure(item)))
            {
                throw new InvalidDataException("Storage snapshot exceeds its capacity.");
            }
        }

        storage.RestoreDiscoveries(
            snapshot.Storage.DiscoveredBases,
            snapshot.Storage.DiscoveredLegendaryRules);
        var state = new WorldState(
            snapshot.Hero.Build,
            snapshot.Mercenaries.Build,
            economy,
            storage,
            ExpeditionDirector.Restore(snapshot.ExpeditionsExpedition));
        state.Filter.ReplaceRules(snapshot.FilterRules);
        state.MapCraftFilter = (snapshot.MapCraftFilter ?? MapFilter.All).Validate();
        state.MapSaleFilter = (snapshot.MapSaleFilter ?? MapFilter.All).Validate();
        state.AutoSellMapFilter = (snapshot.AutoSellMapFilter ?? MapFilter.All).Validate();
        state.MapCraftRule = (snapshot.MapCraftRule ?? new MapBatchRule()).Validate();
        state.RestoreNextMapAcquiredSequence(snapshot.NextMapAcquiredSequence);
        foreach (MapItem source in snapshot.MapInventory)
        {
            MapItem formal = MapGenerationRules.NormalizeLegacy(source.EnsureFormal(0), 0);
            state.AddMap(formal);
        }
        state.Hero.Restore(snapshot.Hero);
        state.Mercenaries.Restore(snapshot.Mercenaries);
        if (!state.Teleporter.TrySetLevel(snapshot.TeleporterLevel))
        {
            throw new InvalidDataException("Teleporter snapshot level is invalid.");
        }
        state.RestoreMaximumUnlockedMapTier(snapshot.MaximumUnlockedMapTier);

        return state;
    }

    private static TeamExpeditionSnapshot CaptureTeam(TeamExpeditionState team) => new(
        team.Kind,
        team.Build,
        team.Policy,
        team.Queue.Maps,
        team.Progression.Level,
        team.Progression.Experience,
        team.Progression.EarnedPassivePoints,
        team.Progression.FirstBossPassivePointClaimed,
        team.IsStopped,
        team.StopReason,
        team.MapsCompleted,
        team.MapsFailed,
        team.ActiveMap,
        team.ActiveRoute,
        team.RemainingMapTimeMilliseconds,
        team.Backpack.Items.ToArray(),
        team.ConsecutiveFailures,
        team.MapsRunSincePolicyApplied,
        team.ActivePolicySnapshot,
        team.PendingPolicy,
        team.ActiveRun,
        team.RouteDecisionRemainingMilliseconds,
        team.ConsecutiveCompletedWithoutMapDrop,
        new Dictionary<string, int>(team.LegendaryPoolMisses));
}

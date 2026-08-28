using GameForWork.Core.P1.Items;
using GameForWork.Core.P4;
using GameForWork.Core.P5;
using GameForWork.Core.P6;

namespace GameForWork.Core.P1.World;

public sealed record TownEconomySnapshot(
    int ExpeditionSupplies,
    int Gold,
    int IronScraps,
    int MemoryAshes,
    int WardenMarks,
    int SkillStones,
    long SupplyProductionRemainderMilliseconds,
    IReadOnlyList<MetalCurrencyStack>? Metals = null);

public sealed record EquipmentStorageSnapshot(
    int Capacity,
    IReadOnlyList<ItemInstance> Items,
    IReadOnlyList<string> DiscoveredBases,
    IReadOnlyList<string> DiscoveredLegendaryRules);

public sealed record P1TeamExpeditionSnapshot(
    ExpeditionTeamKind Kind,
    P1TeamBuild Build,
    ExpeditionPolicy Policy,
    IReadOnlyList<P1MapItem> Queue,
    int Level,
    int Experience,
    int EarnedPassivePoints,
    bool FirstBossPassivePointClaimed,
    bool IsStopped,
    string StopReason,
    int MapsCompleted,
    int MapsFailed,
    P1MapItem? ActiveMap,
    MapRoute ActiveRoute,
    long RemainingMapTimeMilliseconds,
    IReadOnlyList<ItemInstance>? BackpackItems = null,
    int ConsecutiveFailures = 0,
    int MapsRunSincePolicyApplied = 0,
    ExpeditionPolicy? ActivePolicySnapshot = null,
    ExpeditionPolicy? PendingPolicy = null,
    P1MapRunResult? ActiveRun = null);

public sealed record P1WorldSnapshot(
    TownEconomySnapshot Economy,
    EquipmentStorageSnapshot Storage,
    IReadOnlyList<LootFilterRule> FilterRules,
    IReadOnlyList<P1MapItem> MapInventory,
    P1TeamExpeditionSnapshot Hero,
    P1TeamExpeditionSnapshot Mercenaries,
    int TeleporterLevel,
    P5ExpeditionSnapshot? P5Expedition = null);

public static class P1WorldSnapshots
{
    public static P1WorldSnapshot Capture(P1WorldState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var economy = new TownEconomySnapshot(
            state.Economy.ExpeditionSupplies,
            state.Economy.Gold,
            state.Economy.IronScraps,
            state.Economy.MemoryAshes,
            state.Economy.WardenMarks,
            state.Economy.SkillStones,
            state.Economy.SupplyProductionRemainderMilliseconds,
            state.Economy.MetalCurrencies.Select(pair => new MetalCurrencyStack(pair.Key, pair.Value)).ToArray());
        var storage = new EquipmentStorageSnapshot(
            state.Storage.Capacity,
            state.Storage.Items.ToArray(),
            state.Storage.DiscoveredBases.Order(StringComparer.Ordinal).ToArray(),
            state.Storage.DiscoveredLegendaryRules.Order(StringComparer.Ordinal).ToArray());
        return new P1WorldSnapshot(
            economy,
            storage,
            state.Filter.Rules.ToArray(),
            state.MapInventory.ToArray(),
            CaptureTeam(state.Hero),
            CaptureTeam(state.Mercenaries),
            state.Teleporter.Level,
            state.Expedition.Capture());
    }

    public static P1WorldState Restore(P1WorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var economy = new TownEconomyState(
            snapshot.Economy.ExpeditionSupplies,
            snapshot.Economy.Gold,
            snapshot.Economy.IronScraps,
            snapshot.Economy.MemoryAshes,
            snapshot.Economy.WardenMarks,
            snapshot.Economy.SkillStones,
            snapshot.Economy.SupplyProductionRemainderMilliseconds,
            (snapshot.Economy.Metals ?? []).ToDictionary(item => item.Kind, item => item.Amount));
        var storage = new EquipmentStorage(snapshot.Storage.Capacity);
        foreach (ItemInstance item in snapshot.Storage.Items)
        {
            if (!storage.TryStore(P6SocketRules.Ensure(item)))
            {
                throw new InvalidDataException("Storage snapshot exceeds its capacity.");
            }
        }

        storage.RestoreDiscoveries(
            snapshot.Storage.DiscoveredBases,
            snapshot.Storage.DiscoveredLegendaryRules);
        var state = new P1WorldState(
            snapshot.Hero.Build,
            snapshot.Mercenaries.Build,
            economy,
            storage,
            P5ExpeditionDirector.Restore(snapshot.P5Expedition));
        state.Filter.ReplaceRules(snapshot.FilterRules);
        state.MapInventory.AddRange(snapshot.MapInventory);
        state.Hero.Restore(snapshot.Hero);
        state.Mercenaries.Restore(snapshot.Mercenaries);
        if (!state.Teleporter.TrySetLevel(snapshot.TeleporterLevel))
        {
            throw new InvalidDataException("Teleporter snapshot level is invalid.");
        }

        return state;
    }

    private static P1TeamExpeditionSnapshot CaptureTeam(P1TeamExpeditionState team) => new(
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
        team.ActiveRun);
}

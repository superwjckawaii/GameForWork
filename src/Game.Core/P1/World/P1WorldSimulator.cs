using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Offline;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;

namespace GameForWork.Core.P1.World;

public sealed class P1TeamExpeditionState
{
    public P1TeamExpeditionState(
        ExpeditionTeamKind kind,
        P1TeamBuild build,
        ExpeditionPolicy? policy = null)
    {
        Kind = kind;
        Build = build;
        Policy = policy ?? ExpeditionPolicy.Recommended;
    }

    public ExpeditionTeamKind Kind { get; }
    public P1TeamBuild Build { get; private set; }
    public ExpeditionPolicy Policy { get; set; }
    public P1MapQueue Queue { get; } = new();
    public CharacterProgression Progression { get; } = new();
    public bool IsStopped { get; private set; }
    public string StopReason { get; private set; } = string.Empty;
    public int MapsCompleted { get; private set; }
    public int MapsFailed { get; private set; }
    public P1MapRunResult? LastRun { get; private set; }
    public P1MapItem? ActiveMap { get; private set; }
    public MapRoute ActiveRoute { get; private set; }
    public long RemainingMapTimeMilliseconds { get; private set; }

    public void StartMap(P1MapItem map, MapRoute route, long durationMilliseconds)
    {
        if (ActiveMap is not null || durationMilliseconds <= 0)
        {
            throw new InvalidOperationException("The team cannot start another map now.");
        }

        ActiveMap = map;
        ActiveRoute = route;
        RemainingMapTimeMilliseconds = durationMilliseconds;
    }

    public void SaveRemainingMapTime(long remainingMilliseconds)
    {
        if (ActiveMap is null || remainingMilliseconds <= 0)
        {
            throw new InvalidOperationException("There is no active map to save.");
        }

        RemainingMapTimeMilliseconds = remainingMilliseconds;
    }

    public void RecordRun(P1MapRunResult run)
    {
        ActiveMap = null;
        RemainingMapTimeMilliseconds = 0;
        LastRun = run;
        if (run.Succeeded)
        {
            MapsCompleted++;
            Progression.AddExperience(P1MapRewardGenerator.ExperiencePerMap);
            Progression.ClaimFirstBossPassivePoint();
            return;
        }

        MapsFailed++;
        if (Policy.FailureBehavior == QueueFailureBehavior.Stop)
        {
            Stop("map_failed");
        }
    }

    public void Stop(string reason)
    {
        IsStopped = true;
        StopReason = reason;
    }

    public void UpdateBuild(P1TeamBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);
        Build = build;
    }

    public void Restore(P1TeamExpeditionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Kind != Kind || snapshot.MapsCompleted < 0 || snapshot.MapsFailed < 0)
        {
            throw new InvalidDataException("Team expedition snapshot is invalid.");
        }

        Policy = snapshot.Policy;
        Queue.Restore(snapshot.Queue);
        Progression.Restore(
            snapshot.Level,
            snapshot.Experience,
            snapshot.EarnedPassivePoints,
            snapshot.FirstBossPassivePointClaimed);
        MapsCompleted = snapshot.MapsCompleted;
        MapsFailed = snapshot.MapsFailed;
        IsStopped = snapshot.IsStopped;
        StopReason = snapshot.StopReason;
        if (snapshot.ActiveMap is not null)
        {
            StartMap(snapshot.ActiveMap, snapshot.ActiveRoute, snapshot.RemainingMapTimeMilliseconds);
        }
    }
}

public sealed class P1WorldState
{
    public P1WorldState(
        P1TeamBuild hero,
        P1TeamBuild mercenaries,
        TownEconomyState? economy = null,
        EquipmentStorage? storage = null)
    {
        Hero = new P1TeamExpeditionState(ExpeditionTeamKind.Hero, hero);
        Mercenaries = new P1TeamExpeditionState(ExpeditionTeamKind.Mercenaries, mercenaries);
        Economy = economy ?? new TownEconomyState();
        Storage = storage ?? new EquipmentStorage();
    }

    public TownEconomyState Economy { get; }
    public EquipmentStorage Storage { get; }
    public LootFilter Filter { get; } = new();
    public TeleporterState Teleporter { get; } = new();
    public List<P1MapItem> MapInventory { get; } = [];
    public P1TeamExpeditionState Hero { get; }
    public P1TeamExpeditionState Mercenaries { get; }
    public IReadOnlyList<P1TeamExpeditionState> Teams => [Hero, Mercenaries];

    public void AddInitialMaps()
    {
        if (Hero.Queue.Count != 0 || Mercenaries.Queue.Count != 0 || MapInventory.Count != 0)
        {
            throw new InvalidOperationException("Initial maps can only be added to an empty world.");
        }

        for (int index = 0; index < 10; index++)
        {
            var map = new P1MapItem($"initial-map-{index + 1:00}", 1);
            bool queued = index % 2 == 0 ? Hero.Queue.TryEnqueue(map) : Mercenaries.Queue.TryEnqueue(map);
            if (!queued)
            {
                throw new InvalidOperationException("Initial map queue unexpectedly rejected a map.");
            }
        }
    }
}

public sealed record P1OfflineTeamSummary(
    ExpeditionTeamKind Team,
    int MapsCompleted,
    int MapsFailed,
    int RemainingQueue,
    bool Stopped,
    string StopReason);

public sealed record P1OfflineResult(
    long EffectiveMilliseconds,
    bool WasClamped,
    int SuppliesProduced,
    int TotalMapsCompleted,
    int TotalMapsFailed,
    IReadOnlyList<P1OfflineTeamSummary> Teams,
    string FinalHash);

public sealed class P1WorldSimulator(IP1MapAttemptResolver attemptResolver)
{
    private const long SafeMapDurationMilliseconds = 90_000;
    private const long AbyssMapDurationMilliseconds = 120_000;

    public P1OfflineResult Simulate(P1WorldState state, long elapsedMilliseconds, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(attemptResolver);
        long effective = Math.Clamp(elapsedMilliseconds, 0, OfflineTime.MaximumMilliseconds);
        int initialCompleted = state.Teams.Sum(team => team.MapsCompleted);
        int initialFailed = state.Teams.Sum(team => team.MapsFailed);
        var active = new Dictionary<ExpeditionTeamKind, ActiveExpedition>();
        long now = 0;
        int suppliesProduced = 0;
        foreach (P1TeamExpeditionState team in state.Teams.Where(team => team.ActiveMap is not null))
        {
            active[team.Kind] = new ActiveExpedition(
                team,
                team.ActiveMap!,
                team.ActiveRoute,
                team.RemainingMapTimeMilliseconds);
        }

        if (effective > 0)
        {
            StartReadyTeams(state, active, now);
        }

        while (now < effective)
        {
            long nextCompletion = active.Count == 0
                ? long.MaxValue
                : active.Values.Min(item => item.CompletionTimeMilliseconds);
            bool waitingForSupply = state.Teams.Any(team =>
                !team.IsStopped && team.Queue.Count > 0 && !active.ContainsKey(team.Kind));
            long untilSupply = TownEconomyState.SupplyProductionIntervalMilliseconds -
                state.Economy.SupplyProductionRemainderMilliseconds;
            long nextSupply = waitingForSupply ? checked(now + untilSupply) : long.MaxValue;
            long nextEvent = Math.Min(effective, Math.Min(nextCompletion, nextSupply));
            if (nextEvent == long.MaxValue)
            {
                nextEvent = effective;
            }

            long delta = nextEvent - now;
            suppliesProduced = checked(suppliesProduced + state.Economy.AdvanceProduction(delta));
            now = nextEvent;
            ActiveExpedition[] completed = active.Values
                .Where(item => item.CompletionTimeMilliseconds == now)
                .OrderBy(item => item.Team.Kind)
                .ToArray();
            foreach (ActiveExpedition expedition in completed)
            {
                active.Remove(expedition.Team.Kind);
                ResolveExpedition(state, expedition, DeriveExpeditionSeed(seed, expedition));
            }

            if (now < effective)
            {
                StartReadyTeams(state, active, now);
            }
            bool noPendingMaps = state.Teams.All(team => team.Queue.Count == 0 || team.IsStopped);
            if (active.Count == 0 && noPendingMaps)
            {
                if (now < effective)
                {
                    suppliesProduced = checked(
                        suppliesProduced + state.Economy.AdvanceProduction(effective - now));
                    now = effective;
                }

                break;
            }
        }

        foreach (ActiveExpedition expedition in active.Values)
        {
            expedition.Team.SaveRemainingMapTime(expedition.CompletionTimeMilliseconds - effective);
        }

        int totalCompleted = state.Teams.Sum(team => team.MapsCompleted) - initialCompleted;
        int totalFailed = state.Teams.Sum(team => team.MapsFailed) - initialFailed;
        P1OfflineTeamSummary[] summaries = state.Teams.Select(team => new P1OfflineTeamSummary(
            team.Kind,
            team.MapsCompleted,
            team.MapsFailed,
            team.Queue.Count,
            team.IsStopped,
            team.StopReason)).ToArray();
        return new P1OfflineResult(
            effective,
            elapsedMilliseconds > OfflineTime.MaximumMilliseconds,
            suppliesProduced,
            totalCompleted,
            totalFailed,
            summaries,
            Hash(state, effective, seed));
    }

    private static void StartReadyTeams(
        P1WorldState state,
        IDictionary<ExpeditionTeamKind, ActiveExpedition> active,
        long now)
    {
        foreach (P1TeamExpeditionState team in state.Teams)
        {
            if (team.IsStopped || active.ContainsKey(team.Kind) || team.Queue.Count == 0 ||
                !state.Economy.TryConsumeMapSupply())
            {
                continue;
            }

            if (!team.Queue.TryDequeue(out P1MapItem? map) || map is null)
            {
                throw new InvalidOperationException("Map queue count and dequeue result disagree.");
            }

            MapRoute route = team.Policy.SelectUnattendedRoute();
            long duration = route == MapRoute.Safe ? SafeMapDurationMilliseconds : AbyssMapDurationMilliseconds;
            team.StartMap(map, route, duration);
            active[team.Kind] = new ActiveExpedition(team, map, route, checked(now + duration));
        }
    }

    private void ResolveExpedition(P1WorldState state, ActiveExpedition expedition, ulong seed)
    {
        P1MapRunResult run = new P1MapRunner(attemptResolver).Run(
            expedition.Map,
            expedition.Route,
            expedition.Team.Build,
            seed);
        expedition.Team.RecordRun(run);
        if (!run.Succeeded)
        {
            return;
        }

        P1MapRewards rewards = P1MapRewardGenerator.Generate(expedition.Map, expedition.Route, seed ^ 0x9e3779b97f4a7c15UL);
        state.Economy.AddRewards(rewards.Stackables);
        state.MapInventory.AddRange(rewards.Maps);
        LootProcessingResult processed = LootProcessor.Process(
            rewards.Equipment,
            state.Storage,
            state.Filter,
            expedition.Team.Policy.StorageFullBehavior);
        state.Economy.AddDispositionProceeds(processed.GoldGained, processed.IronScrapsGained);
        if (processed.ExpeditionMustStop)
        {
            expedition.Team.Stop("storage_full");
        }
    }

    private static string Hash(P1WorldState state, long elapsed, ulong seed)
    {
        var builder = new StringBuilder();
        builder.Append(seed).Append('|').Append(elapsed).Append('|')
            .Append(state.Economy.ExpeditionSupplies).Append('|')
            .Append(state.Economy.Gold).Append('|')
            .Append(state.Economy.IronScraps).Append('|')
            .Append(state.Economy.MemoryAshes).Append('|')
            .Append(state.Economy.WardenMarks).Append('|')
            .Append(state.Storage.Count).Append('|').Append(state.MapInventory.Count);
        foreach (P1TeamExpeditionState team in state.Teams)
        {
            builder.Append('|').Append(team.Kind).Append(':').Append(team.MapsCompleted).Append(':')
                .Append(team.MapsFailed).Append(':').Append(team.Queue.Count).Append(':')
                .Append(team.Progression.Level).Append(':').Append(team.IsStopped).Append(':')
                .Append(team.ActiveMap?.InstanceId).Append(':').Append(team.RemainingMapTimeMilliseconds);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static ulong DeriveExpeditionSeed(ulong seed, ActiveExpedition expedition)
    {
        string identity = $"{seed}|{expedition.Team.Kind}|{expedition.Map.InstanceId}|" +
            $"{expedition.Map.AreaLevel}|{expedition.Route}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return BitConverter.ToUInt64(hash, 0);
    }

    private sealed record ActiveExpedition(
        P1TeamExpeditionState Team,
        P1MapItem Map,
        MapRoute Route,
        long CompletionTimeMilliseconds);
}

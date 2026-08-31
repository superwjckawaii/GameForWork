using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Offline;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P5;
using GameForWork.Core.P6;
using GameForWork.Core.P26;
using GameForWork.Core.P20;

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
        Policy = (policy ?? ExpeditionPolicy.Recommended).Validate();
    }

    public ExpeditionTeamKind Kind { get; }
    public P1TeamBuild Build { get; private set; }
    public ExpeditionPolicy Policy { get; set; }
    public ExpeditionPolicy? ActivePolicySnapshot { get; private set; }
    public ExpeditionPolicy? PendingPolicy { get; private set; }
    public P1MapQueue Queue { get; } = new();
    public CharacterProgression Progression { get; } = new();
    public bool IsStopped { get; private set; }
    public string StopReason { get; private set; } = string.Empty;
    public int MapsCompleted { get; private set; }
    public int MapsFailed { get; private set; }
    public P1MapRunResult? LastRun { get; private set; }
    public ExpeditionBackpack Backpack { get; } = new();
    public P1MapItem? ActiveMap { get; private set; }
    public P1MapRunResult? ActiveRun { get; private set; }
    public MapRoute ActiveRoute { get; private set; }
    public long RemainingMapTimeMilliseconds { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public int MapsRunSincePolicyApplied { get; private set; }
    public long RouteDecisionRemainingMilliseconds { get; private set; }
    public int ConsecutiveCompletedWithoutMapDrop { get; private set; }
    private readonly Dictionary<string, int> _legendaryPoolMisses = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, int> LegendaryPoolMisses => _legendaryPoolMisses;

    public bool RecordCompletedMapDrop(int mapCount, int threshold)
    {
        if (mapCount > 0) { ConsecutiveCompletedWithoutMapDrop = 0; return false; }
        ConsecutiveCompletedWithoutMapDrop++;
        if (ConsecutiveCompletedWithoutMapDrop < threshold) return false;
        ConsecutiveCompletedWithoutMapDrop = 0; return true;
    }

    public bool ShouldGuaranteeLegendary(string pool, bool dropped)
    {
        if (dropped) { _legendaryPoolMisses[pool] = 0; return false; }
        int misses = _legendaryPoolMisses.GetValueOrDefault(pool);
        if (misses >= 10) { _legendaryPoolMisses[pool] = 0; return true; }
        _legendaryPoolMisses[pool] = misses + 1; return false;
    }

    public bool WaitForRouteDecision(P1MapItem map, bool offline)
    {
        if (offline || map.SelectedRoute is not null || map.EffectiveRouteCandidates.Count <= 1 ||
            Policy.RouteDecisionTimeoutSeconds == 0)
        {
            RouteDecisionRemainingMilliseconds = 0;
            return false;
        }
        if (RouteDecisionRemainingMilliseconds < 0) return false;
        if (RouteDecisionRemainingMilliseconds == 0)
            RouteDecisionRemainingMilliseconds = Policy.RouteDecisionTimeoutSeconds * 1_000L;
        return true;
    }

    public void AdvanceRouteDecision(long elapsedMilliseconds, bool offline)
    {
        if (Queue.Count == 0 || ActiveMap is not null) { RouteDecisionRemainingMilliseconds = 0; return; }
        if (offline) RouteDecisionRemainingMilliseconds = -1;
        else if (RouteDecisionRemainingMilliseconds > 0)
        {
            long remaining = Math.Max(0, RouteDecisionRemainingMilliseconds - elapsedMilliseconds);
            RouteDecisionRemainingMilliseconds = remaining == 0 ? -1 : remaining;
        }
    }

    public void StartMap(
        P1MapItem map,
        MapRoute route,
        long durationMilliseconds,
        P1MapRunResult? plannedRun = null)
    {
        if (ActiveMap is not null || durationMilliseconds <= 0)
        {
            throw new InvalidOperationException("The team cannot start another map now.");
        }

        ActiveMap = map;
        ActiveRoute = route;
        ActiveRun = plannedRun;
        ActivePolicySnapshot = Policy;
        RouteDecisionRemainingMilliseconds = 0;
        MapsRunSincePolicyApplied++;
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

    public void RecordRun(P1MapRunResult run, bool countProgression = true, int defeatedExperience = 0)
    {
        ExpeditionPolicy runPolicy = ActivePolicySnapshot ?? Policy;
        ActiveMap = null;
        ActiveRun = null;
        RemainingMapTimeMilliseconds = 0;
        ActivePolicySnapshot = null;
        LastRun = run;
        if (run.Succeeded)
        {
            if (countProgression)
            {
                MapsCompleted++;
                Progression.AddExperience(P1MapRewardGenerator.ExperiencePerMap);
                Progression.ClaimFirstBossPassivePoint();
            }
            ConsecutiveFailures = 0;
            CommitPendingPolicy();
            return;
        }

        MapsFailed++;
        if (countProgression && defeatedExperience > 0)
            Progression.AddExperience(defeatedExperience);
        ConsecutiveFailures++;
        if (runPolicy.FailureBehavior == QueueFailureBehavior.Stop)
        {
            Stop("map_failed");
        }

        CommitPendingPolicy();
    }

    public void Stop(string reason)
    {
        IsStopped = true;
        StopReason = reason;
    }

    public void Resume()
    {
        IsStopped = false;
        StopReason = string.Empty;
    }

    public void ResumeForNewDispatch()
    {
        Resume();
        ConsecutiveFailures = 0;
    }

    public void ApplyPolicy(ExpeditionPolicy policy)
    {
        policy = policy.Validate();
        if (ActiveMap is null)
        {
            Policy = policy;
            PendingPolicy = null;
            MapsRunSincePolicyApplied = 0;
        }
        else
        {
            PendingPolicy = policy;
        }
    }

    public string? GetStopCondition(P1WorldState state)
    {
        int freeSlots = state.Storage.Capacity - state.Storage.Count;
        if (Policy.MaximumContinuousMaps > 0 && MapsRunSincePolicyApplied >= Policy.MaximumContinuousMaps)
        {
            return "maximum_continuous_maps";
        }

        if (Policy.StopAfterConsecutiveFailures > 0 && ConsecutiveFailures >= Policy.StopAfterConsecutiveFailures)
        {
            return "consecutive_failures";
        }

        return Policy.MinimumStorageFreeSlots > 0 && freeSlots < Policy.MinimumStorageFreeSlots
            ? "minimum_storage_free_slots"
            : null;
    }

    public void UpdateBuild(P1TeamBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);
        Build = build;
    }

    public void Restore(P1TeamExpeditionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Kind != Kind || snapshot.MapsCompleted < 0 || snapshot.MapsFailed < 0 ||
            snapshot.ConsecutiveFailures < 0 || snapshot.MapsRunSincePolicyApplied < 0)
        {
            throw new InvalidDataException("Team expedition snapshot is invalid.");
        }

        try
        {
            Policy = snapshot.Policy.Validate();
            ActivePolicySnapshot = snapshot.ActivePolicySnapshot?.Validate();
            PendingPolicy = snapshot.PendingPolicy?.Validate();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Team expedition policy is invalid.", exception);
        }

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
        Backpack.Replace(snapshot.BackpackItems ?? []);
        ConsecutiveFailures = snapshot.ConsecutiveFailures;
        MapsRunSincePolicyApplied = snapshot.MapsRunSincePolicyApplied;
        RouteDecisionRemainingMilliseconds = snapshot.RouteDecisionRemainingMilliseconds;
        ConsecutiveCompletedWithoutMapDrop = Math.Max(0, snapshot.ConsecutiveCompletedWithoutMapDrop);
        _legendaryPoolMisses.Clear();
        foreach (var pair in snapshot.LegendaryPoolMisses ?? new Dictionary<string, int>())
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value is >= 0 and <= 10) _legendaryPoolMisses[pair.Key] = pair.Value;
        if (snapshot.ActiveMap is not null)
        {
            StartMap(snapshot.ActiveMap, snapshot.ActiveRoute, snapshot.RemainingMapTimeMilliseconds, snapshot.ActiveRun);
            ActivePolicySnapshot = snapshot.ActivePolicySnapshot ?? Policy;
            MapsRunSincePolicyApplied = snapshot.MapsRunSincePolicyApplied;
        }
    }

    public void AttachActiveRun(P1MapRunResult run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (ActiveMap is null || run.Map.InstanceId != ActiveMap.InstanceId)
        {
            throw new InvalidOperationException("The planned run does not belong to the active map.");
        }

        ActiveRun = run;
        RemainingMapTimeMilliseconds = Math.Min(
            RemainingMapTimeMilliseconds,
            Math.Max(50, run.DurationMilliseconds));
    }

    private void CommitPendingPolicy()
    {
        if (PendingPolicy is null)
        {
            return;
        }

        Policy = PendingPolicy;
        PendingPolicy = null;
        MapsRunSincePolicyApplied = 0;
    }
}

public sealed class P1WorldState
{
    public P1WorldState(
        P1TeamBuild hero,
        P1TeamBuild mercenaries,
        TownEconomyState? economy = null,
        EquipmentStorage? storage = null,
        P5ExpeditionDirector? expedition = null)
    {
        Hero = new P1TeamExpeditionState(ExpeditionTeamKind.Hero, hero);
        Mercenaries = new P1TeamExpeditionState(ExpeditionTeamKind.Mercenaries, mercenaries);
        Economy = economy ?? new TownEconomyState();
        Storage = storage ?? new EquipmentStorage();
        Expedition = expedition ?? new P5ExpeditionDirector();
    }

    public TownEconomyState Economy { get; }
    public EquipmentStorage Storage { get; }
    public P5ExpeditionDirector Expedition { get; }
    public LootFilter Filter { get; } = new();
    public TeleporterState Teleporter { get; } = new();
    public List<P1MapItem> MapInventory { get; } = [];
    public P26MapFilter MapCraftFilter { get; set; } = P26MapFilter.All;
    public P26MapFilter MapSaleFilter { get; set; } = P26MapFilter.All;
    public P26MapFilter AutoSellMapFilter { get; set; } = P26MapFilter.All;
    private long _nextMapAcquiredSequence = 1;
    public P1TeamExpeditionState Hero { get; }
    public P1TeamExpeditionState Mercenaries { get; }
    public IReadOnlyList<P1TeamExpeditionState> Teams => [Hero, Mercenaries];
    public int MaximumUnlockedMapTier { get; private set; } = 16;

    public void AddMap(P1MapItem map)
    {
        ArgumentNullException.ThrowIfNull(map);
        P1MapItem sequenced = map.AcquiredSequence > 0 ? map : map with { AcquiredSequence = _nextMapAcquiredSequence++ };
        _nextMapAcquiredSequence = Math.Max(_nextMapAcquiredSequence, sequenced.AcquiredSequence + 1);
        MapInventory.Add(sequenced);
        EnforceMapInventoryLimit();
    }

    public void AddMaps(IEnumerable<P1MapItem> maps)
    {
        foreach (P1MapItem map in maps) AddMap(map);
    }

    public void EnforceMapInventoryLimit()
    {
        IReadOnlyList<P1MapItem> retained = P26MapRules.EnforceInventoryLimit(MapInventory, AutoSellMapFilter,
            out int gold, out _);
        if (retained.Count == MapInventory.Count) return;
        MapInventory.Clear();
        MapInventory.AddRange(retained);
        Economy.AddDispositionProceeds(gold, 0);
    }

    internal long NextMapAcquiredSequence => _nextMapAcquiredSequence;
    internal void RestoreNextMapAcquiredSequence(long value) => _nextMapAcquiredSequence = Math.Max(1, value);

    public void UnlockFinalMapTiers() => MaximumUnlockedMapTier = P1MapItem.MaximumAreaLevel;

    internal void RestoreMaximumUnlockedMapTier(int tier)
    {
        if (tier is < 16 or > P1MapItem.MaximumAreaLevel)
            throw new InvalidDataException("Unlocked map tier is invalid.");
        MaximumUnlockedMapTier = tier;
    }

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

public sealed record P1OfflineSegment(
    ExpeditionTeamKind Team, string MapInstanceId, int Tier, MapRoute Route,
    long StartMilliseconds, long DurationMilliseconds, P1BattleOutcome Outcome,
    int LootCount, string StopReason);

public sealed record P1OfflineResult(
    long EffectiveMilliseconds,
    bool WasClamped,
    int TotalMapsCompleted,
    int TotalMapsFailed,
    IReadOnlyList<P1OfflineTeamSummary> Teams,
    string FinalHash,
    IReadOnlyList<P1OfflineSegment>? Segments = null);

public sealed class P1WorldSimulator(IP1MapAttemptResolver attemptResolver)
{
    private sealed record RunPreparation(string MapId, MapRoute Route, ulong Seed, int BuildHash, Task<P1MapRunResult> Task,
        GameForWork.Core.P28.P28GameplayPolicy? Gameplay, string AtlasKey);
    private readonly Dictionary<ExpeditionTeamKind, RunPreparation> _runPreparations = [];
    public Action<P1TeamExpeditionState, P1MapItem, MapRoute>? MapStarted { get; set; }
    public Func<P1MapItem, P1MapItem>? PrepareMap { get; set; }
    public Action<P1TeamExpeditionState, P1MapRunResult, ulong, int, ExpeditionPolicy>? MapResolved { get; set; }

    public P1OfflineResult Simulate(P1WorldState state, long elapsedMilliseconds, ulong seed, bool offline = false,
        bool asyncPreparation = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(attemptResolver);
        long effective = Math.Clamp(elapsedMilliseconds, 0, OfflineTime.MaximumMilliseconds);
        int initialCompleted = state.Teams.Sum(team => team.MapsCompleted);
        int initialFailed = state.Teams.Sum(team => team.MapsFailed);
        var active = new Dictionary<ExpeditionTeamKind, ActiveExpedition>();
        var segments = new List<P1OfflineSegment>();
        long now = 0;
        foreach (P1TeamExpeditionState team in state.Teams) team.AdvanceRouteDecision(effective, offline);
        foreach (P1TeamExpeditionState team in state.Teams.Where(team => team.ActiveMap is not null))
        {
            if (team.ActiveRun is null)
            {
                ulong activeSeed = DeriveExpeditionSeed(seed, team, team.ActiveMap!, team.ActiveRoute);
                P1MapRunResult restoredRun = new P1MapRunner(attemptResolver).Run(
                    team.ActiveMap!, team.ActiveRoute, team.Build, activeSeed);
                team.AttachActiveRun(restoredRun);
            }

            active[team.Kind] = new ActiveExpedition(
                team,
                team.ActiveMap!,
                team.ActiveRoute,
                team.RemainingMapTimeMilliseconds);
        }

        if (effective > 0)
        {
            StartReadyTeams(state, active, now, seed, asyncPreparation, offline);
        }

        while (now < effective)
        {
            long nextCompletion = active.Count == 0
                ? long.MaxValue
                : active.Values.Min(item => item.CompletionTimeMilliseconds);
            long nextEvent = Math.Min(effective, nextCompletion);
            if (nextEvent == long.MaxValue)
            {
                nextEvent = effective;
            }

            now = nextEvent;
            ActiveExpedition[] completed = active.Values
                .Where(item => item.CompletionTimeMilliseconds == now)
                .OrderBy(item => item.Team.Kind)
                .ToArray();
            foreach (ActiveExpedition expedition in completed)
            {
                active.Remove(expedition.Team.Kind);
                P1MapRunResult? completedRun = expedition.Team.ActiveRun;
                int lootBefore = expedition.Team.Backpack.Count;
                ResolveExpedition(state, expedition, DeriveExpeditionSeed(seed, expedition), offline);
                long duration = completedRun?.DurationMilliseconds ?? 0;
                segments.Add(new P1OfflineSegment(expedition.Team.Kind, expedition.Map.InstanceId,
                    expedition.Map.Tier, expedition.Route, Math.Max(0, now - duration), duration,
                    completedRun is null ? P1BattleOutcome.Timeout : completedRun.Succeeded
                        ? P1BattleOutcome.HeroVictory
                        : completedRun.Attempts.LastOrDefault()?.Timeline?.Outcome ?? P1BattleOutcome.EnemyVictory,
                    Math.Max(0, expedition.Team.Backpack.Count - lootBefore), expedition.Team.StopReason));
            }

            if (now < effective)
            {
                StartReadyTeams(state, active, now, seed, asyncPreparation, offline);
            }
            bool noPendingMaps = state.Teams.All(team => team.Queue.Count == 0 || team.IsStopped);
            if (active.Count == 0 && noPendingMaps)
            {
                now = effective;
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
            totalCompleted,
            totalFailed,
            summaries,
            Hash(state, effective, seed),
            segments);
    }

    private void StartReadyTeams(
        P1WorldState state,
        IDictionary<ExpeditionTeamKind, ActiveExpedition> active,
        long now,
        ulong worldSeed,
        bool asyncPreparation,
        bool offline)
    {
        foreach (P1TeamExpeditionState team in state.Teams)
        {
            state.Expedition.PrepareNext(state, team);
            if (team.IsStopped || active.ContainsKey(team.Kind) || team.Queue.Count == 0)
            {
                continue;
            }


            string? stopCondition = team.GetStopCondition(state);
            if (stopCondition is not null)
            {
                team.Stop(stopCondition);
                continue;
            }

            P1MapItem queuedSource = team.Queue.Maps[0];
            bool enteredAsFormalMap = !string.IsNullOrWhiteSpace(queuedSource.AreaId) && queuedSource.EffectiveRouteCandidates.Count > 0;
            P1MapItem queuedMap = queuedSource.EnsureFormal(worldSeed);
            queuedMap = PrepareMap?.Invoke(queuedMap) ?? queuedMap;
            queuedMap = queuedMap with { Gameplay = team.Policy.Gameplay ?? new() };
            if (queuedMap.Tier > state.MaximumUnlockedMapTier && !P5ExpeditionDirector.IsBoss(queuedMap) &&
                !P5ExpeditionDirector.IsPractice(queuedMap) && !GameForWork.Core.P10.P10EndgameState.IsCitadel(queuedMap))
            {
                team.Stop("tier_locked");
                continue;
            }
            team.Queue.TryReplaceAt(0, queuedMap);
            if (enteredAsFormalMap && team.WaitForRouteDecision(queuedMap, offline)) continue;
            MapRoute route = queuedMap.SelectedRoute ?? team.Policy.SelectUnattendedRoute(
                queuedMap, Math.Clamp(team.Progression.Level, 1, 100), worldSeed);
            if (queuedMap.Tier > team.Policy.MaximumMapTier || !(team.Policy.MapFilter ?? GameForWork.Core.P26.P26MapFilter.All).Matches(queuedMap))
            {
                team.Stop("map_policy_limit");
                continue;
            }
            ulong runSeed = DeriveExpeditionSeed(worldSeed, team, queuedMap, route);
            int buildHash = team.Build.GetHashCode();
            P1MapRunResult plannedRun;
            if (asyncPreparation)
            {
                if (!_runPreparations.TryGetValue(team.Kind, out RunPreparation? preparation) ||
                    preparation.MapId != queuedMap.InstanceId || preparation.Route != route ||
                    preparation.Seed != runSeed || preparation.BuildHash != buildHash ||
                    preparation.Gameplay != queuedMap.Gameplay || preparation.AtlasKey != string.Join('|', queuedMap.AtlasSnapshot ?? []))
                {
                    P1TeamBuild preparedBuild = team.Build;
                    preparation = new RunPreparation(queuedMap.InstanceId, route, runSeed, buildHash,
                        Task.Run(() => new P1MapRunner(attemptResolver).Run(queuedMap, route, preparedBuild, runSeed)), queuedMap.Gameplay,
                        string.Join('|', queuedMap.AtlasSnapshot ?? []));
                    _runPreparations[team.Kind] = preparation;
                }
                if (!preparation.Task.IsCompleted) continue;
                plannedRun = preparation.Task.GetAwaiter().GetResult();
            }
            else
            {
                _runPreparations.Remove(team.Kind);
                plannedRun = new P1MapRunner(attemptResolver).Run(queuedMap, route, team.Build, runSeed);
            }
            if (!team.Queue.TryDequeue(out P1MapItem? map) || map is null)
            {
                throw new InvalidOperationException("Map queue count and dequeue result disagree.");
            }
            _runPreparations.Remove(team.Kind);

            // Third-party/test resolvers created before P3 do not provide a scene timeline. Keep their
            // historical timing contract while production runs always use the authoritative timeline.
            long duration = plannedRun.DurationMilliseconds > 0
                ? plannedRun.DurationMilliseconds
                : route == MapRoute.Abyss ? 120_000 : 90_000;
            team.StartMap(map, route, duration, plannedRun);
            MapStarted?.Invoke(team, map, route);
            active[team.Kind] = new ActiveExpedition(team, map, route, checked(now + duration));
        }
    }

    private void ResolveExpedition(P1WorldState state, ActiveExpedition expedition, ulong seed, bool offline)
    {
        ExpeditionPolicy runPolicy = expedition.Team.ActivePolicySnapshot ?? expedition.Team.Policy;
        P1MapRunResult run = expedition.Team.ActiveRun ?? new P1MapRunner(attemptResolver).Run(
            expedition.Map, expedition.Route, expedition.Team.Build, seed);
        foreach ((MapAttemptResult attempt, int index) in run.Attempts.Select((attempt, index) => (attempt, index)))
        {
            if (attempt.Timeline is not null)
            {
                string encounter = P5ExpeditionDirector.IsBoss(expedition.Map)
                    ? "深渊监守者"
                    : $"T{expedition.Map.Tier} {expedition.Route}";
                state.Expedition.AddCombatReport(P6CombatReportBuilder.Build(attempt.Timeline,
                    $"{expedition.Team.Kind} · {encounter} · 尝试 {index + 1}", offline));
            }
        }
        bool practice = P5ExpeditionDirector.IsPractice(expedition.Map) || GameForWork.Core.P10.P10EndgameState.IsCitadelPractice(expedition.Map);
        (int defeated, int total) = P1MapRewardGenerator.CombatProgress(run);
        P1MapRewards? partial = !run.Succeeded && !practice && defeated > 0
            ? P1MapRewardGenerator.GeneratePartial(expedition.Map, expedition.Route,
                seed ^ 0x9e3779b97f4a7c15UL, run, state.MaximumUnlockedMapTier)
            : null;
        expedition.Team.RecordRun(run, countProgression: !practice, defeatedExperience: partial?.Experience ?? 0);
        state.Expedition.RecordResolved(expedition.Map, run.Succeeded, seed, expedition.Route);
        if (!run.Succeeded)
        {
            if (partial is not null)
            {
                state.Economy.AddRewards(partial.Stackables);
                state.AddMaps(partial.Maps);
                LootProcessingResult defeatedLoot = LootProcessor.Process(partial.Equipment, state.Storage,
                    state.Filter, runPolicy.StorageFullBehavior);
                expedition.Team.Backpack.Replace(defeatedLoot.NotableItems);
                state.Economy.AddDispositionProceeds(defeatedLoot.GoldGained, defeatedLoot.IronScrapsGained);
                if (defeatedLoot.ExpeditionMustStop) expedition.Team.Stop("storage_full");
            }
            if (!practice) MapResolved?.Invoke(expedition.Team, run, seed, partial?.Stackables.SkillStones ?? 0, runPolicy);
            return;
        }

        if (practice)
        {
            return;
        }

        P1MapRewards rewards = P1MapRewardGenerator.Generate(expedition.Map, expedition.Route,
            seed ^ 0x9e3779b97f4a7c15UL, state.MaximumUnlockedMapTier, run);
        state.Economy.AddRewards(rewards.Stackables);
        var maps = rewards.Maps.ToList();
        bool sameTierGuarantee = P26AtlasEffects.Has(expedition.Map.AtlasSnapshot, "p26.atlas.supply.12");
        int pityThreshold = P26AtlasEffects.Has(expedition.Map.AtlasSnapshot, "p26.atlas.supply.08") ? 2 : 3;
        bool needsSameTier = sameTierGuarantee && !maps.Any(map => map.Tier == expedition.Map.Tier);
        bool pityMap = expedition.Team.RecordCompletedMapDrop(maps.Count + (needsSameTier ? 1 : 0), pityThreshold);
        if (needsSameTier || pityMap)
            maps.Add(new P1MapItem($"p29-map-pity-{expedition.Team.Kind}-{expedition.Team.MapsCompleted:000000}", expedition.Map.Tier)
                .EnsureFormal(seed ^ 0x7032396d6170UL));
        state.AddMaps(maps);
        var equipment = rewards.Equipment.ToList();
        string legendaryPool = P20LegendaryDrops.PoolForMap(expedition.Map, expedition.Route);
        if (expedition.Team.ShouldGuaranteeLegendary(legendaryPool, rewards.LegendaryDropped))
        {
            GameForWork.Core.P14.P14UniqueDefinition unique = P20LegendaryDrops.Pick(legendaryPool, new GameForWork.Core.Simulation.Pcg32(seed ^ 0x703239756e69UL));
            equipment.Add(GameForWork.Core.P14.P14UniqueItems.Create(unique.StableId, Math.Min(120, expedition.Map.MonsterLevel + 2),
                $"p29-legendary-pity-{expedition.Team.Kind}-{expedition.Team.MapsCompleted:000000}") with
                { DropSource = $"p29.source.legendary.{legendaryPool}" });
        }
        LootProcessingResult processed = LootProcessor.Process(
            equipment,
            state.Storage,
            state.Filter,
            runPolicy.StorageFullBehavior);
        expedition.Team.Backpack.Replace(processed.NotableItems);
        state.Economy.AddDispositionProceeds(processed.GoldGained, processed.IronScrapsGained);
        if (processed.ExpeditionMustStop)
        {
            expedition.Team.Stop("storage_full");
        }
        MapResolved?.Invoke(expedition.Team, run, seed, rewards.Stackables.SkillStones, runPolicy);
    }

    private static string Hash(P1WorldState state, long elapsed, ulong seed)
    {
        var builder = new StringBuilder();
        builder.Append(seed).Append('|').Append(elapsed).Append('|')
            .Append(state.Economy.Gold).Append('|')
            .Append(state.Economy.IronScraps).Append('|')
            .Append(state.Economy.MemoryAshes).Append('|')
            .Append(state.Economy.WardenMarks).Append('|')
            .Append(state.Storage.Count).Append('|').Append(state.MapInventory.Count).Append('|')
            .Append(state.Expedition.AbyssWardenFragments).Append('|')
            .Append(state.Expedition.AbyssWardenTickets).Append('|')
            .Append(state.Expedition.MapsTowardNextFragment);
        foreach (P1TeamExpeditionState team in state.Teams)
        {
            builder.Append('|').Append(team.Kind).Append(':').Append(team.MapsCompleted).Append(':')
                .Append(team.MapsFailed).Append(':').Append(team.Queue.Count).Append(':')
                .Append(team.Progression.Level).Append(':').Append(team.IsStopped).Append(':')
                .Append(team.ActiveMap?.InstanceId).Append(':').Append(team.RemainingMapTimeMilliseconds).Append(':')
                .Append(team.Backpack.Count);
            foreach (ItemInstance item in team.Backpack.Items)
            {
                builder.Append(':').Append(item.InstanceId);
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static ulong DeriveExpeditionSeed(ulong seed, ActiveExpedition expedition)
        => DeriveExpeditionSeed(seed, expedition.Team, expedition.Map, expedition.Route);

    private static ulong DeriveExpeditionSeed(
        ulong seed,
        P1TeamExpeditionState team,
        P1MapItem map,
        MapRoute route)
    {
        string identity = $"{seed}|{team.Kind}|{map.InstanceId}|{map.Tier}|{route}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return BitConverter.ToUInt64(hash, 0);
    }

    private sealed record ActiveExpedition(
        P1TeamExpeditionState Team,
        P1MapItem Map,
        MapRoute Route,
        long CompletionTimeMilliseconds);
}

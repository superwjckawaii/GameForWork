using GameForWork.Core.Campaign.World;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Expeditions;

public enum ExpeditionTarget
{
    SafeMaps,
    AbyssMaps,
    LifeGardenMaps,
    HighestTierMaps,
    AbyssWarden,
    AbyssWardenPractice,
    WarfrontMaps,
    AshenCitadel,
    AshenCitadelPractice,
    FinalBreakthrough,
}

public sealed record BossScheduleResult(MapItem? Map, string FailureReason = "boss_unavailable");

public enum DispatchMode
{
    Once,
    Repeat,
    HighestAvailable,
}

public sealed record TeamDispatchSnapshot(
    ExpeditionTeamKind Team,
    ExpeditionTarget Target,
    DispatchMode Mode,
    bool Enabled,
    int RemainingRuns,
    string Status);

public sealed record ExpeditionSnapshot(
    int AbyssWardenFragments,
    int AbyssWardenTickets,
    int MapsTowardNextFragment,
    int BossSequence,
    IReadOnlyList<TeamDispatchSnapshot> Teams,
    IReadOnlyList<CombatReport>? Reports = null);

public sealed class ExpeditionDirector
{
    public const int MapsPerFragment = 3;
    public const int FragmentsPerTicket = 4;
    private const string BossPrefix = "expeditions-abyss-warden-";
    private const string PracticePrefix = "expeditions-practice-abyss-warden-";
    private readonly Dictionary<ExpeditionTeamKind, TeamDispatchSnapshot> _dispatches = [];
    private readonly List<CombatReport> _reports = [];

    public int AbyssWardenFragments { get; private set; }
    public int AbyssWardenTickets { get; private set; }
    public int MapsTowardNextFragment { get; private set; }
    public int BossSequence { get; private set; }
    public IReadOnlyDictionary<ExpeditionTeamKind, TeamDispatchSnapshot> Dispatches => _dispatches;
    public IReadOnlyList<CombatReport> Reports => _reports;
    public Func<ExpeditionTarget, BossScheduleResult>? BossMapFactory { get; set; }

    public TeamDispatchSnapshot? Get(ExpeditionTeamKind team) => _dispatches.GetValueOrDefault(team);

    public void Assign(ExpeditionTeamKind team, ExpeditionTarget target, DispatchMode mode, int requestedRuns = 1)
    {
        if (target == ExpeditionTarget.FinalBreakthrough)
        {
            mode = DispatchMode.Once;
            requestedRuns = 1;
        }

        int remaining = mode == DispatchMode.Once ? Math.Clamp(requestedRuns, 1, 999) : int.MaxValue;
        _dispatches[team] = new TeamDispatchSnapshot(team, target, mode, true, remaining, "waiting");
    }

    public void Cancel(ExpeditionTeamKind team, string status = "cancelled")
    {
        if (_dispatches.TryGetValue(team, out TeamDispatchSnapshot? current))
        {
            _dispatches[team] = current with { Enabled = false, Status = status };
        }
    }

    public bool PrepareNext(WorldState world, TeamExpeditionState team)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(team);
        if (team.IsStopped || team.ActiveMap is not null || team.Queue.Count > 0 ||
            !_dispatches.TryGetValue(team.Kind, out TeamDispatchSnapshot? dispatch) || !dispatch.Enabled)
        {
            return false;
        }

        MapItem? map;
        MapRoute route;
        string status;
        if (IsBossTarget(dispatch.Target))
        {
            bool warden = dispatch.Target is ExpeditionTarget.AbyssWarden or ExpeditionTarget.AbyssWardenPractice;
            bool practice = dispatch.Target is ExpeditionTarget.AbyssWardenPractice or ExpeditionTarget.AshenCitadelPractice;
            if (warden && !practice && AbyssWardenTickets <= 0)
            {
                Stop(team, dispatch, "boss_ticket_missing");
                return false;
            }

            if (warden && !practice)
            {
                AbyssWardenTickets--;
            }

            BossSequence++;
            if (warden)
            {
                map = new MapItem(
                    $"{(practice ? PracticePrefix : BossPrefix)}{BossSequence:000000}",
                    10).EnsureFormal((ulong)BossSequence);
                if (!map.EffectiveRouteCandidates.Contains(MapRoute.Abyss))
                    map = map with { RouteCandidates = map.EffectiveRouteCandidates.Append(MapRoute.Abyss).Distinct().Take(3).ToArray() };
                map = map with { SelectedRoute = MapRoute.Abyss };
            }
            else
            {
                BossScheduleResult scheduled = BossMapFactory?.Invoke(dispatch.Target) ?? new(null);
                if (scheduled.Map is null)
                {
                    Stop(team, dispatch, scheduled.FailureReason);
                    return false;
                }
                map = scheduled.Map;
            }
            route = MapRoute.Abyss;
            status = practice ? "practice_scheduled" : "boss_scheduled";
        }
        else
        {
            HashSet<string> legacyMapIds = world.MapInventory
                .Where(item => string.IsNullOrWhiteSpace(item.AreaId) || item.EffectiveRouteCandidates.Count == 0)
                .Select(item => item.InstanceId).ToHashSet(StringComparer.Ordinal);
            for (int index = 0; index < world.MapInventory.Count; index++)
                world.MapInventory[index] = world.MapInventory[index].EnsureFormal((ulong)(BossSequence + index + 1));
            int inventoryIndex = SelectMapIndex(world.MapInventory, dispatch.Target, dispatch.Mode,
                Math.Min(world.MaximumUnlockedMapTier, team.Policy.MaximumMapTier), team.Policy);
            if (inventoryIndex < 0)
            {
                if (team.Policy.NoMatchBehavior == GameForWork.Core.Atlas.NoMatchBehavior.Stop)
                    Stop(team, dispatch, "maps_exhausted");
                else
                    _dispatches[team.Kind] = dispatch with { Status = "waiting_for_matching_map" };
                return false;
            }

            map = world.MapInventory[inventoryIndex].EnsureFormal((ulong)(BossSequence + inventoryIndex + 1));
            world.MapInventory.RemoveAt(inventoryIndex);
            if (dispatch.Target == ExpeditionTarget.HighestTierMaps && legacyMapIds.Contains(map.InstanceId) &&
                !map.EffectiveRouteCandidates.Contains(MapRoute.Abyss))
                map = map with { RouteCandidates = map.EffectiveRouteCandidates.Append(MapRoute.Abyss).Distinct().TakeLast(3).ToArray() };
            MapRoute? requestedRoute = dispatch.Target switch
            {
                ExpeditionTarget.SafeMaps => MapRoute.Safe,
                ExpeditionTarget.LifeGardenMaps => MapRoute.LifeGarden,
                ExpeditionTarget.AbyssMaps => MapRoute.Abyss,
                ExpeditionTarget.WarfrontMaps => MapRoute.Warfront,
                _ => null,
            };
            MapRoute? selectedRoute = map.SelectedRoute is { } selected &&
                !(team.Policy.BlockedRoutes ?? []).Contains(selected) ? selected : null;
            route = requestedRoute ?? selectedRoute ??
                (dispatch.Target == ExpeditionTarget.HighestTierMaps && map.EffectiveRouteCandidates.Contains(MapRoute.Abyss)
                    ? MapRoute.Abyss
                    : team.Policy.SelectUnattendedRoute(map, team.Progression.Level, (ulong)BossSequence));
            map = map with { SelectedRoute = route };
            status = "map_scheduled";
        }

        if (IsBoss(map) || IsPractice(map))
            map = EnsureFormalDispatchMap(map, (ulong)BossSequence);

        if (!team.Queue.TryEnqueue(map))
        {
            throw new InvalidOperationException("Expeditions dispatch could not enqueue an empty team queue.");
        }

        team.ApplyPolicy(team.Policy with
        {
            RouteSelection = RouteSelectionMode.Automatic,
            PreferredRoute = route,
            FailureBehavior = QueueFailureBehavior.Continue,
            StorageFullBehavior = StorageFullBehavior.AcceptStackablesOnly,
            StopAfterConsecutiveFailures = 3,
        });
        team.Resume();
        int remainingRuns = dispatch.RemainingRuns == int.MaxValue ? int.MaxValue : dispatch.RemainingRuns - 1;
        _dispatches[team.Kind] = dispatch with
        {
            Enabled = remainingRuns != 0,
            RemainingRuns = remainingRuns,
            Status = status,
        };
        return true;
    }

    public void RecordResolved(MapItem map, bool succeeded, ulong seed = 0, MapRoute route = MapRoute.Safe)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!succeeded || IsPractice(map) || IsBoss(map))
        {
            return;
        }

        MapsTowardNextFragment += 1;
        while (MapsTowardNextFragment >= MapsPerFragment)
        {
            MapsTowardNextFragment -= MapsPerFragment;
            AbyssWardenFragments++;
        }
        while (AbyssWardenFragments >= FragmentsPerTicket)
        {
            AbyssWardenFragments -= FragmentsPerTicket;
            AbyssWardenTickets++;
        }
    }

    public void AddCombatReport(CombatReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _reports.Add(report);
        if (_reports.Count > 50) _reports.RemoveRange(0, _reports.Count - 50);
    }

    public ExpeditionSnapshot Capture() => new(
        AbyssWardenFragments,
        AbyssWardenTickets,
        MapsTowardNextFragment,
        BossSequence,
        _dispatches.Values.OrderBy(item => item.Team).ToArray(),
        _reports.ToArray());

    public static ExpeditionDirector Restore(ExpeditionSnapshot? snapshot)
    {
        var result = new ExpeditionDirector();
        if (snapshot is null)
        {
            return result;
        }

        if (snapshot.AbyssWardenFragments is < 0 or >= FragmentsPerTicket ||
            snapshot.AbyssWardenTickets < 0 || snapshot.MapsTowardNextFragment is < 0 or >= MapsPerFragment ||
            snapshot.BossSequence < 0 || snapshot.Teams is null)
        {
            throw new InvalidDataException("Expeditions expedition snapshot is invalid.");
        }

        result.AbyssWardenFragments = snapshot.AbyssWardenFragments;
        result.AbyssWardenTickets = snapshot.AbyssWardenTickets;
        result.MapsTowardNextFragment = snapshot.MapsTowardNextFragment;
        result.BossSequence = snapshot.BossSequence;
        foreach (TeamDispatchSnapshot team in snapshot.Teams)
        {
            if (!Enum.IsDefined(team.Team) || !Enum.IsDefined(team.Target) || !Enum.IsDefined(team.Mode) ||
                team.RemainingRuns < 0 || result._dispatches.ContainsKey(team.Team))
            {
                throw new InvalidDataException("Expeditions team dispatch snapshot is invalid.");
            }

            result._dispatches.Add(team.Team, team);
        }

        if ((snapshot.Reports?.Count ?? 0) > 50)
        {
            throw new InvalidDataException("Skills combat report snapshot exceeds capacity.");
        }
        result._reports.AddRange(snapshot.Reports ?? []);

        return result;
    }

    public static bool IsBoss(MapItem map) => Persistence.SaveIdentifierMigration.Normalize(map.InstanceId).StartsWith(BossPrefix, StringComparison.Ordinal) ||
        GameForWork.Core.Endgame.EndgameState.IsCitadel(map) || GameForWork.Core.Endgame.EndgameState.IsBreakthroughTrial(map);
    public static bool IsAbyssWarden(MapItem map) => Persistence.SaveIdentifierMigration.Normalize(map.InstanceId).StartsWith(BossPrefix, StringComparison.Ordinal);
    public static bool IsPractice(MapItem map) => Persistence.SaveIdentifierMigration.Normalize(map.InstanceId).StartsWith(PracticePrefix, StringComparison.Ordinal) ||
        GameForWork.Core.Endgame.EndgameState.IsCitadelPractice(map);
    public static MapItem EnsureFormalDispatchMap(MapItem map, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!IsBoss(map) && !IsPractice(map)) return map.EnsureFormal(seed);
        MapRoute desiredRoute = GameForWork.Core.Endgame.EndgameState.IsBreakthroughTrial(map)
            ? MapRoute.Safe
            : MapRoute.Abyss;
        MapItem formal = (map with { SelectedRoute = null }).EnsureFormal(seed);
        IReadOnlyList<MapRoute> routes = formal.EffectiveRouteCandidates.Contains(desiredRoute)
            ? formal.EffectiveRouteCandidates
            : formal.EffectiveRouteCandidates.Append(desiredRoute).Distinct().TakeLast(3).ToArray();
        return (formal with
        {
            RouteCandidates = routes,
            SelectedRoute = desiredRoute,
            Altar = map.Altar,
        }).Validate();
    }
    public static bool IsBossTarget(ExpeditionTarget target) => target is
        ExpeditionTarget.AbyssWarden or ExpeditionTarget.AbyssWardenPractice or
        ExpeditionTarget.AshenCitadel or ExpeditionTarget.AshenCitadelPractice or
        ExpeditionTarget.FinalBreakthrough;

    private static int SelectMapIndex(
        IReadOnlyList<MapItem> maps,
        ExpeditionTarget target,
        DispatchMode mode,
        int maximumUnlockedTier,
        ExpeditionPolicy policy)
    {
        int[] eligible = maps.Select((map, index) => (map, index))
            .Where(pair => !pair.map.IsProtected && pair.map.Tier <= maximumUnlockedTier && MatchesRoute(pair.map, target, policy) &&
                MatchesAltar(pair.map, policy.AltarPreference) &&
                (policy.MapFilter ?? GameForWork.Core.Atlas.MapFilter.All).Matches(pair.map))
            .Select(pair => pair.index).ToArray();
        if (eligible.Length == 0)
        {
            return -1;
        }

        IEnumerable<(MapItem map, int index)> candidates = eligible.Select(index => (map: maps[index], index));
        IEnumerable<(MapItem map, int index)> ranked = policy.MapOrder switch
        {
            GameForWork.Core.Atlas.MapOrder.TierAscending => candidates.OrderBy(pair => pair.map.Tier).ThenBy(pair => pair.map.AcquiredSequence),
            GameForWork.Core.Atlas.MapOrder.OldestFirst => candidates.OrderBy(pair => pair.map.AcquiredSequence).ThenByDescending(pair => pair.map.Tier),
            _ => candidates.OrderByDescending(pair => pair.map.Tier)
                .ThenByDescending(pair => pair.map.ItemQuantityBonusBasisPoints)
                .ThenByDescending(pair => pair.map.MonsterQuantityBasisPoints)
                .ThenBy(pair => pair.map.AcquiredSequence)
                .ThenBy(pair => pair.map.InstanceId, StringComparer.Ordinal),
        };
        return ranked.First().index;
    }

    private static bool MatchesRoute(MapItem map, ExpeditionTarget target, ExpeditionPolicy policy)
    {
        MapRoute? requested = target switch
        {
            ExpeditionTarget.SafeMaps => MapRoute.Safe,
            ExpeditionTarget.AbyssMaps => MapRoute.Abyss,
            ExpeditionTarget.LifeGardenMaps => MapRoute.LifeGarden,
            ExpeditionTarget.WarfrontMaps => MapRoute.Warfront,
            _ => null,
        };
        return requested is not null
            ? map.EffectiveRouteCandidates.Contains(requested.Value)
            : map.EffectiveRouteCandidates.Any(route => !(policy.BlockedRoutes ?? []).Contains(route));
    }

    private static bool MatchesAltar(MapItem map, MapAltarPreference preference) => preference switch
    {
        MapAltarPreference.Avoid => map.Altar == GameForWork.Core.Maps.MapAltar.None,
        MapAltarPreference.RedOath => map.Altar != GameForWork.Core.Maps.MapAltar.BlueOath,
        MapAltarPreference.BlueOath => map.Altar != GameForWork.Core.Maps.MapAltar.RedOath,
        _ => true,
    };

    private void Stop(TeamExpeditionState team, TeamDispatchSnapshot dispatch, string reason)
    {
        team.Stop(reason);
        _dispatches[team.Kind] = dispatch with { Enabled = false, Status = reason };
    }
}

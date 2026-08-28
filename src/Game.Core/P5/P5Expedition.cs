using GameForWork.Core.P1.World;
using GameForWork.Core.P6;

namespace GameForWork.Core.P5;

public enum P5ExpeditionTarget
{
    SafeMaps,
    AbyssMaps,
    LifeGardenMaps,
    HighestTierMaps,
    AbyssWarden,
    AbyssWardenPractice,
}

public enum P5DispatchMode
{
    Once,
    Repeat,
    HighestAvailable,
}

public sealed record P5TeamDispatchSnapshot(
    ExpeditionTeamKind Team,
    P5ExpeditionTarget Target,
    P5DispatchMode Mode,
    bool Enabled,
    int RemainingRuns,
    string Status);

public sealed record P5ExpeditionSnapshot(
    int AbyssWardenFragments,
    int AbyssWardenTickets,
    int MapsTowardNextFragment,
    int BossSequence,
    IReadOnlyList<P5TeamDispatchSnapshot> Teams,
    IReadOnlyList<P6CombatReport>? Reports = null);

public sealed class P5ExpeditionDirector
{
    public const int MapsPerFragment = 3;
    public const int FragmentsPerTicket = 4;
    private const string BossPrefix = "p5-abyss-warden-";
    private const string PracticePrefix = "p5-practice-abyss-warden-";
    private readonly Dictionary<ExpeditionTeamKind, P5TeamDispatchSnapshot> _dispatches = [];
    private readonly List<P6CombatReport> _reports = [];

    public int AbyssWardenFragments { get; private set; }
    public int AbyssWardenTickets { get; private set; }
    public int MapsTowardNextFragment { get; private set; }
    public int BossSequence { get; private set; }
    public IReadOnlyDictionary<ExpeditionTeamKind, P5TeamDispatchSnapshot> Dispatches => _dispatches;
    public IReadOnlyList<P6CombatReport> Reports => _reports;

    public P5TeamDispatchSnapshot? Get(ExpeditionTeamKind team) => _dispatches.GetValueOrDefault(team);

    public void Assign(ExpeditionTeamKind team, P5ExpeditionTarget target, P5DispatchMode mode)
    {
        if (target is P5ExpeditionTarget.AbyssWarden or P5ExpeditionTarget.AbyssWardenPractice)
        {
            mode = P5DispatchMode.Once;
        }

        int remaining = mode == P5DispatchMode.Once ? 1 : int.MaxValue;
        _dispatches[team] = new P5TeamDispatchSnapshot(team, target, mode, true, remaining, "waiting");
    }

    public void Cancel(ExpeditionTeamKind team, string status = "cancelled")
    {
        if (_dispatches.TryGetValue(team, out P5TeamDispatchSnapshot? current))
        {
            _dispatches[team] = current with { Enabled = false, Status = status };
        }
    }

    public bool PrepareNext(P1WorldState world, P1TeamExpeditionState team)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(team);
        if (team.IsStopped || team.ActiveMap is not null || team.Queue.Count > 0 ||
            !_dispatches.TryGetValue(team.Kind, out P5TeamDispatchSnapshot? dispatch) || !dispatch.Enabled)
        {
            return false;
        }

        P1MapItem? map;
        MapRoute route;
        string status;
        if (dispatch.Target is P5ExpeditionTarget.AbyssWarden or P5ExpeditionTarget.AbyssWardenPractice)
        {
            bool practice = dispatch.Target == P5ExpeditionTarget.AbyssWardenPractice;
            if (!practice && AbyssWardenTickets <= 0)
            {
                Stop(team, dispatch, "boss_ticket_missing");
                return false;
            }

            if (!practice)
            {
                AbyssWardenTickets--;
            }

            BossSequence++;
            map = new P1MapItem(
                $"{(practice ? PracticePrefix : BossPrefix)}{BossSequence:000000}",
                10).EnsureFormal((ulong)BossSequence);
            if (!map.EffectiveRouteCandidates.Contains(MapRoute.Abyss))
                map = map with { RouteCandidates = map.EffectiveRouteCandidates.Append(MapRoute.Abyss).Distinct().Take(3).ToArray() };
            map = map with { SelectedRoute = MapRoute.Abyss };
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
                Stop(team, dispatch, "maps_exhausted");
                return false;
            }

            map = world.MapInventory[inventoryIndex].EnsureFormal((ulong)(BossSequence + inventoryIndex + 1));
            world.MapInventory.RemoveAt(inventoryIndex);
            if (dispatch.Target == P5ExpeditionTarget.HighestTierMaps && legacyMapIds.Contains(map.InstanceId) &&
                !map.EffectiveRouteCandidates.Contains(MapRoute.Abyss))
                map = map with { RouteCandidates = map.EffectiveRouteCandidates.Append(MapRoute.Abyss).Distinct().TakeLast(3).ToArray() };
            MapRoute? requestedRoute = dispatch.Target switch
            {
                P5ExpeditionTarget.SafeMaps => MapRoute.Safe,
                P5ExpeditionTarget.LifeGardenMaps => MapRoute.LifeGarden,
                P5ExpeditionTarget.AbyssMaps => MapRoute.Abyss,
                _ => null,
            };
            route = requestedRoute ?? map.SelectedRoute ??
                (dispatch.Target == P5ExpeditionTarget.HighestTierMaps && map.EffectiveRouteCandidates.Contains(MapRoute.Abyss)
                    ? MapRoute.Abyss
                    : team.Policy.SelectUnattendedRoute(map, team.Progression.Level, (ulong)BossSequence));
            if (requestedRoute is not null) map = map with { SelectedRoute = route };
            status = "map_scheduled";
        }

        if (!team.Queue.TryEnqueue(map))
        {
            throw new InvalidOperationException("P5 dispatch could not enqueue an empty team queue.");
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

    public void RecordResolved(P1MapItem map, bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!succeeded || IsPractice(map) || IsBoss(map))
        {
            return;
        }

        MapsTowardNextFragment++;
        if (MapsTowardNextFragment < MapsPerFragment)
        {
            return;
        }

        MapsTowardNextFragment -= MapsPerFragment;
        AbyssWardenFragments++;
        while (AbyssWardenFragments >= FragmentsPerTicket)
        {
            AbyssWardenFragments -= FragmentsPerTicket;
            AbyssWardenTickets++;
        }
    }

    public void AddCombatReport(P6CombatReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _reports.Add(report);
        if (_reports.Count > 50) _reports.RemoveRange(0, _reports.Count - 50);
    }

    public P5ExpeditionSnapshot Capture() => new(
        AbyssWardenFragments,
        AbyssWardenTickets,
        MapsTowardNextFragment,
        BossSequence,
        _dispatches.Values.OrderBy(item => item.Team).ToArray(),
        _reports.ToArray());

    public static P5ExpeditionDirector Restore(P5ExpeditionSnapshot? snapshot)
    {
        var result = new P5ExpeditionDirector();
        if (snapshot is null)
        {
            return result;
        }

        if (snapshot.AbyssWardenFragments is < 0 or >= FragmentsPerTicket ||
            snapshot.AbyssWardenTickets < 0 || snapshot.MapsTowardNextFragment is < 0 or >= MapsPerFragment ||
            snapshot.BossSequence < 0 || snapshot.Teams is null)
        {
            throw new InvalidDataException("P5 expedition snapshot is invalid.");
        }

        result.AbyssWardenFragments = snapshot.AbyssWardenFragments;
        result.AbyssWardenTickets = snapshot.AbyssWardenTickets;
        result.MapsTowardNextFragment = snapshot.MapsTowardNextFragment;
        result.BossSequence = snapshot.BossSequence;
        foreach (P5TeamDispatchSnapshot team in snapshot.Teams)
        {
            if (!Enum.IsDefined(team.Team) || !Enum.IsDefined(team.Target) || !Enum.IsDefined(team.Mode) ||
                team.RemainingRuns < 0 || result._dispatches.ContainsKey(team.Team))
            {
                throw new InvalidDataException("P5 team dispatch snapshot is invalid.");
            }

            result._dispatches.Add(team.Team, team);
        }

        if ((snapshot.Reports?.Count ?? 0) > 50)
        {
            throw new InvalidDataException("P6 combat report snapshot exceeds capacity.");
        }
        result._reports.AddRange(snapshot.Reports ?? []);

        return result;
    }

    public static bool IsBoss(P1MapItem map) => map.InstanceId.StartsWith(BossPrefix, StringComparison.Ordinal);
    public static bool IsPractice(P1MapItem map) => map.InstanceId.StartsWith(PracticePrefix, StringComparison.Ordinal);

    private static int SelectMapIndex(
        IReadOnlyList<P1MapItem> maps,
        P5ExpeditionTarget target,
        P5DispatchMode mode,
        int maximumUnlockedTier,
        ExpeditionPolicy policy)
    {
        int[] eligible = maps.Select((map, index) => (map, index))
            .Where(pair => pair.map.AreaLevel <= maximumUnlockedTier && MatchesRoute(pair.map, target, policy) &&
                MatchesRisk(pair.map, target, policy))
            .Select(pair => pair.index).ToArray();
        if (eligible.Length == 0)
        {
            return -1;
        }

        IEnumerable<(P1MapItem map, int index)> ranked = eligible.Select(index => (map: maps[index], index))
            .OrderByDescending(pair => AltarScore(pair.map, policy.AltarPreference));
        return target == P5ExpeditionTarget.HighestTierMaps || mode == P5DispatchMode.HighestAvailable
            ? eligible.Select(index => (map: maps[index], index))
                .OrderByDescending(pair => pair.map.AreaLevel)
                .ThenByDescending(pair => AltarScore(pair.map, policy.AltarPreference))
                .ThenBy(pair => pair.map.InstanceId, StringComparer.Ordinal)
                .First().index
            : ranked.First().index;
    }

    private static bool MatchesRoute(P1MapItem map, P5ExpeditionTarget target, ExpeditionPolicy policy)
    {
        MapRoute? requested = target switch
        {
            P5ExpeditionTarget.SafeMaps => MapRoute.Safe,
            P5ExpeditionTarget.AbyssMaps => MapRoute.Abyss,
            P5ExpeditionTarget.LifeGardenMaps => MapRoute.LifeGarden,
            _ => null,
        };
        return requested is not null
            ? map.EffectiveRouteCandidates.Contains(requested.Value)
            : map.EffectiveRouteCandidates.Any(route => !(policy.BlockedRoutes ?? []).Contains(route));
    }

    private static bool MatchesRisk(P1MapItem map, P5ExpeditionTarget target, ExpeditionPolicy policy)
    {
        MapRoute? requested = target switch
        {
            P5ExpeditionTarget.SafeMaps => MapRoute.Safe,
            P5ExpeditionTarget.AbyssMaps => MapRoute.Abyss,
            P5ExpeditionTarget.LifeGardenMaps => MapRoute.LifeGarden,
            _ => null,
        };
        return requested is not null
            ? map.DangerFor(requested.Value) <= policy.MaximumMapDanger
            : map.EffectiveRouteCandidates.Any(route => !(policy.BlockedRoutes ?? []).Contains(route) &&
                map.DangerFor(route) <= policy.MaximumMapDanger);
    }

    private static int AltarScore(P1MapItem map, MapAltarPreference preference) => preference switch
    {
        MapAltarPreference.Avoid => map.Altar == GameForWork.Core.P12.P12MapAltar.None ? 2 : 0,
        MapAltarPreference.RedOath => map.Altar == GameForWork.Core.P12.P12MapAltar.RedOath ? 2 : 0,
        MapAltarPreference.BlueOath => map.Altar == GameForWork.Core.P12.P12MapAltar.BlueOath ? 2 : 0,
        _ => 1,
    };

    private void Stop(P1TeamExpeditionState team, P5TeamDispatchSnapshot dispatch, string reason)
    {
        team.Stop(reason);
        _dispatches[team.Kind] = dispatch with { Enabled = false, Status = reason };
    }
}

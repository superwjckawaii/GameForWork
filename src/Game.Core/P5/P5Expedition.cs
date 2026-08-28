using GameForWork.Core.P1.World;
using GameForWork.Core.P6;

namespace GameForWork.Core.P5;

public enum P5ExpeditionTarget
{
    SafeMaps,
    AbyssMaps,
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
                P1MapItem.MaximumAreaLevel);
            route = MapRoute.Abyss;
            status = practice ? "practice_scheduled" : "boss_scheduled";
        }
        else
        {
            int inventoryIndex = SelectMapIndex(world.MapInventory, dispatch.Target, dispatch.Mode);
            if (inventoryIndex < 0)
            {
                Stop(team, dispatch, "maps_exhausted");
                return false;
            }

            map = world.MapInventory[inventoryIndex];
            world.MapInventory.RemoveAt(inventoryIndex);
            route = dispatch.Target == P5ExpeditionTarget.SafeMaps ? MapRoute.Safe : MapRoute.Abyss;
            status = "map_scheduled";
        }

        if (!team.Queue.TryEnqueue(map))
        {
            throw new InvalidOperationException("P5 dispatch could not enqueue an empty team queue.");
        }

        team.ApplyPolicy(new ExpeditionPolicy(
            RouteSelectionMode.Automatic,
            route,
            QueueFailureBehavior.Continue,
            StorageFullBehavior.AcceptStackablesOnly,
            StopAfterConsecutiveFailures: 3));
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
        P5DispatchMode mode)
    {
        if (maps.Count == 0)
        {
            return -1;
        }

        return target == P5ExpeditionTarget.HighestTierMaps || mode == P5DispatchMode.HighestAvailable
            ? maps.Select((map, index) => (map, index))
                .OrderByDescending(pair => pair.map.AreaLevel)
                .ThenBy(pair => pair.map.InstanceId, StringComparer.Ordinal)
                .First().index
            : 0;
    }

    private void Stop(P1TeamExpeditionState team, P5TeamDispatchSnapshot dispatch, string reason)
    {
        team.Stop(reason);
        _dispatches[team.Kind] = dispatch with { Enabled = false, Status = reason };
    }
}

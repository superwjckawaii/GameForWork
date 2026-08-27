using GameForWork.Core.P1.Combat;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P1.World;

public enum ExpeditionTeamKind
{
    Hero,
    Mercenaries,
}

public enum MapRoute
{
    Safe,
    Abyss,
}

public enum RouteSelectionMode
{
    Manual,
    Automatic,
}

public enum QueueFailureBehavior
{
    Continue,
    Stop,
}

public enum StorageFullBehavior
{
    StopExpedition,
    AcceptStackablesOnly,
}

public sealed record ExpeditionPolicy(
    RouteSelectionMode RouteSelection,
    MapRoute PreferredRoute,
    QueueFailureBehavior FailureBehavior,
    StorageFullBehavior StorageFullBehavior)
{
    public static ExpeditionPolicy Recommended => new(
        RouteSelectionMode.Automatic,
        MapRoute.Safe,
        QueueFailureBehavior.Continue,
        StorageFullBehavior.AcceptStackablesOnly);

    public MapRoute SelectRoute(MapRoute? manualChoice)
    {
        if (RouteSelection == RouteSelectionMode.Manual)
        {
            return manualChoice ?? throw new InvalidOperationException("A manual route choice is required.");
        }

        return PreferredRoute;
    }

    public MapRoute SelectUnattendedRoute() => PreferredRoute;
}

public sealed record P1MapItem(string InstanceId, int AreaLevel)
{
    public const int MinimumAreaLevel = 1;
    public const int MaximumAreaLevel = 10;
    public const int RescueChances = 2;
    public const int TotalAttempts = RescueChances + 1;

    public P1MapItem Validate()
    {
        if (string.IsNullOrWhiteSpace(InstanceId) || AreaLevel is < MinimumAreaLevel or > MaximumAreaLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(AreaLevel), "P1 maps require an ID and area level 1 through 10.");
        }

        return this;
    }
}

public sealed class P1MapQueue
{
    public const int MaximumCount = 10;
    private readonly Queue<P1MapItem> _maps = [];

    public int Count => _maps.Count;
    public IReadOnlyList<P1MapItem> Maps => _maps.ToArray();

    public bool TryEnqueue(P1MapItem map)
    {
        ArgumentNullException.ThrowIfNull(map);
        map.Validate();
        if (_maps.Count >= MaximumCount)
        {
            return false;
        }

        _maps.Enqueue(map);
        return true;
    }

    public bool TryDequeue(out P1MapItem? map) => _maps.TryDequeue(out map);
}

public sealed record P1TeamBuild(
    CharacterSheet Sheet,
    WeaponProfile Weapon,
    SkillConfiguration HeavyStrike,
    int FlatAccuracy = 80,
    int IncreasedDamageBasisPoints = 0,
    int IncreasedCriticalChanceBasisPoints = 0,
    int IncreasedBleedChanceBasisPoints = 0,
    bool UseWarCry = true,
    bool EchoNotableAllocated = false,
    bool DeepWoundAllocated = false,
    bool FasterBleedingAllocated = false,
    string AiSummary = "自动接敌，优先战吼后重击");

public sealed record MapNodeResult(
    int NodeIndex,
    string EnemyStableId,
    bool Elite,
    P1BattleOutcome Outcome,
    int Ticks,
    string FinalHash);

public sealed record MapAttemptResult(
    bool Succeeded,
    IReadOnlyList<MapNodeResult> Nodes,
    string FailureReason);

public interface IP1MapAttemptResolver
{
    MapAttemptResult Resolve(P1MapItem map, MapRoute route, P1TeamBuild team, int attempt, ulong seed);
}

public sealed class P1MapAttemptResolver : IP1MapAttemptResolver
{
    public MapAttemptResult Resolve(P1MapItem map, MapRoute route, P1TeamBuild team, int attempt, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(team);
        map.Validate();
        if (attempt is < 1 or > P1MapItem.TotalAttempts)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }

        var random = new Pcg32(seed);
        var nodes = new List<MapNodeResult>(5);
        for (int nodeIndex = 0; nodeIndex < 5; nodeIndex++)
        {
            bool boss = nodeIndex == 4;
            bool elite = !boss && (nodeIndex == 2 || (route == MapRoute.Abyss && nodeIndex == 3));
            EnemyProfile enemy = boss
                ? P1Enemies.AbyssWarden
                : P1Enemies.NormalEnemies[(int)(random.NextUInt() % (uint)P1Enemies.NormalEnemies.Count)];
            IReadOnlyList<EliteAffix> affixes = elite ? EnemyRules.RollEliteAffixes(random) : [];
            ScaledEnemy scaled = EnemyRules.Scale(enemy, map.AreaLevel, affixes, route == MapRoute.Abyss);
            var encounter = new P1EncounterRequest(
                team.Sheet,
                team.Weapon,
                team.HeavyStrike,
                scaled,
                team.FlatAccuracy,
                team.IncreasedDamageBasisPoints,
                team.IncreasedCriticalChanceBasisPoints,
                team.IncreasedBleedChanceBasisPoints,
                team.UseWarCry,
                team.EchoNotableAllocated,
                team.DeepWoundAllocated,
                team.FasterBleedingAllocated);
            ulong encounterSeed = ((ulong)random.NextUInt() << 32) | random.NextUInt();
            P1EncounterResult result = new P1EncounterRunner().Run(encounter, encounterSeed);
            nodes.Add(new MapNodeResult(
                nodeIndex + 1,
                enemy.StableId,
                elite,
                result.Outcome,
                result.Ticks,
                result.FinalHash));
            if (result.Outcome != P1BattleOutcome.HeroVictory)
            {
                return new MapAttemptResult(false, nodes, $"node_{nodeIndex + 1}_{result.Outcome}");
            }
        }

        return new MapAttemptResult(true, nodes, string.Empty);
    }
}

public sealed record P1MapRunResult(
    P1MapItem Map,
    MapRoute Route,
    bool Succeeded,
    int AttemptsUsed,
    int RescueChancesRemaining,
    IReadOnlyList<MapAttemptResult> Attempts,
    string FailureReason);

public sealed class P1MapRunner(IP1MapAttemptResolver resolver)
{
    public P1MapRunResult Run(P1MapItem map, MapRoute route, P1TeamBuild team, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var attempts = new List<MapAttemptResult>();
        for (int attempt = 1; attempt <= P1MapItem.TotalAttempts; attempt++)
        {
            MapAttemptResult result = resolver.Resolve(map, route, team, attempt, unchecked(seed + (ulong)attempt - 1));
            attempts.Add(result);
            if (result.Succeeded)
            {
                return new P1MapRunResult(
                    map,
                    route,
                    true,
                    attempt,
                    P1MapItem.TotalAttempts - attempt,
                    attempts,
                    string.Empty);
            }
        }

        return new P1MapRunResult(
            map,
            route,
            false,
            P1MapItem.TotalAttempts,
            0,
            attempts,
            attempts[^1].FailureReason);
    }
}

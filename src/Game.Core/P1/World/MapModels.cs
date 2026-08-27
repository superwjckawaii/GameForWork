using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
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
    StorageFullBehavior StorageFullBehavior,
    int MaximumContinuousMaps = 0,
    int ReserveSupplies = 0,
    int StopAfterConsecutiveFailures = 0,
    int MinimumStorageFreeSlots = 0)
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

    public ExpeditionPolicy Validate()
    {
        if (MaximumContinuousMaps is < 0 or > 10_000 || ReserveSupplies is < 0 or > 1_000_000 ||
            StopAfterConsecutiveFailures is < 0 or > 100 || MinimumStorageFreeSlots is < 0 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumContinuousMaps));
        }

        return this;
    }
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

    public P1MapItem? TakeAt(int index)
    {
        if (index < 0 || index >= _maps.Count)
        {
            return null;
        }

        List<P1MapItem> maps = _maps.ToList();
        P1MapItem item = maps[index];
        maps.RemoveAt(index);
        _maps.Clear();
        foreach (P1MapItem map in maps)
        {
            _maps.Enqueue(map);
        }

        return item;
    }

    public bool TryInsert(P1MapItem map, int index)
    {
        ArgumentNullException.ThrowIfNull(map);
        map.Validate();
        if (_maps.Count >= MaximumCount)
        {
            return false;
        }

        List<P1MapItem> maps = _maps.ToList();
        maps.Insert(Math.Clamp(index, 0, maps.Count), map);
        _maps.Clear();
        foreach (P1MapItem value in maps)
        {
            _maps.Enqueue(value);
        }

        return true;
    }

    public bool TryMove(int sourceIndex, int targetIndex)
    {
        P1MapItem? map = TakeAt(sourceIndex);
        return map is not null && TryInsert(map, targetIndex);
    }

    public void Restore(IEnumerable<P1MapItem> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        if (_maps.Count != 0)
        {
            throw new InvalidOperationException("Only an empty map queue can be restored.");
        }

        foreach (P1MapItem map in maps)
        {
            if (!TryEnqueue(map))
            {
                throw new InvalidDataException("Map queue snapshot exceeds capacity.");
            }
        }
    }
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
    string AiSummary = "自动接敌，优先战吼后重击",
    LifeFlaskDefinition? LifeFlask = null,
    int IncreasedLifeFlaskEffectBasisPoints = 0,
    int LifeFlaskUseThresholdBasisPoints = 5_000,
    int AddedPhysicalDamage = 0,
    SkillUseProfile? HeavyStrikeProfile = null,
    LegendaryRule? WeaponLegendaryRule = null);

public sealed record MapNodeResult(
    int NodeIndex,
    string EnemyStableId,
    bool Elite,
    P1BattleOutcome Outcome,
    int Ticks,
    string FinalHash,
    IReadOnlyList<P1CombatEvent>? Events = null);

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
                team.FasterBleedingAllocated,
                MaximumTicks: 2_400,
                LifeFlask: team.LifeFlask,
                IncreasedLifeFlaskEffectBasisPoints: team.IncreasedLifeFlaskEffectBasisPoints,
                LifeFlaskUseThresholdBasisPoints: team.LifeFlaskUseThresholdBasisPoints,
                AddedPhysicalDamage: team.AddedPhysicalDamage,
                HeavyStrikeProfile: team.HeavyStrikeProfile,
                WeaponLegendaryRule: team.WeaponLegendaryRule);
            ulong encounterSeed = ((ulong)random.NextUInt() << 32) | random.NextUInt();
            P1EncounterResult result = new P1EncounterRunner().Run(encounter, encounterSeed);
            nodes.Add(new MapNodeResult(
                nodeIndex + 1,
                enemy.StableId,
                elite,
                result.Outcome,
                result.Ticks,
                result.FinalHash,
                result.Events));
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

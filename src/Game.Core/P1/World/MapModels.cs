using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P3;
using GameForWork.Core.P12;
using GameForWork.Core.P18;
using GameForWork.Core.P26;
using GameForWork.Core.P28;
using GameForWork.Core.Simulation;
using System.Text.Json.Serialization;

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
    LifeGarden,
    Warfront,
}

public enum MapAltarPreference
{
    Any,
    Avoid,
    RedOath,
    BlueOath,
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
    int StopAfterConsecutiveFailures = 0,
    int MinimumStorageFreeSlots = 0,
    IReadOnlyList<MapRoute>? RoutePriority = null,
    IReadOnlyList<MapRoute>? BlockedRoutes = null,
    MapAltarPreference AltarPreference = MapAltarPreference.Any,
    bool UseRareFragments = false,
    int RouteDecisionTimeoutSeconds = 30,
    int MaximumMapTier = P1MapItem.MaximumAreaLevel,
    P26MapFilter? MapFilter = null,
    P26MapOrder MapOrder = P26MapOrder.Recommended,
    P26NoMatchBehavior NoMatchBehavior = P26NoMatchBehavior.Wait,
    P28GameplayPolicy? Gameplay = null)
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

    public MapRoute SelectUnattendedRoute(P1MapItem map, int survivalScore, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        P1MapItem formal = map.EnsureFormal(seed);
        MapRoute[] candidates = formal.EffectiveRouteCandidates
            .Where(route => !(BlockedRoutes ?? []).Contains(route))
            .ToArray();
        if (candidates.Length == 0) candidates = formal.EffectiveRouteCandidates.ToArray();
        if (candidates.Length == 0) return PreferredRoute;

        MapRoute[] priorities = (RoutePriority is { Count: > 0 } ? RoutePriority : [PreferredRoute])
            .Distinct().ToArray();
        foreach (MapRoute priority in priorities)
        {
            if (candidates.Contains(priority))
                return priority;
        }

        return candidates
            .OrderBy(route => StableRouteTie(seed, formal.InstanceId, route))
            .First();
    }

    public ExpeditionPolicy Validate()
    {
        if (MaximumContinuousMaps is < 0 or > 10_000 ||
            StopAfterConsecutiveFailures is < 0 or > 100 || MinimumStorageFreeSlots is < 0 or > 100_000 ||
            RouteDecisionTimeoutSeconds is < 0 or > 300 ||
            MaximumMapTier is < P1MapItem.MinimumAreaLevel or > P1MapItem.MaximumAreaLevel ||
            (RoutePriority?.Any(route => !Enum.IsDefined(route)) ?? false) ||
            (BlockedRoutes?.Any(route => !Enum.IsDefined(route)) ?? false) || !Enum.IsDefined(MapOrder) ||
            !Enum.IsDefined(NoMatchBehavior))
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumContinuousMaps));
        }

        MapFilter?.Validate();
        Gameplay?.Validate();
        return this;
    }

    private static ulong StableRouteTie(ulong seed, string mapId, MapRoute route)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{seed}|{mapId}|{route}"));
        return BitConverter.ToUInt64(bytes, 0);
    }
}

public sealed record P1MapItem(
    string InstanceId,
    [property: JsonPropertyName("AreaLevel")] int Tier,
    string AreaId = "",
    P12MapRarity Rarity = P12MapRarity.Basic,
    int Quality = 0,
    IReadOnlyList<P12MapAffix>? Affixes = null,
    bool IsCorrupted = false,
    IReadOnlyList<MapRoute>? RouteCandidates = null,
    MapRoute? SelectedRoute = null,
    P12MapAltar Altar = P12MapAltar.None,
    IReadOnlyList<string>? Fragments = null,
    IReadOnlyList<string>? AtlasSnapshot = null,
    P26CorruptionRule CorruptionRule = P26CorruptionRule.None,
    bool IsLocked = false,
    bool IsManualPriority = false,
    bool IsRunning = false,
    bool IsQuestMap = false,
    long AcquiredSequence = 0,
    P28GameplayPolicy? Gameplay = null,
    ulong GameplaySeed = 0,
    int ResumeNode = 1)
{
    public const int MinimumTier = 1;
    public const int MaximumTier = 20;
    public const int MinimumAreaLevel = MinimumTier;
    public const int MaximumAreaLevel = MaximumTier;
    public const int RescueChances = 2;
    public const int TotalAttempts = RescueChances + 1;
    public int MonsterLevel => P16MapTierLevels.MonsterLevel(Tier);
    public IReadOnlyList<MapRoute> EffectiveRouteCandidates => RouteCandidates ?? [];
    public IReadOnlyList<P12MapAffix> EffectiveAffixes => Affixes ?? [];
    public bool IsProtected => IsLocked || IsManualPriority || IsRunning || IsQuestMap;
    private (int Monster, int Item) AffixQuantity => P26MapRules.CorruptionBonus(this);
    public int MonsterQuantityBasisPoints => Math.Clamp(AffixQuantity.Monster, 0,
        IsCorrupted ? P26MapRules.CorruptedMonsterQuantityCap : P26MapRules.RareMonsterQuantityCap);
    public int ItemQuantityBonusBasisPoints => Math.Clamp(Quality * 100 + AffixQuantity.Item, 0,
        IsCorrupted ? P26MapRules.CorruptedItemQuantityCap : P26MapRules.RareItemQuantityCap);
    public int ItemQuantityBasisPoints => 10_000 + ItemQuantityBonusBasisPoints;
    public int MonsterCountBasisPoints => 10_000 + MonsterQuantityBasisPoints;

    public P1MapItem EnsureFormal(ulong seed = 0) => P12MapRules.EnsureFormal(this, seed);

    public P1MapItem Validate()
    {
        Gameplay?.Validate();
        if (string.IsNullOrWhiteSpace(InstanceId) || InstanceId.Length > 128 ||
            Tier is < MinimumTier or > MaximumTier || ResumeNode is < 1 or > 32 ||
            Quality is < 0 or > 20 || !Enum.IsDefined(Rarity) || !Enum.IsDefined(Altar) ||
            EffectiveAffixes.Count > 6 || EffectiveRouteCandidates.Count > 3 ||
            EffectiveAffixes.Count(affix => affix.Family == P26MapAffixFamily.DangerousPrefix) > 3 ||
            EffectiveAffixes.Count(affix => affix.Family == P26MapAffixFamily.RewardSuffix) > 3 ||
            EffectiveAffixes.GroupBy(affix => P26MapAffixCatalog.Get(affix.Kind).Group)
                .Any(group => group.Key != P26MapAffixGroup.None && group.Count() > 1) ||
            EffectiveRouteCandidates.Any(route => !Enum.IsDefined(route)) ||
            EffectiveRouteCandidates.Distinct().Count() != EffectiveRouteCandidates.Count ||
            SelectedRoute is not null && !EffectiveRouteCandidates.Contains(SelectedRoute.Value) ||
            (Fragments?.Count ?? 0) > 4 || (AtlasSnapshot?.Count ?? 0) > 120 ||
            !Enum.IsDefined(CorruptionRule) || IsCorrupted != (CorruptionRule != P26CorruptionRule.None) || AcquiredSequence < 0 ||
            (AtlasSnapshot?.Distinct(StringComparer.Ordinal).Count() ?? 0) != (AtlasSnapshot?.Count ?? 0))
        {
            throw new ArgumentOutOfRangeException(nameof(Tier), "Maps require an ID and tier 1 through 20.");
        }

        return this;
    }
}

public static class P16MapTierLevels
{
    private static readonly int[] Levels =
    [
        70, 72, 73, 75, 76,
        78, 79, 81, 83, 84,
        86, 87, 89, 91, 92,
        94, 95, 97, 98, 100,
    ];

    public static IReadOnlyList<int> All => Levels;

    public static int MonsterLevel(int tier)
    {
        if (tier is < P1MapItem.MinimumTier or > P1MapItem.MaximumTier)
        {
            throw new ArgumentOutOfRangeException(nameof(tier));
        }

        return Levels[tier - 1];
    }

    public static int DropItemLevel(int tier, EnemyRarity rarity) => Math.Min(120,
        MonsterLevel(tier) + rarity switch
        {
            EnemyRarity.Rare => 1,
            EnemyRarity.Boss => 2,
            _ => 0,
        });
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

    public bool TryReplaceAt(int index, P1MapItem map)
    {
        ArgumentNullException.ThrowIfNull(map);
        map.Validate();
        if (index < 0 || index >= _maps.Count) return false;
        List<P1MapItem> maps = _maps.ToList();
        maps[index] = map;
        _maps.Clear();
        foreach (P1MapItem value in maps) _maps.Enqueue(value);
        return true;
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
    LegendaryRule? WeaponLegendaryRule = null,
    int MovementSpeedBasisPoints = 10_000,
    IReadOnlyList<SkillConfiguration>? ActiveSkills = null,
    int PartySize = 1,
    int FrontlineCount = 1,
    IReadOnlyList<P1FlaskKind>? Flasks = null,
    bool HasShield = false,
    int BlockChanceBasisPoints = 0,
    P18CombatProfile? Ascendancy = null,
    bool HasUsableWeapon = true,
    P205PassiveModifiers? PassiveProfile = null,
    int CriticalMultiplierBasisPoints = 15_000,
    bool AlwaysHit = false,
    bool CannotCrit = false,
    int IncreasedWarCryCooldownRecoveryBasisPoints = 0,
    int IncreasedWarCryRangeBasisPoints = 0,
    P30.P30VirtueViceLoadout? VirtueViceLoadout = null,
    int MoreAttackDamageBasisPoints = 0,
    int MoreSpellDamageBasisPoints = 0,
    int MoreDamageOverTimeBasisPoints = 0,
    int IncreasedActionSpeedBasisPoints = 0,
    int InstantLifeLeechBasisPoints = 0,
    LocalWeaponStats? LocalWeaponStats = null,
    int IncreasedSpellDamageBasisPoints = 0,
    int IncreasedAttackSpeedBasisPoints = 0,
    int MoreElementalDamageBasisPoints = 0,
    int MoreVoidDamageBasisPoints = 0,
    int MoreRareBossDamageBasisPoints = 0,
    bool HasOffHand = false);

public sealed record MapNodeResult(
    int NodeIndex,
    string EnemyStableId,
    bool Elite,
    P1BattleOutcome Outcome,
    int Ticks,
    string FinalHash,
    IReadOnlyList<P1CombatEvent>? Events = null,
    int WaveIndex = 1,
    int GridWidth = P3SceneTimeline.LogicalWidth,
    int GridHeight = P3SceneTimeline.LogicalHeight);

public sealed record MapAttemptResult(
    bool Succeeded,
    IReadOnlyList<MapNodeResult> Nodes,
    string FailureReason,
    P3SceneTimeline? Timeline = null);

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

        P3SceneTimeline timeline = P3SceneTimelineBuilder.BuildMapAttempt(team, map, route, attempt, seed);
        MapNodeResult[] nodes = timeline.Encounters.Select(segment => new MapNodeResult(
            segment.NodeIndex,
            segment.EnemyStableId,
            segment.Elite,
            segment.Outcome,
            segment.Ticks,
            segment.FinalHash,
            segment.CombatEvents,
            segment.WaveIndex)).ToArray();
        bool succeeded = timeline.Outcome == P1BattleOutcome.HeroVictory;
        string failure = succeeded || nodes.Length == 0
            ? string.Empty
            : $"node_{nodes[^1].NodeIndex}_wave_{nodes[^1].WaveIndex}_{timeline.Outcome}";
        return new MapAttemptResult(succeeded, nodes, failure, timeline);
    }
}

public sealed record P1MapRunResult(
    P1MapItem Map,
    MapRoute Route,
    bool Succeeded,
    int AttemptsUsed,
    int RescueChancesRemaining,
    IReadOnlyList<MapAttemptResult> Attempts,
    string FailureReason,
    long DurationMilliseconds = 0);

public sealed class P1MapRunner(IP1MapAttemptResolver resolver)
{
    public P1MapRunResult Run(P1MapItem map, MapRoute route, P1TeamBuild team, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var attempts = new List<MapAttemptResult>();
        map = map with { GameplaySeed = seed };
        int resumeNode = 1;
        for (int attempt = 1; attempt <= P1MapItem.TotalAttempts; attempt++)
        {
            MapAttemptResult result = resolver.Resolve(map with { ResumeNode = resumeNode }, route, team, attempt,
                resolver is P1MapAttemptResolver ? seed : unchecked(seed + (ulong)attempt - 1));
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
                    string.Empty,
                    attempts.Sum(item => item.Timeline?.DurationMilliseconds ?? 0));
            }
            resumeNode = result.Nodes.LastOrDefault()?.NodeIndex ?? 1;
            if (result.Timeline?.PlannedNodes?.FirstOrDefault(node => node.Index == resumeNode)?.Gameplay?.NoRescue == true)
                break;
        }

        return new P1MapRunResult(
            map,
            route,
            false,
            attempts.Count,
            0,
            attempts,
            attempts[^1].FailureReason,
            attempts.Sum(item => item.Timeline?.DurationMilliseconds ?? 0));
    }
}

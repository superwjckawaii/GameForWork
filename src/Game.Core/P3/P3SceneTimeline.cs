using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.Simulation;
using GameForWork.Core.P4;
using GameForWork.Core.P12;
using GameForWork.Core.P14;

namespace GameForWork.Core.P3;

public enum P3SceneEventKind
{
    TravelStarted,
    NodeEntered,
    WaveStarted,
    WarCry,
    HeavyStrike,
    Aftershock,
    EnemyAttack,
    Bleed,
    Flask,
    BossPhase,
    EnemyDefeated,
    SceneCompleted,
    SceneFailed,
    UnitMoved,
    EarthCleave,
    SpiritBlade,
    Chain,
    SeismicCharge,
    BloodTideSpin,
    Banner,
    SkillFailed,
    AshJavelin,
    EmberNova,
    StormBrand,
    MechanicChoice,
    SkillEffect,
    Ailment,
    Block,
    Guard,
    Ascendancy,
    FlaskCharge,
}

public sealed record P3GridPosition(int X, int Y);

public sealed record P3SceneEvent(
    long AtMilliseconds,
    P3SceneEventKind Kind,
    int NodeIndex,
    int WaveIndex,
    int Value,
    string Detail,
    int HeroLife,
    int HeroMaximumLife,
    int HeroMana,
    int HeroMaximumMana,
    int HeroShield,
    int HeroMaximumShield,
    int EnemyLife,
    int EnemyMaximumLife,
    P3GridPosition Position);

public sealed record P3EncounterSegment(
    int NodeIndex,
    int WaveIndex,
    string EnemyStableId,
    bool Elite,
    bool Boss,
    long StartMilliseconds,
    long DurationMilliseconds,
    P1BattleOutcome Outcome,
    int Ticks,
    string FinalHash,
    IReadOnlyList<P1CombatEvent> CombatEvents);

public sealed record P3SceneTimeline(
    string StableId,
    int GridWidth,
    int GridHeight,
    int NodeCount,
    int TotalWaves,
    long DurationMilliseconds,
    P1BattleOutcome Outcome,
    int FinalHeroLife,
    int FinalHeroMana,
    int FinalHeroShield,
    IReadOnlyList<P3EncounterSegment> Encounters,
    IReadOnlyList<P3SceneEvent> Events,
    string FinalHash,
    IReadOnlyList<P4SpatialFrame>? SpatialFrames = null)
{
    public const int LogicalWidth = 12;
    public const int LogicalHeight = 24;

    public P3SceneEvent? StateAt(long elapsedMilliseconds) => Events
        .TakeWhile(item => item.AtMilliseconds <= elapsedMilliseconds)
        .LastOrDefault();
}

public static class P3SceneTimelineBuilder
{
    private const int TickMilliseconds = 50;
    private const int BaseTilesPerSecond = 4;

    public static P3SceneTimeline BuildCampaign(
        P1TeamBuild build,
        CampaignNodeDefinition node,
        ulong seed)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(node);
        int nodeCount = node.Kind switch
        {
            CampaignNodeKind.NormalCombat => 3,
            CampaignNodeKind.EliteCombat => 4,
            CampaignNodeKind.ActBoss => 5,
            _ => 0,
        };
        if (nodeCount == 0)
        {
            throw new ArgumentException("Story events do not have a combat timeline.", nameof(node));
        }

        return Build(
            $"campaign:{node.StableId}",
            build,
            nodeCount,
            P16CampaignLevels.MonsterLevel(node),
            node.Kind == CampaignNodeKind.EliteCombat,
            node.Kind == CampaignNodeKind.ActBoss,
            abyssRoute: false,
            seed);
    }

    public static P3SceneTimeline BuildMapAttempt(
        P1TeamBuild build,
        P1MapItem map,
        MapRoute route,
        int attempt,
        ulong seed)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(map);
        P12MapCombatModifiers modifiers = P12MapCombatModifiers.From(map);
        P14MapPlan plan = P14MapPlanner.Build(map, route, [], seed ^ (ulong)attempt);
        return Build(
            $"map:{map.AreaId}:{map.InstanceId}:attempt:{attempt}",
            build,
            plan.Nodes.Count,
            map.MonsterLevel,
            forceElite: route == MapRoute.Abyss,
            finalBoss: true,
            abyssRoute: route == MapRoute.Abyss,
            seed,
            modifiers,
            plan);
    }

    public static long TravelMilliseconds(int tileDistance, int movementSpeedBasisPoints)
    {
        if (tileDistance <= 0 || movementSpeedBasisPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileDistance));
        }

        long numerator = checked((long)tileDistance * 1_000 * 10_000);
        long denominator = checked((long)BaseTilesPerSecond * movementSpeedBasisPoints);
        return Math.Max(TickMilliseconds, ((numerator + denominator - 1) / denominator + TickMilliseconds - 1) /
            TickMilliseconds * TickMilliseconds);
    }

    private static P3SceneTimeline Build(
        string stableId,
        P1TeamBuild build,
        int nodeCount,
        int areaLevel,
        bool forceElite,
        bool finalBoss,
        bool abyssRoute,
        ulong seed,
        P12MapCombatModifiers? mapModifiers = null,
        P14MapPlan? mapPlan = null)
    {
        var random = new Pcg32(seed);
        var events = new List<P3SceneEvent>();
        var encounters = new List<P3EncounterSegment>();
        var spatialFrames = new List<P4SpatialFrame>();
        long now = 0;
        int totalWaves = 0;
        int maximumLife = build.Sheet.MaximumLife().Value;
        int maximumMana = build.Sheet.MaximumMana().Value;
        int maximumShield = build.Sheet.MaximumShield().Value;
        int heroLife = maximumLife;
        int heroMana = maximumMana;
        int heroShield = maximumShield;
        P1BattleOutcome sceneOutcome = P1BattleOutcome.HeroVictory;

        for (int nodeIndex = 1; nodeIndex <= nodeCount; nodeIndex++)
        {
            P14MapNode? plannedNode = mapPlan?.Nodes[nodeIndex - 1];
            int distance = 14 + (int)(random.NextUInt() % 9);
            long travel = TravelMilliseconds(distance, build.MovementSpeedBasisPoints);
            events.Add(Event(now, P3SceneEventKind.TravelStarted, nodeIndex, 0, distance, "move", heroLife,
                maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 2)));
            now += travel;
            events.Add(Event(now, P3SceneEventKind.NodeEntered, nodeIndex, 0, 0, "12x24", heroLife,
                maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 20)));

            if (plannedNode is not null && plannedNode.EnemyCount == 0)
            {
                events.Add(Event(now, P3SceneEventKind.MechanicChoice, nodeIndex, 0, 0,
                    $"{plannedNode.Kind}|{plannedNode.DisplayName}", heroLife, maximumLife, heroMana,
                    maximumMana, heroShield, maximumShield, 0, 0, new(6, 12)));
                continue;
            }

            bool campaign = stableId.StartsWith("campaign:", StringComparison.Ordinal);
            bool bossNode = plannedNode?.Kind == P14MapNodeKind.Boss || finalBoss && nodeIndex == nodeCount;
            bool eliteNode = plannedNode?.Kind == P14MapNodeKind.Elite ||
                             !bossNode && (forceElite && nodeIndex % 2 == 0 || !campaign && nodeIndex == nodeCount - 1);
            int enemyCount = plannedNode?.EnemyCount > 0 ? plannedNode.EnemyCount : bossNode
                ? 5 + (int)(random.NextUInt() % 5)
                : campaign
                    ? (eliteNode ? 6 : 4) + (int)(random.NextUInt() % 5)
                    : abyssRoute ? 12 + (int)(random.NextUInt() % 13) : 8 + (int)(random.NextUInt() % 9);
            if (mapModifiers is not null)
                enemyCount = Math.Max(1, checked(enemyCount * mapModifiers.PackSizeBasisPoints / 10_000));
            totalWaves++;
            ulong encounterSeed = ((ulong)random.NextUInt() << 32) | random.NextUInt();
            long start = now;
            events.Add(Event(now, P3SceneEventKind.WaveStarted, nodeIndex, 1, enemyCount,
                plannedNode?.DisplayName ?? (bossNode ? "boss_group" : eliteNode ? "elite_group" : "group"), heroLife, maximumLife,
                heroMana, maximumMana, heroShield, maximumShield, enemyCount, enemyCount, new(6, 4)));
            P4NodeCombatResult result = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
                build,
                nodeIndex,
                areaLevel,
                enemyCount,
                eliteNode || mapModifiers?.ExtraElites == true,
                bossNode,
                abyssRoute,
                Formation: (int)(random.NextUInt() % 3),
                InitialHeroLife: heroLife,
                InitialHeroMana: heroMana,
                InitialHeroShield: heroShield,
                EnemyLifeBasisPoints: mapModifiers?.EnemyLifeBasisPoints ?? 10_000,
                EnemyDamageBasisPoints: mapModifiers?.EnemyDamageBasisPoints ?? 10_000,
                EnemySpeedBasisPoints: mapModifiers?.EnemySpeedBasisPoints ?? 10_000,
                PlayerRecoveryBasisPoints: mapModifiers?.PlayerRecoveryBasisPoints ?? 10_000,
                BossStableId: bossNode ? plannedNode?.BossStableId ?? mapPlan?.FinalBossStableId ?? string.Empty : string.Empty), encounterSeed);
            spatialFrames.AddRange(result.Frames.Select(frame => frame with { AtMilliseconds = start + frame.AtMilliseconds }));
            AppendSpatialEvents(events, result, start, nodeIndex, maximumLife, maximumMana, maximumShield);
            long duration = checked((long)result.Ticks * TickMilliseconds);
            now = checked(now + duration);
            encounters.Add(new P3EncounterSegment(nodeIndex, 1,
                bossNode ? plannedNode?.BossStableId ?? mapPlan?.FinalBossStableId ?? P1Enemies.AbyssWarden.StableId : "core.enemy.group", eliteNode, bossNode,
                start, duration, result.Outcome, result.Ticks, result.FinalHash, []));
            heroLife = result.HeroLife;
            heroMana = result.HeroMana;
            heroShield = result.HeroShield;
            if (result.Outcome != P1BattleOutcome.HeroVictory)
            {
                sceneOutcome = result.Outcome;
                events.Add(Event(now, P3SceneEventKind.SceneFailed, nodeIndex, 1, 0,
                    result.Outcome.ToString(), heroLife, maximumLife, heroMana, maximumMana, heroShield,
                    maximumShield, 0, enemyCount, new(6, 18)));
                return Finish(stableId, nodeCount, totalWaves, now, sceneOutcome, heroLife, heroMana,
                    heroShield, encounters, events, spatialFrames, seed);
            }
        }

        events.Add(Event(now, P3SceneEventKind.SceneCompleted, nodeCount, 0, 0, stableId, heroLife,
            maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 22)));
        return Finish(stableId, nodeCount, totalWaves, now, sceneOutcome, heroLife, heroMana, heroShield,
            encounters, events, spatialFrames, seed);
    }

    private static void AppendSpatialEvents(
        ICollection<P3SceneEvent> target,
        P4NodeCombatResult result,
        long start,
        int nodeIndex,
        int maximumLife,
        int maximumMana,
        int maximumShield)
    {
        foreach (P4SpatialEvent item in result.Events)
        {
            P3SceneEventKind kind = item.Kind switch
            {
                P4SpatialEventKind.HeroMoved or P4SpatialEventKind.EnemyMoved => P3SceneEventKind.UnitMoved,
                P4SpatialEventKind.WarCry => P3SceneEventKind.WarCry,
                P4SpatialEventKind.HeavyStrike => P3SceneEventKind.HeavyStrike,
                P4SpatialEventKind.EarthCleave => P3SceneEventKind.EarthCleave,
                P4SpatialEventKind.SpiritBladeLaunched or P4SpatialEventKind.SpiritBladeHit => P3SceneEventKind.SpiritBlade,
                P4SpatialEventKind.ChainHit => P3SceneEventKind.Chain,
                P4SpatialEventKind.SeismicCharge => P3SceneEventKind.SeismicCharge,
                P4SpatialEventKind.BloodTideSpin => P3SceneEventKind.BloodTideSpin,
                P4SpatialEventKind.BannerActivated => P3SceneEventKind.Banner,
                P4SpatialEventKind.SkillFailed => P3SceneEventKind.SkillFailed,
                P4SpatialEventKind.AshJavelin => P3SceneEventKind.AshJavelin,
                P4SpatialEventKind.EmberNova => P3SceneEventKind.EmberNova,
                P4SpatialEventKind.StormBrand => P3SceneEventKind.StormBrand,
                P4SpatialEventKind.BossTelegraph or P4SpatialEventKind.BossPhaseChanged => P3SceneEventKind.BossPhase,
                P4SpatialEventKind.SkillEffect => P3SceneEventKind.SkillEffect,
                P4SpatialEventKind.Ailment => P3SceneEventKind.Ailment,
                P4SpatialEventKind.Block => P3SceneEventKind.Block,
                P4SpatialEventKind.Guard => P3SceneEventKind.Guard,
                P4SpatialEventKind.Ascendancy => P3SceneEventKind.Ascendancy,
                P4SpatialEventKind.FlaskCharge => P3SceneEventKind.FlaskCharge,
                P4SpatialEventKind.EnemyAttack => P3SceneEventKind.EnemyAttack,
                P4SpatialEventKind.Bleed => P3SceneEventKind.Bleed,
                P4SpatialEventKind.Flask => P3SceneEventKind.Flask,
                P4SpatialEventKind.EnemyDefeated => P3SceneEventKind.EnemyDefeated,
                _ => P3SceneEventKind.BossPhase,
            };
            P4SpatialFrame? frame = result.Frames.TakeWhile(frame => frame.AtMilliseconds <= item.AtMilliseconds).LastOrDefault();
            P4EnemyFrame? enemy = frame?.Enemies.FirstOrDefault(enemy => enemy.EntityId == item.TargetId);
            target.Add(Event(
                start + item.AtMilliseconds,
                kind,
                nodeIndex,
                1,
                item.Value,
                $"{item.SourceId}|{item.TargetId}|{item.Detail}",
                frame?.HeroLife ?? result.HeroLife,
                maximumLife,
                frame?.HeroMana ?? result.HeroMana,
                maximumMana,
                frame?.HeroShield ?? result.HeroShield,
                maximumShield,
                enemy?.Life ?? 0,
                enemy?.MaximumLife ?? 0,
                new P3GridPosition(item.TargetPosition.XRaw / 1_000, item.TargetPosition.YRaw / 1_000)));
        }
    }

    private static void AppendCombatEvents(
        ICollection<P3SceneEvent> target,
        IEnumerable<P1CombatEvent> source,
        long start,
        int nodeIndex,
        int waveIndex,
        int maximumLife,
        int maximumMana,
        int maximumShield,
        int initialLife,
        int initialMana,
        int initialShield,
        int enemyMaximumLife)
    {
        int heroLife = initialLife;
        int heroMana = initialMana;
        int heroShield = initialShield;
        int enemyLife = enemyMaximumLife;
        foreach (P1CombatEvent item in source.Where(item => item.Kind != P1CombatEventKind.BattleEnded))
        {
            P3SceneEventKind kind;
            string detail = item.Detail;
            switch (item.Kind)
            {
                case P1CombatEventKind.WarCryUsed:
                    kind = P3SceneEventKind.WarCry;
                    break;
                case P1CombatEventKind.HeavyStrikeHit:
                    kind = P3SceneEventKind.HeavyStrike;
                    enemyLife = Math.Max(0, enemyLife - item.Value);
                    break;
                case P1CombatEventKind.LegendaryAftershock:
                    kind = P3SceneEventKind.Aftershock;
                    enemyLife = Math.Max(0, enemyLife - item.Value);
                    break;
                case P1CombatEventKind.EnemyHit:
                case P1CombatEventKind.CorpseExplosion:
                    kind = P3SceneEventKind.EnemyAttack;
                    int shieldDamage = Math.Min(heroShield, item.Value);
                    heroShield -= shieldDamage;
                    heroLife = Math.Max(0, heroLife - (item.Value - shieldDamage));
                    break;
                case P1CombatEventKind.BleedDamage:
                    kind = P3SceneEventKind.Bleed;
                    if (item.Detail == "hero")
                    {
                        int bleedShield = Math.Min(heroShield, item.Value);
                        heroShield -= bleedShield;
                        heroLife = Math.Max(0, heroLife - (item.Value - bleedShield));
                    }
                    else
                    {
                        enemyLife = Math.Max(0, enemyLife - item.Value);
                    }

                    break;
                case P1CombatEventKind.LifeFlaskUsed:
                    kind = P3SceneEventKind.Flask;
                    heroLife = Math.Min(maximumLife, heroLife + item.Value);
                    break;
                case P1CombatEventKind.BossPhaseChanged:
                case P1CombatEventKind.BossSummonedWorkers:
                case P1CombatEventKind.BossHazardCreated:
                    kind = P3SceneEventKind.BossPhase;
                    break;
                default:
                    continue;
            }

            target.Add(Event(start + (long)item.Tick * TickMilliseconds, kind, nodeIndex, waveIndex,
                item.Value, detail, heroLife, maximumLife, heroMana, maximumMana, heroShield, maximumShield,
                enemyLife, enemyMaximumLife, new(6, 18)));
        }
    }

    private static P3SceneTimeline Finish(
        string stableId,
        int nodeCount,
        int totalWaves,
        long duration,
        P1BattleOutcome outcome,
        int heroLife,
        int heroMana,
        int heroShield,
        IReadOnlyList<P3EncounterSegment> encounters,
        IReadOnlyList<P3SceneEvent> events,
        IReadOnlyList<P4SpatialFrame> spatialFrames,
        ulong seed)
    {
        string source = $"{stableId}|{seed}|{duration}|{outcome}|{heroLife}|{heroMana}|{heroShield}|" +
            string.Join(';', encounters.Select(item => item.FinalHash));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        return new P3SceneTimeline(stableId, P3SceneTimeline.LogicalWidth, P3SceneTimeline.LogicalHeight,
            nodeCount, totalWaves, duration, outcome, heroLife, heroMana, heroShield, encounters, events, hash,
            spatialFrames);
    }

    private static P3SceneEvent Event(
        long at,
        P3SceneEventKind kind,
        int node,
        int wave,
        int value,
        string detail,
        int heroLife,
        int heroMaximumLife,
        int heroMana,
        int heroMaximumMana,
        int heroShield,
        int heroMaximumShield,
        int enemyLife,
        int enemyMaximumLife,
        P3GridPosition position) => new(at, kind, node, wave, value, detail, heroLife, heroMaximumLife,
        heroMana, heroMaximumMana, heroShield, heroMaximumShield, enemyLife, enemyMaximumLife, position);
}

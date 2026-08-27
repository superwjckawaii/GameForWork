using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.Simulation;

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
    string FinalHash)
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
            Math.Max(1, node.Act),
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
        return Build(
            $"map:{map.InstanceId}:attempt:{attempt}",
            build,
            route == MapRoute.Abyss ? 10 : 8,
            map.AreaLevel,
            forceElite: route == MapRoute.Abyss,
            finalBoss: true,
            abyssRoute: route == MapRoute.Abyss,
            seed);
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
        ulong seed)
    {
        var random = new Pcg32(seed);
        var events = new List<P3SceneEvent>();
        var encounters = new List<P3EncounterSegment>();
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
            int distance = 14 + (int)(random.NextUInt() % 9);
            long travel = TravelMilliseconds(distance, build.MovementSpeedBasisPoints);
            events.Add(Event(now, P3SceneEventKind.TravelStarted, nodeIndex, 0, distance, "move", heroLife,
                maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 2)));
            now += travel;
            events.Add(Event(now, P3SceneEventKind.NodeEntered, nodeIndex, 0, 0, "12x24", heroLife,
                maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 20)));

            bool bossNode = finalBoss && nodeIndex == nodeCount;
            bool eliteNode = !bossNode && (forceElite && nodeIndex % 2 == 0 || nodeIndex == nodeCount - 1);
            int waves = bossNode ? 2 : eliteNode ? 2 : 1 + nodeIndex % 2;
            totalWaves += waves;
            for (int wave = 1; wave <= waves; wave++)
            {
                bool bossWave = bossNode && wave == waves;
                EnemyProfile enemy = bossWave
                    ? P1Enemies.AbyssWarden
                    : P1Enemies.NormalEnemies[(int)(random.NextUInt() % (uint)P1Enemies.NormalEnemies.Count)];
                IReadOnlyList<EliteAffix> affixes = eliteNode ? EnemyRules.RollEliteAffixes(random) : [];
                ScaledEnemy scaled = EnemyRules.Scale(enemy, areaLevel, affixes, abyssRoute);
                if (!abyssRoute)
                {
                    scaled = scaled with
                    {
                        Life = Math.Max(1, scaled.Life * 3 / 4),
                        MinimumPhysicalDamage = Math.Max(1, scaled.MinimumPhysicalDamage * 3 / 4),
                        MaximumPhysicalDamage = Math.Max(1, scaled.MaximumPhysicalDamage * 3 / 4),
                    };
                }

                ulong encounterSeed = ((ulong)random.NextUInt() << 32) | random.NextUInt();
                var request = new P1EncounterRequest(
                    build.Sheet,
                    build.Weapon,
                    build.HeavyStrike,
                    scaled,
                    build.FlatAccuracy,
                    build.IncreasedDamageBasisPoints,
                    build.IncreasedCriticalChanceBasisPoints,
                    build.IncreasedBleedChanceBasisPoints,
                    build.UseWarCry,
                    build.EchoNotableAllocated,
                    build.DeepWoundAllocated,
                    build.FasterBleedingAllocated,
                    MaximumTicks: 2_400,
                    LifeFlask: build.LifeFlask,
                    IncreasedLifeFlaskEffectBasisPoints: build.IncreasedLifeFlaskEffectBasisPoints,
                    LifeFlaskUseThresholdBasisPoints: build.LifeFlaskUseThresholdBasisPoints,
                    AddedPhysicalDamage: build.AddedPhysicalDamage,
                    HeavyStrikeProfile: build.HeavyStrikeProfile,
                    WeaponLegendaryRule: build.WeaponLegendaryRule,
                    InitialHeroLife: heroLife,
                    InitialHeroMana: heroMana,
                    InitialHeroShield: heroShield);
                P1EncounterResult result = new P1EncounterRunner().Run(request, encounterSeed);
                long start = now;
                events.Add(Event(now, P3SceneEventKind.WaveStarted, nodeIndex, wave, 0,
                    bossWave ? "boss" : eliteNode ? "elite" : "normal", heroLife, maximumLife, heroMana,
                    maximumMana, heroShield, maximumShield, scaled.Life, scaled.Life, new(6, 18)));
                AppendCombatEvents(events, result.Events, start, nodeIndex, wave, maximumLife, maximumMana,
                    maximumShield, heroLife, heroMana, heroShield, scaled.Life);
                long duration = checked((long)result.Ticks * TickMilliseconds);
                now = checked(now + duration);
                encounters.Add(new P3EncounterSegment(nodeIndex, wave, enemy.StableId, eliteNode, bossWave,
                    start, duration, result.Outcome, result.Ticks, result.FinalHash, result.Events));
                heroLife = result.HeroLife;
                heroMana = result.HeroMana;
                heroShield = result.HeroShield;
                if (result.Outcome != P1BattleOutcome.HeroVictory)
                {
                    sceneOutcome = result.Outcome;
                    events.Add(Event(now, P3SceneEventKind.SceneFailed, nodeIndex, wave, 0,
                        result.Outcome.ToString(), heroLife, maximumLife, heroMana, maximumMana, heroShield,
                        maximumShield, result.EnemyLife, scaled.Life, new(6, 18)));
                    return Finish(stableId, nodeCount, totalWaves, now, sceneOutcome, heroLife, heroMana,
                        heroShield, encounters, events, seed);
                }

                events.Add(Event(now, P3SceneEventKind.EnemyDefeated, nodeIndex, wave, 0, enemy.StableId,
                    heroLife, maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, scaled.Life,
                    new(6, 18)));
            }
        }

        events.Add(Event(now, P3SceneEventKind.SceneCompleted, nodeCount, 0, 0, stableId, heroLife,
            maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 22)));
        return Finish(stableId, nodeCount, totalWaves, now, sceneOutcome, heroLife, heroMana, heroShield,
            encounters, events, seed);
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
        ulong seed)
    {
        string source = $"{stableId}|{seed}|{duration}|{outcome}|{heroLife}|{heroMana}|{heroShield}|" +
            string.Join(';', encounters.Select(item => item.FinalHash));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        return new P3SceneTimeline(stableId, P3SceneTimeline.LogicalWidth, P3SceneTimeline.LogicalHeight,
            nodeCount, totalWaves, duration, outcome, heroLife, heroMana, heroShield, encounters, events, hash);
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

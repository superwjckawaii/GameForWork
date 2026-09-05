using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Simulation;
using GameForWork.Core.Spatial;
using GameForWork.Core.Maps;
using GameForWork.Core.Content;
using GameForWork.Core.Monsters;
using GameForWork.Core.Encounters;

namespace GameForWork.Core.Scenes;

public enum SceneEventKind
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

public sealed record GridPosition(int X, int Y);

public sealed record SceneEvent(
    long AtMilliseconds,
    SceneEventKind Kind,
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
    GridPosition Position, Point? EffectPosition = null);

public sealed record EncounterSegment(
    int NodeIndex,
    int WaveIndex,
    string EnemyStableId,
    bool Elite,
    bool Boss,
    long StartMilliseconds,
    long DurationMilliseconds,
    BattleOutcome Outcome,
    int Ticks,
    string FinalHash,
    IReadOnlyList<CombatEvent> CombatEvents);

public sealed record SceneTimeline(
    string StableId,
    int GridWidth,
    int GridHeight,
    int NodeCount,
    int TotalWaves,
    long DurationMilliseconds,
    BattleOutcome Outcome,
    int FinalHeroLife,
    int FinalHeroMana,
    int FinalHeroShield,
    IReadOnlyList<EncounterSegment> Encounters,
    IReadOnlyList<SceneEvent> Events,
    string FinalHash,
    IReadOnlyList<SpatialFrame>? SpatialFrames = null,
    IReadOnlyList<MapNode>? PlannedNodes = null)
{
    public const int LogicalWidth = 12;
    public const int LogicalHeight = 24;

    public SceneEvent? StateAt(long elapsedMilliseconds) => Events
        .TakeWhile(item => item.AtMilliseconds <= elapsedMilliseconds)
        .LastOrDefault();
}

public static class SceneTimelineBuilder
{
    private const int TickMilliseconds = 50;
    private const int BaseTilesPerSecond = 4;

    public static SceneTimeline BuildCampaign(
        TeamBuild build,
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
            CampaignLevels.MonsterLevel(node),
            node.Kind == CampaignNodeKind.EliteCombat,
            node.Kind == CampaignNodeKind.ActBoss,
            abyssRoute: false,
            seed,
            areaId: $"core.campaign.act{node.Act}",
            finalBossStableId: node.Kind == CampaignNodeKind.ActBoss
                ? Bosses.CampaignBosses[node.Act - 1].StableId : "");
    }

    public static SceneTimeline BuildMapAttempt(
        TeamBuild build,
        MapItem map,
        MapRoute route,
        int attempt,
        ulong seed)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(map);
        map = map with { EquipmentSnapshot = map.EquipmentSnapshot ?? GameForWork.Core.Maps.MapEquipmentSnapshot.From(build) };
        MapCombatModifiers modifiers = MapCombatModifiers.From(map);
        MapPlan plan = MapPlanner.Build(map, route, [], map.GameplaySeed == 0 ? seed : map.GameplaySeed);
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
            plan,
            map.AreaId, map: map) with { PlannedNodes = plan.Nodes };
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

    private static SceneTimeline Build(
        string stableId,
        TeamBuild build,
        int nodeCount,
        int areaLevel,
        bool forceElite,
        bool finalBoss,
        bool abyssRoute,
        ulong seed,
        MapCombatModifiers? mapModifiers = null,
        MapPlan? mapPlan = null,
        string areaId = "",
        string finalBossStableId = "",
        MapItem? map = null)
    {
        var random = new Pcg32(seed);
        var events = new List<SceneEvent>();
        var encounters = new List<EncounterSegment>();
        var spatialFrames = new List<SpatialFrame>();
        long now = 0;
        int totalWaves = 0;
        int maximumLife = build.Sheet.MaximumLife().Value;
        int maximumMana = build.Sheet.MaximumMana().Value;
        int maximumShield = build.Sheet.MaximumShield().Value;
        int heroLife = maximumLife;
        int heroMana = maximumMana;
        int heroShield = maximumShield;
        BattleOutcome sceneOutcome = BattleOutcome.HeroVictory;
        var flasks = new GameForWork.Core.Combat.FlaskRack(build);

        for (int nodeIndex = 1; nodeIndex <= nodeCount; nodeIndex++)
        {
            // Node-local randomness is stable across rescues. Cleared nodes are never replayed.
            if (map is not null) random = new Pcg32(seed ^ ((ulong)nodeIndex * 0x9e3779b97f4a7c15UL));
            if (nodeIndex < (map?.ResumeNode ?? 1)) continue;
            MapNode? plannedNode = mapPlan?.Nodes[nodeIndex - 1];
            EncounterModifiers gameplay = EncounterModifiers.For(mapPlan, nodeIndex, map);
            TeamBuild encounterBuild = gameplay.Apply(build);
            maximumLife = encounterBuild.Sheet.MaximumLife().Value;
            heroLife = Math.Min(heroLife, maximumLife);
            int distance = 14 + (int)(random.NextUInt() % 9);
            long travel = TravelMilliseconds(distance, build.MovementSpeedBasisPoints);
            events.Add(Event(now, SceneEventKind.TravelStarted, nodeIndex, 0, distance, "move", heroLife,
                maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 2)));
            now += travel;
            events.Add(Event(now, SceneEventKind.NodeEntered, nodeIndex, 0, 0, "12x24", heroLife,
                maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 20)));
            if (plannedNode?.Gameplay?.Candidates is { } candidates)
                events.Add(Event(now, SceneEventKind.MechanicChoice, nodeIndex, 0, 0,
                    $"选择：{plannedNode.DisplayName}；代价数值：{plannedNode.Gameplay.Choice?.Magnitude}；刷新{plannedNode.Gameplay.Refreshes}次；" +
                    string.Join(" / ", candidates.Select(c => $"{c.Name}·{c.Enemy}·{c.Rule}·{c.Tag}")),
                    heroLife, maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 12)));

            if (plannedNode is not null && plannedNode.EnemyCount == 0)
            {
                events.Add(Event(now, SceneEventKind.MechanicChoice, nodeIndex, 0, 0,
                    $"{plannedNode.Kind}|{plannedNode.DisplayName}", heroLife, maximumLife, heroMana,
                    maximumMana, heroShield, maximumShield, 0, 0, new(6, 12)));
                continue;
            }

            bool campaign = stableId.StartsWith("campaign:", StringComparison.Ordinal);
            bool bossNode = !string.IsNullOrEmpty(plannedNode?.BossStableId) || plannedNode?.Kind is MapNodeKind.Boss or MapNodeKind.WarfrontOfficer or MapNodeKind.WarfrontCommander ||
                            finalBoss && nodeIndex == nodeCount;
            bool eliteNode = plannedNode?.Kind == MapNodeKind.Elite ||
                             !bossNode && (forceElite && nodeIndex % 2 == 0 || !campaign && nodeIndex == nodeCount - 1);
            int enemyCount = plannedNode?.EnemyCount > 0 ? plannedNode.EnemyCount : bossNode
                ? 5 + (int)(random.NextUInt() % 5)
                : campaign
                    ? (eliteNode ? 6 : 4) + (int)(random.NextUInt() % 5)
                    : abyssRoute ? 12 + (int)(random.NextUInt() % 13) : 8 + (int)(random.NextUInt() % 9);
            if (mapModifiers is not null)
                enemyCount = Math.Max(1, checked(enemyCount * mapModifiers.MonsterQuantityBasisPoints / 10_000));
            if (bossNode && mapModifiers is not null)
                enemyCount = checked(enemyCount + mapModifiers.BossAdditionalGuards + mapModifiers.BossCount - 1);
            totalWaves++;
            ulong encounterSeed = ((ulong)random.NextUInt() << 32) | random.NextUInt();
            long start = now;
            events.Add(Event(now, SceneEventKind.WaveStarted, nodeIndex, 1, enemyCount,
                plannedNode?.DisplayName ?? (bossNode ? "boss_group" : eliteNode ? "elite_group" : "group"), heroLife, maximumLife,
                heroMana, maximumMana, heroShield, maximumShield, enemyCount, enemyCount, new(6, 4)));
            NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
                encounterBuild,
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
                EnemyLifeBasisPoints: Gameplay.Scale(mapModifiers?.EnemyLifeBasisPoints ?? 10_000, gameplay.Life),
                EnemyDamageBasisPoints: Gameplay.Scale(mapModifiers?.EnemyDamageBasisPoints ?? 10_000, gameplay.Damage),
                EnemySpeedBasisPoints: Gameplay.Scale(mapModifiers?.EnemySpeedBasisPoints ?? 10_000, gameplay.Speed),
                PlayerRecoveryBasisPoints: mapModifiers?.PlayerRecoveryBasisPoints ?? 10_000,
                BossStableId: bossNode ? plannedNode?.BossStableId ?? mapPlan?.FinalBossStableId ?? finalBossStableId ?? string.Empty : string.Empty,
                BossLifeBasisPoints: mapModifiers?.BossLifeBasisPoints ?? 10_000,
                BossDamageBasisPoints: mapModifiers?.BossDamageBasisPoints ?? 10_000,
                EnemyPhysicalReductionBasisPoints: mapModifiers?.EnemyPhysicalReductionBasisPoints ?? 0,
                EnemyElementalResistanceBasisPoints: mapModifiers?.EnemyElementalResistanceBasisPoints ?? 0,
                EnemyVoidResistanceBasisPoints: mapModifiers?.EnemyVoidResistanceBasisPoints ?? 0,
                EnemyPenetrationBasisPoints: mapModifiers?.EnemyPenetrationBasisPoints ?? 0,
                ExtraEnemyProjectiles: mapModifiers?.ExtraProjectiles ?? 0,
                EnemyProjectileDamageBasisPoints: mapModifiers?.ProjectileDamageBasisPoints ?? 10_000,
                EnemyAreaBasisPoints: mapModifiers?.EnemyAreaBasisPoints ?? 10_000,
                EnemyAreaDamageBasisPoints: mapModifiers?.EnemyAreaDamageBasisPoints ?? 10_000,
                BossCount: bossNode ? mapModifiers?.BossCount ?? 1 : 1,
                AdditionalRareEnemies: mapModifiers?.AdditionalRareEnemies ?? 0,
                EncounterFamily: mapPlan is null ? null : MonsterCatalog.FamilyForEncounter(
                    areaId, plannedNode?.Kind, mapPlan.Altar, nodeIndex, encounterSeed),
                FlaskRecoveryBasisPoints: gameplay.FlaskRecovery,
                IncomingHitBasisPoints: gameplay.IncomingHits,
                ExtraBossPhase: gameplay.ExtraPhase,
                GardenTags: plannedNode?.Gameplay?.Selections?.Select(c => c.Tag).ToArray() ??
                    (plannedNode?.Gameplay?.Mechanic == Mechanic.Garden ? [plannedNode.Gameplay.Choice!.Tag] : null),
                FlaskState: flasks), encounterSeed);
            spatialFrames.AddRange(result.Frames.Select(frame => frame with { AtMilliseconds = start + frame.AtMilliseconds }));
            AppendSpatialEvents(events, result, start, nodeIndex, maximumLife, maximumMana, maximumShield);
            long duration = checked((long)result.Ticks * TickMilliseconds);
            now = checked(now + duration);
            encounters.Add(new EncounterSegment(nodeIndex, 1,
                bossNode ? plannedNode?.BossStableId ?? mapPlan?.FinalBossStableId ?? finalBossStableId ?? Enemies.AbyssWarden.StableId : "core.enemy.group", eliteNode, bossNode,
                start, duration, result.Outcome, result.Ticks, result.FinalHash, []));
            heroLife = result.HeroLife;
            heroMana = result.HeroMana;
            heroShield = result.HeroShield;
            if (result.Outcome != BattleOutcome.HeroVictory)
            {
                sceneOutcome = result.Outcome;
                events.Add(Event(now, SceneEventKind.SceneFailed, nodeIndex, 1, 0,
                    result.Outcome.ToString(), heroLife, maximumLife, heroMana, maximumMana, heroShield,
                    maximumShield, 0, enemyCount, new(6, 18)));
                return Finish(stableId, nodeCount, totalWaves, now, sceneOutcome, heroLife, heroMana,
                    heroShield, encounters, events, spatialFrames, seed);
            }
        }

        events.Add(Event(now, SceneEventKind.SceneCompleted, nodeCount, 0, 0, stableId, heroLife,
            maximumLife, heroMana, maximumMana, heroShield, maximumShield, 0, 0, new(6, 22)));
        return Finish(stableId, nodeCount, totalWaves, now, sceneOutcome, heroLife, heroMana, heroShield,
            encounters, events, spatialFrames, seed);
    }

    private static void AppendSpatialEvents(
        ICollection<SceneEvent> target,
        NodeCombatResult result,
        long start,
        int nodeIndex,
        int maximumLife,
        int maximumMana,
        int maximumShield)
    {
        foreach (SpatialEvent item in result.Events)
        {
            SceneEventKind kind = item.Kind switch
            {
                SpatialEventKind.HeroMoved or SpatialEventKind.EnemyMoved => SceneEventKind.UnitMoved,
                SpatialEventKind.WarCry => SceneEventKind.WarCry,
                SpatialEventKind.HeavyStrike => SceneEventKind.HeavyStrike,
                SpatialEventKind.EarthCleave => SceneEventKind.EarthCleave,
                SpatialEventKind.SpiritBladeLaunched or SpatialEventKind.SpiritBladeHit => SceneEventKind.SpiritBlade,
                SpatialEventKind.ChainHit => SceneEventKind.Chain,
                SpatialEventKind.SeismicCharge => SceneEventKind.SeismicCharge,
                SpatialEventKind.BloodTideSpin => SceneEventKind.BloodTideSpin,
                SpatialEventKind.BannerActivated => SceneEventKind.Banner,
                SpatialEventKind.SkillFailed => SceneEventKind.SkillFailed,
                SpatialEventKind.AshJavelin => SceneEventKind.AshJavelin,
                SpatialEventKind.EmberNova => SceneEventKind.EmberNova,
                SpatialEventKind.StormBrand => SceneEventKind.StormBrand,
                SpatialEventKind.BossTelegraph or SpatialEventKind.BossPhaseChanged => SceneEventKind.BossPhase,
                SpatialEventKind.SkillEffect => SceneEventKind.SkillEffect,
                SpatialEventKind.Ailment => SceneEventKind.Ailment,
                SpatialEventKind.Block => SceneEventKind.Block,
                SpatialEventKind.Guard => SceneEventKind.Guard,
                SpatialEventKind.Ascendancy => SceneEventKind.Ascendancy,
                SpatialEventKind.FlaskCharge => SceneEventKind.FlaskCharge,
                SpatialEventKind.EnemyAttack => SceneEventKind.EnemyAttack,
                SpatialEventKind.Bleed => SceneEventKind.Bleed,
                SpatialEventKind.Flask => SceneEventKind.Flask,
                SpatialEventKind.EnemyDefeated => SceneEventKind.EnemyDefeated,
                _ => SceneEventKind.BossPhase,
            };
            SpatialFrame? frame = result.Frames.TakeWhile(frame => frame.AtMilliseconds <= item.AtMilliseconds).LastOrDefault();
            EnemyFrame? enemy = frame?.Enemies.FirstOrDefault(enemy => enemy.EntityId == item.TargetId);
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
                new GridPosition(item.TargetPosition.XRaw / 1_000, item.TargetPosition.YRaw / 1_000)) with { EffectPosition = item.TargetPosition });
        }
    }

    private static void AppendCombatEvents(
        ICollection<SceneEvent> target,
        IEnumerable<CombatEvent> source,
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
        foreach (CombatEvent item in source.Where(item => item.Kind != CombatEventKind.BattleEnded))
        {
            SceneEventKind kind;
            string detail = item.Detail;
            switch (item.Kind)
            {
                case CombatEventKind.WarCryUsed:
                    kind = SceneEventKind.WarCry;
                    break;
                case CombatEventKind.HeavyStrikeHit:
                    kind = SceneEventKind.HeavyStrike;
                    enemyLife = Math.Max(0, enemyLife - item.Value);
                    break;
                case CombatEventKind.LegendaryAftershock:
                    kind = SceneEventKind.Aftershock;
                    enemyLife = Math.Max(0, enemyLife - item.Value);
                    break;
                case CombatEventKind.EnemyHit:
                case CombatEventKind.CorpseExplosion:
                    kind = SceneEventKind.EnemyAttack;
                    int shieldDamage = Math.Min(heroShield, item.Value);
                    heroShield -= shieldDamage;
                    heroLife = Math.Max(0, heroLife - (item.Value - shieldDamage));
                    break;
                case CombatEventKind.BleedDamage:
                    kind = SceneEventKind.Bleed;
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
                case CombatEventKind.LifeFlaskUsed:
                    kind = SceneEventKind.Flask;
                    heroLife = Math.Min(maximumLife, heroLife + item.Value);
                    break;
                case CombatEventKind.BossPhaseChanged:
                case CombatEventKind.BossSummonedWorkers:
                case CombatEventKind.BossHazardCreated:
                    kind = SceneEventKind.BossPhase;
                    break;
                default:
                    continue;
            }

            target.Add(Event(start + (long)item.Tick * TickMilliseconds, kind, nodeIndex, waveIndex,
                item.Value, detail, heroLife, maximumLife, heroMana, maximumMana, heroShield, maximumShield,
                enemyLife, enemyMaximumLife, new(6, 18)));
        }
    }

    private static SceneTimeline Finish(
        string stableId,
        int nodeCount,
        int totalWaves,
        long duration,
        BattleOutcome outcome,
        int heroLife,
        int heroMana,
        int heroShield,
        IReadOnlyList<EncounterSegment> encounters,
        IReadOnlyList<SceneEvent> events,
        IReadOnlyList<SpatialFrame> spatialFrames,
        ulong seed)
    {
        string source = $"{stableId}|{seed}|{duration}|{outcome}|{heroLife}|{heroMana}|{heroShield}|" +
            string.Join(';', encounters.Select(item => item.FinalHash));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
        return new SceneTimeline(stableId, SceneTimeline.LogicalWidth, SceneTimeline.LogicalHeight,
            nodeCount, totalWaves, duration, outcome, heroLife, heroMana, heroShield, encounters, events, hash,
            spatialFrames);
    }

    private static SceneEvent Event(
        long at,
        SceneEventKind kind,
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
        GridPosition position) => new(at, kind, node, wave, value, detail, heroLife, heroMaximumLife,
        heroMana, heroMaximumMana, heroShield, heroMaximumShield, enemyLife, enemyMaximumLife, position);
}

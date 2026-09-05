using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P3;
using GameForWork.Core.P4;
using GameForWork.Core.P12;
using GameForWork.Core.P17;
using GameForWork.Core.P21;
using GameForWork.Core.P30;
using GameForWork.Core.P31;
using Godot;

namespace GameForWork.GodotClient;

public enum P1ViewMode
{
    Active,
    Hero,
    Mercenaries,
}

public partial class P1WorldView : Control
{
    private P1GameSession? _session;
    private P1ViewMode _mode;
    private double _visualClock;
    private double _visualElapsedMilliseconds;
    private long _lastObservedElapsed = -1;
    private string? _lastObservedScene;
    private Texture2D? _townBackground;
    private Texture2D? _combatBackground;
    private Texture2D? _regionAtlas;
    private Texture2D? _p31VfxAtlas;
    private Texture2D? _actorAnimationAtlas;
    private Texture2D? _enemyAnimationAtlas;
    private Texture2D? _bossAnimationAtlas;
    private readonly Dictionary<string, P4EnemyFrame> _nextEnemies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _positions = new(StringComparer.Ordinal);
    private readonly List<P3SceneEvent> _recentEvents = [];

    public P1GameSession? Session
    {
        get => _session;
        set
        {
            if (!ReferenceEquals(_session, value))
            {
                _session = value;
                ResetObservation();
            }
        }
    }

    public P1ViewMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                ResetObservation();
            }
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _townBackground = LoadOptional("res://assets/p21/town/p21-town-district.png");
        _combatBackground = LoadOptional("res://assets/p21/regions/act-1.png");
        _regionAtlas = LoadOptional("res://assets/p21/regions/p21-region-atlas.png");
        _p31VfxAtlas = LoadOptional("res://assets/p31/vfx/p31-combat-vfx.png");
        _actorAnimationAtlas = LoadOptional("res://assets/p21/characters/p21-actor-animation.png");
        _enemyAnimationAtlas = LoadOptional("res://assets/p21/enemies/p21-enemy-animation.png");
        _bossAnimationAtlas = LoadOptional("res://assets/p21/enemies/p21-boss-animation.png");
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
        {
            return;
        }

        _visualClock += delta;
        SyncObservation();
        if (_lastObservedScene is not null)
        {
            _visualElapsedMilliseconds += delta * 1_000 * (_session?.SimulationSpeed ?? 1);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 shake = P31VisualPreferences.ScreenShake ? ScreenShakeOffset() : Vector2.Zero;
        Rect2 bounds = new(new Vector2(4, 4) + shake, new Vector2(Math.Max(100, Size.X - 8), Math.Max(80, Size.Y - 8)));
        DrawRect(bounds, new Color("0c1017"), true);
        DrawActiveBattle(bounds);

        DrawRect(bounds, new Color("8b7353"), false, 2);
    }

    private void DrawTown(Rect2 bounds)
    {
        if (_townBackground is not null)
        {
            DrawTextureCover(_townBackground, bounds, new Rect2(Vector2.Zero, _townBackground.GetSize()));
        }
        else
        {
            DrawRect(bounds, new Color("161b24"), true);
            for (int row = 0; row < 7; row++)
            {
                float y = bounds.Position.Y + bounds.Size.Y * (0.34f + row * 0.085f);
                DrawLine(new Vector2(bounds.Position.X, y), new Vector2(bounds.End.X, y), new Color("252a30"), 1);
            }

            DrawBuilding(bounds, new Vector2(0.08f, 0.3f), new Vector2(0.24f, 0.5f), new Color("574438"));
            DrawBuilding(bounds, new Vector2(0.37f, 0.38f), new Vector2(0.2f, 0.42f), new Color("3c4b54"));
            DrawBuilding(bounds, new Vector2(0.62f, 0.25f), new Vector2(0.18f, 0.55f), new Color("483a43"));
        }

        float pulse = 0.55f + 0.25f * MathF.Sin((float)_visualClock * 2.4f);
        Vector2 portal = bounds.Position + new Vector2(bounds.Size.X * 0.89f, bounds.Size.Y * 0.48f);
        DrawCircle(portal, Math.Max(16, bounds.Size.Y * 0.17f), new Color(0.18f, 0.42f, 0.56f, 0.32f + pulse * 0.2f));
        DrawArc(portal, Math.Max(14, bounds.Size.Y * 0.14f), 0, MathF.Tau, 32, new Color("79c5d9"), 3);
        for (int index = 0; index < 7; index++)
        {
            float phase = (float)_visualClock * 0.4f + index;
            Vector2 mote = portal + new Vector2(MathF.Cos(phase) * 25, MathF.Sin(phase * 1.3f) * 20);
            DrawCircle(mote, 1.5f, new Color("a1e0e2"));
        }

        DrawCaption(bounds, "军锋镇 · 门扉仍在低鸣", new Color("eed9b4"));
    }

    private void DrawActiveBattle(Rect2 bounds)
    {
        ObservedScene? observed = Observe();
        P3SceneTimeline? timeline = observed?.Timeline;
        if (timeline is null)
        {
            DrawRect(bounds, new Color("0b0f16"), true);
            DrawCaption(bounds, "暂无进行中的战斗", new Color("8f98a4"));
            return;
        }

        if (_regionAtlas is not null && timeline.StableId.StartsWith("map:", StringComparison.Ordinal))
        {
            int region = RegionVisualIndex(timeline.StableId);
            DrawTextureCover(_regionAtlas, bounds, GridCell(_regionAtlas, region, 4, 3));
        }
        else if (_combatBackground is not null)
        {
            DrawTextureCover(_combatBackground, bounds, new Rect2(Vector2.Zero, _combatBackground.GetSize()));
        }
        else
        {
            DrawRect(bounds, new Color("111722"), true);
            float tileWidth = Math.Max(32, bounds.Size.X / 12);
            float tileHeight = tileWidth / 2;
            int row = 0;
            for (float y = bounds.Position.Y + bounds.Size.Y * 0.35f; y < bounds.End.Y + tileHeight; y += tileHeight)
            {
                for (float x = bounds.Position.X - tileWidth; x < bounds.End.X + tileWidth; x += tileWidth)
                {
                    Vector2 center = new(x + ((row & 1) == 0 ? 0 : tileWidth / 2), y);
                    Vector2[] diamond =
                    [
                        center + new Vector2(0, -tileHeight / 2),
                        center + new Vector2(tileWidth / 2, 0),
                        center + new Vector2(0, tileHeight / 2),
                        center + new Vector2(-tileWidth / 2, 0),
                    ];
                    DrawColoredPolygon(diamond, new Color("202735"));
                    DrawPolyline([.. diamond, diamond[0]], new Color("303948"), 1);
                }

                row++;
            }
        }

        long elapsed = Math.Clamp((long)_visualElapsedMilliseconds, 0, timeline.DurationMilliseconds);
        P3SceneEvent? state = timeline.StateAt(elapsed);
        float sceneProgress = timeline.DurationMilliseconds <= 0
            ? 0
            : Math.Clamp((float)elapsed / timeline.DurationMilliseconds, 0, 1);
        bool hero = observed?.Hero ?? true;
        P3EncounterSegment? activeEncounter = timeline.Encounters.FirstOrDefault(segment =>
            elapsed >= segment.StartMilliseconds && elapsed < segment.StartMilliseconds + segment.DurationMilliseconds);
        if (timeline.SpatialFrames is { Count: > 0 } && activeEncounter is not null)
        {
            DrawSpatialBattle(bounds, observed!, timeline, elapsed, sceneProgress, hero);
            return;
        }
        if (timeline.SpatialFrames is { Count: > 0 })
        {
            DrawTravel(bounds, observed!, state, elapsed);
            return;
        }

        float bob = MathF.Sin((float)_visualClock * 5) * 1.8f;
        Vector2 actor = bounds.Position + new Vector2(bounds.Size.X * 0.36f, bounds.Size.Y * 0.64f + bob);
        Vector2 enemy = bounds.Position + new Vector2(bounds.Size.X * 0.67f, bounds.Size.Y * 0.61f);
        DrawShadow(actor, 19);
        DrawShadow(enemy, 23);
        DrawP21Sprite(_actorAnimationAtlas, hero ? 0 : 1, P21Facing.Right,
            P21SpriteAction.Attack, (long)_visualElapsedMilliseconds, actor, new Vector2(44, 56),
            P21ArtContract.ActorRigCount, P21ArtContract.ActorCellWidth, P21ArtContract.ActorCellHeight);
        bool boss = sceneProgress > .82f;
        Texture2D? enemyAnimation = boss ? _bossAnimationAtlas : _enemyAnimationAtlas;
        DrawP21Sprite(enemyAnimation,
            boss ? P21ArtContract.BossRig(timeline.StableId) : P21ArtContract.EnemyRig(timeline.StableId),
            P21Facing.Left, P21SpriteAction.Attack, (long)_visualElapsedMilliseconds, enemy,
            boss ? new Vector2(62, 70) : new Vector2(40, 50),
            boss ? P21ArtContract.BossRigCount : P21ArtContract.EnemyBodyRigCount,
            boss ? P21ArtContract.BossCellWidth : P21ArtContract.ActorCellWidth,
            boss ? P21ArtContract.BossCellHeight : P21ArtContract.ActorCellHeight);

        IReadOnlyList<P3SceneEvent> recent = timeline?.Events
            .Where(item => item.AtMilliseconds <= elapsed && item.AtMilliseconds >= elapsed - 1_100)
            .ToArray() ?? [];
        DrawCompactSkillEffects(actor, enemy, recent);
        DrawFloatingNumbers(enemy, actor, recent, elapsed);

        float heroLife = state is null || state.HeroMaximumLife <= 0
            ? 1
            : (float)state.HeroLife / state.HeroMaximumLife;
        float heroMana = state is null || state.HeroMaximumMana <= 0
            ? 1
            : (float)state.HeroMana / state.HeroMaximumMana;
        float enemyLife = state is null || state.EnemyMaximumLife <= 0
            ? 0
            : (float)state.EnemyLife / state.EnemyMaximumLife;
        DrawBar(new Rect2(actor + new Vector2(-48, 11), new Vector2(96, 7)), heroLife, new Color("a73737"));
        DrawBar(new Rect2(actor + new Vector2(-48, 20), new Vector2(96, 5)), heroMana, new Color("356db4"));
        if (state?.HeroMaximumShield > 0)
        {
            DrawBar(new Rect2(actor + new Vector2(-48, 27), new Vector2(96, 4)),
                (float)state.HeroShield / state.HeroMaximumShield, new Color("76c7d9"));
        }

        if (state?.EnemyMaximumLife > 0)
        {
            DrawBar(new Rect2(enemy + new Vector2(-54, 25), new Vector2(108, 7)), enemyLife, new Color("8d2739"));
        }

        string title = observed?.Title ?? "等待主线战斗或远征地图";
        DrawCaption(bounds, title, new Color("e5d7be"));
        if (timeline is not null)
        {
            string phase = state?.Kind == P3SceneEventKind.TravelStarted
                ? $"移动中 · {state.Value} 格"
                : $"节点 {Math.Max(1, state?.NodeIndex ?? 1)}/{timeline.NodeCount} · 波次 {Math.Max(1, state?.WaveIndex ?? 1)}";
            DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(16, 59),
                phase, HorizontalAlignment.Left, -1, 13, new Color("c6bca9"));
        }
    }

    private void DrawTravel(Rect2 bounds, ObservedScene observed, P3SceneEvent? state, long elapsed)
    {
        Rect2 field = new(bounds.Position + new Vector2(18, 54), bounds.Size - new Vector2(36, 82));
        bool moving = state?.Kind == P3SceneEventKind.TravelStarted;
        float cycle = moving ? (float)(elapsed % 1_200) / 1_200 : .5f;
        Vector2 actor = field.Position + new Vector2(
            field.Size.X * (.18f + cycle * .64f),
            field.Size.Y * (.62f + MathF.Sin(cycle * MathF.Tau) * .08f));
        DrawShadow(actor + new Vector2(0, 6), 10);
        DrawP21Sprite(_actorAnimationAtlas, observed.Hero ? 0 : 1, P21Facing.Right,
            moving ? P21SpriteAction.Move : P21SpriteAction.Idle, elapsed, actor, new Vector2(44, 56),
            P21ArtContract.ActorRigCount, P21ArtContract.ActorCellWidth, P21ArtContract.ActorCellHeight);
        DrawCaption(bounds, observed.Title, new Color("e5d7be"));
        DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(16, 43),
            moving ? $"前往节点 {Math.Max(1, state?.NodeIndex ?? 1)} · 移动 {state?.Value ?? 0} 格" : "抵达节点，准备接敌",
            HorizontalAlignment.Left, -1, 13, new Color("c6bca9"));
    }

    private void SyncObservation()
    {
        ObservedScene? observed = Observe();
        string? scene = observed?.Timeline.StableId;
        long elapsed = observed?.ElapsedMilliseconds ?? 0;
        if (scene != _lastObservedScene || elapsed != _lastObservedElapsed)
        {
            _lastObservedScene = scene;
            _lastObservedElapsed = elapsed;
            _visualElapsedMilliseconds = elapsed;
        }
    }

    private void DrawSpatialBattle(
        Rect2 bounds,
        ObservedScene observed,
        P3SceneTimeline timeline,
        long elapsed,
        float sceneProgress,
        bool hero)
    {
        IReadOnlyList<P4SpatialFrame> frames = timeline.SpatialFrames!;
        int frameIndex = FindFrameIndex(frames, elapsed);
        P4SpatialFrame current = frames[frameIndex];
        P4SpatialFrame next = frameIndex + 1 < frames.Count ? frames[frameIndex + 1] : current;
        float interpolation = next.AtMilliseconds == current.AtMilliseconds
            ? 0
            : Math.Clamp((elapsed - current.AtMilliseconds) / (float)(next.AtMilliseconds - current.AtMilliseconds), 0, 1);

        Rect2 field = new(bounds.Position + new Vector2(18, 68), bounds.Size - new Vector2(36, 102));
        DrawRect(field, new Color(0.03f, 0.05f, 0.08f, 0.34f), true);
        for (int column = 0; column <= P3SceneTimeline.LogicalWidth; column++)
        {
            float x = field.Position.X + field.Size.X * column / P3SceneTimeline.LogicalWidth;
            DrawLine(new Vector2(x, field.Position.Y), new Vector2(x, field.End.Y), new Color(0.33f, 0.38f, 0.44f, 0.17f), 1);
        }
        for (int row = 0; row <= P3SceneTimeline.LogicalHeight; row++)
        {
            float y = field.Position.Y + field.Size.Y * row / P3SceneTimeline.LogicalHeight;
            DrawLine(new Vector2(field.Position.X, y), new Vector2(field.End.X, y), new Color(0.33f, 0.38f, 0.44f, 0.12f), 1);
        }

        bool heroStanding = current.HeroPosition == next.HeroPosition;
        Vector2 actor = MapPoint(field, Lerp(current.HeroPosition, next.HeroPosition, interpolation)) +
                        (heroStanding ? VisualFootwork("hero", elapsed, 3.2f) : Vector2.Zero);
        _nextEnemies.Clear();
        foreach (P4EnemyFrame enemy in next.Enemies) _nextEnemies[enemy.EntityId] = enemy;
        _positions.Clear();
        _positions["hero"] = actor;
        foreach (P4EnemyFrame enemy in current.Enemies)
        {
            P4Point position = _nextEnemies.TryGetValue(enemy.EntityId, out P4EnemyFrame? future)
                ? Lerp(enemy.Position, future.Position, interpolation)
                : enemy.Position;
            bool standing = future?.Position == enemy.Position;
            _positions[enemy.EntityId] = MapPoint(field, position) +
                                         (standing ? VisualFootwork(enemy.EntityId, elapsed, enemy.Boss ? 2.2f : 3f) : Vector2.Zero);
        }

        CollectRecentEvents(timeline.Events, elapsed, _recentEvents);
        DrawSpatialSkills(field, actor, _positions, _recentEvents, elapsed);

        foreach (P4EnemyFrame enemy in current.Enemies.Where(enemy => enemy.Life > 0 ||
                     HasRecentEvent(_recentEvents, P3SceneEventKind.EnemyDefeated, targetId: enemy.EntityId)))
        {
            Vector2 position = _positions[enemy.EntityId];
            float radius = enemy.Boss ? 12 : enemy.Elite ? 9 : 7;
            DrawShadow(position + new Vector2(0, 5), radius + 2);
            P4EnemyFrame future = _nextEnemies.GetValueOrDefault(enemy.EntityId) ?? enemy;
            P21Facing facing = FacingBetween(enemy.Position, future.Position, current.HeroPosition);
            P21SpriteAction action = ResolveEnemyAction(enemy, future, _recentEvents);
            long actionAge = EventAge(_recentEvents, elapsed, action == P21SpriteAction.Death
                ? P3SceneEventKind.EnemyDefeated
                : action == P21SpriteAction.Hit ? null : P3SceneEventKind.EnemyAttack, enemy.EntityId);
            DrawSpatialEnemy(position, enemy, radius, enemy.EntityId == current.HeroTargetId, facing, action, actionAge);
            if (enemy.Life > 0)
            {
                DrawBar(new Rect2(position + new Vector2(-16, 10), new Vector2(32, 4)),
                    enemy.MaximumLife <= 0 ? 0 : (float)enemy.Life / enemy.MaximumLife,
                    enemy.Boss ? new Color("cf4055") : new Color("8d2739"));
                DrawEnemyStatusIcons(position, enemy);
            }
        }

        DrawShadow(actor + new Vector2(0, 6), 10);
        P4EnemyFrame? heroTarget = current.Enemies.FirstOrDefault(enemy => enemy.EntityId == current.HeroTargetId);
        P21Facing heroFacing = FacingBetween(current.HeroPosition, next.HeroPosition,
            heroTarget?.Position ?? new P4Point(current.HeroPosition.XRaw, current.HeroPosition.YRaw - 1));
        P21SpriteAction heroAction = ResolveHeroAction(current, next, _recentEvents);
        foreach (P4AllyFrame ally in current.Allies ?? [])
        {
            Vector2 allyPosition = MapPoint(field, ally.Position) + VisualFootwork(ally.EntityId, elapsed, 2.6f);
            _positions[ally.EntityId] = allyPosition;
            DrawShadow(allyPosition + new Vector2(0, 5), 7);
            if (ally.SkillId.Length > 0)
            {
                P4AllyFrame? nextAlly = next.Allies?.FirstOrDefault(item => item.EntityId == ally.EntityId);
                P3SceneEvent? action = _recentEvents.LastOrDefault(item => EventSource(item, ally.EntityId) && item.Value > 0);
                P21Facing facing = FacingBetween(ally.Position, nextAlly?.Position ?? ally.Position,
                    action?.EffectPosition ?? current.HeroPosition);
                P21SpriteAction unitAction = nextAlly is not null && nextAlly.Position != ally.Position ? P21SpriteAction.Move :
                    action is not null && current.AtMilliseconds - action.AtMilliseconds < 500 ? P21SpriteAction.Attack : P21SpriteAction.Idle;
                int unitRig = ally.SkillId switch
                {
                    "p24.skill.summon_boneguard" => P21ArtContract.EnemyRig("core.enemy.oathless_guard"),
                    "p24.skill.summon_soulbow" => P21ArtContract.EnemyRig("core.enemy.ash_bone_archer"),
                    "p24.skill.summon_spirit_beast" => P21ArtContract.EnemyRig("core.enemy.gate_hound"),
                    _ => -1,
                };
                if (unitRig >= 0)
                    DrawP21Sprite(_enemyAnimationAtlas, unitRig, facing, unitAction, elapsed, allyPosition,
                        new Vector2(36, 46), P21ArtContract.EnemyBodyRigCount, P21ArtContract.ActorCellWidth, P21ArtContract.ActorCellHeight);
                else
                {
                    // Static construct silhouette uses its placed base and barrel, not a walking character rig.
                    DrawRect(new Rect2(allyPosition + new Vector2(-11, -9), new Vector2(22, 17)), new Color("516169"));
                    DrawLine(allyPosition + new Vector2(0, -8), allyPosition + new Vector2(0, -24), new Color("b7ac83"), 6);
                }
                DrawBar(new Rect2(allyPosition + new Vector2(-17, 11), new Vector2(34, 4)),
                    (float)ally.Life / Math.Max(1, ally.MaximumLife), new Color("54b599"));
                continue;
            }
            int rig = 1 + StableVisualIndex(ally.EntityId, 4);
            DrawP21Sprite(_actorAnimationAtlas, rig, heroFacing,
                heroAction == P21SpriteAction.Idle ? P21SpriteAction.Idle : heroAction,
                elapsed, allyPosition, new Vector2(36, 46), P21ArtContract.ActorRigCount,
                P21ArtContract.ActorCellWidth, P21ArtContract.ActorCellHeight);
        }
        DrawP21Sprite(_actorAnimationAtlas, hero ? 0 : 1, heroFacing, heroAction, elapsed,
            actor, new Vector2(44, 56), P21ArtContract.ActorRigCount,
            P21ArtContract.ActorCellWidth, P21ArtContract.ActorCellHeight,
            loop: heroAction != P21SpriteAction.Death);
        DrawBar(new Rect2(actor + new Vector2(-30, 11), new Vector2(60, 5)),
            current.HeroMaximumLife <= 0 ? 0 : (float)current.HeroLife / current.HeroMaximumLife, new Color("a73737"));
        DrawBar(new Rect2(actor + new Vector2(-30, 18), new Vector2(60, 4)),
            current.HeroMaximumMana <= 0 ? 0 : (float)current.HeroMana / current.HeroMaximumMana, new Color("356db4"));
        if (current.HeroMaximumShield > 0)
        {
            DrawBar(new Rect2(actor + new Vector2(-30, 24), new Vector2(60, 3)),
                (float)current.HeroShield / current.HeroMaximumShield, new Color("76c7d9"));
        }
        DrawVirtueViceIcons(actor, current.HeroVirtueViceLayers);

        DrawSpatialNumbers(field, _positions, _recentEvents, elapsed);
        DrawRewardStrip(bounds, hero);
        DrawCaption(bounds, observed.Title, new Color("e5d7be"));
        int alive = current.Enemies.Count(enemy => enemy.Life > 0);
        string target = current.Enemies.FirstOrDefault(enemy => enemy.EntityId == current.HeroTargetId)?.DisplayName ?? "移动接敌";
        string mechanic = timeline.Events.TakeWhile(item => item.AtMilliseconds <= elapsed)
            .LastOrDefault(item => item.NodeIndex == current.NodeIndex && item.Kind is P3SceneEventKind.WaveStarted or P3SceneEventKind.MechanicChoice)?.Detail ?? string.Empty;
        DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(16, 59),
            $"节点 {Math.Max(1, current.NodeIndex)}/{timeline.NodeCount} · {mechanic} · 队伍 {(current.Allies?.Count ?? 0) + 1} · 敌人 {alive}/{current.Enemies.Count} · 目标 {target}",
            HorizontalAlignment.Left, -1, 13, new Color("c6bca9"));
    }

    private static Vector2 VisualFootwork(string stableId, long elapsed, float amplitude)
    {
        const long interval = 720;
        long phase = Math.Max(0, elapsed / interval);
        float progress = (elapsed % interval) / (float)interval;
        progress = progress * progress * (3 - 2 * progress);
        Vector2 from = FootworkTarget(stableId, phase, amplitude);
        Vector2 to = FootworkTarget(stableId, phase + 1, amplitude);
        return from.Lerp(to, progress);
    }

    private static Vector2 FootworkTarget(string stableId, long phase, float amplitude)
    {
        int x = StableVisualIndex($"{stableId}|foot-x|{phase}", 7) - 3;
        int y = StableVisualIndex($"{stableId}|foot-y|{phase}", 7) - 3;
        return new Vector2(x / 3f * amplitude, y / 3f * amplitude * .55f);
    }

    private void DrawSpatialEnemy(Vector2 position, P4EnemyFrame enemy, float radius, bool targeted,
        P21Facing facing, P21SpriteAction action, long actionAge)
    {
        Texture2D? animation = enemy.Boss ? _bossAnimationAtlas : _enemyAnimationAtlas;
        if (animation is not null)
        {
            int rig = enemy.Boss ? P21ArtContract.BossRig(enemy.EnemyStableId) : P21ArtContract.EnemyRig(enemy.EnemyStableId);
            int cellWidth = enemy.Boss ? P21ArtContract.BossCellWidth : P21ArtContract.ActorCellWidth;
            int cellHeight = enemy.Boss ? P21ArtContract.BossCellHeight : P21ArtContract.ActorCellHeight;
            int rigCount = enemy.Boss ? P21ArtContract.BossRigCount : P21ArtContract.EnemyBodyRigCount;
            Vector2 size = enemy.Boss ? new Vector2(62, 70) : enemy.Elite ? new Vector2(40, 50) : new Vector2(34, 43);
            DrawP21Sprite(animation, rig, facing, action, actionAge, position, size, rigCount,
                cellWidth, cellHeight, loop: action != P21SpriteAction.Death);
            DrawEnemyRoleMarker(position, enemy);
            DrawEliteMarkers(position, enemy);
            if (enemy.Elite || enemy.Boss)
                DrawArc(position, radius + 7, 0, MathF.Tau, 16, new Color("e2b85d"), enemy.Boss ? 3 : 2);
            if (targeted) DrawArc(position, radius + 10, 0, MathF.Tau, 20, new Color(1, .85f, .35f, .9f), 2);
            return;
        }
        Color color = enemy.Role switch
        {
            P4UnitRole.Melee => new Color("91434b"),
            P4UnitRole.Ranged => new Color("7b5b45"),
            P4UnitRole.Caster => new Color("665194"),
            P4UnitRole.Charger => new Color("a16438"),
            P4UnitRole.Summoner => new Color("48705d"),
            _ => new Color("b03345"),
        };
        DrawCircle(position, radius, color);
        if (enemy.Role is P4UnitRole.Ranged or P4UnitRole.Caster)
        {
            DrawArc(position, radius + 3, 0, MathF.Tau, 12, color.Lightened(0.35f), 2);
        }
        if (enemy.Elite || enemy.Boss)
        {
            DrawArc(position, radius + 4, 0, MathF.Tau, 16, new Color("e2b85d"), enemy.Boss ? 3 : 2);
        }
        if (targeted)
        {
            DrawArc(position, radius + 8, 0, MathF.Tau, 20, new Color(1, 0.85f, 0.35f, 0.9f), 2);
        }
    }

    private void DrawSpatialSkills(
        Rect2 field,
        Vector2 actor,
        IReadOnlyDictionary<string, Vector2> positions,
        IEnumerable<P3SceneEvent> recent,
        long elapsed)
    {
        int effectLimit = P31VisualPreferences.EffectLimit(Size.X);
        foreach (P3SceneEvent item in recent.OrderByDescending(item => item.Kind == P3SceneEventKind.BossPhase || item.Detail.Contains("持续危险地面", StringComparison.Ordinal))
                     .ThenByDescending(item => item.AtMilliseconds).Take(effectLimit))
        {
            P31SkillVisualDescriptor? visual = VisualForEvent(item);
            float lifetime = visual?.LifetimeMilliseconds ?? 900f;
            float age = Math.Clamp((elapsed - item.AtMilliseconds) / lifetime, 0, 1);
            string[] ids = item.Detail.Split('|');
            Vector2 target = ids.Length > 1 && positions.TryGetValue(ids[1], out Vector2 live)
                ? live
                : MapPoint(field, new P4Point(item.Position.X * 1_000, item.Position.Y * 1_000));
            Vector2 source = ids.Length > 0 && positions.TryGetValue(ids[0], out Vector2 origin) ? origin : actor;
            bool ground = item.Detail.Contains("持续危险地面", StringComparison.Ordinal);
            bool warning = item.Kind == P3SceneEventKind.BossPhase && item.Detail.Contains("until:", StringComparison.Ordinal);
            if ((ground || warning) && item.EffectPosition is { } effect)
            {
                target = MapPoint(field, effect);
                float radius = field.Size.X * (ground ? .15f : 1f / 6f);
                DrawCircle(target, radius, new Color(ground ? "ab4c2230" : "f0804020"));
                DrawArc(target, radius, 0, MathF.Tau, 32, new Color(ground ? "dc772aaa" : "ffb050dd"), 2);
                continue;
            }
            if (visual is not null && age < .86f)
            {
                float size = 38f * visual.ScaleBasisPoints / 10_000f * (visual.Signature ? 1.12f : 1f);
                Vector2 variation = new((visual.VariationSeed % 3 - 1) * 2, ((visual.VariationSeed / 3) % 3 - 1) * 2);
                Vector2 center = (visual.UsesSourceToTarget ? source.Lerp(target, Math.Clamp(age * 1.25f, 0, 1)) : target) + variation;
                DrawP31Vfx(visual.AtlasCell, center, size, visual.UsesSourceToTarget ? source.AngleToPoint(target) : 0);
                if (visual.Signature && age > .28f)
                {
                    float secondAge = Math.Clamp((age - .28f) / .58f, 0, 1);
                    DrawP31Vfx((visual.AtlasCell + 1 + visual.VariationSeed) % 15, target,
                        size * (.72f + secondAge * .45f), -secondAge * .8f);
                }
                DrawSupportLayers(source, target, center, age, ReadSupportLayers(item.Detail));
            }
            else if (item.Kind == P3SceneEventKind.BossPhase && age < .9f)
            {
                DrawP31Vfx((int)P31SkillVisualFamily.BossWarning, target, 58 + age * 24, age * .35f);
            }
        }
        DrawCombatFeedback(field, positions, recent, elapsed);
    }

    private void DrawSpatialNumbers(
        Rect2 field,
        IReadOnlyDictionary<string, Vector2> positions,
        IEnumerable<P3SceneEvent> recent,
        long elapsed)
    {
        if (P31VisualPreferences.DamageNumbers == P31DamageNumberMode.Off) return;
        P3SceneEvent[] candidates = recent.Where(item => item.Value > 0 && item.Kind is
                P3SceneEventKind.HeavyStrike or P3SceneEventKind.EarthCleave or P3SceneEventKind.SpiritBlade or
                P3SceneEventKind.Chain or P3SceneEventKind.SeismicCharge or P3SceneEventKind.BloodTideSpin or
                P3SceneEventKind.AshJavelin or P3SceneEventKind.EmberNova or P3SceneEventKind.StormBrand or
                P3SceneEventKind.EnemyAttack or P3SceneEventKind.Bleed or P3SceneEventKind.SkillEffect or
                P3SceneEventKind.Ascendancy or P3SceneEventKind.Ailment).ToArray();
        List<(P3SceneEvent Event, int Value)> entries;
        if (P31VisualPreferences.DamageNumbers == P31DamageNumberMode.Full)
        {
            entries = candidates.Select(item => (item, item.Value)).ToList();
        }
        else
        {
            entries = candidates.GroupBy(item =>
            {
                string[] parts = item.Detail.Split('|');
                string target = parts.Length > 1 ? parts[1] : item.Position.ToString();
                return $"{target}|{item.AtMilliseconds / 100}|{DamageGroup(item)}";
            })
            .Select(group => (Event: group.OrderByDescending(item => item.AtMilliseconds).First(), Value: group.Sum(item => item.Value)))
            .OrderBy(entry => entry.Event.AtMilliseconds).ToList();
        }
        int lane = 0;
        foreach ((P3SceneEvent item, int value) in entries.Take(P31VisualPreferences.EffectLimit(Size.X) * 2))
        {
            float age = Math.Clamp((elapsed - item.AtMilliseconds) / 900f, 0, 1);
            string[] ids = item.Detail.Split('|');
            bool heroDamage = item.Kind == P3SceneEventKind.EnemyAttack ||
                              item.Kind == P3SceneEventKind.Bleed && ids.Length > 1 && ids[1] == "hero";
            string targetId = heroDamage ? "hero" : ids.Length > 1 ? ids[1] : string.Empty;
            Vector2 origin = positions.TryGetValue(targetId, out Vector2 live)
                ? live
                : MapPoint(field, new P4Point(item.Position.X * 1_000, item.Position.Y * 1_000));
            Vector2 position = origin + new Vector2((lane++ % 3 - 1) * 12, -18 - age * 30);
            bool critical = item.Detail.Contains("critical", StringComparison.Ordinal);
            Color color = heroDamage ? new Color(1, 0.35f, 0.3f, 1 - age * 0.7f) : DamageColor(item.Detail, 1 - age * .7f);
            DrawString(ThemeDB.FallbackFont, position, critical ? $"!{value:N0}" : $"{value:N0}",
                HorizontalAlignment.Center, critical ? 54 : 46, critical ? 17 : 14, color);
        }
    }

    private static Vector2 MapPoint(Rect2 field, P4Point point) => new(
        field.Position.X + field.Size.X * Math.Clamp(point.XRaw, 0, 12_000) / 12_000f,
        field.Position.Y + field.Size.Y * Math.Clamp(point.YRaw, 0, 24_000) / 24_000f);

    private static P4Point Lerp(P4Point from, P4Point to, float weight) => new(
        (int)MathF.Round(Mathf.Lerp(from.XRaw, to.XRaw, weight)),
        (int)MathF.Round(Mathf.Lerp(from.YRaw, to.YRaw, weight)));

    private void DrawP21Sprite(Texture2D? atlas, int rig, P21Facing facing, P21SpriteAction action,
        long elapsed, Vector2 feetPosition, Vector2 maximumSize, int rigCount, int cellWidth, int cellHeight,
        bool loop = true)
    {
        if (atlas is null)
        {
            DrawActor(feetPosition, rig == 0 ? new Color("6da9c0") : new Color("a885bd"), rig == 0, 0);
            return;
        }
        int column = P21ArtContract.AnimationColumn(action, Math.Max(0, elapsed), loop);
        int row = P21ArtContract.AnimationRow(rig, facing, rigCount);
        Rect2 source = P21ArtAtlas.AnimationCell(column, row, cellWidth, cellHeight);
        float scale = Math.Min(maximumSize.X / cellWidth, maximumSize.Y / cellHeight);
        Vector2 size = new(cellWidth * scale, cellHeight * scale);
        Rect2 destination = new(feetPosition + new Vector2(-size.X / 2, -size.Y), size);
        DrawTextureRectRegion(atlas, destination, source);
    }

    private static P21Facing FacingBetween(P4Point from, P4Point to, P4Point fallback)
    {
        int x = to.XRaw - from.XRaw;
        int y = to.YRaw - from.YRaw;
        if (x == 0 && y == 0)
        {
            x = fallback.XRaw - from.XRaw;
            y = fallback.YRaw - from.YRaw;
        }
        if (Math.Abs(x) >= Math.Abs(y)) return x < 0 ? P21Facing.Left : P21Facing.Right;
        return y < 0 ? P21Facing.Up : P21Facing.Down;
    }

    private static P21SpriteAction ResolveHeroAction(P4SpatialFrame current, P4SpatialFrame next,
        IReadOnlyList<P3SceneEvent> recent)
    {
        if (current.HeroLife <= 0) return P21SpriteAction.Death;
        if (recent.Any(item => EventTargets(item, "hero") && item.Kind is P3SceneEventKind.EnemyAttack or P3SceneEventKind.Bleed))
            return P21SpriteAction.Hit;
        P3SceneEvent? action = recent.LastOrDefault(item => EventSource(item, "hero") && IsHeroAction(item.Kind));
        if (action is not null)
            return action.Kind is P3SceneEventKind.WarCry or P3SceneEventKind.Banner or P3SceneEventKind.Guard or
                P3SceneEventKind.Flask or P3SceneEventKind.EmberNova or P3SceneEventKind.StormBrand
                ? P21SpriteAction.Cast
                : P21SpriteAction.Attack;
        return current.HeroPosition != next.HeroPosition ? P21SpriteAction.Move : P21SpriteAction.Idle;
    }

    private static P21SpriteAction ResolveEnemyAction(P4EnemyFrame current, P4EnemyFrame next,
        IReadOnlyList<P3SceneEvent> recent)
    {
        if (current.Life <= 0) return P21SpriteAction.Death;
        if (recent.Any(item => EventTargets(item, current.EntityId) && IsDamageEvent(item.Kind))) return P21SpriteAction.Hit;
        if (recent.Any(item => item.Kind == P3SceneEventKind.EnemyAttack && EventSource(item, current.EntityId)))
            return current.Role is P4UnitRole.Caster or P4UnitRole.Summoner ? P21SpriteAction.Cast : P21SpriteAction.Attack;
        return current.Position != next.Position ? P21SpriteAction.Move : P21SpriteAction.Idle;
    }

    private static bool IsHeroAction(P3SceneEventKind kind) => kind is
        P3SceneEventKind.WarCry or P3SceneEventKind.HeavyStrike or P3SceneEventKind.EarthCleave or
        P3SceneEventKind.SpiritBlade or P3SceneEventKind.SeismicCharge or P3SceneEventKind.BloodTideSpin or
        P3SceneEventKind.Banner or P3SceneEventKind.AshJavelin or P3SceneEventKind.EmberNova or
        P3SceneEventKind.StormBrand or P3SceneEventKind.SkillEffect or P3SceneEventKind.Guard or P3SceneEventKind.Flask;

    private static bool IsDamageEvent(P3SceneEventKind kind) => kind is
        P3SceneEventKind.HeavyStrike or P3SceneEventKind.EarthCleave or P3SceneEventKind.SpiritBlade or
        P3SceneEventKind.Chain or P3SceneEventKind.SeismicCharge or P3SceneEventKind.BloodTideSpin or
        P3SceneEventKind.AshJavelin or P3SceneEventKind.EmberNova or P3SceneEventKind.StormBrand or
        P3SceneEventKind.EnemyAttack or P3SceneEventKind.Bleed or P3SceneEventKind.SkillEffect;

    private static bool HasRecentEvent(IEnumerable<P3SceneEvent> events, P3SceneEventKind kind,
        string? sourceId = null, string? targetId = null) => events.Any(item => item.Kind == kind &&
        (sourceId is null || EventSource(item, sourceId)) && (targetId is null || EventTargets(item, targetId)));

    private static long EventAge(IEnumerable<P3SceneEvent> events, long elapsed, P3SceneEventKind? kind, string entityId)
    {
        P3SceneEvent? item = events.LastOrDefault(candidate =>
            (kind is null || candidate.Kind == kind) && (EventSource(candidate, entityId) || EventTargets(candidate, entityId)));
        return item is null ? elapsed : Math.Max(0, elapsed - item.AtMilliseconds);
    }

    private static bool EventSource(P3SceneEvent item, string entityId)
    {
        int separator = item.Detail.IndexOf('|');
        return separator > 0 && item.Detail.AsSpan(0, separator).SequenceEqual(entityId);
    }

    private static bool EventTargets(P3SceneEvent item, string entityId)
    {
        int first = item.Detail.IndexOf('|');
        if (first < 0) return false;
        int second = item.Detail.IndexOf('|', first + 1);
        ReadOnlySpan<char> target = second < 0 ? item.Detail.AsSpan(first + 1) : item.Detail.AsSpan(first + 1, second - first - 1);
        return target.SequenceEqual(entityId);
    }

    private void DrawEnemyRoleMarker(Vector2 position, P4EnemyFrame enemy)
    {
        int variant = P21ArtContract.EnemyVariant(enemy.EnemyStableId);
        Color color = variant switch { 0 => new Color("c44f4f"), 1 => new Color("59a4c7"), _ => new Color("a374c6") };
        Vector2 marker = position + new Vector2(-5 + variant * 5, enemy.Boss ? -65 : -44);
        if (enemy.Role is P4UnitRole.Ranged or P4UnitRole.Charger)
            DrawColoredPolygon([marker + new Vector2(0, -3), marker + new Vector2(3, 3), marker + new Vector2(-3, 3)], color);
        else if (enemy.Role is P4UnitRole.Caster or P4UnitRole.Summoner)
            DrawArc(marker, 3, 0, MathF.Tau, 8, color, 2);
        else DrawRect(new Rect2(marker - new Vector2(2, 2), new Vector2(4, 4)), color, true);
    }

    private void DrawEliteMarkers(Vector2 position, P4EnemyFrame enemy)
    {
        if (enemy.EliteAffixes is not { Count: > 0 }) return;
        int index = 0;
        foreach (EliteAffix affix in enemy.EliteAffixes.Take(4))
        {
            Color color = EliteAffixColor(affix);
            Vector2 point = position + new Vector2(-9 + index * 6, enemy.Boss ? -58 : -38);
            DrawCircle(point, 2.2f, color);
            index++;
        }
    }

    private static Color EliteAffixColor(EliteAffix affix) => affix switch
    {
        EliteAffix.FlameTouched or EliteAffix.CorpseExplosion => new Color("ef6337"),
        EliteAffix.FrostTouched => new Color("65cfe2"),
        EliteAffix.StormTouched or EliteAffix.Swift or EliteAffix.HastedAura => new Color("e6cb4d"),
        EliteAffix.VoidTouched or EliteAffix.ArcaneWard or EliteAffix.Suppressor => new Color("a66bd7"),
        EliteAffix.Vampiric or EliteAffix.Lacerating => new Color("c73c55"),
        EliteAffix.Regenerating => new Color("62b86e"),
        EliteAffix.IronSkin or EliteAffix.FortifiedAura or EliteAffix.Massive => new Color("aeb6bd"),
        _ => new Color("d6a85a"),
    };

    private void DrawP31Vfx(int index, Vector2 center, float size, float rotation)
    {
        if (_p31VfxAtlas is null) return;
        Rect2 source = new((index % 4) * 64, (index / 4) * 64, 64, 64);
        DrawSetTransform(center, rotation, Vector2.One);
        DrawTextureRectRegion(_p31VfxAtlas, new Rect2(new Vector2(-size / 2, -size / 2), new Vector2(size, size)), source);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    private void DrawSupportLayers(Vector2 source, Vector2 target, Vector2 center, float age, P31SupportVisualLayer layers)
    {
        float alpha = Math.Clamp(1 - age, 0, 1);
        if (layers.HasFlag(P31SupportVisualLayer.ExtraProjectiles))
        {
            Vector2 perpendicular = (target - source).Normalized().Orthogonal() * 7;
            DrawLine(source + perpendicular, target + perpendicular, new Color(.88f, .83f, .55f, alpha * .65f), 1);
            DrawLine(source - perpendicular, target - perpendicular, new Color(.88f, .83f, .55f, alpha * .65f), 1);
        }
        if (layers.HasFlag(P31SupportVisualLayer.ChainOrFork))
            DrawPolyline([source, center + new Vector2(0, -10), target], new Color(.42f, .86f, 1, alpha), 2);
        if (layers.HasFlag(P31SupportVisualLayer.Return))
            DrawArc(center, 10 + age * 15, .2f, 5.2f, 16, new Color(.95f, .8f, .32f, alpha), 2);
        if (layers.HasFlag(P31SupportVisualLayer.AreaPulse))
            DrawArc(target, 10 + age * 30, 0, MathF.Tau, 20, new Color(.75f, .54f, 1, alpha), 2);
        if (layers.HasFlag(P31SupportVisualLayer.Repeat))
            DrawArc(center, 7 + age * 18, 0, MathF.Tau, 14, new Color(1, .55f, .2f, alpha), 2);
        if (layers.HasFlag(P31SupportVisualLayer.CriticalFlash))
        {
            DrawLine(center - new Vector2(8, 0), center + new Vector2(8, 0), new Color(1, .92f, .55f, alpha), 2);
            DrawLine(center - new Vector2(0, 8), center + new Vector2(0, 8), new Color(1, .92f, .55f, alpha), 2);
        }
        if (layers.HasFlag(P31SupportVisualLayer.AilmentTrail))
            DrawCircle(center + new Vector2(0, age * 10), 3, new Color(.48f, .92f, .3f, alpha));
        if (layers.HasFlag(P31SupportVisualLayer.GuardShell))
            DrawArc(source, 15, 0, MathF.Tau, 18, new Color(.35f, .8f, 1, alpha), 2);
        if (layers.HasFlag(P31SupportVisualLayer.TriggerRune))
            DrawRect(new Rect2(target - new Vector2(7, 7), new Vector2(14, 14)), new Color(.72f, .36f, 1, alpha), false, 2);
        if (layers.HasFlag(P31SupportVisualLayer.MinionAura))
            DrawArc(source, 20 + age * 10, 0, MathF.Tau, 18, new Color(.58f, .84f, .62f, alpha), 2);
    }

    private void DrawCombatFeedback(Rect2 field, IReadOnlyDictionary<string, Vector2> positions,
        IEnumerable<P3SceneEvent> recent, long elapsed)
    {
        int lane = 0;
        foreach (P3SceneEvent item in recent.Where(NeedsFeedback).OrderByDescending(item => item.AtMilliseconds)
                     .Take(P31VisualPreferences.EffectLimit(Size.X)))
        {
            string[] ids = item.Detail.Split('|');
            string targetId = ids.Length > 1 ? ids[1] : "hero";
            Vector2 target = positions.TryGetValue(targetId, out Vector2 live)
                ? live : MapPoint(field, new P4Point(item.Position.X * 1_000, item.Position.Y * 1_000));
            float age = Math.Clamp((elapsed - item.AtMilliseconds) / 850f, 0, 1);
            string text = FeedbackText(item);
            if (text.Length == 0) continue;
            Color color = item.Kind switch
            {
                P3SceneEventKind.Block or P3SceneEventKind.Guard => new Color(.4f, .85f, 1, 1 - age),
                P3SceneEventKind.SkillFailed => new Color(1, .45f, .35f, 1 - age),
                P3SceneEventKind.Ailment => new Color(.65f, .9f, .36f, 1 - age),
                P3SceneEventKind.EnemyDefeated => new Color(1, .78f, .3f, 1 - age),
                _ => new Color(1, .9f, .7f, 1 - age),
            };
            Vector2 position = target + new Vector2((lane++ % 3 - 1) * 16, -34 - age * 22);
            DrawString(ThemeDB.FallbackFont, position, text, HorizontalAlignment.Center, 72, 12, color);
        }
    }

    private void DrawRewardStrip(Rect2 bounds, bool hero)
    {
        if (_session is null) return;
        P1TeamExpeditionState team = hero ? _session.World.Hero : _session.World.Mercenaries;
        ItemInstance[] items = team.Backpack.Items.TakeLast(Math.Min(5, team.Backpack.Count)).ToArray();
        if (items.Length == 0) return;
        float width = Math.Min(bounds.Size.X - 28, 360);
        Rect2 strip = new(bounds.End.X - width - 12, bounds.End.Y - 27, width, 18);
        DrawRect(strip, new Color(0.03f, .04f, .06f, .82f), true);
        DrawRect(strip, new Color("78623f"), false, 1);
        string text = "拾取 " + string.Join(" · ", items.Select(item => item.DisplayName));
        DrawString(ThemeDB.FallbackFont, strip.Position + new Vector2(6, 13), text,
            HorizontalAlignment.Left, (int)strip.Size.X - 12, 11, new Color("e3c98c"));
    }

    private void DrawEnemyStatusIcons(Vector2 position, P4EnemyFrame enemy)
    {
        var statuses = new List<(string Text, Color Color, int Layers)>();
        if (enemy.BleedStacks > 0) statuses.Add(("血", new Color("d64252"), enemy.BleedStacks));
        if (enemy.DamageOverTimeAilment != P17Ailment.None)
            statuses.Add((AilmentGlyph(enemy.DamageOverTimeAilment), AilmentColor(enemy.DamageOverTimeAilment), 1));
        if (enemy.ShockStacks > 0) statuses.Add(("电", new Color("f0d64c"), enemy.ShockStacks));
        if (enemy.ArmorBreakStacks > 0) statuses.Add(("破", new Color("c88950"), enemy.ArmorBreakStacks));
        if (enemy.Impaired) statuses.Add(("缓", new Color("68cde5"), 1));
        int index = 0;
        foreach ((string text, Color color, int layers) in statuses.Take(5))
        {
            Vector2 origin = position + new Vector2(-14 + index * 7, 17);
            DrawRect(new Rect2(origin, new Vector2(6, 6)), new Color(.03f, .04f, .06f, .9f), true);
            DrawString(ThemeDB.FallbackFont, origin + new Vector2(0, 6), layers > 1 ? layers.ToString() : text,
                HorizontalAlignment.Center, 6, 6, color);
            index++;
        }
    }

    private void DrawVirtueViceIcons(Vector2 actor, IReadOnlyDictionary<P30VirtueViceKind, int>? layers)
    {
        if (layers is null || layers.Count == 0) return;
        int index = 0;
        foreach ((P30VirtueViceKind kind, int count) in layers.OrderBy(item => item.Key))
        {
            Color color = kind switch
            {
                P30VirtueViceKind.Mercy => new Color("70c98b"),
                P30VirtueViceKind.Temperance => new Color("7bb6db"),
                P30VirtueViceKind.Humility => new Color("d1c29a"),
                P30VirtueViceKind.Rage => new Color("db4a3f"),
                P30VirtueViceKind.Sloth => new Color("8971c7"),
                _ => new Color("d4a14e"),
            };
            Vector2 origin = actor + new Vector2(-18 + index * 8, 30);
            DrawCircle(origin, 3, color);
            DrawString(ThemeDB.FallbackFont, origin + new Vector2(-3, 8), count.ToString(),
                HorizontalAlignment.Center, 7, 7, color.Lightened(.2f));
            index++;
        }
    }

    private static string AilmentGlyph(P17Ailment ailment) => ailment switch
    {
        P17Ailment.Ignite => "燃", P17Ailment.Erosion => "蚀", P17Ailment.Wither => "凋",
        P17Ailment.Chill or P17Ailment.Freeze => "冰", P17Ailment.Shock => "电",
        P17Ailment.Paralysis => "麻", P17Ailment.ArmorBreak => "破", _ => "异",
    };

    private static Color AilmentColor(P17Ailment ailment) => ailment switch
    {
        P17Ailment.Ignite => new Color("f06a38"),
        P17Ailment.Chill or P17Ailment.Freeze => new Color("68cde5"),
        P17Ailment.Shock or P17Ailment.Paralysis => new Color("f0d64c"),
        P17Ailment.Erosion or P17Ailment.Wither => new Color("a56bd6"),
        _ => new Color("70c96d"),
    };

    private static P31SkillVisualDescriptor? VisualForEvent(P3SceneEvent item)
    {
        string skillId = ReadDetailValue(item.Detail, "skill:") ?? item.Kind switch
        {
            P3SceneEventKind.HeavyStrike => P1SkillIds.HeavyStrike,
            P3SceneEventKind.EarthCleave => P1SkillIds.EarthCleave,
            P3SceneEventKind.Aftershock => P1SkillIds.SeismicCharge,
            P3SceneEventKind.SpiritBlade or P3SceneEventKind.Chain => P1SkillIds.SpiritBlade,
            P3SceneEventKind.SeismicCharge => P1SkillIds.SeismicCharge,
            P3SceneEventKind.BloodTideSpin => P1SkillIds.BloodTideSpin,
            P3SceneEventKind.AshJavelin => P1SkillIds.AshJavelin,
            P3SceneEventKind.EmberNova => P1SkillIds.EmberNova,
            P3SceneEventKind.StormBrand => P1SkillIds.StormBrand,
            P3SceneEventKind.WarCry => P1SkillIds.WarCry,
            P3SceneEventKind.Banner => P1SkillIds.IronOathBanner,
            _ => string.Empty,
        };
        return skillId.Length > 0 && P31VisualCatalog.TryForSkill(skillId, out P31SkillVisualDescriptor? result)
            ? result : null;
    }

    private static P31SupportVisualLayer ReadSupportLayers(string detail)
    {
        string? token = ReadDetailValue(detail, "supports:");
        return ulong.TryParse(token, out ulong flags)
            ? P31VisualCatalog.LayersForLegacySupport(flags)
            : P31SupportVisualLayer.None;
    }

    private static string? ReadDetailValue(string detail, string prefix)
    {
        int start = detail.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0) return null;
        start += prefix.Length;
        int end = detail.IndexOf('|', start);
        return end < 0 ? detail[start..] : detail[start..end];
    }

    private static bool NeedsFeedback(P3SceneEvent item)
    {
        if (item.Kind is P3SceneEventKind.Block or P3SceneEventKind.Guard or P3SceneEventKind.SkillFailed or
            P3SceneEventKind.Ailment or P3SceneEventKind.EnemyDefeated or P3SceneEventKind.BossPhase) return true;
        return item.Detail.Contains("|miss|", StringComparison.Ordinal) || item.Detail.EndsWith("|miss", StringComparison.Ordinal) ||
               item.Detail.Contains("result:dodge", StringComparison.Ordinal);
    }

    private static string FeedbackText(P3SceneEvent item)
    {
        if (item.Detail.Contains("spell_suppression", StringComparison.Ordinal)) return "法术压制";
        if (item.Detail.Contains("result:dodge", StringComparison.Ordinal)) return "闪避";
        if (item.Detail.Contains("|miss|", StringComparison.Ordinal) || item.Detail.EndsWith("|miss", StringComparison.Ordinal)) return "未命中";
        return item.Kind switch
        {
            P3SceneEventKind.Block => "格挡",
            P3SceneEventKind.Guard => "减伤",
            P3SceneEventKind.SkillFailed => "资源不足",
            P3SceneEventKind.EnemyDefeated => "击破",
            P3SceneEventKind.BossPhase => item.Detail.Contains("enraged", StringComparison.Ordinal) ? "狂暴" : "阶段变化",
            P3SceneEventKind.Ailment => AilmentText(ReadDetailValue(item.Detail, "ailment:") ?? ReadDetailValue(item.Detail, "dot:")),
            _ => string.Empty,
        };
    }

    private static string AilmentText(string? value) => value switch
    {
        "bleed" => "流血", "ignite" => "点燃", "chill" => "冰缓", "freeze" => "冻结",
        "shock" => "感电", "paralysis" => "麻痹", "erosion" => "侵蚀", "wither" => "凋零",
        "stun" => "眩晕", "armorbreak" or "armor-break" => "破甲", _ => "异常",
    };

    private static string DamageGroup(P3SceneEvent item)
    {
        if (item.Kind == P3SceneEventKind.EnemyAttack) return "hero";
        return DamageGroup(item.Detail);
    }

    private static string DamageGroup(string detail)
    {
        string[] types = ["physical", "fire", "cold", "lightning", "void"];
        return types.OrderByDescending(type => ReadDamageComponent(detail, type)).First();
    }

    private static Color DamageColor(string detail, float alpha)
    {
        return DamageGroup(detail) switch
        {
            "fire" => new Color(1, .37f, .12f, alpha),
            "cold" => new Color(.38f, .78f, 1, alpha),
            "lightning" => new Color(1, .87f, .28f, alpha),
            "void" => new Color(.72f, .38f, 1, alpha),
            _ => new Color(.94f, .82f, .58f, alpha),
        };
    }

    private static int ReadDamageComponent(string detail, string type)
    {
        int start = detail.IndexOf(type + ":", StringComparison.Ordinal);
        if (start < 0) return 0;
        start += type.Length + 1;
        int comma = detail.IndexOf(',', start);
        int separator = detail.IndexOf('|', start);
        int end = comma < 0 ? separator : separator < 0 ? comma : Math.Min(comma, separator);
        ReadOnlySpan<char> value = end < 0 ? detail.AsSpan(start) : detail.AsSpan(start, end - start);
        return int.TryParse(value, out int result) ? result : 0;
    }

    private Vector2 ScreenShakeOffset()
    {
        ObservedScene? observed = Observe();
        if (observed is null) return Vector2.Zero;
        IReadOnlyList<P3SceneEvent> events = observed.Timeline.Events;
        for (int index = events.Count - 1; index >= 0; index--)
        {
            P3SceneEvent item = events[index];
            long age = observed.ElapsedMilliseconds - item.AtMilliseconds;
            if (age > 180) break;
            if (age < 0 || item.Kind is not (P3SceneEventKind.HeavyStrike or P3SceneEventKind.BossPhase or
                P3SceneEventKind.EnemyDefeated or P3SceneEventKind.Ascendancy)) continue;
            float fade = 1 - age / 180f;
            return new Vector2(MathF.Sin((float)_visualClock * 96) * 3 * fade,
                MathF.Cos((float)_visualClock * 83) * 2 * fade);
        }
        return Vector2.Zero;
    }

    private static int FindFrameIndex(IReadOnlyList<P4SpatialFrame> frames, long elapsed)
    {
        int low = 0;
        int high = frames.Count - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (frames[middle].AtMilliseconds <= elapsed) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    private static void CollectRecentEvents(IReadOnlyList<P3SceneEvent> events, long elapsed, List<P3SceneEvent> destination)
    {
        destination.Clear();
        long minimum = elapsed - 900;
        int low = 0;
        int high = events.Count;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (events[middle].AtMilliseconds < minimum) low = middle + 1;
            else high = middle;
        }
        for (int index = low; index < events.Count && events[index].AtMilliseconds <= elapsed; index++)
            destination.Add(events[index]);
    }

    private ObservedScene? Observe()
    {
        if (_session is null)
        {
            return null;
        }

        if (_mode == P1ViewMode.Active && !_session.Campaign.Completed && _session.Campaign.ActiveTimeline is not null)
        {
            CampaignNodeDefinition node = _session.Campaign.CurrentNode!;
            return new ObservedScene(
                _session.Campaign.ActiveTimeline,
                _session.Campaign.CurrentNodeElapsedMilliseconds,
                $"主线 · 第 {node.Act} 幕 · {node.DisplayName}",
                true);
        }

        P1TeamExpeditionState preferred = _mode == P1ViewMode.Mercenaries
            ? _session.World.Mercenaries
            : _session.World.Hero;
        ObservedScene? expedition = ObserveExpedition(preferred, preferred.Kind == ExpeditionTeamKind.Hero);
        if (expedition is not null || _mode != P1ViewMode.Active)
        {
            return expedition;
        }

        return ObserveExpedition(_session.World.Mercenaries, false);
    }

    private static ObservedScene? ObserveExpedition(P1TeamExpeditionState team, bool hero)
    {
        if (team.ActiveMap is null || team.ActiveRun is null)
        {
            return null;
        }

        long elapsed = Math.Max(0, team.ActiveRun.DurationMilliseconds - team.RemainingMapTimeMilliseconds);
        foreach (MapAttemptResult attempt in team.ActiveRun.Attempts)
        {
            if (attempt.Timeline is null)
            {
                continue;
            }

            if (elapsed <= attempt.Timeline.DurationMilliseconds)
            {
                return new ObservedScene(attempt.Timeline, elapsed,
                    $"{(hero ? "主角" : "佣兵")}远征 · T{team.ActiveMap.Tier} · 怪物等级 {team.ActiveMap.MonsterLevel} · {team.ActiveRoute}", hero);
            }

            elapsed -= attempt.Timeline.DurationMilliseconds;
        }

        return null;
    }

    private void ResetObservation()
    {
        _lastObservedScene = null;
        _lastObservedElapsed = -1;
    }

    private void DrawCompactSkillEffects(Vector2 actor, Vector2 enemy, IEnumerable<P3SceneEvent> recent)
    {
        foreach (P3SceneEvent item in recent)
        {
            P31SkillVisualDescriptor? visual = VisualForEvent(item);
            if (visual is null) continue;
            float age = Math.Clamp((float)((_visualElapsedMilliseconds - item.AtMilliseconds) /
                                           visual.LifetimeMilliseconds), 0, 1);
            if (age >= .86f) continue;
            Vector2 center = visual.UsesSourceToTarget
                ? actor.Lerp(enemy, Math.Clamp(age * 1.25f, 0, 1))
                : item.Kind is P3SceneEventKind.WarCry or P3SceneEventKind.Banner ? actor : enemy;
            float size = 44f * visual.ScaleBasisPoints / 10_000f;
            DrawP31Vfx(visual.AtlasCell, center, size,
                visual.UsesSourceToTarget ? actor.AngleToPoint(enemy) : 0);
            DrawSupportLayers(actor, enemy, center, age, ReadSupportLayers(item.Detail));
        }
    }

    private void DrawFloatingNumbers(Vector2 enemy, Vector2 actor, IEnumerable<P3SceneEvent> recent, long elapsed)
    {
        if (P31VisualPreferences.DamageNumbers == P31DamageNumberMode.Off) return;
        int lane = 0;
        foreach (P3SceneEvent item in recent.Where(item => item.Value > 0 && item.Kind is
                     P3SceneEventKind.HeavyStrike or P3SceneEventKind.Aftershock or
                     P3SceneEventKind.EnemyAttack or P3SceneEventKind.Bleed))
        {
            float age = Math.Clamp((elapsed - item.AtMilliseconds) / 1_100f, 0, 1);
            bool hurtsHero = item.Kind == P3SceneEventKind.EnemyAttack ||
                             item.Kind == P3SceneEventKind.Bleed && item.Detail == "hero";
            Vector2 origin = hurtsHero ? actor : enemy;
            Vector2 position = origin + new Vector2((lane++ % 3 - 1) * 18, -45 - age * 35);
            DrawString(ThemeDB.FallbackFont, position, item.Value.ToString(), HorizontalAlignment.Center, 54, 16,
                hurtsHero ? new Color(1, 0.35f, 0.3f, 1 - age * 0.65f) : new Color(1, 0.82f, 0.35f, 1 - age * 0.65f));
        }
    }

    private sealed record ObservedScene(P3SceneTimeline Timeline, long ElapsedMilliseconds, string Title, bool Hero);

    private static Texture2D? LoadOptional(string path) => ResourceLoader.Exists(path)
        ? GD.Load<Texture2D>(path)
        : null;

    private void DrawBuilding(Rect2 bounds, Vector2 relativePosition, Vector2 relativeSize, Color color)
    {
        Rect2 building = new(bounds.Position + bounds.Size * relativePosition, bounds.Size * relativeSize);
        DrawRect(building, color, true);
        DrawColoredPolygon(
        [
            building.Position + new Vector2(-8, 0),
            building.Position + new Vector2(building.Size.X / 2, -building.Size.Y * 0.28f),
            building.Position + new Vector2(building.Size.X + 8, 0),
        ], color.Darkened(0.28f));
        DrawRect(new Rect2(building.Position + new Vector2(building.Size.X * 0.42f, building.Size.Y * 0.55f), new Vector2(building.Size.X * 0.2f, building.Size.Y * 0.45f)), new Color("221f20"), true);
        DrawCircle(building.Position + new Vector2(building.Size.X * 0.2f, building.Size.Y * 0.45f), 3, new Color("d3a85a"));
    }

    private void DrawActor(Vector2 position, Color cloth, bool hero, float attackCycle)
    {
        float lean = attackCycle is > 0.18f and < 0.38f ? 6 : 0;
        DrawCircle(position + new Vector2(lean, -27), 7, new Color("d4ad8c"));
        DrawColoredPolygon(
        [
            position + new Vector2(-9, -21),
            position + new Vector2(8 + lean, -21),
            position + new Vector2(13, 5),
            position + new Vector2(-13, 5),
        ], cloth);
        DrawLine(position + new Vector2(-6, 4), position + new Vector2(-8, 18), new Color("403733"), 5);
        DrawLine(position + new Vector2(6, 4), position + new Vector2(9, 18), new Color("403733"), 5);
        Vector2 weaponEnd = position + (attackCycle is > 0.18f and < 0.38f ? new Vector2(31, -16) : new Vector2(17, -37));
        DrawLine(position + new Vector2(5, -15), weaponEnd, hero ? new Color("d9c08b") : new Color("b9a8cf"), 4);
    }

    private static Rect2 GridCell(Texture2D texture, int index, int columns, int rows)
    {
        float width = texture.GetWidth() / (float)columns;
        float height = texture.GetHeight() / (float)rows;
        int column = Math.Abs(index) % columns;
        int row = Math.Abs(index) / columns % rows;
        return new Rect2(column * width + width * .01f, row * height + height * .01f, width * .98f, height * .98f);
    }

    private static int StableVisualIndex(string value, int count)
    {
        uint hash = 2166136261;
        foreach (char character in value) hash = (hash ^ character) * 16777619;
        return (int)(hash % (uint)Math.Max(1, count));
    }

    private static int RegionVisualIndex(string stableId)
    {
        for (int index = 0; index < P12MapCatalog.Areas.Count; index++)
            if (stableId.Contains($":{P12MapCatalog.Areas[index].StableId}:", StringComparison.Ordinal)) return index;
        return StableVisualIndex(stableId, P12MapCatalog.Areas.Count);
    }

    private void DrawShadow(Vector2 position, float radius)
    {
        DrawSetTransform(position, 0, new Vector2(1, 0.34f));
        DrawCircle(Vector2.Zero, radius, new Color(0, 0, 0, 0.42f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    private void DrawCaption(Rect2 bounds, string text, Color color) =>
        DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(16, 24), text, HorizontalAlignment.Left, -1, 14, color);

    private void DrawBar(Rect2 rect, float progress, Color color)
    {
        DrawRect(rect, new Color("242936"), true);
        DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * Math.Clamp(progress, 0, 1), rect.Size.Y)), color, true);
        DrawRect(rect, new Color("8a7962"), false, 1);
    }

    private void DrawTextureCover(Texture2D texture, Rect2 destination, Rect2 source)
    {
        if (destination.Size.X <= 0 || destination.Size.Y <= 0 || source.Size.X <= 0 || source.Size.Y <= 0) return;
        float destinationAspect = destination.Size.X / destination.Size.Y;
        float sourceAspect = source.Size.X / source.Size.Y;
        if (sourceAspect > destinationAspect)
        {
            float width = source.Size.Y * destinationAspect;
            source = new Rect2(source.Position + new Vector2((source.Size.X - width) / 2, 0), new Vector2(width, source.Size.Y));
        }
        else if (sourceAspect < destinationAspect)
        {
            float height = source.Size.X / destinationAspect;
            source = new Rect2(source.Position + new Vector2(0, (source.Size.Y - height) / 2), new Vector2(source.Size.X, height));
        }
        DrawTextureRectRegion(texture, destination, source);
    }
}

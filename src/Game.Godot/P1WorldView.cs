using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P3;
using GameForWork.Core.P4;
using GameForWork.Core.P12;
using GameForWork.Core.P21;
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
    private Texture2D? _characterAtlas;
    private Texture2D? _mercenaryTexture;
    private Texture2D? _enemyAtlas;
    private Texture2D? _bossAtlas;
    private Texture2D? _regionAtlas;
    private Texture2D? _vfxAtlas;
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
        _townBackground = LoadOptional("res://assets/p21/town/p21-town-district.png") ??
                          LoadOptional("res://assets/p2/town/military-town.png");
        _combatBackground = LoadOptional("res://assets/p21/regions/act-1.png") ??
                            LoadOptional("res://assets/p2/combat/gate-ruins.png");
        _characterAtlas = LoadOptional("res://assets/p15/characters/p15-character-directions.png") ??
                          LoadOptional("res://assets/p2/characters/p2-character-grid.png");
        _mercenaryTexture = null;
        _enemyAtlas = LoadOptional("res://assets/p15/enemies/p15-enemy-elite-atlas.png");
        _bossAtlas = LoadOptional("res://assets/p15/enemies/p15-boss-atlas.png");
        _regionAtlas = LoadOptional("res://assets/p21/regions/p21-region-atlas.png") ??
                       LoadOptional("res://assets/p15/regions/p15-region-atlas.png");
        _vfxAtlas = LoadOptional("res://assets/p21/vfx/p21-combat-vfx.png") ??
                    LoadOptional("res://assets/p15/vfx/p15-skill-mechanic-atlas.png");
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
        Rect2 bounds = new(4, 4, Math.Max(100, Size.X - 8), Math.Max(80, Size.Y - 8));
        DrawRect(bounds, new Color("0c1017"), true);
        DrawActiveBattle(bounds);

        DrawRect(bounds, new Color("8b7353"), false, 2);
    }

    private void DrawTown(Rect2 bounds)
    {
        if (_townBackground is not null)
        {
            DrawTextureRect(_townBackground, bounds, false);
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
            DrawTextureRectRegion(_regionAtlas, bounds, GridCell(_regionAtlas, region, 4, 3));
        }
        else if (_combatBackground is not null)
        {
            DrawTextureRect(_combatBackground, bounds, false);
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
        if (timeline.SpatialFrames is { Count: > 0 })
        {
            DrawSpatialBattle(bounds, observed!, timeline, elapsed, sceneProgress, hero);
            return;
        }

        float attackCycle = (float)(_visualClock * (hero ? 2.2 : 1.9) % 1.0);
        float bob = MathF.Sin((float)_visualClock * 5) * 1.8f;
        Vector2 actor = bounds.Position + new Vector2(bounds.Size.X * 0.36f, bounds.Size.Y * 0.64f + bob);
        Vector2 enemy = bounds.Position + new Vector2(bounds.Size.X * 0.67f, bounds.Size.Y * 0.61f);
        DrawShadow(actor, 19);
        DrawShadow(enemy, 23);
        if (_characterAtlas is null)
        {
            DrawActor(actor, hero ? new Color("6da9c0") : new Color("a885bd"), hero, attackCycle);
            DrawEnemy(enemy, attackCycle);
        }
        else
        {
            DrawAtlasActor(actor, hero, attackCycle);
            DrawAtlasEnemy(enemy, sceneProgress, attackCycle);
        }

        IReadOnlyList<P3SceneEvent> recent = timeline?.Events
            .Where(item => item.AtMilliseconds <= elapsed && item.AtMilliseconds >= elapsed - 1_100)
            .ToArray() ?? [];
        if (recent.Any(item => item.Kind == P3SceneEventKind.HeavyStrike))
        {
            float reach = Math.Clamp(attackCycle * 2, 0, 1);
            Vector2 slashCenter = actor.Lerp(enemy, 0.45f + reach * 0.35f);
            DrawArc(slashCenter, 18 + reach * 18, -1.2f, 1.1f, 14, new Color(0.95f, 0.78f, 0.42f, 1 - reach * 0.4f), 4);
            DrawCircle(enemy + new Vector2(-6, -12), 6 * (1 - reach), new Color("a83838"));
        }

        DrawSkillShapes(actor, enemy, recent);
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
        DrawBar(new Rect2(actor + new Vector2(-48, 25), new Vector2(96, 7)), heroLife, new Color("a73737"));
        DrawBar(new Rect2(actor + new Vector2(-48, 35), new Vector2(96, 5)), heroMana, new Color("356db4"));
        if (state?.HeroMaximumShield > 0)
        {
            DrawBar(new Rect2(actor + new Vector2(-48, 43), new Vector2(96, 4)),
                (float)state.HeroShield / state.HeroMaximumShield, new Color("76c7d9"));
        }

        if (state?.EnemyMaximumLife > 0)
        {
            DrawBar(new Rect2(enemy + new Vector2(-54, 25), new Vector2(108, 7)), enemyLife, new Color("8d2739"));
        }

        DrawBar(new Rect2(bounds.Position + new Vector2(16, 35), new Vector2(bounds.Size.X - 32, 7)), sceneProgress, new Color("bb8442"));
        string title = observed?.Title ?? "等待主线战斗或远征地图";
        DrawCaption(bounds, title, new Color("e5d7be"));
        if (timeline is not null)
        {
            string phase = state?.Kind == P3SceneEventKind.TravelStarted
                ? $"移动中 · {state.Value} 格"
                : $"节点 {Math.Max(1, state?.NodeIndex ?? 1)}/{timeline.NodeCount} · 波次 {Math.Max(1, state?.WaveIndex ?? 1)}";
            DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(16, 59),
                $"{phase} · 场景 {sceneProgress * 100:0.0}%", HorizontalAlignment.Left, -1, 13, new Color("c6bca9"));
        }
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

        Vector2 actor = MapPoint(field, Lerp(current.HeroPosition, next.HeroPosition, interpolation));
        _nextEnemies.Clear();
        foreach (P4EnemyFrame enemy in next.Enemies) _nextEnemies[enemy.EntityId] = enemy;
        _positions.Clear();
        _positions["hero"] = actor;
        foreach (P4EnemyFrame enemy in current.Enemies)
        {
            P4Point position = _nextEnemies.TryGetValue(enemy.EntityId, out P4EnemyFrame? future)
                ? Lerp(enemy.Position, future.Position, interpolation)
                : enemy.Position;
            _positions[enemy.EntityId] = MapPoint(field, position);
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
            }
        }

        DrawShadow(actor + new Vector2(0, 6), 10);
        P4EnemyFrame? heroTarget = current.Enemies.FirstOrDefault(enemy => enemy.EntityId == current.HeroTargetId);
        P21Facing heroFacing = FacingBetween(current.HeroPosition, next.HeroPosition,
            heroTarget?.Position ?? new P4Point(current.HeroPosition.XRaw, current.HeroPosition.YRaw - 1));
        P21SpriteAction heroAction = ResolveHeroAction(current, next, _recentEvents);
        foreach (P4AllyFrame ally in current.Allies ?? [])
        {
            Vector2 allyPosition = MapPoint(field, ally.Position);
            _positions[ally.EntityId] = allyPosition;
            DrawShadow(allyPosition + new Vector2(0, 5), 7);
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
        if (hero) DrawHeroEquipmentOverlay(actor, heroFacing);
        DrawBar(new Rect2(actor + new Vector2(-30, 21), new Vector2(60, 5)),
            current.HeroMaximumLife <= 0 ? 0 : (float)current.HeroLife / current.HeroMaximumLife, new Color("a73737"));
        DrawBar(new Rect2(actor + new Vector2(-30, 28), new Vector2(60, 4)),
            current.HeroMaximumMana <= 0 ? 0 : (float)current.HeroMana / current.HeroMaximumMana, new Color("356db4"));
        if (current.HeroMaximumShield > 0)
        {
            DrawBar(new Rect2(actor + new Vector2(-30, 34), new Vector2(60, 3)),
                (float)current.HeroShield / current.HeroMaximumShield, new Color("76c7d9"));
        }

        DrawSpatialNumbers(field, _positions, _recentEvents, elapsed);
        DrawBar(new Rect2(bounds.Position + new Vector2(16, 35), new Vector2(bounds.Size.X - 32, 7)), sceneProgress, new Color("bb8442"));
        DrawCaption(bounds, observed.Title, new Color("e5d7be"));
        int alive = current.Enemies.Count(enemy => enemy.Life > 0);
        string target = current.Enemies.FirstOrDefault(enemy => enemy.EntityId == current.HeroTargetId)?.DisplayName ?? "移动接敌";
        string mechanic = timeline.Events.TakeWhile(item => item.AtMilliseconds <= elapsed)
            .LastOrDefault(item => item.NodeIndex == current.NodeIndex && item.Kind is P3SceneEventKind.WaveStarted or P3SceneEventKind.MechanicChoice)?.Detail ?? string.Empty;
        DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(16, 59),
            $"节点 {Math.Max(1, current.NodeIndex)}/{timeline.NodeCount} · {mechanic} · 队伍 {(current.Allies?.Count ?? 0) + 1} · 敌人 {alive}/{current.Enemies.Count} · 目标 {target} · {sceneProgress * 100:0.0}%",
            HorizontalAlignment.Left, -1, 13, new Color("c6bca9"));
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
        Texture2D? atlas = enemy.Boss ? _bossAtlas : _enemyAtlas;
        if (atlas is not null)
        {
            int count = enemy.Boss ? 10 : 24;
            int index = StableVisualIndex(enemy.EnemyStableId, count);
            DrawGridSprite(atlas, index, enemy.Boss ? 5 : 6, enemy.Boss ? 2 : 5, position,
                enemy.Boss ? new Vector2(46, 54) : enemy.Elite ? new Vector2(35, 42) : new Vector2(29, 35));
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
        foreach (P3SceneEvent item in recent.OrderByDescending(item => item.AtMilliseconds).Take(12))
        {
            float age = Math.Clamp((elapsed - item.AtMilliseconds) / 900f, 0, 1);
            string[] ids = item.Detail.Split('|');
            Vector2 target = ids.Length > 1 && positions.TryGetValue(ids[1], out Vector2 live)
                ? live
                : MapPoint(field, new P4Point(item.Position.X * 1_000, item.Position.Y * 1_000));
            Vector2 source = ids.Length > 0 && positions.TryGetValue(ids[0], out Vector2 origin) ? origin : actor;
            int vfxIndex = VfxIndex(item.Kind);
            if (vfxIndex >= 0 && age < .58f) DrawVfx(vfxIndex, target, item.Kind == P3SceneEventKind.BossPhase ? 54 : 38);
            if (item.Kind == P3SceneEventKind.WarCry)
            {
                DrawArc(actor, 14 + age * 38, 0, MathF.Tau, 24, new Color(0.95f, 0.62f, 0.2f, 1 - age), 3);
            }
            else if (item.Kind == P3SceneEventKind.EarthCleave)
            {
                DrawArc(actor, 20 + age * 48, -2.7f, -0.25f, 22, new Color(0.83f, 0.55f, 0.24f, 1 - age), 5);
            }
            else if (item.Kind == P3SceneEventKind.SeismicCharge)
            {
                DrawLine(source, target, new Color(0.92f, 0.55f, 0.2f, 1 - age), 6);
                DrawArc(target, 10 + age * 34, 0, MathF.Tau, 18, new Color(0.9f, 0.45f, 0.18f, 1 - age), 4);
            }
            else if (item.Kind == P3SceneEventKind.BloodTideSpin)
            {
                DrawArc(actor, 22 + age * 22, 0, MathF.Tau, 28, new Color(0.76f, 0.12f, 0.18f, 1 - age), 5);
            }
            else if (item.Kind == P3SceneEventKind.Banner)
            {
                DrawLine(actor + new Vector2(-12, 5), actor + new Vector2(-12, -45), new Color("d7c07b"), 3);
                DrawColoredPolygon([actor + new Vector2(-10, -44), actor + new Vector2(18, -37), actor + new Vector2(-10, -27)],
                    new Color(0.58f, 0.12f, 0.16f, 1 - age * 0.35f));
            }
            else if (item.Kind is P3SceneEventKind.SpiritBlade or P3SceneEventKind.Chain)
            {
                DrawLine(source, target, new Color(0.43f, 0.86f, 0.92f, 1 - age * 0.8f), item.Kind == P3SceneEventKind.Chain ? 2 : 4);
                DrawCircle(source.Lerp(target, age), 4, new Color(0.75f, 0.96f, 1, 1 - age * 0.5f));
            }
            else if (item.Kind == P3SceneEventKind.AshJavelin)
            {
                DrawLine(source, target, new Color(1, .38f, .08f, 1 - age * .65f), 4);
                Vector2 projectile = source.Lerp(target, age);
                DrawColoredPolygon([projectile + new Vector2(7, 0), projectile + new Vector2(-5, -3),
                    projectile + new Vector2(-5, 3)], new Color(1, .72f, .18f, 1 - age * .45f));
            }
            else if (item.Kind == P3SceneEventKind.EmberNova)
            {
                DrawArc(source, 10 + age * 55, 0, MathF.Tau, 30, new Color(1, .28f, .06f, 1 - age), 6);
                DrawArc(source, 5 + age * 38, 0, MathF.Tau, 24, new Color(1, .78f, .22f, 1 - age), 2);
            }
            else if (item.Kind == P3SceneEventKind.StormBrand)
            {
                Vector2 middle = source.Lerp(target, .5f) + new Vector2(0, age < .5f ? -10 : 10);
                DrawPolyline([source, middle, target], new Color(.32f, .82f, 1, 1 - age * .75f), 4);
                DrawArc(target, 7 + age * 16, 0, MathF.Tau, 12, new Color(.62f, .92f, 1, 1 - age), 2);
            }
            else if (item.Kind == P3SceneEventKind.HeavyStrike)
            {
                DrawArc(target, 8 + age * 20, -1.8f, 0.8f, 12, new Color(1, 0.78f, 0.34f, 1 - age), 4);
            }
            else if (item.Kind == P3SceneEventKind.BossPhase)
            {
                DrawArc(target, 18 + age * 34, 0, MathF.Tau, 24,
                    new Color(1, .25f + age * .25f, .12f, 1 - age * .45f), 3);
                DrawLine(source, target, new Color(1, .7f, .25f, 1 - age), 2);
                DrawRect(new Rect2(target - new Vector2(22, 22), new Vector2(44, 44)),
                    new Color(1, .35f, .15f, 1 - age), false, 2);
            }
        }
    }

    private void DrawSpatialNumbers(
        Rect2 field,
        IReadOnlyDictionary<string, Vector2> positions,
        IEnumerable<P3SceneEvent> recent,
        long elapsed)
    {
        var merged = recent.Where(item => item.Value > 0 && item.Kind is
                P3SceneEventKind.HeavyStrike or P3SceneEventKind.EarthCleave or P3SceneEventKind.SpiritBlade or
                P3SceneEventKind.Chain or P3SceneEventKind.SeismicCharge or P3SceneEventKind.BloodTideSpin or
                P3SceneEventKind.AshJavelin or P3SceneEventKind.EmberNova or P3SceneEventKind.StormBrand or
                P3SceneEventKind.EnemyAttack or P3SceneEventKind.Bleed)
            .GroupBy(item =>
            {
                string[] parts = item.Detail.Split('|');
                string target = parts.Length > 1 ? parts[1] : item.Position.ToString();
                return $"{target}|{item.AtMilliseconds / 50}|{(item.Kind == P3SceneEventKind.EnemyAttack ? "hero" : "enemy")}";
            })
            .Select(group => (Event: group.OrderByDescending(item => item.AtMilliseconds).First(), Value: group.Sum(item => item.Value)))
            .OrderBy(entry => entry.Event.AtMilliseconds)
            .ToArray();
        int lane = 0;
        foreach ((P3SceneEvent item, int value) in merged)
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
            DrawString(ThemeDB.FallbackFont, position, value.ToString(), HorizontalAlignment.Center, 42, 14,
                heroDamage ? new Color(1, 0.35f, 0.3f, 1 - age * 0.7f) : new Color(1, 0.82f, 0.35f, 1 - age * 0.7f));
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
            DrawCharacterSprite(feetPosition, Math.Clamp(rig, 0, 4), (int)facing, maximumSize);
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

    private void DrawHeroEquipmentOverlay(Vector2 actor, P21Facing facing)
    {
        if (_session is null) return;
        if (_session.HeroEquipment.Items.TryGetValue(EquipmentSlot.MainHand, out ItemInstance? weapon))
        {
            Color color = P1UiText.RarityColor(weapon.Rarity);
            float side = facing == P21Facing.Left ? -1 : 1;
            DrawLine(actor + new Vector2(side * 7, -28), actor + new Vector2(side * 18, -44), color, 2);
        }
        if (_session.HeroEquipment.Items.ContainsKey(EquipmentSlot.OffHand))
        {
            float side = facing == P21Facing.Right ? -1 : 1;
            DrawArc(actor + new Vector2(side * 10, -23), 6, -.8f, 2.2f, 8, new Color("a7b0b7"), 2);
        }
        if (_session.HeroEquipment.Items.ContainsKey(EquipmentSlot.Helmet))
            DrawLine(actor + new Vector2(-5, -47), actor + new Vector2(5, -47), new Color("c0aa78"), 2);
        if (_session.HeroEquipment.Items.ContainsKey(EquipmentSlot.Chest))
            DrawRect(new Rect2(actor + new Vector2(-3, -32), new Vector2(6, 3)), new Color("8d6f52"), true);
    }

    private void DrawVfx(int index, Vector2 center, float size)
    {
        if (_vfxAtlas is null) return;
        Rect2 source = new((index % 8) * 64, (index / 8) * 64, 64, 64);
        DrawTextureRectRegion(_vfxAtlas, new Rect2(center - new Vector2(size / 2, size / 2), new Vector2(size, size)), source);
    }

    private static int VfxIndex(P3SceneEventKind kind) => kind switch
    {
        P3SceneEventKind.HeavyStrike => 0,
        P3SceneEventKind.EarthCleave => 2,
        P3SceneEventKind.Aftershock => 4,
        P3SceneEventKind.BloodTideSpin => 5,
        P3SceneEventKind.SpiritBlade => 7,
        P3SceneEventKind.Chain => 8,
        P3SceneEventKind.AshJavelin => 9,
        P3SceneEventKind.EmberNova => 10,
        P3SceneEventKind.StormBrand => 11,
        P3SceneEventKind.SeismicCharge => 14,
        P3SceneEventKind.WarCry => 16,
        P3SceneEventKind.Banner => 17,
        P3SceneEventKind.Guard => 18,
        P3SceneEventKind.Block => 19,
        P3SceneEventKind.Bleed => 22,
        P3SceneEventKind.Ailment => 24,
        P3SceneEventKind.Flask => 32,
        P3SceneEventKind.EnemyDefeated => 42,
        P3SceneEventKind.BossPhase => 40,
        _ => -1,
    };

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

    private void DrawSkillShapes(Vector2 actor, Vector2 enemy, IEnumerable<P3SceneEvent> recent)
    {
        foreach (P3SceneEvent item in recent)
        {
            float age = Math.Clamp((float)((_visualElapsedMilliseconds - item.AtMilliseconds) / 1_100), 0, 1);
            if (item.Kind == P3SceneEventKind.WarCry)
            {
                DrawArc(actor + new Vector2(0, -12), 18 + age * 42, 0, MathF.Tau, 28,
                    new Color(0.94f, 0.62f, 0.23f, 0.8f * (1 - age)), 3);
            }
            else if (item.Kind == P3SceneEventKind.Aftershock)
            {
                DrawArc(enemy, 12 + age * 55, 0, MathF.Tau, 24,
                    new Color(0.62f, 0.43f, 0.22f, 0.75f * (1 - age)), 5);
            }
            else if (item.Kind == P3SceneEventKind.Bleed)
            {
                for (int index = 0; index < 4; index++)
                {
                    DrawCircle(enemy + new Vector2(index * 6 - 9, -28 + age * 24 + index * 2), 2,
                        new Color(0.72f, 0.08f, 0.12f, 1 - age));
                }
            }
        }
    }

    private void DrawFloatingNumbers(Vector2 enemy, Vector2 actor, IEnumerable<P3SceneEvent> recent, long elapsed)
    {
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

    private void DrawAtlasActor(Vector2 position, bool hero, float attackCycle)
    {
        if (!hero && _mercenaryTexture is not null)
        {
            float height = 116;
            float width = height * _mercenaryTexture.GetWidth() / _mercenaryTexture.GetHeight();
            DrawTextureRect(_mercenaryTexture,
                new Rect2(position + new Vector2(-width / 2, -height), new Vector2(width, height)), false);
            return;
        }

        int column = attackCycle is > .18f and < .38f ? 2 : 0;
        float lean = attackCycle is > 0.18f and < 0.38f ? 6 : 0;
        DrawCharacterSprite(position + new Vector2(lean, 0), hero ? 0 : 1, column, new Vector2(92, 112));
    }

    private void DrawAtlasEnemy(Vector2 position, float mapProgress, float attackCycle)
    {
        float recoil = attackCycle is > 0.25f and < 0.45f ? 4 : 0;
        Texture2D? atlas = mapProgress > .82f ? _bossAtlas : _enemyAtlas;
        if (atlas is not null)
        {
            int index = Math.Clamp((int)(mapProgress * (mapProgress > .82f ? 10 : 24)), 0, mapProgress > .82f ? 9 : 23);
            DrawGridSprite(atlas, index, mapProgress > .82f ? 5 : 6, mapProgress > .82f ? 2 : 5,
                position + new Vector2(recoil, 0), mapProgress > .82f ? new Vector2(148, 152) : new Vector2(104, 116));
        }
        else DrawEnemy(position, attackCycle);
    }

    private void DrawCharacterSprite(Vector2 feetPosition, int row, int column, Vector2 maximumSize)
    {
        if (_characterAtlas is null) return;
        DrawGridSprite(_characterAtlas, row * 4 + column, 4, 5, feetPosition, maximumSize);
    }

    private void DrawGridSprite(Texture2D texture, int index, int columns, int rows, Vector2 feetPosition, Vector2 maximumSize)
    {
        Rect2 source = GridCell(texture, index, columns, rows);
        float scale = Math.Min(maximumSize.X / source.Size.X, maximumSize.Y / source.Size.Y);
        Vector2 size = source.Size * scale;
        DrawTextureRectRegion(texture, new Rect2(feetPosition + new Vector2(-size.X / 2, -size.Y), size), source);
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

    private Rect2 AtlasCell(int column, int row)
    {
        float[] starts = row == 0
            ? [0, 280, 575, 850, 1_190]
            : [0, 275, 595, 855, 1_145];
        float[] widths = row == 0
            ? [270, 270, 275, 335, 346]
            : [265, 310, 250, 280, 391];
        float scaleX = _characterAtlas!.GetWidth() / 1_536f;
        float height = _characterAtlas.GetHeight() / 2f;
        return new Rect2(starts[column] * scaleX, row * height, widths[column] * scaleX, height);
    }

    private void DrawAtlasSprite(Vector2 feetPosition, Rect2 source, Vector2 maximumSize)
    {
        float scale = Math.Min(maximumSize.X / source.Size.X, maximumSize.Y / source.Size.Y);
        Vector2 size = source.Size * scale;
        Rect2 destination = new(feetPosition + new Vector2(-size.X / 2, -size.Y), size);
        DrawTextureRectRegion(_characterAtlas!, destination, source);
    }

    private void DrawEnemy(Vector2 position, float attackCycle)
    {
        float recoil = attackCycle is > 0.25f and < 0.45f ? 4 : 0;
        Color flesh = new("8d4144");
        DrawCircle(position + new Vector2(recoil, -24), 10, flesh);
        DrawColoredPolygon(
        [
            position + new Vector2(-13 + recoil, -17),
            position + new Vector2(14 + recoil, -17),
            position + new Vector2(18, 10),
            position + new Vector2(-18, 10),
        ], flesh.Darkened(0.18f));
        DrawLine(position + new Vector2(-9, 8), position + new Vector2(-13, 22), new Color("332b2f"), 6);
        DrawLine(position + new Vector2(9, 8), position + new Vector2(13, 22), new Color("332b2f"), 6);
        DrawCircle(position + new Vector2(-4 + recoil, -26), 2, new Color("f07a62"));
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
}

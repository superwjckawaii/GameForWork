using GameForWork.Core.P1;
using GameForWork.Core.P1.World;
using Godot;

namespace GameForWork.GodotClient;

public enum P1ViewMode
{
    Town,
    Hero,
    Mercenaries,
}

public partial class P1WorldView : Control
{
    private P1GameSession? _session;
    private P1ViewMode _mode;
    private double _visualClock;
    private double _visualRemainingMilliseconds;
    private long _lastObservedRemaining = -1;
    private string? _lastObservedMap;
    private Texture2D? _townBackground;
    private Texture2D? _combatBackground;
    private Texture2D? _characterAtlas;

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
        _townBackground = LoadOptional("res://assets/p2/town/military-town.png");
        _combatBackground = LoadOptional("res://assets/p2/combat/gate-ruins.png");
        _characterAtlas = LoadOptional("res://assets/p2/characters/p2-character-grid.png");
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
        {
            return;
        }

        _visualClock += delta;
        SyncObservation();
        if (_mode != P1ViewMode.Town && _lastObservedMap is not null)
        {
            _visualRemainingMilliseconds = Math.Max(
                0,
                _visualRemainingMilliseconds - delta * 1_000 * (_session?.SimulationSpeed ?? 1));
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        Rect2 bounds = new(4, 4, Math.Max(100, Size.X - 8), Math.Max(80, Size.Y - 8));
        DrawRect(bounds, new Color("0c1017"), true);
        if (_mode == P1ViewMode.Town)
        {
            DrawTown(bounds);
        }
        else
        {
            DrawExpedition(bounds, _mode == P1ViewMode.Hero);
        }

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

    private void DrawExpedition(Rect2 bounds, bool hero)
    {
        if (_combatBackground is not null)
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

        P1TeamExpeditionState? team = Team(hero);
        float duration = team?.ActiveRoute == MapRoute.Abyss ? 120_000f : 90_000f;
        float mapProgress = team?.ActiveMap is null
            ? 0
            : 1f - Math.Clamp((float)(_visualRemainingMilliseconds / duration), 0, 1);
        float attackCycle = (float)(_visualClock * (hero ? 1.8 : 1.5) % 1.0);
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
            DrawAtlasEnemy(enemy, mapProgress, attackCycle);
        }

        if (team?.ActiveMap is not null && attackCycle is > 0.18f and < 0.38f)
        {
            float reach = (attackCycle - 0.18f) / 0.2f;
            Vector2 slashCenter = actor.Lerp(enemy, 0.45f + reach * 0.35f);
            DrawArc(slashCenter, 18 + reach * 18, -1.2f, 1.1f, 14, new Color(0.95f, 0.78f, 0.42f, 1 - reach * 0.4f), 4);
            DrawCircle(enemy + new Vector2(-6, -12), 6 * (1 - reach), new Color("a83838"));
        }

        DrawBar(new Rect2(bounds.Position + new Vector2(16, 35), new Vector2(bounds.Size.X - 32, 7)), mapProgress, new Color("bb8442"));
        string title = team?.ActiveMap is null
            ? "等待远征补给或地图"
            : $"{team.ActiveMap.InstanceId} · 区域 {team.ActiveMap.AreaLevel} · {team.ActiveRoute}";
        DrawCaption(bounds, title, new Color("e5d7be"));
        if (team?.ActiveMap is not null)
        {
            DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(16, 58), $"远征进度 {mapProgress * 100:0.0}%", HorizontalAlignment.Left, -1, 12, new Color("c6bca9"));
        }
    }

    private void SyncObservation()
    {
        P1TeamExpeditionState? team = Team(_mode == P1ViewMode.Hero);
        string? map = team?.ActiveMap?.InstanceId;
        long remaining = team?.RemainingMapTimeMilliseconds ?? 0;
        if (map != _lastObservedMap || remaining != _lastObservedRemaining)
        {
            _lastObservedMap = map;
            _lastObservedRemaining = remaining;
            _visualRemainingMilliseconds = remaining;
        }
    }

    private P1TeamExpeditionState? Team(bool hero) => _session is null
        ? null
        : hero ? _session.World.Hero : _session.World.Mercenaries;

    private void ResetObservation()
    {
        _lastObservedMap = null;
        _lastObservedRemaining = -1;
    }

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
        int column = hero && _session is not null
            ? ((int)_session.Player.Gender + (int)_session.Player.SkinTone + (int)_session.Player.HairStyle) % 4
            : 0;
        float lean = attackCycle is > 0.18f and < 0.38f ? 6 : 0;
        Rect2 source = AtlasCell(hero ? column : 4, 0);
        DrawAtlasSprite(position + new Vector2(lean, 0), source, new Vector2(92, 112));
    }

    private void DrawAtlasEnemy(Vector2 position, float mapProgress, float attackCycle)
    {
        float recoil = attackCycle is > 0.25f and < 0.45f ? 4 : 0;
        (int column, Vector2 maximumSize) = mapProgress switch
        {
            < 0.25f => (0, new Vector2(88, 112)),
            < 0.5f => (1, new Vector2(116, 94)),
            < 0.72f => (2, new Vector2(88, 112)),
            < 0.82f => (3, new Vector2(102, 122)),
            _ => (4, new Vector2(148, 152)),
        };
        DrawAtlasSprite(position + new Vector2(recoil, 0), AtlasCell(column, 1), maximumSize);
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

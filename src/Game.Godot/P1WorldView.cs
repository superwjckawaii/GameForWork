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
    public P1GameSession? Session { get; set; }
    public P1ViewMode Mode { get; set; }

    public override void _Draw()
    {
        Rect2 bounds = new(4, 4, Math.Max(100, Size.X - 8), Math.Max(80, Size.Y - 8));
        DrawRect(bounds, new Color("10141c"), true);
        DrawRect(bounds, new Color("74644f"), false, 2);
        if (Mode == P1ViewMode.Town)
        {
            DrawTown(bounds);
        }
        else
        {
            DrawExpedition(bounds, Mode == P1ViewMode.Hero);
        }
    }

    private void DrawTown(Rect2 bounds)
    {
        DrawRect(new Rect2(bounds.Position + new Vector2(18, 22), new Vector2(126, 70)), new Color("574638"), true);
        DrawRect(new Rect2(bounds.Position + new Vector2(166, 34), new Vector2(108, 58)), new Color("40505b"), true);
        DrawRect(new Rect2(bounds.Position + new Vector2(300, 18), new Vector2(92, 74)), new Color("4d3e48"), true);
        DrawCircle(bounds.Position + new Vector2(bounds.Size.X - 62, 62), 32, new Color("465f72"));
        DrawCircle(bounds.Position + new Vector2(bounds.Size.X - 62, 62), 22, new Color("111a28"));
        DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(24, 118), "工坊", HorizontalAlignment.Left, -1, 14, new Color("e4d6bd"));
        DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(172, 118), "仓库", HorizontalAlignment.Left, -1, 14, new Color("e4d6bd"));
        DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(306, 118), "指挥所", HorizontalAlignment.Left, -1, 14, new Color("e4d6bd"));
        DrawString(ThemeDB.FallbackFont, bounds.Position + new Vector2(bounds.Size.X - 98, 118), "古代门扉", HorizontalAlignment.Left, -1, 14, new Color("8fc5df"));
    }

    private void DrawExpedition(Rect2 bounds, bool hero)
    {
        for (int index = 0; index < 12; index++)
        {
            float x = bounds.Position.X + index * bounds.Size.X / 12;
            DrawLine(new Vector2(x, bounds.Position.Y), new Vector2(x, bounds.End.Y), new Color("202834"));
        }

        P1TeamExpeditionState? team = Session is null ? null : hero ? Session.World.Hero : Session.World.Mercenaries;
        float duration = team?.ActiveRoute == MapRoute.Abyss ? 120_000f : 90_000f;
        float progress = team?.ActiveMap is null
            ? 0
            : 1f - Math.Clamp(team.RemainingMapTimeMilliseconds / duration, 0, 1);
        Vector2 actor = bounds.Position + new Vector2(42 + progress * (bounds.Size.X - 100), bounds.Size.Y * 0.58f);
        DrawCircle(actor, 14, hero ? new Color("6db4d3") : new Color("b999d2"));
        DrawCircle(actor, 14, new Color("ead9b8"), false, 2);
        DrawCircle(bounds.Position + new Vector2(bounds.Size.X - 42, bounds.Size.Y * 0.58f), 18, new Color("a54f4d"));
        DrawString(
            ThemeDB.FallbackFont,
            bounds.Position + new Vector2(18, 26),
            team?.ActiveMap is null ? "等待远征" : $"{team.ActiveMap.InstanceId} · 区域 {team.ActiveMap.AreaLevel}",
            HorizontalAlignment.Left,
            -1,
            14,
            new Color("e4d6bd"));
    }
}

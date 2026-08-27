using GameForWork.Core.Combat;
using GameForWork.Core.Simulation;
using Godot;

namespace GameForWork.GodotClient;

public partial class ArenaView : Control
{
    public BattleState? State { get; set; }

    public override void _Draw()
    {
        Rect2 arena = new(18, 18, Math.Max(120, Size.X - 36), Math.Max(120, Size.Y - 36));
        DrawRect(arena, new Color("171b24"), true);
        DrawRect(arena, new Color("4d566b"), false, 2);

        for (int tile = 1; tile < 12; tile++)
        {
            float x = arena.Position.X + arena.Size.X * tile / 12f;
            float y = arena.Position.Y + arena.Size.Y * tile / 12f;
            DrawLine(new Vector2(x, arena.Position.Y), new Vector2(x, arena.End.Y), new Color(0.18f, 0.2f, 0.25f), 1);
            DrawLine(new Vector2(arena.Position.X, y), new Vector2(arena.End.X, y), new Color(0.18f, 0.2f, 0.25f), 1);
        }

        if (State is null)
        {
            return;
        }

        foreach (ActorState actor in State.Actors.Values)
        {
            float x = arena.Position.X + arena.Size.X * actor.XRaw / (12f * FixedPoint.Scale);
            float y = arena.Position.Y + arena.Size.Y * actor.YRaw / (12f * FixedPoint.Scale);
            Color color = actor.Team == Team.Hero ? new Color("5db7de") : new Color("db6a5e");
            if (!actor.IsAlive)
            {
                color = new Color("59606c");
            }

            DrawCircle(new Vector2(x, y), 15, color);
            DrawCircle(new Vector2(x, y), 15, new Color("e6d9bc"), false, 2);
            float lifeRatio = actor.MaxLife == 0 ? 0 : (float)actor.Life / actor.MaxLife;
            Rect2 healthBackground = new(x - 22, y - 27, 44, 6);
            DrawRect(healthBackground, new Color("2a1115"), true);
            DrawRect(new Rect2(healthBackground.Position, new Vector2(44 * lifeRatio, 6)), new Color("65bf73"), true);
        }
    }
}

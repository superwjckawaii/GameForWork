using GameForWork.Core.P1;
using GameForWork.Core.P10;
using Godot;

namespace GameForWork.GodotClient;

public partial class P10AtlasTreeView : Control
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Vector2 _pan;
    private bool _dragging;
    private Vector2 _press;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    { _session = session; _changed = changed; CustomMinimumSize = new Vector2(760, 390); MouseFilter = MouseFilterEnum.Stop; QueueRedraw(); }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("10151d"), true);
        Vector2 origin = Size / 2 + _pan;
        foreach (P10AtlasNode node in P10AtlasTree.Nodes)
        {
            Vector2 point = NodePosition(node, origin);
            if (node.PrerequisiteId is not null) DrawLine(NodePosition(P10AtlasTree.Get(node.PrerequisiteId), origin), point, new Color("354151"), 1);
            bool allocated = _session?.Invoke().Endgame.AtlasPassives.Contains(node.StableId) == true;
            bool available = node.PrerequisiteId is null || _session?.Invoke().Endgame.AtlasPassives.Contains(node.PrerequisiteId) == true;
            Color color = allocated ? new Color("c58b3c") : available ? new Color("477d79") : new Color("303641");
            DrawCircle(point, node.Notable ? 7 : 4, color);
            if (node.Notable) DrawCircle(point, 8, color.Lightened(.3f), false, 1.5f);
        }
        DrawString(ThemeDB.FallbackFont, new Vector2(10, 20), "异界星图 · 360 个功能节点 · 首次完成每个地图阶级获得 1 点", HorizontalAlignment.Left, -1, 12, new Color("d4c6a5"));
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed) { _dragging = false; _press = mouse.Position; }
            else if (!_dragging && Hit(mouse.Position) is { } node)
            {
                _changed?.Invoke(_session!().TryAllocateAtlasPassive(node.StableId) ? $"异界天赋已分配：{node.DisplayName}。" : "节点不可达或异界天赋点不足。");
                QueueRedraw();
            }
            AcceptEvent();
        }
        else if (inputEvent is InputEventMouseMotion motion && motion.ButtonMask.HasFlag(MouseButtonMask.Left))
        {
            if (!_dragging && motion.Position.DistanceTo(_press) > 7) _dragging = true;
            if (_dragging) { _pan += motion.Relative; QueueRedraw(); AcceptEvent(); }
        }
        else if (inputEvent is InputEventMouseMotion hover)
        {
            P10AtlasNode? node = Hit(hover.Position);
            TooltipText = node is null ? string.Empty : $"{node.DisplayName}\n{ThemeName(node.Theme)}收益提高 {node.RewardBasisPoints / 100.0:0.#}%";
        }
    }

    private P10AtlasNode? Hit(Vector2 screen) => P10AtlasTree.Nodes.Select(node => (node, distance: NodePosition(node, Size / 2 + _pan).DistanceTo(screen)))
        .Where(entry => entry.distance <= (entry.node.Notable ? 12 : 9)).OrderBy(entry => entry.distance).Select(entry => entry.node).FirstOrDefault();
    private static Vector2 NodePosition(P10AtlasNode node, Vector2 origin)
    { float angle = -MathF.PI / 2 + node.OrbitIndex / 30 * MathF.Tau / 12; float radius = 38 + node.OrbitIndex % 30 * 9.2f; return origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .72f) * radius; }
    private static string ThemeName(P10AtlasTheme theme) => theme switch
    { P10AtlasTheme.MapSupply => "地图续航", P10AtlasTheme.Abyss => "裂渊", P10AtlasTheme.LifeGarden => "命能花园", P10AtlasTheme.RedAltar => "赤誓祭坛", P10AtlasTheme.BlueAltar => "苍誓祭坛", _ => "攻坚" };
}

public partial class P10EndgamePanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Label? _summary;
    private P10AtlasTreeView? _atlas;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session; _changed = changed;
        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_summary);
        _atlas = new P10AtlasTreeView { SizeFlagsVertical = SizeFlags.ExpandFill };
        _atlas.Initialize(session, changed); AddChild(_atlas);
        var ascendancy = new HFlowContainer(); AddChild(ascendancy);
        foreach (P10AscendancyNode node in P10IronOathAscendancy.Nodes)
        {
            var button = new Button { Text = node.DisplayName, TooltipText = node.Effect, CustomMinimumSize = new Vector2(126, 38) };
            button.Pressed += () => { changed(session().TryAllocateAscendancyPassive(node.StableId) ? $"突破天赋已分配：{node.DisplayName}。" : "突破点不足或前置未分配。"); Refresh(true); };
            ascendancy.AddChild(button);
        }
        var boss = new Button { Text = "挑战终局：灰烬天垒", TooltipText = "消耗 1 枚天垒门票；首次击败获得突破点。" };
        boss.Pressed += () => { changed(session().TryChallengeCitadel() ? "灰烬天垒已排入主角远征；胜利后获得突破点。" : "主角队必须空闲，并持有由 8 枚 T11+ 碎片合成的门票。"); Refresh(true); };
        AddChild(boss);
    }

    public void Refresh(bool force = false)
    {
        if (_session is null) return;
        P10EndgameState state = _session().Endgame;
        string signature = $"{state.EarnedAtlasPoints}:{state.AtlasPassives.Count}:{state.LifeForce}:{state.RedFavor}:{state.BlueFavor}:{state.CitadelFragments}:{state.CitadelTickets}:{state.BreakthroughPoints}:{state.AscendancyPassives.Count}";
        if (!force && signature == _signature) return;
        _signature = signature;
        _summary!.Text = $"T1–T20 首次完成 {state.CompletedTiers.Count}/20 · 异界点 {state.AtlasPassives.Count}/{state.EarnedAtlasPoints} · " +
            $"命能 {state.LifeForce} · 赤誓 {state.RedFavor} · 苍誓 {state.BlueFavor}\n" +
            $"天垒碎片 {state.CitadelFragments}/{P10EndgameState.CitadelFragmentsPerTicket} · 门票 {state.CitadelTickets} · " +
            $"突破点 {state.AscendancyPassives.Count}/{state.BreakthroughPoints} · 每张地图出现 1–3 条收益路线";
        _atlas?.QueueRedraw();
    }
}

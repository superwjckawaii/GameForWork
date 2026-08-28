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
    private OptionButton? _schemes;
    private LineEdit? _schemeName;
    private Button? _breakthrough;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session; _changed = changed;
        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_summary);
        var schemeBar = new HFlowContainer(); AddChild(schemeBar);
        schemeBar.AddChild(new Label { Text = "异界方案" });
        _schemes = new OptionButton(); schemeBar.AddChild(_schemes);
        for (int index = 0; index < 3; index++) _schemes.AddItem($"方案 {index + 1}", index);
        _schemes.ItemSelected += index =>
        {
            if ((int)index == session().Endgame.ActiveAtlasSchemeIndex) return;
            changed(session().TrySwitchAtlasScheme((int)index) ? "已消耗 1 份记忆灰烬并切换异界方案。" : "记忆灰烬不足，无法切换方案。");
            Refresh(true);
        };
        _schemeName = new LineEdit { PlaceholderText = "方案名", CustomMinimumSize = new Vector2(120, 0), MaxLength = 12 };
        schemeBar.AddChild(_schemeName);
        var rename = new Button { Text = "重命名" }; schemeBar.AddChild(rename);
        rename.Pressed += () => { changed(session().TryRenameAtlasScheme(session().Endgame.ActiveAtlasSchemeIndex, _schemeName.Text) ? "异界方案已重命名。" : "请输入 1–12 个字符。"); Refresh(true); };
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
        _breakthrough = new Button { Text = "门扉突破试炼（P14 正式战斗）", Disabled = true,
            TooltipText = "P12 已建立门禁与存档状态；达到 100 级后，P14 的试炼胜利将开放 101–120 级和 T17–T20。" };
        AddChild(_breakthrough);
    }

    public void Refresh(bool force = false)
    {
        if (_session is null) return;
        P10EndgameState state = _session().Endgame;
        string signature = $"{state.EarnedAtlasPoints}:{state.AtlasPassives.Count}:{state.LifeForce}:{state.RedFavor}:{state.BlueFavor}:{state.CitadelFragments}:{state.CitadelTickets}:{state.BreakthroughPoints}:{state.AscendancyPassives.Count}:{state.ActiveAtlasSchemeIndex}:{state.FinalBreakthroughCompleted}:{_session().World.Economy.MemoryAshes}";
        if (!force && signature == _signature) return;
        _signature = signature;
        _summary!.Text = $"T1–T16 常规异界 · T17–T20 {(state.FinalBreakthroughCompleted ? "已开放" : "未开放")} · 首次完成 {state.CompletedTiers.Count}/20 · 异界点 {state.AtlasPassives.Count}/{state.EarnedAtlasPoints} · " +
            $"命能 {state.LifeForce} · 赤誓 {state.RedFavor} · 苍誓 {state.BlueFavor}\n" +
            $"天垒碎片 {state.CitadelFragments}/{P10EndgameState.CitadelFragmentsPerTicket} · 门票 {state.CitadelTickets} · " +
            $"突破点 {state.AscendancyPassives.Count}/{state.BreakthroughPoints} · 记忆灰烬 {_session().World.Economy.MemoryAshes} · 每张地图出现 1–3 条收益路线";
        if (_schemes is not null)
        {
            for (int index = 0; index < 3; index++) _schemes.SetItemText(index, state.AtlasSchemeNames[index]);
            _schemes.Select(state.ActiveAtlasSchemeIndex);
        }
        if (_schemeName is not null) _schemeName.Text = state.AtlasSchemeNames[state.ActiveAtlasSchemeIndex];
        if (_breakthrough is not null)
        {
            _breakthrough.Text = state.FinalBreakthroughCompleted ? "门扉突破已完成" :
                _session().World.Hero.Progression.Level >= 100 ? "门扉突破试炼（等待 P14 战斗）" : "门扉突破试炼（需要 100 级）";
        }
        _atlas?.QueueRedraw();
    }
}

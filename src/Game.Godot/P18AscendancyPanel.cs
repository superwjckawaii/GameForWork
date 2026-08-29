using GameForWork.Core.P1;
using GameForWork.Core.P18;
using Godot;

namespace GameForWork.GodotClient;

public partial class P18AscendancyPanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Label? _summary;
    private OptionButton? _path;
    private P18AscendancyTreeView? _tree;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        var bar = new HFlowContainer(); AddChild(bar);
        _summary = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        bar.AddChild(_summary);
        _path = new OptionButton();
        foreach (P18Ascendancy value in Enum.GetValues<P18Ascendancy>().Where(value => value != P18Ascendancy.None))
            _path.AddItem(P18AscendancyCatalog.DisplayName(value), (int)value);
        bar.AddChild(_path);
        var select = new Button { Text = "选择升华" }; bar.AddChild(select);
        select.Pressed += () =>
        {
            P18Ascendancy value = (P18Ascendancy)_path.GetItemId(_path.Selected);
            changed(session().TrySelectAscendancy(value) ? $"已选择升华：{P18AscendancyCatalog.DisplayName(value)}。" : "需要至少1点升华点，且已选路线只能通过重置更换。");
            Refresh();
        };
        var reset = new Button { Text = "重置节点（50000金币）" }; bar.AddChild(reset);
        reset.Pressed += () => { changed(session().TryResetAscendancy(false) ? "升华节点已全部重置。" : "没有可重置节点，或金币不足。"); Refresh(); };
        var change = new Button { Text = "更换路线（100000金币）" }; bar.AddChild(change);
        change.Pressed += () => { changed(session().TryResetAscendancy(true) ? "升华路线已清除，可以重新选择。" : "尚未选择路线，或金币不足。"); Refresh(); };

        _tree = new P18AscendancyTreeView { SizeFlagsVertical = SizeFlags.ExpandFill };
        _tree.Initialize(session, changed);
        AddChild(_tree);
        AddChild(new Label { Text = "左键分配 · 右键退还（强化点2000金币，核心点10000金币）· 每条路线只能中心→强化→核心", HorizontalAlignment = HorizontalAlignment.Center });
    }

    public void Refresh()
    {
        if (_session is null) return;
        var state = _session().Endgame;
        _summary!.Text = $"{P18AscendancyCatalog.DisplayName(state.SelectedAscendancy)} · 已用 {state.AscendancyPassives.Count}/{state.BreakthroughPoints}（上限8） · 金币 {_session().World.Economy.Gold}";
        _tree?.QueueRedraw();
    }
}

public partial class P18AscendancyTreeView : Control
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Vector2 _pan;
    private float _zoom = 1f;
    private bool _dragging;
    private Vector2 _press;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session; _changed = changed;
        CustomMinimumSize = new Vector2(720, 430); MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0d1118"));
        if (_session is null) return;
        P18Ascendancy selected = _session().Endgame.SelectedAscendancy;
        Vector2 origin = Size / 2 + _pan;
        DrawCircle(origin, 29 * _zoom, new Color("6b5434"));
        DrawString(ThemeDB.FallbackFont, origin + new Vector2(-42, 5),
            P18AscendancyCatalog.DisplayName(selected), HorizontalAlignment.Center, 84, 13, new Color("f0d394"));
        if (selected == P18Ascendancy.None)
        {
            DrawString(ThemeDB.FallbackFont, origin + new Vector2(-160, 65), "先在上方选择血征者、壁垒使或破阵者", HorizontalAlignment.Center, 320, 15, new Color("a8b0ba"));
            return;
        }
        foreach (P18AscendancyNode node in P18AscendancyCatalog.For(selected))
        {
            Vector2 point = Point(node, origin);
            Vector2 parent = node.PrerequisiteId is null ? origin : Point(P18AscendancyCatalog.Get(node.PrerequisiteId), origin);
            bool allocated = _session().Endgame.AscendancyPassives.Contains(node.StableId);
            bool available = node.PrerequisiteId is null || _session().Endgame.AscendancyPassives.Contains(node.PrerequisiteId);
            DrawLine(parent, point, allocated ? new Color("b57a37") : new Color("303b48"), allocated ? 3 : 2);
            float radius = (node.Kind == P18NodeKind.Core ? 24 : 16) * _zoom;
            DrawCircle(point, radius, allocated ? new Color("b56b2e") : available ? new Color("436b67") : new Color("29303a"));
            DrawCircle(point, radius, new Color("d2b47a"), false, 1.5f);
            DrawString(ThemeDB.FallbackFont, point + new Vector2(-65, radius + 15), node.DisplayName,
                HorizontalAlignment.Center, 130, 12, allocated ? new Color("ffd895") : new Color("c4c9ce"));
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (_session is null) return;
        if (inputEvent is InputEventMouseButton mouse && mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            if (mouse.Pressed) { _press = mouse.Position; _dragging = false; }
            else if (!_dragging && Hit(mouse.Position) is { } node)
            {
                bool success = mouse.ButtonIndex == MouseButton.Left
                    ? _session().TryAllocateAscendancyPassive(node.StableId)
                    : _session().TryRefundAscendancyPassive(node.StableId);
                _changed?.Invoke(success
                    ? $"升华节点已{(mouse.ButtonIndex == MouseButton.Left ? "分配" : "退还")}：{node.DisplayName}。"
                    : mouse.ButtonIndex == MouseButton.Left ? "前置未分配或升华点不足。" : "后续节点尚未退还，或金币不足。");
                QueueRedraw();
            }
            AcceptEvent();
        }
        else if (inputEvent is InputEventMouseMotion motion && motion.ButtonMask.HasFlag(MouseButtonMask.Left))
        {
            if (!_dragging && motion.Position.DistanceTo(_press) > 6) _dragging = true;
            if (_dragging) { _pan += motion.Relative; QueueRedraw(); AcceptEvent(); }
        }
        else if (inputEvent is InputEventMouseMotion hover)
        {
            P18AscendancyNode? node = Hit(hover.Position);
            TooltipText = node is null ? string.Empty : $"{node.DisplayName}\n{node.Effect}";
        }
        else if (inputEvent is InputEventMouseButton wheel && wheel.Pressed &&
                 wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            _zoom = Math.Clamp(_zoom + (wheel.ButtonIndex == MouseButton.WheelUp ? .1f : -.1f), .75f, 1.5f);
            QueueRedraw(); AcceptEvent();
        }
    }

    private P18AscendancyNode? Hit(Vector2 position)
    {
        if (_session is null || _session().Endgame.SelectedAscendancy == P18Ascendancy.None) return null;
        Vector2 origin = Size / 2 + _pan;
        return P18AscendancyCatalog.For(_session().Endgame.SelectedAscendancy)
            .Select(node => (node, distance: Point(node, origin).DistanceTo(position)))
            .Where(item => item.distance <= (item.node.Kind == P18NodeKind.Core ? 28 : 21) * _zoom)
            .OrderBy(item => item.distance).Select(item => item.node).FirstOrDefault();
    }

    private Vector2 Point(P18AscendancyNode node, Vector2 origin) =>
        origin + new Vector2(node.X, node.Y) * _zoom;
}

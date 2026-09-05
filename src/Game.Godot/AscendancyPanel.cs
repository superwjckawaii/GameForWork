using GameForWork.Core.Campaign;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Characters;
using GameForWork.Core.Presentation;
using Godot;

namespace GameForWork.GodotClient;

public partial class AscendancyPanel : Control
{
    private Func<GameSession>? _session;
    private Action<string>? _changed;
    private Label? _summary;
    private OptionButton? _path;
    private Button? _select;
    private BaseClass? _pathClass;
    private AscendancyTreeView? _tree;

    public void Initialize(Func<GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        ClipContents = true;
        _tree = new AscendancyTreeView { MouseFilter = MouseFilterEnum.Stop };
        _tree.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _tree.Initialize(session, changed);
        AddChild(_tree);

        var overlay = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10 };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(overlay);
        var top = new VBoxContainer();
        _summary = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        top.AddChild(_summary);
        var bar = new HFlowContainer();
        top.AddChild(bar);
        _path = new OptionButton();
        _path.Disabled = true;
        bar.AddChild(_path);
        _select = new Button { Text = "选择升华", Disabled = true }; bar.AddChild(_select);
        _select.Pressed += () =>
        {
            if (_path.Selected < 0 || _path.Selected >= _path.ItemCount) return;
            Ascendancy value = (Ascendancy)_path.GetItemId(_path.Selected);
            changed(session().TrySelectAscendancy(value) ? $"已选择升华：{WarriorAscendancyCatalog.DisplayName(value)}。" : "需要至少1点升华点，且已选路线只能通过重置更换。");
            Refresh();
        };
        var reset = new Button { Text = "重置节点（50000金币）" }; bar.AddChild(reset);
        reset.Pressed += () => { changed(session().TryResetAscendancy(false) ? "升华节点已全部重置。" : "没有可重置节点，或金币不足。"); Refresh(); };
        var change = new Button { Text = "更换路线（100000金币）" }; bar.AddChild(change);
        change.Pressed += () => { changed(session().TryResetAscendancy(true) ? "升华路线已清除，可以重新选择。" : "尚未选择路线，或金币不足。"); Refresh(); };
        overlay.AddChild(Hud(top));
        overlay.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        overlay.AddChild(Hud(new Label
        {
            Text = "左键分配 · 右键退还（强化点2000金币，核心点10000金币）· 每条路线只能中心→强化→核心",
            HorizontalAlignment = HorizontalAlignment.Center,
        }));
    }

    public void Refresh()
    {
        if (_session is null) return;
        GameSession current = _session();
        RefreshPaths(current);
        var state = current.Endgame;
        _summary!.Text = $"{WarriorAscendancyCatalog.DisplayName(state.SelectedAscendancy)} · 已用 {state.AscendancyPassives.Count}/{state.BreakthroughPoints}（上限8） · 金币 {current.World.Economy.Gold}";
        _tree?.QueueRedraw();
    }

    private void RefreshPaths(GameSession session)
    {
        if (_pathClass == session.Player.BaseClass) return;
        _pathClass = session.Player.BaseClass;
        _path!.Clear();
        Ascendancy[] available = ClassCatalog.Get(session.Player.BaseClass).Ascendancies
            .Where(WarriorAscendancyCatalog.IsImplemented).ToArray();
        foreach (Ascendancy value in available)
            _path.AddItem(WarriorAscendancyCatalog.DisplayName(value), (int)value);
        if (available.Length == 0)
            _path.AddItem("暂无可用升华", (int)Ascendancy.None);
        _path.Disabled = available.Length == 0;
        _select!.Disabled = available.Length == 0;
    }

    private static PanelContainer Hud(Control content)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0d1118e8"),
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6,
        });
        panel.AddChild(content);
        return panel;
    }
}

public partial class AscendancyTreeView : Control
{
    private Func<GameSession>? _session;
    private Action<string>? _changed;
    private Vector2 _pan;
    private float _zoom = .82f;
    private bool _dragging;
    private Vector2 _press;
    private Texture2D? _backdrop;
    private Ascendancy _backdropAscendancy;

    private static readonly string[] BackdropNames =
    [
        "blood-fighter", "iron-guardian", "warbreaker", "marksman", "shadowblade", "venomist",
        "soul-shepherd", "spirit-cantor", "hexbinder", "elementalist", "void-scholar", "aegis-mage",
        "martial-monk", "beast-keeper", "phantom-master", "runecarver", "spellarmor", "idol-forger",
    ];

    public void Initialize(Func<GameSession> session, Action<string> changed)
    {
        _session = session; _changed = changed;
        MouseFilter = MouseFilterEnum.Stop;
        Resized += () => { ClampView(); QueueRedraw(); };
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0d1118"));
        if (_session is null) return;
        Ascendancy selected = _session().Endgame.SelectedAscendancy;
        Vector2 origin = Size / 2 + _pan;
        Texture2D? backdrop = BackdropFor(selected);
        if (backdrop is not null)
        {
            const float extent = 240f;
            ProjectedSquare square = TreeProjection.BackdropSquare(origin.X, origin.Y, extent, _zoom);
            DrawTextureRect(backdrop, new Rect2(square.X, square.Y, square.Side, square.Side), false);
        }
        DrawCircle(origin, 29 * _zoom, new Color("6b5434"));
        DrawString(ThemeDB.FallbackFont, origin + new Vector2(-42, 5),
            WarriorAscendancyCatalog.DisplayName(selected), HorizontalAlignment.Center, 84, 13, new Color("f0d394"));
        if (selected == Ascendancy.None)
        {
            Ascendancy[] available = ClassCatalog.Get(_session().Player.BaseClass).Ascendancies
                .Where(WarriorAscendancyCatalog.IsImplemented).ToArray();
            string prompt = available.Length == 0
                ? "该职业暂未配置可用升华"
                : $"先在上方选择{string.Join('、', available.Select(WarriorAscendancyCatalog.DisplayName))}";
            DrawString(ThemeDB.FallbackFont, origin + new Vector2(-190, 65), prompt,
                HorizontalAlignment.Center, 380, 15, new Color("a8b0ba"));
            return;
        }
        foreach (AscendancyNode node in WarriorAscendancyCatalog.For(selected))
        {
            Vector2 point = Point(node, origin);
            Vector2 parent = node.PrerequisiteId is null ? origin : Point(WarriorAscendancyCatalog.Get(node.PrerequisiteId), origin);
            bool allocated = _session().Endgame.AscendancyPassives.Contains(node.StableId);
            bool available = node.PrerequisiteId is null || _session().Endgame.AscendancyPassives.Contains(node.PrerequisiteId);
            if (allocated)
            {
                DrawLine(parent, point, new Color("683719"), 6 * _zoom);
                DrawLine(parent, point, new Color("f1ad43"), 2.5f * _zoom);
            }
            float radius = (node.Kind == NodeKind.Core ? 24 : 16) * _zoom;
            DrawCircle(point, radius, allocated ? new Color("b56b2e") : available ? new Color("436b67") : new Color("29303a"));
            DrawCircle(point, radius, new Color("d2b47a"), false, 1.5f);
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
            if (_dragging) { _pan += motion.Relative; ClampView(); QueueRedraw(); AcceptEvent(); }
        }
        else if (inputEvent is InputEventMouseMotion hover)
        {
            AscendancyNode? node = Hit(hover.Position);
            TooltipText = node is null ? string.Empty : $"{node.DisplayName}\n{node.Effect}";
        }
        else if (inputEvent is InputEventMouseButton wheel && wheel.Pressed &&
                 wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            Vector2 origin = Size / 2 + _pan;
            Vector2 world = (wheel.Position - origin) / _zoom;
            _zoom = Math.Clamp(_zoom * (wheel.ButtonIndex == MouseButton.WheelUp ? 1.12f : .89f), .5f, 1.6f);
            _pan = wheel.Position - Size / 2 - world * _zoom;
            ClampView();
            QueueRedraw(); AcceptEvent();
        }
    }

    private void ClampView()
    {
        const float extent = 240f;
        float half = extent * _zoom;
        float limitX = Math.Max(0, half - Size.X / 2);
        float limitY = Math.Max(0, half - Size.Y / 2);
        _pan = new Vector2(Math.Clamp(_pan.X, -limitX, limitX), Math.Clamp(_pan.Y, -limitY, limitY));
    }

    private AscendancyNode? Hit(Vector2 position)
    {
        if (_session is null || _session().Endgame.SelectedAscendancy == Ascendancy.None) return null;
        Vector2 origin = Size / 2 + _pan;
        return WarriorAscendancyCatalog.For(_session().Endgame.SelectedAscendancy)
            .Select(node => (node, distance: Point(node, origin).DistanceTo(position)))
            .Where(item => item.distance <= (item.node.Kind == NodeKind.Core ? 28 : 21) * _zoom)
            .OrderBy(item => item.distance).Select(item => item.node).FirstOrDefault();
    }

    private Vector2 Point(AscendancyNode node, Vector2 origin)
    {
        ProjectedPoint point = TreeProjection.WorldToScreen(node.X, node.Y, origin.X, origin.Y, _zoom);
        return new Vector2(point.X, point.Y);
    }

    private Texture2D? BackdropFor(Ascendancy selected)
    {
        if (selected == Ascendancy.None) return null;
        if (_backdropAscendancy == selected) return _backdrop;
        int index = (int)selected - 1;
        if (index < 0 || index >= BackdropNames.Length) return null;
        string path = $"res://assets/presentation/trees/ascendancy/presentation-ascendancy-{index + 1:D2}-{BackdropNames[index]}.png";
        _backdrop = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        _backdropAscendancy = selected;
        return _backdrop;
    }
}

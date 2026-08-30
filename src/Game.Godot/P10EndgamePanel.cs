using GameForWork.Core.P1;
using GameForWork.Core.P10;
using GameForWork.Core.P14;
using GameForWork.Core.P1.World;
using GameForWork.Core.P18;
using Godot;

namespace GameForWork.GodotClient;

public partial class P10AtlasTreeView : Control
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Vector2 _pan;
    private float _zoom = .22f;
    private bool _dragging;
    private Vector2 _press;
    private Texture2D? _backdrop;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session; _changed = changed; MouseFilter = MouseFilterEnum.Stop;
        const string backdrop = "res://assets/p21/trees/p21-atlas-backdrop.png";
        if (ResourceLoader.Exists(backdrop)) _backdrop = GD.Load<Texture2D>(backdrop);
        Resized += () => { ClampView(); QueueRedraw(); };
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("10151d"), true);
        Vector2 origin = Size / 2 + _pan;
        if (_backdrop is not null)
        {
            float side = P10AtlasTree.LayoutExtent * 2 * _zoom;
            DrawTextureRect(_backdrop, new Rect2(origin - new Vector2(side, side) / 2, new Vector2(side, side)), false);
        }
        foreach (P10AtlasNode node in P10AtlasTree.Nodes)
        {
            Vector2 point = NodePosition(node, origin);
            bool allocated = _session?.Invoke().Endgame.AtlasPassives.Contains(node.StableId) == true;
            bool available = node.PrerequisiteId is null || _session?.Invoke().Endgame.AtlasPassives.Contains(node.PrerequisiteId) == true;
            Color color = allocated ? new Color("c58b3c") : available ? new Color("477d79") : new Color("303641");
            if (allocated && node.PrerequisiteId is not null)
            {
                Vector2 parent = NodePosition(P10AtlasTree.Get(node.PrerequisiteId), origin);
                DrawLine(parent, point, new Color("633c17"), 5);
                DrawLine(parent, point, new Color("e5a43d"), 2);
            }
            float nodeScale = Math.Clamp(_zoom * 2.4f, .45f, 1.3f);
            float radius = (node.Notable ? 10 : 5) * nodeScale;
            DrawCircle(point, radius, color);
            DrawCircle(point, radius + 1.5f, color.Lightened(.3f), false, 1.2f);
        }
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
            if (_dragging) { _pan += motion.Relative; ClampView(); QueueRedraw(); AcceptEvent(); }
        }
        else if (inputEvent is InputEventMouseMotion hover)
        {
            P10AtlasNode? node = Hit(hover.Position);
            TooltipText = node is null ? string.Empty : $"{node.DisplayName}\n{ThemeName(node.Theme)}收益提高 {node.RewardBasisPoints / 100.0:0.#}% · 出现权重 +{node.MechanicWeightBasisPoints / 100.0:0.#}%" +
                (string.IsNullOrEmpty(node.SpecialRule) ? string.Empty : $"\n规则：{node.SpecialRule}");
        }
        else if (inputEvent is InputEventMouseButton wheel && wheel.Pressed &&
                 wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            Vector2 origin = Size / 2 + _pan;
            Vector2 world = (wheel.Position - origin) / _zoom;
            _zoom = Math.Clamp(_zoom * (wheel.ButtonIndex == MouseButton.WheelUp ? 1.12f : .89f), .16f, 1.5f);
            _pan = wheel.Position - Size / 2 - world * _zoom;
            ClampView();
            QueueRedraw(); AcceptEvent();
        }
    }

    private void ClampView()
    {
        float half = P10AtlasTree.LayoutExtent * _zoom;
        float limitX = Math.Max(0, half - Size.X / 2);
        float limitY = Math.Max(0, half - Size.Y / 2);
        _pan = new Vector2(Math.Clamp(_pan.X, -limitX, limitX), Math.Clamp(_pan.Y, -limitY, limitY));
    }

    private P10AtlasNode? Hit(Vector2 screen) => P10AtlasTree.Nodes.Select(node => (node, distance: NodePosition(node, Size / 2 + _pan).DistanceTo(screen)))
        .Where(entry => entry.distance <= (entry.node.Notable ? 13 : 9) * Math.Clamp(_zoom * 2.4f, .55f, 1.3f))
        .OrderBy(entry => entry.distance).Select(entry => entry.node).FirstOrDefault();
    private Vector2 NodePosition(P10AtlasNode node, Vector2 origin) => origin + new Vector2(node.X, node.Y) * _zoom;
    private static string ThemeName(P10AtlasTheme theme) => theme switch
    { P10AtlasTheme.MapSupply => "地图续航", P10AtlasTheme.Abyss => "裂渊", P10AtlasTheme.LifeGarden => "命能花园", P10AtlasTheme.RedAltar => "赤誓祭坛", P10AtlasTheme.BlueAltar => "苍誓祭坛", _ => "攻坚" };
}

public partial class P10EndgamePanel : Control
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Label? _summary;
    private P10AtlasTreeView? _atlas;
    private OptionButton? _schemes;
    private LineEdit? _schemeName;
    private Button? _breakthrough;
    private Label? _preflight;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session; _changed = changed;
        ClipContents = true;
        _atlas = new P10AtlasTreeView { MouseFilter = MouseFilterEnum.Stop };
        _atlas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _atlas.Initialize(session, changed);
        AddChild(_atlas);

        var overlay = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10 };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(overlay);
        var top = new VBoxContainer();
        _summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        top.AddChild(_summary);
        var schemeBar = new HFlowContainer(); top.AddChild(schemeBar);
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
        overlay.AddChild(Hud(top));
        overlay.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        var bottom = new VBoxContainer();
        var bossActions = new HFlowContainer(); bottom.AddChild(bossActions);
        var boss = new Button { Text = "正式挑战：灰烬天垒", TooltipText = "消耗 1 枚天垒门票；正式模式只有一次战斗机会。" };
        boss.Pressed += () => { changed(session().TryChallengeCitadel() ? "灰烬天垒三阶段已排入主角远征。" : "主角队必须空闲，并持有由 8 枚 T11+ 碎片合成的门票。"); Refresh(true); };
        bossActions.AddChild(boss);
        var practice = new Button { Text = "天垒练习", TooltipText = "免费练习三阶段；不消耗门票，也不产生奖励。" };
        practice.Pressed += () => { changed(session().TryPracticeCitadel() ? "灰烬天垒练习已排入主角远征。" : "主角队必须空闲。"); Refresh(true); };
        bossActions.AddChild(practice);
        _breakthrough = new Button { Text = "门扉突破试炼", TooltipText = "达到 100 级后免费重复挑战；胜利开放 101–120 级和 T17–T20。" };
        _breakthrough.Pressed += () => { changed(session().TryChallengeFinalBreakthrough() ? "百级门扉试炼已排入主角远征。" : "需要 100 级、未完成突破且主角队空闲。"); Refresh(true); };
        bossActions.AddChild(_breakthrough);
        _preflight = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        bottom.AddChild(_preflight);
        overlay.AddChild(Hud(bottom));
    }

    private static PanelContainer Hud(Control content)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("10151de8"),
            ContentMarginLeft = 8,
            ContentMarginTop = 6,
            ContentMarginRight = 8,
            ContentMarginBottom = 6,
        });
        panel.AddChild(content);
        return panel;
    }

    public void Refresh(bool force = false)
    {
        if (_session is null) return;
        P10EndgameState state = _session().Endgame;
        string signature = $"{state.EarnedAtlasPoints}:{state.AtlasPassives.Count}:{state.LifeForce}:{state.RedFavor}:{state.BlueFavor}:{state.CitadelFragments}:{state.CitadelTickets}:{state.BreakthroughPoints}:{state.SelectedAscendancy}:{state.AscendancyPassives.Count}:{state.ActiveAtlasSchemeIndex}:{state.FinalBreakthroughCompleted}:{state.CitadelVictories}:{state.MythicReforgeMaterials}:{_session().World.Economy.MemoryAshes}";
        if (!force && signature == _signature) return;
        _signature = signature;
        _summary!.Text = $"T1–T16 常规异界 · T17–T20 {(state.FinalBreakthroughCompleted ? "已开放" : "未开放")} · 首次完成 {state.CompletedTiers.Count}/20 · 异界点 {state.AtlasPassives.Count}/{state.EarnedAtlasPoints} · " +
            $"命能 {state.LifeForce} · 赤誓 {state.RedFavor} · 苍誓 {state.BlueFavor}\n" +
            $"天垒碎片 {state.CitadelFragments}/{P10EndgameState.CitadelFragmentsPerTicket} · 门票 {state.CitadelTickets} · " +
            $"升华 {P18AscendancyCatalog.DisplayName(state.SelectedAscendancy)} · 升华点 {state.AscendancyPassives.Count}/{state.BreakthroughPoints} · 记忆灰烬 {_session().World.Economy.MemoryAshes} · 天垒胜利 {state.CitadelVictories} · 神话重铸 {state.MythicReforgeMaterials}";
        if (_schemes is not null)
        {
            for (int index = 0; index < 3; index++) _schemes.SetItemText(index, state.AtlasSchemeNames[index]);
            _schemes.Select(state.ActiveAtlasSchemeIndex);
        }
        if (_schemeName is not null) _schemeName.Text = state.AtlasSchemeNames[state.ActiveAtlasSchemeIndex];
        if (_breakthrough is not null)
        {
            _breakthrough.Text = state.FinalBreakthroughCompleted ? "门扉突破已完成" :
                _session().World.Hero.Progression.Level >= 100 ? "门扉突破试炼（可挑战）" : "门扉突破试炼（需要 100 级）";
            _breakthrough.Disabled = state.FinalBreakthroughCompleted || _session().World.Hero.Progression.Level < 100;
        }
        if (_preflight is not null)
        {
            P14BossDefinition boss = P14Bosses.CitadelStages[^1];
            P14PreflightReport report = P14Preflight.ForMap(new P1MapItem("preview-citadel", 20), boss);
            _preflight.Text = $"战前情报｜{report.EncounterName}｜伤害：{string.Join('、', report.DamageTypes)}｜风险 {report.RiskScore}\n" +
                              $"门槛：{string.Join('；', report.Requirements)}｜{report.EnrageCondition}";
        }
        _atlas?.QueueRedraw();
    }
}

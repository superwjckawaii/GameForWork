using GameForWork.Core.P1;
using GameForWork.Core.P10;
using GameForWork.Core.P14;
using GameForWork.Core.P1.World;
using GameForWork.Core.P18;
using GameForWork.Core.P26;
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

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session; _changed = changed; MouseFilter = MouseFilterEnum.Stop;
        Resized += () => { ClampView(); QueueRedraw(); };
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("10151d"), true);
        Vector2 origin = Size / 2 + _pan;
        for (int lane = 0; lane < 10; lane++)
        {
            float x = origin.X + (-630 + lane * 140) * _zoom;
            DrawRect(new Rect2(x - 48 * _zoom, origin.Y - 610 * _zoom, 96 * _zoom, 1_220 * _zoom),
                lane % 2 == 0 ? new Color("18202b80") : new Color("11192380"), true);
        }
        foreach (P10AtlasNode node in VisibleNodes())
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
                _changed?.Invoke(_session!().TryAllocateAtlasPassive(node.StableId)
                    ? $"已花费 {node.GoldCost:N0} 金币购买：{node.DisplayName}。"
                    : "无法购买：请检查金币、前置节点和 T 阶/突破门槛。");
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
            TooltipText = node is null ? string.Empty : $"{ThemeName(node.Theme)} · 第 {node.Position} 点 · {node.GoldCost:N0} 金币\n{node.DisplayName}\n{node.SpecialRule}\n解锁：{GateName(node.Gate)}";
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

    private IEnumerable<P10AtlasNode> VisibleNodes() => P10AtlasTree.Nodes.Where(node =>
        node.Theme != P10AtlasTheme.Warfront || _session?.Invoke().Endgame.WarfrontDiscovered == true);

    private P10AtlasNode? Hit(Vector2 screen) => VisibleNodes()
        .Select(node => (node, distance: NodePosition(node, Size / 2 + _pan).DistanceTo(screen)))
        .Where(entry => entry.distance <= (entry.node.Notable ? 13 : 9) * Math.Clamp(_zoom * 2.4f, .55f, 1.3f))
        .OrderBy(entry => entry.distance).Select(entry => entry.node).FirstOrDefault();
    private Vector2 NodePosition(P10AtlasNode node, Vector2 origin) => origin + new Vector2(node.X, node.Y) * _zoom;
    private static string ThemeName(P10AtlasTheme theme) => theme switch
    {
        P10AtlasTheme.MapBasics => "地图基础", P10AtlasTheme.MapSupply => "地图续航", P10AtlasTheme.Crafting => "地图打造",
        P10AtlasTheme.PacksAndElites => "怪群精英", P10AtlasTheme.Boss => "Boss攻坚", P10AtlasTheme.Abyss => "深渊",
        P10AtlasTheme.LifeGarden => "命能花园", P10AtlasTheme.RedAltar => "赤誓祭坛",
        P10AtlasTheme.BlueAltar => "苍誓祭坛", _ => "战阵前线"
    };
    private static string GateName(P26AtlasGate gate) => gate switch
    {
        P26AtlasGate.Act5 => "第五幕", P26AtlasGate.Tier5 => "完成 T5", P26AtlasGate.Tier10 => "完成 T10",
        P26AtlasGate.Tier16 => "完成 T16", P26AtlasGate.FinalBreakthrough => "最终突破", _ => "完成 T20"
    };
}

public partial class P10EndgamePanel : Control
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Label? _summary;
    private P10AtlasTreeView? _atlas;
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
        foreach (var entry in new[] { (GameForWork.Core.P28.P28RewardPreference.Weapons, "武器"),
            (GameForWork.Core.P28.P28RewardPreference.Armor, "护甲"), (GameForWork.Core.P28.P28RewardPreference.Jewelry, "饰品"),
            (GameForWork.Core.P28.P28RewardPreference.Materials, "材料") })
        {
            var supply = new Button { Text = $"兑换{entry.Item2}军需", TooltipText = "发现亡旗战阵后可用战功重复兑换；声望0/15/60解锁1/2/3阶，费用50/100/150战功。满仓放入回收。" };
            supply.Pressed += () => { changed(session().TryExchangeWarfrontSupply(entry.Item1) ? "军需已兑换。" : "尚未发现战阵或战功不足。"); Refresh(true); };
            bossActions.AddChild(supply);
        }
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
        string signature = $"{state.AtlasPassives.Count}:{state.LifeForce}:{state.RedFavor}:{state.BlueFavor}:{state.WarfrontDiscovered}:{state.WarfrontMerit}:{state.WarfrontReputation}:{state.CitadelFragments}:{state.CitadelTickets}:{state.BreakthroughPoints}:{state.SelectedAscendancy}:{state.AscendancyPassives.Count}:{state.FinalBreakthroughCompleted}:{state.CitadelVictories}:{state.MythicReforgeMaterials}:{_session().World.Economy.Gold}";
        if (!force && signature == _signature) return;
        _signature = signature;
        _summary!.Text = $"T1–T16 常规异界 · T17–T20 {(state.FinalBreakthroughCompleted ? "已开放" : "未开放")} · 首次完成 {state.CompletedTiers.Count}/20 · 异界天赋 {state.AtlasPassives.Count}/120 · 金币 {_session().World.Economy.Gold:N0} · " +
            $"命能 {state.LifeForce} · 赤誓 {state.RedFavor} · 苍誓 {state.BlueFavor} · " +
            $"亡旗 {(state.WarfrontDiscovered ? $"战功 {state.WarfrontMerit} / 声望 {state.WarfrontReputation}" : "未发现")}\n" +
            $"天垒碎片 {state.CitadelFragments}/{P10EndgameState.CitadelFragmentsPerTicket} · 门票 {state.CitadelTickets} · " +
            $"升华 {P18AscendancyCatalog.DisplayName(state.SelectedAscendancy)} · 升华点 {state.AscendancyPassives.Count}/{state.BreakthroughPoints} · 天垒胜利 {state.CitadelVictories} · 神话重铸 {state.MythicReforgeMaterials}";
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

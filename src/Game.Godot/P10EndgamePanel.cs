using GameForWork.Core.P1;
using GameForWork.Core.P10;
using GameForWork.Core.P14;
using GameForWork.Core.P1.World;
using GameForWork.Core.P18;
using GameForWork.Core.P26;
using GameForWork.Core.P31;
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
        const string backdropPath = "res://assets/p31/trees/p31-atlas-backdrop.png";
        _backdrop = ResourceLoader.Exists(backdropPath) ? GD.Load<Texture2D>(backdropPath) : null;
        Resized += () => { ClampView(); QueueRedraw(); };
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("10151d"), true);
        Vector2 origin = Size / 2 + _pan;
        if (_backdrop is not null)
        {
            P31ProjectedSquare square = P31TreeProjection.BackdropSquare(origin.X, origin.Y,
                P10AtlasTree.LayoutExtent, _zoom);
            DrawTextureRect(_backdrop, new Rect2(square.X, square.Y, square.Side, square.Side), false);
        }
        else for (int lane = 0; lane < 10; lane++)
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
    private Vector2 NodePosition(P10AtlasNode node, Vector2 origin)
    {
        P31ProjectedPoint point = P31TreeProjection.WorldToScreen(node.X, node.Y, origin.X, origin.Y, _zoom);
        return new Vector2(point.X, point.Y);
    }
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
        bossActions.AddChild(new Label { Text = "Boss 挑战已统一移动到：远征 → 主角派遣。" });
        for (int tier = 1; tier <= 3; tier++)
        {
            int selectedTier = tier;
            var supply = new Button { Text = $"兑换 T{tier} 战功基底", TooltipText = "从该阶戒指/项链/腰带共6种强力基底中等概率获取；同一种底材不会连续出现。费用50/100/150战功。" };
            supply.Pressed += () => { changed(session().TryExchangeWarfrontSupply(selectedTier) ? $"T{selectedTier} 战功基底已入仓。" : "该军需阶级未解锁或战功不足。"); Refresh(true); };
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

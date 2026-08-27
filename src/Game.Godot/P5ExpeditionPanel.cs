using GameForWork.Core.P1;
using GameForWork.Core.P1.World;
using GameForWork.Core.P5;
using Godot;

namespace GameForWork.GodotClient;

public partial class P5ExpeditionPanel : VBoxContainer
{
    private readonly Dictionary<ExpeditionTeamKind, TeamControls> _teams = [];
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Label? _resources;
    private VBoxContainer? _mapInventory;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        _resources = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_resources);

        var body = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 14);
        AddChild(body);

        var warehouse = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(235, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        warehouse.AddChild(new Label { Text = "目标仓库" });
        _mapInventory = new VBoxContainer();
        warehouse.AddChild(_mapInventory);
        body.AddChild(Frame(warehouse));

        var dispatches = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        dispatches.AddChild(BuildTeamCard(ExpeditionTeamKind.Hero, "主角"));
        dispatches.AddChild(BuildTeamCard(ExpeditionTeamKind.Mercenaries, "佣兵队"));
        var help = new Label
        {
            Text = "选择队伍目标后开始即可。补给自动消耗；地图耗尽、连续失败 3 次或缺少 Boss 门票时会明确停止。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        dispatches.AddChild(help);
        body.AddChild(dispatches);
    }

    public void RefreshState()
    {
        if (_session is null || _resources is null || _mapInventory is null)
        {
            return;
        }

        P1GameSession session = _session();
        string signature = $"{session.World.Economy.ExpeditionSupplies}|{session.World.Expedition.AbyssWardenFragments}|" +
            $"{session.World.Expedition.AbyssWardenTickets}|{session.World.Expedition.MapsTowardNextFragment}|" +
            string.Join(',', session.World.MapInventory.OrderBy(map => map.InstanceId).Select(map => $"{map.InstanceId}:{map.AreaLevel}")) + "|" +
            string.Join('|', session.World.Teams.Select(TeamSignature)) + "|" +
            string.Join('|', session.World.Expedition.Dispatches.Values.OrderBy(item => item.Team));
        if (signature == _signature)
        {
            return;
        }

        _signature = signature;
        _resources.Text =
            $"地图 {session.World.MapInventory.Count}　深渊监守者碎片 {session.World.Expedition.AbyssWardenFragments}/{P5ExpeditionDirector.FragmentsPerTicket}　" +
            $"Boss 门票 {session.World.Expedition.AbyssWardenTickets}　远征补给 {session.World.Economy.ExpeditionSupplies}";
        Clear(_mapInventory);
        for (int tier = P1MapItem.MinimumAreaLevel; tier <= P1MapItem.MaximumAreaLevel; tier++)
        {
            int count = session.World.MapInventory.Count(map => map.AreaLevel == tier);
            if (count > 0)
            {
                _mapInventory.AddChild(new Label { Text = $"T{tier} 地图　×{count}" });
            }
        }

        if (session.World.MapInventory.Count == 0)
        {
            _mapInventory.AddChild(new Label { Text = "地图仓库为空" });
        }

        _mapInventory.AddChild(new HSeparator());
        _mapInventory.AddChild(new Label
        {
            Text = $"碎片进度：地图 Boss {session.World.Expedition.MapsTowardNextFragment}/{P5ExpeditionDirector.MapsPerFragment}\n" +
                   $"深渊监守者碎片 {session.World.Expedition.AbyssWardenFragments}/{P5ExpeditionDirector.FragmentsPerTicket}\n" +
                   $"完整门票 ×{session.World.Expedition.AbyssWardenTickets}",
        });

        foreach ((ExpeditionTeamKind kind, TeamControls controls) in _teams)
        {
            P1TeamExpeditionState team = kind == ExpeditionTeamKind.Hero ? session.World.Hero : session.World.Mercenaries;
            P5TeamDispatchSnapshot? dispatch = session.World.Expedition.Get(kind);
            if (dispatch is not null)
            {
                controls.Target.Select(controls.Target.GetItemIndex((int)dispatch.Target));
                controls.Mode.Select(controls.Mode.GetItemIndex((int)dispatch.Mode));
                controls.Mode.Disabled = IsBossTarget(dispatch.Target);
            }
            controls.Status.Text = TeamStatus(team, dispatch);
        }
    }

    private Control BuildTeamCard(ExpeditionTeamKind kind, string title)
    {
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(new Label { Text = title });
        var selectors = new HFlowContainer();
        content.AddChild(selectors);
        var target = new OptionButton();
        target.AddItem("安全探索", (int)P5ExpeditionTarget.SafeMaps);
        target.AddItem("裂渊追猎", (int)P5ExpeditionTarget.AbyssMaps);
        target.AddItem("最高阶推进", (int)P5ExpeditionTarget.HighestTierMaps);
        target.AddItem("深渊监守者", (int)P5ExpeditionTarget.AbyssWarden);
        target.AddItem("Boss 练习", (int)P5ExpeditionTarget.AbyssWardenPractice);
        selectors.AddChild(target);
        var mode = new OptionButton();
        mode.AddItem("执行一次", (int)P5DispatchMode.Once);
        mode.AddItem("重复同类", (int)P5DispatchMode.Repeat);
        mode.AddItem("最高阶持续推进", (int)P5DispatchMode.HighestAvailable);
        selectors.AddChild(mode);
        target.ItemSelected += index =>
        {
            P5ExpeditionTarget selected = (P5ExpeditionTarget)target.GetItemId((int)index);
            mode.Disabled = IsBossTarget(selected);
            if (mode.Disabled)
            {
                mode.Select(mode.GetItemIndex((int)P5DispatchMode.Once));
            }
        };
        var start = new Button { Text = "开始派遣" };
        start.Pressed += () =>
        {
            P5ExpeditionTarget selectedTarget = (P5ExpeditionTarget)target.GetItemId(target.Selected);
            P5DispatchMode selectedMode = (P5DispatchMode)mode.GetItemId(mode.Selected);
            _session!().AssignExpedition(kind, selectedTarget, selectedMode);
            _signature = string.Empty;
            _changed?.Invoke($"{title}已派往{TargetName(selectedTarget)}。");
            RefreshState();
        };
        selectors.AddChild(start);
        var stop = new Button { Text = "停止" };
        stop.Pressed += () =>
        {
            _session!().CancelExpedition(kind);
            _signature = string.Empty;
            _changed?.Invoke($"{title}已停止派遣。");
            RefreshState();
        };
        selectors.AddChild(stop);
        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(status);
        _teams.Add(kind, new TeamControls(target, mode, status));
        return Frame(content);
    }

    private static string TeamStatus(P1TeamExpeditionState team, P5TeamDispatchSnapshot? dispatch)
    {
        if (team.ActiveMap is not null)
        {
            string target = P5ExpeditionDirector.IsPractice(team.ActiveMap) ? "Boss 练习" :
                P5ExpeditionDirector.IsBoss(team.ActiveMap) ? "深渊监守者" : $"T{team.ActiveMap.AreaLevel} 地图";
            return $"执行中：{target} · 剩余约 {Math.Max(1, team.RemainingMapTimeMilliseconds / 1_000)} 秒\n" +
                   $"路线 {team.ActiveRoute} · 最近 {(team.LastRun?.Succeeded == true ? "成功" : team.LastRun is null ? "暂无结算" : "失败")}";
        }

        if (team.Queue.Count > 0)
        {
            P1MapItem map = team.Queue.Maps[0];
            return $"准备中：{TargetName(dispatch?.Target ?? P5ExpeditionTarget.SafeMaps)} · T{map.AreaLevel}";
        }

        if (team.IsStopped)
        {
            return $"已停止：{StopReason(team.StopReason)}\n完成 {team.MapsCompleted} · 失败 {team.MapsFailed}";
        }

        return dispatch is null ? "空闲：请选择目标。" :
            $"{(dispatch.Enabled ? "等待执行" : "已完成本次派遣")}：{TargetName(dispatch.Target)}\n完成 {team.MapsCompleted} · 失败 {team.MapsFailed}";
    }

    private static string TeamSignature(P1TeamExpeditionState team) =>
        $"{team.Kind}:{team.ActiveMap?.InstanceId}:{team.Queue.Count}:{team.IsStopped}:{team.StopReason}:" +
        $"{team.MapsCompleted}:{team.MapsFailed}:{team.RemainingMapTimeMilliseconds / 1_000}";

    private static string TargetName(P5ExpeditionTarget target) => target switch
    {
        P5ExpeditionTarget.SafeMaps => "安全探索",
        P5ExpeditionTarget.AbyssMaps => "裂渊追猎",
        P5ExpeditionTarget.HighestTierMaps => "最高阶推进",
        P5ExpeditionTarget.AbyssWarden => "深渊监守者",
        P5ExpeditionTarget.AbyssWardenPractice => "Boss 练习",
        _ => "未知目标",
    };

    private static bool IsBossTarget(P5ExpeditionTarget target) =>
        target is P5ExpeditionTarget.AbyssWarden or P5ExpeditionTarget.AbyssWardenPractice;

    private static string StopReason(string reason) => reason switch
    {
        "maps_exhausted" => "没有可用地图",
        "boss_ticket_missing" => "缺少 Boss 门票",
        "consecutive_failures" => "连续失败达到 3 次",
        "storage_full" => "仓库已满",
        "manual_stop" => "玩家手动停止",
        "cancelled" => "玩家取消派遣",
        _ when string.IsNullOrWhiteSpace(reason) => "等待重新派遣",
        _ => reason,
    };

    private static PanelContainer Frame(Control content)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("151a22"),
            BorderColor = new Color("786747"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8,
        });
        panel.AddChild(content);
        return panel;
    }

    private static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            child.QueueFree();
        }
    }

    private sealed record TeamControls(OptionButton Target, OptionButton Mode, Label Status);
}

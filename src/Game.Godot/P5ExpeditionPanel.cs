using GameForWork.Core.P1;
using GameForWork.Core.P1.World;
using GameForWork.Core.P5;
using GameForWork.Core.P6;
using GameForWork.Core.P10;
using GameForWork.Core.P12;
using GameForWork.Core.P26;
using Godot;

namespace GameForWork.GodotClient;

public partial class P5ExpeditionPanel : VBoxContainer
{
    public event Action? ReportsViewed;
    private readonly Dictionary<ExpeditionTeamKind, TeamControls> _teams = [];
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Label? _resources;
    private GridContainer? _mapInventory;
    private VBoxContainer? _reports;
    private P26MapFilterWindow? _mapFilterWindow;
    private P12MapCraftWindow? _mapCraftWindow;
    private ConfirmationDialog? _abandonDialog;
    private ConfirmationDialog? _switchDialog;
    private Action? _pendingSwitchAction;
    private ExpeditionTeamKind _pendingAbandonTeam;
    private readonly Dictionary<MapFilterScope, Label> _filterCounts = [];
    private string _mapSignature = string.Empty;
    private string _reportSignature = string.Empty;
    private string _dispatchSignature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        _session = session;
        _changed = changed;
        _mapFilterWindow = new P26MapFilterWindow();
        _mapFilterWindow.Initialize();
        AddChild(_mapFilterWindow);
        _mapCraftWindow = new P12MapCraftWindow();
        _mapCraftWindow.Initialize();
        AddChild(_mapCraftWindow);
        _abandonDialog = new ConfirmationDialog
        {
            Title = "放弃当前远征",
            DialogText = "当前地图会被永久消耗，且不会获得任何结算奖励。确定放弃并停止吗？",
            OkButtonText = "放弃并停止",
            CancelButtonText = "返回",
            Exclusive = true,
        };
        _abandonDialog.Confirmed += ConfirmAbandon;
        AddChild(_abandonDialog);
        _switchDialog = new ConfirmationDialog
        {
            Title = "切换主角任务",
            OkButtonText = "放弃并切换",
            CancelButtonText = "取消",
            Exclusive = true,
        };
        _switchDialog.Confirmed += () =>
        {
            _session!().AbandonExpedition(ExpeditionTeamKind.Hero);
            Action? action = _pendingSwitchAction;
            _pendingSwitchAction = null;
            action?.Invoke();
        };
        _switchDialog.Canceled += () => _pendingSwitchAction = null;
        AddChild(_switchDialog);
        _resources = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_resources);

        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        AddChild(tabs);

        var dispatches = new VBoxContainer { Name = "派遣", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var teamTabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        teamTabs.AddChild(BuildTeamCard(ExpeditionTeamKind.Hero, "主角派遣"));
        teamTabs.AddChild(BuildTeamCard(ExpeditionTeamKind.Mercenaries, "佣兵派遣"));
        dispatches.AddChild(teamTabs);
        dispatches.AddChild(new Label
        {
            Text = "两支队伍分别保存地图筛选与玩法设置；无匹配地图、连续失败 3 次时自动停止。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        tabs.AddChild(dispatches);

        var warehouse = new VBoxContainer { Name = "地图仓与制图", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var craftToolbar = new HFlowContainer();
        craftToolbar.AddChild(BuildMapFilterButton(MapFilterScope.Crafting, "地图筛选"));
        var craftSettings = new Button { Text = "做图目标设置" };
        craftSettings.Pressed += () => _mapCraftWindow!.Open(_session!().World.MapCraftRule, rule =>
        {
            _session!().World.MapCraftRule = rule.Validate();
            _changed?.Invoke("做图目标已保存。");
        });
        craftToolbar.AddChild(craftSettings);
        var batch = new Button { Text = "执行批量制图" };
        batch.Pressed += () =>
        {
            P12MapBatchResult result = _session!().BatchCraftMaps(_session!().World.MapCraftRule, _session!().World.MapCraftFilter);
            _mapSignature = string.Empty; _changed?.Invoke(result.Summary); RefreshState();
        };
        craftToolbar.AddChild(batch);
        var sell = new Button { Text = "出售筛选结果", TooltipText = "锁定、运行中、手动优先队列和任务地图不会出售。" };
        sell.Pressed += () =>
        {
            (int sold, int gold) = _session!().SellMaps(_session!().World.MapCraftFilter);
            _mapSignature = string.Empty;
            _changed?.Invoke($"已出售 {sold} 张地图，获得 {gold} 金币。"); RefreshState();
        };
        craftToolbar.AddChild(sell);
        warehouse.AddChild(craftToolbar);
        var mapScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 230), SizeFlagsVertical = SizeFlags.ExpandFill };
        _mapInventory = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mapScroll.AddChild(_mapInventory); warehouse.AddChild(mapScroll);
        warehouse.AddChild(new Label
        {
            Text = "地图按 T 级与区域三列汇总。批量做图、出售与两队远征分别使用各自的地图筛选。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        tabs.AddChild(warehouse);

        var reportsPage = new ScrollContainer { Name = "战斗报告", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _reports = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reportsPage.AddChild(_reports);
        tabs.AddChild(reportsPage);
        tabs.TabChanged += index =>
        {
            if (tabs.GetTabControl((int)index) != reportsPage) return;
            _reportSignature = string.Empty;
            ReportsViewed?.Invoke();
            RefreshState();
        };
    }

    public void RefreshState()
    {
        if (_session is null || _resources is null || _mapInventory is null || _reports is null)
        {
            return;
        }

        P1GameSession session = _session();
        int formalMapCount = Math.Min(200, session.World.MapInventory.Count);
        for (int index = 0; index < formalMapCount; index++)
        {
            P1MapItem source = session.World.MapInventory[index];
            P1MapItem formal = source.EnsureFormal(session.Seed ^ (ulong)index);
            if (!ReferenceEquals(source, formal)) session.World.MapInventory[index] = formal;
        }
        var mapGroups = session.World.MapInventory
            .Select((map, index) => (Map: map, Index: index))
            .GroupBy(entry => (entry.Map.Tier, entry.Map.AreaId))
            .OrderByDescending(group => group.Key.Tier)
            .ThenBy(group => group.Key.AreaId, StringComparer.Ordinal)
            .ToArray();
        _resources.Text =
            $"地图 {session.World.MapInventory.Count}　深渊监守者碎片 {session.World.Expedition.AbyssWardenFragments}/{P5ExpeditionDirector.FragmentsPerTicket}　" +
            $"Boss 门票 {session.World.Expedition.AbyssWardenTickets}";
        string mapSignature = $"{session.World.Expedition.AbyssWardenFragments}:{session.World.Expedition.AbyssWardenTickets}:" +
            $"{session.World.Expedition.MapsTowardNextFragment}|" +
            string.Join(',', session.World.MapInventory.Select(map => $"{map.InstanceId}:{map.Tier}:{map.Rarity}:{map.Quality}:{map.CorruptionRule}:{map.SelectedRoute}:{string.Join('-', map.EffectiveAffixes.Select(affix => affix.Kind))}"));
        if (mapSignature != _mapSignature)
        {
            _mapSignature = mapSignature;
            Clear(_mapInventory);
            foreach (var group in mapGroups)
            {
                var entries = group.ToArray();
                var representative = entries
                    .OrderByDescending(entry => entry.Map.IsCorrupted)
                    .ThenByDescending(entry => entry.Map.Rarity)
                    .ThenByDescending(entry => entry.Map.ItemQuantityBonusBasisPoints)
                    .ThenByDescending(entry => entry.Map.MonsterQuantityBasisPoints)
                    .ThenByDescending(entry => entry.Map.Quality)
                    .ThenBy(entry => entry.Map.AcquiredSequence)
                    .First();
                P1MapItem map = representative.Map;
                P12MapArea area = ResolveArea(map);
                int rare = entries.Count(entry => entry.Map.Rarity == P12MapRarity.Rare);
                int magic = entries.Count(entry => entry.Map.Rarity == P12MapRarity.Magic);
                int corrupted = entries.Count(entry => entry.Map.IsCorrupted);
                int locked = entries.Count(entry => entry.Map.IsLocked);
                var card = new PanelContainer
                {
                    CustomMinimumSize = new Vector2(0, 52),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                };
                card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
                {
                    BgColor = new Color("151a22"), BorderColor = new Color("4b5665"),
                    BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                    ContentMarginLeft = 8, ContentMarginTop = 5, ContentMarginRight = 8, ContentMarginBottom = 5,
                });
                card.AddChild(new Label
                {
                    Text = $"T{map.Tier} · {area.DisplayName}　×{entries.Length}\n稀有 {rare} · 魔法 {magic}" +
                           (corrupted > 0 ? $" · 腐化 {corrupted}" : string.Empty) +
                           (locked > 0 ? $" · 锁定 {locked}" : string.Empty),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                });
                _mapInventory.AddChild(card);
            }
            if (session.World.MapInventory.Count == 0) _mapInventory.AddChild(new Label { Text = "地图仓库为空" });
        }

        string reportSignature = string.Join('|', session.World.Expedition.Reports.Select(report => report.StableId));
        if (_reports.Visible && reportSignature != _reportSignature)
        {
            _reportSignature = reportSignature;
            Clear(_reports);
            foreach (P6CombatReport report in session.World.Expedition.Reports.Reverse())
            {
                var card = new VBoxContainer();
                card.AddChild(new Label { Text = $"{report.Context} · {report.Outcome} · {report.DurationMilliseconds / 1_000.0:0.0}s" + (report.Offline ? " · 离线" : string.Empty) });
                string skills = report.Skills.Count == 0 ? "无有效输出" : string.Join(" · ", report.Skills.Take(6).Select(skill => $"{skill.Skill} {skill.Damage}({skill.DamageBasisPoints / 100.0:0.#}%)/{skill.Uses}次"));
                string sources = report.DamageSources.Count == 0 ? "无承伤" : string.Join(" · ", report.DamageSources.Take(4).Select(source => $"{source.Source} {source.Damage}({source.DamageBasisPoints / 100.0:0.#}%)"));
                string supports = report.Supports.Count == 0 ? "无可归因辅助触发" : string.Join(" · ", report.Supports.Take(6).Select(support => $"{support.Support} {support.Triggers}次/贡献约{support.EstimatedDamageContribution:+#;-#;0}"));
                card.AddChild(new Label
                {
                    Text = $"输出 {report.DamageDealt}：{skills}\n辅助：{supports}\n承伤 {report.DamageTaken}：{sources}\n" +
                           $"战吼覆盖 {report.WarCryCoverageBasisPoints / 100.0:0.#}% · 战旗覆盖 {report.BannerCoverageBasisPoints / 100.0:0.#}% · " +
                           $"护盾覆盖 {report.ShieldCoverageBasisPoints / 100.0:0.#}% · 药剂 {report.FlaskUses}次/+{report.FlaskRecovery} · 击杀充能 +{report.FlaskChargesGained} · 资源失败 {report.ResourceFailureCount}" +
                           (string.IsNullOrEmpty(report.TimeoutReason) ? string.Empty : $"\n超时归因：{report.TimeoutReason}") +
                           $"\n最后 5 秒：{string.Join("；", report.LastFiveSeconds.TakeLast(12))}",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                });
                if (report.DeathReport is { } death)
                {
                    card.AddChild(new Label
                    {
                        Text = $"死亡归因：{death.FatalSkill} · {death.RawDamageType} {death.FatalDamage} · " +
                               $"可规避：{(death.Avoidable ? "是" : "否")}\n" +
                               $"防御层：{string.Join('、', death.DefensiveLayers)} · 异常：" +
                               (death.Ailments.Count == 0 ? "无" : string.Join('、', death.Ailments)),
                        AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    });
                }
                _reports.AddChild(Frame(card));
            }
            if (session.World.Expedition.Reports.Count == 0) _reports.AddChild(new Label { Text = "尚无战斗报告；完成主线战斗或远征后自动生成。" });
        }

        string dispatchSignature = string.Join('|', session.World.Expedition.Dispatches.Values.OrderBy(item => item.Team));
        RefreshFilterCounts(session);
        foreach ((ExpeditionTeamKind kind, TeamControls controls) in _teams)
        {
            P1TeamExpeditionState team = kind == ExpeditionTeamKind.Hero ? session.World.Hero : session.World.Mercenaries;
            P5TeamDispatchSnapshot? dispatch = session.World.Expedition.Get(kind);
            if (dispatch is not null && dispatchSignature != _dispatchSignature)
            {
                controls.Mode.Select(controls.Mode.GetItemIndex((int)dispatch.Mode));
                controls.RunCount.Visible = dispatch.Mode == P5DispatchMode.Once;
                if (dispatch.Mode == P5DispatchMode.Once && dispatch.RemainingRuns > 0)
                    controls.RunCount.Value = dispatch.RemainingRuns;
            }
            controls.Status.Text = TeamStatus(team, dispatch);
            if (controls.Boss is { } boss)
            {
                SetBossItemText(boss.Target, P5ExpeditionTarget.AbyssWarden,
                    $"深渊监守者（门票 ×{session.World.Expedition.AbyssWardenTickets}）");
                SetBossItemText(boss.Target, P5ExpeditionTarget.AbyssWardenPractice, "深渊监守者练习（免费）");
                SetBossItemText(boss.Target, P5ExpeditionTarget.AshenCitadel,
                    $"灰烬天垒（门票 ×{session.Endgame.CitadelTickets}）");
                SetBossItemText(boss.Target, P5ExpeditionTarget.AshenCitadelPractice, "灰烬天垒练习（免费）");
                SetBossItemText(boss.Target, P5ExpeditionTarget.FinalBreakthrough,
                    session.Endgame.FinalBreakthroughCompleted ? "百级门扉（已完成）" :
                    session.World.Hero.Progression.Level >= 100 ? "百级门扉（可挑战）" : "百级门扉（需要 Lv100）");
                P5ExpeditionTarget selected = (P5ExpeditionTarget)boss.Target.GetItemId(boss.Target.Selected);
                P5BossChallengeAvailability availability = session.GetBossChallengeAvailability(selected);
                string runs = availability.AvailableRuns == int.MaxValue ? "不限次数" : $"可挑战 {availability.AvailableRuns} 次";
                boss.Target.TooltipText = $"{BossName(selected)}：{runs} · {availability.Requirement}";
            }
        }
        _dispatchSignature = dispatchSignature;
    }

    private Control BuildTeamCard(ExpeditionTeamKind kind, string title)
    {
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddChild(BuildMapFilterButton(
            kind == ExpeditionTeamKind.Hero ? MapFilterScope.Hero : MapFilterScope.Mercenaries,
            "通用地图筛选"));
        var gameplay = new P28GameplayPanel();
        gameplay.Initialize(_session!, kind, message => _changed?.Invoke(message));
        content.AddChild(gameplay);
        var selectors = new HFlowContainer();
        content.AddChild(selectors);
        var mode = new OptionButton();
        mode.AddItem("执行指定次数", (int)P5DispatchMode.Once);
        mode.AddItem("重复执行", (int)P5DispatchMode.Repeat);
        mode.AddItem("最高阶推进", (int)P5DispatchMode.HighestAvailable);
        selectors.AddChild(mode);
        var runCount = new SpinBox { MinValue = 1, MaxValue = 999, Value = 1, Prefix = "执行 ", Suffix = " 次" };
        selectors.AddChild(runCount);
        mode.ItemSelected += _ => runCount.Visible = (P5DispatchMode)mode.GetItemId(mode.Selected) == P5DispatchMode.Once;
        var start = new Button { Text = "开始远征" };
        void StartMapExpedition()
        {
            P5DispatchMode selectedMode = (P5DispatchMode)mode.GetItemId(mode.Selected);
            P1TeamExpeditionState team = kind == ExpeditionTeamKind.Hero ? _session!().World.Hero : _session!().World.Mercenaries;
            ExpeditionPolicy current = team.PendingPolicy ?? team.Policy;
            P26MapFilter filter = current.MapFilter ?? P26MapFilter.All;
            MapRoute[] priority = [MapRoute.Abyss, MapRoute.LifeGarden, MapRoute.Warfront, MapRoute.Safe];
            _session!().SetExpeditionPolicy(kind, current with
            {
                PreferredRoute = MapRoute.Abyss,
                RouteDecisionTimeoutSeconds = 0,
                RoutePriority = priority,
                MaximumMapTier = filter.MaximumTier,
                MapFilter = filter,
                MapOrder = P26MapOrder.Recommended,
                NoMatchBehavior = P26NoMatchBehavior.Stop,
                UseRareFragments = false,
                Gameplay = P28GameplayPanel.AutomaticDifficulty(),
            });
            _session!().AssignExpedition(kind, P5ExpeditionTarget.HighestTierMaps, selectedMode, (int)runCount.Value);
            _mapSignature = string.Empty;
            _changed?.Invoke(selectedMode == P5DispatchMode.Once
                ? $"{title}已开始，计划执行 {(int)runCount.Value} 次。"
                : $"{title}已开始：{ModeName(selectedMode)}。");
            RefreshState();
        }
        start.Pressed += () =>
        {
            if (kind == ExpeditionTeamKind.Hero && HasBossChallenge(_session!()))
            {
                ConfirmTaskSwitch("主角正在进行 Boss 挑战。切换到地图远征会立即放弃当前挑战，已消耗的门票不会返还。", StartMapExpedition);
                return;
            }
            StartMapExpedition();
        };
        selectors.AddChild(start);
        var stop = new Button { Text = "停止远征", TooltipText = "不再派发新地图；当前地图会正常结算。" };
        stop.Pressed += () =>
        {
            _session!().CancelExpedition(kind);
            _mapSignature = string.Empty;
            _changed?.Invoke($"{title}已停止；当前地图仍会正常结算。");
            RefreshState();
        };
        selectors.AddChild(stop);
        var abandon = new Button
        {
            Text = "放弃当前远征并停止",
            TooltipText = "立即失去当前地图且不获得结算奖励；操作前会再次确认。",
        };
        abandon.Pressed += () =>
        {
            P1TeamExpeditionState team = kind == ExpeditionTeamKind.Hero
                ? _session!().World.Hero
                : _session!().World.Mercenaries;
            _pendingAbandonTeam = kind;
            _abandonDialog!.DialogText = team.ActiveMap is null
                ? $"{title}当前没有进行中的地图。仍要清空待派地图并停止吗？"
                : $"{title}正在执行的 T{team.ActiveMap.Tier} 地图会永久消耗，且不会获得任何结算奖励。确定放弃并停止吗？";
            _abandonDialog.PopupCentered(new Vector2I(520, 180));
        };
        selectors.AddChild(abandon);
        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(status);
        BossControls? bossControls = null;
        if (kind == ExpeditionTeamKind.Hero)
        {
            bossControls = BuildBossControls(content);
        }
        _teams.Add(kind, new TeamControls(mode, runCount, status, bossControls));
        PanelContainer frame = Frame(content);
        frame.Name = title;
        return frame;
    }

    private Control BuildMapFilterButton(MapFilterScope scope, string buttonText)
    {
        var row = new HBoxContainer();
        var button = new Button { Text = buttonText };
        var count = new Label { Text = "符合筛选 0 张", VerticalAlignment = VerticalAlignment.Center };
        _filterCounts[scope] = count;
        button.Pressed += () => OpenMapFilter(scope);
        row.AddChild(button);
        row.AddChild(count);
        return row;
    }

    private BossControls BuildBossControls(Control parent)
    {
        parent.AddChild(new Label { Text = "Boss 挑战", Modulate = new Color("e5ca8a") });
        var row = new HFlowContainer();
        parent.AddChild(row);
        var target = new OptionButton();
        target.AddItem("深渊监守者", (int)P5ExpeditionTarget.AbyssWarden);
        target.AddItem("深渊监守者练习", (int)P5ExpeditionTarget.AbyssWardenPractice);
        target.AddItem("灰烬天垒", (int)P5ExpeditionTarget.AshenCitadel);
        target.AddItem("灰烬天垒练习", (int)P5ExpeditionTarget.AshenCitadelPractice);
        target.AddItem("百级门扉", (int)P5ExpeditionTarget.FinalBreakthrough);
        row.AddChild(target);
        var mode = new OptionButton();
        mode.AddItem("执行指定次数", (int)P5DispatchMode.Once);
        mode.AddItem("重复执行", (int)P5DispatchMode.Repeat);
        row.AddChild(mode);
        var count = new SpinBox { MinValue = 1, MaxValue = 999, Value = 1, Prefix = "执行 ", Suffix = " 次" };
        row.AddChild(count);
        mode.ItemSelected += _ => count.Visible = (P5DispatchMode)mode.GetItemId(mode.Selected) == P5DispatchMode.Once;
        target.ItemSelected += _ =>
        {
            bool onceOnly = (P5ExpeditionTarget)target.GetItemId(target.Selected) == P5ExpeditionTarget.FinalBreakthrough;
            mode.Disabled = onceOnly;
            if (onceOnly) mode.Select(mode.GetItemIndex((int)P5DispatchMode.Once));
            count.Visible = true;
            if (onceOnly) count.Value = 1;
        };
        var start = new Button { Text = "开始 Boss 挑战" };
        void StartBoss()
        {
            P5ExpeditionTarget selected = (P5ExpeditionTarget)target.GetItemId(target.Selected);
            P5DispatchMode selectedMode = (P5DispatchMode)mode.GetItemId(mode.Selected);
            bool started = _session!().AssignBossChallenge(selected, selectedMode, (int)count.Value);
            _changed?.Invoke(started ? $"已开始 {BossName(selected)}。" : $"无法开始：{_session!().GetBossChallengeAvailability(selected).Requirement}。");
            RefreshState();
        }
        start.Pressed += () =>
        {
            if (HasMapExpedition(_session!()))
            {
                ConfirmTaskSwitch("主角正在进行地图远征。切换到 Boss 挑战会立即放弃当前地图，尚未开始的地图会返回地图仓。", StartBoss);
                return;
            }
            if (HasBossChallenge(_session!()))
            {
                _changed?.Invoke("已有 Boss 挑战正在进行；请先停止或放弃当前挑战。");
                return;
            }
            StartBoss();
        };
        row.AddChild(start);
        var stop = new Button { Text = "停止挑战", TooltipText = "当前 Boss 正常结算，之后不再重复挑战。" };
        stop.Pressed += () =>
        {
            _session!().CancelExpedition(ExpeditionTeamKind.Hero);
            _changed?.Invoke("Boss 挑战将在当前场结算后停止。");
            RefreshState();
        };
        row.AddChild(stop);
        var abandon = new Button { Text = "放弃当前挑战并停止" };
        abandon.Pressed += () =>
        {
            _pendingAbandonTeam = ExpeditionTeamKind.Hero;
            _abandonDialog!.DialogText = "当前 Boss 挑战会立即中止、不会获得奖励，已消耗的门票不会返还。确定放弃吗？";
            _abandonDialog.PopupCentered(new Vector2I(540, 180));
        };
        row.AddChild(abandon);
        return new BossControls(target, mode, count);
    }

    private static void SetBossItemText(OptionButton target, P5ExpeditionTarget id, string text)
    {
        int index = target.GetItemIndex((int)id);
        if (index >= 0) target.SetItemText(index, text);
    }

    private void ConfirmTaskSwitch(string message, Action action)
    {
        _pendingSwitchAction = action;
        _switchDialog!.DialogText = message + "\n\n确定放弃当前任务并切换吗？";
        _switchDialog.PopupCentered(new Vector2I(560, 210));
    }

    private static bool HasBossChallenge(P1GameSession session)
    {
        bool bossMap = session.World.Hero.ActiveMap is { } active &&
            (P5ExpeditionDirector.IsBoss(active) || P5ExpeditionDirector.IsPractice(active));
        bossMap |= session.World.Hero.Queue.Maps.Any(map =>
            P5ExpeditionDirector.IsBoss(map) || P5ExpeditionDirector.IsPractice(map));
        P5TeamDispatchSnapshot? dispatch = session.World.Expedition.Get(ExpeditionTeamKind.Hero);
        return bossMap || dispatch is not null && P5ExpeditionDirector.IsBossTarget(dispatch.Target) && dispatch.Enabled;
    }

    private static bool HasMapExpedition(P1GameSession session)
    {
        bool normalMap = session.World.Hero.ActiveMap is { } active &&
            !P5ExpeditionDirector.IsBoss(active) && !P5ExpeditionDirector.IsPractice(active);
        normalMap |= session.World.Hero.Queue.Maps.Any(map =>
            !P5ExpeditionDirector.IsBoss(map) && !P5ExpeditionDirector.IsPractice(map));
        P5TeamDispatchSnapshot? dispatch = session.World.Expedition.Get(ExpeditionTeamKind.Hero);
        return normalMap || dispatch is not null && !P5ExpeditionDirector.IsBossTarget(dispatch.Target) && dispatch.Enabled;
    }

    private static string BossName(P5ExpeditionTarget target) => target switch
    {
        P5ExpeditionTarget.AbyssWarden => "深渊监守者",
        P5ExpeditionTarget.AbyssWardenPractice => "深渊监守者练习",
        P5ExpeditionTarget.AshenCitadel => "灰烬天垒",
        P5ExpeditionTarget.AshenCitadelPractice => "灰烬天垒练习",
        P5ExpeditionTarget.FinalBreakthrough => "百级门扉",
        _ => "未知 Boss",
    };

    private void OpenMapFilter(MapFilterScope scope)
    {
        P1GameSession session = _session!();
        P26MapFilter current = FilterFor(session, scope);
        string title = scope switch
        {
            MapFilterScope.Hero => "主角派遣 · 通用地图筛选",
            MapFilterScope.Mercenaries => "佣兵派遣 · 通用地图筛选",
            _ => "做图 · 通用地图筛选",
        };
        _mapFilterWindow!.Open(title, current, filter =>
        {
            if (scope == MapFilterScope.Crafting)
            {
                session.World.MapCraftFilter = filter;
            }
            else
            {
                ExpeditionTeamKind kind = scope == MapFilterScope.Hero
                    ? ExpeditionTeamKind.Hero
                    : ExpeditionTeamKind.Mercenaries;
                P1TeamExpeditionState team = kind == ExpeditionTeamKind.Hero ? session.World.Hero : session.World.Mercenaries;
                session.SetExpeditionPolicy(kind, (team.PendingPolicy ?? team.Policy) with
                {
                    MapFilter = filter,
                    MaximumMapTier = filter.MaximumTier,
                });
            }
            _mapSignature = string.Empty;
            _changed?.Invoke($"{title}已保存，共有 {MatchingMapCount(session, filter)} 张可用地图符合筛选。");
            RefreshState();
        });
    }

    private void RefreshFilterCounts(P1GameSession session)
    {
        foreach ((MapFilterScope scope, Label label) in _filterCounts)
            label.Text = $"符合筛选 {MatchingMapCount(session, FilterFor(session, scope))} 张";
    }

    private static P26MapFilter FilterFor(P1GameSession session, MapFilterScope scope) => scope switch
    {
        MapFilterScope.Hero => (session.World.Hero.PendingPolicy ?? session.World.Hero.Policy).MapFilter ?? P26MapFilter.All,
        MapFilterScope.Mercenaries => (session.World.Mercenaries.PendingPolicy ?? session.World.Mercenaries.Policy).MapFilter ?? P26MapFilter.All,
        _ => session.World.MapCraftFilter,
    };

    private static int MatchingMapCount(P1GameSession session, P26MapFilter filter) =>
        session.World.MapInventory.Count(map => !map.IsProtected && filter.Matches(map));

    private void ConfirmAbandon()
    {
        bool hadActiveMap = _session!().AbandonExpedition(_pendingAbandonTeam);
        string title = _pendingAbandonTeam == ExpeditionTeamKind.Hero ? "主角远征" : "佣兵远征";
        _mapSignature = string.Empty;
        _changed?.Invoke(hadActiveMap
            ? $"{title}已放弃，当前地图已永久消耗。"
            : $"{title}已停止；当前没有进行中的地图。");
        RefreshState();
    }

    private static string ModeName(P5DispatchMode mode) => mode switch
    {
        P5DispatchMode.Repeat => "重复执行",
        P5DispatchMode.HighestAvailable => "最高阶推进",
        _ => "执行指定次数",
    };

    private static string TeamStatus(P1TeamExpeditionState team, P5TeamDispatchSnapshot? dispatch)
    {
        if (team.ActiveMap is not null)
        {
            string target = P10EndgameState.IsCitadel(team.ActiveMap) ? "灰烬天垒" :
                P5ExpeditionDirector.IsPractice(team.ActiveMap) ? "Boss 练习" :
                P5ExpeditionDirector.IsBoss(team.ActiveMap) ? "深渊监守者" : $"T{team.ActiveMap.Tier} · Lv{team.ActiveMap.MonsterLevel} 地图";
            return $"执行中：{target} · 剩余约 {Math.Max(1, team.RemainingMapTimeMilliseconds / 1_000)} 秒" +
                   (team.IsStopped ? " · 本图结算后停止" : string.Empty) + "\n" +
                   $"路线 {team.ActiveRoute} · 最近 {(team.LastRun?.Succeeded == true ? "成功" : team.LastRun is null ? "暂无结算" : "失败")}";
        }

        if (team.IsStopped)
        {
            return $"已停止：{StopReason(team.StopReason)}\n完成 {team.MapsCompleted} · 失败 {team.MapsFailed}";
        }

        if (team.Queue.Count > 0)
        {
            P1MapItem map = team.Queue.Maps[0];
            return $"准备中：{TargetName(dispatch?.Target ?? P5ExpeditionTarget.SafeMaps)} · T{map.Tier} · Lv{map.MonsterLevel}";
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
        P5ExpeditionTarget.LifeGardenMaps => "命能花园",
        P5ExpeditionTarget.WarfrontMaps => "亡旗战阵",
        P5ExpeditionTarget.HighestTierMaps => "最高阶推进",
        P5ExpeditionTarget.AbyssWarden => "深渊监守者",
        P5ExpeditionTarget.AbyssWardenPractice => "Boss 练习",
        P5ExpeditionTarget.AshenCitadel => "灰烬天垒",
        P5ExpeditionTarget.AshenCitadelPractice => "灰烬天垒练习",
        P5ExpeditionTarget.FinalBreakthrough => "百级门扉",
        _ => "未知目标",
    };

    private static string StopReason(string reason) => reason switch
    {
        "maps_exhausted" => "没有可用地图",
        "boss_ticket_missing" => "缺少 Boss 门票",
        "citadel_ticket_missing" => "缺少灰烬天垒门票",
        "level_100_required" => "角色未达到 100 级",
        "breakthrough_completed" => "百级门扉已经完成",
        "consecutive_failures" => "连续失败达到 3 次",
        "storage_full" => "仓库已满",
        "tier_locked" => "T17–T20 尚未通过门扉突破",
        "map_policy_limit" => "地图不符合本队筛选条件或阶级上限",
        "manual_stop" => "玩家手动停止",
        "abandoned" => "玩家放弃当前远征",
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

    private static P12MapArea ResolveArea(P1MapItem map) =>
        P12MapCatalog.TryGet(map.AreaId, out P12MapArea area)
            ? area
            : new P12MapArea(map.AreaId, "未登记路印", "未知区域", "未知敌群", "未知首领");

    private enum MapFilterScope { Hero, Mercenaries, Crafting }
    private sealed record BossControls(OptionButton Target, OptionButton Mode, SpinBox Count);
    private sealed record TeamControls(OptionButton Mode, SpinBox RunCount, Label Status, BossControls? Boss);
}

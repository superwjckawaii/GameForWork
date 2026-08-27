using GameForWork.Core.Offline;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using Godot;

namespace GameForWork.GodotClient;

public partial class P1Dashboard : VBoxContainer
{
    private readonly List<string> _activity = [];
    private P1GameSession? _session;
    private Action<PlayerIdentity>? _createCharacter;
    private Action? _stateChanged;
    private Action<string>? _notice;
    private Control? _creationPanel;
    private Control? _fullPanel;
    private VBoxContainer? _miniPanel;
    private Label? _miniStatus;
    private Label? _townStatus;
    private Label? _expeditionStatus;
    private Label? _buildStatus;
    private Label? _storageStatus;
    private RichTextLabel? _report;
    private P1WorldView? _worldView;
    private P1PassiveTreeView? _passiveTree;
    private Label? _selectedPassive;
    private P1ItemGrid? _storageGrid;
    private P1ItemGrid? _heroBackpackGrid;
    private P1ItemGrid? _mercenaryBackpackGrid;
    private OptionButton? _equipmentSlots;
    private readonly Dictionary<SkillSupport, CheckButton> _supportToggles = [];
    private CheckButton? _debugSpeed;
    private double _refreshAccumulator;

    public void Initialize(
        P1GameSession? session,
        Action<PlayerIdentity> createCharacter,
        Action stateChanged,
        Action<string> notice)
    {
        _session = session;
        _createCharacter = createCharacter;
        _stateChanged = stateChanged;
        _notice = notice;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        Build();
        Refresh();
    }

    public void SetSession(P1GameSession? session)
    {
        _session = session;
        if (session is null)
        {
            _creationPanel?.Show();
            _fullPanel?.Hide();
            _miniPanel?.Hide();
        }
        else
        {
            _creationPanel?.Hide();
            _fullPanel?.Show();
        }

        Refresh();
    }

    public void SetMiniMode(bool mini)
    {
        if (_session is null)
        {
            return;
        }

        _fullPanel!.Visible = !mini;
        _miniPanel!.Visible = mini;
        Refresh();
    }

    public void Tick(double delta)
    {
        _refreshAccumulator += delta;
        if (_refreshAccumulator >= 0.2)
        {
            _refreshAccumulator = 0;
            Refresh();
        }
    }

    public void AdvanceWorld(long realMilliseconds)
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            P1OfflineResult result = _session.Advance(realMilliseconds);
            if (result.TotalMapsCompleted + result.TotalMapsFailed > 0)
            {
                AddActivity(
                    $"推进 {result.EffectiveMilliseconds / 1_000}s：完成 {result.TotalMapsCompleted}，失败 {result.TotalMapsFailed}");
            }

            _stateChanged?.Invoke();
            Refresh();
        }
        catch (Exception exception)
        {
            _notice?.Invoke($"推进失败：{exception.Message}");
        }
    }

    private void Build()
    {
        _creationPanel = BuildCreationPanel();
        AddChild(_creationPanel);
        _fullPanel = BuildFullPanel();
        AddChild(_fullPanel);
        _miniPanel = BuildMiniPanel();
        AddChild(_miniPanel);
        _creationPanel.Visible = _session is null;
        _fullPanel.Visible = _session is not null;
        _miniPanel.Visible = false;
    }

    private Control BuildCreationPanel()
    {
        var center = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        var card = new VBoxContainer { CustomMinimumSize = new Vector2(430, 0) };
        center.AddChild(card);
        var title = new Label { Text = "建立门扉契约 · 创建主角", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        card.AddChild(title);
        card.AddChild(new Label { Text = "P1B 精细像素角色支持肤色、发型与装备组合。" });
        var name = new LineEdit { PlaceholderText = "角色名（2～16 字）", Text = "铁誓者" };
        card.AddChild(name);
        OptionButton gender = AddOptions(card, "性别", ["女性", "男性", "中性"]);
        OptionButton skin = AddOptions(card, "肤色", ["苍白", "浅色", "棕褐", "深色"]);
        OptionButton hair = AddOptions(card, "发型", ["短发", "长发", "编发", "剃发"]);
        OptionButton ascendancy = AddOptions(card, "进阶", ["铁誓者", "破阵者"]);
        card.AddChild(new Label
        {
            Text = "铁誓者：稳定的双手武器与防御起点。\n破阵者：P1 开放身份选择，专属分支将在后续内容扩展。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        AddButton(card, "确认创建并进入军锋镇", () =>
        {
            try
            {
                var identity = new PlayerIdentity(
                    name.Text,
                    (CharacterGender)gender.Selected,
                    (CharacterSkinTone)skin.Selected,
                    (CharacterHairStyle)hair.Selected,
                    (P1Ascendancy)ascendancy.Selected).Validate();
                _createCharacter?.Invoke(identity);
            }
            catch (Exception exception)
            {
                _notice?.Invoke(exception.Message);
            }
        });
        return center;
    }

    private Control BuildFullPanel()
    {
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        tabs.AddChild(BuildTownTab());
        tabs.AddChild(BuildExpeditionTab());
        tabs.AddChild(BuildBuildTab());
        tabs.AddChild(BuildStorageTab());
        tabs.AddChild(BuildReportTab());
        return tabs;
    }

    private Control BuildTownTab()
    {
        VBoxContainer page = Page("军锋镇");
        var buttons = new HBoxContainer();
        page.AddChild(buttons);
        AddButton(buttons, "观察城镇", () => SetView(P1ViewMode.Town));
        AddButton(buttons, "观察主角", () => SetView(P1ViewMode.Hero));
        AddButton(buttons, "观察佣兵", () => SetView(P1ViewMode.Mercenaries));
        _worldView = new P1WorldView
        {
            Session = _session,
            Mode = P1ViewMode.Town,
            CustomMinimumSize = new Vector2(768, 432),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        page.AddChild(_worldView);
        _townStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_townStatus);
        return Wrap(page);
    }

    private Control BuildExpeditionTab()
    {
        VBoxContainer page = Page("远征");
        page.AddChild(new Label { Text = "两支固定队伍独立运行；路线方针在无人操作和离线时同样生效。" });
        AddTeamPolicyRow(page, ExpeditionTeamKind.Hero, "主角单人队");
        AddTeamPolicyRow(page, ExpeditionTeamKind.Mercenaries, "佣兵队");
        var controls = new HFlowContainer();
        page.AddChild(controls);
        AddButton(controls, "推进 5 秒", () => AdvanceWorld(5_000));
        AddButton(controls, "推进一张图时间", () => AdvanceWorld(90_000));
        AddButton(controls, "模拟离线 48h", () => AdvanceWorld(OfflineTime.MaximumMilliseconds));
        AddButton(controls, "将掉落地图加入两队", () =>
        {
            int moved = RequireSession().EnqueueInventoryMaps();
            Changed($"已加入 {moved} 张地图。");
        });
        _debugSpeed = new CheckButton { Text = "开发专用 20× 加速" };
        _debugSpeed.Toggled += enabled =>
        {
            RequireSession().DebugTwentyTimes = enabled;
            Changed(enabled ? "20× 加速已开启。" : "20× 加速已关闭。");
        };
        page.AddChild(_debugSpeed);
        _expeditionStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_expeditionStatus);
        return Wrap(page);
    }

    private Control BuildBuildTab()
    {
        VBoxContainer page = Page("构筑");
        var supports = new HFlowContainer();
        page.AddChild(supports);
        AddSupportToggle(supports, "扩大范围", SkillSupport.IncreasedArea);
        AddSupportToggle(supports, "攻击速度", SkillSupport.AttackSpeed);
        AddSupportToggle(supports, "流血", SkillSupport.Bleed);
        AddSupportToggle(supports, "生命消耗", SkillSupport.LifeCost);

        page.AddChild(new HSeparator());
        page.AddChild(new Label { Text = "主角 AI（修改在下一张地图生效）" });
        var aiRow = new HBoxContainer();
        page.AddChild(aiRow);
        OptionButton preset = AddOptions(aiRow, "预设", ["稳健", "均衡", "激进"]);
        preset.Select(1);
        var warCry = new CheckButton { Text = "使用战吼", ButtonPressed = true };
        aiRow.AddChild(warCry);
        var threshold = new SpinBox { MinValue = 10, MaxValue = 90, Step = 5, Value = 50, Suffix = "%" };
        aiRow.AddChild(new Label { Text = "药剂阈值" });
        aiRow.AddChild(threshold);
        AddButton(aiRow, "应用 AI", () =>
        {
            RequireSession().SetHeroAi(new HeroAiConfiguration(
                preset.GetItemText(preset.Selected),
                warCry.ButtonPressed,
                (int)threshold.Value * 100));
            Changed("主角 AI 已更新。");
        });

        page.AddChild(new HSeparator());
        page.AddChild(new Label { Text = "共享被动天赋树（悬浮查看说明，点击选择节点）" });
        _passiveTree = new P1PassiveTreeView();
        _passiveTree.NodeSelected += stableId =>
        {
            PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
            _selectedPassive!.Text = $"已选择：{node.DisplayName} · {string.Join("；", node.Effects.Select(P1UiText.PassiveEffect))}";
        };
        page.AddChild(_passiveTree);
        var passiveRow = new HBoxContainer();
        page.AddChild(passiveRow);
        _selectedPassive = new Label
        {
            Text = "点击图中的天赋节点后进行分配或退还",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        passiveRow.AddChild(_selectedPassive);
        AddButton(passiveRow, "分配", AllocateSelectedPassive);
        AddButton(passiveRow, "退还", RefundSelectedPassive);
        AddButton(passiveRow, "完整重置", () =>
        {
            bool changed = RequireSession().TryResetPassives();
            Changed(changed ? "天赋已重置。" : "需要 10 个记忆灰烬且至少已分配一个节点。");
        });
        _buildStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_buildStatus);
        return Wrap(page);
    }

    private Control BuildStorageTab()
    {
        VBoxContainer page = Page("仓库 · 过滤 · 工坊");
        var equipRow = new HBoxContainer();
        page.AddChild(equipRow);
        equipRow.AddChild(new Label { Text = "点击仓库格选择装备" });
        _equipmentSlots = AddOptions(equipRow, "槽位", ["主手", "胸甲", "头盔", "左戒", "右戒", "药剂1"]);
        AddButton(equipRow, "装备选中物品", EquipSelectedStorageItem);

        var inventories = new HBoxContainer();
        inventories.AddThemeConstantOverride("separation", 18);
        page.AddChild(inventories);
        var backpackColumn = new VBoxContainer();
        inventories.AddChild(backpackColumn);
        backpackColumn.AddChild(new Label { Text = "主角远征背包 · 最近结算 20 格" });
        _heroBackpackGrid = new P1ItemGrid();
        _heroBackpackGrid.Configure(5, ExpeditionBackpack.Capacity, 34);
        backpackColumn.AddChild(_heroBackpackGrid);
        backpackColumn.AddChild(new Label { Text = "佣兵远征背包 · 最近结算 20 格" });
        _mercenaryBackpackGrid = new P1ItemGrid();
        _mercenaryBackpackGrid.Configure(5, ExpeditionBackpack.Capacity, 34);
        backpackColumn.AddChild(_mercenaryBackpackGrid);

        var storageColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inventories.AddChild(storageColumn);
        storageColumn.AddChild(new Label { Text = "军锋镇装备仓库 · 100 格（悬浮查看完整装备信息）" });
        _storageGrid = new P1ItemGrid();
        _storageGrid.Configure(10, EquipmentStorage.InitialCapacity, 34);
        storageColumn.AddChild(_storageGrid);
        var workshop = new HFlowContainer();
        page.AddChild(workshop);
        AddButton(workshop, "工坊：50 金币 + 10 铁屑制作物理前缀", () =>
        {
            WorkshopResult result = RequireSession().CraftEquippedWeapon();
            Changed(result.Succeeded ? "工坊制作成功。" : $"制作失败：{result.FailureReason}");
        });
        AddButton(workshop, "10 监守印记兑换传奇", () =>
            Changed(RequireSession().TryExchangeLegendary() ? "传奇已存入仓库。" : "印记不足或仓库已满。"));

        page.AddChild(new HSeparator());
        page.AddChild(new Label { Text = "过滤器首条自定义规则（首次底材与首次传奇始终强制保留）" });
        var filterRow = new HFlowContainer();
        page.AddChild(filterRow);
        OptionButton rarity = AddOptions(filterRow, "稀有度", ["任意", "基础", "魔法", "稀有", "传奇"]);
        var baseId = new LineEdit { PlaceholderText = "底材稳定 ID（可空）", CustomMinimumSize = new Vector2(180, 0) };
        filterRow.AddChild(baseId);
        var affixId = new LineEdit { PlaceholderText = "词缀族 ID（可空）", CustomMinimumSize = new Vector2(180, 0) };
        filterRow.AddChild(affixId);
        var minimum = new SpinBox { MinValue = 0, MaxValue = 50_000, Step = 1 };
        filterRow.AddChild(minimum);
        OptionButton disposition = AddOptions(filterRow, "处理", ["保留", "出售", "分解"]);
        AddButton(filterRow, "插入规则", () =>
        {
            ItemRarity? selectedRarity = rarity.Selected == 0 ? null : (ItemRarity)(rarity.Selected - 1);
            var rule = new LootFilterRule(
                $"user.filter.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                (LootDisposition)disposition.Selected,
                selectedRarity,
                EmptyToNull(baseId.Text),
                EmptyToNull(affixId.Text),
                minimum.Value <= 0 ? null : (int)minimum.Value);
            RequireSession().World.Filter.ReplaceRules(
                new[] { rule }.Concat(RequireSession().World.Filter.Rules));
            Changed("过滤规则已插入首位。");
        });
        _storageStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_storageStatus);
        return Wrap(page);
    }

    private Control BuildReportTab()
    {
        VBoxContainer page = Page("报告 · 调试验收");
        _report = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        page.AddChild(_report);
        return page;
    }

    private VBoxContainer BuildMiniPanel()
    {
        var panel = new VBoxContainer { Visible = false, SizeFlagsVertical = SizeFlags.ExpandFill };
        _miniStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _miniStatus.AddThemeFontSizeOverride("font_size", 13);
        panel.AddChild(_miniStatus);
        var route = new HBoxContainer();
        panel.AddChild(route);
        AddButton(route, "主角安全", () => QuickRoute(ExpeditionTeamKind.Hero, MapRoute.Safe));
        AddButton(route, "主角裂渊", () => QuickRoute(ExpeditionTeamKind.Hero, MapRoute.Abyss));
        AddButton(route, "佣兵安全", () => QuickRoute(ExpeditionTeamKind.Mercenaries, MapRoute.Safe));
        AddButton(route, "佣兵裂渊", () => QuickRoute(ExpeditionTeamKind.Mercenaries, MapRoute.Abyss));
        return panel;
    }

    private void AddTeamPolicyRow(Container page, ExpeditionTeamKind kind, string label)
    {
        var row = new HBoxContainer();
        page.AddChild(row);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(100, 0) });
        OptionButton route = AddOptions(row, "优先路线", ["安全", "裂渊"]);
        var automatic = new CheckButton { Text = "无人操作时自动选择", ButtonPressed = true };
        row.AddChild(automatic);
        var stopOnFailure = new CheckButton { Text = "失败后停止" };
        row.AddChild(stopOnFailure);
        AddButton(row, "应用", () =>
        {
            P1TeamExpeditionState team = Team(kind);
            team.Policy = new ExpeditionPolicy(
                automatic.ButtonPressed ? RouteSelectionMode.Automatic : RouteSelectionMode.Manual,
                (MapRoute)route.Selected,
                stopOnFailure.ButtonPressed ? QueueFailureBehavior.Stop : QueueFailureBehavior.Continue,
                team.Policy.StorageFullBehavior);
            Changed($"{label}方针已更新。");
        });
    }

    private void AddSupportToggle(Container parent, string text, SkillSupport support)
    {
        var toggle = new CheckButton { Text = text };
        toggle.Toggled += enabled =>
        {
            P1GameSession session = RequireSession();
            SkillSupport updated = enabled
                ? session.HeavyStrikeSupports | support
                : session.HeavyStrikeSupports & ~support;
            session.SetHeavyStrikeSupports(updated);
            Changed($"重击辅助已更新：{updated}");
        };
        parent.AddChild(toggle);
        _supportToggles[support] = toggle;
    }

    private void AllocateSelectedPassive()
    {
        if (_passiveTree?.SelectedStableId is not string stableId)
        {
            Changed("请先在天赋图中选择节点。");
            return;
        }

        PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
        Changed(RequireSession().TryAllocatePassive(node.StableId)
            ? $"已分配：{node.DisplayName}"
            : "无法分配：检查前置节点与可用点数。");
    }

    private void RefundSelectedPassive()
    {
        if (_passiveTree?.SelectedStableId is not string stableId)
        {
            Changed("请先在天赋图中选择节点。");
            return;
        }

        PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
        Changed(RequireSession().TryRefundPassive(node.StableId)
            ? $"已退还：{node.DisplayName}"
            : "无法退还：节点未分配、存在后续节点或灰烬不足。");
    }

    private void EquipSelectedStorageItem()
    {
        EquipmentSlot[] slots =
        [
            EquipmentSlot.MainHand,
            EquipmentSlot.Chest,
            EquipmentSlot.Helmet,
            EquipmentSlot.RingLeft,
            EquipmentSlot.RingRight,
            EquipmentSlot.Flask1,
        ];
        int selected = _storageGrid?.SelectedIndex ?? -1;
        bool equipped = RequireSession().TryEquipFromStorage(selected, slots[_equipmentSlots!.Selected]);
        Changed(equipped ? "装备已更换，技能容量与面板已刷新。" : "该物品不能装备到目标槽位。");
    }

    private void QuickRoute(ExpeditionTeamKind kind, MapRoute route)
    {
        P1TeamExpeditionState team = Team(kind);
        team.Policy = team.Policy with { PreferredRoute = route, RouteSelection = RouteSelectionMode.Automatic };
        Changed($"{kind} 已切换为 {route} 路线。");
    }

    private void SetView(P1ViewMode mode)
    {
        _worldView!.Mode = mode;
        _worldView.QueueRedraw();
    }

    private void Refresh()
    {
        if (_session is null)
        {
            return;
        }

        _worldView!.Session = _session;
        _worldView.QueueRedraw();
        _passiveTree?.SetState(_session.Passives.Allocated, _session.World.Hero.Progression.EarnedPassivePoints);
        foreach ((SkillSupport support, CheckButton toggle) in _supportToggles)
        {
            toggle.SetPressedNoSignal(_session.HeavyStrikeSupports.HasFlag(support));
        }

        _debugSpeed?.SetPressedNoSignal(_session.DebugTwentyTimes);
        TownEconomyState economy = _session.World.Economy;
        _townStatus!.Text =
            $"补给 {economy.ExpeditionSupplies}（150 秒/份，无维护费） · 金币 {economy.Gold} · 铁屑 {economy.IronScraps} · " +
            $"技能石 {economy.SkillStones} · 记忆灰烬 {_session.Passives.MemoryAshes} · 监守印记 {economy.WardenMarks}\n" +
            $"传送装置 Lv.{_session.World.Teleporter.Level} · 佣兵队人数上限 {_session.World.Teleporter.MercenaryTeamCapacity}";
        _expeditionStatus!.Text = TeamText(_session.World.Hero) + "\n" + TeamText(_session.World.Mercenaries) +
            $"\n地图库存 {_session.World.MapInventory.Count} · 模拟速度 {_session.SimulationSpeed}×";
        EquipmentSummary equipment = _session.HeroBuild.Equipment;
        _buildStatus!.Text =
            $"{_session.Player.Name} · {_session.Player.Ascendancy} · Lv.{_session.World.Hero.Progression.Level} " +
            $"XP {_session.World.Hero.Progression.Experience}/{CharacterProgression.TotalExperienceToCap}\n" +
            $"体魄 {_session.HeroBuild.Sheet.Attributes.Physique} · 灵巧 {_session.HeroBuild.Sheet.Attributes.Dexterity} · " +
            $"精神 {_session.HeroBuild.Sheet.Attributes.Spirit} · 能量 {_session.HeroBuild.Sheet.Attributes.Energy}\n" +
            $"核心槽 {equipment.CoreSkillCapacity} · 连接 {equipment.SupportLinkCapacity} · 已用辅助 {CountSupports(_session.HeavyStrikeSupports)}\n" +
            $"被动点 {_session.Passives.Allocated.Count}/{_session.World.Hero.Progression.EarnedPassivePoints} · 灰烬 {_session.Passives.MemoryAshes}\n" +
            $"AI：{_session.World.Hero.Build.AiSummary}\n佣兵 {_session.MercenaryName}：{_session.World.Mercenaries.Build.AiSummary}";
        _storageStatus!.Text =
            $"装备仓库 {_session.World.Storage.Count}/{_session.World.Storage.Capacity} · 地图独立库存 {_session.World.MapInventory.Count}\n" +
            $"默认：传奇/稀有保留，魔法出售，基础分解；现有过滤规则 {_session.World.Filter.Rules.Count} 条。";
        _storageGrid?.SetItems(_session.World.Storage.Items);
        _heroBackpackGrid?.SetItems(_session.World.Hero.Backpack.Items);
        _mercenaryBackpackGrid?.SetItems(_session.World.Mercenaries.Backpack.Items);
        RefreshReport();
        _miniStatus!.Text =
            $"{_session.Player.Name} Lv.{_session.World.Hero.Progression.Level}  " +
            $"主角[{CompactTeam(_session.World.Hero)}]  佣兵[{CompactTeam(_session.World.Mercenaries)}]\n" +
            $"补给 {economy.ExpeditionSupplies} · 金币 {economy.Gold} · 图 {_session.World.MapInventory.Count} · {_session.SimulationSpeed}×";
    }

    private void RefreshReport()
    {
        CombatPreview preview = _session!.GetCombatPreview();
        _report!.Text =
            $"[b]构筑即时面板[/b]\n" +
            $"平均命中 {preview.AverageHitDamage.Value} · 攻击频率 {preview.AttacksPerSecondMilli.Value / 1000.0:0.00}/s · " +
            $"命中率 {preview.HitChanceBasisPoints.Value / 100.0:0.0}% · 暴击率 {preview.CriticalChanceBasisPoints.Value / 100.0:0.0}%\n" +
            $"流血 DPS {preview.ExpectedBleedDamagePerSecond.Value} · 有效生命 {preview.EffectiveLife.Value} · " +
            $"护甲减伤 {preview.ArmorReductionAgainstMinimumHit.Value / 100.0:0.0}%～{preview.ArmorReductionAgainstMaximumHit.Value / 100.0:0.0}% · " +
            $"护盾恢复 {preview.ShieldRecoveryPerSecond.Value}/s\n\n" +
            $"[b]平均命中公式展开[/b]\n{FormatSteps(preview.AverageHitDamage)}\n\n" +
            $"[b]命中公式展开[/b]\n{FormatSteps(preview.HitChanceBasisPoints)}\n\n" +
            $"[b]最近地图战斗[/b]\n{FormatRun(_session.World.Hero)}\n{FormatRun(_session.World.Mercenaries)}\n\n" +
            $"[b]最近活动[/b]\n{string.Join('\n', _activity.TakeLast(20))}";
    }

    private void Changed(string message)
    {
        AddActivity(message);
        _stateChanged?.Invoke();
        _notice?.Invoke(message);
        Refresh();
    }

    private void AddActivity(string message)
    {
        _activity.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        if (_activity.Count > 100)
        {
            _activity.RemoveRange(0, _activity.Count - 100);
        }
    }

    private P1GameSession RequireSession() =>
        _session ?? throw new InvalidOperationException("Create a character first.");

    private P1TeamExpeditionState Team(ExpeditionTeamKind kind) =>
        kind == ExpeditionTeamKind.Hero ? RequireSession().World.Hero : RequireSession().World.Mercenaries;

    private static string TeamText(P1TeamExpeditionState team) =>
        $"{team.Kind}: 队列 {team.Queue.Count}/10 · 完成 {team.MapsCompleted} · 失败 {team.MapsFailed} · " +
        $"路线 {team.Policy.PreferredRoute} · " +
        (team.ActiveMap is null
            ? team.IsStopped ? $"停止：{team.StopReason}" : "等待资源/地图"
            : $"进行中 {team.ActiveMap.InstanceId}，剩余 {team.RemainingMapTimeMilliseconds / 1_000}s");

    private static string CompactTeam(P1TeamExpeditionState team) => team.ActiveMap is null
        ? team.IsStopped ? "停止" : $"等待 Q{team.Queue.Count}"
        : $"{team.ActiveRoute} {team.RemainingMapTimeMilliseconds / 1_000}s";

    private static string FormatSteps(CalculatedValue value) => string.Join(
        '\n',
        value.Steps.Select(step => $"• {step.Label}: {step.Expression} = {step.Result}"));

    private static string FormatRun(P1TeamExpeditionState team)
    {
        if (team.LastRun is null)
        {
            return $"{team.Kind}: 尚无结算";
        }

        string nodes = string.Join(
            " | ",
            team.LastRun.Attempts[^1].Nodes.Select(node =>
            {
                string eventSummary = node.Events is null
                    ? string.Empty
                    : string.Join(',', node.Events
                        .Where(item => item.Kind is P1CombatEventKind.BossPhaseChanged or
                            P1CombatEventKind.BossSummonedWorkers or
                            P1CombatEventKind.BossHazardCreated or
                            P1CombatEventKind.LifeFlaskUsed or
                            P1CombatEventKind.LegendaryAftershock)
                        .Select(item => item.Kind)
                        .Distinct());
                return $"N{node.NodeIndex}:{node.EnemyStableId}{(node.Elite ? "[精英]" : string.Empty)} " +
                    $"{node.Outcome} {node.Ticks / 20.0:0.0}s{(eventSummary.Length == 0 ? string.Empty : $"<{eventSummary}>")}";
            }));
        return $"{team.Kind}: {(team.LastRun.Succeeded ? "成功" : "失败")} · 尝试 {team.LastRun.AttemptsUsed}/3 · {nodes}";
    }

    private static int CountSupports(SkillSupport supports) =>
        System.Numerics.BitOperations.PopCount((uint)supports);

    private static VBoxContainer Page(string name) => new()
    {
        Name = name,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill,
    };

    private static ScrollContainer Wrap(Control child)
    {
        var scroll = new ScrollContainer
        {
            Name = child.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        child.Name = "Content";
        scroll.AddChild(child);
        return scroll;
    }

    private static OptionButton AddOptions(Container parent, string label, IReadOnlyList<string> options)
    {
        parent.AddChild(new Label { Text = label });
        var button = new OptionButton();
        foreach (string option in options)
        {
            button.AddItem(option);
        }

        parent.AddChild(button);
        return button;
    }

    private static void AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using GameForWork.Core.Offline;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2Dashboard : VBoxContainer
{
    private const int ContextEquip = 1;
    private const int ContextUnequip = 2;
    private const int ContextTransfer = 3;
    private const int ContextSell = 4;
    private const int ContextDismantle = 5;
    private const int ContextLock = 6;
    private const int ContextCrafting = 7;
    private const int ContextBuyback = 8;

    private readonly Dictionary<SkillSupport, CheckButton> _supportToggles = [];
    private readonly List<BaseButton> _heroOnlyControls = [];
    private P1GameSession? _session;
    private Action<PlayerIdentity>? _createCharacter;
    private Action? _stateChanged;
    private Action<string>? _notice;
    private Control? _creationPanel;
    private Control? _fullPanel;
    private VBoxContainer? _miniPanel;
    private TabContainer? _mainTabs;
    private Control? _expeditionPage;
    private Label? _miniStatus;
    private Label? _overviewStatus;
    private Label? _expeditionStatus;
    private Label? _characterStatus;
    private Label? _storageStatus;
    private Label? _selectedPassive;
    private Label? _skillSummary;
    private RichTextLabel? _history;
    private RichTextLabel? _storyLog;
    private Label? _storyStatus;
    private P2CampaignRouteView? _campaignRoute;
    private P1WorldView? _worldView;
    private P1PassiveTreeView? _passiveTree;
    private P1ItemGrid? _storageGrid;
    private P1ItemGrid? _sortingGrid;
    private P1ItemGrid? _equipmentGrid;
    private P1ItemGrid? _recoveryGrid;
    private P1ItemGrid? _buybackGrid;
    private P1ItemGrid? _heroLootGrid;
    private P1ItemGrid? _mercenaryLootGrid;
    private P2LootFilterPanel? _filterPanel;
    private P2SkillStonePanel? _skillStonePanel;
    private P2MapQueuePanel? _mapQueuePanel;
    private OptionButton? _characterSelector;
    private PopupMenu? _itemMenu;
    private ConfirmationDialog? _confirmDialog;
    private ItemContainerKind _contextContainer;
    private int _contextIndex = -1;
    private Action? _pendingConfirmation;
    private P2CharacterKind _selectedCharacter;
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
        _creationPanel!.Visible = session is null;
        _fullPanel!.Visible = session is not null;
        _miniPanel!.Visible = false;
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
        if (_refreshAccumulator >= 0.1)
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
                _session.Management.AddHistory(
                    $"推进 {result.EffectiveMilliseconds / 1_000}s：完成 {result.TotalMapsCompleted}，失败 {result.TotalMapsFailed}。");
            }

            Changed("世界时间已推进。");
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
        BuildContextMenu();
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
        card.AddChild(new Label { Text = "新角色将从第一幕“余烬营地”开始挂机推进。" });
        var name = new LineEdit { PlaceholderText = "角色名（2～16 字）", Text = "铁誓者" };
        card.AddChild(name);
        OptionButton gender = AddOptions(card, "性别", ["女性", "男性", "中性"]);
        OptionButton skin = AddOptions(card, "肤色", ["苍白", "浅色", "棕褐", "深色"]);
        OptionButton hair = AddOptions(card, "发型", ["短发", "长发", "编发", "剃发"]);
        OptionButton ascendancy = AddOptions(card, "进阶", ["铁誓者", "破阵者"]);
        AddButton(card, "确认创建并进入余烬营地", () =>
        {
            try
            {
                _createCharacter?.Invoke(new PlayerIdentity(
                    name.Text,
                    (CharacterGender)gender.Selected,
                    (CharacterSkinTone)skin.Selected,
                    (CharacterHairStyle)hair.Selected,
                    (P1Ascendancy)ascendancy.Selected).Validate());
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
        _mainTabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _mainTabs.AddChild(BuildOverviewPage());
        _mainTabs.AddChild(BuildStoryPage());
        _expeditionPage = BuildExpeditionPage();
        _mainTabs.AddChild(_expeditionPage);
        _mainTabs.AddChild(BuildCharacterItemsPage());
        _mainTabs.AddChild(BuildTownPage());
        return _mainTabs;
    }

    private Control BuildOverviewPage()
    {
        VBoxContainer page = Page("总览");
        var controls = new HFlowContainer();
        page.AddChild(controls);
        AddButton(controls, "观察城镇", () => SetView(P1ViewMode.Town));
        AddButton(controls, "观察主角", () => SetView(P1ViewMode.Hero));
        AddButton(controls, "观察佣兵", () => SetView(P1ViewMode.Mercenaries));
        _worldView = new P1WorldView
        {
            Session = _session,
            Mode = P1ViewMode.Town,
            CustomMinimumSize = new Vector2(640, 300),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        page.AddChild(_worldView);
        _overviewStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_overviewStatus);
        return page;
    }

    private Control BuildStoryPage()
    {
        VBoxContainer page = Page("主线");
        var workspace = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddChild(workspace);
        _campaignRoute = new P2CampaignRouteView
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _campaignRoute.Initialize(RequireSession, _ => Refresh());
        workspace.AddChild(_campaignRoute);
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
        workspace.AddChild(right);
        _storyStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        right.AddChild(_storyStatus);
        AddButton(right, "继续 / 战败后重试", () =>
        {
            RequireSession().ResumeCampaignAfterDefeat();
            Changed("主线自动推进已继续。");
        });
        AddButton(right, "重玩选中节点", () =>
        {
            string? selected = _campaignRoute.SelectedStableId;
            Changed(selected is not null && RequireSession().ReplayCampaignNode(selected)
                ? "节点重玩完成；固定剧情奖励未重复发放。"
                : "只能重玩已经完成的战斗节点。");
        });
        page.AddChild(new Label { Text = "剧情记录" });
        _storyLog = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 130),
            ScrollActive = true,
        };
        page.AddChild(_storyLog);
        return page;
    }

    private Control BuildExpeditionPage()
    {
        VBoxContainer page = Page("远征");
        page.AddChild(new Label { Text = "主角队与佣兵队拥有独立队列；当前地图使用启动时的方针快照。" });
        _mapQueuePanel = new P2MapQueuePanel();
        _mapQueuePanel.Initialize(RequireSession, Changed);
        page.AddChild(_mapQueuePanel);
        AddTeamPolicyRow(page, ExpeditionTeamKind.Hero, "主角单人队");
        AddTeamPolicyRow(page, ExpeditionTeamKind.Mercenaries, "佣兵队");
        var controls = new HFlowContainer();
        page.AddChild(controls);
        AddButton(controls, "推进 5 秒", () => AdvanceWorld(5_000));
        AddButton(controls, "推进一张图时间", () => AdvanceWorld(90_000));
        AddButton(controls, "模拟离线 48h", () => AdvanceWorld(OfflineTime.MaximumMilliseconds));
        AddButton(controls, "分配地图库存", () => Changed($"已加入 {RequireSession().EnqueueInventoryMaps()} 张地图。"));
        _expeditionStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_expeditionStatus);
        return Wrap(page);
    }

    private Control BuildCharacterItemsPage()
    {
        VBoxContainer page = Page("角色与物品");
        var header = new HBoxContainer();
        page.AddChild(header);
        _characterSelector = AddOptions(header, "当前角色", ["主角", "佣兵"]);
        _characterSelector.ItemSelected += index =>
        {
            _selectedCharacter = (P2CharacterKind)index;
            Refresh();
        };
        var collapseSidebar = new Button
        {
            Text = "收起装备侧栏",
            FocusMode = FocusModeEnum.None,
            TooltipText = "收起常驻装备备栏，为技能、天赋和仓库腾出空间",
        };
        header.AddChild(collapseSidebar);
        _characterStatus = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        header.AddChild(_characterStatus);

        var workspace = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        workspace.AddThemeConstantOverride("separation", 12);
        page.AddChild(workspace);
        var modes = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        workspace.AddChild(modes);
        modes.AddChild(BuildEquipmentMode());
        modes.AddChild(BuildSkillMode());
        modes.AddChild(BuildPassiveMode());
        modes.AddChild(BuildAiMode());
        var equipment = new VBoxContainer { CustomMinimumSize = new Vector2(210, 0) };
        workspace.AddChild(equipment);
        collapseSidebar.Pressed += () =>
        {
            equipment.Visible = !equipment.Visible;
            collapseSidebar.Text = equipment.Visible ? "收起装备侧栏" : "展开装备侧栏";
        };
        equipment.AddChild(new Label { Text = "角色装备备栏 · 所有模式常驻" });
        _equipmentGrid = BuildGrid(ItemContainerKind.Equipped, 3, Enum.GetValues<EquipmentSlot>().Length, 42);
        _equipmentGrid.ExtraTooltip = EquipmentComparisonText;
        equipment.AddChild(_equipmentGrid);
        var details = new Button { Text = "详细属性", ToggleMode = true };
        equipment.AddChild(details);
        _storageStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        equipment.AddChild(_storageStatus);
        details.Toggled += expanded => _storageStatus.Visible = expanded;
        _storageStatus.Visible = false;
        return page;
    }

    private Control BuildEquipmentMode()
    {
        var scroll = new ScrollContainer
        {
            Name = "装备与仓库",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(body);
        body.AddChild(new Label
        {
            Text = "双击自动换装/卸装 · Shift+左键快速转移 · 拖拽精确移动 · 右键更多操作 · Alt 悬浮查看完整说明",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 14);
        body.AddChild(columns);

        var containers = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddChild(containers);
        containers.AddChild(new Label { Text = "共享整理背包 · 20 格" });
        _sortingGrid = BuildGrid(ItemContainerKind.SortingBag, 5, P2ManagementState.SortingBagCapacity, 36);
        containers.AddChild(_sortingGrid);
        containers.AddChild(new Label { Text = "军锋镇仓库 · 100 格" });
        _storageGrid = BuildGrid(ItemContainerKind.Storage, 10, EquipmentStorage.InitialCapacity, 32);
        _storageGrid.ExtraTooltip = EquipmentComparisonText;
        containers.AddChild(_storageGrid);
        var batch = new HFlowContainer();
        containers.AddChild(batch);
        AddButton(batch, "批量出售普通/魔法", () => ConfirmBatch(sell: true));
        AddButton(batch, "批量分解普通/魔法", () => ConfirmBatch(sell: false));

        var safetyTabs = new TabContainer { CustomMinimumSize = new Vector2(0, 150) };
        body.AddChild(safetyTabs);
        var recovery = Page("恢复箱");
        recovery.AddChild(new Label { Text = "永久保存，只能取出；非空时总览持续警告。" });
        _recoveryGrid = BuildGrid(ItemContainerKind.Recovery, 10, 30, 30);
        recovery.AddChild(_recoveryGrid);
        safetyTabs.AddChild(recovery);
        var buyback = Page("回购");
        buyback.AddChild(new Label { Text = "保留最近 20 件手动售出的物品，按原价回购。" });
        _buybackGrid = BuildGrid(ItemContainerKind.Buyback, 10, P2ManagementState.BuybackCapacity, 30);
        buyback.AddChild(_buybackGrid);
        safetyTabs.AddChild(buyback);
        return scroll;
    }

    private Control BuildSkillMode()
    {
        VBoxContainer page = Page("技能");
        page.AddChild(new Label { Text = "技能石拥有独立实例、等级和经验；主角可配置，佣兵由自主成长决定。" });
        _skillSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        page.AddChild(_skillSummary);
        _skillStonePanel = new P2SkillStonePanel();
        _skillStonePanel.Initialize(RequireSession, Changed);
        page.AddChild(_skillStonePanel);
        var supports = new HFlowContainer();
        page.AddChild(supports);
        AddSupportToggle(supports, "扩大范围", SkillSupport.IncreasedArea);
        AddSupportToggle(supports, "攻击速度", SkillSupport.AttackSpeed);
        AddSupportToggle(supports, "流血", SkillSupport.Bleed);
        AddSupportToggle(supports, "生命消耗", SkillSupport.LifeCost);
        return page;
    }

    private Control BuildPassiveMode()
    {
        VBoxContainer page = Page("天赋");
        var search = new LineEdit { PlaceholderText = "搜索天赋名称或效果；规划不会消耗点数" };
        search.TextChanged += query => _passiveTree?.SetSearch(query);
        page.AddChild(search);
        _passiveTree = new P1PassiveTreeView();
        _passiveTree.NodeSelected += stableId =>
        {
            PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
            _selectedPassive!.Text = $"已选择：{node.DisplayName} · {string.Join("；", node.Effects.Select(P1UiText.PassiveEffect))}";
        };
        page.AddChild(_passiveTree);
        var row = new HBoxContainer();
        page.AddChild(row);
        _selectedPassive = new Label
        {
            Text = "点击图中的天赋节点后进行分配、规划或退还",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(_selectedPassive);
        Button allocate = AddButton(row, "分配", AllocateSelectedPassive);
        AddButton(row, "规划最短路径", () => Changed(_passiveTree?.PlanPathToSelected() == true
            ? "已规划到目标节点的最短路径；未消耗天赋点。"
            : "请先选择目标节点。"));
        AddButton(row, "清除规划", () =>
        {
            _passiveTree?.ClearPlan();
            Changed("天赋规划已清除。");
        });
        Button refund = AddButton(row, "退还", RefundSelectedPassive);
        Button reset = AddButton(row, "完整重置", ResetPassives);
        _heroOnlyControls.AddRange([allocate, refund, reset]);
        return Wrap(page);
    }

    private Control BuildAiMode()
    {
        VBoxContainer page = Page("AI");
        page.AddChild(new Label
        {
            Text = "规则只支持一层“全部满足 / 任一满足”。AI 可读取敌人与地图危险度，不读取隐藏掉落结果。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var row = new HFlowContainer();
        page.AddChild(row);
        OptionButton preset = AddOptions(row, "预设", ["稳健", "均衡", "激进", "Boss 优先", "清图优先"]);
        preset.Select(1);
        var warCry = new CheckButton { Text = "资源允许时战吼", ButtonPressed = true };
        row.AddChild(warCry);
        var threshold = new SpinBox { MinValue = 10, MaxValue = 90, Step = 5, Value = 50, Suffix = "%" };
        row.AddChild(new Label { Text = "生命药剂阈值" });
        row.AddChild(threshold);
        OptionButton matchMode = AddOptions(row, "条件", ["全部满足", "任一满足"]);
        var enemyCount = new SpinBox { MinValue = 1, MaxValue = 20, Step = 1, Value = 1, Prefix = "敌人≥" };
        row.AddChild(enemyCount);
        OptionButton rarity = AddOptions(row, "稀有度", ["任意", "普通", "精英", "Boss"]);
        var distance = new SpinBox { MinValue = 1, MaxValue = 30, Step = 1, Value = 8, Prefix = "距离≤" };
        row.AddChild(distance);
        var danger = new SpinBox { MinValue = 0, MaxValue = 100, Step = 5, Value = 50, Prefix = "危险度≥" };
        row.AddChild(danger);
        var boss = new CheckBox { Text = "Boss 优先" };
        row.AddChild(boss);
        Button apply = AddButton(row, "应用到下一战斗节点", () =>
        {
            RequireSession().SetHeroAi(new HeroAiConfiguration(
                preset.GetItemText(preset.Selected),
                warCry.ButtonPressed,
                (int)threshold.Value * 100,
                (AiRuleMatchMode)matchMode.Selected,
                (int)enemyCount.Value,
                rarity.GetItemText(rarity.Selected),
                (int)distance.Value,
                boss.ButtonPressed,
                (int)danger.Value));
            Changed("主角 AI 已更新，将从下一战斗节点生效。");
        });
        _heroOnlyControls.Add(apply);
        return page;
    }

    private Control BuildTownPage()
    {
        VBoxContainer page = Page("城镇");
        var workshop = new HFlowContainer();
        page.AddChild(workshop);
        AddButton(workshop, "物理锻造", () => CraftSelected(P2WorkshopRecipe.WeaponPhysical));
        AddButton(workshop, "防具加固", () => CraftSelected(P2WorkshopRecipe.ReinforceDefense));
        AddButton(workshop, "生命刻印", () => CraftSelected(P2WorkshopRecipe.VitalityEtching));
        AddButton(workshop, "监守印记兑换传奇", () =>
            Changed(RequireSession().TryExchangeLegendary() ? "传奇已存入仓库。" : "印记不足或仓库已满。"));
        page.AddChild(new Label { Text = "掉落过滤器 · 有序规则（从上到下首次匹配）" });
        _filterPanel = new P2LootFilterPanel();
        _filterPanel.Initialize(
            RequireSession,
            () => _storageGrid?.SelectedItem ?? _sortingGrid?.SelectedItem,
            Changed);
        page.AddChild(_filterPanel);
        page.AddChild(new Label { Text = "最近操作与掉落（最多 200 条）" });
        _history = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        page.AddChild(_history);
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

    private void BuildContextMenu()
    {
        _itemMenu = new PopupMenu();
        _itemMenu.IdPressed += OnContextAction;
        AddChild(_itemMenu);
        _confirmDialog = new ConfirmationDialog { Title = "确认操作", OkButtonText = "确认" };
        _confirmDialog.Confirmed += () =>
        {
            Action? action = _pendingConfirmation;
            _pendingConfirmation = null;
            action?.Invoke();
        };
        _confirmDialog.Canceled += () => _pendingConfirmation = null;
        AddChild(_confirmDialog);
    }

    private P1ItemGrid BuildGrid(ItemContainerKind kind, int columns, int capacity, float cellSize)
    {
        var grid = new P1ItemGrid { ContainerKind = kind };
        grid.Configure(columns, capacity, cellSize);
        grid.ItemActivated += index => ActivateItem(kind, index);
        grid.ItemContextRequested += (index, position) => OpenItemMenu(kind, index, position);
        grid.QuickTransferRequested += index => Execute(new P2ItemCommandService(RequireSession(), _selectedCharacter).QuickTransfer(kind, index));
        grid.ItemDropped += (source, sourceIndex, targetIndex) => HandleDrop(source, sourceIndex, kind, targetIndex);
        return grid;
    }

    private void ActivateItem(ItemContainerKind kind, int index)
    {
        var commands = new P2ItemCommandService(RequireSession(), _selectedCharacter);
        if (kind == ItemContainerKind.Equipped)
        {
            Execute(commands.TryUnequip((EquipmentSlot)index));
            return;
        }

        if (kind == ItemContainerKind.Buyback)
        {
            Execute(commands.BuyBack(index));
            return;
        }

        if (kind == ItemContainerKind.Recovery)
        {
            Execute(commands.QuickTransfer(kind, index));
            return;
        }

        ItemInstance? item = ItemAt(kind, index);
        if (item is null)
        {
            return;
        }

        Execute(commands.TryEquip(kind, index, PreferredSlot(item)));
    }

    private void HandleDrop(ItemContainerKind source, int sourceIndex, ItemContainerKind target, int targetIndex)
    {
        var commands = new P2ItemCommandService(RequireSession(), _selectedCharacter);
        if (target == ItemContainerKind.Equipped)
        {
            Execute(commands.TryEquip(source, sourceIndex, (EquipmentSlot)targetIndex));
        }
        else if (source == ItemContainerKind.Equipped && target is ItemContainerKind.SortingBag or ItemContainerKind.Storage)
        {
            Execute(commands.TryUnequip((EquipmentSlot)sourceIndex));
        }
        else
        {
            Execute(commands.Move(source, sourceIndex, target, targetIndex));
        }
    }

    private void OpenItemMenu(ItemContainerKind kind, int index, Vector2 screenPosition)
    {
        _contextContainer = kind;
        _contextIndex = index;
        _itemMenu!.Clear();
        if (kind == ItemContainerKind.Equipped)
        {
            _itemMenu.AddItem("卸下", ContextUnequip);
            _itemMenu.AddItem("锁定 / 解锁", ContextLock);
        }
        else if (kind == ItemContainerKind.Buyback)
        {
            _itemMenu.AddItem("按原价回购", ContextBuyback);
        }
        else if (kind == ItemContainerKind.Recovery)
        {
            _itemMenu.AddItem("取出", ContextTransfer);
            _itemMenu.AddItem("锁定 / 解锁", ContextLock);
        }
        else
        {
            _itemMenu.AddItem("装备", ContextEquip);
            _itemMenu.AddItem("移入另一容器", ContextTransfer);
            _itemMenu.AddSeparator();
            _itemMenu.AddItem("出售", ContextSell);
            _itemMenu.AddItem("分解", ContextDismantle);
            _itemMenu.AddSeparator();
            _itemMenu.AddItem("锁定 / 解锁", ContextLock);
            _itemMenu.AddItem("标记 / 取消制作底材", ContextCrafting);
        }

        _itemMenu.Position = new Vector2I((int)screenPosition.X, (int)screenPosition.Y);
        _itemMenu.Popup();
    }

    private void OnContextAction(long id)
    {
        var commands = new P2ItemCommandService(RequireSession(), _selectedCharacter);
        switch (id)
        {
            case ContextEquip:
                ItemInstance? item = ItemAt(_contextContainer, _contextIndex);
                if (item is not null)
                {
                    Execute(commands.TryEquip(_contextContainer, _contextIndex, PreferredSlot(item)));
                }
                break;
            case ContextUnequip:
                Execute(commands.TryUnequip((EquipmentSlot)_contextIndex));
                break;
            case ContextTransfer:
                Execute(commands.QuickTransfer(_contextContainer, _contextIndex));
                break;
            case ContextSell:
                Execute(commands.Sell(_contextContainer, _contextIndex));
                break;
            case ContextDismantle:
                ConfirmDismantle(commands);
                break;
            case ContextLock:
                Execute(commands.ToggleLock(
                    _contextContainer,
                    _contextIndex,
                    _contextContainer == ItemContainerKind.Equipped ? (EquipmentSlot)_contextIndex : null));
                break;
            case ContextCrafting:
                Execute(commands.ToggleCraftingBase(_contextContainer, _contextIndex));
                break;
            case ContextBuyback:
                Execute(commands.BuyBack(_contextIndex));
                break;
        }
    }

    private void ConfirmDismantle(P2ItemCommandService commands)
    {
        P2ItemCommandResult initial = commands.Dismantle(_contextContainer, _contextIndex, confirmed: false);
        if (initial.Code != "confirmation_required")
        {
            Execute(initial);
            return;
        }

        _confirmDialog!.DialogText = "该物品为稀有或传奇品质。确认永久分解？";
        ItemContainerKind container = _contextContainer;
        int index = _contextIndex;
        _pendingConfirmation = () => Execute(commands.Dismantle(container, index, confirmed: true));
        _confirmDialog.PopupCentered();
    }

    private void AddTeamPolicyRow(Container page, ExpeditionTeamKind kind, string label)
    {
        var row = new HFlowContainer();
        page.AddChild(row);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(95, 0) });
        OptionButton route = AddOptions(row, "优先路线", ["安全", "裂渊"]);
        var automatic = new CheckButton { Text = "自动选择", ButtonPressed = true };
        row.AddChild(automatic);
        var stopOnFailure = new CheckButton { Text = "失败后停止" };
        row.AddChild(stopOnFailure);
        OptionButton storageFull = AddOptions(row, "满仓", ["仅收取堆叠物", "停止远征"]);
        var maximumMaps = new SpinBox { MinValue = 0, MaxValue = 1_000, Step = 1, Value = 0, Prefix = "最多图数 " };
        row.AddChild(maximumMaps);
        var reserve = new SpinBox { MinValue = 0, MaxValue = 100_000, Step = 1, Value = 0, Prefix = "保留补给 " };
        row.AddChild(reserve);
        var failures = new SpinBox { MinValue = 0, MaxValue = 100, Step = 1, Value = 0, Prefix = "连败停止 " };
        row.AddChild(failures);
        var freeSlots = new SpinBox { MinValue = 0, MaxValue = 100, Step = 1, Value = 0, Prefix = "最少空格 " };
        row.AddChild(freeSlots);
        AddButton(row, "应用（下一张地图）", () =>
        {
            P1TeamExpeditionState team = Team(kind);
            team.ApplyPolicy(new ExpeditionPolicy(
                automatic.ButtonPressed ? RouteSelectionMode.Automatic : RouteSelectionMode.Manual,
                (MapRoute)route.Selected,
                stopOnFailure.ButtonPressed ? QueueFailureBehavior.Stop : QueueFailureBehavior.Continue,
                storageFull.Selected == 0 ? StorageFullBehavior.AcceptStackablesOnly : StorageFullBehavior.StopExpedition,
                (int)maximumMaps.Value,
                (int)reserve.Value,
                (int)failures.Value,
                (int)freeSlots.Value));
            Changed($"{label}方针已更新。");
        });
        AddButton(row, "继续队列", () =>
        {
            Team(kind).Resume();
            Changed($"{label}已解除停止状态；停止条件会在下一张地图前重新检查。");
        });
    }

    private void AddSupportToggle(Container parent, string text, SkillSupport support)
    {
        var toggle = new CheckButton { Text = text };
        toggle.Toggled += enabled =>
        {
            if (_selectedCharacter != P2CharacterKind.Hero)
            {
                return;
            }

            P1GameSession session = RequireSession();
            string definitionId = support switch
            {
                SkillSupport.IncreasedArea => "core.skill_stone.increased_area",
                SkillSupport.AttackSpeed => "core.skill_stone.attack_speed",
                SkillSupport.Bleed => "core.skill_stone.bleed",
                SkillSupport.LifeCost => "core.skill_stone.life_cost",
                _ => string.Empty,
            };
            SkillStoneInstance active = session.Management.SkillStones.Single(
                item => item.DefinitionId == "core.skill_stone.heavy_strike");
            SkillStoneInstance stone = session.Management.SkillStones.Single(item => item.DefinitionId == definitionId);
            bool changed = enabled
                ? session.Management.TryLinkSupport(active.InstanceId, stone.InstanceId)
                : session.Management.UnlinkSupport(active.InstanceId, stone.InstanceId);
            if (changed)
            {
                session.SyncHeavyStrikeFromSkillStones();
            }

            Changed(changed ? "重击技能石连接已更新。" : "技能石连接没有变化。");
        };
        parent.AddChild(toggle);
        _supportToggles[support] = toggle;
        _heroOnlyControls.Add(toggle);
    }

    private void AllocateSelectedPassive()
    {
        if (_passiveTree?.SelectedStableId is not string stableId)
        {
            Changed("请先选择天赋节点。");
            return;
        }

        Changed(RequireSession().TryAllocatePassive(stableId) ? "天赋已分配。" : "前置节点或可用点数不足。");
    }

    private void RefundSelectedPassive()
    {
        if (_passiveTree?.SelectedStableId is not string stableId)
        {
            Changed("请先选择天赋节点。");
            return;
        }

        Changed(RequireSession().TryRefundPassive(stableId) ? "天赋已退还。" : "存在后续节点或记忆灰烬不足。");
    }

    private void ResetPassives()
    {
        bool changed = RequireSession().TryResetPassives();
        Changed(changed ? "天赋已完整重置。" : "没有可重置节点，或需要 10 个记忆灰烬。");
    }

    private void CraftSelected(P2WorkshopRecipe recipe)
    {
        int index = _storageGrid?.SelectedIndex ?? -1;
        if (index < 0)
        {
            Changed("请先在角色与物品页的仓库中选择一个制作目标。");
            return;
        }

        P2WorkshopPreview preview = P2Workshop.Preview(RequireSession().World.Storage.Items[index], recipe);
        if (!preview.Succeeded)
        {
            Changed($"制作失败：{preview.Summary}");
            return;
        }

        _confirmDialog!.DialogText =
            $"{preview.Summary}\n消耗：{preview.GoldCost} 金币、{preview.IronScrapCost} 铁屑\n确认对 {RequireSession().World.Storage.Items[index].Base.DisplayName} 执行？";
        _pendingConfirmation = () =>
        {
            P2WorkshopPreview result = RequireSession().CraftStorageItem(index, recipe);
            Changed(result.Succeeded ? $"制作完成：{result.Summary}" : $"制作失败：{result.FailureReason}");
        };
        _confirmDialog.PopupCentered();
    }

    private void ConfirmBatch(bool sell)
    {
        P1GameSession session = RequireSession();
        (ItemContainerKind Kind, int Index, ItemInstance Item)[] targets =
            session.World.Storage.Items.Select((item, index) => (ItemContainerKind.Storage, index, item))
                .Concat(session.Management.SortingBag.Select((item, index) => (ItemContainerKind.SortingBag, index, item)))
                .Where(entry => !entry.item.IsLocked && entry.item.Rarity is ItemRarity.Basic or ItemRarity.Magic)
                .Select(entry => (entry.Item1, entry.index, entry.item))
                .ToArray();
        if (targets.Length == 0)
        {
            Changed("没有符合条件的未锁定普通/魔法物品。");
            return;
        }

        int proceeds = sell
            ? targets.Sum(entry => P2ManagementState.SalePrice(entry.Item))
            : targets.Sum(entry => entry.Item.Rarity == ItemRarity.Basic ? 1 : 2);
        _confirmDialog!.DialogText =
            $"将{(sell ? "出售" : "分解")} {targets.Length} 件未锁定普通/魔法物品。\n" +
            $"预计获得 {proceeds} {(sell ? "金币" : "铁屑")}。锁定与已装备物品已排除。\n确认执行？";
        _pendingConfirmation = () =>
        {
            var commands = new P2ItemCommandService(session, _selectedCharacter);
            foreach (IGrouping<ItemContainerKind, (ItemContainerKind Kind, int Index, ItemInstance Item)> group in
                     targets.GroupBy(entry => entry.Kind))
            {
                foreach ((ItemContainerKind kind, int index, _) in group.OrderByDescending(entry => entry.Index))
                {
                    if (sell)
                    {
                        commands.Sell(kind, index);
                    }
                    else
                    {
                        commands.Dismantle(kind, index, confirmed: true);
                    }
                }
            }

            Changed($"批量{(sell ? "出售" : "分解")}完成：{targets.Length} 件。");
        };
        _confirmDialog.PopupCentered();
    }

    private void QuickRoute(ExpeditionTeamKind kind, MapRoute route)
    {
        P1TeamExpeditionState team = Team(kind);
        team.Policy = team.Policy with { PreferredRoute = route };
        Changed($"{kind} 后续路线已设为 {route}。");
    }

    private void SetView(P1ViewMode mode)
    {
        _worldView!.Mode = mode;
        _worldView.QueueRedraw();
    }

    private void Execute(P2ItemCommandResult result) => Changed(result.Message);

    private void Changed(string message)
    {
        RequireSession().Management.AddHistory(message);
        _stateChanged?.Invoke();
        _notice?.Invoke(message);
        Refresh();
    }

    private void Refresh()
    {
        if (_session is null)
        {
            return;
        }

        _worldView!.Session = _session;
        _worldView.QueueRedraw();
        TownEconomyState economy = _session.World.Economy;
        string recoveryWarning = _session.Management.Recovery.Count == 0
            ? string.Empty
            : $" · ⚠ 恢复箱 {_session.Management.Recovery.Count}";
        _overviewStatus!.Text =
            $"{_session.Player.Name} Lv.{_session.World.Hero.Progression.Level} · 佣兵 {_session.MercenaryName} Lv.{_session.World.Mercenaries.Progression.Level}\n" +
            $"补给 {economy.ExpeditionSupplies} · 金币 {economy.Gold} · 铁屑 {economy.IronScraps} · 地图 {_session.World.MapInventory.Count}{recoveryWarning}";
        _expeditionStatus!.Text = TeamText(_session.World.Hero, _session.World) + "\n" +
            TeamText(_session.World.Mercenaries, _session.World) +
            $"\n地图库存 {_session.World.MapInventory.Count} · 模拟速度 {_session.SimulationSpeed}×";

        EquipmentLoadout selectedLoadout = _selectedCharacter == P2CharacterKind.Hero
            ? _session.HeroEquipment
            : _session.MercenaryEquipment;
        P1TeamExpeditionState selectedTeam = _selectedCharacter == P2CharacterKind.Hero
            ? _session.World.Hero
            : _session.World.Mercenaries;
        EquipmentSummary equipment = selectedLoadout.CalculateSummary();
        _characterStatus!.Text = _selectedCharacter == P2CharacterKind.Hero
            ? $"{_session.Player.Name} · {_session.Player.Ascendancy} · Lv.{selectedTeam.Progression.Level}"
            : $"{_session.MercenaryName} · 颂仪者倾向 · Lv.{selectedTeam.Progression.Level}";
        _storageStatus!.Text =
            $"生命 {selectedTeam.Build.Sheet.MaximumLife().Value} · 法力 {selectedTeam.Build.Sheet.MaximumMana().Value} · 护盾 {selectedTeam.Build.Sheet.Equipment.Shield}\n" +
            $"体魄 {selectedTeam.Build.Sheet.Attributes.Physique} · 灵巧 {selectedTeam.Build.Sheet.Attributes.Dexterity} · 精神 {selectedTeam.Build.Sheet.Attributes.Spirit} · 能量 {selectedTeam.Build.Sheet.Attributes.Energy}\n" +
            $"核心槽 {equipment.CoreSkillCapacity} · 连接 {equipment.SupportLinkCapacity}";

        _storageGrid!.SetItems(_session.World.Storage.Items);
        _sortingGrid!.SetItems(_session.Management.SortingBag);
        _recoveryGrid!.SetItems(_session.Management.Recovery.Take(30).ToArray());
        _buybackGrid!.SetItems(_session.Management.Buyback.Select(entry => entry.Item).ToArray());
        ItemInstance?[] slots = Enum.GetValues<EquipmentSlot>()
            .Select(slot => selectedLoadout.Items.GetValueOrDefault(slot))
            .ToArray();
        _equipmentGrid!.SetSlots(slots);
        _passiveTree!.SetState(_session.Passives.Allocated, _session.World.Hero.Progression.EarnedPassivePoints);
        bool heroSelected = _selectedCharacter == P2CharacterKind.Hero;
        foreach (BaseButton control in _heroOnlyControls)
        {
            control.Disabled = !heroSelected;
        }

        foreach ((SkillSupport support, CheckButton toggle) in _supportToggles)
        {
            toggle.SetPressedNoSignal(_session.HeavyStrikeSupports.HasFlag(support));
        }

        _skillSummary!.Text = heroSelected
            ? string.Join("\n", _session.Management.SkillStones.Select(stone =>
                $"◆ {stone.Definition.DisplayName} · {stone.Definition.Kind} · Lv.{stone.Level} XP {stone.Experience}"))
            : $"佣兵技能、辅助、天赋与 AI 由自主成长生成，玩家不可修改。\n{_session.World.Mercenaries.Build.AiSummary}";
        _skillStonePanel?.SetReadOnly(!heroSelected);
        _skillStonePanel?.RefreshState();
        _history!.Text = string.Join('\n', _session.Management.OperationHistory.TakeLast(200).Select(item => $"• {item}"));
        _filterPanel?.RefreshRules();
        _mapQueuePanel?.RefreshState();
        _campaignRoute?.RefreshState();
        CampaignNodeDefinition? currentNode = _session.Campaign.CurrentNode;
        _storyStatus!.Text = _session.Campaign.Completed
            ? "五幕主线已完成\n远征功能已开放。"
            : $"第 {currentNode!.Act} 幕 · {P2CampaignCatalog.ActNames[currentNode.Act - 1]}\n" +
              $"当前：{currentNode.DisplayName}（{currentNode.Kind}）\n" +
              $"进度 {_session.Campaign.CurrentNodeElapsedMilliseconds / 1_000}/{currentNode.DurationMilliseconds / 1_000}s\n" +
              (_session.Campaign.Defeated ? "⚠ 战败：调整构筑后点击继续。" : "自动推进中；离线时间同样有效。 ");
        _storyLog!.Text = string.Join('\n', _session.Campaign.StoryLog.TakeLast(60).Select(item => $"• {item}"));
        int expeditionIndex = _mainTabs!.GetTabIdxFromControl(_expeditionPage!);
        _mainTabs.SetTabHidden(expeditionIndex, !_session.IsExpeditionUnlocked);
        _miniStatus!.Text =
            $"{_session.Player.Name} Lv.{_session.World.Hero.Progression.Level}  主角[{CompactTeam(_session.World.Hero)}]\n" +
            $"佣兵[{CompactTeam(_session.World.Mercenaries)}] · 补给 {economy.ExpeditionSupplies} · 图 {_session.World.MapInventory.Count}";
    }

    private ItemInstance? ItemAt(ItemContainerKind kind, int index) => kind switch
    {
        ItemContainerKind.Storage when index >= 0 && index < RequireSession().World.Storage.Items.Count =>
            RequireSession().World.Storage.Items[index],
        ItemContainerKind.SortingBag when index >= 0 && index < RequireSession().Management.SortingBag.Count =>
            RequireSession().Management.SortingBag[index],
        ItemContainerKind.Recovery when index >= 0 && index < RequireSession().Management.Recovery.Count =>
            RequireSession().Management.Recovery[index],
        ItemContainerKind.Equipped => (_selectedCharacter == P2CharacterKind.Hero
            ? RequireSession().HeroEquipment
            : RequireSession().MercenaryEquipment).Items.GetValueOrDefault((EquipmentSlot)index),
        _ => null,
    };

    private static EquipmentSlot PreferredSlot(ItemInstance item) => item.Base.Category switch
    {
        ItemCategory.Ring => EquipmentSlot.RingLeft,
        ItemCategory.LifeFlask => EquipmentSlot.Flask1,
        _ => item.Base.PrimarySlot,
    };

    private string EquipmentComparisonText(ItemInstance item)
    {
        if (_session is null || _selectedCharacter != P2CharacterKind.Hero)
        {
            return _selectedCharacter == P2CharacterKind.Mercenary
                ? "佣兵换装：最终面板会在装备后重新计算。"
                : string.Empty;
        }

        try
        {
            EquipmentSlot slot = PreferredSlot(item);
            P2EquipmentComparison compare = _session.CompareHeroEquipment(item, slot);
            static string Delta(int value) => value >= 0 ? $"+{value}" : value.ToString();
            return $"假设装备到 {slot}\n" +
                $"平均命中 {Delta(compare.AverageHitDelta)} · 有效生命 {Delta(compare.EffectiveLifeDelta)}\n" +
                $"生命 {Delta(compare.MaximumLifeDelta)} · 法力 {Delta(compare.MaximumManaDelta)} · " +
                $"护甲 {Delta(compare.ArmorDelta)} · 闪避 {Delta(compare.EvasionDelta)} · 护盾 {Delta(compare.ShieldDelta)}\n" +
                $"核心槽 {Delta(compare.CoreCapacityDelta)} · 连接 {Delta(compare.LinkCapacityDelta)}" +
                (compare.DisabledSkillLinks == 0 ? string.Empty : $" · ⚠ 将禁用 {compare.DisabledSkillLinks} 个辅助连接");
        }
        catch
        {
            return "该物品无法生成当前构筑的换装对比。";
        }
    }

    private P1GameSession RequireSession() => _session ?? throw new InvalidOperationException("请先创建角色。");

    private P1TeamExpeditionState Team(ExpeditionTeamKind kind) =>
        kind == ExpeditionTeamKind.Hero ? RequireSession().World.Hero : RequireSession().World.Mercenaries;

    private static string TeamText(P1TeamExpeditionState team, P1WorldState world) =>
        $"{team.Kind}: 队列 {team.Queue.Count}/10 · 完成 {team.MapsCompleted} · 失败 {team.MapsFailed} · " +
        (team.ActiveMap is null
            ? team.IsStopped ? $"停止：{team.StopReason}" : "等待资源/地图"
            : $"进行中 {team.ActiveMap.InstanceId}，剩余 {team.RemainingMapTimeMilliseconds / 1_000}s") +
        $" · 停止条件[图数 {DisplayLimit(team.Policy.MaximumContinuousMaps)} / 补给保留 {DisplayLimit(team.Policy.ReserveSupplies)} / " +
        $"连败 {DisplayLimit(team.Policy.StopAfterConsecutiveFailures)} / 空格 {DisplayLimit(team.Policy.MinimumStorageFreeSlots)}]" +
        (team.PendingPolicy is null ? string.Empty : " · 新方针将在下一张地图生效") +
        $" · 预计最早：{EstimateFirstStop(team, world)}";

    private static string DisplayLimit(int value) => value == 0 ? "关" : value.ToString();

    private static string EstimateFirstStop(P1TeamExpeditionState team, P1WorldState world)
    {
        var candidates = new List<(int Maps, string Reason)>();
        if (team.Policy.MaximumContinuousMaps > 0)
        {
            candidates.Add((Math.Max(0, team.Policy.MaximumContinuousMaps - team.MapsRunSincePolicyApplied), "连续图数"));
        }

        if (team.Policy.ReserveSupplies > 0)
        {
            candidates.Add((Math.Max(0, world.Economy.ExpeditionSupplies - team.Policy.ReserveSupplies), "补给保留"));
        }

        if (team.Policy.StopAfterConsecutiveFailures > 0)
        {
            candidates.Add((Math.Max(0, team.Policy.StopAfterConsecutiveFailures - team.ConsecutiveFailures), "连败"));
        }

        if (team.Policy.MinimumStorageFreeSlots > 0 &&
            world.Storage.Capacity - world.Storage.Count < team.Policy.MinimumStorageFreeSlots)
        {
            candidates.Add((0, "仓库空格"));
        }

        if (candidates.Count == 0)
        {
            return "未启用可估算停止条件";
        }

        (int maps, string reason) = candidates
            .OrderBy(item => item.Maps)
            .ThenBy(item => item.Reason, StringComparer.Ordinal)
            .First();
        return $"约 {maps} 张后（{reason}）";
    }

    private static string CompactTeam(P1TeamExpeditionState team) => team.ActiveMap is null
        ? team.IsStopped ? "停止" : $"等待 Q{team.Queue.Count}"
        : $"{team.ActiveRoute} {team.RemainingMapTimeMilliseconds / 1_000}s";

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

    private static Button AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }
}

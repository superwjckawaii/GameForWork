using GameForWork.Core.Offline;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P4;
using GameForWork.Core.P5;
using GameForWork.Core.P6;
using GameForWork.Core.P9;
using GameForWork.Core.P10;
using GameForWork.Core.P16;
using GameForWork.Core.P18;
using GameForWork.Core.P20;
using GameForWork.Core.P23;
using Godot;
using System.Security.Cryptography;
using System.Text.Json;

namespace GameForWork.GodotClient;

public partial class P2Dashboard : VBoxContainer
{
    private static readonly Vector2I CharacterWindowPreferredSize = new(1180, 800);
    private static readonly Vector2I CharacterWindowMinimumSize = new(900, 640);
    private const int CharacterWindowGap = 12;
    private const int ContextEquip = 1;
    private const int ContextUnequip = 2;
    private const int ContextTransfer = 3;
    private const int ContextSell = 4;
    private const int ContextDismantle = 5;
    private const int ContextLock = 6;
    private const int ContextCrafting = 7;
    private const int ContextBuyback = 8;

    private readonly List<BaseButton> _heroOnlyControls = [];
    private P1GameSession? _session;
    private Action<PlayerIdentity, bool>? _createCharacter;
    private Action? _stateChanged;
    private Action<string>? _notice;
    private Action? _expandWindow;
    private Control? _creationPanel;
    private Control? _fullPanel;
    private VBoxContainer? _miniPanel;
    private TabContainer? _mainTabs;
    private Control? _overviewPage;
    private Control? _storyPage;
    private Control? _characterPage;
    private Window? _characterWindow;
    private Button? _characterButton;
    private bool _characterWindowPairInitialized;
    private bool _syncingCharacterWindowPair;
    private Vector2I _pairedMainPosition;
    private Vector2I _pairedMainSize;
    private Vector2I _pairedCharacterPosition;
    private Control? _townPage;
    private Control? _expeditionPage;
    private TabContainer? _characterModes;
    private Control? _equipmentMode;
    private Control? _skillMode;
    private Control? _passiveMode;
    private Control? _aiMode;
    private Control? _metalMode;
    private TabContainer? _characterSidebar;
    private P205JewelStashPanel? _jewelStashPanel;
    private Label? _bossFragmentsStatus;
    private Label? _miniStatus;
    private Label? _overviewStatus;
    private Label? _characterStatus;
    private Label? _storageStatus;
    private Label? _selectedPassive;
    private Label? _craftingStatus;
    private string _storageSearch = string.Empty;
    private RichTextLabel? _history;
    private RichTextLabel? _storyLog;
    private Label? _storyStatus;
    private P2CampaignRouteView? _campaignRoute;
    private P1WorldView? _worldView;
    private P1PassiveTreeView? _passiveTree;
    private P1ItemGrid? _storageGrid;
    private P1ItemGrid? _sortingGrid;
    private P3EquipmentPaperDoll? _equipmentGrid;
    private P1ItemGrid? _recoveryGrid;
    private P1ItemGrid? _buybackGrid;
    private P1ItemGrid? _heroLootGrid;
    private P1ItemGrid? _mercenaryLootGrid;
    private P2LootFilterPanel? _filterPanel;
    private P2SkillStonePanel? _skillStonePanel;
    private P5ExpeditionPanel? _expeditionPanel;
    private EquipmentCraftingPanel? _metalPanel;
    private P9TownPanel? _townPanel;
    private P10EndgamePanel? _endgamePanel;
    private P18AscendancyPanel? _ascendancyPanel;
    private OptionButton? _characterSelector;
    private PopupMenu? _itemMenu;
    private ConfirmationDialog? _confirmDialog;
    private OptionButton? _batchRarity;
    private OptionButton? _batchScope;
    private ItemContainerKind _contextContainer;
    private int _contextIndex = -1;
    private ItemContainerKind _craftContainer = ItemContainerKind.Storage;
    private int _craftIndex = -1;
    private Action? _pendingConfirmation;
    private P2CharacterKind _selectedCharacter;
    private string _selectedMercenaryId = string.Empty;
    private string _characterSelectorSignature = string.Empty;
    private double _refreshAccumulator;
    public double LastRefreshMilliseconds { get; private set; }
    public double PeakRefreshMilliseconds { get; private set; }
    private bool _miniMode;
    private Label? _journeyStatus;
    private Button? _journeyGo;
    private Button? _miniJourney;
    private HBoxContainer? _warningBar;
    private Label? _warningText;
    private Button? _warningGo;
    private AcceptDialog? _journeyDialog;
    private AcceptDialog? _handbookDialog;
    private AcceptDialog? _completionDialog;
    private P8JourneyDestination _pendingDestination;
    private P8JourneyDestination _warningDestination;
    private int _lastJourneyStepIndex = -1;

    public void Initialize(
        P1GameSession? session,
        Action<PlayerIdentity, bool> createCharacter,
        Action stateChanged,
        Action<string> notice,
        Action expandWindow)
    {
        _session = session;
        _createCharacter = createCharacter;
        _stateChanged = stateChanged;
        _notice = notice;
        _expandWindow = expandWindow;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        Build();
        Refresh();
    }

    public void SetSession(P1GameSession? session)
    {
        _session = session;
        _miniMode = false;
        _lastJourneyStepIndex = -1;
        _creationPanel!.Visible = session is null;
        _fullPanel!.Visible = session is not null;
        _miniPanel!.Visible = false;
        if (session is null)
        {
            _characterWindow?.Hide();
            _characterWindowPairInitialized = false;
        }
        Refresh();
    }

    public void SetMiniMode(bool mini)
    {
        if (_session is null || _miniMode == mini)
        {
            return;
        }

        _miniMode = mini;
        if (mini)
        {
            _characterWindow?.Hide();
            _characterWindowPairInitialized = false;
        }
        _fullPanel!.Visible = !mini;
        _miniPanel!.Visible = mini;
        Refresh();
    }

    public void Tick(double delta)
    {
        SyncCharacterWindowPair();
        _refreshAccumulator += delta;
        if (_refreshAccumulator >= 0.25)
        {
            _refreshAccumulator = 0;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            Refresh();
            LastRefreshMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            PeakRefreshMilliseconds = Math.Max(PeakRefreshMilliseconds, LastRefreshMilliseconds);
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
            P1OfflineResult result = _session.AdvanceResponsive(realMilliseconds);
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
        var name = new LineEdit { PlaceholderText = "角色名（2～16 字）", Text = "冒险者" };
        card.AddChild(name);
        OptionButton gender = AddOptions(card, "性别", ["女性", "男性", "中性"]);
        OptionButton skin = AddOptions(card, "肤色", ["苍白", "浅色", "棕褐", "深色"]);
        OptionButton hair = AddOptions(card, "发型", ["短发", "长发", "编发", "剃发"]);
        P23ClassDefinition[] classes = P23ClassCatalog.All.ToArray();
        OptionButton baseClass = AddOptions(card, "基础职业", classes.Select(value => value.DisplayName).ToArray());
        var classSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        card.AddChild(classSummary);
        void RefreshClassSummary(long index)
        {
            P23ClassDefinition definition = classes[(int)index];
            classSummary.Text = $"{definition.DisplayName}：{definition.Summary}\n" +
                                $"初始属性 {definition.StartingAttributes.Physique}/{definition.StartingAttributes.Dexterity}/" +
                                $"{definition.StartingAttributes.Spirit}/{definition.StartingAttributes.Energy} · " +
                                $"升华：{string.Join("、", definition.Ascendancies.Select(P18AscendancyCatalog.DisplayName))}\n" +
                                "基础职业创建后不能更换；升华在旅程中选择。";
        }
        baseClass.ItemSelected += RefreshClassSummary;
        RefreshClassSummary(0);
        var skipTutorial = new CheckBox
        {
            Text = "跳过首次引导（创建后不能重新开启）",
            TooltipText = "仍会保留 Demo 主目标，但不再弹出强制聚焦教学。",
        };
        card.AddChild(skipTutorial);
        AddButton(card, "确认创建并进入余烬营地", () =>
        {
            try
            {
                _createCharacter?.Invoke(new PlayerIdentity(
                    name.Text,
                    (CharacterGender)gender.Selected,
                    (CharacterSkinTone)skin.Selected,
                    (CharacterHairStyle)hair.Selected,
                    classes[baseClass.Selected].Id).Validate(), !skipTutorial.ButtonPressed);
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
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        var journeyBar = new HBoxContainer();
        root.AddChild(journeyBar);
        journeyBar.AddChild(new Label { Text = "旅程目标" });
        _journeyStatus = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        journeyBar.AddChild(_journeyStatus);
        _journeyGo = AddButton(journeyBar, "前往", NavigateToCurrentJourney);
        _characterButton = AddButton(journeyBar, "角色与物品", OpenCharacterWindow);
        AddButton(journeyBar, "旅程手册", ShowHandbook);
        AddButton(journeyBar, "重播教学", ReplayTutorial);
        _warningBar = new HBoxContainer { Visible = false };
        root.AddChild(_warningBar);
        _warningText = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color("f0b36a") };
        _warningBar.AddChild(_warningText);
        _warningGo = AddButton(_warningBar, "前往处理", () => Navigate(_warningDestination));
        _mainTabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_mainTabs);
        _overviewPage = BuildOverviewPage();
        _mainTabs.AddChild(_overviewPage);
        _storyPage = BuildStoryPage();
        _mainTabs.AddChild(_storyPage);
        _expeditionPage = BuildExpeditionPage();
        _mainTabs.AddChild(_expeditionPage);
        _characterPage = BuildCharacterItemsPage();
        _characterPage.SizeFlagsVertical = SizeFlags.ExpandFill;
        _characterWindow = new Window
        {
            Title = "角色与物品",
            Size = CharacterWindowPreferredSize,
            MinSize = CharacterWindowMinimumSize,
            Visible = false,
            ForceNative = true,
            Transient = true,
            Exclusive = false,
            WrapControls = false,
            Borderless = true,
        };
        void CloseCharacterWindow()
        {
            _characterWindow.Hide();
            _characterWindowPairInitialized = false;
        }
        _characterWindow.CloseRequested += CloseCharacterWindow;
        var characterFrame = new PanelContainer();
        characterFrame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        characterFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0e1118"),
            BorderColor = new Color("8f7043"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        });
        var characterContent = new VBoxContainer();
        characterContent.AddThemeConstantOverride("separation", 0);
        characterFrame.AddChild(characterContent);
        var characterTitleBar = new P3SecondaryTitleBar();
        characterTitleBar.Initialize(_characterWindow, "角色与物品", CloseCharacterWindow);
        characterContent.AddChild(characterTitleBar);
        characterContent.AddChild(_characterPage);
        _characterWindow.AddChild(characterFrame);
        AddChild(_characterWindow);
        _townPage = BuildTownPage();
        _mainTabs.AddChild(_townPage);
        BuildJourneyDialogs();
        return root;
    }

    private Control BuildOverviewPage()
    {
        VBoxContainer page = Page("总览");
        var controls = new HFlowContainer();
        page.AddChild(controls);
        AddButton(controls, "观察当前战斗", () => SetView(P1ViewMode.Active));
        AddButton(controls, "观察主角", () => SetView(P1ViewMode.Hero));
        AddButton(controls, "观察佣兵", () => SetView(P1ViewMode.Mercenaries));
        _worldView = new P1WorldView
        {
            Session = _session,
            Mode = P1ViewMode.Active,
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
            CustomMinimumSize = new Vector2(0, 82),
            ScrollActive = true,
            ScrollFollowing = true,
        };
        var storyLogFrame = new PanelContainer { CustomMinimumSize = new Vector2(0, 92) };
        storyLogFrame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0d1219"), BorderColor = new Color("4b5665"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 5, ContentMarginTop = 3, ContentMarginRight = 7, ContentMarginBottom = 5,
        });
        storyLogFrame.AddChild(_storyLog);
        page.AddChild(storyLogFrame);
        return page;
    }

    private Control BuildExpeditionPage()
    {
        VBoxContainer page = Page("远征");
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddChild(tabs);
        var dispatch = new VBoxContainer { Name = "远征派遣", SizeFlagsVertical = SizeFlags.ExpandFill };
        tabs.AddChild(dispatch);
        _expeditionPanel = new P5ExpeditionPanel
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _expeditionPanel.Initialize(RequireSession, Changed);
        _expeditionPanel.ReportsViewed += () =>
        {
            RequireSession().RecordJourneyEvent(P8JourneyEvent.ViewedCombatReport);
            _stateChanged?.Invoke();
            Refresh();
        };
        dispatch.AddChild(_expeditionPanel);
        var endgameTab = new VBoxContainer { Name = "异界与突破", SizeFlagsVertical = SizeFlags.ExpandFill };
        _endgamePanel = new P10EndgamePanel { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _endgamePanel.Initialize(RequireSession, Changed);
        endgameTab.AddChild(_endgamePanel); tabs.AddChild(endgameTab);
        return Wrap(page);
    }

    private Control BuildCharacterItemsPage()
    {
        VBoxContainer page = Page("角色与物品");
        var header = new HFlowContainer();
        page.AddChild(header);
        _characterSelector = AddOptions(header, "当前角色", ["主角"]);
        _characterSelector.ItemSelected += index =>
        {
            _selectedCharacter = index == 0 ? P2CharacterKind.Hero : P2CharacterKind.Mercenary;
            _selectedMercenaryId = index == 0 || index - 1 >= RequireSession().Town.Roster.Count
                ? string.Empty : RequireSession().Town.Roster[(int)index - 1].Identity.StableId;
            Refresh();
        };
        var collapseSidebar = new Button
        {
            Text = "收起装备侧栏",
            FocusMode = FocusModeEnum.None,
            TooltipText = "收起常驻装备备栏，为技能、天赋和仓库腾出空间",
        };
        header.AddChild(collapseSidebar);
        AddButton(header, "撤销移动", () => Execute(
            ItemCommands().UndoLastMovement()));
        _characterStatus = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        page.AddChild(_characterStatus);

        var workspace = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        workspace.AddThemeConstantOverride("separation", 12);
        page.AddChild(workspace);
        _characterModes = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        workspace.AddChild(_characterModes);
        _equipmentMode = BuildEquipmentMode();
        _characterModes.AddChild(_equipmentMode);
        _skillMode = BuildSkillMode();
        _characterModes.AddChild(_skillMode);
        _passiveMode = BuildPassiveMode();
        _characterModes.AddChild(_passiveMode);
        _aiMode = BuildAiMode();
        _characterModes.AddChild(_aiMode);
        _characterModes.TabChanged += index =>
        {
            if (_skillMode is not null && _characterModes.GetTabControl((int)index) == _skillMode)
            {
                RequireSession().RecordJourneyEvent(P8JourneyEvent.InspectedSkills);
                _stateChanged?.Invoke();
                Refresh();
            }
            if (_passiveMode is not null && _characterModes.GetTabControl((int)index) == _passiveMode && _characterSidebar is not null)
            {
                _characterSidebar.CurrentTab = 2;
                _characterSidebar.CustomMinimumSize = new Vector2(440, 0);
            }
            else if (_characterSidebar is not null)
                _characterSidebar.CustomMinimumSize = new Vector2(280, 0);
        };
        var sidebar = new TabContainer { CustomMinimumSize = new Vector2(280, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        _characterSidebar = sidebar;
        workspace.AddChild(sidebar);
        var equipmentScroll = new ScrollContainer { Name = "装备", SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        sidebar.AddChild(equipmentScroll);
        var equipment = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        equipmentScroll.AddChild(equipment);
        _metalMode = BuildMetalMode();
        _metalMode.Name = "打造";
        sidebar.AddChild(_metalMode);
        _jewelStashPanel = new P205JewelStashPanel { Name = "珠宝", SizeFlagsVertical = SizeFlags.ExpandFill };
        _jewelStashPanel.Initialize(RequireSession, Changed);
        sidebar.AddChild(_jewelStashPanel);
        sidebar.AddChild(BuildBossFragmentMode());
        collapseSidebar.Pressed += () =>
        {
            sidebar.Visible = !sidebar.Visible;
            collapseSidebar.Text = sidebar.Visible ? "收起装备侧栏" : "展开装备侧栏";
        };
        equipment.AddChild(new Label { Text = "角色装备备栏 · 所有模式常驻" });
        _equipmentGrid = new P3EquipmentPaperDoll();
        _equipmentGrid.ItemActivated += index => ActivateItem(ItemContainerKind.Equipped, index);
        _equipmentGrid.ItemSelected += index => SelectCraftTarget(ItemContainerKind.Equipped, index);
        _equipmentGrid.ItemContextRequested += (index, position) => OpenItemMenu(ItemContainerKind.Equipped, index, position);
        _equipmentGrid.QuickTransferRequested += index =>
            Execute(ItemCommands()
                .QuickTransfer(ItemContainerKind.Equipped, index));
        _equipmentGrid.ItemDropped += (source, sourceIndex, targetIndex) =>
            HandleDrop(source, sourceIndex, ItemContainerKind.Equipped, targetIndex);
        _equipmentGrid.DropValidator = CanDropOnEquipment;
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
        var storageSearch = new LineEdit { PlaceholderText = "搜索仓库：名称 / 类型 / 稀有度 / 连接数（如 5连）" };
        storageSearch.TextChanged += query =>
        {
            _storageSearch = query.Trim();
            Refresh();
        };
        containers.AddChild(storageSearch);
        _storageGrid = BuildGrid(ItemContainerKind.Storage, 10, EquipmentStorage.InitialCapacity, 32);
        containers.AddChild(_storageGrid);
        var batch = new HFlowContainer();
        containers.AddChild(batch);
        _batchRarity = AddOptions(batch, "最高稀有度", ["基础及以下", "魔法及以下", "稀有及以下", "传奇及以下"]);
        _batchRarity.Select((int)ItemRarity.Magic);
        _batchScope = AddOptions(batch, "范围", ["整理背包", "仓库", "两者"]);
        _batchScope.Select((int)P16BatchScope.Storage);
        AddButton(batch, "批量出售", () => ConfirmBatch(P16BatchAction.Sell));
        AddButton(batch, "批量分解", () => ConfirmBatch(P16BatchAction.Dismantle));
        AddButton(batch, "按连接数整理", () => SortItems(P16ItemSortMode.LinkedSockets));
        AddButton(batch, "按稀有度整理", () => SortItems(P16ItemSortMode.Rarity));
        AddButton(batch, "按物等整理", () => SortItems(P16ItemSortMode.ItemLevel));
        AddButton(batch, "按底材整理", () => SortItems(P16ItemSortMode.Base));
        AddButton(batch, "按最高T级整理", () => SortItems(P16ItemSortMode.HighestAffixTier));

        body.AddChild(new Label { Text = "装备制作与附魔已集中到“打造”页；选择物品后切换该页操作。" });
        _craftingStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_craftingStatus);

        var safetyTabs = new TabContainer { CustomMinimumSize = new Vector2(0, 210) };
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

    private Control BuildMetalMode()
    {
        _metalPanel = new EquipmentCraftingPanel { Name = "打造", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _metalPanel.Initialize(RequireSession, CurrentCraftTarget, Changed);
        return _metalPanel;
    }

    private Control BuildBossFragmentMode()
    {
        var scroll = new ScrollContainer
        {
            Name = "Boss 碎片",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(body);
        body.AddChild(new Label
        {
            Text = "Boss 碎片仓",
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        body.AddChild(new Label
        {
            Text = "碎片集齐后自动合成为门票；门票在主角派遣页选择 Boss 挑战时消耗。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("111720"), BorderColor = new Color("786747"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 10, ContentMarginTop = 9, ContentMarginRight = 10, ContentMarginBottom = 9,
        });
        _bossFragmentsStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        frame.AddChild(_bossFragmentsStatus);
        body.AddChild(frame);
        return scroll;
    }

    private Control BuildSkillMode()
    {
        VBoxContainer page = Page("技能");
        _skillStonePanel = new P2SkillStonePanel();
        _skillStonePanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _skillStonePanel.Initialize(RequireSession, Changed);
        page.AddChild(_skillStonePanel);
        return page;
    }

    private Control BuildPassiveMode()
    {
        VBoxContainer page = Page("天赋");
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddChild(tabs);
        var main = new Control
        {
            Name = "主天赋",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        tabs.AddChild(main);
        OptionButton? mastery = null;
        _passiveTree = new P1PassiveTreeView { MouseFilter = MouseFilterEnum.Stop };
        _passiveTree.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _passiveTree.NodeSelected += stableId =>
        {
            PassiveNodeDefinition node = P1PassiveTree.Get(stableId);
            string selectedEffect = !string.IsNullOrWhiteSpace(node.SpecialRule)
                ? node.SpecialRule
                : string.Join("；", node.Effects.Select(P1UiText.PassiveEffect));
            _selectedPassive!.Text = $"已选择：{node.DisplayName} · {selectedEffect}";
            mastery!.Clear();
            IReadOnlyList<PassiveEffect> options = P1PassiveTree.MasteryOptions(node);
            IReadOnlyList<string> optionDescriptions = P1PassiveTree.MasteryOptionDescriptions(node);
            for (int index = 0; index < options.Count; index++) mastery.AddItem(optionDescriptions[index], index);
            mastery.Disabled = options.Count == 0;
        };
        _passiveTree.NodeAllocateRequested += stableId =>
        {
            if (_selectedCharacter != P2CharacterKind.Hero)
            {
                Changed("佣兵天赋由自主成长决定，不能手动分配。");
                return;
            }

            Changed(RequireSession().TryAllocatePassive(stableId)
                ? "天赋已分配。"
                : "节点不可达或可用天赋点不足。");
        };
        _passiveTree.NodeRefundRequested += stableId =>
        {
            if (_selectedCharacter != P2CharacterKind.Hero)
            {
                Changed("佣兵天赋由自主成长决定，不能手动洗点。");
                return;
            }

            Changed(RequireSession().TryRefundPassive(stableId)
                ? "天赋已退还。"
                : "洗点会切断已分配路径，或记忆灰烬不足。");
        };
        _passiveTree.JewelDropRequested += (stableId, instanceId) =>
        {
            bool changed = RequireSession().TrySocketP30Jewel(stableId, instanceId, out string reason);
            Changed(changed ? "珠宝已从珠宝仓拖入棱孔。" : reason);
        };
        main.AddChild(_passiveTree);

        var overlay = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10 };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        main.AddChild(overlay);
        var header = new VBoxContainer();
        var search = new LineEdit
        {
            PlaceholderText = "搜索天赋名称或效果；规划不会消耗点数",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        search.TextChanged += query => _passiveTree?.SetSearch(query);
        header.AddChild(search);
        header.AddChild(new Label { Text = "铁誓星盘 · 1,475 节点 · 左键拖曳 / 滚轮缩放 · 双击分配 · 右键双击洗点" });
        overlay.AddChild(TreeHud(header));
        overlay.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        var footer = new VBoxContainer();
        var row = new HFlowContainer();
        footer.AddChild(row);
        _selectedPassive = new Label
        {
            Text = "单击查看 · 左键双击加点 · 右键双击洗点（不会切断已分配路径）",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddChild(_selectedPassive);
        Button allocate = AddButton(row, "分配", AllocateSelectedPassive);
        AddButton(row, "规划最短路径", () => Changed(_passiveTree?.PlanPathToSelected() == true
            ? $"已规划最短路径，共需 {_passiveTree.PlannedCost} 点；未消耗天赋点。"
            : "请先选择目标节点。"));
        Button allocatePath = AddButton(row, "按规划分配", () =>
        {
            if (_selectedCharacter != P2CharacterKind.Hero)
            {
                Changed("佣兵天赋由自主成长决定，不能手动分配。");
                return;
            }
            string? id = _passiveTree?.SelectedStableId;
            Changed(id is not null && RequireSession().TryAllocatePassivePath(id)
                ? "最短路径已一次性分配。" : "可用点数不足，或目标已分配。");
        });
        AddButton(row, "回到起点", () => _passiveTree?.CenterOnStart());
        AddButton(row, "全图", () => _passiveTree?.FitAll());
        AddButton(row, "清除规划", () =>
        {
            _passiveTree?.ClearPlan();
            Changed("天赋规划已清除。");
        });
        Button refund = AddButton(row, "退还", RefundSelectedPassive);
        Button reset = AddButton(row, "完整重置", ResetPassives);
        _heroOnlyControls.AddRange([allocate, allocatePath, refund, reset]);
        var specialization = new HFlowContainer();
        footer.AddChild(specialization);
        mastery = new OptionButton { TooltipText = "分配专精节点后，从该类别七个已确认效果中选择一个；同类专精效果全局唯一。", Disabled = true };
        specialization.AddChild(mastery);
        Button chooseMastery = AddButton(specialization, "选择专精效果", () =>
        {
            string? id = _passiveTree?.SelectedStableId;
            Changed(id is not null && RequireSession().TrySelectMastery(id, mastery.Selected)
                ? "专精效果已切换。" : "请先分配并选中一个专精节点。");
        });
        Button unsocketJewel = AddButton(specialization, "取下珠宝", () =>
        {
            string? id = _passiveTree?.SelectedStableId;
            Changed(id is not null && RequireSession().TryUnsocketP30Jewel(id) ? "珠宝已取下。" : "该棱孔没有珠宝。");
        });
        _heroOnlyControls.AddRange([chooseMastery, unsocketJewel]);
        overlay.AddChild(TreeHud(footer));
        _ascendancyPanel = new P18AscendancyPanel { Name = "升华", SizeFlagsVertical = SizeFlags.ExpandFill };
        _ascendancyPanel.Initialize(RequireSession, Changed);
        tabs.AddChild(_ascendancyPanel);
        return page;
    }

    private Control BuildAiMode()
    {
        VBoxContainer page = Page("AI");
        page.AddChild(new Label
        {
            Text = "规则只支持一层“全部满足 / 任一满足”。AI 可读取敌人数量、稀有度和威胁等级，不读取隐藏掉落结果。",
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
        var danger = new SpinBox { MinValue = 0, MaxValue = 100, Step = 5, Value = 50, Prefix = "威胁等级≥" };
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
        VBoxContainer page = Page("城镇事务");
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddChild(tabs);
        VBoxContainer management = Page("城区运营");
        tabs.AddChild(management);
        _townPanel = new P9TownPanel { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _townPanel.Initialize(RequireSession, Changed);
        management.AddChild(_townPanel);
        VBoxContainer loot = Page("掉落与日志");
        tabs.AddChild(loot);
        var workshop = new HFlowContainer();
        loot.AddChild(workshop);
        OptionButton legendary = AddOptions(workshop, "指定传奇", P20LegendaryDrops.ExchangePool.Select(item => item.DisplayName).ToArray());
        AddButton(workshop, $"{P20LegendaryDrops.PityMarkCost} 印记兑换", () =>
        {
            string id = P20LegendaryDrops.ExchangePool[legendary.Selected].StableId;
            Changed(RequireSession().TryExchangeLegendary(id) ? "指定传奇已存入仓库。" : "印记不足或仓库已满。");
        });
        loot.AddChild(new Label { Text = "掉落过滤器 · 有序规则（从上到下首次匹配）" });
        _filterPanel = new P2LootFilterPanel();
        _filterPanel.Initialize(
            RequireSession,
            () => _storageGrid?.SelectedItem ?? _sortingGrid?.SelectedItem,
            Changed);
        loot.AddChild(_filterPanel);
        loot.AddChild(new Label { Text = "最近操作与掉落（最多 200 条）" });
        _history = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        loot.AddChild(_history);
        return page;
    }

    private EquipmentCraftTarget? CurrentCraftTarget()
    {
        if (_craftIndex < 0) return null;
        ItemInstance? item = _craftContainer switch
        {
            ItemContainerKind.Storage when _craftIndex < RequireSession().World.Storage.Items.Count => RequireSession().World.Storage.Items[_craftIndex],
            ItemContainerKind.SortingBag when _craftIndex < RequireSession().Management.SortingBag.Count => RequireSession().Management.SortingBag[_craftIndex],
            ItemContainerKind.Equipped => SelectedLoadout()
                .Items.GetValueOrDefault((EquipmentSlot)_craftIndex),
            _ => null,
        };
        return item is null ? null : new EquipmentCraftTarget(_craftContainer, _craftIndex, item, _selectedCharacter, _selectedMercenaryId);
    }

    private VBoxContainer BuildMiniPanel()
    {
        var panel = new VBoxContainer { Visible = false, SizeFlagsVertical = SizeFlags.ExpandFill };
        _miniStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _miniStatus.AddThemeFontSizeOverride("font_size", 13);
        panel.AddChild(_miniStatus);
        _miniJourney = new Button { Text = "当前目标：等待角色创建", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _miniJourney.Pressed += () =>
        {
            _expandWindow?.Invoke();
            NavigateToCurrentJourney();
        };
        panel.AddChild(_miniJourney);
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
        _characterWindow!.AddChild(_itemMenu);
        _confirmDialog = new ConfirmationDialog { Title = "确认操作", OkButtonText = "确认" };
        _confirmDialog.Confirmed += () =>
        {
            Action? action = _pendingConfirmation;
            _pendingConfirmation = null;
            action?.Invoke();
        };
        _confirmDialog.Canceled += () => _pendingConfirmation = null;
        _characterWindow.AddChild(_confirmDialog);
    }

    private P1ItemGrid BuildGrid(ItemContainerKind kind, int columns, int capacity, float cellSize)
    {
        var grid = new P1ItemGrid { ContainerKind = kind };
        grid.Configure(columns, capacity, cellSize);
        grid.ItemActivated += index => ActivateItem(kind, index);
        grid.ItemSelected += index => SelectCraftTarget(kind, index);
        grid.ItemContextRequested += (index, position) => OpenItemMenu(kind, index, position);
        grid.QuickTransferRequested += index => Execute(ItemCommands().QuickTransfer(kind, index));
        grid.ItemDropped += (source, sourceIndex, targetIndex) => HandleDrop(source, sourceIndex, kind, targetIndex);
        return grid;
    }

    private void SelectCraftTarget(ItemContainerKind kind, int index)
    {
        if (index < 0)
        {
            _craftIndex = -1;
            Refresh();
            return;
        }
        if (kind is ItemContainerKind.Storage or ItemContainerKind.SortingBag or ItemContainerKind.Equipped)
        {
            _craftContainer = kind;
            _craftIndex = index;
            if (_batchScope is not null && kind is ItemContainerKind.Storage or ItemContainerKind.SortingBag)
                _batchScope.Select(kind == ItemContainerKind.Storage ? (int)P16BatchScope.Storage : (int)P16BatchScope.SortingBag);
            Refresh();
        }
    }

    private bool CanDropOnEquipment(ItemContainerKind source, int sourceIndex, int targetIndex)
    {
        ItemInstance? item = ItemAt(source, sourceIndex);
        return item is not null && Enum.IsDefined(typeof(EquipmentSlot), targetIndex) &&
               EquipmentLoadout.CanEquip((EquipmentSlot)targetIndex, item.Base.Category);
    }

    private void ActivateItem(ItemContainerKind kind, int index)
    {
        P2ItemCommandService commands = ItemCommands();
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
        P2ItemCommandService commands = ItemCommands();
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
        P2ItemCommandService commands = ItemCommands();
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
        ItemInstance? currentItem = ItemAt(_craftContainer, _craftIndex);
        if (currentItem is null)
        {
            Changed("请先在角色与物品页选择背包、仓库或已装备物品作为制作目标。");
            return;
        }

        P2WorkshopPreview preview = P2Workshop.Preview(currentItem, recipe);
        if (!preview.Succeeded)
        {
            Changed($"制作失败：{preview.Summary}");
            return;
        }

        MetalCurrencyDefinition metal = P4MetalCurrencies.Get(preview.MetalCostKind!.Value);
        ItemContainerKind container = _craftContainer;
        int index = _craftIndex;
        _confirmDialog!.DialogText =
            $"{preview.Summary}\n消耗：{preview.MetalCost} {metal.DisplayName}\n\n" +
            $"制作前：{currentItem.Affixes.Count} 条词缀\n" +
            $"制作后：{preview.Result!.Affixes.Count} 条词缀（新增 {preview.Summary}）\n\n" +
            $"确认对 {currentItem.Base.DisplayName} 执行？";
        _pendingConfirmation = () =>
        {
            P2WorkshopPreview result = ItemCommands()
                .Craft(container, index, recipe);
            Changed(result.Succeeded ? $"制作完成：{result.Summary}" : $"制作失败：{result.Summary}");
        };
        _confirmDialog.PopupCentered();
    }

    private void CraftP6Selected(P6CraftOperation operation)
    {
        ItemInstance? currentItem = ItemAt(_craftContainer, _craftIndex);
        if (currentItem is null)
        {
            Changed("请先选择背包、仓库或已装备物品作为制作目标。");
            return;
        }

        string fractureFamily = operation == P6CraftOperation.FractureAffix
            ? currentItem.Affixes.FirstOrDefault(affix => !affix.Crafted)?.Definition.StableFamilyId ?? string.Empty
            : string.Empty;
        P6CraftPreview preview = P6CraftingRules.Preview(currentItem, operation, fractureFamily);
        if (!preview.Succeeded)
        {
            Changed($"制作失败：{preview.Summary}");
            return;
        }

        MetalCurrencyDefinition metal = P4MetalCurrencies.Get(preview.Currency);
        ItemContainerKind container = _craftContainer;
        int index = _craftIndex;
        string socketWarning = string.Empty;
        if (container == ItemContainerKind.Equipped && preview.ResultLinks < preview.CurrentLinks)
        {
            string groupId = P6SocketGroupIds.For((EquipmentSlot)index);
            SkillLinkConfiguration? link = RequireSession().Management.SkillLinks.FirstOrDefault(item => item.ChainId == groupId);
            string[] ejected = link?.SocketStoneInstanceIds?.Skip(preview.ResultLinks)
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => RequireSession().Management.SkillStones.First(stone => stone.InstanceId == id).Definition.DisplayName)
                .ToArray() ?? [];
            if (ejected.Length > 0)
            {
                socketWarning = $"\n⚠ 超出新连接数的技能石将弹回技能石背包：{string.Join("、", ejected)}";
            }
        }

        _confirmDialog!.DialogText =
            $"{preview.Summary}\n消耗：{preview.Cost} {metal.DisplayName}\n" +
            $"连接：{preview.CurrentLinks} → {preview.ResultLinks}{socketWarning}\n\n" +
            $"确认对 {currentItem.Base.DisplayName} 执行？";
        _pendingConfirmation = () =>
        {
            P6CraftPreview result = ItemCommands()
                .CraftP6(container, index, operation, fractureFamily);
            Changed(result.Succeeded ? $"制作完成：{result.Summary}" : $"制作失败：{result.Summary}");
        };
        _confirmDialog.PopupCentered();
    }

    private void ConfirmBatch(P16BatchAction action)
    {
        P1GameSession session = RequireSession();
        ItemRarity maximum = (ItemRarity)(_batchRarity?.Selected ?? (int)ItemRarity.Magic);
        P16BatchScope scope = (P16BatchScope)(_batchScope?.Selected ?? (int)P16BatchScope.Storage);
        P16BatchPreview preview = P16BatchItems.Preview(session, action, scope, maximum);
        if (preview.Total == 0)
        {
            string reasons = preview.ExcludedReasons.Count == 0 ? string.Empty :
                $"（{string.Join("、", preview.ExcludedReasons.Select(pair => $"{pair.Key} {pair.Value}"))}）";
            Changed($"没有符合“{RarityLabel(maximum)}及以下”的可处理物品；锁定、关键、神话和制作底材已排除{reasons}。");
            return;
        }

        bool sell = action == P16BatchAction.Sell;
        string counts = string.Join("、", Enum.GetValues<ItemRarity>()
            .Where(rarity => preview.Counts.ContainsKey(rarity))
            .Select(rarity => $"{RarityLabel(rarity)} {preview.Counts[rarity]}"));
        string warning = maximum == ItemRarity.Legendary
            ? "\n⚠ 已选择传奇及以下：普通传奇会被处理，神话装备仍受保护。"
            : string.Empty;
        string protectedItems = preview.ExcludedReasons.Count == 0 ? string.Empty :
            $"（{string.Join("、", preview.ExcludedReasons.Select(pair => $"{pair.Key} {pair.Value}"))}）";
        string buyback = sell && preview.BuybackEvictions > 0
            ? $"\n⚠ 回购栏将挤出最早的 {preview.BuybackEvictions} 件物品。"
            : string.Empty;
        _confirmDialog!.DialogText =
            $"将{(sell ? "出售" : "分解")} {preview.Total} 件物品：{counts}。\n" +
            $"预计获得 {preview.Proceeds} {(sell ? "金币" : "铁屑")}；另有 {preview.Excluded} 件受保护{protectedItems}。" +
            warning + buyback + "\n确认执行？";
        _pendingConfirmation = () =>
        {
            P16BatchExecution result = P16BatchItems.Execute(session, preview);
            Changed($"批量{(sell ? "出售" : "分解")}完成：成功 {result.Completed} 件，失败 {result.Failed} 件，受保护 {preview.Excluded} 件。");
        };
        _confirmDialog.PopupCentered();
    }

    private void SortItems(P16ItemSortMode mode)
    {
        P1GameSession session = RequireSession();
        P16BatchScope scope = (P16BatchScope)(_batchScope?.Selected ?? (int)P16BatchScope.Storage);
        if (scope is P16BatchScope.Storage or P16BatchScope.Both) session.World.Storage.Sort(mode);
        if (scope is P16BatchScope.SortingBag or P16BatchScope.Both) session.Management.SortSortingBag(mode);
        string modeName = mode switch
        {
            P16ItemSortMode.LinkedSockets => "连接数",
            P16ItemSortMode.Rarity => "稀有度",
            P16ItemSortMode.ItemLevel => "物品等级",
            P16ItemSortMode.Base => "底材",
            _ => "最高词缀T级",
        };
        Changed($"{(scope == P16BatchScope.Both ? "整理背包与仓库" : scope == P16BatchScope.Storage ? "仓库" : "整理背包")}已按{modeName}整理。");
    }

    private static string RarityLabel(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Basic => "基础",
        ItemRarity.Magic => "魔法",
        ItemRarity.Rare => "稀有",
        ItemRarity.Legendary => "传奇",
        _ => rarity.ToString(),
    };

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

    private void BuildJourneyDialogs()
    {
        _journeyDialog = new AcceptDialog
        {
            Title = "旅程引导",
            OkButtonText = "前往目标",
            Exclusive = true,
            MinSize = new Vector2I(520, 230),
        };
        _journeyDialog.Confirmed += () => Navigate(_pendingDestination);
        AddChild(_journeyDialog);
        _handbookDialog = new AcceptDialog
        {
            Title = "旅程手册",
            OkButtonText = "关闭",
            Exclusive = true,
            MinSize = new Vector2I(680, 520),
        };
        AddChild(_handbookDialog);
        _completionDialog = new AcceptDialog
        {
            Title = "GameForWork Demo 完成",
            OkButtonText = "继续游戏",
            Exclusive = true,
            MinSize = new Vector2I(620, 500),
        };
        AddChild(_completionDialog);
    }

    private void RefreshJourneyInterface()
    {
        P1GameSession session = RequireSession();
        session.Journey.Synchronize(session);
        P8JourneyStepDefinition? current = session.Journey.CurrentStep;
        _journeyStatus!.Text = current is null ? "Demo 主旅程已完成 · 可以继续挂机与优化构筑" : current.Title + " · " + current.Instruction;
        _journeyGo!.Text = current is null ? "查看结算" : "前往";
        _journeyGo.Disabled = false;
        if (_miniJourney is not null) _miniJourney.Text = current is null ? "目标：Demo 已完成" : "目标：" + current.Title;
        NotifyJourneyRewards(session);
        RefreshUnlockedPages(session);
        RefreshStopWarning(session);
        if (session.Journey.TryPresentCurrentStep() && current is not null)
        {
            _pendingDestination = current.Destination;
            _journeyDialog!.DialogText = $"{current.Title}\n\n{current.Instruction}\n\n{current.HelpText}";
            _stateChanged?.Invoke();
            Callable.From(() => _journeyDialog.PopupCentered(new Vector2I(560, 260))).CallDeferred();
        }
        if (session.Journey.TryMarkCompletionShown())
        {
            _stateChanged?.Invoke();
            Callable.From(ShowCompletionSummary).CallDeferred();
        }
    }

    private void NotifyJourneyRewards(P1GameSession session)
    {
        int currentIndex = session.Journey.CurrentStepIndex;
        if (_lastJourneyStepIndex >= 0 && currentIndex > _lastJourneyStepIndex)
        {
            string completed = string.Join("、", session.Journey.AllSteps
                .Skip(_lastJourneyStepIndex)
                .Take(currentIndex - _lastJourneyStepIndex)
                .Select(item => item.Title));
            _notice?.Invoke($"旅程目标完成：{completed}。奖励已自动领取。");
        }
        _lastJourneyStepIndex = currentIndex;
    }

    private void RefreshUnlockedPages(P1GameSession session)
    {
        bool characterLocked = !session.Journey.TutorialAllowsPage(P8JourneyStep.EquipItem);
        if (_characterButton is not null) _characterButton.Disabled = characterLocked;
        if (characterLocked)
        {
            _characterWindow?.Hide();
            _characterWindowPairInitialized = false;
        }
        SetHidden(_mainTabs!, _townPage!, !session.Journey.TutorialAllowsPage(P8JourneyStep.CraftItem, requireGateCompletion: true));
        SetHidden(_mainTabs!, _expeditionPage!, !session.Campaign.Completed);
        if (_characterModes is not null)
        {
            // Core build pages remain available from the start; the journey guides without hiding them.
            SetHidden(_characterModes, _skillMode!, hidden: false);
            SetHidden(_characterModes, _passiveMode!, !session.Journey.TutorialAllowsPage(P8JourneyStep.AllocatePassive));
            SetHidden(_characterModes, _aiMode!, !session.Journey.TutorialAllowsPage(P8JourneyStep.ConfigureSkillTarget, requireGateCompletion: true));
        }
    }

    private void RefreshStopWarning(P1GameSession session)
    {
        string text = string.Empty;
        P8JourneyDestination destination = P8JourneyDestination.Overview;
        if (session.Campaign.Defeated)
        {
            text = "主线战败：调整装备、技能或天赋后前往主线页继续。";
            destination = P8JourneyDestination.Story;
        }
        else if (session.Management.SkillLinks.Any(link => string.IsNullOrEmpty(link.ActiveStoneInstanceId) &&
                     (link.SocketStoneInstanceIds?.Any(id => !string.IsNullOrEmpty(id)) ?? false)))
        {
            text = "技能孔组缺少主动技能：该孔组不会产生战斗效果。";
            destination = P8JourneyDestination.Skills;
        }
        else
        {
            P1TeamExpeditionState? stopped = session.World.Teams.FirstOrDefault(team => team.IsStopped && team.StopReason != "manual_stop");
            if (stopped is not null)
            {
                text = $"{(stopped.Kind == ExpeditionTeamKind.Hero ? "主角" : "佣兵队")}远征已停止：{JourneyStopReason(stopped.StopReason)}。";
                destination = P8JourneyDestination.Expedition;
            }
        }
        _warningBar!.Visible = text.Length > 0;
        _warningText!.Text = text;
        _warningDestination = destination;
    }

    private void NavigateToCurrentJourney()
    {
        P8JourneyStepDefinition? current = RequireSession().Journey.CurrentStep;
        if (current is null)
        {
            ShowCompletionSummary();
            return;
        }
        Navigate(current.Destination);
    }

    private void ShowCompletionSummary()
    {
        P1GameSession session = RequireSession();
        byte[] state = JsonSerializer.SerializeToUtf8Bytes(session.Capture());
        string hash = Convert.ToHexString(SHA256.HashData(state)).ToLowerInvariant();
        P8DemoSummary summary = session.Journey.BuildSummary(session, hash);
        _completionDialog!.DialogText = BuildCompletionText(summary);
        _completionDialog.PopupCentered(new Vector2I(660, 540));
    }

    private void Navigate(P8JourneyDestination destination)
    {
        if (_mainTabs is null) return;
        bool characterDestination = destination is P8JourneyDestination.Equipment or P8JourneyDestination.Skills or
            P8JourneyDestination.Passives;
        Control? main = destination switch
        {
            P8JourneyDestination.Overview => _overviewPage!,
            P8JourneyDestination.Story => _storyPage!,
            P8JourneyDestination.Expedition => _expeditionPage!,
            P8JourneyDestination.Town => _townPage!,
            _ => null,
        };
        if (main is not null)
        {
            int mainIndex = _mainTabs.GetTabIdxFromControl(main);
            if (mainIndex >= 0 && !_mainTabs.IsTabHidden(mainIndex)) _mainTabs.CurrentTab = mainIndex;
        }
        if (_characterModes is null)
        {
            if (characterDestination) OpenCharacterWindow();
            return;
        }
        Control? mode = destination switch
        {
            P8JourneyDestination.Equipment => _equipmentMode,
            P8JourneyDestination.Skills => _skillMode,
            P8JourneyDestination.Passives => _passiveMode,
            _ => null,
        };
        if (mode is not null)
        {
            int modeIndex = _characterModes.GetTabIdxFromControl(mode);
            if (modeIndex >= 0 && !_characterModes.IsTabHidden(modeIndex)) _characterModes.CurrentTab = modeIndex;
        }
        if (characterDestination) OpenCharacterWindow();
    }

    private void OpenCharacterWindow()
    {
        if (_session is null || _characterWindow is null || _characterButton?.Disabled == true) return;
        _expandWindow?.Invoke();
        if (!_characterWindow.Visible)
        {
            Rect2I bounds = PrepareCharacterWindowBounds();
            _characterWindowPairInitialized = false;
            _characterWindow.Popup(bounds);
        }
        SyncCharacterWindowPair();
        Refresh();
    }

    private Rect2I PrepareCharacterWindowBounds()
    {
        Window mainWindow = GetWindow();
        Rect2I usable = DisplayServer.ScreenGetUsableRect(mainWindow.CurrentScreen);
        Vector2I mainSize = mainWindow.Size;
        int availableWidth = usable.Size.X - mainSize.X - CharacterWindowGap;
        int characterWidth = Math.Min(CharacterWindowPreferredSize.X,
            Math.Max(CharacterWindowMinimumSize.X, availableWidth));
        int characterHeight = Math.Min(CharacterWindowPreferredSize.Y,
            Math.Max(CharacterWindowMinimumSize.Y, usable.Size.Y));
        var characterSize = new Vector2I(characterWidth, characterHeight);

        int groupWidth = mainSize.X + CharacterWindowGap + characterSize.X;
        int groupHeight = Math.Max(mainSize.Y, characterSize.Y);
        int maximumMainX = Math.Max(usable.Position.X, usable.End.X - groupWidth);
        int maximumMainY = Math.Max(usable.Position.Y, usable.End.Y - groupHeight);
        var fittedMainPosition = new Vector2I(
            Math.Clamp(mainWindow.Position.X, usable.Position.X, maximumMainX),
            Math.Clamp(mainWindow.Position.Y, usable.Position.Y, maximumMainY));
        mainWindow.Position = fittedMainPosition;
        return new Rect2I(
            fittedMainPosition + new Vector2I(mainSize.X + CharacterWindowGap, 0),
            characterSize);
    }

    private void SyncCharacterWindowPair()
    {
        if (_characterWindow?.Visible != true || !IsInsideTree() || _syncingCharacterWindowPair) return;
        _syncingCharacterWindowPair = true;
        try
        {
            Window mainWindow = GetWindow();
            Vector2I mainPosition = mainWindow.Position;
            Vector2I mainSize = mainWindow.Size;
            Vector2I characterPosition = _characterWindow.Position;
            Vector2I offset = new(mainSize.X + CharacterWindowGap, 0);

            if (!_characterWindowPairInitialized)
            {
                _characterWindow.Position = mainPosition + offset;
            }
            else if (mainPosition != _pairedMainPosition || mainSize != _pairedMainSize)
            {
                _characterWindow.Position = mainPosition + offset;
            }
            else if (characterPosition != _pairedCharacterPosition)
            {
                mainWindow.Position = characterPosition - offset;
                mainPosition = mainWindow.Position;
                _characterWindow.Position = mainPosition + offset;
            }

            _pairedMainPosition = mainWindow.Position;
            _pairedMainSize = mainWindow.Size;
            _pairedCharacterPosition = _characterWindow.Position;
            _characterWindowPairInitialized = true;
        }
        finally
        {
            _syncingCharacterWindowPair = false;
        }
    }

    private void ShowHandbook()
    {
        P1GameSession session = RequireSession();
        string steps = string.Join('\n', session.Journey.AllSteps.Select((definition, index) =>
            $"{(index < session.Journey.CurrentStepIndex ? "✓" : index == session.Journey.CurrentStepIndex ? "▶" : "·")} {definition.Title}：{definition.HelpText}"));
        _handbookDialog!.DialogText = steps +
            "\n\n常用术语\n连接孔：同组主动技能与辅助技能共享效果。\n法术压制：成功时该次法术命中伤害降低 70%。" +
            "\n收益路线：地图中选择的主要风险与奖励方向。" +
            (session.Journey.TutorialEnabled ? string.Empty : "\n\n本存档创建时已跳过强制引导；全部页面保持开放，可用上方‘重播教学’重新查看提示。");
        _handbookDialog.PopupCentered(new Vector2I(720, 560));
    }

    private void ReplayTutorial()
    {
        P1GameSession session = RequireSession();
        session.Journey.ReplayTutorial();
        _stateChanged?.Invoke();
        _notice?.Invoke("教学提示已重播；不会重置旅程进度，也不会重新隐藏页面。");
        ShowHandbook();
    }

    private static string BuildCompletionText(P8DemoSummary summary) =>
        $"你已经击败灰烬天垒，完成首个 Demo 主旅程。之后仍可继续挂机、制作装备和优化构筑。\n\n" +
        $"现实游玩 {TimeText(summary.RealPlayMilliseconds)} · 离线收益 {TimeText(summary.OfflineMilliseconds)}\n" +
        $"完成幕数 {summary.ActsCompleted}/5 · 地图成功 {summary.MapsCompleted} · 失败 {summary.MapsFailed}\n" +
        $"Boss 尝试 {summary.BossAttempts} · 最终等级 {summary.Level}\n" +
        $"主要技能 {summary.MainSkill} · {summary.MainSkillLinks} 连 · 最高技能总伤害 {summary.HighestDamage}\n" +
        $"装备评分 {summary.EquipmentScore} · 传奇物品 {summary.LegendaryItems} · 神话物品 {summary.MythicItems}\n" +
        $"最高地图 T{summary.HighestMapTier} · 机制遭遇 {summary.MechanicEncounters} · 天垒胜利 {summary.CitadelVictories} · 城区总等级 {summary.TownLevelTotal}\n" +
        $"结算存档哈希 {summary.SaveHash[..16]}…";

    private static string TimeText(long milliseconds) => TimeSpan.FromMilliseconds(milliseconds) is TimeSpan time
        ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
        : "00:00:00";

    private static void SetHidden(TabContainer tabs, Control control, bool hidden)
    {
        int index = tabs.GetTabIdxFromControl(control);
        if (index >= 0) tabs.SetTabHidden(index, hidden);
    }

    private static string JourneyStopReason(string reason) => reason switch
    {
        "maps_exhausted" => "地图已经耗尽",
        "boss_ticket_missing" => "缺少 Boss 门票",
        "consecutive_failures" => "连续失败达到停止条件",
        "minimum_storage_free_slots" => "仓库空位不足",
        "map_failed" => "地图失败策略要求停止",
        _ => reason,
    };

    private void Refresh()
    {
        if (_session is null)
        {
            return;
        }

        bool overviewActive = _mainTabs?.GetTabControl(_mainTabs.CurrentTab) == _overviewPage;
        bool storyActive = _mainTabs?.GetTabControl(_mainTabs.CurrentTab) == _storyPage;
        bool expeditionActive = _mainTabs?.GetTabControl(_mainTabs.CurrentTab) == _expeditionPage;
        bool characterActive = _characterWindow?.Visible == true;
        bool townActive = _mainTabs?.GetTabControl(_mainTabs.CurrentTab) == _townPage;
        _worldView!.Session = _session;
        if (overviewActive) _worldView.QueueRedraw();
        if (characterActive) RefreshCharacterSelector();
        string recoveryWarning = _session.Management.Recovery.Count == 0
            ? string.Empty
            : $" · ⚠ 恢复箱 {_session.Management.Recovery.Count}";
        _overviewStatus!.Text =
            $"佣兵队 {_session.Town.ActiveMembers().Count}/{_session.Town.MercenaryCapacity} 人{recoveryWarning}";

        TownEconomyState economy = _session.World.Economy;

        RefreshJourneyInterface();
        if (townActive) _townPanel?.Refresh();
        if (characterActive)
        {
            EquipmentLoadout selectedLoadout = SelectedLoadout();
            P1TeamExpeditionState selectedTeam = _selectedCharacter == P2CharacterKind.Hero
                ? _session.World.Hero
                : _session.World.Mercenaries;
            EquipmentSummary equipment = selectedLoadout.CalculateSummary();
            P9MercenaryMember? selectedMercenary = SelectedMercenary();
            CharacterSheet selectedSheet = selectedTeam.Build.Sheet;
            if (selectedMercenary is not null)
            {
                selectedSheet = CharacterBuildAssembler.Assemble(
                    selectedMercenary.Level,
                    selectedMercenary.Identity.FinalAttributes,
                    selectedMercenary.Equipment,
                    new PassiveTreeAllocation(),
                    new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed)).Sheet;
            }
            _characterStatus!.Text = _selectedCharacter == P2CharacterKind.Hero
                ? $"{_session.Player.Name} · {P23ClassCatalog.Get(_session.Player.BaseClass).DisplayName} · Lv.{selectedTeam.Progression.Level}"
                : $"{selectedMercenary?.Identity.Name ?? "佣兵"} · {MercenaryArchetypeName(selectedMercenary?.Identity.Archetype)} · Lv.{selectedMercenary?.Level ?? selectedTeam.Progression.Level}";
            _storageStatus!.Text =
                $"生命 {selectedSheet.MaximumLife().Value} · 法力 {selectedSheet.MaximumMana().Value} · 护盾 {selectedSheet.Equipment.Shield}\n" +
                $"体魄 {selectedSheet.Attributes.Physique} · 灵巧 {selectedSheet.Attributes.Dexterity} · 精神 {selectedSheet.Attributes.Spirit} · 能量 {selectedSheet.Attributes.Energy}\n" +
                $"核心槽 {equipment.CoreSkillCapacity} · 旧制连接 {equipment.SupportLinkCapacity}\n" +
                (selectedMercenary is null
                    ? BuildSummaryText(_session.GetBuildSummary())
                    : $"最终属性已公开；内部加点隐藏。\n技能：{selectedMercenary.Identity.SkillSummary}\nAI：{selectedMercenary.Identity.AiSummary}");

            _metalPanel?.Refresh();
            if (string.IsNullOrWhiteSpace(_storageSearch))
            {
                _storageGrid!.SetItems(_session.World.Storage.Items);
            }

            else
            {
                string query = _storageSearch.ToLowerInvariant();
                _storageGrid!.SetFilteredItems(_session.World.Storage.Items.Select((item, index) => (index, item))
                    .Where(entry => $"{entry.item.Base.DisplayName} {entry.item.Base.Category} {entry.item.Rarity} {entry.item.LinkedSocketCount}连"
                        .ToLowerInvariant().Contains(query, StringComparison.Ordinal))
                    .ToArray());
            }
            _sortingGrid!.SetItems(_session.Management.SortingBag);
            _recoveryGrid!.SetItems(_session.Management.Recovery.Take(30).ToArray());
            _buybackGrid!.SetItems(_session.Management.Buyback.Select(entry => entry.Item).ToArray());
            ItemInstance?[] slots = Enum.GetValues<EquipmentSlot>()
                .Select(slot => selectedLoadout.Items.GetValueOrDefault(slot))
                .ToArray();
            _equipmentGrid!.SetSlots(slots);
            _passiveTree!.SetState(_session.Passives.Allocated, _session.World.Hero.Progression.EarnedPassivePoints,
                _session.Passives.StartKind, _session.Jewels.Socketed);
            _jewelStashPanel?.RefreshState();
            _bossFragmentsStatus!.Text =
                $"◆ 深渊监守者\n碎片 {_session.World.Expedition.AbyssWardenFragments}/{P5ExpeditionDirector.FragmentsPerTicket}　门票 ×{_session.World.Expedition.AbyssWardenTickets}\n" +
                $"下枚碎片进度 {_session.World.Expedition.MapsTowardNextFragment}/{P5ExpeditionDirector.MapsPerFragment}\n\n" +
                $"◆ 灰烬天垒\n碎片 {_session.Endgame.CitadelFragments}/{P10EndgameState.CitadelFragmentsPerTicket}　门票 ×{_session.Endgame.CitadelTickets}";
            _ascendancyPanel?.Refresh();
            bool heroSelected = _selectedCharacter == P2CharacterKind.Hero;
            foreach (BaseButton control in _heroOnlyControls)
            {
                control.Disabled = !heroSelected;
            }

            ItemInstance? craftItem = ItemAt(_craftContainer, _craftIndex);
            _craftingStatus!.Text =
                $"金属库存：淬刃铁 {economy.MetalAmount(MetalCurrencyKind.TemperingIron)} · " +
                $"守壁钢 {economy.MetalAmount(MetalCurrencyKind.WardSteel)} · 活血银 {economy.MetalAmount(MetalCurrencyKind.VitalSilver)} · " +
                $"链铸钢 {economy.MetalAmount(MetalCurrencyKind.ChainSteel)} · 混沌金 {economy.MetalAmount(MetalCurrencyKind.ChaosGold)} · " +
                $"神铸银 {economy.MetalAmount(MetalCurrencyKind.DivineSilver)} · 破溃钢 {economy.MetalAmount(MetalCurrencyKind.FractureSteel)}\n" +
                (craftItem is null ? "当前未选择制作目标。" : $"当前目标：{craftItem.Base.DisplayName}（{_craftContainer}）");
            _skillStonePanel?.SetReadOnly(!heroSelected);
            if (characterActive) _skillStonePanel?.RefreshState();
            _history!.Text = string.Join('\n', _session.Management.OperationHistory.TakeLast(200).Select(item => $"• {item}"));
            _filterPanel?.RefreshRules();
        }
        if (expeditionActive) { _expeditionPanel?.RefreshState(); _endgamePanel?.Refresh(); }
        if (storyActive) _campaignRoute?.RefreshState();
        CampaignNodeDefinition? currentNode = _session.Campaign.CurrentNode;
        long currentNodeDuration = _session.Campaign.ActiveTimeline?.DurationMilliseconds ??
                                   currentNode?.DurationMilliseconds ?? 0;
        _storyStatus!.Text = _session.Campaign.Completed
            ? "五幕主线已完成\n远征功能已开放。"
            : $"第 {currentNode!.Act} 幕 · {P2CampaignCatalog.ActNames[currentNode.Act - 1]}\n" +
              $"当前：{currentNode.DisplayName}（{currentNode.Kind}）\n" +
              $"进度 {_session.Campaign.CurrentNodeElapsedMilliseconds / 1_000}/{currentNodeDuration / 1_000}s\n" +
              (_session.Campaign.Defeated ? "⚠ 战败：调整构筑后点击继续。" : "自动推进中；离线时间同样有效。 ");
        _storyLog!.Text = string.Join('\n', _session.Campaign.StoryLog.TakeLast(60).Select(item => $"• {item}"));
        _miniStatus!.Text =
            $"{_session.Player.Name} Lv.{_session.World.Hero.Progression.Level}  主角[{CompactTeam(_session.World.Hero)}]\n" +
            $"佣兵[{CompactTeam(_session.World.Mercenaries)}] · 金币 {economy.Gold} · 图 {_session.World.MapInventory.Count}";
    }

    private ItemInstance? ItemAt(ItemContainerKind kind, int index) => kind switch
    {
        ItemContainerKind.Storage when index >= 0 && index < RequireSession().World.Storage.Items.Count =>
            RequireSession().World.Storage.Items[index],
        ItemContainerKind.SortingBag when index >= 0 && index < RequireSession().Management.SortingBag.Count =>
            RequireSession().Management.SortingBag[index],
        ItemContainerKind.Recovery when index >= 0 && index < RequireSession().Management.Recovery.Count =>
            RequireSession().Management.Recovery[index],
        ItemContainerKind.Equipped => SelectedLoadout().Items.GetValueOrDefault((EquipmentSlot)index),
        _ => null,
    };

    private static EquipmentSlot PreferredSlot(ItemInstance item) => item.Base.Category switch
    {
        ItemCategory.Ring => EquipmentSlot.RingLeft,
        ItemCategory.LifeFlask => EquipmentSlot.Flask1,
        _ => item.Base.PrimarySlot,
    };

    private static string BuildSummaryText(P6BuildSummary summary) =>
        $"主技能 {summary.MainSkill} · {summary.MainSkillLinks} 连 · 单体估算 {summary.EstimatedSingleTargetDamage}/s · 清图估算 {summary.EstimatedClearDamage}/s\n" +
        $"有效生命 {summary.EffectiveLife} · 护甲 {summary.Armor} · 闪避 {summary.Evasion} · 护盾 {summary.Shield}\n" +
        $"恢复：{summary.Recovery} · 增益：{summary.BuffCoverage}\n" +
        (summary.Issues.Count == 0 ? "构筑检查：未发现孔位或兼容性问题\n" : $"构筑检查：{string.Join("；", summary.Issues)}\n") +
        summary.Assumptions;

    private P1GameSession RequireSession() => _session ?? throw new InvalidOperationException("请先创建角色。");

    private P2ItemCommandService ItemCommands() => new(RequireSession(), _selectedCharacter, _selectedMercenaryId);

    private P9MercenaryMember? SelectedMercenary()
    {
        if (_session is null || _selectedCharacter != P2CharacterKind.Mercenary) return null;
        P9MercenaryMember? selected = _session.Town.Roster.FirstOrDefault(member => member.Identity.StableId == _selectedMercenaryId);
        return selected ?? _session.Town.Roster.FirstOrDefault();
    }

    private EquipmentLoadout SelectedLoadout() => _selectedCharacter == P2CharacterKind.Hero
        ? RequireSession().HeroEquipment
        : SelectedMercenary()?.Equipment ?? RequireSession().MercenaryEquipment;

    private void RefreshCharacterSelector()
    {
        if (_characterSelector is null || _session is null) return;
        string signature = string.Join('|', _session.Town.Roster.Select(member => member.Identity.StableId));
        if (signature == _characterSelectorSignature) return;
        _characterSelectorSignature = signature;
        string selectedId = _selectedMercenaryId;
        _characterSelector.Clear();
        _characterSelector.AddItem("主角");
        foreach (P9MercenaryMember member in _session.Town.Roster)
            _characterSelector.AddItem($"佣兵：{member.Identity.Name}");
        int selectedIndex = _selectedCharacter == P2CharacterKind.Hero ? 0 :
            Math.Max(1, _session.Town.Roster.ToList().FindIndex(member => member.Identity.StableId == selectedId) + 1);
        if (selectedIndex >= _characterSelector.ItemCount) selectedIndex = 0;
        _characterSelector.Select(selectedIndex);
        if (selectedIndex == 0) { _selectedCharacter = P2CharacterKind.Hero; _selectedMercenaryId = string.Empty; }
        else _selectedMercenaryId = _session.Town.Roster[selectedIndex - 1].Identity.StableId;
    }

    private static string MercenaryArchetypeName(P9MercenaryArchetype? archetype) => archetype switch
    {
        P9MercenaryArchetype.Guardian => "守卫",
        P9MercenaryArchetype.Ranger => "游猎者",
        P9MercenaryArchetype.Cantor => "颂仪者",
        P9MercenaryArchetype.Arcanist => "秘械师",
        _ => "未知职业",
    };

    private P1TeamExpeditionState Team(ExpeditionTeamKind kind) =>
        kind == ExpeditionTeamKind.Hero ? RequireSession().World.Hero : RequireSession().World.Mercenaries;

    private static string TeamText(P1TeamExpeditionState team, P1WorldState world) =>
        $"{team.Kind}: 队列 {team.Queue.Count}/10 · 完成 {team.MapsCompleted} · 失败 {team.MapsFailed} · " +
        (team.ActiveMap is null
            ? team.IsStopped ? $"停止：{team.StopReason}" : "等待资源/地图"
            : $"进行中 {team.ActiveMap.InstanceId}，剩余 {team.RemainingMapTimeMilliseconds / 1_000}s") +
        $" · 停止条件[图数 {DisplayLimit(team.Policy.MaximumContinuousMaps)} / " +
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

    private static PanelContainer TreeHud(Control content)
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

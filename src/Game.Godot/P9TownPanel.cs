using GameForWork.Core.P1;
using GameForWork.Core.P9;
using Godot;

namespace GameForWork.GodotClient;

public partial class P9TownPanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private OptionButton? _policy;
    private GridContainer? _buildings;
    private VBoxContainer? _candidates;
    private VBoxContainer? _roster;
    private RichTextLabel? _events;
    private Label? _summary;
    private Label? _tavernRefresh;
    private Texture2D? _mercenaryAtlas;
    private Texture2D? _buildingAtlas;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        const string p21Actors = "res://assets/p21/characters/p21-actor-animation.png";
        _mercenaryAtlas = ResourceLoader.Exists(p21Actors) ? GD.Load<Texture2D>(p21Actors) : null;
        const string p21Buildings = "res://assets/p21/town/p21-building-atlas.png";
        _buildingAtlas = ResourceLoader.Exists(p21Buildings) ? GD.Load<Texture2D>(p21Buildings) : null;
        var top = new HBoxContainer();
        AddChild(top);
        top.AddChild(new Label { Text = "固定城区 · 城镇方针" });
        _policy = new OptionButton();
        _policy.AddItem("开拓：施工速度 +20%", (int)P9TownPolicy.Expansion);
        _policy.AddItem("远征：地图产出速度 +20%", (int)P9TownPolicy.Expedition);
        _policy.AddItem("练兵：后备经验提高", (int)P9TownPolicy.Training);
        _policy.ItemSelected += index => { session().SetTownPolicy((P9TownPolicy)_policy.GetItemId((int)index)); changed("城镇方针已切换。"); };
        top.AddChild(_policy);
        _summary = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Right };
        top.AddChild(_summary);
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(tabs);
        var leftScroll = new ScrollContainer { Name = "城区建筑", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        tabs.AddChild(leftScroll);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        leftScroll.AddChild(left);
        left.AddChild(new Label { Text = "城区总览 · 施工期间保留旧等级效果" });
        _buildings = new GridContainer { Columns = 4 };
        left.AddChild(_buildings);
        var rightScroll = new ScrollContainer { Name = "酒馆名册", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        tabs.AddChild(rightScroll);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rightScroll.AddChild(right);
        var tavernHeader = new HBoxContainer();
        right.AddChild(tavernHeader);
        _tavernRefresh = new Label { Text = "酒馆候选 · 每 30 分钟刷新" };
        tavernHeader.AddChild(_tavernRefresh);
        AddButton(tavernHeader, "100 金币立即刷新", () => changed(session().TryRefreshTavern() ? "酒馆候选已刷新。" : "金币不足。"));
        _candidates = new VBoxContainer();
        right.AddChild(_candidates);
        right.AddChild(new HSeparator());
        right.AddChild(new Label { Text = "佣兵名册 · 编入/撤下出征队伍；站位由职业与 AI 自动安排" });
        _roster = new VBoxContainer();
        right.AddChild(_roster);
        var eventScroll = new ScrollContainer { Name = "城镇事件", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        tabs.AddChild(eventScroll);
        var eventBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        eventScroll.AddChild(eventBody);
        eventBody.AddChild(new Label { Text = "安全城镇事件" });
        _events = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(0, 260), ScrollActive = true };
        eventBody.AddChild(_events);
    }

    public void Refresh(bool force = false)
    {
        if (_session is null) return;
        P1GameSession session = _session();
        P9TownState town = session.Town;
        string signature = string.Join('|', town.Buildings.Select(pair => $"{pair.Key}:{pair.Value}")) +
            $"|{town.Policy}|{town.TavernUntilRefreshMilliseconds / 60_000}|{session.World.Economy.Gold}|{session.World.Economy.IronScraps}|" +
            string.Join(',', town.Construction.Select(job => $"{job.Kind}:{job.RemainingMilliseconds / 60_000}")) + '|' +
            string.Join(',', town.Candidates.Select(item => item.StableId)) + '|' +
            string.Join(',', town.LockedCandidates.OrderBy(id => id, StringComparer.Ordinal)) + '|' +
            string.Join(',', town.Formation) + '|' + string.Join(',', town.Roster.Select(member => $"{member.Identity.StableId}:{member.Level}"));
        if (!force && signature == _signature) return;
        _signature = signature;
        _policy!.Select((int)town.Policy);
        _summary!.Text = $"金币 {session.World.Economy.Gold:N0} · 铁屑 {session.World.Economy.IronScraps:N0} · 施工位 {town.Construction.Count}/{town.ConstructionSlots}";
        _tavernRefresh!.Text = $"酒馆候选 · {Math.Max(1, (town.TavernUntilRefreshMilliseconds + 59_999) / 60_000)} 分钟后刷新";
        RebuildBuildings(session);
        RebuildCandidates(session);
        RebuildRoster(session);
        _events!.Text = string.Join('\n', town.EventLog.TakeLast(12).Select(item => "• " + item));
    }

    private void RebuildBuildings(P1GameSession session)
    {
        foreach (Node child in _buildings!.GetChildren()) child.QueueFree();
        P9BuildingKind[] layout = [P9BuildingKind.Tavern, P9BuildingKind.Workshop, P9BuildingKind.Alchemy,
            P9BuildingKind.Cartography, P9BuildingKind.Storage, P9BuildingKind.Reliquary, P9BuildingKind.Teleporter];
        for (int layoutIndex = 0; layoutIndex < layout.Length; layoutIndex++)
        {
            P9BuildingKind kind = layout[layoutIndex];
            int level = session.Town.Level(kind);
            P9ConstructionSnapshot? job = session.Town.Construction.FirstOrDefault(item => item.Kind == kind);
            P9BuildingUpgradeCost? cost = P9TownState.NextUpgradeCost(level);
            string state = job is not null
                ? $"施工 {Math.Max(1, (job.RemainingMilliseconds + 59_999) / 60_000)} 分钟"
                : cost is null ? "最高级"
                : $"需要 {cost.Gold:N0} 金 + {cost.IronScraps} 铁屑 · {cost.DurationMilliseconds / 60_000} 分钟";
            string tooltip = $"{P9TownState.DisplayName(kind)} · Lv.{level}\n当前：{BuildingEffect(kind, level)}\n{BuildingTooltip(kind)}" +
                             (cost is null ? "\n已达到最高等级。" : $"\n下一级：{BuildingEffect(kind, level + 1)}\n消耗：{cost.Gold:N0} 金币、{cost.IronScraps} 铁屑\n施工：{cost.DurationMilliseconds / 60_000} 分钟");
            var card = new PanelContainer
            {
                CustomMinimumSize = new Vector2(184, 218),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = tooltip,
            };
            var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
            content.AddThemeConstantOverride("separation", 4);
            card.AddChild(content);
            if (_buildingAtlas is not null)
            {
                content.AddChild(new TextureRect
                {
                    Texture = P21ArtAtlas.Icon(_buildingAtlas, layoutIndex, 4),
                    CustomMinimumSize = new Vector2(160, 104),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = MouseFilterEnum.Ignore,
                });
            }
            content.AddChild(new Label
            {
                Text = $"{P9TownState.DisplayName(kind)} · Lv.{level}",
                HorizontalAlignment = HorizontalAlignment.Center,
                TooltipText = tooltip,
            });
            content.AddChild(new Label
            {
                Text = BuildingEffect(kind, level),
                CustomMinimumSize = new Vector2(168, 32),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                TooltipText = tooltip,
            });
            content.AddChild(new Label
            {
                Text = state,
                CustomMinimumSize = new Vector2(168, 30),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                TooltipText = tooltip,
            });
            var button = new Button
            {
                Text = job is not null ? "施工中" : cost is null ? "已满级" : "升级",
                CustomMinimumSize = new Vector2(96, 30),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                Disabled = cost is null || job is not null,
                TooltipText = tooltip,
                ClipText = true,
            };
            button.Pressed += () =>
            {
                _ = session.TryUpgradeTownBuilding(kind, out string message);
                _changed?.Invoke(message);
            };
            content.AddChild(button);
            _buildings.AddChild(card);
        }
    }

    private void RebuildCandidates(P1GameSession session)
    {
        foreach (Node child in _candidates!.GetChildren()) child.QueueFree();
        foreach (P9MercenaryCandidate candidate in session.Town.Candidates)
        {
            var row = new HBoxContainer();
            _candidates.AddChild(row);
            row.AddChild(MercenaryPortrait(candidate.Archetype));
            row.AddChild(new Label
            {
                Text = $"{candidate.Name} · {Archetype(candidate.Archetype)} · {Potential(candidate.Potential)} · Lv.{candidate.Level}\n" +
                       $"{candidate.PositiveTrait}{(string.IsNullOrEmpty(candidate.NegativeTrait) ? string.Empty : " / " + candidate.NegativeTrait)} · {candidate.SkillSummary}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                TooltipText = $"最终属性：体魄 {candidate.FinalAttributes.Physique}、灵巧 {candidate.FinalAttributes.Dexterity}、精神 {candidate.FinalAttributes.Spirit}、能量 {candidate.FinalAttributes.Energy}\nAI：{candidate.AiSummary}",
            });
            AddButton(row, session.Town.LockedCandidates.Contains(candidate.StableId) ? "解锁" : "锁定", () =>
                _changed?.Invoke(session.Town.ToggleCandidateLock(candidate.StableId) ? "候选锁定状态已更新。" : "最多锁定两名候选。"));
            AddButton(row, $"招募 {candidate.RecruitmentCost}", () =>
            {
                bool ok = session.TryRecruitMercenary(candidate.StableId, out string message);
                _changed?.Invoke(message);
            });
        }
    }

    private void RebuildRoster(P1GameSession session)
    {
        foreach (Node child in _roster!.GetChildren()) child.QueueFree();
        foreach (P9MercenaryMember member in session.Town.Roster)
        {
            bool active = session.Town.Formation.Contains(member.Identity.StableId);
            var row = new HBoxContainer();
            _roster.AddChild(row);
            row.AddChild(MercenaryPortrait(member.Identity.Archetype));
            var button = new Button
            {
                Text = $"{(active ? "●" : "○")} {member.Identity.Name} · {Archetype(member.Identity.Archetype)} · Lv.{member.Level} · {Potential(member.Identity.Potential)}",
                Alignment = HorizontalAlignment.Left,
                TooltipText = $"{member.Identity.SkillSummary}\n{member.Identity.AiSummary}\n玩家配置装备和出征名单；战斗站位由职业与 AI 自动安排。",
            };
            button.Pressed += () => _changed?.Invoke($"{member.Identity.Name} · {member.Identity.SkillSummary}");
            row.AddChild(button);
            AddButton(row, active ? "撤下" : "编入", () =>
            {
                bool changed = active
                    ? session.TryRemoveMercenaryFromParty(member.Identity.StableId)
                    : session.TryAddMercenaryToParty(member.Identity.StableId);
                _changed?.Invoke(changed
                    ? $"{member.Identity.Name}已{(active ? "撤下" : "编入")}出征队伍。"
                    : "远征中不能调整队伍，或队伍人数已达到传送装置上限。");
            });
            AddButton(row, "解雇", () =>
            {
                session.TryDismissMercenary(member.Identity.StableId, out string message);
                _changed?.Invoke(message);
            });
        }
    }

    private static Button AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private TextureRect MercenaryPortrait(P9MercenaryArchetype archetype)
    {
        AtlasTexture? texture = null;
        if (_mercenaryAtlas is not null)
        {
            int rig = (int)archetype + 1;
            Rect2 region = P21ArtAtlas.AnimationCell(0, rig * 4, 48, 64);
            texture = new AtlasTexture
            {
                Atlas = _mercenaryAtlas,
                Region = region,
                FilterClip = true,
            };
        }
        return new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = new Vector2(48, 48),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
    }
    private static string BuildingEffect(P9BuildingKind kind, int level) => kind switch
    {
        P9BuildingKind.Tavern => $"名册 {level * 2 + 4} 人", P9BuildingKind.Workshop => $"附魔阶级 {level}",
        P9BuildingKind.Alchemy => $"炼金配方阶级 {level}", P9BuildingKind.Cartography => $"生成最高 T{Math.Min(10, level * 2 + 2)}",
        P9BuildingKind.Storage => $"容量 {level switch { 1 => 100, 2 => 150, 3 => 225, _ => 325 }}",
        P9BuildingKind.Reliquary => $"里程碑奖励 ×{level}", P9BuildingKind.Teleporter => $"佣兵上阵 {level + 2} 人", _ => string.Empty,
    };
    private static string BuildingTooltip(P9BuildingKind kind) => kind switch
    {
        P9BuildingKind.Tavern => "候选刷新、锁定、招募、后备经验和名册容量。",
        P9BuildingKind.Workshop => "解锁更强工匠词缀与独立附魔；加工仍在角色与物品页完成。",
        P9BuildingKind.Alchemy => "解锁公开、确定性的金属兑换配方。",
        P9BuildingKind.Cartography => "定时生成路印；远征方针会提高速度。",
        P9BuildingKind.Storage => "扩充装备仓库并管理无限堆叠的金属仓。",
        P9BuildingKind.Reliquary => "记录首杀、传奇收藏和城镇里程碑。",
        _ => "提高佣兵队人数上限并缩短远征准备。",
    };
    private static string Archetype(P9MercenaryArchetype kind) => kind switch
    { P9MercenaryArchetype.Guardian => "守卫", P9MercenaryArchetype.Ranger => "游猎者", P9MercenaryArchetype.Cantor => "颂仪者", _ => "秘械师" };
    private static string Potential(P9MercenaryPotential kind) => kind switch
    { P9MercenaryPotential.Common => "普通", P9MercenaryPotential.Promising => "优秀", P9MercenaryPotential.Exceptional => "卓越", _ => "传奇" };
}

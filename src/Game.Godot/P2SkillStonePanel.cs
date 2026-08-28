using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P2;
using GameForWork.Core.P5;
using GameForWork.Core.P17;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2SkillStonePanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private GridContainer? _inventory;
    private VBoxContainer? _groups;
    private Label? _errors;
    private HBoxContainer? _wideColumns;
    private VBoxContainer? _compactStack;
    private VBoxContainer? _inventoryColumn;
    private VBoxContainer? _groupsColumn;
    private bool _compact;
    private string _signature = string.Empty;
    private bool _readOnly;
    private LineEdit? _search;
    private OptionButton? _kindFilter;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        AddChild(new Label { Text = "技能石背包与装备孔组 · 拖曳装入/换孔 · 右键卸下 · 悬浮查看完整说明", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        SizeFlagsVertical = SizeFlags.ExpandFill;
        _wideColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _wideColumns.AddThemeConstantOverride("separation", 12);
        AddChild(_wideColumns);
        _compactStack = new VBoxContainer { Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _compactStack.AddThemeConstantOverride("separation", 10);
        AddChild(_compactStack);
        _inventoryColumn = Column(_wideColumns, "技能石背包", 600);
        var filters = new HBoxContainer();
        _search = new LineEdit { PlaceholderText = "搜索名称、标签或说明", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _search.TextChanged += _ => Invalidate();
        filters.AddChild(_search);
        _kindFilter = new OptionButton();
        foreach (string label in new[] { "全部", "主动", "辅助" }) _kindFilter.AddItem(label);
        _kindFilter.ItemSelected += _ => Invalidate();
        filters.AddChild(_kindFilter);
        _inventoryColumn.AddChild(filters);
        var inventoryScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inventoryColumn.AddChild(FramedScroll(inventoryScroll));
        var inventoryBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inventoryScroll.AddChild(inventoryBody);
        _inventory = new GridContainer { Columns = 12, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inventory.AddThemeConstantOverride("h_separation", 4);
        _inventory.AddThemeConstantOverride("v_separation", 4);
        inventoryBody.AddChild(_inventory);
        inventoryBody.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12), MouseFilter = MouseFilterEnum.Ignore });
        _groupsColumn = Column(_wideColumns, "当前装备孔组", 340);
        var groupScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _groupsColumn.AddChild(FramedScroll(groupScroll));
        var groupBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        groupScroll.AddChild(groupBody);
        _groups = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        groupBody.AddChild(_groups);
        groupBody.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12), MouseFilter = MouseFilterEnum.Ignore });
        _errors = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_errors);
        Resized += QueueResponsiveLayout;
    }

    public void SetReadOnly(bool readOnly)
    {
        if (_readOnly == readOnly) return;
        _readOnly = readOnly;
        Invalidate();
    }

    public void RefreshState()
    {
        if (_session is null || _inventory is null || _groups is null || _errors is null) return;
        P1GameSession session = _session();
        P2ManagementState management = session.Management;
        IReadOnlyList<P5SkillChainDefinition> chains = session.GetSkillChains();
        string signature = _readOnly + "|" + string.Join('|', management.SkillStones.Select(item => $"{item.InstanceId}:{item.Level}:{item.Experience}")) + "|" +
            string.Join('|', management.SkillLinks.Select(item => $"{item.ChainId}:{item.Priority}:{item.AiRule?.TargetPolicy}:{string.Join(',', item.SocketStoneInstanceIds ?? [])}")) + "|" +
            string.Join('|', chains.Select(chain => $"{chain.StableId}:{chain.TotalSockets}"));
        if (signature == _signature) return;
        _signature = signature;
        Clear(_inventory);
        Clear(_groups);
        if (_readOnly)
        {
            _inventory.AddChild(new Label { Text = "佣兵技能由自主成长配置，玩家只能查看最终属性。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            _errors.Text = "佣兵技能构筑只读";
            return;
        }

        string query = _search?.Text.Trim() ?? string.Empty;
        int kindFilter = _kindFilter?.Selected ?? 0;
        SkillStoneInstance[] stones = management.UninstalledSkillStones
            .Where(stone => kindFilter == 0 || kindFilter == 1 && stone.Definition.Kind == SkillStoneKind.Active ||
                            kindFilter == 2 && stone.Definition.Kind == SkillStoneKind.Support)
            .Where(stone => query.Length == 0 || stone.Definition.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            stone.Definition.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            stone.Definition.Tags.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            stone.Definition.SupportedTags.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(stone => stone.Definition.Kind)
            .ThenBy(stone => stone.Definition.DisplayName, StringComparer.Ordinal).ToArray();
        foreach (SkillStoneInstance stone in stones)
        {
            _inventory.AddChild(new P7SkillStoneCell
            {
                StoneInstanceId = stone.InstanceId,
                Text = stone.Definition.Kind == SkillStoneKind.Active ? "◆" : "◇",
                TooltipText = P7SkillTooltip.Build(stone, "技能石背包"),
                CustomMinimumSize = new Vector2(44, 44),
            });
        }
        int visibleSlots = Math.Max(36, ((stones.Length + 11) / 12) * 12);
        for (int index = stones.Length; index < visibleSlots; index++)
            _inventory.AddChild(new Button { Disabled = true, CustomMinimumSize = new Vector2(44, 44) });

        var invalid = new List<string>();
        foreach (P5SkillChainDefinition chain in chains)
        {
            SkillLinkConfiguration? link = management.SkillLinks.FirstOrDefault(item => item.ChainId == chain.StableId);
            string?[] sockets = link?.SocketStoneInstanceIds?.Take(chain.TotalSockets).ToArray() ?? LegacySockets(link, chain.TotalSockets);
            Array.Resize(ref sockets, chain.TotalSockets);
            var panel = new VBoxContainer();
            panel.AddThemeStyleboxOverride("panel", Frame());
            panel.AddChild(new Label { Text = chain.DisplayName, TooltipText = $"来源：{chain.SourceSlot} · {chain.TotalSockets} 个相连孔" });
            var row = new HBoxContainer();
            panel.AddChild(row);
            bool hasActive = false;
            for (int index = 0; index < chain.TotalSockets; index++)
            {
                SkillStoneInstance? stone = string.IsNullOrEmpty(sockets[index]) ? null : management.SkillStones.FirstOrDefault(item => item.InstanceId == sockets[index]);
                hasActive |= stone?.Definition.Kind == SkillStoneKind.Active;
                row.AddChild(new P6SkillSocketZone
                {
                    Panel = this, ChainId = chain.StableId, SocketIndex = index, StoneInstanceId = stone?.InstanceId ?? string.Empty,
                    Text = stone is null ? "○" : stone.Definition.Kind == SkillStoneKind.Active ? "◆" : "◇",
                    TooltipText = stone is null ? $"空连接孔 {index + 1}\n从左侧技能石背包拖入" : P7SkillTooltip.Build(stone, $"{chain.DisplayName} · 孔 {index + 1}") + "\n右键卸下",
                    CustomMinimumSize = new Vector2(48, 44),
                });
                if (index + 1 < chain.TotalSockets) row.AddChild(new Label { Text = "—", VerticalAlignment = VerticalAlignment.Center });
            }
            if (!hasActive && sockets.Any(id => !string.IsNullOrEmpty(id)))
            {
                panel.AddChild(new Label { Text = "等待主动技能 · 当前整组不产生战斗效果", Modulate = new Color("d39b58") });
                invalid.Add($"{chain.DisplayName} 等待主动技能");
            }
            if (link is not null && !string.IsNullOrEmpty(link.ActiveStoneInstanceId))
            {
                SkillStoneInstance active = management.SkillStones.Single(stone => stone.InstanceId == link.ActiveStoneInstanceId);
                if (active.Definition.Tags.HasFlag(SkillTag.Attack)) panel.AddChild(BuildTargetSelector(session, link));
            }
            _groups.AddChild(panel);
        }
        if (chains.Count == 0)
        {
            _groups.AddChild(new Label { Text = "当前装备没有连接孔组。" });
            invalid.Add("没有提供连接孔的装备");
        }
        _errors.Text = invalid.Count == 0 ? "构筑错误：无" : "构筑错误：" + string.Join("；", invalid);
    }

    public void DropOnSocket(string chainId, int socketIndex, string stoneInstanceId)
    {
        if (_readOnly || _session is null) return;
        bool changed = _session().TryPlaceSkillStone(chainId, socketIndex, stoneInstanceId);
        _changed?.Invoke(changed ? "技能石已装入唯一孔位。" : "该孔位不兼容、同名辅助重复或连接组已有主动技能。");
        Invalidate();
    }

    public void Unsocket(string chainId, int socketIndex)
    {
        if (_readOnly || _session is null) return;
        bool changed = _session().UnsocketSkillStone(chainId, socketIndex);
        _changed?.Invoke(changed ? "技能石已返回技能石背包。" : "孔位没有变化。");
        Invalidate();
    }

    private Control BuildTargetSelector(P1GameSession session, SkillLinkConfiguration link)
    {
        SkillTargetPolicy selected = link.AiRule?.TargetPolicy ?? SkillTargetPolicy.AllEnemies;
        var row = new HFlowContainer();
        row.AddChild(new Label { Text = "攻击目标：", VerticalAlignment = VerticalAlignment.Center });
        var group = new ButtonGroup();
        AddTarget(row, group, "仅 Boss", SkillTargetPolicy.BossOnly, selected, session, link.ActiveStoneInstanceId);
        AddTarget(row, group, "仅精英和 Boss", SkillTargetPolicy.EliteAndBoss, selected, session, link.ActiveStoneInstanceId);
        AddTarget(row, group, "所有敌人", SkillTargetPolicy.AllEnemies, selected, session, link.ActiveStoneInstanceId);
        return row;
    }

    private void AddTarget(Container row, ButtonGroup group, string text, SkillTargetPolicy policy, SkillTargetPolicy selected, P1GameSession session, string activeStoneId)
    {
        var button = new CheckBox { Text = text, ButtonGroup = group, ButtonPressed = selected == policy };
        button.Pressed += () =>
        {
            bool changed = session.ConfigureSkillTarget(activeStoneId, policy);
            _changed?.Invoke(changed ? $"攻击目标已切换为：{text}。" : "攻击目标配置没有变化。");
            Invalidate();
        };
        row.AddChild(button);
    }

    private void Invalidate() { _signature = string.Empty; RefreshState(); }

    private void QueueResponsiveLayout()
    {
        bool compact = Size.X > 0 && Size.X < 960;
        if (compact == _compact) return;
        _compact = compact;
        Callable.From(ApplyResponsiveLayout).CallDeferred();
    }

    private void ApplyResponsiveLayout()
    {
        if (_wideColumns is null || _compactStack is null || _inventoryColumn is null || _groupsColumn is null) return;
        if (_compact)
        {
            _inventoryColumn.Reparent(_compactStack);
            _groupsColumn.Reparent(_compactStack);
            _wideColumns.Visible = false;
            _compactStack.Visible = true;
        }
        else
        {
            _inventoryColumn.Reparent(_wideColumns);
            _groupsColumn.Reparent(_wideColumns);
            _compactStack.Visible = false;
            _wideColumns.Visible = true;
        }
    }

    private static string?[] LegacySockets(SkillLinkConfiguration? link, int count)
    {
        if (link is null) return new string?[count];
        string?[] result = new string?[] { link.ActiveStoneInstanceId }.Concat(link.SupportStoneInstanceIds.Cast<string?>()).Take(count).ToArray();
        Array.Resize(ref result, count);
        return result;
    }

    private static VBoxContainer Column(Container parent, string title, float minimumWidth)
    {
        var column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(minimumWidth, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddChild(new Label { Text = title });
        parent.AddChild(column);
        return column;
    }

    private static void Clear(Node node) { foreach (Node child in node.GetChildren()) child.QueueFree(); }

    private static PanelContainer FramedScroll(ScrollContainer scroll)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Frame());
        panel.AddChild(scroll);
        return panel;
    }

    private static StyleBoxFlat Frame() => new()
    {
        BgColor = new Color("151a22"), BorderColor = new Color("786747"), BorderWidthLeft = 1, BorderWidthTop = 1,
        BorderWidthRight = 1, BorderWidthBottom = 1, ContentMarginLeft = 8, ContentMarginTop = 6, ContentMarginRight = 8, ContentMarginBottom = 6,
    };
}

public static class P7SkillTooltip
{
    public static string Build(SkillStoneInstance stone, string location)
    {
        SkillStoneDefinition definition = stone.Definition;
        string tags = definition.Kind == SkillStoneKind.Active ? definition.Tags.ToString() : definition.SupportedTags.ToString();
        string mechanics = string.Empty;
        string compatibility = string.Empty;
        if (definition.Kind == SkillStoneKind.Active)
        {
            string skillId = definition.StableId.Replace("core.skill_stone.", "core.skill.", StringComparison.Ordinal);
            try
            {
                SkillDefinition skill = P1Skills.Get(skillId);
                mechanics = $"\n法力消耗 {skill.BaseManaCost} · 范围 {skill.RangeRaw / 1000.0:0.#}m · 施法 {skill.CastTimeTicks * 50}ms · 冷却 {skill.CooldownTicks * 50}ms";
                compatibility = $"\n执行能力：{definition.Capabilities}";
            }
            catch (KeyNotFoundException) { }
        }
        else
        {
            compatibility = $"\n辅助条件：全部[{definition.RequiredAllCapabilities}] · 任一[{definition.RequiredAnyCapabilities}] · 排除[{definition.ExcludedCapabilities}]";
        }
        return $"{definition.DisplayName}\n{(definition.Kind == SkillStoneKind.Active ? "主动" : "辅助")}技能石 · Lv.{stone.Level}/20 · XP {stone.Experience}\n" +
               $"标签：{tags}{mechanics}{compatibility}\n{definition.Description}\n位置：{location}\n来源：" +
               (stone.InstanceId.StartsWith("starter-", StringComparison.Ordinal) ? "初始技能" : "战斗掉落");
    }
}

public partial class P7SkillStoneCell : Button
{
    public string StoneInstanceId { get; set; } = string.Empty;
    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label { Text = Text };
        SetDragPreview(preview);
        return Variant.From($"p2-skill|{StoneInstanceId}");
    }
}

public partial class P6SkillSocketZone : Button
{
    public P2SkillStonePanel? Panel { get; set; }
    public string ChainId { get; set; } = string.Empty;
    public int SocketIndex { get; set; }
    public string StoneInstanceId { get; set; } = string.Empty;
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (string.IsNullOrEmpty(StoneInstanceId)) return default;
        var preview = new Label { Text = Text };
        SetDragPreview(preview);
        return Variant.From($"p2-skill|{StoneInstanceId}");
    }
    public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.String && data.AsString().StartsWith("p2-skill|", StringComparison.Ordinal);
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        string[] parts = data.AsString().Split('|');
        if (parts.Length == 2) Panel?.DropOnSocket(ChainId, SocketIndex, parts[1]);
    }
    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && !string.IsNullOrEmpty(StoneInstanceId))
        {
            Panel?.Unsocket(ChainId, SocketIndex);
            AcceptEvent();
        }
    }
}

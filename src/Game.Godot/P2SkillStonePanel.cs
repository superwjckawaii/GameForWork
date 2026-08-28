using GameForWork.Core.P1;
using GameForWork.Core.P2;
using GameForWork.Core.P5;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2SkillStonePanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private VBoxContainer? _inventory;
    private VBoxContainer? _groups;
    private Label? _details;
    private Label? _errors;
    private string _signature = string.Empty;
    private bool _readOnly;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        AddChild(new Label { Text = "装备实例连接孔组 · 每组最多一个主动技能；技能石只能位于仓库或一个孔位" });
        var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 10);
        AddChild(columns);
        VBoxContainer left = Column(columns, "未安装技能石", 205);
        var inventoryScroll = new ScrollContainer { CustomMinimumSize = new Vector2(195, 270) };
        left.AddChild(inventoryScroll);
        _inventory = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inventoryScroll.AddChild(_inventory);
        VBoxContainer middle = Column(columns, "当前装备孔组", 430);
        _groups = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        middle.AddChild(_groups);
        VBoxContainer right = Column(columns, "技能说明与兼容性", 235);
        _details = new Label
        {
            Text = "选择技能石查看实例与最终效果。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        right.AddChild(_details);
        var schemes = new HFlowContainer();
        AddChild(schemes);
        schemes.AddChild(new Label { Text = "技能方案：" });
        foreach (string name in new[] { "清图", "Boss", "自定义" })
        {
            schemes.AddChild(new Button { Text = name, Disabled = true, TooltipText = "P6 第四批开放方案切换" });
        }
        _errors = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_errors);
    }

    public void SetReadOnly(bool readOnly)
    {
        _readOnly = readOnly;
        _signature = string.Empty;
        RefreshState();
    }

    public void RefreshState()
    {
        if (_session is null || _inventory is null || _groups is null || _details is null || _errors is null) return;
        P1GameSession session = _session();
        P2ManagementState management = session.Management;
        IReadOnlyList<P5SkillChainDefinition> chains = session.GetSkillChains();
        string signature = _readOnly + "|" + string.Join('|', management.SkillStones.Select(item =>
            $"{item.InstanceId}:{item.Level}:{item.Experience}")) + "|" + string.Join('|', management.SkillLinks.Select(item =>
            $"{item.ChainId}:{string.Join(',', item.SocketStoneInstanceIds ?? [])}")) + "|" +
            string.Join('|', chains.Select(chain => $"{chain.StableId}:{chain.TotalSockets}"));
        if (signature == _signature) return;
        _signature = signature;
        Clear(_inventory);
        Clear(_groups);
        if (_readOnly)
        {
            _inventory.AddChild(new Label { Text = "佣兵技能由装备孔组和自主成长生成；玩家只能查看。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            _errors.Text = "佣兵构筑只读";
            return;
        }

        foreach (SkillStoneInstance stone in management.UninstalledSkillStones
                     .OrderBy(stone => stone.Definition.Kind).ThenBy(stone => stone.Definition.DisplayName, StringComparer.Ordinal))
        {
            P2SkillStoneButton button = StoneButton(stone);
            button.Pressed += () => ShowDetails(stone, installed: false);
            _inventory.AddChild(button);
        }
        if (management.UninstalledSkillStones.Count == 0)
        {
            _inventory.AddChild(new Label { Text = "所有技能石均已安装。", Modulate = new Color("7c8490") });
        }

        var invalid = new List<string>();
        foreach (P5SkillChainDefinition chain in chains)
        {
            SkillLinkConfiguration? link = management.SkillLinks.FirstOrDefault(item => item.ChainId == chain.StableId);
            string?[] sockets = link?.SocketStoneInstanceIds?.Take(chain.TotalSockets).ToArray() ?? LegacySockets(link, chain.TotalSockets);
            Array.Resize(ref sockets, chain.TotalSockets);
            var panel = new VBoxContainer();
            panel.AddThemeStyleboxOverride("panel", Frame());
            panel.AddChild(new Label { Text = chain.DisplayName, TooltipText = $"来源：{chain.SourceSlot} · 共 {chain.TotalSockets} 个相连孔" });
            var row = new HBoxContainer();
            panel.AddChild(row);
            bool hasActive = false;
            for (int index = 0; index < chain.TotalSockets; index++)
            {
                int socketIndex = index;
                SkillStoneInstance? stone = string.IsNullOrEmpty(sockets[index]) ? null :
                    management.SkillStones.FirstOrDefault(item => item.InstanceId == sockets[index]);
                hasActive |= stone?.Definition.Kind == SkillStoneKind.Active;
                var socket = new P6SkillSocketZone
                {
                    Panel = this, ChainId = chain.StableId, SocketIndex = index,
                    StoneInstanceId = stone?.InstanceId ?? string.Empty,
                    Text = stone is null ? "○" : stone.Definition.Kind == SkillStoneKind.Active ? "◆" : "◇",
                    TooltipText = stone is null ? $"空连接孔 {index + 1}" :
                        $"{stone.Definition.DisplayName}\n等级 {stone.Level} · XP {stone.Experience}\n拖动可换孔；右键卸下",
                    CustomMinimumSize = new Vector2(45, 42),
                };
                if (stone is not null) socket.Pressed += () => ShowDetails(stone, installed: true);
                row.AddChild(socket);
                if (index + 1 < chain.TotalSockets)
                {
                    row.AddChild(new Label { Text = "—", VerticalAlignment = VerticalAlignment.Center });
                }
            }
            if (!hasActive && sockets.Any(id => !string.IsNullOrEmpty(id)))
            {
                panel.AddChild(new Label { Text = "等待主动技能 · 当前整组不产生战斗效果", Modulate = new Color("d39b58") });
                invalid.Add($"{chain.DisplayName} 等待主动技能");
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
        _signature = string.Empty;
        RefreshState();
    }

    public void Unsocket(string chainId, int socketIndex)
    {
        if (_readOnly || _session is null) return;
        bool changed = _session().UnsocketSkillStone(chainId, socketIndex);
        _changed?.Invoke(changed ? "技能石已返回角色仓库。" : "孔位没有变化。");
        _signature = string.Empty;
        RefreshState();
    }

    private void ShowDetails(SkillStoneInstance stone, bool installed)
    {
        _details!.Text = $"{stone.Definition.DisplayName}\n{stone.Definition.Kind}技能石\n" +
            $"等级 {stone.Level}/20 · 经验 {stone.Experience}\n位置：{(installed ? "已安装" : "角色技能石仓库")}\n\n" +
            $"实例 ID：{stone.InstanceId}\n来源：{(stone.InstanceId.StartsWith("starter-", StringComparison.Ordinal) ? "初始技能" : "战斗掉落")}";
    }

    private static P2SkillStoneButton StoneButton(SkillStoneInstance stone) => new()
    {
        StoneInstanceId = stone.InstanceId,
        Text = stone.Definition.Kind == SkillStoneKind.Active ? $"◆ {stone.Definition.DisplayName}" : $"◇ {stone.Definition.DisplayName}",
        TooltipText = $"{stone.Definition.Kind}技能石 · Lv.{stone.Level} · XP {stone.Experience}\n按住 Alt 查看实例详情",
        CustomMinimumSize = new Vector2(175, 36),
    };

    private static string?[] LegacySockets(SkillLinkConfiguration? link, int count)
    {
        if (link is null) return new string?[count];
        string?[] result = new string?[] { link.ActiveStoneInstanceId }
            .Concat(link.SupportStoneInstanceIds.Cast<string?>()).Take(count).ToArray();
        Array.Resize(ref result, count);
        return result;
    }

    private static VBoxContainer Column(Container parent, string title, float minimumWidth)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(minimumWidth, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(new Label { Text = title });
        parent.AddChild(column);
        return column;
    }

    private static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren()) child.QueueFree();
    }

    private static StyleBoxFlat Frame() => new()
    {
        BgColor = new Color("151a22"), BorderColor = new Color("786747"),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        ContentMarginLeft = 8, ContentMarginTop = 6, ContentMarginRight = 8, ContentMarginBottom = 6,
    };
}

public partial class P2SkillStoneButton : Button
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

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.VariantType == Variant.Type.String && data.AsString().StartsWith("p2-skill|", StringComparison.Ordinal);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        string[] parts = data.AsString().Split('|');
        if (parts.Length == 2) Panel?.DropOnSocket(ChainId, SocketIndex, parts[1]);
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } &&
            !string.IsNullOrEmpty(StoneInstanceId))
        {
            Panel?.Unsocket(ChainId, SocketIndex);
            AcceptEvent();
        }
    }
}

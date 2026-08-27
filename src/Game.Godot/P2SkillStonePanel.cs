using GameForWork.Core.P1;
using GameForWork.Core.P2;
using GameForWork.Core.P5;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2SkillStonePanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private HFlowContainer? _inventory;
    private VBoxContainer? _links;
    private string _signature = string.Empty;
    private bool _readOnly;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        AddChild(new Label { Text = "技能石仓库 · 装备生成技能链；主动石放入核心孔，辅助石放入相连孔" });
        _inventory = new HFlowContainer();
        AddChild(_inventory);
        _links = new VBoxContainer();
        AddChild(_links);
    }

    public void SetReadOnly(bool readOnly)
    {
        if (_readOnly == readOnly)
        {
            return;
        }

        _readOnly = readOnly;
        _signature = string.Empty;
        RefreshState();
    }

    public void RefreshState()
    {
        if (_session is null || _inventory is null || _links is null)
        {
            return;
        }

        P1GameSession session = _session();
        P2ManagementState management = session.Management;
        IReadOnlyList<P5SkillChainDefinition> chains = session.GetSkillChains();
        string signature = _readOnly + "|" + string.Join('|', management.SkillStones.Select(item =>
            $"{item.InstanceId}:{item.Level}:{item.Experience}")) + "|" + string.Join('|', management.SkillLinks.Select(item =>
            $"{item.ActiveStoneInstanceId}:{item.ChainId}:{string.Join(',', item.SupportStoneInstanceIds)}")) + "|" +
            string.Join('|', chains.Select(chain => $"{chain.StableId}:{chain.SupportCapacity}"));
        if (signature == _signature)
        {
            return;
        }

        _signature = signature;
        Clear(_inventory);
        Clear(_links);
        if (_readOnly)
        {
            _inventory.AddChild(new Label
            {
                Text = "佣兵技能链由装备与自主成长共同生成，玩家只能查看最终结果。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            return;
        }

        foreach (SkillStoneInstance stone in management.SkillStones)
        {
            var button = new P2SkillStoneButton
            {
                StoneInstanceId = stone.InstanceId,
                Text = stone.Definition.Kind == SkillStoneKind.Active
                    ? $"◆ {stone.Definition.DisplayName}"
                    : $"◇ {stone.Definition.DisplayName}",
                TooltipText = $"{stone.Definition.Kind} 技能石\n等级 {stone.Level} · 经验 {stone.Experience}\n" +
                    (stone.Definition.Kind == SkillStoneKind.Active ? "拖到装备生成的核心孔" : "拖到相连辅助孔"),
                CustomMinimumSize = new Vector2(116, 42),
            };
            _inventory.AddChild(button);
        }

        foreach (P5SkillChainDefinition chain in chains)
        {
            SkillLinkConfiguration? link = management.SkillLinks.FirstOrDefault(item => item.ChainId == chain.StableId);
            SkillStoneInstance? active = link is null ? null :
                management.SkillStones.Single(item => item.InstanceId == link.ActiveStoneInstanceId);
            var zone = new P2SkillLinkZone
            {
                Panel = this,
                ChainId = chain.StableId,
                ActiveStoneInstanceId = active?.InstanceId ?? string.Empty,
                CustomMinimumSize = new Vector2(0, 92),
            };
            zone.AddThemeStyleboxOverride("panel", Frame());
            zone.AddChild(new Label
            {
                Text = $"{chain.DisplayName}　◆ {(active?.Definition.DisplayName ?? "空核心孔")}　" +
                       $"辅助 {link?.SupportStoneInstanceIds.Count ?? 0}/{chain.SupportCapacity}",
                TooltipText = $"来源：{chain.SourceSlot}\n此链提供 {chain.SupportCapacity} 个相连辅助孔。",
            });
            var sockets = new HFlowContainer();
            zone.AddChild(sockets);
            foreach (string supportId in link?.SupportStoneInstanceIds ?? [])
            {
                SkillStoneInstance support = management.SkillStones.Single(item => item.InstanceId == supportId);
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = $"◇ {support.Definition.DisplayName}" });
                var remove = new Button { Text = "×", TooltipText = "解除连接；技能石返回技能石仓库" };
                remove.Pressed += () =>
                {
                    if (session.UnlinkSkillSupport(active!.InstanceId, support.InstanceId))
                    {
                        _changed?.Invoke("辅助技能石已解除连接。");
                        _signature = string.Empty;
                        RefreshState();
                    }
                };
                row.AddChild(remove);
                sockets.AddChild(row);
            }

            for (int index = link?.SupportStoneInstanceIds.Count ?? 0; index < chain.SupportCapacity; index++)
            {
                sockets.AddChild(new Label { Text = "◇ 空", Modulate = new Color("7c8490") });
            }

            _links.AddChild(zone);
        }
    }

    public void DropOnChain(string chainId, string activeInstanceId, string droppedInstanceId)
    {
        if (_readOnly || _session is null)
        {
            return;
        }

        P1GameSession session = _session();
        SkillStoneInstance? dropped = session.Management.SkillStones.FirstOrDefault(item => item.InstanceId == droppedInstanceId);
        if (dropped?.Definition.Kind == SkillStoneKind.Active)
        {
            bool assigned = session.TryAssignActiveSkill(droppedInstanceId, chainId);
            _changed?.Invoke(assigned ? "主动技能石已装入装备技能链。" : "该技能与此核心孔不兼容。");
            _signature = string.Empty;
            RefreshState();
            return;
        }

        if (dropped?.Definition.Kind != SkillStoneKind.Support || string.IsNullOrEmpty(activeInstanceId))
        {
            _changed?.Invoke("请先把主动技能石装入此链的核心孔。");
            return;
        }

        bool linked = session.TryLinkSkillSupport(activeInstanceId, droppedInstanceId);
        _changed?.Invoke(linked ? "辅助技能石已连接。" : "该辅助已经连接或装备提供的连接孔已满。");
        _signature = string.Empty;
        RefreshState();
    }

    private static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static StyleBoxFlat Frame() => new()
    {
        BgColor = new Color("151a22"),
        BorderColor = new Color("786747"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        ContentMarginLeft = 8,
        ContentMarginTop = 8,
        ContentMarginRight = 8,
        ContentMarginBottom = 8,
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

public partial class P2SkillLinkZone : VBoxContainer
{
    public P2SkillStonePanel? Panel { get; set; }
    public string ChainId { get; set; } = string.Empty;
    public string ActiveStoneInstanceId { get; set; } = string.Empty;

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.VariantType == Variant.Type.String && data.AsString().StartsWith("p2-skill|", StringComparison.Ordinal);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        string[] parts = data.AsString().Split('|');
        if (parts.Length == 2)
        {
            Panel?.DropOnChain(ChainId, ActiveStoneInstanceId, parts[1]);
        }
    }
}

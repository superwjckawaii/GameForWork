using GameForWork.Core.P1;
using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2SkillStonePanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private HFlowContainer? _inventory;
    private HBoxContainer? _links;
    private string _signature = string.Empty;
    private bool _readOnly;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        AddChild(new Label { Text = "技能石背包 · 拖拽辅助石到主动技能连接区" });
        _inventory = new HFlowContainer();
        AddChild(_inventory);
        _links = new HBoxContainer();
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

        P2ManagementState management = _session().Management;
        string signature = _readOnly + "|" + string.Join('|', management.SkillStones.Select(item =>
            $"{item.InstanceId}:{item.Level}:{item.Experience}")) + "|" + string.Join('|', management.SkillLinks.Select(item =>
            $"{item.ActiveStoneInstanceId}:{string.Join(',', item.SupportStoneInstanceIds)}"));
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
                Text = "佣兵技能与辅助由自主成长配置，玩家只能查看最终结果。",
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
                TooltipText = $"{stone.Definition.Kind} 技能石\n等级 {stone.Level} · 经验 {stone.Experience}\n拖拽到主动技能连接区",
                CustomMinimumSize = new Vector2(116, 42),
            };
            _inventory.AddChild(button);
        }

        foreach (SkillStoneInstance active in management.SkillStones.Where(item => item.Definition.Kind == SkillStoneKind.Active))
        {
            var zone = new P2SkillLinkZone
            {
                Panel = this,
                ActiveStoneInstanceId = active.InstanceId,
                CustomMinimumSize = new Vector2(250, 120),
            };
            zone.AddThemeStyleboxOverride("panel", Frame());
            zone.AddChild(new Label { Text = $"◆ {active.Definition.DisplayName} · 支持拖入辅助" });
            SkillLinkConfiguration? link = management.SkillLinks.FirstOrDefault(item => item.ActiveStoneInstanceId == active.InstanceId);
            foreach (string supportId in link?.SupportStoneInstanceIds ?? [])
            {
                SkillStoneInstance support = management.SkillStones.Single(item => item.InstanceId == supportId);
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = $"└ ◇ {support.Definition.DisplayName}", SizeFlagsHorizontal = SizeFlags.ExpandFill });
                var remove = new Button { Text = "解除" };
                remove.Pressed += () =>
                {
                    if (management.UnlinkSupport(active.InstanceId, support.InstanceId))
                    {
                        _session().SyncHeavyStrikeFromSkillStones();
                        _changed?.Invoke("辅助技能石已解除连接。");
                        _signature = string.Empty;
                        RefreshState();
                    }
                };
                row.AddChild(remove);
                zone.AddChild(row);
            }

            _links.AddChild(zone);
        }
    }

    public void DropOnActive(string activeInstanceId, string droppedInstanceId)
    {
        if (_readOnly || _session is null)
        {
            return;
        }

        P2ManagementState management = _session().Management;
        SkillStoneInstance? dropped = management.SkillStones.FirstOrDefault(item => item.InstanceId == droppedInstanceId);
        if (dropped?.Definition.Kind != SkillStoneKind.Support)
        {
            _changed?.Invoke("只有辅助技能石可以连接到主动技能。");
            return;
        }

        bool linked = management.TryLinkSupport(activeInstanceId, droppedInstanceId);
        if (linked)
        {
            _session().SyncHeavyStrikeFromSkillStones();
        }
        _changed?.Invoke(linked ? "辅助技能石已连接。" : "该辅助已经连接或连接已满。 ");
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
    public string ActiveStoneInstanceId { get; set; } = string.Empty;

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.VariantType == Variant.Type.String && data.AsString().StartsWith("p2-skill|", StringComparison.Ordinal);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        string[] parts = data.AsString().Split('|');
        if (parts.Length == 2)
        {
            Panel?.DropOnActive(ActiveStoneInstanceId, parts[1]);
        }
    }
}

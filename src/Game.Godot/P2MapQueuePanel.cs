using GameForWork.Core.P1;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2MapQueuePanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private OptionButton? _selectedTeam;
    private HBoxContainer? _columns;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        var header = new HBoxContainer();
        header.AddChild(new Label { Text = "双击地图加入当前队伍；拖拽排序或跨队移动。正在运行的地图不在队列中。" });
        _selectedTeam = new OptionButton();
        _selectedTeam.AddItem("主角队", (int)ExpeditionTeamKind.Hero);
        _selectedTeam.AddItem("佣兵队", (int)ExpeditionTeamKind.Mercenaries);
        header.AddChild(_selectedTeam);
        AddChild(header);
        _columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _columns.AddThemeConstantOverride("separation", 12);
        AddChild(_columns);
    }

    public void RefreshState()
    {
        if (_session is null || _columns is null)
        {
            return;
        }

        P1GameSession session = _session();
        string signature = string.Join(',', session.World.MapInventory.Select(map => map.InstanceId)) + "|" +
            string.Join(',', session.World.Hero.Queue.Maps.Select(map => map.InstanceId)) + "|" +
            string.Join(',', session.World.Mercenaries.Queue.Maps.Select(map => map.InstanceId)) + "|" +
            session.World.Hero.ActiveMap?.InstanceId + "|" + session.World.Mercenaries.ActiveMap?.InstanceId;
        if (signature == _signature)
        {
            return;
        }

        _signature = signature;
        foreach (Node child in _columns.GetChildren())
        {
            child.QueueFree();
        }

        AddColumn("地图仓库", P2MapContainerKind.Inventory, session.World.MapInventory);
        AddColumn("主角队列", P2MapContainerKind.HeroQueue, session.World.Hero.Queue.Maps);
        AddColumn("佣兵队列", P2MapContainerKind.MercenaryQueue, session.World.Mercenaries.Queue.Maps);
    }

    public void ReceiveDrop(P2MapContainerKind source, int sourceIndex, P2MapContainerKind target, int targetIndex)
    {
        P2ItemCommandResult result = new P2MapCommandService(_session!()).Move(source, sourceIndex, target, targetIndex);
        _changed?.Invoke(result.Message);
        _signature = string.Empty;
        RefreshState();
    }

    private void AddColumn(string title, P2MapContainerKind kind, IReadOnlyList<P1MapItem> maps)
    {
        var column = new P2MapDropColumn
        {
            Panel = this,
            ContainerKind = kind,
            CustomMinimumSize = new Vector2(190, 160),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        column.AddChild(new Label { Text = $"{title} · {maps.Count}" });
        for (int index = 0; index < maps.Count; index++)
        {
            int captured = index;
            P1MapItem map = maps[index];
            var cell = new P2MapCell
            {
                Panel = this,
                ContainerKind = kind,
                SourceIndex = index,
                Text = $"{index + 1}. {map.InstanceId} · T{map.AreaLevel}",
                TooltipText = "左键选择 · 双击加入当前队伍 · 拖拽精确排序",
                Alignment = HorizontalAlignment.Left,
            };
            cell.ItemDoubleClicked += () =>
            {
                if (kind != P2MapContainerKind.Inventory)
                {
                    return;
                }

                ExpeditionTeamKind team = (ExpeditionTeamKind)_selectedTeam!.GetItemId(_selectedTeam.Selected);
                P2ItemCommandResult result = new P2MapCommandService(_session!()).AddToQueue(captured, team);
                _changed?.Invoke(result.Message);
                _signature = string.Empty;
                RefreshState();
            };
            column.AddChild(cell);
        }

        _columns!.AddChild(column);
    }
}

public partial class P2MapCell : Button
{
    public P2MapQueuePanel? Panel { get; set; }
    public P2MapContainerKind ContainerKind { get; set; }
    public int SourceIndex { get; set; }
    public event Action? ItemDoubleClicked;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label { Text = Text };
        SetDragPreview(preview);
        return Variant.From($"p2-map|{(int)ContainerKind}|{SourceIndex}");
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton
            {
                Pressed: true,
                ButtonIndex: MouseButton.Left,
                DoubleClick: true,
            })
        {
            ItemDoubleClicked?.Invoke();
            AcceptEvent();
        }
    }
}

public partial class P2MapDropColumn : VBoxContainer
{
    public P2MapQueuePanel? Panel { get; set; }
    public P2MapContainerKind ContainerKind { get; set; }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => TryParse(data, out _, out _);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!TryParse(data, out P2MapContainerKind source, out int sourceIndex))
        {
            return;
        }

        int target = Math.Max(0, GetChildCount() - 1);
        for (int index = 1; index < GetChildCount(); index++)
        {
            Control row = GetChild<Control>(index);
            if (atPosition.Y < row.Position.Y + row.Size.Y / 2)
            {
                target = index - 1;
                break;
            }
        }

        Panel?.ReceiveDrop(source, sourceIndex, ContainerKind, target);
    }

    private static bool TryParse(Variant data, out P2MapContainerKind source, out int index)
    {
        source = default;
        index = -1;
        if (data.VariantType != Variant.Type.String)
        {
            return false;
        }

        string[] parts = data.AsString().Split('|');
        if (parts.Length != 3 || parts[0] != "p2-map" || !int.TryParse(parts[1], out int raw) ||
            !Enum.IsDefined(typeof(P2MapContainerKind), raw) || !int.TryParse(parts[2], out index))
        {
            return false;
        }

        source = (P2MapContainerKind)raw;
        return index >= 0;
    }
}

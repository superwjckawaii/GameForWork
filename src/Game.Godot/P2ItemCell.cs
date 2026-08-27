using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2ItemCell : Button
{
    public P1ItemGrid? Grid { get; set; }
    public int CellIndex { get; set; }
    public bool HasItem { get; set; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!HasItem || Grid is null)
        {
            return default;
        }

        var preview = new Label
        {
            Text = Text.Length == 0 ? "◆" : Text,
            CustomMinimumSize = new Vector2(34, 34),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = new Color(1, 1, 1, 0.86f),
        };
        SetDragPreview(preview);
        return Variant.From($"p2-item|{(int)Grid.ContainerKind}|{CellIndex}");
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        Grid is not null && TryParse(data, out _, out _);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (Grid is not null && TryParse(data, out ItemContainerKind source, out int sourceIndex))
        {
            Grid.ReceiveDrop(source, sourceIndex, CellIndex);
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouse || !mouse.Pressed || Grid is null)
        {
            return;
        }

        if (mouse.ButtonIndex == MouseButton.Right && HasItem)
        {
            Grid.OpenContext(CellIndex, GetScreenPosition() + mouse.Position);
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex == MouseButton.Left && HasItem && mouse.DoubleClick)
        {
            Grid.Activate(CellIndex);
            AcceptEvent();
            return;
        }

        if (mouse.ButtonIndex == MouseButton.Left && HasItem && mouse.ShiftPressed)
        {
            Grid.QuickTransfer(CellIndex);
            AcceptEvent();
        }
    }

    private static bool TryParse(Variant data, out ItemContainerKind source, out int index)
    {
        source = default;
        index = -1;
        if (data.VariantType != Variant.Type.String)
        {
            return false;
        }

        string[] parts = data.AsString().Split('|');
        if (parts.Length != 3 || parts[0] != "p2-item" ||
            !int.TryParse(parts[1], out int rawSource) || !Enum.IsDefined(typeof(ItemContainerKind), rawSource) ||
            !int.TryParse(parts[2], out index) || index < 0)
        {
            return false;
        }

        source = (ItemContainerKind)rawSource;
        return true;
    }
}

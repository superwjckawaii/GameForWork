using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2ItemCell : Button
{
    public P1ItemGrid? Grid { get; set; }
    public int CellIndex { get; set; }
    public bool HasItem { get; set; }
    public Color TooltipRarityColor { get; set; } = new("d6d1c5");

    public override Control _MakeCustomTooltip(string forText)
    {
        string[] lines = forText.Split('\n');
        string first = lines.Length == 0 ? string.Empty : EscapeBbCode(lines[0]);
        string rest = string.Join('\n', lines.Skip(1).Select(EscapeBbCode));
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("10141bcc"),
            BorderColor = TooltipRarityColor.Darkened(0.15f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ContentMarginLeft = 9,
            ContentMarginRight = 9,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        });
        var text = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            CustomMinimumSize = new Vector2(285, Math.Max(34, lines.Length * 17)),
            Text = $"[color=#{TooltipRarityColor.ToHtml(false)}][font_size=15]{first}[/font_size][/color]" +
                   (rest.Length == 0 ? string.Empty : $"\n[font_size=12]{rest}[/font_size]"),
        };
        text.AddThemeConstantOverride("line_separation", -2);
        panel.AddChild(text);
        return panel;
    }

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
        return Variant.From($"p2-item|{(int)Grid.ContainerKind}|{Grid.ToExternalIndex(CellIndex)}");
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

    private static string EscapeBbCode(string text) => text.Replace("[", "[​", StringComparison.Ordinal);
}

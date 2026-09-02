using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2ItemCell : Button
{
    public P1ItemGrid? Grid { get; set; }
    public int CellIndex { get; set; }
    public bool HasItem { get; set; }
    public Color TooltipRarityColor { get; set; } = new("d6d1c5");
    public ItemInstance? TooltipItem { get; set; }
    public string ExtraTooltipText { get; set; } = string.Empty;

    public override Control _MakeCustomTooltip(string forText)
    {
        var panel = new P2ItemTooltipPanel();
        panel.Initialize(forText, TooltipItem, ExtraTooltipText, TooltipRarityColor);
        return panel;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!HasItem || Grid is null)
        {
            return default;
        }

        Control preview;
        if (Icon is not null)
        {
            var frame = new PanelContainer { CustomMinimumSize = new Vector2(42, 42), MouseFilter = MouseFilterEnum.Ignore };
            frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color("11151de6"), BorderColor = TooltipRarityColor,
                BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            });
            frame.AddChild(new TextureRect
            {
                Texture = Icon,
                CustomMinimumSize = new Vector2(38, 38),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            });
            preview = frame;
        }
        else
        {
            preview = new Label
            {
                Text = Text.Length == 0 ? "◆" : Text,
                CustomMinimumSize = new Vector2(38, 38),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(1, 1, 1, 0.9f),
            };
        }
        preview.Position = new Vector2(-20, -20);
        SetDragPreview(preview);
        return Variant.From($"p2-item|{(int)Grid.ContainerKind}|{Grid.ToExternalIndex(CellIndex)}");
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool valid = Grid is not null && TryParse(data, out ItemContainerKind source, out int index) &&
                     Grid.CanReceiveDrop(source, index, CellIndex);
        SelfModulate = valid ? new Color("b7efbd") : new Color("ef9b91");
        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (Grid is not null && TryParse(data, out ItemContainerKind source, out int sourceIndex))
        {
            SelfModulate = Colors.White;
            Grid.ReceiveDrop(source, sourceIndex, CellIndex);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd)
        {
            SelfModulate = Colors.White;
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

public partial class P2ItemTooltipPanel : PanelContainer
{
    private string _fallbackText = string.Empty;
    private ItemInstance? _item;
    private string _extraText = string.Empty;
    private Color _rarityColor;
    private RichTextLabel? _label;
    private bool _showDetails;

    public void Initialize(string fallbackText, ItemInstance? item, string extraText, Color rarityColor)
    {
        _fallbackText = fallbackText;
        _item = item;
        _extraText = extraText;
        _rarityColor = rarityColor;
    }

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("10141bcc"), BorderColor = _rarityColor.Darkened(0.15f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 9, ContentMarginRight = 9, ContentMarginTop = 6, ContentMarginBottom = 6,
        });
        _label = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false };
        _label.AddThemeConstantOverride("line_separation", -2);
        AddChild(_label);
        _showDetails = Input.IsKeyPressed(Key.Alt);
        RefreshText();
        SetProcess(_item is not null);
    }

    public override void _Process(double delta)
    {
        bool showDetails = Input.IsKeyPressed(Key.Alt);
        if (showDetails == _showDetails) return;
        _showDetails = showDetails;
        RefreshText();
    }

    private void RefreshText()
    {
        if (_label is null) return;
        string raw = _item is null ? _fallbackText : P1UiText.ItemTooltip(_item, _showDetails);
        if (_extraText.Length > 0) raw += $"\n\n{_extraText}";
        string[] lines = raw.Split('\n');
        string first = lines.Length == 0 ? string.Empty : EscapeBbCode(lines[0]);
        string rest = string.Join('\n', lines.Skip(1).Select(FormatTooltipLine));
        _label.CustomMinimumSize = new Vector2(285, Math.Max(34, lines.Length * 17));
        _label.Text = $"[color=#{_rarityColor.ToHtml(false)}][font_size=15]{first}[/font_size][/color]" +
                      (rest.Length == 0 ? string.Empty : $"\n[font_size=12]{rest}[/font_size]");
    }

    private static string EscapeBbCode(string text) => text.Replace("[", "[​", StringComparison.Ordinal);

    private static string FormatTooltipLine(string line)
    {
        const string legendaryMarker = "[LEGENDARY]";
        if (line.StartsWith(legendaryMarker, StringComparison.Ordinal))
        {
            return $"[color=#{P1UiText.LegendaryAffixColor.ToHtml(false)}]" +
                   $"{EscapeBbCode(line[legendaryMarker.Length..])}[/color]";
        }

        const string marker = "[TIER:";
        if (!line.StartsWith(marker, StringComparison.Ordinal))
        {
            return EscapeBbCode(line);
        }

        int end = line.IndexOf(']');
        if (end < marker.Length || !int.TryParse(line.AsSpan(marker.Length, end - marker.Length), out int tier))
        {
            return EscapeBbCode(line);
        }

        Color color = P1UiText.AffixTierColor(tier);
        return $"[color=#{color.ToHtml(false)}]{EscapeBbCode(line[(end + 1)..])}[/color]";
    }
}

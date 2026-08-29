using GameForWork.Core.P1;
using GameForWork.Core.P1.Progression;
using Godot;

namespace GameForWork.GodotClient;

public partial class P205JewelStashPanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private GridContainer? _grid;
    private string _signature = string.Empty;
    private P21ArtAtlas? _art;

    public void Initialize(Func<P1GameSession> session)
    {
        _session = session;
        _art = new P21ArtAtlas();
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(new Label
        {
            Text = "珠宝仓 · 拖到天赋树的记忆棱孔",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        AddChild(new Label
        {
            Text = "每种独特记忆珠宝各一枚；镶嵌后会离开仓库。先在天赋树选中孔位，再拖入即可。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("a9a18f"),
        });
        _grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 6);
        _grid.AddThemeConstantOverride("v_separation", 6);
        AddChild(_grid);
    }

    public void RefreshState()
    {
        if (_session is null || _grid is null) return;
        IReadOnlyDictionary<string, PassiveJewelKind> socketed = _session().Passives.SocketedJewels;
        string signature = string.Join('|', socketed.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
        if (signature == _signature) return;
        _signature = signature;
        foreach (Node child in _grid.GetChildren()) child.QueueFree();
        foreach (PassiveJewelKind jewel in Enum.GetValues<PassiveJewelKind>())
        {
            bool isInstalled = socketed.Any(pair => pair.Value == jewel);
            KeyValuePair<string, PassiveJewelKind> installed = socketed.FirstOrDefault(pair => pair.Value == jewel);
            Color color = P205JewelVisual.ColorFor(jewel);
            Texture2D? jewelIcon = isInstalled ? null : _art?.JewelIcon((int)jewel);
            var cell = new P205JewelStashCell
            {
                Jewel = jewel,
                JewelColor = color,
                Icon = jewelIcon,
                Text = isInstalled ? "已镶嵌" : jewelIcon is null ? P205JewelVisual.Glyph(jewel) : string.Empty,
                ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Center,
                Disabled = isInstalled,
                CustomMinimumSize = new Vector2(78, 62),
                TooltipText = $"{P205JewelVisual.Name(jewel)}\n{P205JewelVisual.Description(jewel)}\n" +
                              (isInstalled ? $"位置：{P1PassiveTree.Get(installed.Key).DisplayName}\n先点击天赋页的‘取下珠宝’。" : "状态：珠宝仓中，可拖曳镶嵌。"),
            };
            cell.AddThemeColorOverride("font_color", color);
            cell.AddThemeColorOverride("font_hover_color", color.Lightened(.18f));
            _grid.AddChild(cell);
        }
    }
}

public partial class P205JewelStashCell : Button
{
    public PassiveJewelKind Jewel { get; set; }
    public Color JewelColor { get; set; } = Colors.White;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Disabled) return default;
        SetDragPreview(P205JewelVisual.DragPreview(Icon, Jewel, JewelColor));
        return Variant.From($"p205-jewel|{(int)Jewel}");
    }
}

internal static class P205JewelVisual
{
    public static string Name(PassiveJewelKind jewel) => jewel switch
    {
        PassiveJewelKind.CrimsonMemory => "赤铁记忆",
        PassiveJewelKind.VerdantMemory => "翠生记忆",
        _ => "苍风记忆",
    };

    public static string Glyph(PassiveJewelKind jewel) => jewel switch
    {
        PassiveJewelKind.CrimsonMemory => "◆赤",
        PassiveJewelKind.VerdantMemory => "◆翠",
        _ => "◆苍",
    };

    public static string Description(PassiveJewelKind jewel) => jewel switch
    {
        PassiveJewelKind.CrimsonMemory => "按半径内已分配节点提高攻击伤害。",
        PassiveJewelKind.VerdantMemory => "按半径内已分配节点提高最大生命。",
        _ => "按半径内已分配节点提高移动速度。",
    };

    public static Color ColorFor(PassiveJewelKind jewel) => jewel switch
    {
        PassiveJewelKind.CrimsonMemory => new Color("d45f52"),
        PassiveJewelKind.VerdantMemory => new Color("60b57a"),
        _ => new Color("5c9ed8"),
    };

    public static Control DragPreview(Texture2D? icon, PassiveJewelKind jewel, Color color)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(46, 46), Position = new Vector2(-22, -22),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = color.Darkened(.72f), BorderColor = color,
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
        });
        Control content = icon is null ? new Label
        {
            Text = Glyph(jewel)[..1], HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
        } : new TextureRect
        {
            Texture = icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (content is Label label)
        {
            label.AddThemeColorOverride("font_color", color.Lightened(.2f));
            label.AddThemeFontSizeOverride("font_size", 25);
        }
        panel.AddChild(content);
        return panel;
    }
}

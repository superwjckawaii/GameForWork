using GameForWork.Core.P1;
using GameForWork.Core.P30;
using Godot;

namespace GameForWork.GodotClient;

public partial class P205JewelStashPanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private GridContainer? _grid;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session)
    {
        _session = session;
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(new Label { Text = "珠宝仓 · 240 格 · 12 列 · 拖到已分配的记忆棱孔", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _grid = new GridContainer { Columns = 12, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 3); _grid.AddThemeConstantOverride("v_separation", 3);
        AddChild(_grid);
    }

    public void RefreshState()
    {
        if (_session is null || _grid is null) return;
        P1GameSession session = _session();
        string signature = string.Join('|', session.Jewels.Items.OrderBy(j => j.InstanceId)
            .Select(j => $"{j.InstanceId}:{j.Corrupted}:{session.Jewels.Socketed.Values.Contains(j.InstanceId)}"));
        if (signature == _signature) return;
        _signature = signature;
        foreach (Node child in _grid.GetChildren()) child.QueueFree();
        foreach (P30JewelInstance jewel in session.Jewels.Items.OrderByDescending(j => j.Rarity).ThenBy(j => j.DisplayName))
        {
            string? socket = session.Jewels.Socketed.FirstOrDefault(pair => pair.Value == jewel.InstanceId).Key;
            Color color = ColorFor(jewel);
            var cell = new P205JewelStashCell
            {
                InstanceId = jewel.InstanceId, JewelColor = color, Disabled = socket is not null,
                Text = socket is null ? Glyph(jewel) : "已嵌", CustomMinimumSize = new Vector2(42, 42),
                TooltipText = $"{jewel.DisplayName}\n{jewel.Rarity} · 物品等级 {jewel.ItemLevel} · 共鸣度 {jewel.Resonance}%\n" +
                    string.Join('\n', jewel.Affixes.Select(a => $"T{a.Tier} {a.Effect}")) +
                    (jewel.Legendary is null ? string.Empty : $"\n{jewel.Legendary.Effect}\n来源：{jewel.Legendary.Source}") +
                    (socket is null ? "\n状态：珠宝仓中" : $"\n已镶嵌：{socket}"),
            };
            cell.AddThemeColorOverride("font_color", color); _grid.AddChild(cell);
        }
    }

    private static string Glyph(P30JewelInstance jewel) => jewel.Legendary is not null ? "◆" : jewel.Base switch
    { P30JewelBase.Crimson => "赤", P30JewelBase.Verdant => "翠", P30JewelBase.Golden => "金", P30JewelBase.Azure => "苍", _ => "四" };
    private static Color ColorFor(P30JewelInstance jewel) => jewel.Legendary is not null ? new Color("c58be2") : jewel.Base switch
    { P30JewelBase.Crimson => new("d45f52"), P30JewelBase.Verdant => new("60b57a"), P30JewelBase.Golden => new("d6ad55"), P30JewelBase.Azure => new("5c9ed8"), _ => new("d7d2c7") };
}

public partial class P205JewelStashCell : Button
{
    public string InstanceId { get; set; } = string.Empty;
    public Color JewelColor { get; set; } = Colors.White;
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Disabled) return default;
        var preview = new Label { Text = "◆", Position = new Vector2(-16, -16), CustomMinimumSize = new Vector2(32, 32),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        preview.AddThemeColorOverride("font_color", JewelColor); preview.AddThemeFontSizeOverride("font_size", 24);
        SetDragPreview(preview);
        return Variant.From($"p30-jewel|{InstanceId}");
    }
}

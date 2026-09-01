using GameForWork.Core.P1;
using GameForWork.Core.P4;
using GameForWork.Core.P30;
using Godot;

namespace GameForWork.GodotClient;

public partial class P205JewelStashPanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private GridContainer? _grid;
    private Label? _selection;
    private Button? _reroll;
    private Button? _dissolve;
    private Button? _corrupt;
    private Button? _unsocket;
    private string? _selectedInstanceId;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Action<string>? changed = null)
    {
        _session = session;
        _changed = changed;
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(new Label { Text = "珠宝仓 · 240 格 · 拖到已分配的记忆棱孔", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _grid = new GridContainer { Columns = 12, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 3); _grid.AddThemeConstantOverride("v_separation", 3);
        var scroll = new ScrollContainer
        {
            Name = "珠宝仓滚动区",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        scroll.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("0e1218"), BorderColor = new Color("535d6c"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 4, ContentMarginTop = 4, ContentMarginRight = 8, ContentMarginBottom = 4,
        });
        scroll.AddChild(_grid);
        AddChild(scroll);

        _selection = new Label { Text = "选择珠宝后可加工。", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_selection);
        var actions = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _reroll = CraftButton("混沌金重铸", P30JewelCraftOperation.RerollRare);
        _dissolve = CraftButton("消解银剥离", P30JewelCraftOperation.DissolveAffix);
        _corrupt = CraftButton("赤蚀铁腐化", P30JewelCraftOperation.Corrupt);
        _unsocket = new Button { Text = "从天赋取下", CustomMinimumSize = new Vector2(0, 30) };
        _unsocket.Pressed += UnsocketSelected;
        actions.AddChild(_reroll); actions.AddChild(_dissolve); actions.AddChild(_corrupt); actions.AddChild(_unsocket);
        AddChild(actions);
    }

    public void RefreshState(bool force = false)
    {
        if (_session is null || _grid is null) return;
        P1GameSession session = _session();
        string signature = string.Join('|', session.Jewels.Items.OrderBy(j => j.InstanceId)
            .Select(j => $"{j.InstanceId}:{j.Resonance}:{j.Corrupted}:{j.Locked}:" +
                         $"{session.Jewels.Socketed.Values.Contains(j.InstanceId)}:" +
                         string.Join(',', j.Affixes.Select(a => $"{a.StableId}:{a.Tier}:{a.Value}")))) +
            $"|wallet:{string.Join(',', new[] { MetalCurrencyKind.ChaosGold, MetalCurrencyKind.DissolutionSilver, MetalCurrencyKind.CorruptionIron }.Select(kind => session.World.Economy.MetalAmount(kind)))}";
        if (!force && signature == _signature) return;
        _signature = signature;
        foreach (Node child in _grid.GetChildren()) child.QueueFree();
        foreach (P30JewelInstance jewel in session.Jewels.Items.OrderByDescending(j => j.Rarity).ThenBy(j => j.DisplayName))
        {
            string? socket = session.Jewels.Socketed.FirstOrDefault(pair => pair.Value == jewel.InstanceId).Key;
            Color color = ColorFor(jewel);
            var cell = new P205JewelStashCell
            {
                InstanceId = jewel.InstanceId, JewelColor = color, Jewel = jewel, SocketId = socket,
                Text = socket is null ? Glyph(jewel) : "◆", CustomMinimumSize = new Vector2(32, 32),
                TooltipText = jewel.DisplayName,
                ToggleMode = true,
                ButtonPressed = jewel.InstanceId == _selectedInstanceId,
            };
            cell.Pressed += () => Select(jewel.InstanceId);
            cell.AddThemeColorOverride("font_color", color); _grid.AddChild(cell);
        }
        if (_selectedInstanceId is not null && session.Jewels.Items.All(item => item.InstanceId != _selectedInstanceId))
            _selectedInstanceId = null;
        RefreshSelection();
    }

    private Button CraftButton(string text, P30JewelCraftOperation operation)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 30) };
        button.Pressed += () => Craft(operation);
        return button;
    }

    private void Select(string instanceId)
    {
        _selectedInstanceId = _selectedInstanceId == instanceId ? null : instanceId;
        RefreshState(force: true);
    }

    private void Craft(P30JewelCraftOperation operation)
    {
        if (_session is null || _selectedInstanceId is null) return;
        bool changed = _session().TryCraftP30Jewel(_selectedInstanceId, operation, out string message);
        _changed?.Invoke(message);
        if (changed) RefreshState(force: true);
    }

    private void UnsocketSelected()
    {
        if (_session is null || _selectedInstanceId is null) return;
        P1GameSession session = _session();
        string? socket = session.Jewels.Socketed.FirstOrDefault(pair => pair.Value == _selectedInstanceId).Key;
        bool changed = socket is not null && session.TryUnsocketP30Jewel(socket);
        _changed?.Invoke(changed ? "珠宝已取回珠宝仓。" : "所选珠宝尚未镶嵌。 ");
        RefreshState(force: changed);
    }

    private void RefreshSelection()
    {
        if (_session is null || _selection is null || _reroll is null || _dissolve is null || _corrupt is null || _unsocket is null) return;
        P1GameSession session = _session();
        P30JewelInstance? jewel = session.Jewels.Items.FirstOrDefault(item => item.InstanceId == _selectedInstanceId);
        string? socket = jewel is null ? null : session.Jewels.Socketed.FirstOrDefault(pair => pair.Value == jewel.InstanceId).Key;
        _selection.Text = jewel is null
            ? "选择珠宝后可加工。"
            : $"已选：{jewel.DisplayName} · {P30Jewels.RarityName(jewel.Rarity)}" + (socket is null ? string.Empty : " · 已镶嵌");
        bool craftable = jewel is { Legendary: null, Corrupted: false, Locked: false };
        _reroll.Disabled = !craftable || jewel?.Rarity != P30JewelRarity.Rare;
        _dissolve.Disabled = !craftable || jewel?.Affixes.Any(affix =>
            affix.Position is P30JewelAffixPosition.Prefix or P30JewelAffixPosition.Suffix) != true;
        _corrupt.Disabled = !craftable || jewel?.Rarity != P30JewelRarity.Rare;
        _unsocket.Disabled = socket is null;
        _reroll.Text = $"混沌金重铸 ({session.World.Economy.MetalAmount(MetalCurrencyKind.ChaosGold)})";
        _dissolve.Text = $"消解银剥离 ({session.World.Economy.MetalAmount(MetalCurrencyKind.DissolutionSilver)})";
        _corrupt.Text = $"赤蚀铁腐化 ({session.World.Economy.MetalAmount(MetalCurrencyKind.CorruptionIron)})";
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
    public P30JewelInstance? Jewel { get; set; }
    public string? SocketId { get; set; }

    public override Control _MakeCustomTooltip(string forText)
    {
        if (Jewel is null) return new Label { Text = forText };
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("10141bee"), BorderColor = JewelColor.Darkened(.1f),
            BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 9, ContentMarginRight = 9, ContentMarginTop = 7, ContentMarginBottom = 7,
        });
        string affixes = string.Join('\n', Jewel.Affixes.Select(affix =>
            $"[color=#{P1UiText.AffixTierColor(affix.Tier).ToHtml(false)}]" +
            $"{P30Jewels.PositionName(affix.Position)} - {P30Jewels.AffixText(affix)}[/color]"));
        string legendary = Jewel.Legendary is null ? string.Empty :
            $"\n[color=#d4a2ed]{Jewel.Legendary.Effect}[/color]\n来源：{Jewel.Legendary.Source}";
        var text = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            CustomMinimumSize = new Vector2(330, Math.Max(50, (Jewel.Affixes.Count + 5) * 17)),
            Text = $"[color=#{JewelColor.ToHtml(false)}][font_size=15]{Jewel.DisplayName}[/font_size][/color]\n" +
                   $"{P30Jewels.RarityName(Jewel.Rarity)} · 物品等级 {Jewel.ItemLevel} · 共鸣度 {Jewel.Resonance}%" +
                   (affixes.Length == 0 ? string.Empty : "\n" + affixes) + legendary +
                   (SocketId is null ? "\n状态：珠宝仓中" : $"\n已镶嵌：{SocketId}"),
        };
        text.AddThemeConstantOverride("line_separation", -2);
        panel.AddChild(text);
        return panel;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (SocketId is not null) return default;
        var preview = new Label { Text = "◆", Position = new Vector2(-16, -16), CustomMinimumSize = new Vector2(32, 32),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        preview.AddThemeColorOverride("font_color", JewelColor); preview.AddThemeFontSizeOverride("font_size", 24);
        SetDragPreview(preview);
        return Variant.From($"p30-jewel|{InstanceId}");
    }
}

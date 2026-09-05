using GameForWork.Core.Campaign;
using GameForWork.Core.Spatial;
using GameForWork.Core.Builds;
using Godot;

namespace GameForWork.GodotClient;

public partial class JewelStashPanel : VBoxContainer
{
    private Func<GameSession>? _session;
    private Action<string>? _changed;
    private GridContainer? _grid;
    private Label? _selection;
    private Button? _reroll;
    private Button? _dissolve;
    private Button? _corrupt;
    private Button? _divine;
    private Button? _unsocket;
    private Button? _dismantleSelected;
    private Button? _dismantleBatch;
    private OptionButton? _cleanupRarity;
    private ConfirmationDialog? _confirmation;
    private Action? _pendingConfirmation;
    private string? _selectedInstanceId;
    private string _signature = string.Empty;

    public void Initialize(Func<GameSession> session, Action<string>? changed = null)
    {
        _session = session;
        _changed = changed;
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(new Label { Text = "珠宝仓 · 240 格 · 拖到已分配的记忆棱孔 · 同名珠宝按半径从大到小", AutowrapMode = TextServer.AutowrapMode.WordSmart });
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
        _reroll = CraftButton("混沌金重铸", JewelCraftOperation.RerollRare);
        _dissolve = CraftButton("消解银剥离", JewelCraftOperation.DissolveAffix);
        _corrupt = CraftButton("赤蚀铁腐化", JewelCraftOperation.Corrupt);
        _divine = CraftButton("神铸银重投半径", JewelCraftOperation.RerollLegendaryRadius);
        _unsocket = new Button { Text = "从天赋取下", CustomMinimumSize = new Vector2(0, 30) };
        _unsocket.Pressed += UnsocketSelected;
        _dismantleSelected = new Button { Text = "分解所选", CustomMinimumSize = new Vector2(0, 30) };
        _dismantleSelected.Pressed += DismantleSelected;
        _cleanupRarity = new OptionButton { CustomMinimumSize = new Vector2(0, 30) };
        _cleanupRarity.AddItem("清理普通", (int)JewelRarity.Normal);
        _cleanupRarity.AddItem("清理魔法及以下", (int)JewelRarity.Magic);
        _cleanupRarity.AddItem("清理稀有及以下", (int)JewelRarity.Rare);
        _cleanupRarity.AddItem("清理传奇及以下", (int)JewelRarity.Legendary);
        _cleanupRarity.Select((int)JewelRarity.Magic);
        _dismantleBatch = new Button { Text = "批量清理未镶嵌珠宝", CustomMinimumSize = new Vector2(0, 30) };
        _dismantleBatch.Pressed += ConfirmBatchDismantle;
        actions.AddChild(_reroll); actions.AddChild(_dissolve); actions.AddChild(_corrupt); actions.AddChild(_divine);
        actions.AddChild(_unsocket); actions.AddChild(_dismantleSelected);
        actions.AddChild(_cleanupRarity); actions.AddChild(_dismantleBatch);
        AddChild(actions);

        _confirmation = new ConfirmationDialog { Title = "确认分解珠宝", OkButtonText = "确认分解" };
        _confirmation.Confirmed += () =>
        {
            Action? action = _pendingConfirmation;
            _pendingConfirmation = null;
            action?.Invoke();
        };
        _confirmation.Canceled += () => _pendingConfirmation = null;
        AddChild(_confirmation);
    }

    public void RefreshState(bool force = false)
    {
        if (_session is null || _grid is null) return;
        GameSession session = _session();
        string signature = string.Join('|', session.Jewels.Items.OrderBy(j => j.InstanceId)
            .Select(j => $"{j.InstanceId}:{j.Resonance}:{j.Corrupted}:{j.Locked}:" +
                         $"{j.RolledRadius}:" +
                         $"{session.Jewels.Socketed.Values.Contains(j.InstanceId)}:" +
                         string.Join(',', j.Affixes.Select(a => $"{a.StableId}:{a.Tier}:{a.Value}")))) +
            $"|wallet:{string.Join(',', new[] { MetalCurrencyKind.ChaosGold, MetalCurrencyKind.DissolutionSilver, MetalCurrencyKind.CorruptionIron, MetalCurrencyKind.DivineSilver }.Select(kind => session.World.Economy.MetalAmount(kind)))}" +
            $"|scraps:{session.World.Economy.IronScraps}";
        if (!force && signature == _signature) return;
        _signature = signature;
        foreach (Node child in _grid.GetChildren()) child.QueueFree();
        foreach (JewelInstance jewel in JewelCatalog.OrderForStash(session.Jewels.Items))
        {
            string? socket = session.Jewels.Socketed.FirstOrDefault(pair => pair.Value == jewel.InstanceId).Key;
            Color color = ColorFor(jewel);
            var cell = new JewelStashCell
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

    private Button CraftButton(string text, JewelCraftOperation operation)
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

    private void Craft(JewelCraftOperation operation)
    {
        if (_session is null || _selectedInstanceId is null) return;
        bool changed = _session().TryCraftBuildsJewel(_selectedInstanceId, operation, out string message);
        _changed?.Invoke(message);
        if (changed) RefreshState(force: true);
    }

    private void UnsocketSelected()
    {
        if (_session is null || _selectedInstanceId is null) return;
        GameSession session = _session();
        string? socket = session.Jewels.Socketed.FirstOrDefault(pair => pair.Value == _selectedInstanceId).Key;
        bool changed = socket is not null && session.TryUnsocketBuildsJewel(socket);
        _changed?.Invoke(changed ? "珠宝已取回珠宝仓。" : "所选珠宝尚未镶嵌。 ");
        RefreshState(force: changed);
    }

    private void DismantleSelected()
    {
        if (_session is null || _selectedInstanceId is null) return;
        GameSession session = _session();
        JewelInstance? jewel = session.Jewels.Items.FirstOrDefault(item => item.InstanceId == _selectedInstanceId);
        if (jewel is null) return;
        if (session.Jewels.Socketed.Values.Contains(jewel.InstanceId, StringComparer.Ordinal))
        {
            _changed?.Invoke("已镶嵌的珠宝不能分解，请先从天赋树取下。");
            return;
        }
        if (jewel.Rarity >= JewelRarity.Rare)
        {
            Confirm($"确定分解 {jewel.DisplayName}？将获得 {JewelCatalog.DismantleYield(jewel.Rarity)} 铁屑。",
                () => ExecuteDismantleSelected(jewel.InstanceId, confirmed: true));
            return;
        }
        ExecuteDismantleSelected(jewel.InstanceId, confirmed: false);
    }

    private void ExecuteDismantleSelected(string instanceId, bool confirmed)
    {
        if (_session is null) return;
        bool changed = _session().TryDismantleBuildsJewel(instanceId, confirmed, out string message);
        _changed?.Invoke(message);
        if (!changed) return;
        _selectedInstanceId = null;
        RefreshState(force: true);
    }

    private void ConfirmBatchDismantle()
    {
        if (_session is null || _cleanupRarity is null) return;
        GameSession session = _session();
        JewelRarity maximum = (JewelRarity)_cleanupRarity.GetSelectedId();
        session.TryDismantleBuildsJewels(maximum, confirmed: false, out string preview);
        if (!session.Jewels.Items.Any(item => item.Rarity <= maximum &&
                !session.Jewels.Socketed.Values.Contains(item.InstanceId, StringComparer.Ordinal)))
        {
            _changed?.Invoke(preview);
            return;
        }
        Confirm(preview, () =>
        {
            bool changed = session.TryDismantleBuildsJewels(maximum, confirmed: true, out string message);
            _changed?.Invoke(message);
            if (changed) RefreshState(force: true);
        });
    }

    private void Confirm(string text, Action action)
    {
        if (_confirmation is null) return;
        _pendingConfirmation = action;
        _confirmation.DialogText = text;
        _confirmation.PopupCentered();
    }

    private void RefreshSelection()
    {
        if (_session is null || _selection is null || _reroll is null || _dissolve is null || _corrupt is null ||
            _divine is null || _unsocket is null || _dismantleSelected is null || _dismantleBatch is null) return;
        GameSession session = _session();
        JewelInstance? jewel = session.Jewels.Items.FirstOrDefault(item => item.InstanceId == _selectedInstanceId);
        string? socket = jewel is null ? null : session.Jewels.Socketed.FirstOrDefault(pair => pair.Value == jewel.InstanceId).Key;
        _selection.Text = jewel is null
            ? "选择珠宝后可加工。"
            : $"已选：{jewel.DisplayName} · {JewelCatalog.RarityName(jewel.Rarity)}" + (socket is null ? string.Empty : " · 已镶嵌");
        bool craftable = jewel is { Legendary: null, Corrupted: false, Locked: false };
        _reroll.Disabled = !craftable || jewel?.Rarity != JewelRarity.Rare;
        _dissolve.Disabled = !craftable || jewel?.Affixes.Any(affix =>
            affix.Position is JewelAffixPosition.Prefix or JewelAffixPosition.Suffix) != true;
        _corrupt.Disabled = !craftable || jewel?.Rarity != JewelRarity.Rare;
        _divine.Disabled = jewel?.Legendary is not { MinimumRadius: > 0 };
        _unsocket.Disabled = socket is null;
        _dismantleSelected.Disabled = jewel is null || socket is not null;
        _dismantleBatch.Disabled = session.Jewels.Items.All(item =>
            session.Jewels.Socketed.Values.Contains(item.InstanceId, StringComparer.Ordinal));
        _reroll.Text = $"混沌金重铸 ({session.World.Economy.MetalAmount(MetalCurrencyKind.ChaosGold)})";
        _dissolve.Text = $"消解银剥离 ({session.World.Economy.MetalAmount(MetalCurrencyKind.DissolutionSilver)})";
        _corrupt.Text = $"赤蚀铁腐化 ({session.World.Economy.MetalAmount(MetalCurrencyKind.CorruptionIron)})";
        _divine.Text = $"神铸银重投半径 ({session.World.Economy.MetalAmount(MetalCurrencyKind.DivineSilver)})";
    }

    private static string Glyph(JewelInstance jewel) => jewel.Legendary is not null ? "◆" : jewel.Base switch
    { JewelBase.Crimson => "赤", JewelBase.Verdant => "翠", JewelBase.Golden => "金", JewelBase.Azure => "苍", _ => "四" };
    private static Color ColorFor(JewelInstance jewel) => jewel.Legendary is not null ? new Color("c58be2") : jewel.Base switch
    { JewelBase.Crimson => new("d45f52"), JewelBase.Verdant => new("60b57a"), JewelBase.Golden => new("d6ad55"), JewelBase.Azure => new("5c9ed8"), _ => new("d7d2c7") };
}

public partial class JewelStashCell : Button
{
    public string InstanceId { get; set; } = string.Empty;
    public Color JewelColor { get; set; } = Colors.White;
    public JewelInstance? Jewel { get; set; }
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
            $"[color=#{UiText.AffixTierColor(affix.Tier).ToHtml(false)}]" +
            $"{JewelCatalog.PositionName(affix.Position)} - {JewelCatalog.AffixText(affix)}[/color]"));
        string legendary = Jewel.Legendary is null ? string.Empty :
            $"\n[color=#d4a2ed]{(Jewel.EffectiveRadius > 0 ? $"半径：{Jewel.EffectiveRadius}\n" : string.Empty)}" +
            $"{Jewel.Legendary.Effect}[/color]\n来源：{Jewel.Legendary.Source}";
        var text = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            CustomMinimumSize = new Vector2(330, Math.Max(50, (Jewel.Affixes.Count + 5) * 17)),
            Text = $"[color=#{JewelColor.ToHtml(false)}][font_size=15]{Jewel.DisplayName}[/font_size][/color]\n" +
                   $"{JewelCatalog.RarityName(Jewel.Rarity)} · 物品等级 {Jewel.ItemLevel} · 共鸣度 {Jewel.Resonance}%" +
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
        return Variant.From($"builds-jewel|{InstanceId}");
    }
}
